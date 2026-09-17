# Feature Matrix - Web scope hiện tại

Legend: ✅ đã hiện thực Web/API/DB; ⏳ contract/UI/DB đã sẵn sàng nhưng cần model/provider thật.

| Nhóm | Chức năng | Trạng thái |
|---|---|---|
| Auth | Login/logout/đổi mật khẩu/JWT/RBAC Admin-Lecturer-Tech | ✅ |
| User | Danh sách, tạo, sửa role, lock/inactive, reset password | ✅ |
| Danh mục | Khoa, bộ môn, lớp sinh hoạt, môn, GV, lớp học phần, phòng, camera | ✅ |
| Student | CRUD, search, soft-delete/reactivate, import CSV | ✅ |
| Roster | Xem/thay roster, import MSSV CSV | ✅ |
| Camera | RTSP metadata, health TCP test, active/inactive | ✅ |
| Video | Upload private MinIO, preview presigned, delete có kiểm tra tham chiếu | ✅ |
| Face enrollment | Batch ảnh nhiều pose, validation/hash/private MinIO, preview, submit/cancel | ✅ |
| Face AI | quality + ArcFace template thật | ⏳ model |
| Session | create/edit/cancel, roster snapshot, camera XOR video, schedule validation | ✅ |
| Session lifecycle | STARTING/RUNNING/FINALIZING/FINALIZE_FAILED/COMPLETED + retry | ✅ |
| Realtime | SignalR hub + polling snapshot fallback | ✅ |
| Detection/tracking | YOLO + BoT-SORT inference thật | ⏳ model |
| Identity | stable_id/track/evidence/UNKNOWN/conflict/backfill storage & callback logic | ✅ |
| Identity inference | ArcFace + OSNet + re-entry resolver thật | ⏳ model |
| Behavior | BehaviorEvent state contract + overlap guards | ✅ |
| Behavior inference | MediaPipe + 14 features + HTBAM GRU/Transformer + smoothing | ⏳ model |
| Alert | alert rules duration/confidence/quality; ACK/CLOSE/REOPEN | ✅ |
| Attendance | policy configurable; union track intervals; valid-observed time | ✅ |
| History | behavior/attendance/identity evidence/student history | ✅ |
| Reports | Session/Student/ClassSection + attention list + timeline | ✅ |
| Export | Excel/PDF cho Session/Student/Class | ✅ |
| Search | SV/lớp/buổi/behavior/time | ✅ |
| Comment | UI/API + Google Gemini provider + deterministic fallback | ✅ |
| Audit | login/admin/config/session/face actions + query UI | ✅ |
| UI | responsive management/dashboard/history/report/admin pages | ✅ |
| Mobile | Flutter | ngoài phạm vi hiện tại |
| Research pipeline | KMeans/Intelligent KMeans/pseudo-label integration slots | ⏳ model/data pipeline |
