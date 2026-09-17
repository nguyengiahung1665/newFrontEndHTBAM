# HTBAM – Angular Migration

Project này được tạo trực tiếp từ bản HTBAM người dùng cung cấp. Frontend React/Vite trong `apps/web` đã được thay bằng Angular, trong khi giữ nguyên kiến trúc backend/AI/database/storage.

## Stack mới

- Angular 20 + TypeScript
- Standalone Components
- Angular Router + route guards
- Reactive Forms + Angular Signals + RxJS
- HttpInterceptor cho JWT
- ASP.NET Core Web API
- SQL Server
- MinIO
- FastAPI AI service
- Nginx khi chạy container
- `@microsoft/signalr` đã giữ trong dependencies để tiếp tục tích hợp realtime

## Các phần đã chuyển sang Angular

- Đăng nhập/JWT
- Layout/sidebar/navigation
- Dashboard
- Sinh viên
  - tìm kiếm
  - dropdown `Tất cả / Hoạt động / Ngừng hoạt động`
  - thêm/sửa bằng modal
  - ngừng/kích hoạt
  - import CSV + hiển thị lỗi theo dòng
  - tải CSV mẫu
  - Face Enrollment
- Danh mục & roster
  - Khoa
  - Bộ môn
  - Lớp sinh hoạt
  - Môn học
  - Giảng viên
  - Lớp học phần
  - Phòng học
  - Camera
  - form thêm/sửa bằng modal
  - trường `Ghi chú`
  - roster lớp học phần
- Video upload/preview/delete
- Buổi học + dashboard chi tiết
- Cảnh báo
- Lịch sử
- Báo cáo
- Tìm kiếm
- Chính sách chuyên cần
- Trạng thái hệ thống
- Tài khoản
- Người dùng & quyền
- Audit log

## Backend/database đã đồng bộ thêm

Bản Angular này cũng đã bổ sung các thay đổi cần thiết để UI mới không chỉ là giao diện:

- `Note` cho 8 danh mục:
  - Faculties
  - Departments
  - StudentClasses
  - Courses
  - Teachers
  - ClassSections
  - Rooms
  - Cameras
- EF Core map `Note` thành `nvarchar(max)`.
- API catalog đọc/ghi `Note`.
- Script idempotent:
  - `database/005_add_catalog_notes.sql`
  - `database/sqlserver/005_add_catalog_notes.sql`
- API sinh viên hỗ trợ filter `status=all|active|inactive` và vẫn giữ tương thích `includeInactive`.
- Import CSV sinh viên chạy transaction bên trong EF execution strategy để tương thích `EnableRetryOnFailure`.

## Chạy project

Từ thư mục project:

```powershell
cd infra
docker compose down
docker compose build --no-cache web backend
docker compose up -d --force-recreate
docker compose ps
```

Mở:

```text
http://127.0.0.1:5173
```

## Database hiện hữu

Nếu database HTBAM đã tồn tại từ trước, chỉ chạy migration:

```text
database/sqlserver/005_add_catalog_notes.sql
```

Không cần chạy lại `001_schema.sql` lên database đang có dữ liệu.

## Kiểm thử tại môi trường tạo artifact

- Đã chạy kiểm tra cú pháp TypeScript bằng TypeScript parser: PASS.
- Môi trường tạo artifact không có .NET SDK nên chưa thể chạy `dotnet build` tại đây.
- Môi trường cũng không truy cập được npm registry nên chưa thể thực hiện `npm install/ng build` thực tế tại đây.
- Dockerfile và Compose đã được giữ theo topology cũ để bạn có thể build trực tiếp trên máy Windows đang có Docker/internet package cache.

## Nâng cấp tiếp theo nên làm

1. Sinh Angular API client tự động từ OpenAPI/Swagger của ASP.NET Core.
2. Dùng SignalR thật cho tiến độ xử lý session, alert và AI result realtime.
3. Thêm Apache ECharts cho timeline hành vi, heatmap, stacked chart và dashboard báo cáo.
4. Playwright E2E cho ADMIN / LECTURER / TECH_AI.
5. Serilog + OpenTelemetry cho log/tracing.
6. Khi xử lý video AI dài hoặc nhiều job song song: RabbitMQ + Background Worker.
7. Có thể thêm PrimeNG sau khi chức năng ổn định để chuẩn hóa Dialog/Table/Dropdown/Toast mà không làm phức tạp giai đoạn migrate ban đầu.
