# Backend

ASP.NET Core Web API project for EMI QMS.

## Run

```powershell
dotnet run --project src\Emi.Qms.Api --urls http://localhost:5080
```

## Health endpoints

- `GET /health/live`: process liveness, no authentication.
- `GET /health/ready`: PostgreSQL readiness, no authentication.

## Test

```powershell
dotnet test Emi.Qms.sln
```
