using HTBAM.Api.Support;
using HTBAM.Application.Interfaces;
using HTBAM.Domain.Entities;
using HTBAM.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HTBAM.Api.Controllers;

[ApiController, Authorize(Roles = "LECTURER"), Route("api/management-assignments")]
public sealed class ManagementAssignmentsController(AppDbContext db, IAuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> List(CancellationToken ct)
    {
        var teacherId = await AccessScope.TeacherIdAsync(db, User, ct);
        if (teacherId is not > 0) return Forbid();
        var scope = await db.ManagementAssignmentsSet.AsNoTracking()
            .Where(a => a.IsActive && a.TeacherId == teacherId)
            .ToListAsync(ct);
        var managedFacultyIds = scope.Where(a => a.PositionType == "FACULTY_HEAD" && a.FacultyId != null).Select(a => a.FacultyId!.Value).ToArray();
        var managedDepartmentIds = scope.Where(a => a.PositionType == "DEPARTMENT_HEAD" && a.DepartmentId != null).Select(a => a.DepartmentId!.Value).ToArray();
        var teacherRows = await db.Teachers.AsNoTracking()
            .Where(t => t.IsActive && (
                (t.DepartmentId != null && managedDepartmentIds.Contains(t.DepartmentId.Value)) ||
                (t.DepartmentId != null && db.DepartmentsSet.Any(d => d.Id == t.DepartmentId && managedFacultyIds.Contains(d.FacultyId))) ||
                t.Id == teacherId))
            .Select(t => new
            {
                t.Id,
                t.TeacherCode,
                t.FullName,
                t.Email,
                t.UserId,
                t.DepartmentId,
                Department = t.DepartmentId == null ? null : db.DepartmentsSet.Where(d => d.Id == t.DepartmentId).Select(d => d.Name).FirstOrDefault(),
                FacultyId = t.DepartmentId == null ? null : db.DepartmentsSet.Where(d => d.Id == t.DepartmentId).Select(d => (long?)d.FacultyId).FirstOrDefault()
            })
            .OrderBy(x => x.TeacherCode)
            .ToListAsync(ct);

        var teacherIds = teacherRows.Select(t => t.Id).ToArray();
        var assignments = await db.ManagementAssignmentsSet.AsNoTracking()
            .Where(a => a.IsActive && teacherIds.Contains(a.TeacherId))
            .Select(a => new { a.Id, a.TeacherId, a.PositionType, a.FacultyId, a.DepartmentId, a.EffectiveFrom, a.Note })
            .ToListAsync(ct);

        var rows = teacherRows.Select(t => new
        {
            t.Id,
            t.TeacherCode,
            t.FullName,
            t.Email,
            t.UserId,
            t.DepartmentId,
            t.Department,
            t.FacultyId,
            Assignments = assignments.Where(a => a.TeacherId == t.Id).ToList()
        }).ToList();
        return Ok(rows);
    }

    public record AssignFacultyHeadRequest(long TeacherId, long FacultyId, string? Note);
    public record AssignDepartmentHeadRequest(long TeacherId, long DepartmentId, string? Note);

    [HttpPost("faculty-head")]
    public async Task<ActionResult> TransferFacultyHead(AssignFacultyHeadRequest r, CancellationToken ct)
    {
        if (!await AccessScope.CanManageFacultyAsync(db, User, r.FacultyId, ct)) return Forbid();
        var valid = await db.Teachers.AsNoTracking().AnyAsync(t => t.Id == r.TeacherId && t.IsActive && t.UserId != null && t.DepartmentId != null && db.UsersSet.Any(u => u.Id == t.UserId && u.Status == "ACTIVE") && db.DepartmentsSet.Any(d => d.Id == t.DepartmentId && d.FacultyId == r.FacultyId), ct);
        if (!valid) return BadRequest("Giảng viên nhận quyền Trưởng khoa không hợp lệ hoặc không thuộc khoa.");
        var assignment = await ReplaceActive("FACULTY_HEAD", r.TeacherId, r.FacultyId, null, r.Note, ct);
        await audit.WriteAsync(UserContext.Id(User), "FACULTY_HEAD_TRANSFER", "ManagementAssignment", assignment.Id.ToString(), new { r.TeacherId, r.FacultyId }, ct);
        return Ok(assignment);
    }

    [HttpPost("department-head")]
    public async Task<ActionResult> AssignDepartmentHead(AssignDepartmentHeadRequest r, CancellationToken ct)
    {
        if (!await AccessScope.CanManageDepartmentAsync(db, User, r.DepartmentId, ct)) return Forbid();
        var valid = await db.Teachers.AsNoTracking().AnyAsync(t => t.Id == r.TeacherId && t.IsActive && t.UserId != null && t.DepartmentId == r.DepartmentId && db.UsersSet.Any(u => u.Id == t.UserId && u.Status == "ACTIVE"), ct);
        if (!valid) return BadRequest("Giảng viên nhận quyền Trưởng bộ môn không hợp lệ hoặc không thuộc bộ môn.");
        var assignment = await ReplaceActive("DEPARTMENT_HEAD", r.TeacherId, null, r.DepartmentId, r.Note, ct);
        await audit.WriteAsync(UserContext.Id(User), "DEPARTMENT_HEAD_ASSIGN", "ManagementAssignment", assignment.Id.ToString(), new { r.TeacherId, r.DepartmentId }, ct);
        return Ok(assignment);
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Revoke(long id, CancellationToken ct)
    {
        var assignment = await db.ManagementAssignmentsSet.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (assignment is null) return NotFound();
        var allowed = assignment.PositionType == "FACULTY_HEAD" && assignment.FacultyId != null
            ? await AccessScope.CanManageFacultyAsync(db, User, assignment.FacultyId.Value, ct)
            : assignment.DepartmentId != null && await AccessScope.CanManageDepartmentAsync(db, User, assignment.DepartmentId.Value, ct);
        if (!allowed) return Forbid();
        assignment.IsActive = false;
        assignment.EffectiveTo = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(UserContext.Id(User), "MANAGEMENT_ASSIGNMENT_REVOKE", "ManagementAssignment", id.ToString(), new { assignment.TeacherId, assignment.PositionType, assignment.FacultyId, assignment.DepartmentId }, ct);
        return NoContent();
    }

    private async Task<ManagementAssignment> ReplaceActive(string positionType, long teacherId, long? facultyId, long? departmentId, string? note, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var current = await db.ManagementAssignmentsSet.Where(x => x.PositionType == positionType && x.IsActive && x.FacultyId == facultyId && x.DepartmentId == departmentId).ToListAsync(ct);
        foreach (var old in current)
        {
            old.IsActive = false;
            old.EffectiveTo = now;
        }
        var actorTeacherId = await AccessScope.TeacherIdAsync(db, User, ct);
        var created = new ManagementAssignment { TeacherId = teacherId, PositionType = positionType, FacultyId = facultyId, DepartmentId = departmentId, EffectiveFrom = now, IsActive = true, AssignedByUserId = UserContext.Id(User), AssignedByTeacherId = actorTeacherId > 0 ? actorTeacherId : null, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim() };
        db.ManagementAssignmentsSet.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }
}
