# Mapping nghiệp vụ -> source v2

| Nghiệp vụ | Backend | Database/Object Storage | Web |
|---|---|---|---|
| Đăng nhập/RBAC | AuthController/TokenService | Users/Roles/UserRoles | LoginPage |
| Sinh viên | StudentsController | Students | StudentsPage |
| Face Enrollment | FaceEnrollmentController/Service | FaceEnrollments, StudentFaceImages + MinIO | FaceEnrollmentPage |
| Face AI callback | AiFaceEnrollmentController | StudentFaceTemplates + MinIO template | trạng thái trên FaceEnrollmentPage |
| Danh mục/roster | CatalogsController | Courses/Teachers/ClassSections/Enrollments/Rooms/Cameras | Session form dùng class/room/camera/roster |
| Video | VideosController | Videos + MinIO | VideosPage |
| Buổi học | SessionsController/SessionService | Sessions/SessionStudents | SessionsPage |
| AI job | AiClient/SessionService | AnalysisJobs | SessionsPage/SessionDashboardPage |
| Stable identity | AiEventsController/AiEventService | StableIdentities/TrackSegments/IdentityLinks | SessionDashboardPage |
| Idempotency AI | AiEventService | AiEventReceipts | - |
| Hành vi | AiEventService | BehaviorEvents | Dashboard/Reports |
| Cảnh báo | AlertEngine/AlertsController | Alerts/AlertRules | AlertsPage |
| Chuyên cần | SummaryService | Attendance | ReportsPage |
| Báo cáo | ReportsController | StudentSessionSummaries/ClassSessionSummaries | ReportsPage |
| Audit | AuditService + controllers | AuditLogs | API sẵn; UI audit có thể bổ sung ở pha quản trị |

Xem luồng chi tiết tại `WORKFLOWS_AND_STORAGE.md`.
