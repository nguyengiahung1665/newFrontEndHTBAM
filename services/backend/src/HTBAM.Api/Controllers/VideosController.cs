using System.Security.Cryptography;
using HTBAM.Api.Support;
using HTBAM.Application.Interfaces;
using HTBAM.Domain.Entities;
using HTBAM.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HTBAM.Api.Controllers;

[ApiController, Authorize(Roles = "LECTURER,TECH_AI"), Route("api/videos")]
public sealed class VideosController(AppDbContext db, IObjectStorage storage, IAuditService audit, IConfiguration cfg, ILogger<VideosController> logger) : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mov", ".avi", ".mkv", ".webm" };

    [HttpGet]
    public async Task<ActionResult> List(CancellationToken ct)
    {
        var teacherId = await AccessScope.TeacherIdAsync(db, User, ct);
        var query = AccessScope.Videos(db, User, teacherId).AsNoTracking().Where(x => x.Status != "DELETED");
        return Ok(await query.OrderByDescending(x => x.UploadedAt).Select(x => new
        {
            x.Id,
            x.FileName,
            x.ContentType,
            x.SizeBytes,
            x.Status,
            x.VideoType,
            x.SessionId,
            SessionCode = x.SessionId == null ? null : db.SessionsSet.Where(s => s.Id == x.SessionId).Select(s => s.ClassSection.Code).FirstOrDefault(),
            CourseName = x.SessionId == null ? null : db.SessionsSet.Where(s => s.Id == x.SessionId).Select(s => s.ClassSection.Course.Name).FirstOrDefault(),
            SourceType = x.VideoType == "INPUT_UPLOAD" ? "UPLOAD" : (x.ParentVideoId == null ? "CAMERA" : "VIDEO"),
            x.ParentVideoId,
            ParentVideoName = x.ParentVideoId == null ? null : db.VideosSet.Where(v => v.Id == x.ParentVideoId).Select(v => v.FileName).FirstOrDefault(),
            x.IsSystemGenerated,
            x.UploadedAt,
            x.UploadedByUserId
        }).ToListAsync(ct));
    }

    [HttpPost]
    [RequestFormLimits(MultipartBodyLengthLimit = 2L * 1024 * 1024 * 1024)]
    [RequestSizeLimit(2L * 1024 * 1024 * 1024)]
    public async Task<ActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length <= 0) return BadRequest("Thiếu video.");
        var maxBytes = cfg.GetValue<long>("Uploads:MaxVideoBytes", 2L * 1024 * 1024 * 1024);
        if (file.Length > maxBytes) return BadRequest($"Video vượt giới hạn {maxBytes} bytes.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext)) return BadRequest("Định dạng video không được hỗ trợ.");
        if (!string.IsNullOrWhiteSpace(file.ContentType) && !file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) && ext != ".mkv")
            return BadRequest("Loại nội dung của video không hợp lệ.");

        var temp = Path.GetTempFileName();
        try
        {
            await using (var output = System.IO.File.Create(temp)) await file.CopyToAsync(output, ct);
            string sha;
            await using (var input = System.IO.File.OpenRead(temp)) sha = Convert.ToHexString(await SHA256.HashDataAsync(input, ct)).ToLowerInvariant();
            if (await db.VideosSet.AnyAsync(x => x.Sha256 == sha && x.Status != "DELETED", ct))
                return Conflict(ApiValidation.Duplicate("VIDEO_CONTENT_DUPLICATE", "file", "Video này trùng nội dung với một video đã được tải lên trước đó."));

            var key = $"videos/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{ext}";
            await using (var input = System.IO.File.OpenRead(temp)) await storage.PutAsync(key, input, input.Length, string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType, ct);
            var video = new Video { FileName = Path.GetFileName(file.FileName), StorageRef = key, ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType, SizeBytes = file.Length, Sha256 = sha, Status = "READY", VideoType = "INPUT_UPLOAD", IsSystemGenerated = false, UploadedByUserId = UserContext.Id(User) };
            db.VideosSet.Add(video); await db.SaveChangesAsync(ct);
            await audit.WriteAsync(UserContext.Id(User), "VIDEO_UPLOAD", "Video", video.Id.ToString(), new { video.FileName, video.SizeBytes }, ct);
            return Ok(new { video.Id, video.FileName, video.ContentType, video.SizeBytes, video.Status, video.UploadedAt });
        }
        finally { try { System.IO.File.Delete(temp); } catch { } }
    }

    [HttpGet("{id:long}/preview-url")]
    public async Task<ActionResult> Preview(long id, CancellationToken ct)
    {
        var video = await db.VideosSet.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Status != "DELETED", ct);
        if (video is null) return NotFound(new { message = "Video không tồn tại." });
        if (!await AccessScope.CanViewVideoAsync(db, User, id, ct)) return Forbid();
        if (video.Status != "READY") return Conflict(new { message = "Video chưa sẵn sàng để xem trước." });
        if (string.IsNullOrWhiteSpace(video.StorageRef)) return NotFound(new { message = "Video không có tệp trong kho lưu trữ." });
        try
        {
            if (!await storage.ExistsAsync(video.StorageRef, ct))
                return NotFound(new { message = $"Tệp '{video.FileName}' không còn tồn tại trong kho lưu trữ." });
            var url = await storage.GetReadUrlAsync(video.StorageRef, 900, ct);
            return Ok(new { url, video.ContentType, video.FileName, expiresInSeconds = 900 });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cannot create preview URL for video {VideoId}", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Không thể truy cập kho video lúc này. Vui lòng thử lại sau." });
        }
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id, CancellationToken ct)
    {
        var video = await db.VideosSet.FirstOrDefaultAsync(x => x.Id == id && x.Status != "DELETED", ct);
        if (video is null) return NotFound();
        if (!await AccessScope.CanDeleteVideoAsync(db, User, id, ct)) return Forbid();
        var used = await db.SessionsSet.AnyAsync(x => x.VideoId == id && x.Status != "COMPLETED" && x.Status != "CANCELLED", ct);
        if (used) return Conflict("Video đang được dùng bởi buổi học chưa kết thúc.");
        if (await db.VideosSet.AnyAsync(x => x.ParentVideoId == id && x.Status != "DELETED", ct))
            return Conflict("Video là nguồn của một video chú thích và không thể xóa.");
        await storage.DeleteAsync(video.StorageRef, ct);
        video.Status = "DELETED"; await db.SaveChangesAsync(ct);
        await audit.WriteAsync(UserContext.Id(User), "VIDEO_DELETE", "Video", id.ToString(), new { video.FileName }, ct);
        return NoContent();
    }
}
