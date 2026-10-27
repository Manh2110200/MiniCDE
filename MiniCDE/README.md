# MiniCDE — Tuần 1: nền tảng + Auth + Tổ chức/Dự án

## Chạy thử
```bash
docker compose up -d                      # Postgres, Keycloak, MinIO (+ tạo bucket "minicde")
cd api/MiniCde.Api
dotnet tool install --global dotnet-ef    # nếu chưa có
dotnet ef migrations add InitialCreate
dotnet run --urls http://localhost:5000   # Development tự chạy migration
```
Mở `api/MiniCde.http` trong Visual Studio, chạy lần lượt các request.
Tài khoản test (Keycloak, mật khẩu = tên + 123): `alice`, `bob` (@acme.com), `carol` (@other.com).
Keycloak admin: http://localhost:8080 (admin/admin). MinIO console: http://localhost:9001 (minioadmin/minioadmin).

## Quy ước thiết kế (nên nhớ để bảo vệ đồ án)
- Tổ chức = domain email của người dùng -> alice & bob cùng tổ chức, carol khác tổ chức.
- Mọi truy vấn đều lọc theo `OrganizationId` / membership -> dữ liệu không lộ sang tổ chức khác.
- Không phải thành viên dự án -> **404** (không lộ sự tồn tại); là thành viên nhưng thiếu quyền -> **403**.
- Lỗi trả theo ProblemDetails (RFC 9457).

## Lộ trình 4 tuần
| Tuần | Backend | Frontend |
|---|---|---|
| 1 (xong khung) | Docker Compose, Keycloak, EF Core, /api/me, projects, members | — |
| 2 | Folders cây + phân quyền kế thừa, documents, mã tài liệu, presigned URL, versions P01 | Đăng nhập OIDC + danh sách dự án |
| 3 | Quy trình ISO 19650 (transitions, approvals, 409), xóa mềm, job nền (FOR UPDATE SKIP LOCKED) | Trang tài liệu + upload XHR |
| 4 | Outbox + SignalR, audit log, xUnit + Testcontainers | Trang duyệt + chuông thông báo |
