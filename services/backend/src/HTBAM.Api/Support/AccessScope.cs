using System.Security.Claims;
using HTBAM.Domain.Entities;
using HTBAM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HTBAM.Api.Support;

public static class AccessScope
{
    public static async Task<long?> TeacherIdAsync(AppDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)) return -1;
        return await db.Teachers.AsNoTracking()
            .Where(x => x.UserId == uid && x.IsActive)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(ct) ?? -1;
    }

    public static IQueryable<Session> Sessions(AppDbContext db, ClaimsPrincipal user, long? teacherId)
    {
        if (teacherId is not > 0) return db.SessionsSet.Where(_ => false);
        return ScopedSessions(db, teacherId.Value);
    }

    public static async Task<bool> CanViewClassSectionAsync(AppDbContext db, ClaimsPrincipal user, long classSectionId, CancellationToken ct)
    {
        var teacherId = await TeacherIdAsync(db, user, ct);
        if (teacherId is not > 0) return false;
        return await db.ClassSections.AsNoTracking().AnyAsync(c =>
            c.Id == classSectionId &&
            (
                c.TeacherId == teacherId ||
                db.ManagementAssignmentsSet.Any(a =>
                    a.TeacherId == teacherId && a.IsActive &&
                    (
                        (a.PositionType == "DEPARTMENT_HEAD" && a.DepartmentId != null && c.Course.DepartmentId == a.DepartmentId) ||
                        (a.PositionType == "FACULTY_HEAD" && a.FacultyId != null && c.Course.DepartmentId != null &&
                            db.DepartmentsSet.Any(d => d.Id == c.Course.DepartmentId && d.FacultyId == a.FacultyId))
                    ))
            ), ct);
    }

    public static Task<bool> CanManageClassSectionAsync(AppDbContext db, ClaimsPrincipal user, long classSectionId, CancellationToken ct) =>
        CanViewClassSectionAsync(db, user, classSectionId, ct);

    public static Task<bool> CanManageRosterAsync(AppDbContext db, ClaimsPrincipal user, long classSectionId, CancellationToken ct) =>
        CanManageClassSectionAsync(db, user, classSectionId, ct);

    public static async Task<bool> CanViewStudentAsync(AppDbContext db, ClaimsPrincipal user, long studentId, CancellationToken ct)
    {
        var teacherId = await TeacherIdAsync(db, user, ct);
        if (teacherId is not > 0) return false;
        var sessionIds = ScopedSessions(db, teacherId.Value).Select(s => s.Id);
        if (await db.SessionStudentsSet.AnyAsync(x => x.StudentId == studentId && sessionIds.Contains(x.SessionId), ct)) return true;
        return await db.Enrollments.AnyAsync(x => x.StudentId == studentId &&
            (x.ClassSection.TeacherId == teacherId || db.ManagementAssignmentsSet.Any(a => a.TeacherId == teacherId && a.IsActive &&
                ((a.PositionType == "DEPARTMENT_HEAD" && a.DepartmentId == x.ClassSection.Course.DepartmentId) ||
                 (a.PositionType == "FACULTY_HEAD" && a.FacultyId != null && db.DepartmentsSet.Any(d => d.Id == x.ClassSection.Course.DepartmentId && d.FacultyId == a.FacultyId))))), ct);
    }

    public static async Task<bool> CanManageStudentAsync(AppDbContext db, ClaimsPrincipal user, long studentId, CancellationToken ct)
    {
        var teacherId = await TeacherIdAsync(db, user, ct);
        if (teacherId is not > 0) return false;
        return await db.Enrollments.AnyAsync(x => x.StudentId == studentId &&
            (x.ClassSection.TeacherId == teacherId || db.ManagementAssignmentsSet.Any(a => a.TeacherId == teacherId && a.IsActive &&
                ((a.PositionType == "DEPARTMENT_HEAD" && a.DepartmentId == x.ClassSection.Course.DepartmentId) ||
                 (a.PositionType == "FACULTY_HEAD" && a.FacultyId != null && db.DepartmentsSet.Any(d => d.Id == x.ClassSection.Course.DepartmentId && d.FacultyId == a.FacultyId))))), ct);
    }

    public static async Task<bool> CanManageStudentClassAsync(AppDbContext db, ClaimsPrincipal user, long studentClassId, CancellationToken ct)
    {
        var facultyId = await db.StudentClassesSet.Where(x => x.Id == studentClassId).Select(x => (long?)x.FacultyId).FirstOrDefaultAsync(ct);
        return facultyId != null && await CanManageFacultyAsync(db, user, facultyId.Value, ct);
    }

    public static async Task<bool> CanManageDepartmentDataAsync(AppDbContext db, ClaimsPrincipal user, long departmentId, CancellationToken ct)
    {
        var teacherId = await TeacherIdAsync(db, user, ct);
        if (teacherId is not > 0) return false;
        return await db.ManagementAssignmentsSet.AsNoTracking().AnyAsync(a => a.TeacherId == teacherId && a.IsActive &&
            ((a.PositionType == "DEPARTMENT_HEAD" && a.DepartmentId == departmentId) ||
             (a.PositionType == "FACULTY_HEAD" && a.FacultyId != null && db.DepartmentsSet.Any(d => d.Id == departmentId && d.FacultyId == a.FacultyId))), ct);
    }

    public static Task<bool> CanAccessClassAsync(AppDbContext db, ClaimsPrincipal user, long classSectionId, CancellationToken ct) =>
        CanViewClassSectionAsync(db, user, classSectionId, ct);

    public static async Task<bool> CanViewSessionAsync(AppDbContext db, ClaimsPrincipal user, long sessionId, CancellationToken ct)
    {
        var teacherId = await TeacherIdAsync(db, user, ct);
        if (teacherId is not > 0) return false;
        return await ScopedSessions(db, teacherId.Value).AnyAsync(x => x.Id == sessionId, ct);
    }

    public static Task<bool> CanAccessSessionAsync(AppDbContext db, ClaimsPrincipal user, long sessionId, CancellationToken ct) =>
        CanViewSessionAsync(db, user, sessionId, ct);

    public static async Task<bool> CanOperateSessionAsync(AppDbContext db, ClaimsPrincipal user, long sessionId, CancellationToken ct)
    {
        var teacherId = await TeacherIdAsync(db, user, ct);
        if (teacherId is not > 0) return false;
        return await db.SessionsSet.AsNoTracking().AnyAsync(s =>
            s.Id == sessionId &&
            (
                (db.SessionSubstitutionsSet.Any(sub => sub.SessionId == s.Id && sub.Status == "ACTIVE")
                    ? db.SessionSubstitutionsSet.Any(sub => sub.SessionId == s.Id && sub.Status == "ACTIVE" && sub.SubstituteTeacherId == teacherId)
                    : (s.OriginalTeacherId == teacherId || (s.OriginalTeacherId == 0 && s.ClassSection.TeacherId == teacherId)))
            ), ct);
    }

    public static async Task<bool> CanManageSessionAsync(AppDbContext db, ClaimsPrincipal user, long sessionId, CancellationToken ct)
    {
        var teacherId = await TeacherIdAsync(db, user, ct);
        if (teacherId is not > 0) return false;
        return await db.SessionsSet.AsNoTracking().AnyAsync(s => s.Id == sessionId &&
            (s.OriginalTeacherId == teacherId ||
             (s.OriginalTeacherId == 0 && s.ClassSection.TeacherId == teacherId) ||
             db.ManagementAssignmentsSet.Any(a => a.TeacherId == teacherId && a.IsActive &&
                ((a.PositionType == "DEPARTMENT_HEAD" && a.DepartmentId == s.ClassSection.Course.DepartmentId) ||
                 (a.PositionType == "FACULTY_HEAD" && a.FacultyId != null &&
                    db.DepartmentsSet.Any(d => d.Id == s.ClassSection.Course.DepartmentId && d.FacultyId == a.FacultyId))))), ct);
    }

    public static Task<bool> CanExportSessionReportAsync(AppDbContext db, ClaimsPrincipal user, long sessionId, CancellationToken ct) =>
        CanViewSessionAsync(db, user, sessionId, ct);

    public static async Task<bool> CanAssignSubstituteAsync(AppDbContext db, ClaimsPrincipal user, long sessionId, CancellationToken ct)
    {
        var teacherId = await TeacherIdAsync(db, user, ct);
        if (teacherId is not > 0) return false;
        return await db.SessionsSet.AsNoTracking().AnyAsync(s =>
            s.Id == sessionId &&
            db.ManagementAssignmentsSet.Any(a =>
                a.TeacherId == teacherId && a.IsActive &&
                (
                    (a.PositionType == "DEPARTMENT_HEAD" && a.DepartmentId != null && s.ClassSection.Course.DepartmentId == a.DepartmentId) ||
                    (a.PositionType == "FACULTY_HEAD" && a.FacultyId != null && s.ClassSection.Course.DepartmentId != null &&
                        db.DepartmentsSet.Any(d => d.Id == s.ClassSection.Course.DepartmentId && d.FacultyId == a.FacultyId))
                )), ct);
    }

    public static async Task<bool> CanManageDepartmentAsync(AppDbContext db, ClaimsPrincipal user, long departmentId, CancellationToken ct)
    {
        var teacherId = await TeacherIdAsync(db, user, ct);
        if (teacherId is not > 0) return false;
        return await db.ManagementAssignmentsSet.AsNoTracking().AnyAsync(a =>
            a.TeacherId == teacherId && a.IsActive && a.PositionType == "FACULTY_HEAD" && a.FacultyId != null &&
            db.DepartmentsSet.Any(d => d.Id == departmentId && d.FacultyId == a.FacultyId), ct);
    }

    public static async Task<bool> CanManageFacultyAsync(AppDbContext db, ClaimsPrincipal user, long facultyId, CancellationToken ct)
    {
        var teacherId = await TeacherIdAsync(db, user, ct);
        if (teacherId is not > 0) return false;
        return await db.ManagementAssignmentsSet.AsNoTracking().AnyAsync(a =>
            a.TeacherId == teacherId && a.IsActive && a.PositionType == "FACULTY_HEAD" && a.FacultyId == facultyId, ct);
    }

    private static IQueryable<Session> ScopedSessions(AppDbContext db, long teacherId) =>
        db.SessionsSet.Where(s =>
            s.OriginalTeacherId == teacherId ||
            (s.OriginalTeacherId == 0 && s.ClassSection.TeacherId == teacherId) ||
            db.SessionSubstitutionsSet.Any(sub => sub.SessionId == s.Id && sub.Status == "ACTIVE" && sub.SubstituteTeacherId == teacherId) ||
            db.ManagementAssignmentsSet.Any(a =>
                a.TeacherId == teacherId && a.IsActive &&
                (
                    (a.PositionType == "DEPARTMENT_HEAD" && a.DepartmentId != null && s.ClassSection.Course.DepartmentId == a.DepartmentId) ||
                    (a.PositionType == "FACULTY_HEAD" && a.FacultyId != null && s.ClassSection.Course.DepartmentId != null &&
                        db.DepartmentsSet.Any(d => d.Id == s.ClassSection.Course.DepartmentId && d.FacultyId == a.FacultyId))
                )));
}
