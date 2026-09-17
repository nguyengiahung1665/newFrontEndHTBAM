using System.Security.Claims; using HTBAM.Infrastructure.Data; using Microsoft.EntityFrameworkCore;
namespace HTBAM.Api.Support;
public static class AccessScope
{
 public static bool IsAdmin(ClaimsPrincipal user)=>user.IsInRole("ADMIN");
 public static async Task<long?> TeacherIdAsync(AppDbContext db,ClaimsPrincipal user,CancellationToken ct){if(IsAdmin(user)||user.IsInRole("TECH_AI"))return null;if(!long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier),out var uid))return -1;return await db.Teachers.AsNoTracking().Where(x=>x.UserId==uid&&x.IsActive).Select(x=>(long?)x.Id).FirstOrDefaultAsync(ct)??-1;}
 public static async Task<bool> CanAccessClassAsync(AppDbContext db,ClaimsPrincipal user,long classSectionId,CancellationToken ct){if(IsAdmin(user)||user.IsInRole("TECH_AI"))return true;var tid=await TeacherIdAsync(db,user,ct);return tid>0&&await db.ClassSections.AsNoTracking().AnyAsync(x=>x.Id==classSectionId&&x.TeacherId==tid,ct);}
 public static async Task<bool> CanAccessSessionAsync(AppDbContext db,ClaimsPrincipal user,long sessionId,CancellationToken ct){if(IsAdmin(user)||user.IsInRole("TECH_AI"))return true;var tid=await TeacherIdAsync(db,user,ct);return tid>0&&await db.SessionsSet.AsNoTracking().AnyAsync(s=>s.Id==sessionId&&s.ClassSection.TeacherId==tid,ct);}
 public static IQueryable<HTBAM.Domain.Entities.Session> Sessions(AppDbContext db,ClaimsPrincipal user,long? teacherId){var q=db.SessionsSet.AsQueryable();return IsAdmin(user)||user.IsInRole("TECH_AI")?q:q.Where(x=>x.ClassSection.TeacherId==teacherId);}
}
