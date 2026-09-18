using System.Security.Claims;
using System.Text;
using HTBAM.Api.Controllers;
using HTBAM.Api.Support;
using HTBAM.Application.DTOs;
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
        await SeedFacultyHead(db);
        db.StudentClassesSet.Add(new StudentClass { Id = 10, Code = "12DHTH01", Name = "Lớp 12DHTH01", FacultyId = 1, IsActive = true });
        await db.SaveChangesAsync();
        var controller = WithUser(new StudentsController(db, new AuditSpy()), 100, "LECTURER");
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
        await SeedFacultyHead(db);
        db.StudentClassesSet.Add(new StudentClass { Id = 10, Code = "12DHTH01", Name = "Class", FacultyId = 1, IsActive = true });
        db.StudentsSet.Add(new Student { Id = 20, StudentCode = "SV001", FullName = "Existing", Email = "old@example.edu.vn", StudentClassId = 10, AnonymousCode = "ANON-OLD" });
        await db.SaveChangesAsync();
        var controller = WithUser(new StudentsController(db, new AuditSpy()), 100, "LECTURER");
        var csv = "StudentCode,FullName,Email,StudentClassCode,AnonymousCode\r\nSV001,Duplicate,new@example.edu.vn,12DHTH01,ANON-NEW\r\nSV002,Valid,sv002@example.edu.vn,12DHTH01,ANON-SV002";

        var result = await controller.ImportCsv(File(csv), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(await db.StudentsSet.ToListAsync());
    }

    [Fact]
    public async Task Csv_invalid_class_returns_line_error_without_partial_insert()
    {
        await using var db = CreateDb();
        await SeedFacultyHead(db);
        db.StudentClassesSet.Add(new StudentClass { Id = 10, Code = "12DHTH01", Name = "Class", FacultyId = 1, IsActive = true });
        await db.SaveChangesAsync();
        var controller = WithUser(new StudentsController(db, new AuditSpy()), 100, "LECTURER");
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
        await SeedFacultyHead(db);
        db.StudentClassesSet.AddRange(
            new StudentClass { Id = 1, Code = "EMPTY", Name = "Empty", FacultyId = 1, IsActive = true },
            new StudentClass { Id = 2, Code = "USED", Name = "Used", FacultyId = 1, IsActive = true });
        db.StudentsSet.Add(new Student { Id = 10, StudentCode = "SV010", FullName = "Student", StudentClassId = 2, AnonymousCode = "ANON-10" });
        await db.SaveChangesAsync();
        var controller = WithUser(new CatalogsController(db, new AuditSpy()), 100, "LECTURER");

        var success = await controller.DeactivateCatalog("student-classes", 1, CancellationToken.None);
        var conflict = await controller.DeactivateCatalog("student-classes", 2, CancellationToken.None);

        Assert.IsType<NoContentResult>(success);
        Assert.False((await db.StudentClassesSet.FindAsync(1L))!.IsActive);
        Assert.IsType<ConflictObjectResult>(conflict);
        Assert.True((await db.StudentClassesSet.FindAsync(2L))!.IsActive);
    }

    [Fact]
    public async Task Admin_cannot_deactivate_academic_catalog()
    {
        await using var db = CreateDb();
        db.FacultiesSet.Add(new Faculty { Id = 1, Code = "F", Name = "Faculty" });
        db.StudentClassesSet.Add(new StudentClass { Id = 1, Code = "C", Name = "Class", FacultyId = 1 });
        await db.SaveChangesAsync();
        var controller = WithAdmin(new CatalogsController(db, new AuditSpy()));

        Assert.IsType<ForbidResult>(await controller.DeactivateCatalog("student-classes", 1, CancellationToken.None));
    }

    [Fact]
    public async Task Admin_does_not_bypass_session_business_scope()
    {
        await using var db = CreateDb();
        var ids = await SeedScopeFixture(db);
        var admin = Principal(90, "ADMIN");

        Assert.False(await HTBAM.Api.Support.AccessScope.CanViewSessionAsync(db, admin, ids.SessionA, CancellationToken.None));
        Assert.False(await HTBAM.Api.Support.AccessScope.CanOperateSessionAsync(db, admin, ids.SessionA, CancellationToken.None));
    }

    [Fact]
    public async Task Lecturer_sees_own_session_but_not_other_lecturer_session()
    {
        await using var db = CreateDb();
        var ids = await SeedScopeFixture(db);

        Assert.True(await HTBAM.Api.Support.AccessScope.CanViewSessionAsync(db, Principal(ids.UserA, "LECTURER"), ids.SessionA, CancellationToken.None));
        Assert.False(await HTBAM.Api.Support.AccessScope.CanViewSessionAsync(db, Principal(ids.UserA, "LECTURER"), ids.SessionB, CancellationToken.None));
    }

    [Fact]
    public async Task Substitute_operates_active_substitution_and_original_only_views_reports()
    {
        await using var db = CreateDb();
        var ids = await SeedScopeFixture(db);
        db.SessionSubstitutionsSet.Add(new SessionSubstitution { SessionId = ids.SessionA, OriginalTeacherId = ids.TeacherA, SubstituteTeacherId = ids.TeacherB, Reason = "Busy", Status = "ACTIVE" });
        await db.SaveChangesAsync();

        Assert.True(await HTBAM.Api.Support.AccessScope.CanViewSessionAsync(db, Principal(ids.UserA, "LECTURER"), ids.SessionA, CancellationToken.None));
        Assert.False(await HTBAM.Api.Support.AccessScope.CanOperateSessionAsync(db, Principal(ids.UserA, "LECTURER"), ids.SessionA, CancellationToken.None));
        Assert.True(await HTBAM.Api.Support.AccessScope.CanViewSessionAsync(db, Principal(ids.UserB, "LECTURER"), ids.SessionA, CancellationToken.None));
        Assert.True(await HTBAM.Api.Support.AccessScope.CanOperateSessionAsync(db, Principal(ids.UserB, "LECTURER"), ids.SessionA, CancellationToken.None));
    }

    [Fact]
    public async Task Substitute_can_view_session_student_but_cannot_edit_student_record()
    {
        await using var db = CreateDb();
        var ids = await SeedScopeFixture(db);
        db.StudentClassesSet.Add(new StudentClass { Id = 701, Code = "SC1", Name = "Student class", FacultyId = ids.Faculty1, IsActive = true });
        db.StudentsSet.Add(new Student { Id = 702, StudentCode = "SV702", FullName = "Student", Email = "student@example.test", StudentClassId = 701, AnonymousCode = "ANON-702", IsActive = true });
        db.SessionStudentsSet.Add(new SessionStudent { SessionId = ids.SessionA, StudentId = 702 });
        db.SessionSubstitutionsSet.Add(new SessionSubstitution { SessionId = ids.SessionA, OriginalTeacherId = ids.TeacherA, SubstituteTeacherId = ids.TeacherB, Reason = "Busy", Status = "ACTIVE" });
        await db.SaveChangesAsync();

        var substitute = Principal(ids.UserB, "LECTURER");
        Assert.True(await HTBAM.Api.Support.AccessScope.CanViewStudentAsync(db, substitute, 702, CancellationToken.None));
        Assert.False(await HTBAM.Api.Support.AccessScope.CanManageStudentAsync(db, substitute, 702, CancellationToken.None));
    }

    [Fact]
    public async Task Admin_cannot_create_business_student()
    {
        await using var db = CreateDb();
        db.FacultiesSet.Add(new Faculty { Id = 1, Code = "F", Name = "Faculty" });
        db.StudentClassesSet.Add(new StudentClass { Id = 1, Code = "SC", Name = "Class", FacultyId = 1, IsActive = true });
        await db.SaveChangesAsync();
        var controller = WithAdmin(new StudentsController(db, new AuditSpy()));
        var request = new UpsertStudentRequest("SV1", "Student", "student@example.test", 1, "ANON-1", true);

        Assert.IsType<ForbidResult>(await controller.Create(request, CancellationToken.None));
    }

    [Fact]
    public async Task Admin_cannot_create_session_read_report_or_replace_roster()
    {
        await using var db = CreateDb();
        var ids = await SeedScopeFixture(db);
        var sessionRequest = new CreateSessionRequest(401, null, 1, null, 501, DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(5).AddHours(2), [], "DEFAULT");
        var sessions = WithAdmin(new SessionsController(db, null!, EmptyConfiguration(), new AuditSpy(), null!));
        var reports = WithAdmin(new ReportsController(db, null!));
        var catalogs = WithAdmin(new CatalogsController(db, new AuditSpy()));

        Assert.IsType<ForbidResult>(await sessions.Create(sessionRequest, CancellationToken.None));
        Assert.IsType<ForbidResult>(await reports.Session(ids.SessionA, CancellationToken.None));
        Assert.IsType<ForbidResult>(await catalogs.SetRoster(401, new CatalogsController.RosterReq([]), CancellationToken.None));
    }

    [Fact]
    public async Task Lecturer_cannot_modify_student_outside_own_scope()
    {
        await using var db = CreateDb();
        var ids = await SeedScopeFixture(db);
        db.StudentClassesSet.Add(new StudentClass { Id = 701, Code = "SC1", Name = "Student class", FacultyId = ids.Faculty2, IsActive = true });
        db.StudentsSet.Add(new Student { Id = 702, StudentCode = "SV702", FullName = "Student", Email = "student@example.test", StudentClassId = 701, AnonymousCode = "ANON-702", IsActive = true });
        db.Enrollments.Add(new Enrollment { ClassSectionId = 403, StudentId = 702 });
        await db.SaveChangesAsync();
        var controller = WithUser(new StudentsController(db, new AuditSpy()), ids.UserA, "LECTURER");
        var request = new UpsertStudentRequest("SV702", "Changed", "student@example.test", 701, "ANON-702", true);

        Assert.IsType<ForbidResult>(await controller.Update(702, request, CancellationToken.None));
    }

    [Fact]
    public async Task Substitution_rejects_second_active_assignment()
    {
        await using var db = CreateDb();
        var ids = await SeedScopeFixture(db);
        db.ManagementAssignmentsSet.Add(new ManagementAssignment { TeacherId = ids.HeadTeacher, PositionType = "DEPARTMENT_HEAD", DepartmentId = ids.Department1, IsActive = true });
        db.SessionSubstitutionsSet.Add(new SessionSubstitution { SessionId = ids.SessionA, OriginalTeacherId = ids.TeacherA, SubstituteTeacherId = ids.TeacherB, Reason = "Existing", Status = "ACTIVE" });
        await db.SaveChangesAsync();
        var controller = WithUser(new SessionsController(db, null!, EmptyConfiguration(), new AuditSpy(), null!), ids.HeadUser, "LECTURER");

        Assert.IsType<ConflictObjectResult>(await controller.AssignSubstitution(ids.SessionA, new(ids.TeacherB, "Again", null), CancellationToken.None));
    }

    [Fact]
    public async Task Substitution_rejects_inactive_teacher_and_inactive_account()
    {
        await using var db = CreateDb();
        var ids = await SeedScopeFixture(db);
        db.ManagementAssignmentsSet.Add(new ManagementAssignment { TeacherId = ids.HeadTeacher, PositionType = "DEPARTMENT_HEAD", DepartmentId = ids.Department1, IsActive = true });
        await db.SaveChangesAsync();
        var controller = WithUser(new SessionsController(db, null!, EmptyConfiguration(), new AuditSpy(), null!), ids.HeadUser, "LECTURER");

        (await db.Teachers.FindAsync(ids.TeacherB))!.IsActive = false;
        await db.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await controller.AssignSubstitution(ids.SessionA, new(ids.TeacherB, "Inactive teacher", null), CancellationToken.None));

        (await db.Teachers.FindAsync(ids.TeacherB))!.IsActive = true;
        (await db.UsersSet.FindAsync(ids.UserB))!.Status = "LOCKED";
        await db.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await controller.AssignSubstitution(ids.SessionA, new(ids.TeacherB, "Inactive account", null), CancellationToken.None));
    }

    [Fact]
    public async Task Substitution_rejects_teacher_schedule_conflict()
    {
        await using var db = CreateDb();
        var ids = await SeedScopeFixture(db);
        db.ManagementAssignmentsSet.Add(new ManagementAssignment { TeacherId = ids.HeadTeacher, PositionType = "DEPARTMENT_HEAD", DepartmentId = ids.Department1, IsActive = true });
        var target = await db.SessionsSet.FindAsync(ids.SessionA);
        db.SessionsSet.Add(new Session { Id = 604, ClassSectionId = 402, OriginalTeacherId = ids.TeacherB, AttendancePolicyId = 501, ScheduledStart = target!.ScheduledStart.AddMinutes(30), ScheduledEnd = target.ScheduledEnd, Status = "READY" });
        await db.SaveChangesAsync();
        var controller = WithUser(new SessionsController(db, null!, EmptyConfiguration(), new AuditSpy(), null!), ids.HeadUser, "LECTURER");

        Assert.IsType<ConflictObjectResult>(await controller.AssignSubstitution(ids.SessionA, new(ids.TeacherB, "Conflict", null), CancellationToken.None));
    }

    [Fact]
    public async Task Department_head_scope_is_limited_to_managed_department()
    {
        await using var db = CreateDb();
        var ids = await SeedScopeFixture(db);
        db.ManagementAssignmentsSet.Add(new ManagementAssignment { TeacherId = ids.HeadTeacher, PositionType = "DEPARTMENT_HEAD", DepartmentId = ids.Department1, IsActive = true });
        await db.SaveChangesAsync();

        Assert.True(await HTBAM.Api.Support.AccessScope.CanViewSessionAsync(db, Principal(ids.HeadUser, "LECTURER"), ids.SessionA, CancellationToken.None));
        Assert.False(await HTBAM.Api.Support.AccessScope.CanViewSessionAsync(db, Principal(ids.HeadUser, "LECTURER"), ids.SessionC, CancellationToken.None));
    }

    [Fact]
    public async Task Faculty_head_scope_is_limited_to_managed_faculty()
    {
        await using var db = CreateDb();
        var ids = await SeedScopeFixture(db);
        db.ManagementAssignmentsSet.Add(new ManagementAssignment { TeacherId = ids.HeadTeacher, PositionType = "FACULTY_HEAD", FacultyId = ids.Faculty1, IsActive = true });
        await db.SaveChangesAsync();

        Assert.True(await HTBAM.Api.Support.AccessScope.CanViewSessionAsync(db, Principal(ids.HeadUser, "LECTURER"), ids.SessionB, CancellationToken.None));
        Assert.False(await HTBAM.Api.Support.AccessScope.CanViewSessionAsync(db, Principal(ids.HeadUser, "LECTURER"), ids.SessionC, CancellationToken.None));
    }

    [Fact]
    public async Task Faculty_head_transfer_keeps_history_and_only_one_active_head()
    {
        await using var db = CreateDb();
        var ids = await SeedScopeFixture(db);
        db.ManagementAssignmentsSet.Add(new ManagementAssignment { TeacherId = ids.HeadTeacher, PositionType = "FACULTY_HEAD", FacultyId = ids.Faculty1, IsActive = true });
        await db.SaveChangesAsync();
        var controller = WithUser(new ManagementAssignmentsController(db, new AuditSpy()), ids.HeadUser, "LECTURER");

        var result = await controller.TransferFacultyHead(new ManagementAssignmentsController.AssignFacultyHeadRequest(ids.TeacherA, ids.Faculty1, "transfer"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var all = await db.ManagementAssignmentsSet.Where(x => x.PositionType == "FACULTY_HEAD" && x.FacultyId == ids.Faculty1).ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.Single(all, x => x.IsActive);
        Assert.Contains(all, x => !x.IsActive && x.EffectiveTo != null);
    }

    [Fact]
    public async Task Catalog_departments_endpoint_limits_lecturer_to_own_department()
    {
        await using var db = CreateDb();
        var ids = await SeedScopeFixture(db);
        var controller = WithUser(new CatalogsController(db, new AuditSpy()), ids.UserA, "LECTURER");

        var result = await controller.Departments(CancellationToken.None);

        Assert.Equal([ids.Department1], ResultIds(result));
    }

    [Fact]
    public async Task Catalog_departments_endpoint_resolves_department_head_scope()
    {
        await using var db = CreateDb();
        var ids = await SeedScopeFixture(db);
        db.ManagementAssignmentsSet.Add(new ManagementAssignment { TeacherId = ids.HeadTeacher, PositionType = "DEPARTMENT_HEAD", DepartmentId = ids.Department1, IsActive = true });
        await db.SaveChangesAsync();
        var controller = WithUser(new CatalogsController(db, new AuditSpy()), ids.HeadUser, "LECTURER");

        var result = await controller.Departments(CancellationToken.None);

        Assert.Equal([ids.Department1], ResultIds(result));
    }

    [Fact]
    public async Task Catalog_departments_endpoint_resolves_faculty_head_scope()
    {
        await using var db = CreateDb();
        var ids = await SeedScopeFixture(db);
        db.ManagementAssignmentsSet.Add(new ManagementAssignment { TeacherId = ids.HeadTeacher, PositionType = "FACULTY_HEAD", FacultyId = ids.Faculty1, IsActive = true });
        await db.SaveChangesAsync();
        var controller = WithUser(new CatalogsController(db, new AuditSpy()), ids.HeadUser, "LECTURER");

        var result = await controller.Departments(CancellationToken.None);

        Assert.Equal([ids.Department1, ids.Department2], ResultIds(result));
    }

    [Fact]
    public async Task Catalog_departments_endpoint_does_not_allow_admin_business_bypass()
    {
        await using var db = CreateDb();
        await SeedScopeFixture(db);
        var controller = WithAdmin(new CatalogsController(db, new AuditSpy()));

        var result = await controller.Departments(CancellationToken.None);

        Assert.Empty(ResultIds(result));
    }

    [Fact]
    public async Task ApiExceptionMiddleware_request_abort_cancellation_does_not_return_internal_error()
    {
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };
        context.Response.Body = new MemoryStream();
        var middleware = new ApiExceptionMiddleware(_ => throw new TaskCanceledException("A task was canceled"), NullLogger<ApiExceptionMiddleware>.Instance);

        await middleware.Invoke(context);

        Assert.Equal(499, context.Response.StatusCode);
    }

    [Fact]
    public async Task ApiExceptionMiddleware_non_request_cancellation_is_not_swallowed()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ApiExceptionMiddleware(_ => throw new TaskCanceledException("Internal timeout"), NullLogger<ApiExceptionMiddleware>.Instance);

        await middleware.Invoke(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
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

    private static T WithUser<T>(T controller, long userId, params string[] roles) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(userId, roles) } };
        return controller;
    }

    private static async Task SeedFacultyHead(AppDbContext db)
    {
        if (!await db.FacultiesSet.AnyAsync(x => x.Id == 1)) db.FacultiesSet.Add(new Faculty { Id = 1, Code = "F1", Name = "Faculty 1" });
        if (!await db.DepartmentsSet.AnyAsync(x => x.Id == 1)) db.DepartmentsSet.Add(new Department { Id = 1, Code = "D1", Name = "Department 1", FacultyId = 1 });
        if (!await db.UsersSet.AnyAsync(x => x.Id == 100)) db.UsersSet.Add(new User { Id = 100, UserName = "faculty-head", FullName = "Faculty Head", Status = "ACTIVE" });
        if (!await db.Teachers.AnyAsync(x => x.Id == 100)) db.Teachers.Add(new Teacher { Id = 100, TeacherCode = "GV-H", FullName = "Faculty Head", UserId = 100, DepartmentId = 1, IsActive = true });
        if (!await db.ManagementAssignmentsSet.AnyAsync(x => x.TeacherId == 100)) db.ManagementAssignmentsSet.Add(new ManagementAssignment { TeacherId = 100, PositionType = "FACULTY_HEAD", FacultyId = 1, IsActive = true });
        await db.SaveChangesAsync();
    }

    private static ClaimsPrincipal Principal(long userId, params string[] roles) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }.Concat(roles.Select(role => new Claim(ClaimTypes.Role, role))), "Test"));

    private static long[] ResultIds(ActionResult result)
    {
        var value = Assert.IsType<OkObjectResult>(result).Value;
        return Assert.IsAssignableFrom<IEnumerable<object>>(value)
            .Select(item => (long)item.GetType().GetProperty("Id")!.GetValue(item)!)
            .OrderBy(id => id)
            .ToArray();
    }

    private sealed record ScopeIds(long UserA, long UserB, long UserC, long HeadUser, long TeacherA, long TeacherB, long TeacherC, long HeadTeacher, long Faculty1, long Faculty2, long Department1, long Department2, long Department3, long SessionA, long SessionB, long SessionC);

    private static async Task<ScopeIds> SeedScopeFixture(AppDbContext db)
    {
        db.FacultiesSet.AddRange(new Faculty { Id = 1, Code = "F1", Name = "Faculty 1" }, new Faculty { Id = 2, Code = "F2", Name = "Faculty 2" });
        db.DepartmentsSet.AddRange(new Department { Id = 10, Code = "D1", Name = "Dept 1", FacultyId = 1 }, new Department { Id = 11, Code = "D2", Name = "Dept 2", FacultyId = 1 }, new Department { Id = 12, Code = "D3", Name = "Dept 3", FacultyId = 2 });
        db.UsersSet.AddRange(
            new User { Id = 101, UserName = "a", FullName = "A", Status = "ACTIVE" },
            new User { Id = 102, UserName = "b", FullName = "B", Status = "ACTIVE" },
            new User { Id = 103, UserName = "c", FullName = "C", Status = "ACTIVE" },
            new User { Id = 104, UserName = "head", FullName = "Head", Status = "ACTIVE" },
            new User { Id = 90, UserName = "admin", FullName = "Admin", Status = "ACTIVE" });
        db.Teachers.AddRange(
            new Teacher { Id = 201, TeacherCode = "GV-A", FullName = "Teacher A", UserId = 101, DepartmentId = 10, IsActive = true },
            new Teacher { Id = 202, TeacherCode = "GV-B", FullName = "Teacher B", UserId = 102, DepartmentId = 10, IsActive = true },
            new Teacher { Id = 203, TeacherCode = "GV-C", FullName = "Teacher C", UserId = 103, DepartmentId = 12, IsActive = true },
            new Teacher { Id = 204, TeacherCode = "GV-H", FullName = "Head Teacher", UserId = 104, DepartmentId = 10, IsActive = true });
        db.Courses.AddRange(new Course { Id = 301, Code = "C1", Name = "Course 1", DepartmentId = 10, IsActive = true }, new Course { Id = 302, Code = "C2", Name = "Course 2", DepartmentId = 11, IsActive = true }, new Course { Id = 303, Code = "C3", Name = "Course 3", DepartmentId = 12, IsActive = true });
        db.ClassSections.AddRange(new ClassSection { Id = 401, Code = "CS1", Name = "CS1", CourseId = 301, TeacherId = 201, Semester = "HK1", AcademicYear = "2026", IsActive = true }, new ClassSection { Id = 402, Code = "CS2", Name = "CS2", CourseId = 302, TeacherId = 202, Semester = "HK1", AcademicYear = "2026", IsActive = true }, new ClassSection { Id = 403, Code = "CS3", Name = "CS3", CourseId = 303, TeacherId = 203, Semester = "HK1", AcademicYear = "2026", IsActive = true });
        db.AttendancePoliciesSet.Add(new AttendancePolicy { Id = 501, Code = "P", Name = "Policy", PresentThreshold = .8m, PartialThreshold = .5m, PresentScore = 10, PartialScore = 5, AbsentScore = 0, Version = "v1", IsActive = true });
        db.SessionsSet.AddRange(
            new Session { Id = 601, ClassSectionId = 401, OriginalTeacherId = 201, AttendancePolicyId = 501, ScheduledStart = DateTime.UtcNow.AddDays(1), ScheduledEnd = DateTime.UtcNow.AddDays(1).AddHours(2), Status = "READY" },
            new Session { Id = 602, ClassSectionId = 402, OriginalTeacherId = 202, AttendancePolicyId = 501, ScheduledStart = DateTime.UtcNow.AddDays(2), ScheduledEnd = DateTime.UtcNow.AddDays(2).AddHours(2), Status = "READY" },
            new Session { Id = 603, ClassSectionId = 403, OriginalTeacherId = 203, AttendancePolicyId = 501, ScheduledStart = DateTime.UtcNow.AddDays(3), ScheduledEnd = DateTime.UtcNow.AddDays(3).AddHours(2), Status = "READY" });
        await db.SaveChangesAsync();
        return new ScopeIds(101, 102, 103, 104, 201, 202, 203, 204, 1, 2, 10, 11, 12, 601, 602, 603);
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
