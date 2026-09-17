using HTBAM.Api.Support; using HTBAM.Infrastructure.Data; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore;
namespace HTBAM.Api.Controllers;
[ApiController,Authorize,Route("api/search")]
public sealed class SearchController(AppDbContext db):ControllerBase
{
 [HttpGet]
 public async Task<ActionResult> Search([FromQuery]string? q,[FromQuery]string? behavior,[FromQuery]DateTime? from,[FromQuery]DateTime? to,CancellationToken ct)
 {
  var key=q?.Trim()??"";
  var students=await db.StudentsSet.AsNoTracking().Where(x=>x.IsActive&&(key==""||x.StudentCode.Contains(key)||x.FullName.Contains(key))).OrderBy(x=>x.StudentCode).Take(30).Select(x=>new{x.Id,x.StudentCode,x.FullName}).ToListAsync(ct);
  var classesQ=db.ClassSections.AsNoTracking().AsQueryable();if(User.IsInRole("LECTURER")){var tid=await AccessScope.TeacherIdAsync(db,User,ct);classesQ=classesQ.Where(x=>x.TeacherId==tid);}if(key!="")classesQ=classesQ.Where(x=>x.Code.Contains(key)||x.Name.Contains(key));var classes=await classesQ.OrderBy(x=>x.Code).Take(30).Select(x=>new{x.Id,x.Code,x.Name,x.Semester,x.AcademicYear}).ToListAsync(ct);
  var sessionsQ=db.SessionsSet.AsNoTracking().Include(x=>x.ClassSection).AsQueryable();if(User.IsInRole("LECTURER")){var tid=await AccessScope.TeacherIdAsync(db,User,ct);sessionsQ=sessionsQ.Where(x=>x.ClassSection.TeacherId==tid);}if(from!=null)sessionsQ=sessionsQ.Where(x=>x.ScheduledStart>=from);if(to!=null)sessionsQ=sessionsQ.Where(x=>x.ScheduledStart<to);if(key!="")sessionsQ=sessionsQ.Where(x=>x.ClassSection.Code.Contains(key)||x.Status.Contains(key));var sessions=await sessionsQ.OrderByDescending(x=>x.ScheduledStart).Take(50).Select(x=>new{x.Id,x.ClassSectionId,ClassSection=x.ClassSection.Code,x.ScheduledStart,x.Status}).ToListAsync(ct);
  var sessionIds=sessionsQ.Select(x=>x.Id);var behaviorQ=db.BehaviorEventsSet.AsNoTracking().Where(x=>sessionIds.Contains(x.SessionId));if(!string.IsNullOrWhiteSpace(behavior))behaviorQ=behaviorQ.Where(x=>x.Label==behavior.ToUpper());if(from!=null)behaviorQ=behaviorQ.Where(x=>x.StartedAt>=from);if(to!=null)behaviorQ=behaviorQ.Where(x=>x.StartedAt<to);if(key!="")behaviorQ=behaviorQ.Where(x=>x.StudentId!=null&&db.StudentsSet.Any(s=>s.Id==x.StudentId&&(s.StudentCode.Contains(key)||s.FullName.Contains(key))));var behaviors=await behaviorQ.OrderByDescending(x=>x.StartedAt).Take(100).ToListAsync(ct);
  return Ok(new{students,classes,sessions,behaviors});
 }
}
