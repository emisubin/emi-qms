# Frontend

React + TypeScript development UI for EMI PMS.

## Run

```powershell
corepack pnpm install
corepack pnpm --filter emi-qms-frontend run dev
```

The development screen calls the backend `GET /health/ready` endpoint and displays API/database readiness.

## Verify

```powershell
corepack pnpm --filter emi-qms-frontend run lint
corepack pnpm --filter emi-qms-frontend run typecheck
corepack pnpm --filter emi-qms-frontend test
corepack pnpm --filter emi-qms-frontend run build
```
