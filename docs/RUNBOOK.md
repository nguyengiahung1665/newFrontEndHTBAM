# Runbook - HTBAM v3-complete

## A. Lần chạy đầu trên máy của bạn

### 1. SQL Server
Tạo database trống `HTBAM`, sau đó chạy **đúng thứ tự**:
```text
database/001_schema.sql
database/002_seed.sql
database/003_dev_sample.sql   # tùy chọn, nên chạy khi test
```
Không chạy `database/legacy/004_upgrade_v1_to_v2.sql` vì bạn chưa có DB v1/v2.

### 2. MinIO
Có thể dùng Docker Compose hoặc MinIO local. Cấu hình endpoint/access key/secret/bucket phải khớp `appsettings.json`/environment.
Bucket `htbam-private` được backend tạo nếu `ObjectStorage:EnsureBucketOnStartup=true` và giữ private.

### 3. AI contract service
```bash
cd services/ai
python -m venv .venv
# Windows:
.venv\Scripts\activate
pip install -r requirements.txt
uvicorn app.main:app --reload --port 8001
```
AI hiện ở contract-only, model capabilities = false.

### 4. Backend
Yêu cầu .NET 9 SDK:
```bash
cd services/backend/src
dotnet restore
dotnet build HTBAM.Api/HTBAM.Api.csproj
dotnet run --project HTBAM.Api
```
Fresh DB đã được tạo bằng SQL script nên `Database:EnsureCreated=false` là đúng.

### 5. Web
```bash
cd apps/web
npm install
npm run build
npm run dev
```
Vite development proxy mặc định gọi backend tại `http://localhost:5000`.

### 6. Login demo
- admin / Admin@123456
- lecturer / Lecturer@123456

## B. Luồng test không cần model
1. Login admin.
2. Quản lý Khoa/Bộ môn/Lớp/Môn/GV/Phòng/Camera.
3. Tạo/import sinh viên.
4. Thiết lập roster lớp học phần.
5. Upload video hoặc khai báo/test camera.
6. Tạo Face Enrollment, upload đủ ảnh FRONT/LEFT/RIGHT; submit -> `PENDING_AI`.
7. Tạo Session, chọn đúng một nguồn Camera hoặc Video, chọn attendance policy.
8. Kiểm tra dashboard/capability: AI model phải hiển thị chưa sẵn sàng.
9. Start Session phải bị chặn với thông báo model inference chưa tích hợp — đây là hành vi đúng ở giai đoạn hiện tại.
10. Kiểm tra history/report/search/export trên dữ liệu đã có hoặc sau khi AI production được nối.

## C. Sau khi model train xong
Thay contract-only engine trong `services/ai` bằng production adapters theo `docs/MODEL_INTEGRATION.md`. Không thay schema chính/API Web.
