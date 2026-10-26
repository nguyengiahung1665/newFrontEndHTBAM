# Kết quả sửa HTBAM — 25/10/2026

Mã nguồn frontend, backend, migration và kiểm thử đã được triển khai. Image backend/web đã build thành công. **Chưa áp dụng migration 014 lên database HTBAM hiện có và chưa thay container backend/web tại cổng 5173**, vì bộ duyệt tự động từ chối thao tác này và yêu cầu người dùng phê duyệt trực tiếp.

Bản Angular production để xem trước: http://127.0.0.1:5174. Preview dùng API hiện có để đọc dữ liệu; chỉ cho phép GET và đăng nhập, chặn các yêu cầu ghi khác. Các popup trong kiểm thử trình duyệt đều được đóng mà không lưu. Kiểm thử lưu dữ liệu/cấp mã chạy trên database SQL Server tạm riêng.

## Nhận diện HUIT

- Asset: `apps/web/public/assets/huit-logo.png`.
- Dùng tại `layout/shell.component.ts` và `features/login.component.ts`, gồm cả nhận diện đăng nhập trên mobile.
- Sao chép nguyên PNG người dùng cung cấp ở ảnh `19_18_28`; SHA256 bản gốc và asset đều là `D101302172C4AE8B5A0035A9169EEDD2E2FC03FF05570110D067F51957158253`. Không chuyển định dạng, không mất alpha, không cắt chữ/biểu tượng. Ảnh gốc không có khoảng trắng lớn cần crop.
- Logo 52×52px khi sidebar mở; 40×40px khi thu gọn. Dùng `object-fit: contain`, alt text đầy đủ của trường. Logo con mắt đã được thay khỏi nhận diện chính.
- Đã xác minh HTTP 200 cho `/assets/huit-logo.png` và nội dung tải xuống trùng asset trong container Nginx tạm của image mới. Container đang phục vụ cổng 5173 chưa bị thay thế.

## Thông báo DEMO

Gỡ banner chung trong `.content` của shell, nên không lặp lại ở các trang con và không chừa vùng trống dành cho banner. Các nhãn DEMO gắn với từng bản ghi và hướng dẫn học vụ vẫn được giữ.

`NotificationCenterComponent` hiển thị chuông cho các vai trò đăng nhập, badge chưa đọc, tiêu đề/nội dung đầy đủ, thời gian, mức Thông tin, trạng thái đọc, đánh dấu từng mục/tất cả, đường dẫn dữ liệu DEMO khi có quyền, và trạng thái rỗng. Panel có giới hạn chiều cao, cuộn, đóng bằng click ngoài, chuyển focus ra ngoài và Escape; mở bằng bàn phím được.

Thông báo dùng khóa cố định `system-demo-data-v1`. Trạng thái đã đọc và thời điểm ghi nhận đầu tiên được lưu trong localStorage riêng theo user ID. Reload không tạo thêm mục. Đây là cấu hình cảnh báo hệ thống dùng chung, không lưu notification vào database, không lấy dữ liệu nhạy cảm để lọc ở frontend. Khi backend không trả `developmentDataWarning`, mục DEMO và đường dẫn DEMO không xuất hiện. Các cảnh báo nghiệp vụ theo lớp vẫn dùng API và phạm vi quyền hiện có.

Giới hạn: trạng thái đã đọc của thông báo chung được lưu theo người dùng trên trình duyệt hiện tại, chưa đồng bộ giữa các thiết bị.

## Form, bảng Môn học và ngày

Các popup đã bỏ hiển thị/nhập mã, kể cả ô readonly và mã dự kiến: Khoa, Bộ môn, Lớp sinh hoạt, Môn học, Giảng viên, Phòng học, Camera, Lớp học phần, chính sách chuyên cần, phiên bản giờ học. Đã bỏ control/payload mã ẩn danh khỏi form Sinh viên. Tài khoản và Cơ cấu/phân quyền không có ô nhập mã nghiệp vụ cần thêm.

**Ngoại lệ MSSV chính thức:** hệ thống hiện dùng MSSV nguồn của nhà trường. Form tạo Sinh viên vẫn yêu cầu MSSV chính thức; popup sửa không cho nhập lại. Backend giữ nguyên MSSV và mã ẩn danh khi sửa, kể cả client gửi giá trị khác. Import CSV giữ MSSV/mã nguồn chính thức, không áp mã DEMO lên sinh viên chính thức. Seed DEMO mới tự cấp mã riêng.

Form danh mục không gửi Code/TeacherCode. Backend cấp mã khi tạo và không gán lại mã khi sửa. Các mã cũ dạng GUID được thay bằng nhãn “Mã cũ” tại những danh sách/lookup liên quan; giá trị database được giữ nguyên.

Bảng Môn học có: Tên môn học, Bộ môn, Số tín chỉ, Năm học bắt đầu, Học kỳ bắt đầu, Ghi chú, Trạng thái, Thao tác. Mobile ẩn cột Ghi chú. Tìm theo tên, bộ môn, năm học và học kỳ. Lookup chọn Môn học hiển thị tên thay vì mã.

Form Môn học bố trí tên toàn hàng, bộ môn/tín chỉ tỷ lệ 2:1, năm học/học kỳ 1:1, ghi chú toàn hàng, hoạt động và footer. Mobile chuyển một cột. Năm học được chuẩn hóa khoảng trắng/dấu nối và kiểm tra hai năm liên tiếp; học kỳ nhận 1/2/HK1/HK2, chuẩn hóa HK1/HK2. Backend kiểm tra lại bằng `CourseStartPeriod`.

`DateInputComponent` dùng chung cho tất cả ô ngày đang có trong cấu hình học vụ, giai đoạn, mốc Tuần 1, ngày nghỉ, phiên bản giờ, lịch lớp học phần và bộ lọc nhật ký. Không còn `input type="date"` trong frontend.

Control hỗ trợ ngModel/ControlValueAccessor, min/max/required/disabled/placeholder/errorMessage, nhập tay và lịch tháng. Parse/format dùng chung trong `date-only.ts`: nhận `1/2/2026`, hiển thị `01/02/2026`, xuất ISO ngày `2026-02-01`, kiểm tra ngày thực/năm nhuận bằng số nguyên, không parse chuỗi Việt bằng `new Date(string)` và không chuyển ngày thuần qua UTC. Các cặp ngày có lỗi ngay dưới trường khi đảo khoảng ngày; form không gửi nếu control ngày không hợp lệ. Lịch đặt theo viewport/footer, hỗ trợ Enter/Space, Escape, Tab và phím mũi tên. Bộ lọc nhật ký chuyển mốc ngày sang thời điểm theo UTC+7 rõ ràng.

## Quy tắc mã mới

| Loại | Quy tắc / ví dụ |
|---|---|
| Khoa | `KHOA-CNTT`; trùng viết tắt dùng hậu tố `-02`, `-03` |
| Bộ môn | `BM-CNDL`; trùng viết tắt dùng `BM-CNDL-02` |
| Lớp sinh hoạt | `LSH-2026-CNDL-01`; phải có khóa và bộ môn thuộc khoa |
| Môn học | `MH-CNDL-001`; bộ đếm riêng theo bộ môn |
| Giảng viên | `GV-CNDL-0001`; bộ đếm riêng theo bộ môn |
| Phòng học | `PH-CS1-A-101` hoặc `PH-ONLINE-ZOOM48`; yêu cầu metadata, không tự đoán |
| Camera | `CAM-CS1-A-101-01`; lấy phạm vi từ phòng có mã hợp lệ |
| Lớp học phần | `LHP-2627-HK1-CNDL-001-01`; bỏ tiền tố MH khi ghép |
| Buổi học | `BH-2627-HK1-CNDL-001-01-001`; chỉ sinh từ lịch, không thêm thủ công |
| Sinh viên DEMO | `SV-DEMO-00001`; chỉ seed DEMO, giữ MSSV nguồn của sinh viên chính thức |
| Mã ẩn danh nội bộ | `SV-ANON-000001` |
| Chính sách chuyên cần | `CC-00001` |
| Phiên bản giờ | `GIO-A-0001` / `GIO-B-0001` |

Mã mới viết hoa, bỏ dấu Việt, dùng chữ/số/gạch ngang, không ghép GUID/timestamp hay tên/chức vụ giảng viên. Đổi tên, chức vụ, năm bắt đầu hoặc học kỳ bắt đầu không sinh lại mã.

Việc cấp số dùng transaction Serializable và `sp_getapplock` độc quyền cùng phạm vi `HTBAM:academic-scheduling`. Có `CourseCodeCounters`, `LecturerCodeSequences`, `ClassSectionCounters` và `BusinessCodeSequences`. Khởi tạo/đối chiếu số cũ cũng nằm trong transaction đã khóa; unique index bảo vệ ở database. Vượt giới hạn số trả lỗi, không tạo mã tạm. Import sinh viên dùng cùng khóa khi cấp mã ẩn danh.

Seed mới dùng quy tắc mới và có đủ năm/học kỳ bắt đầu cho môn mới. `CatalogSeedIdentities` ghi tham chiếu ổn định để chạy lại không phụ thuộc tên/mã; seed nhận lại bản ghi cũ, không đổi mã đã cấp và không tự điền năm/học kỳ cho môn cũ.

## Migration và dữ liệu hiện có

Migration mới duy nhất: `database/sqlserver/014_course_period_and_business_sequences.sql`, có bản mirror tương ứng tại `database/014_course_period_and_business_sequences.sql`.

- Courses thêm `StartYear`, `EndYear`, `StartSemester`, ban đầu nullable để giữ dữ liệu cũ. Check constraint kiểm tra năm liên tiếp và HK1/HK2. UI ghi “Chưa thiết lập”; sửa môn cũ phải bổ sung trước khi lưu.
- Thêm `BusinessCodeSequences`; Sessions thêm Code nullable và unique filtered index, không gán lại mã cho buổi cũ.
- Mở rộng khóa seed cho Khoa/Lớp sinh hoạt/Phòng/Sinh viên bằng cột nullable, FK, unique filtered index và ràng buộc đúng một đích.
- Các unique index mã danh mục khác đã có trong migration 001/008/012/013, được giữ lại. Không sửa migration cũ.
- Runner thêm script 014; cơ chế chờ database ONLINE, retry SQL 4060 và exit code lỗi 1/2 được giữ nguyên.

Hai snapshot mã trước/sau kiểm thử bằng nhau: `.runtime-fix-backup-huit-ui/legacy-codes-before.txt` và `legacy-codes-after.txt`. Mọi mã cũ, gồm CNPM, HTBAM-DEMO, mã MH dạng GUID, mã khoa/bộ môn/phòng/sinh viên DEMO đời cũ và các mã nguồn import, vẫn được giữ. Không reset/stash/clean/commit/push, không xóa database hoặc Docker volume.

**Chưa thực hiện:** migration/seed trên volume đang dùng và thay container backend/web cổng 5173. Bộ duyệt tự động đã từ chối hai lần vì xem đây là thay đổi schema bền vững cần người dùng phê duyệt trực tiếp; bước khởi động lại có thể làm gián đoạn ngắn backend/web. Snapshot và image đã sẵn sàng để thực hiện sau phê duyệt.

## Kiểm thử thực tế

| Kiểm tra | Kết quả |
|---|---|
| Backend unit/integration, có SQL Server | 200 passed, 0 failed, 1 skipped / 201 |
| Test bị skip | Workbook HUIT thật 10.202 dòng/41 cột: thiếu tệp nguồn riêng của test |
| Frontend behavior/unit | 40 passed, 0 failed |
| TypeScript/row actions, nguồn lịch HUIT, SQL mirrors | Passed |
| Angular production build | Passed, local và Docker |
| Docker Compose build backend/web | Passed |
| Nginx image mới phục vụ logo qua HTTP | 200, file tải xuống trùng asset |
| Browser preview desktop/mobile | 220 checks, 13 tài khoản DEMO, 10 ảnh, không có pageerror |
| Database HTBAM hiện có | Mã trước/sau không đổi; chưa chạy migration 014 |
| git diff --check | Passed |
| Tự rà soát thay đổi | Đã đọc diff và các file mới, sửa lỗi hợp lệ, chạy lại frontend/backend/SQL/browser tests liên quan |

Test SQL dùng database tạm riêng để kiểm tra migration lặp lại/nâng cấp, ràng buộc năm/học kỳ, nullable cho dữ liệu cũ, cấp mã đồng thời, rollback, unique index và seed lặp lại. Không dùng database HTBAM hiện có làm nơi ghi dữ liệu thử.

Browser chạy 1440×1000 và 390×844 cho admin, dean, head1–head3, lecturer1–lecturer6, editor và viewer. Đã kiểm tra logo 52px/40px, sidebar mobile, chuông/badge/đọc/reload/Escape/click ngoài/trạng thái không DEMO, cột Môn học, form năm/học kỳ, nhập ngày/chọn lịch/lỗi ngày/lỗi khoảng ngày/footer và tràn màn hình. Đã tự xem ảnh desktop/mobile; sửa lỗi header che nút thu gọn và Escape khi focus chưa vào panel.

Ảnh/report browser: `.runtime-fix-backup-huit-ui/browser/`. Test script giữ trong `tests/huit_identity_browser_smoke.cjs`; chỉ đăng nhập/đọc và đóng popup, không tạo bản ghi catalog. Báo cáo backend: `services/backend/tests/HTBAM.Tests/TestResults/huit-sql.trx`.

Giới hạn xác minh: chưa kiểm thử lưu từ trình duyệt đến backend mới trên volume hiện có, vì bước áp migration/khởi động lại bị chặn. Lưu/tải lại trường mới và chống Code giả đã kiểm thử ở backend/database tạm; UI được kiểm tra riêng với dữ liệu đọc từ backend hiện có. Lịch chính thức và năm/học kỳ bắt đầu của dữ liệu cũ chưa được cung cấp; không tự suy đoán.

## File đã thay đổi/thêm

- `apps/web/public/assets/huit-logo.png`
- `apps/web/src/app/core/models.ts`
- `apps/web/src/app/features/academic-configuration.component.ts`
- `apps/web/src/app/features/audit.component.ts`
- `apps/web/src/app/features/class-sections.component.ts`
- `apps/web/src/app/features/login.component.ts`
- `apps/web/src/app/features/management.component.ts`
- `apps/web/src/app/features/organization.component.ts`
- `apps/web/src/app/features/policies.component.ts`
- `apps/web/src/app/features/students.component.ts`
- `apps/web/src/app/layout/shell.component.ts`
- `apps/web/src/app/shared/date-input.component.ts`
- `apps/web/src/app/shared/date-only.ts`
- `apps/web/src/app/shared/notification-center.component.ts`
- `apps/web/src/app/shared/search-select.component.ts`
- `apps/web/src/app/shared/ui.ts`
- `apps/web/src/styles.scss`
- `database/014_course_period_and_business_sequences.sql`
- `database/sqlserver/014_course_period_and_business_sequences.sql`
- `docs/HUIT_IDENTITY_NOTIFICATIONS_AND_FORMS.md`
- `services/backend/src/HTBAM.Api/Controllers/AcademicConfigurationController.cs`
- `services/backend/src/HTBAM.Api/Controllers/AttendancePoliciesController.cs`
- `services/backend/src/HTBAM.Api/Controllers/CatalogsController.cs`
- `services/backend/src/HTBAM.Api/Controllers/StudentsController.cs`
- `services/backend/src/HTBAM.Api/Support/ApiExceptionMiddleware.cs`
- `services/backend/src/HTBAM.Api/Support/BusinessCodes.cs`
- `services/backend/src/HTBAM.Api/Support/ClassSchedulePlanner.cs`
- `services/backend/src/HTBAM.Api/Support/DemoDataSeeder.cs`
- `services/backend/src/HTBAM.Application/DTOs/AcademicContracts.cs`
- `services/backend/src/HTBAM.Application/Services/ClassSectionCodes.cs`
- `services/backend/src/HTBAM.Application/Services/CourseStartPeriod.cs`
- `services/backend/src/HTBAM.Domain/Entities/BusinessCodeSequence.cs`
- `services/backend/src/HTBAM.Domain/Entities/CourseCodeCounter.cs`
- `services/backend/src/HTBAM.Domain/Entities/Entities.cs`
- `services/backend/src/HTBAM.Infrastructure/Data/AppDbContext.cs`
- `services/backend/src/HTBAM.Infrastructure/Data/SqlMigrationRunner.cs`
- `services/backend/tests/HTBAM.Tests/AcademicSqlServerTests.cs`
- `services/backend/tests/HTBAM.Tests/CatalogCodeTests.cs`
- `services/backend/tests/HTBAM.Tests/ClassSectionImportSqlTests.cs`
- `services/backend/tests/HTBAM.Tests/CoursePeriodAndNumberingTests.cs`
- `services/backend/tests/HTBAM.Tests/FeatureRegressionTests.cs`
- `services/backend/tests/HTBAM.Tests/LecturerCodeTests.cs`
- `tests/class_section_ui_check.js`
- `tests/frontend_syntax_check.js`
- `tests/huit_identity_browser_smoke.cjs`
- `tests/search_select_check.js`
