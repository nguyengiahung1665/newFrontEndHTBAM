# Requirements Traceability

Bản v3-complete được rà theo hai nguồn: các tài liệu KLCN/HTBAM đã chốt và danh sách yêu cầu Web được xác nhận sau đó.

## Chức năng Web
- Quản trị người dùng -> `AuthController`, `AdminUsersController`, Account/Admin UI.
- Khoa/lớp/môn/GV/SV/phòng/camera -> `CatalogsController`, `StudentsController`, Management/Students UI.
- Lớp học & buổi học -> ClassSection + roster + `SessionsController` + Sessions UI.
- Video -> `VideosController` + MinIO + Videos UI.
- stable_id và liên kết hồ sơ -> `StableIdentities`, `TrackSegments`, `IdentityLinks`, `AiEventService`.
- 4 trạng thái -> `BehaviorEvents`, dashboard/report data contract; inference thật để sau.
- Realtime -> SignalR `SessionHub` + dashboard snapshot fallback.
- Cảnh báo -> `AlertRules` + `Alerts` + Alerts UI.
- Báo cáo SV/lớp/buổi -> `ReportsController` + Reports UI.
- Chuyên cần -> `AttendancePolicies`, `Attendance`, summary finalize.
- Excel/PDF -> ClosedXML/QuestPDF endpoints.
- Tìm kiếm -> `SearchController` + Search UI.
- Nhận xét tự động -> report auto-comment endpoints; Google Gemini qua `ILlmProvider`; rule-based fallback khi tắt/thiếu key/lỗi provider.

## Cơ sở lý thuyết / thuật toán
Các thư mục tích hợp đã dành sẵn cho YOLO, OpenCV, MediaPipe, GRU, Transformer, HTBAM, KMeans, Intelligent KMeans và pseudo-label. Không đưa các thuật toán training/research này vào backend nghiệp vụ hoặc chạy giả ở runtime.

## Flutter
Không hiện thực trong bản này theo phạm vi hiện tại. API không phụ thuộc React để có thể tái sử dụng cho Flutter sau.
