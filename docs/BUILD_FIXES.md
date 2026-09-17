# Build fixes

## 2026-08-28 — v3.1 build fix

Docker build on a real .NET 9 SDK environment exposed a missing compile-time dependency in `HTBAM.Infrastructure`:

`IConfiguration.GetValue(...)` is implemented by `Microsoft.Extensions.Configuration.Binder`.

Fix applied:

```xml
<PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="9.0.8" />
```

No database schema or API contract changes are required for this fix.
