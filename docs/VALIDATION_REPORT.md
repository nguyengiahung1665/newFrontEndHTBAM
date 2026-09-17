# Validation Report - HTBAM Web System v3-complete

## Đã kiểm tra trong môi trường tạo artifact
- `tests/structure_check.py`: PASS.
- `tests/contract_check.py`: PASS.
- `tests/static_syntax_check.py`: PASS.
- `tests/workflow_check.py`: PASS.
- `tests/requirements_check.py`: PASS.
- `tests/frontend_syntax_check.js`: PASS, parse/transpile syntax toàn bộ TS/TSX bằng TypeScript 5.8.3.
- Python source compile tĩnh: PASS thông qua static test.
- JSON parse và csproj XML parse: PASS.
- DB fresh schema có đầy đủ bảng v3; legacy upgrade tách khỏi fresh scripts.
- Kiểm tra source cho các luồng: private face/video storage, face hash/magic bytes, PENDING_AI, model capability gate, Session finalize/retry, AI event idempotency, UNKNOWN/conflict/backfill, alert rule, attendance interval/policy, report/export, RBAC scope.

## Lỗi phát hiện và đã sửa trong lượt rà cuối
- Sửa query cảnh báo của Lecturer để không tham chiếu navigation `Alert.Session` không tồn tại; dùng allowed Session IDs.
- Tách `legacy/004_upgrade_v1_to_v2.sql` khỏi luồng fresh DB.
- Đồng bộ schema/entity/DbContext cho Faculty, Department, StudentClass, AttendancePolicy và các field v3.
- Bổ sung Vite client type declaration cho `import.meta.env`.
- Sửa React `useEffect` không trả Promise ở các trang Users/Audit/Policies/System/Videos và khởi tạo `useRef` timer đúng kiểu trên realtime dashboard.
- AI stub chuyển thành contract-only và không thể tạo Session inference/Face template giả.

## Chưa thể xác nhận trong môi trường hiện tại
- Không có .NET SDK -> chưa chạy `dotnet restore/build` thực tế.
- Không có Docker -> chưa chạy full `docker compose up` end-to-end.
- Môi trường không có Internet/cache npm đầy đủ -> `npm install --offline` thất bại vì thiếu `@microsoft/signalr`, nên chưa thể tuyên bố `npm run build` dependency-aware PASS.

Do đó trạng thái chính xác: **source đã PASS bộ static/contract/workflow/requirements checks; cần chạy ba lệnh runtime trên máy Windows của dự án trước khi coi là deployment PASS: `dotnet build`, `npm install && npm run build`, `docker compose up` (nếu dùng Docker).**

## Ghi chú model
Các capability AI production cố ý = false. Đây không phải lỗi thiếu chức năng Web; đó là gate để chờ model thật và tránh sinh kết quả giả trước khi train xong.
