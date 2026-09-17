using System.Security.Cryptography;
using HTBAM.Application.DTOs;
using HTBAM.Application.Interfaces;
using HTBAM.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HTBAM.Application.Services;

public sealed class FaceEnrollmentService(IAppDbContext db, IObjectStorage storage) : IFaceEnrollmentService
{
    private const long MaxImageBytes = 8 * 1024 * 1024;
    private static readonly HashSet<string> AllowedPoses = new(StringComparer.OrdinalIgnoreCase) { "FRONT", "LEFT", "RIGHT", "UP", "DOWN" };
    private static readonly HashSet<string> AllowedSources = new(StringComparer.OrdinalIgnoreCase) { "UPLOAD", "CAMERA" };

    public async Task<FaceEnrollment> StartAsync(long studentId, CancellationToken ct)
    {
        if (!await db.Students.AnyAsync(x => x.Id == studentId && x.IsActive, ct))
            throw new KeyNotFoundException("Sinh viên không tồn tại hoặc đã ngừng hoạt động.");

        var inProgress = await db.FaceEnrollments.FirstOrDefaultAsync(x => x.StudentId == studentId && (x.Status == "IN_PROGRESS" || x.Status == "PENDING_AI" || x.Status == "PROCESSING"), ct);
        if (inProgress is not null)
            throw new InvalidOperationException($"Sinh viên đang có Face Enrollment #{inProgress.Id} ở trạng thái {inProgress.Status}.");

        var enrollment = new FaceEnrollment { StudentId = studentId, Status = "IN_PROGRESS", RequiredPoses = "FRONT,LEFT,RIGHT", MinAcceptedImages = 6 };
        await db.AddAsync(enrollment, ct);
        await db.SaveChangesAsync(ct);
        return enrollment;
    }

    public async Task<StudentFaceImage> UploadImageAsync(long studentId, long enrollmentId, string originalFileName, string contentType, long sizeBytes, string capturePose, string sourceType, Stream stream, CancellationToken ct)
    {
        var enrollment = await GetOwnedEnrollment(studentId, enrollmentId, ct);
        if (enrollment.Status is not ("IN_PROGRESS" or "NEEDS_RETAKE"))
            throw new InvalidOperationException("Chỉ được thêm ảnh khi enrollment đang IN_PROGRESS hoặc NEEDS_RETAKE.");

        capturePose = capturePose.Trim().ToUpperInvariant();
        sourceType = sourceType.Trim().ToUpperInvariant();
        if (!AllowedPoses.Contains(capturePose)) throw new InvalidOperationException("Pose không hợp lệ: FRONT/LEFT/RIGHT/UP/DOWN.");
        if (!AllowedSources.Contains(sourceType)) throw new InvalidOperationException("SourceType không hợp lệ: UPLOAD/CAMERA.");
        if (sizeBytes <= 0 || sizeBytes > MaxImageBytes) throw new InvalidOperationException("Ảnh phải có dung lượng > 0 và <= 8 MB.");

        using var ms = new MemoryStream((int)Math.Min(sizeBytes, MaxImageBytes));
        await stream.CopyToAsync(ms, ct);
        if (ms.Length != sizeBytes) sizeBytes = ms.Length;
        var bytes = ms.ToArray();
        var detectedContentType = DetectImageType(bytes);
        if (detectedContentType is null) throw new InvalidOperationException("File không phải JPEG/PNG/WebP hợp lệ theo chữ ký file.");
        if (!string.IsNullOrWhiteSpace(contentType) && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Content-Type không hợp lệ.");

        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (await db.StudentFaceImages.AnyAsync(x => x.StudentId == studentId && x.Sha256 == sha && x.IsActive, ct))
            throw new InvalidOperationException("Ảnh này đã được đăng ký trước đó cho sinh viên.");

        var ext = detectedContentType switch { "image/jpeg" => ".jpg", "image/png" => ".png", "image/webp" => ".webp", _ => ".img" };
        var objectKey = $"face-enrollment/{studentId}/{enrollmentId}/{Guid.NewGuid():N}{ext}";
        ms.Position = 0;
        await storage.PutAsync(objectKey, ms, ms.Length, detectedContentType, ct);

        var image = new StudentFaceImage
        {
            FaceEnrollmentId = enrollmentId,
            StudentId = studentId,
            ObjectKey = objectKey,
            OriginalFileName = Path.GetFileName(originalFileName),
            ContentType = detectedContentType,
            SizeBytes = ms.Length,
            Sha256 = sha,
            CapturePose = capturePose,
            SourceType = sourceType,
            QualityStatus = "PENDING",
            IsActive = true
        };
        try
        {
            await db.AddAsync(image, ct);
            if (enrollment.Status == "NEEDS_RETAKE") { enrollment.Status = "IN_PROGRESS"; enrollment.FailureReason = null; db.Update(enrollment); }
            await db.SaveChangesAsync(ct);
            return image;
        }
        catch
        {
            await storage.DeleteAsync(objectKey, ct);
            throw;
        }
    }

    public async Task<FaceEnrollment> SubmitAsync(long studentId, long enrollmentId, CancellationToken ct)
    {
        var enrollment = await GetOwnedEnrollment(studentId, enrollmentId, ct);
        if (enrollment.Status is not ("IN_PROGRESS" or "NEEDS_RETAKE"))
            throw new InvalidOperationException("Enrollment không ở trạng thái cho phép submit.");

        var images = await db.StudentFaceImages.Where(x => x.FaceEnrollmentId == enrollmentId && x.IsActive).ToListAsync(ct);
        if (images.Count < enrollment.MinAcceptedImages)
            throw new InvalidOperationException($"Cần tối thiểu {enrollment.MinAcceptedImages} ảnh trước khi gửi AI. Hiện có {images.Count}.");

        var required = enrollment.RequiredPoses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToUpperInvariant()).ToHashSet();
        var captured = images.Select(x => x.CapturePose.ToUpperInvariant()).ToHashSet();
        var missing = required.Except(captured).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException($"Thiếu góc bắt buộc: {string.Join(", ", missing)}.");

        enrollment.Status = "PENDING_AI";
        enrollment.SubmittedAt = DateTime.UtcNow;
        enrollment.FailureReason = null;
        db.Update(enrollment);
        await db.SaveChangesAsync(ct);
        return enrollment;
    }

    public async Task CancelAsync(long studentId, long enrollmentId, CancellationToken ct)
    {
        var enrollment = await GetOwnedEnrollment(studentId, enrollmentId, ct);
        if (enrollment.Status is not ("IN_PROGRESS" or "NEEDS_RETAKE" or "PENDING_AI"))
            throw new InvalidOperationException("Chỉ được hủy enrollment trước khi AI bắt đầu PROCESSING. Enrollment PROCESSING phải hoàn tất hoặc xử lý lỗi trước để tránh race callback/template.");
        var images = await db.StudentFaceImages.Where(x => x.FaceEnrollmentId == enrollmentId && x.IsActive).ToListAsync(ct);
        foreach (var image in images) { image.IsActive = false; db.Update(image); }
        enrollment.Status = "CANCELLED";
        enrollment.CompletedAt = DateTime.UtcNow;
        db.Update(enrollment);
        await db.SaveChangesAsync(ct);
        foreach (var image in images) await storage.DeleteAsync(image.ObjectKey, ct);
    }

    public async Task DeleteImageAsync(long studentId, long enrollmentId, long imageId, CancellationToken ct)
    {
        var enrollment = await GetOwnedEnrollment(studentId, enrollmentId, ct);
        if (enrollment.Status is not ("IN_PROGRESS" or "NEEDS_RETAKE")) throw new InvalidOperationException("Không thể xóa ảnh sau khi đã gửi AI.");
        var image = await db.StudentFaceImages.FirstOrDefaultAsync(x => x.Id == imageId && x.FaceEnrollmentId == enrollmentId && x.StudentId == studentId && x.IsActive, ct) ?? throw new KeyNotFoundException("Ảnh không tồn tại.");
        image.IsActive = false;
        db.Update(image);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(image.ObjectKey, ct);
    }

    public async Task<FaceEnrollmentManifest> BuildManifestAsync(long studentId, long enrollmentId, string callbackUrl, string callbackApiKey, CancellationToken ct)
    {
        var enrollment = await GetOwnedEnrollment(studentId, enrollmentId, ct);
        if (enrollment.Status != "PENDING_AI") throw new InvalidOperationException("Enrollment chưa ở trạng thái PENDING_AI.");
        var student = await db.Students.FirstAsync(x => x.Id == studentId, ct);
        var images = await db.StudentFaceImages.Where(x => x.FaceEnrollmentId == enrollmentId && x.IsActive).OrderBy(x => x.Id).ToListAsync(ct);
        var manifestImages = new List<FaceEnrollmentManifestImage>();
        foreach (var image in images)
            manifestImages.Add(new FaceEnrollmentManifestImage(image.Id, image.CapturePose, image.ContentType, await storage.GetInternalReadUrlAsync(image.ObjectKey, 900, ct)));
        var templateObjectKey = $"face-templates/{studentId}/{enrollmentId}/arcface.npy";
        var templateUploadUrl = await storage.GetInternalWriteUrlAsync(templateObjectKey, 900, ct);
        return new FaceEnrollmentManifest(enrollmentId, studentId, student.StudentCode, manifestImages, templateObjectKey, templateUploadUrl, callbackUrl, callbackApiKey);
    }

    public async Task ApplyAiResultAsync(long enrollmentId, FaceEnrollmentAiResult result, CancellationToken ct)
    {
        var enrollment = await db.FaceEnrollments.FirstOrDefaultAsync(x => x.Id == enrollmentId, ct) ?? throw new KeyNotFoundException("Face Enrollment không tồn tại.");
        // Callback AI có thể retry sau khi nhận timeout; READY + READY được coi là idempotent.
        if (enrollment.Status == "READY" && result.Status.Equals("READY", StringComparison.OrdinalIgnoreCase)) return;
        if (enrollment.Status is not ("PENDING_AI" or "PROCESSING")) throw new InvalidOperationException($"Không nhận callback khi enrollment đang {enrollment.Status}.");

        var images = await db.StudentFaceImages.Where(x => x.FaceEnrollmentId == enrollmentId && x.IsActive).ToListAsync(ct);
        foreach (var r in result.Images)
        {
            var image = images.FirstOrDefault(x => x.Id == r.ImageId);
            if (image is null) continue;
            image.QualityScore = Math.Clamp(r.QualityScore, 0, 1);
            image.QualityStatus = NormalizeQualityStatus(r.QualityStatus);
            image.QualityReason = r.QualityReason;
            image.ProcessedAt = DateTime.UtcNow;
            db.Update(image);
        }

        var passed = images.Count(x => x.QualityStatus == "PASS");
        enrollment.AcceptedImageCount = passed;
        var required = enrollment.RequiredPoses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToUpperInvariant()).ToHashSet();
        var passedPoses = images.Where(x => x.QualityStatus == "PASS").Select(x => x.CapturePose.ToUpperInvariant()).ToHashSet();
        var qualityReady = passed >= enrollment.MinAcceptedImages && required.All(passedPoses.Contains);
        var aiReady = result.Status.Equals("READY", StringComparison.OrdinalIgnoreCase);

        var expectedTemplateRef = $"face-templates/{enrollment.StudentId}/{enrollment.Id}/arcface.npy";
        var templateRefValid = string.Equals(result.TemplateRef, expectedTemplateRef, StringComparison.Ordinal);
        var templateStored = templateRefValid && await storage.ExistsAsync(expectedTemplateRef, ct);
        if (aiReady && qualityReady && templateRefValid && templateStored && result.QualityScore is not null)
        {
            var oldTemplates = await db.StudentFaceTemplates.Where(x => x.StudentId == enrollment.StudentId && x.IsActive).ToListAsync(ct);
            foreach (var old in oldTemplates) { old.IsActive = false; db.Update(old); }
            await db.AddAsync(new StudentFaceTemplate
            {
                StudentId = enrollment.StudentId,
                FaceEnrollmentId = enrollment.Id,
                TemplateRef = expectedTemplateRef,
                QualityScore = Math.Clamp(result.QualityScore.Value, 0, 1),
                PoseCoverage = result.PoseCoverage ?? string.Join(',', passedPoses),
                ModelName = result.ModelName ?? "ArcFace",
                TemplateVersion = result.TemplateVersion ?? "unknown",
                EmbeddingDimension = result.EmbeddingDimension ?? 512,
                SourceImageCount = passed,
                QualityStatus = "PASS",
                IsActive = true
            }, ct);
            enrollment.Status = "READY";
            enrollment.CompletedAt = DateTime.UtcNow;
            enrollment.FailureReason = null;
        }
        else
        {
            enrollment.Status = result.Status.Equals("FAILED", StringComparison.OrdinalIgnoreCase) ? "FAILED" : "NEEDS_RETAKE";
            enrollment.FailureReason = result.FailureReason ?? (!qualityReady
                ? "Chưa đủ ảnh PASS hoặc độ phủ pose sau quality assessment."
                : !templateRefValid ? "AI callback trả TemplateRef không hợp lệ."
                : !templateStored ? "AI chưa upload template vào Object Storage trước callback READY."
                : "AI không tạo được template hợp lệ.");
            if (enrollment.Status == "FAILED") enrollment.CompletedAt = DateTime.UtcNow;
        }
        db.Update(enrollment);
        await db.SaveChangesAsync(ct);
    }

    private async Task<FaceEnrollment> GetOwnedEnrollment(long studentId, long enrollmentId, CancellationToken ct) =>
        await db.FaceEnrollments.FirstOrDefaultAsync(x => x.Id == enrollmentId && x.StudentId == studentId, ct) ?? throw new KeyNotFoundException("Face Enrollment không tồn tại.");

    private static string NormalizeQualityStatus(string value)
    {
        var v = value.Trim().ToUpperInvariant();
        return v is "PASS" or "RETAKE" or "REJECTED" ? v : "REJECTED";
    }

    private static string? DetectImageType(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return "image/png";
        if (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50) return "image/webp";
        return null;
    }
}
