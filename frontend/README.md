# Frontend

React + TypeScript development UI for EMI QMS.

## Run

```powershell
pnpm install
pnpm --filter emi-qms-frontend run dev
```

The development screen calls the backend `GET /health/ready` endpoint and displays API/database readiness.

## Verify

```powershell
pnpm --filter emi-qms-frontend run lint
pnpm --filter emi-qms-frontend run typecheck
pnpm --filter emi-qms-frontend test
pnpm --filter emi-qms-frontend run build
```
