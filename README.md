> **v3.1 build fix (2026-08-28):** added `Microsoft.Extensions.Configuration.Binder` to `HTBAM.Infrastructure` so Docker/.NET 9 can compile `IConfiguration.GetValue(...)`. No DB schema change.

# HTBAM Web Management System v3-complete

Bộ source nền chính thức cho KLCN **Hệ thống quản lý và phân tích hành vi sinh viên trong lớp học theo thời gian thực**.

## Phạm vi bản này
Bản này hoàn thiện các phần **không phụ thuộc model đã train**: Web Angular, ASP.NET Core API, SQL Server schema, JWT/RBAC, quản lý danh mục, sinh viên, roster, camera/video, Face Enrollment storage, Session lifecycle, realtime contract/SignalR, stable identity storage, alert/rule, attendance policy, history, reports, Excel/PDF, search, audit và UI responsive.

Các khối AI/CV production vẫn cố ý ở trạng thái contract-only vì model chưa train/tích hợp: YOLO, BoT-SORT, ArcFace, OSNet, Stable Identity Resolver, MediaPipe feature extractor, HTBAM (GRU + Transformer), smoothing và KMeans/Intelligent KMeans/pseudo-label pipeline. LLM Provider là module backend độc lập và đã được tích hợp để diễn giải summary có sẵn; luồng inference **không sinh kết quả AI/CV giả**. Development seed có fixture tổng hợp được gắn nhãn DEMO riêng để kiểm thử báo cáo, không phải kết quả inference thực tế.

Flutter chưa nằm trong phạm vi hiện tại theo yêu cầu triển khai hiện tại; Backend API được giữ reusable để thêm Flutter sau.

## Stack
- Web: Angular + TypeScript
- Backend: ASP.NET Core Web API (.NET 9), EF Core, SignalR
- Database: SQL Server
- Object storage: MinIO private bucket
- AI service contract: Python + FastAPI
- Auth: JWT + ASP.NET PasswordHasher + RBAC
- Export: ClosedXML + QuestPDF
- Infra: Docker Compose

## Cấu trúc
```text
apps/web/                 React UI
services/backend/         Domain/Application/Infrastructure/API
services/ai/              FastAPI contract + integration slots
  app/detection/          YOLO (pending model)
  app/tracking/           BoT-SORT (pending model)
  app/identity/           ArcFace/OSNet/stable resolver (pending model)
  app/features/           MediaPipe/OpenCV feature extraction (pending model)
  app/htbam/              GRU + Transformer inference (pending model)
  app/clustering/         KMeans/Intelligent KMeans research pipeline
  app/weak_labeling/      pseudo-label research pipeline
  app/events/             production event engine slot
  app/inference/          production orchestrator slot
database/                 fresh SQL schema/seed/sample
database/legacy/          upgrade script cũ, KHÔNG dùng cho cài mới
infra/                    Docker Compose
docs/                     workflows, requirements, integration, runbook
tests/                    static/contract/workflow/requirements checks
```

## Docker, database và migration

Từ `infra`: chạy `docker compose build backend web`, rồi `docker compose up -d --force-recreate`.
Compose chờ SQL healthy, chạy migration có lịch sử/checksum và seed trước khi mở backend/web. Giữ volume SQL hiện có; không cần xóa dữ liệu để nâng cấp. Nguồn SQL chuẩn là `database/sqlserver`; script 003 và legacy không được tự chạy.

Xem [DATABASE_MIGRATION_AND_DEVELOPMENT_SEED.md](docs/DATABASE_MIGRATION_AND_DEVELOPMENT_SEED.md) để biết cách chạy lại, tắt seed, tài khoản và kết quả kiểm thử thực tế.

## Tài khoản Development Seeder
Khi lệnh seed chạy với `ASPNETCORE_ENVIRONMENT=Development` và `Seed__Development=true`, có các tài khoản `demo.admin`, `demo.dean`, `demo.head1`–`demo.head3`, `demo.lecturer1`–`demo.lecturer6`, `demo.editor`, `demo.viewer`.
Mật khẩu lấy từ `SEED_DEFAULT_PASSWORD`; fallback cục bộ là `HTBAM-Local-Demo!2026#`, chỉ dành cho Development. Seed không đổi mật khẩu tài khoản đã có. Lịch và báo cáo mang nhãn DEMO; lịch học kỳ mẫu không phải lịch chính thức HUIT.

## Trạng thái AI hiện tại
`GET /capabilities` ở AI service trả:
```json
{"sessionInference":false,"faceEnrollment":false,"loadedModels":[]}
```
Do đó:
- Có thể upload/quản lý ảnh Face Enrollment nhưng dừng tại `PENDING_AI`.
- Có thể tạo/cấu hình Session nhưng backend không cho chuyển `RUNNING` nếu model inference chưa sẵn sàng.
- Không có embedding/prediction/StudentId/behavior giả.

Xem `docs/FEATURE_MATRIX.md`, `docs/WORKFLOWS_AND_STORAGE.md`, `docs/MODEL_INTEGRATION.md`, `docs/RUNBOOK.md` và `docs/VALIDATION_REPORT.md`.

## Google Gemini LLM Provider

Backend dùng Google Gemini qua abstraction `ILlmProvider`; model mặc định là `gemini-2.5-pro`. Angular không gọi Gemini trực tiếp. Khi Gemini tắt, thiếu cấu hình hoặc gặp lỗi, report vẫn sinh nhận xét bằng `RULE_BASED_FALLBACK`.

=========================================================
VỊ TRÍ THAY GEMINI API KEY THẬT
=========================================================

1. Sao chép `infra/.env.example` thành `infra/.env`.
2. Chỉ thay dòng sau trong `infra/.env`:

```dotenv
GEMINI_API_KEY=PASTE_YOUR_REAL_GEMINI_API_KEY_HERE
```

Ví dụ định dạng: `GEMINI_API_KEY=xxxxxxxxxxxxxxxxxxxxxxxx` (không phải key thật).

Không điền key vào `appsettings.json`, `appsettings.Development.json`, `docker-compose.yml`, React `.env`, TypeScript source hoặc Git repository. Muốn đổi model, đặt `GEMINI_MODEL=<MODEL_ID_MOI>` trong `infra/.env`; không cần sửa code nếu API/SDK còn tương thích. Xem hướng dẫn đầy đủ tại `docs/LLM_PROVIDER.md`.
