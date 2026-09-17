# Checklist chạy bản Angular

1. Mở terminal tại project.
2. Nếu DB hiện tại chưa có Note, chạy `database/sqlserver/005_add_catalog_notes.sql` trong database `HTBAM`.
3. Build sạch:
   ```powershell
   cd infra
   docker compose down
   docker compose build --no-cache web backend
   docker compose up -d --force-recreate
   docker compose ps
   ```
4. Mở `http://127.0.0.1:5173` bằng InPrivate/Incognito lần đầu.
5. Test ADMIN:
   - Login
   - Sinh viên: dropdown 3 trạng thái
   - + Thêm sinh viên
   - Import CSV
   - Danh mục > + Thêm Khoa: modal + Ghi chú
   - Bộ môn/Lớp/Môn/GV/LHP/Phòng/Camera: modal + Ghi chú
   - Video preview
   - Buổi học
   - Alert
   - User & quyền
6. Nếu frontend không build, gửi log `docker compose build --no-cache web`.
7. Nếu backend không build, gửi log `docker compose build --no-cache backend`.
