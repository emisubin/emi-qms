# EMI QR 생산·품질·출하 통합관리 시스템

EMI의 프로젝트·제품·판넬을 QR로 식별하고, 설계 출도부터 생산·품질·부적합 조치·포장·출하·납품 확인까지 하나의 이력으로 관리하는 내부 업무시스템입니다.

## 핵심 원칙

- 관리 단위는 **프로젝트가 아니라 개별 제품·판넬**입니다.
- 제품당 QR은 하나이며, 로그인 사용자의 역할과 제품 상태에 따라 첫 화면이 달라집니다.
- 승인·완료 이력은 덮어쓰지 않고 정정 절차와 감사 로그를 남깁니다.
- 열린 부적합이 있으면 품질 승인과 출하를 차단합니다.
- PC는 전체 계획·현황 관리, 모바일은 QR 기반 현장 입력에 최적화합니다.

## 예정 기술 구조

- Frontend: React + TypeScript
- Backend: ASP.NET Core Web API
- Database: PostgreSQL
- Authentication: Microsoft Entra ID
- File storage: Azure Blob Storage
- Hosting: Azure App Service
- Notifications: Microsoft Teams / Power Automate 또는 Microsoft Graph

기술 선택은 `docs/adr/0001-system-architecture.md`에 기록하며, 구현 중 변경하려면 ADR을 추가해야 합니다.

## 저장소 구조

```text
frontend/        PC·모바일 반응형 웹 화면
backend/         API, 권한, 업무 규칙
database/       DB 스키마·마이그레이션·샘플 데이터
infrastructure/  로컬·Azure 배포 구성
 tests/          단위·통합·E2E 테스트
 docs/           업무·설계·권한·상태 문서
 tasks/          Codex에 전달할 기능 단위 작업지시서
```

## 시작 순서

1. `START_HERE.md`에 따라 비공개 GitHub 저장소로 게시합니다.
2. Codex에서 이 폴더를 프로젝트로 엽니다.
3. Codex에게 `tasks/001-bootstrap-development-environment.md`를 수행하도록 요청합니다.
4. 변경 내용을 검토한 뒤 테스트가 통과할 때만 커밋합니다.

## 로컬 개발환경

### 필요 도구

- Git
- .NET SDK 10 LTS
- Node.js 24 LTS
- pnpm 11 via Corepack
- Docker Desktop 또는 Docker Compose 호환 런타임

Node.js 설치 후 pnpm은 Corepack으로 실행합니다.

```powershell
corepack enable
corepack pnpm --version
```

### 전체 시작

```powershell
Copy-Item .env.example .env
.\scripts\dev-start.ps1
```

스크립트는 PostgreSQL 컨테이너를 시작하고, 백엔드는 `http://localhost:5080`, 프런트엔드는 `http://localhost:5173`에서 실행합니다.

### 전체 종료

```powershell
.\scripts\dev-stop.ps1
```

### 개별 실행

PostgreSQL:

```powershell
Copy-Item .env.example .env
docker compose --env-file .\.env -f infrastructure\docker-compose.yml up -d
```

백엔드:

```powershell
Copy-Item .env.example .env
Get-Content .env | Where-Object { $_ -and -not $_.StartsWith("#") } | ForEach-Object {
  $name, $value = $_ -split "=", 2
  [Environment]::SetEnvironmentVariable($name.Trim(), $value.Trim(), "Process")
}
$env:ASPNETCORE_ENVIRONMENT="Development"
cd backend
dotnet run --project src\Emi.Qms.Api --urls http://localhost:5080
```

프런트엔드:

```powershell
corepack pnpm install
corepack pnpm --filter emi-qms-frontend run dev
```

### 검증 명령

백엔드:

```powershell
dotnet restore backend\Emi.Qms.sln
dotnet build backend\Emi.Qms.sln
dotnet test backend\Emi.Qms.sln
```

프런트엔드:

```powershell
corepack pnpm --filter emi-qms-frontend run lint
corepack pnpm --filter emi-qms-frontend run typecheck
corepack pnpm --filter emi-qms-frontend test
corepack pnpm --filter emi-qms-frontend run build
```

### Health endpoint

- `GET http://localhost:5080/health/live`
- `GET http://localhost:5080/health/ready`

## 문서 우선순위

충돌이 있을 때 다음 순서로 적용합니다.

1. 승인된 ADR
2. `AGENTS.md`
3. 업무 요구사항 문서
4. 개별 작업지시서
5. 구현 코드
