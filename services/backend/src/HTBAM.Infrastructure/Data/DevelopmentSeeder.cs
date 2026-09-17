using HTBAM.Domain.Entities; using Microsoft.AspNetCore.Identity; using Microsoft.EntityFrameworkCore;
namespace HTBAM.Infrastructure.Data;
public static class DevelopmentSeeder
{
 public static async Task SeedAsync(AppDbContext db,CancellationToken ct=default)
 {
  if(!await db.Database.CanConnectAsync(ct))return;
  foreach(var name in new[]{"ADMIN","LECTURER","TECH_AI"}) if(!await db.RolesSet.AnyAsync(x=>x.Name==name,ct)) db.RolesSet.Add(new Role{Name=name}); await db.SaveChangesAsync(ct);
  var admin=await EnsureUser(db,"admin","admin@htbam.local","Quản trị HTBAM","Admin@123456","ADMIN",ct);
  var lecturer=await EnsureUser(db,"lecturer","lecturer@htbam.local","Giảng viên Demo","Lecturer@123456","LECTURER",ct);
  await EnsureRule(db,"DISTRACTION_LONG","Mất tập trung kéo dài","DISTRACTED",15,0.70m,0.55m,ct); await EnsureRule(db,"SLEEP","Ngủ gật","SLEEPY",10,0.75m,0.55m,ct); await EnsureRule(db,"PHONE","Sử dụng điện thoại","PHONE_USE",5,0.70m,0.55m,ct); await EnsureRule(db,"OUT_OF_VIEW","Rời vùng quan sát","OUT_OF_VIEW",20,0.80m,0.00m,ct);
  if(!await db.AttendancePoliciesSet.AnyAsync(x=>x.Code=="DEFAULT",ct))db.AttendancePoliciesSet.Add(new AttendancePolicy{Code="DEFAULT",Name="Chính sách chuyên cần mặc định",PresentThreshold=.80m,PartialThreshold=.50m,PresentScore=10,PartialScore=5,AbsentScore=0,Version="v1",IsActive=true});
  await db.SaveChangesAsync(ct);
  var demoTeacher=await db.Teachers.FirstOrDefaultAsync(x=>x.TeacherCode=="GV001",ct);if(demoTeacher is not null&&demoTeacher.UserId is null){demoTeacher.UserId=lecturer.Id;demoTeacher.Email=lecturer.Email;await db.SaveChangesAsync(ct);}
 }
 private static async Task<User> EnsureUser(AppDbContext db,string userName,string email,string fullName,string password,string roleName,CancellationToken ct){var u=await db.UsersSet.Include(x=>x.UserRoles).FirstOrDefaultAsync(x=>x.UserName==userName,ct);if(u is null){u=new User{UserName=userName,Email=email,FullName=fullName,Status="ACTIVE"};u.PasswordHash=new PasswordHasher<User>().HashPassword(u,password);db.UsersSet.Add(u);await db.SaveChangesAsync(ct);}var role=await db.RolesSet.SingleAsync(x=>x.Name==roleName,ct);if(!await db.UserRoles.AnyAsync(x=>x.UserId==u.Id&&x.RoleId==role.Id,ct)){db.UserRoles.Add(new UserRole{UserId=u.Id,RoleId=role.Id});await db.SaveChangesAsync(ct);}return u;}
 private static async Task EnsureRule(AppDbContext db,string code,string name,string label,int duration,decimal confidence,decimal quality,CancellationToken ct){if(!await db.AlertRulesSet.AnyAsync(x=>x.Code==code,ct))db.AlertRulesSet.Add(new AlertRule{Code=code,Name=name,BehaviorLabel=label,MinDurationSeconds=duration,MinConfidence=confidence,MinObservationQuality=quality,Enabled=true,Version="v1"});}
}
