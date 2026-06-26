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
- Node.js 24.18.0 LTS
- pnpm 11.8.0 via Corepack
- Docker Desktop 또는 Docker Compose 호환 런타임

Node.js 버전은 `.node-version`, pnpm 버전은 루트 `package.json`의 `packageManager`로 고정합니다. Node.js 설치 후 pnpm은 Corepack으로 실행합니다.

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

Development 환경에서는 `.env.example`의 명시적 설정으로 TASK-002의 개발용 인증과 개발 데이터 seeding이 활성화됩니다. 프런트엔드는 `VITE_DEV_USER_KEY` 값이 없으면 `dev-admin`으로 `/api/me`를 호출합니다.
TASK-002A 기준으로 모든 활성 내부 개발 역할과 `dev-viewer`는 `Project.Read.All`을 통해 두 demo 프로젝트를 모두 조회할 수 있습니다.

TASK-003B 기준으로 패널정보 직접 입력은 변경 의도를 명시하는 update mask 방식입니다. 화면은 canonical mm 저장값과 mm/inch 표시 문자열을 분리하므로 표시 단위 전환만으로 저장 요청, 감사이력, version 증가가 발생하지 않습니다. Excel Preview/Apply는 `.xlsx`만 허용하며 ZIP/Workbook 리소스 제한과 인스턴스 단위 parse 동시실행 제한을 적용하고, Apply 시 project row lock 이후 최신 포장방식과 panel version을 다시 검증합니다. Panel History는 Direct/Excel 입력방식, Import Batch 연결, 입력단위, 원본 입력값과 canonical mm 변경값을 함께 반환하며 legacy audit의 null metadata도 읽을 수 있습니다.

TASK-003B-1 기준으로 프로젝트 상세은 읽기 전용 제품·패널 목록을 보여주고, 입력권한 사용자는 `/projects/{projectId}/panel-information/edit` 수정 페이지에서만 직접 입력과 Excel 다운로드·업로드를 수행합니다. 제품 업무상태는 패널 관리상태 `Active/Cancelled`와 분리된 `workflow_stage`로 저장하며 기본값은 `BeforeManufacturing`입니다. 프로젝트 상세 요약은 Backend가 계산한 QR 가능, 제조 완료, 검사 완료 집계를 활성 패널 수 기준으로 표시합니다. Excel Apply는 파일에 포함된 일부 No만 변경할 수 있고 빈 editable 행은 `Skipped`로 처리하며, Preview 상단 sticky action bar에서 건수·수정사유·Excel 저장 버튼을 제공합니다. Field-level audit row는 유지하되 관리자 전용 `Audit.Read.All` 권한을 가진 System Administrator만 ImportBatchId 또는 CorrelationId 기준으로 그룹화된 전체 이력을 조회합니다.

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

Full-Stack E2E와 CI에서는 PostgreSQL이 healthy가 될 때까지 기다린 뒤 실행합니다.

```powershell
docker compose --env-file .\.env -f infrastructure\docker-compose.yml up -d --wait --wait-timeout 120 postgres
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
corepack pnpm install --frozen-lockfile
corepack pnpm --filter emi-qms-frontend run dev
```

### 검증 명령

백엔드:

```powershell
dotnet restore backend\Emi.Qms.sln
dotnet build backend\Emi.Qms.sln --configuration Release --no-restore
dotnet test backend\Emi.Qms.sln --configuration Release --no-build
```

백엔드 테스트는 각 테스트 factory가 Development Seed 활성/비활성 상태를 명시적으로 주입하므로, 로컬 `.env`의 `DEV_DATA_SEED_ENABLED` 값과 관계없이 같은 결과가 나와야 합니다.

프런트엔드:

```powershell
corepack pnpm install --frozen-lockfile
corepack pnpm --filter emi-qms-frontend run lint
corepack pnpm --filter emi-qms-frontend run typecheck
corepack pnpm --filter emi-qms-frontend test
corepack pnpm --filter emi-qms-frontend run build
corepack pnpm --filter emi-qms-frontend exec playwright install chromium
corepack pnpm --filter emi-qms-frontend run e2e:mock
corepack pnpm --filter emi-qms-frontend run e2e:full-stack
```

`e2e:mock`은 API route mock 기반 UI browser smoke test입니다. `e2e:full-stack`은 전용 임시 PostgreSQL DB, 실제 Backend, 실제 Frontend를 함께 실행합니다. full-stack E2E는 `emi_qms_e2e` 임시 DB만 reset/drop하며 기존 개발 DB와 Docker volume은 삭제하지 않습니다.

### Health endpoint

- `GET http://localhost:5080/health/live`
- `GET http://localhost:5080/health/ready`

### 개발용 인증과 권한 확인

실제 Microsoft Entra ID 연동 전까지 Development/Testing 환경에서만 `X-Dev-User` 헤더를 사용합니다. Production에서 개발용 인증을 활성화하면 애플리케이션 시작이 실패합니다.

개발용 사용자 키:

- `dev-admin`
- `dev-sales`
- `dev-production`
- `dev-manufacturing`
- `dev-quality`
- `dev-logistics`
- `dev-viewer`
- `dev-no-role`

예시:

```powershell
Invoke-WebRequest -UseBasicParsing -Headers @{ "X-Dev-User" = "dev-viewer" } http://localhost:5080/api/projects/demo-project-alpha/overview
Invoke-WebRequest -UseBasicParsing -Headers @{ "X-Dev-User" = "dev-viewer" } http://localhost:5080/api/projects/demo-project-beta/overview
```

## 문서 우선순위

충돌이 있을 때 다음 순서로 적용합니다.

1. 승인된 ADR
2. `AGENTS.md`
3. 업무 요구사항 문서
4. 개별 작업지시서
5. 구현 코드
