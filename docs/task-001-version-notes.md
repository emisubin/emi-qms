# TASK-001 version notes

Date: 2026-06-22

The initial development environment uses these supported stable lines:

- .NET 10 LTS for the ASP.NET Core Web API.
- Node.js 24 LTS for the frontend toolchain.
- React 19.2 for the frontend UI.
- PostgreSQL 18 for local relational storage.
- Npgsql 10 for PostgreSQL access from the backend health check.

Rationale:

- ADR-0001 selects React + TypeScript, ASP.NET Core Web API, PostgreSQL, Microsoft Entra ID, Azure Blob Storage, Azure App Service, Azure Key Vault, and Application Insights.
- TASK-001 only requires local development infrastructure, anonymous health endpoints, API status UI, and CI. It does not authorize Entra ID integration, Azure integration, business tables, or production secrets.
- .NET 10 is the current LTS line as of 2026-06-22. .NET 8 is also LTS but is already in maintenance and reaches end of support in November 2026.
- PostgreSQL 19 is still beta as of 2026-06-22, so PostgreSQL 18 is used for the local development database.

Official references checked:

- https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- https://nodejs.org/en/about/previous-releases
- https://react.dev/versions
- https://www.postgresql.org/docs/release/
