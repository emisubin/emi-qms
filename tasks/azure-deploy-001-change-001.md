# TASK-AZURE-DEPLOY-001 Change 001 — 20일 Azure 시범 배포 실행 준비

## 승인과 분류

- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true`
- implementationApproved: `true`
- approvalSource: `USER_EXPLICIT_20_DAY_PILOT_DEPLOYMENT_START`
- 비용 경계: `COST_OWNER_USER`
- 승인일: 2026-07-31

## 확정된 시범 구성

- Azure Front Door Standard와 custom rate limit
- Container Apps workload profiles environment의 Consumption profile
- Frontend `0.25 vCPU / 0.5 GiB / minimum 1 / maximum 2`
- API와 worker `1 vCPU / 2 GiB / minimum 1 / maximum 1`
- ClamAV `2 vCPU / 4 GiB / minimum 1 / maximum 1`
- one-shot migration job `1 vCPU / 2 GiB`
- PostgreSQL Flexible Server Burstable B2s, 32 GB, auto-grow, PITR 14일, HA 없음
- ACR Basic, ClamAV signature용 Azure Files 5 GB
- Key Vault, Log Analytics와 Application Insights
- 기존 PostgreSQL 첨부 저장 구조 유지
- Entra SPA/API app registration 분리와 관리자 승인 전 일반 사용자 권한 없음
- Teams manifest를 PMS 기준으로 새로 생성

실제 hostname, tenant/client identifier, 관리자 email, 경보 수신자와 provider secret은 Git에 기록하지 않고 배포 시 secure parameter 또는 Key Vault로만 주입한다.

## 포함 범위

1. Azure Container Apps용 HTTP Frontend image와 Front Door origin 검증을 추가한다.
2. 동적 Container Apps 주소를 신뢰할 수 있도록 Backend trusted proxy network를 fail-closed 방식으로 지원한다.
3. Foundation, workload, edge를 분리한 Azure IaC와 단계별 rollout·rollback 절차를 만든다.
4. one-shot migration이 성공하기 전 application traffic을 활성화하지 않는 gate를 만든다.
5. Teams app manifest template, 필수 icon과 privacy-safe package builder를 만든다.
6. 비용 없는 로컬 build, test, template와 package 검증을 수행한다.

## 비용 중단선

다음 동작은 이 change에서 자동 실행하지 않는다.

- Azure resource provider 등록
- Resource group, Front Door, Container Apps, PostgreSQL, ACR, Storage, Key Vault, Log Analytics 생성
- ACR image push
- PostgreSQL migration job 실행
- DNS record와 managed certificate 활성화
- budget·alert 생성
- 실제 Teams·Gmail 발송
- public traffic 전환

위 동작에 도달하면 Codex는 먼저 생성 서비스, 사양, 예상 비용과 rollback 영향을 보고하고 중단한다. 비용 관련 Portal·CLI 실행은 사용자가 직접 수행한다.

## 보존할 불변조건

- `main`과 현재 운영 runtime을 직접 변경하지 않는다.
- 실제 secret, email, company domain과 identifier를 tracked 파일·로그에 남기지 않는다.
- Backend와 ClamAV는 public ingress를 사용하지 않는다.
- upload malware scan은 Production에서 fail-closed다.
- migration은 web startup과 분리하고 ledger 검증이 성공해야 한다.
- API replica는 background worker 중복을 피하기 위해 시범 기간에는 1개다.
- 기존 Docker-host Production Compose를 깨뜨리지 않고 Azure 전용 artifact를 추가한다.

## 검증 기준

- Backend Release build와 보안 집중 test
- Frontend Azure Production image build와 Nginx configuration smoke
- Bicep compile 또는 동등한 local syntax 검증
- Teams manifest JSON/schema 필수값, placeholder와 icon 크기 검증
- shell syntax와 negative-path 검증
- tracked secret·identifier scan
- Azure 실제 resource와 provider 호출 없음 확인

## 작업공간 기록

- purpose: 20일 Azure 시범 배포 provider-specific 로컬 구현
- owner: Codex
- 기준 SHA: `c02499d`
- branch: `feat/task-azure-deploy-001-planning`
- 예상 종료: 비용 발생 Azure resource 생성 gate 직전
- cleanup 경계: commit reachability, clean 상태와 process 미사용을 확인한 뒤 사용자 승인 범위에서만 정리
