# Backend

ASP.NET Core Web API project for EMI QMS.

## Run

```powershell
dotnet run --project src\Emi.Qms.Api --urls http://localhost:5080
```

## Health endpoints

- `GET /health/live`: process liveness, no authentication.
- `GET /health/ready`: PostgreSQL readiness, no authentication.

## Development authentication

TASK-002 adds a development-only authentication scheme. It reads `X-Dev-User` and maps it to fake local users such as `dev-admin`, `dev-sales`, `dev-quality`, and `dev-viewer`.

Protected endpoints:

- `GET /api/me`
- `GET /api/projects/{projectId}/overview`
- `GET /api/admin/users`

Development authentication is only allowed outside Production. If it is enabled while `ASPNETCORE_ENVIRONMENT=Production`, startup fails.

## Test

```powershell
dotnet test Emi.Qms.sln
```
