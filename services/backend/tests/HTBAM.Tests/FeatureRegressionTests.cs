using System.Security.Claims;
using System.Text;
using HTBAM.Api.Controllers;
using HTBAM.Application.Interfaces;
using HTBAM.Domain.Entities;
using HTBAM.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HTBAM.Tests;

public sealed class FeatureRegressionTests
{
    [Theory]
    [InlineData("", "Name", "DISTRACTED", 10)]
    [InlineData("RULE", "Name", "", 10)]
    [InlineData("RULE", "Name", "DISTRACTED", 0)]
    public async Task Alert_rule_rejects_missing_or_invalid_required_values(string code, string name, string behavior, int duration)
    {
        await using var db = CreateDb();
        var controller = WithAdmin(new AlertsController(db, new AuditSpy()));
        var request = new AlertsController.RuleReq(code, name, behavior, duration, .7m, .5m, true, "v1");

        var result = await controller.AddRule(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await db.AlertRulesSet.ToListAsync());
    }

    [Fact]
    public async Task Alert_rule_accepts_valid_values_and_trims_strings()
    {
        await using var db = CreateDb();
        var controller = WithAdmin(new AlertsController(db, new AuditSpy()));
        var request = new AlertsController.RuleReq(" rule-1 ", " Rule name ", " distracted ", 10, .7m, .5m, true, " v1 ");

        var result = await controller.AddRule(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var saved = await db.AlertRulesSet.SingleAsync();
        Assert.Equal("RULE-1", saved.Code);
        Assert.Equal("Rule name", saved.Name);
        Assert.Equal("DISTRACTED", saved.BehaviorLabel);
        Assert.Equal("v1", saved.Version);
    }

    [Fact]
    public async Task Alert_rule_rejects_every_missing_field_and_out_of_range_score()
    {
        await using var db = CreateDb();
        var controller = WithAdmin(new AlertsController(db, new AuditSpy()));
        AlertsController.RuleReq[] invalid =
        [
            new("RULE", " ", "DISTRACTED", 10, .7m, .5m, true, "v1"),
            new("RULE", "Name", "DISTRACTED", 10, null, .5m, true, "v1"),
            new("RULE", "Name", "DISTRACTED", 10, 1.01m, .5m, true, "v1"),
            new("RULE", "Name", "DISTRACTED", 10, .7m, null, true, "v1"),
            new("RULE", "Name", "DISTRACTED", 10, .7m, -.01m, true, "v1"),
            new("RULE", "Name", "DISTRACTED", 10, .7m, .5m, null, "v1"),
            new("RULE", "Name", "DISTRACTED", 10, .7m, .5m, true, " ")
        ];

        foreach (var request in invalid)
            Assert.IsType<BadRequestObjectResult>(await controller.AddRule(request, CancellationToken.None));

        Assert.Empty(await db.AlertRulesSet.ToListAsync());
    }

    [Fact]
    public async Task Alert_rule_update_accepts_valid_values()
    {
        await using var db = CreateDb();
        db.AlertRulesSet.Add(new AlertRule { Id = 1, Code = "OLD", Name = "Old", BehaviorLabel = "SLEEPY", MinDurationSeconds = 5, MinConfidence = .5m, MinObservationQuality = .5m, Enabled = true, Version = "v1" });
        await db.SaveChangesAsync();
        var controller = WithAdmin(new AlertsController(db, new AuditSpy()));

        var result = await controller.UpdateRule(1, new(" NEW ", " New name ", "PHONE_USE", 8, .8m, .6m, false, "v2"), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var saved = await db.AlertRulesSet.SingleAsync();
        Assert.Equal("NEW", saved.Code);
        Assert.False(saved.Enabled);
    }

    [Fact]
    public async Task Csv_with_utf8_bom_and_five_valid_rows_imports_all_and_maps_class_code()
    {
        await using var db = CreateDb();
        db.StudentClassesSet.Add(new StudentClass { Id = 10, Code = "12DHTH01", Name = "Lớp 12DHTH01", FacultyId = 1, IsActive = true });
        await db.SaveChangesAsync();
        var controller = WithAdmin(new StudentsController(db, new AuditSpy()));
        var csv = "\uFEFFStudentCode,FullName,Email,StudentClassCode,AnonymousCode\r\n" +
                  string.Join("\r\n", Enumerable.Range(1, 5).Select(i => $"SV{i:000},Nguyễn Văn {i},sv{i:000}@example.edu.vn,12DHTH01,"));

        var result = await controller.ImportCsv(File(csv), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var students = await db.StudentsSet.OrderBy(x => x.StudentCode).ToListAsync();
        Assert.Equal(5, students.Count);
        Assert.All(students, student => Assert.Equal(10, student.StudentClassId));
        Assert.All(students, student => Assert.StartsWith("ANON-", student.AnonymousCode));
        Assert.Contains(students, student => student.FullName == "Nguyễn Văn 1");
    }

    [Fact]
    public async Task Csv_duplicate_student_code_returns_conflict_without_partial_insert()
    {
        await using var db = CreateDb();
        db.StudentClassesSet.Add(new StudentClass { Id = 10, Code = "12DHTH01", Name = "Class", FacultyId = 1, IsActive = true });
        db.StudentsSet.Add(new Student { Id = 20, StudentCode = "SV001", FullName = "Existing", Email = "old@example.edu.vn", StudentClassId = 10, AnonymousCode = "ANON-OLD" });
        await db.SaveChangesAsync();
        var controller = WithAdmin(new StudentsController(db, new AuditSpy()));
        var csv = "StudentCode,FullName,Email,StudentClassCode,AnonymousCode\r\nSV001,Duplicate,new@example.edu.vn,12DHTH01,ANON-NEW\r\nSV002,Valid,sv002@example.edu.vn,12DHTH01,ANON-SV002";

        var result = await controller.ImportCsv(File(csv), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(await db.StudentsSet.ToListAsync());
    }

    [Fact]
    public async Task Csv_invalid_class_returns_line_error_without_partial_insert()
    {
        await using var db = CreateDb();
        db.StudentClassesSet.Add(new StudentClass { Id = 10, Code = "12DHTH01", Name = "Class", FacultyId = 1, IsActive = true });
        await db.SaveChangesAsync();
        var controller = WithAdmin(new StudentsController(db, new AuditSpy()));
        var csv = "StudentCode,FullName,Email,StudentClassCode,AnonymousCode\r\nSV001,Valid,sv001@example.edu.vn,12DHTH01,ANON-SV001\r\nSV002,Invalid,sv002@example.edu.vn,12DHTH99,ANON-SV002";

        var result = await controller.ImportCsv(File(csv), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await db.StudentsSet.ToListAsync());
    }

    [Fact]
    public async Task Video_preview_returns_presigned_url_only_when_object_exists()
    {
        await using var db = CreateDb();
        db.VideosSet.Add(new Video { Id = 1, FileName = "lesson.mp4", StorageRef = "videos/lesson.mp4", ContentType = "video/mp4", Status = "READY" });
        await db.SaveChangesAsync();
        var storage = new FakeStorage { Exists = true };
        var controller = WithAdmin(new VideosController(db, storage, new AuditSpy(), EmptyConfiguration(), NullLogger<VideosController>.Instance));

        var result = await controller.Preview(1, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("videos/lesson.mp4", storage.LastReadKey);
    }

    [Fact]
    public async Task Video_preview_returns_not_found_when_object_was_removed()
    {
        await using var db = CreateDb();
        db.VideosSet.Add(new Video { Id = 1, FileName = "missing.mp4", StorageRef = "videos/missing.mp4", ContentType = "video/mp4", Status = "READY" });
        await db.SaveChangesAsync();
        var controller = WithAdmin(new VideosController(db, new FakeStorage { Exists = false }, new AuditSpy(), EmptyConfiguration(), NullLogger<VideosController>.Instance));

        var result = await controller.Preview(1, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Catalog_deactivation_succeeds_without_dependencies_and_conflicts_when_referenced()
    {
        await using var db = CreateDb();
        db.StudentClassesSet.AddRange(
            new StudentClass { Id = 1, Code = "EMPTY", Name = "Empty", FacultyId = 1, IsActive = true },
            new StudentClass { Id = 2, Code = "USED", Name = "Used", FacultyId = 1, IsActive = true });
        db.StudentsSet.Add(new Student { Id = 10, StudentCode = "SV010", FullName = "Student", StudentClassId = 2, AnonymousCode = "ANON-10" });
        await db.SaveChangesAsync();
        var controller = WithAdmin(new CatalogsController(db, new AuditSpy()));

        var success = await controller.DeactivateCatalog("student-classes", 1, CancellationToken.None);
        var conflict = await controller.DeactivateCatalog("student-classes", 2, CancellationToken.None);

        Assert.IsType<NoContentResult>(success);
        Assert.False((await db.StudentClassesSet.FindAsync(1L))!.IsActive);
        Assert.IsType<ConflictObjectResult>(conflict);
        Assert.True((await db.StudentClassesSet.FindAsync(2L))!.IsActive);
    }

    [Fact]
    public void Catalog_deactivation_endpoint_is_admin_only()
    {
        var method = typeof(CatalogsController).GetMethod(nameof(CatalogsController.DeactivateCatalog));
        var authorize = Assert.Single(method!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("ADMIN", authorize.Roles);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
    private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().AddInMemoryCollection().Build();
    private static FormFile File(string csv)
    {
        var bytes = new UTF8Encoding(true).GetBytes(csv);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "students.csv") { Headers = new HeaderDictionary(), ContentType = "text/csv" };
    }
    private static T WithAdmin<T>(T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, "ADMIN")], "Test")) } };
        return controller;
    }

    private sealed class AuditSpy : IAuditService
    {
        public Task WriteAsync(long? userId, string action, string entityType, string entityId, object? metadata = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeStorage : IObjectStorage
    {
        public bool Exists { get; init; }
        public string? LastReadKey { get; private set; }
        public Task EnsureReadyAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task PutAsync(string objectKey, Stream stream, long sizeBytes, string contentType, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(string objectKey, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string> GetReadUrlAsync(string objectKey, int expirySeconds = 900, CancellationToken ct = default) { LastReadKey = objectKey; return Task.FromResult($"https://storage.test/{objectKey}"); }
        public Task<string> GetInternalReadUrlAsync(string objectKey, int expirySeconds = 900, CancellationToken ct = default) => GetReadUrlAsync(objectKey, expirySeconds, ct);
        public Task<string> GetInternalWriteUrlAsync(string objectKey, int expirySeconds = 900, CancellationToken ct = default) => Task.FromResult($"https://storage.test/{objectKey}");
        public Task<bool> ExistsAsync(string objectKey, CancellationToken ct = default) => Task.FromResult(Exists);
        public Task<bool> HealthAsync(CancellationToken ct = default) => Task.FromResult(true);
    }
}
