# TASK-002 authorization notes

Date: 2026-06-22

TASK-002 implements the authorization foundation without connecting Microsoft Entra ID.

## Decisions

- Development and Testing can use a temporary `X-Dev-User` header only when development authentication is explicitly enabled.
- Production, Staging, QA, and every environment other than Development and Testing block development authentication during application startup when it is explicitly enabled.
- ASP.NET Core authorization policies enforce role permission and project access on protected APIs.
- UI menu visibility is informational only; protected APIs always re-check authorization on the server.
- PostgreSQL schema changes are managed through SQL migrations under `database/migrations`.
- Development fake users and projects are not part of the schema migration. They are created only by the Development/Testing seeder when `DevelopmentData:SeedEnabled` or `DEV_DATA_SEED_ENABLED` is explicitly true.
- Role and Permission rows remain in the schema migration because they are operating baseline reference data from `docs/04-permission-matrix.md`; their codes are stable business roles/permissions rather than development fixtures, and inserts are idempotent through `on conflict`.
- `Project.Read.All` is the confirmed full-project read permission for active internal users.
- `UserProjectAccess` remains in the model for future external users or restricted accounts that do not have `Project.Read.All`.
- `Project.SalesAmount.Read` and `Manufacturing.WorkTime.Read` are sensitive read permissions allowed only for Sales and System Administrator.
- `projects.access.all` is a legacy/deprecated TASK-002 permission kept for 0001 migration compatibility. New endpoints and policies must not use it for full project read access.

## Fake data

The seed data uses only fake users and fake project identifiers:

- Users: `dev-admin`, `dev-sales`, `dev-production`, `dev-manufacturing`, `dev-quality`, `dev-logistics`, `dev-viewer`, `dev-no-role`, `dev-disabled`
- Projects: `demo-project-alpha`, `demo-project-beta`

All active internal development roles, including `dev-viewer`, can read both demo projects through `Project.Read.All`. Restricted project-scope denial is tested with a test-only identity store so the production baseline does not need fake restricted operating users.

## Follow-up authorization work

The following items remain intentionally out of TASK-002 scope and must be handled before or during the project/product business API work:

- Project list APIs must return all projects for active internal users with `Project.Read.All`, and only assigned projects for restricted accounts without `Project.Read.All`.
- Every write API must apply server-side project scope authorization.
- Project write APIs must check write permissions such as `projects.manage`; full read access is not write access.
- Project DTOs and manufacturing work-time APIs must omit sensitive fields unless the caller has `Project.SalesAmount.Read` or `Manufacturing.WorkTime.Read`.
- Review dependencies on legacy `projects.access.all` and add a cleanup migration if it is safe to remove.
- Product and Project identifiers must be validated regardless of whether they appear in route, query string, or request body.
- Add a common authorization convention and integration tests for list, create, update, delete, and status transition APIs.

## Security notes

The development authentication header is not a token and is not valid outside local development and automated tests. Authorization denials are logged with user ID, reason, endpoint, and target project key; passwords, tokens, and full personal data are not logged.
