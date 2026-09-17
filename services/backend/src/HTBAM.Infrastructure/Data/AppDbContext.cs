using HTBAM.Application.Interfaces;
using HTBAM.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HTBAM.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> UsersSet => Set<User>(); public IQueryable<User> Users => UsersSet;
    public DbSet<Role> RolesSet => Set<Role>(); public IQueryable<Role> Roles => RolesSet;
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Faculty> FacultiesSet => Set<Faculty>(); public IQueryable<Faculty> Faculties => FacultiesSet;
    public DbSet<Department> DepartmentsSet => Set<Department>(); public IQueryable<Department> Departments => DepartmentsSet;
    public DbSet<StudentClass> StudentClassesSet => Set<StudentClass>(); public IQueryable<StudentClass> StudentClasses => StudentClassesSet;
    public DbSet<Student> StudentsSet => Set<Student>(); public IQueryable<Student> Students => StudentsSet;
    public DbSet<Teacher> Teachers => Set<Teacher>(); public DbSet<Course> Courses => Set<Course>(); public DbSet<ClassSection> ClassSections => Set<ClassSection>(); IQueryable<ClassSection> IAppDbContext.ClassSections => ClassSections;
    public DbSet<Enrollment> Enrollments => Set<Enrollment>(); IQueryable<Enrollment> IAppDbContext.Enrollments => Enrollments;
    public DbSet<Room> Rooms => Set<Room>(); IQueryable<Room> IAppDbContext.Rooms => Rooms;
    public DbSet<Camera> CamerasSet => Set<Camera>(); public IQueryable<Camera> Cameras => CamerasSet;
    public DbSet<FaceEnrollment> FaceEnrollmentsSet => Set<FaceEnrollment>(); public IQueryable<FaceEnrollment> FaceEnrollments => FaceEnrollmentsSet;
    public DbSet<StudentFaceImage> StudentFaceImagesSet => Set<StudentFaceImage>(); public IQueryable<StudentFaceImage> StudentFaceImages => StudentFaceImagesSet;
    public DbSet<StudentFaceTemplate> StudentFaceTemplatesSet => Set<StudentFaceTemplate>(); public IQueryable<StudentFaceTemplate> StudentFaceTemplates => StudentFaceTemplatesSet;
    public DbSet<Video> VideosSet => Set<Video>(); public IQueryable<Video> Videos => VideosSet;
    public DbSet<AttendancePolicy> AttendancePoliciesSet => Set<AttendancePolicy>(); public IQueryable<AttendancePolicy> AttendancePolicies => AttendancePoliciesSet;
    public DbSet<Session> SessionsSet => Set<Session>(); public IQueryable<Session> Sessions => SessionsSet;
    public DbSet<SessionStudent> SessionStudentsSet => Set<SessionStudent>(); public IQueryable<SessionStudent> SessionStudents => SessionStudentsSet;
    public DbSet<AnalysisJob> AnalysisJobsSet => Set<AnalysisJob>(); public IQueryable<AnalysisJob> AnalysisJobs => AnalysisJobsSet;
    public DbSet<StableIdentity> StableIdentitiesSet => Set<StableIdentity>(); public IQueryable<StableIdentity> StableIdentities => StableIdentitiesSet;
    public DbSet<TrackSegment> TrackSegmentsSet => Set<TrackSegment>(); public IQueryable<TrackSegment> TrackSegments => TrackSegmentsSet;
    public DbSet<IdentityLink> IdentityLinksSet => Set<IdentityLink>(); public IQueryable<IdentityLink> IdentityLinks => IdentityLinksSet;
    public DbSet<AiEventReceipt> AiEventReceiptsSet => Set<AiEventReceipt>(); public IQueryable<AiEventReceipt> AiEventReceipts => AiEventReceiptsSet;
    public DbSet<BehaviorEvent> BehaviorEventsSet => Set<BehaviorEvent>(); public IQueryable<BehaviorEvent> BehaviorEvents => BehaviorEventsSet;
    public DbSet<Alert> AlertsSet => Set<Alert>(); public IQueryable<Alert> Alerts => AlertsSet;
    public DbSet<AlertRule> AlertRulesSet => Set<AlertRule>(); public IQueryable<AlertRule> AlertRules => AlertRulesSet;
    public DbSet<Attendance> AttendanceSet => Set<Attendance>(); public IQueryable<Attendance> Attendance => AttendanceSet;
    public DbSet<StudentSessionSummary> StudentSessionSummariesSet => Set<StudentSessionSummary>(); public IQueryable<StudentSessionSummary> StudentSessionSummaries => StudentSessionSummariesSet;
    public DbSet<ClassSessionSummary> ClassSessionSummariesSet => Set<ClassSessionSummary>(); public IQueryable<ClassSessionSummary> ClassSessionSummaries => ClassSessionSummariesSet;
    public DbSet<AuditLog> AuditLogsSet => Set<AuditLog>(); public IQueryable<AuditLog> AuditLogs => AuditLogsSet;

    Task IAppDbContext.AddAsync<T>(T entity, CancellationToken ct) => Set<T>().AddAsync(entity, ct).AsTask();
    void IAppDbContext.Update<T>(T entity) => Set<T>().Update(entity);

    protected override void OnModelCreating(ModelBuilder b)
    {
        var tableNames = new Dictionary<Type,string> {
            [typeof(User)]="Users",[typeof(Role)]="Roles",[typeof(UserRole)]="UserRoles",[typeof(Faculty)]="Faculties",[typeof(Department)]="Departments",[typeof(StudentClass)]="StudentClasses",[typeof(Student)]="Students",[typeof(Teacher)]="Teachers",[typeof(Course)]="Courses",[typeof(ClassSection)]="ClassSections",[typeof(Enrollment)]="Enrollments",[typeof(Room)]="Rooms",[typeof(Camera)]="Cameras",[typeof(FaceEnrollment)]="FaceEnrollments",[typeof(StudentFaceImage)]="StudentFaceImages",[typeof(StudentFaceTemplate)]="StudentFaceTemplates",[typeof(Video)]="Videos",[typeof(AttendancePolicy)]="AttendancePolicies",[typeof(Session)]="Sessions",[typeof(SessionStudent)]="SessionStudents",[typeof(AnalysisJob)]="AnalysisJobs",[typeof(StableIdentity)]="StableIdentities",[typeof(TrackSegment)]="TrackSegments",[typeof(IdentityLink)]="IdentityLinks",[typeof(AiEventReceipt)]="AiEventReceipts",[typeof(BehaviorEvent)]="BehaviorEvents",[typeof(AlertRule)]="AlertRules",[typeof(Alert)]="Alerts",[typeof(Attendance)]="Attendance",[typeof(StudentSessionSummary)]="StudentSessionSummaries",[typeof(ClassSessionSummary)]="ClassSessionSummaries",[typeof(AuditLog)]="AuditLogs"};
        foreach (var kv in tableNames) b.Entity(kv.Key).ToTable(kv.Value);

        b.Entity<User>().HasIndex(x=>x.UserName).IsUnique(); b.Entity<User>().HasIndex(x=>x.Email).IsUnique().HasFilter("[Email] <> ''");
        b.Entity<Role>().HasIndex(x=>x.Name).IsUnique(); b.Entity<UserRole>().HasKey(x=>new{x.UserId,x.RoleId});
        b.Entity<Faculty>().HasIndex(x=>x.Code).IsUnique(); b.Entity<Department>().HasIndex(x=>x.Code).IsUnique(); b.Entity<StudentClass>().HasIndex(x=>x.Code).IsUnique();
        b.Entity<Student>().Property(x=>x.StudentCode).HasMaxLength(64); b.Entity<Student>().Property(x=>x.FullName).HasMaxLength(256); b.Entity<Student>().Property(x=>x.Email).HasMaxLength(256); b.Entity<Student>().Property(x=>x.AnonymousCode).HasMaxLength(128);
        b.Entity<Student>().HasIndex(x=>x.StudentCode).IsUnique(); b.Entity<Student>().HasIndex(x=>x.AnonymousCode).IsUnique(); b.Entity<Student>().HasIndex(x=>x.Email).IsUnique().HasFilter("[Email] <> ''");
        b.Entity<Teacher>().HasIndex(x=>x.TeacherCode).IsUnique(); b.Entity<Teacher>().HasIndex(x=>x.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
        b.Entity<Course>().HasIndex(x=>x.Code).IsUnique(); b.Entity<ClassSection>().HasIndex(x=>x.Code).IsUnique(); b.Entity<Enrollment>().HasKey(x=>new{x.ClassSectionId,x.StudentId});
        b.Entity<Room>().HasIndex(x=>x.Code).IsUnique(); b.Entity<Camera>().HasIndex(x=>x.Code).IsUnique();
        b.Entity<Faculty>().Property(x=>x.Note).HasColumnType("nvarchar(max)");
        b.Entity<Department>().Property(x=>x.Note).HasColumnType("nvarchar(max)");
        b.Entity<StudentClass>().Property(x=>x.Note).HasColumnType("nvarchar(max)");
        b.Entity<Course>().Property(x=>x.Note).HasColumnType("nvarchar(max)");
        b.Entity<Teacher>().Property(x=>x.Note).HasColumnType("nvarchar(max)");
        b.Entity<ClassSection>().Property(x=>x.Note).HasColumnType("nvarchar(max)");
        b.Entity<Room>().Property(x=>x.Note).HasColumnType("nvarchar(max)");
        b.Entity<Camera>().Property(x=>x.Note).HasColumnType("nvarchar(max)");
        b.Entity<FaceEnrollment>().HasIndex(x=>new{x.StudentId,x.Status}); b.Entity<StudentFaceImage>().HasIndex(x=>x.ObjectKey).IsUnique(); b.Entity<StudentFaceImage>().HasIndex(x=>new{x.StudentId,x.Sha256,x.IsActive}); b.Entity<StudentFaceTemplate>().HasIndex(x=>new{x.StudentId,x.IsActive});
        b.Entity<AttendancePolicy>().HasIndex(x=>x.Code).IsUnique(); b.Entity<Session>().Property(x=>x.RowVersion).IsRowVersion(); b.Entity<SessionStudent>().HasKey(x=>new{x.SessionId,x.StudentId});
        b.Entity<AlertRule>().Property(x=>x.Code).HasMaxLength(64); b.Entity<AlertRule>().Property(x=>x.Name).HasMaxLength(256); b.Entity<AlertRule>().Property(x=>x.BehaviorLabel).HasMaxLength(32); b.Entity<AlertRule>().Property(x=>x.Version).HasMaxLength(32);
        b.Entity<AnalysisJob>().HasIndex(x=>x.CorrelationId).IsUnique(); b.Entity<StableIdentity>().HasIndex(x=>new{x.SessionId,x.StableId}).IsUnique(); b.Entity<AiEventReceipt>().HasIndex(x=>x.ExternalEventId).IsUnique(); b.Entity<BehaviorEvent>().HasIndex(x=>x.ExternalEventId).IsUnique(); b.Entity<Alert>().HasIndex(x=>x.ExternalEventId).IsUnique(); b.Entity<AlertRule>().HasIndex(x=>x.Code).IsUnique(); b.Entity<Attendance>().HasIndex(x=>new{x.SessionId,x.StudentId}).IsUnique(); b.Entity<StudentSessionSummary>().HasIndex(x=>new{x.SessionId,x.StudentId}).IsUnique(); b.Entity<ClassSessionSummary>().HasIndex(x=>x.SessionId).IsUnique();
        foreach(var p in b.Model.GetEntityTypes().SelectMany(t=>t.GetProperties()).Where(p=>p.ClrType==typeof(decimal)||p.ClrType==typeof(decimal?))){p.SetPrecision(8);p.SetScale(5);}
    }
}
