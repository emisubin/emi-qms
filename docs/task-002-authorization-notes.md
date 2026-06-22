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

## Fake data

The seed data uses only fake users and fake project identifiers:

- Users: `dev-admin`, `dev-sales`, `dev-production`, `dev-manufacturing`, `dev-quality`, `dev-logistics`, `dev-viewer`, `dev-no-role`, `dev-disabled`
- Projects: `demo-project-alpha`, `demo-project-beta`

`dev-viewer` can access only `demo-project-alpha`, which is used to test project-scope denial for `demo-project-beta`.

## Follow-up authorization work

The following items remain intentionally out of TASK-002 scope and must be handled before or during the project/product business API work:

- Project list APIs must return only projects the current user can access.
- Every write API must apply server-side project scope authorization.
- Product and Project identifiers must be validated regardless of whether they appear in route, query string, or request body.
- Add a common authorization convention and integration tests for list, create, update, delete, and status transition APIs.

## Security notes

The development authentication header is not a token and is not valid outside local development and automated tests. Authorization denials are logged with user ID, reason, endpoint, and target project key; passwords, tokens, and full personal data are not logged.
