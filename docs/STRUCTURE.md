# Cấu trúc thư mục chuẩn - không đổi tùy tiện

```text
HTBAM-Web-System-v2/
├─ apps/
│  └─ web/                         # ReactJS client
│     └─ src/
│        ├─ api/                   # HTTP client, API modules
│        ├─ components/            # UI dùng lại
│        ├─ layout/                # App shell/sidebar/header
│        ├─ pages/                 # Route-level pages
│        └─ types/                 # DTO/type frontend
├─ services/
│  ├─ backend/
│  │  └─ src/
│  │     ├─ HTBAM.Domain/          # Entity, enum, invariant lõi
│  │     ├─ HTBAM.Application/     # DTO, interface, use-case/service
│  │     ├─ HTBAM.Infrastructure/  # EF Core, SQL Server, AI client, JWT
│  │     └─ HTBAM.Api/             # Controller, SignalR Hub, DI, middleware
│  └─ ai/
│     └─ app/                      # FastAPI: contract và adapter inference
├─ database/                       # schema.sql, seed.sql
├─ infra/                          # docker-compose, deployment config
├─ docs/                           # tài liệu kỹ thuật
└─ tests/                          # contract/structure checks
```

## Quy tắc cố định
1. React không truy cập SQL Server trực tiếp.
2. ASP.NET Core là nguồn chân lý nghiệp vụ.
3. Python AI không tự quản lý roster/user/report; chỉ chạy inference và phát event/metric.
4. `track_id` là tạm thời; `stable_id` là identity kỹ thuật trong session.
5. StudentId có thể null/UNKNOWN. Không ép gán MSSV.
6. Behavior/Event/Alert phải gắn Session + StableIdentity; StudentId chỉ gắn khi đủ bằng chứng.
7. Raw frame không truyền qua SignalR và không lưu mặc định.
8. Mọi AI event có `event_id` để chống ghi trùng khi retry.
