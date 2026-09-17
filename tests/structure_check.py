from pathlib import Path
import json, sys
root=Path(__file__).resolve().parents[1]
required=[
'apps/web/src/App.tsx','apps/web/src/pages/FaceEnrollmentPage.tsx','apps/web/src/pages/SessionDashboardPage.tsx','apps/web/src/pages/VideosPage.tsx',
'apps/web/src/pages/ManagementPage.tsx','apps/web/src/pages/ReportsPage.tsx','apps/web/src/pages/HistoryPage.tsx','apps/web/src/pages/PoliciesPage.tsx','apps/web/src/pages/AdminUsersPage.tsx','apps/web/src/pages/AuditPage.tsx',
'services/backend/src/HTBAM.Domain/Entities/Entities.cs','services/backend/src/HTBAM.Application/Services/SessionServices.cs',
'services/backend/src/HTBAM.Application/Services/FaceEnrollmentService.cs','services/backend/src/HTBAM.Infrastructure/Data/AppDbContext.cs',
'services/backend/src/HTBAM.Infrastructure/Services/MinioObjectStorage.cs','services/backend/src/HTBAM.Api/Program.cs',
'services/backend/src/HTBAM.Api/Controllers/ReportsController.cs','services/backend/src/HTBAM.Api/Controllers/CatalogsController.cs',
'services/ai/app/main.py','database/001_schema.sql','database/002_seed.sql','database/003_dev_sample.sql','database/legacy/004_upgrade_v1_to_v2.sql','docs/MODEL_INTEGRATION.md']
missing=[x for x in required if not (root/x).exists()]
if missing: print('MISSING',missing);sys.exit(1)
json.loads((root/'apps/web/package.json').read_text())
schema=(root/'database/001_schema.sql').read_text()
for table in ['Faculties','Departments','StudentClasses','Students','Teachers','Courses','ClassSections','Enrollments','Rooms','Cameras','FaceEnrollments','StudentFaceImages','StudentFaceTemplates','Videos','AttendancePolicies','Sessions','SessionStudents','StableIdentities','TrackSegments','IdentityLinks','AiEventReceipts','BehaviorEvents','AlertRules','Alerts','Attendance','StudentSessionSummaries','ClassSessionSummaries','AnalysisJobs','AuditLogs']:
    assert f'CREATE TABLE {table}' in schema, table
entities=(root/'services/backend/src/HTBAM.Domain/Entities/Entities.cs').read_text()
for name in ['Faculty','Department','StudentClass','Student','Teacher','Course','ClassSection','FaceEnrollment','StudentFaceImage','StudentFaceTemplate','AttendancePolicy','Session','SessionStudent','StableIdentity','TrackSegment','IdentityLink','AiEventReceipt','BehaviorEvent','Alert','Attendance','StudentSessionSummary','ClassSessionSummary','AnalysisJob','AuditLog']:
    assert f'class {name}' in entities, name
compose=(root/'infra/docker-compose.yml').read_text()
for service in ['sqlserver:','minio:','ai:','backend:','web:']: assert service in compose,service
print('STRUCTURE_OK')
