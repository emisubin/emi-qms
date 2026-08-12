# TASK-AZURE-DEPLOY-001 Change 023 — 프로젝트·Pending·관리자·설계 열반 통합 운영 배포

## Gate projection

- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true` — 사용자의 전체 사용자 검수 완료와 이번 `main` 병합·공개배포 명시 승인
- gateStatus: `PASS_REUSE`
- sourceBaseline: `origin/main` `af796547ffb260ae427932a4734894af23c21ae6`
- sourceTasks: `TASK-PROJECT-PENDING-001`, `TASK-ADMIN-001 Change 001`, `TASK-PANEL-DESIGN-001 Change 001`
- mainMergeApprovalCount: `1`
- productionDeploymentApproved: `true`

## 포함 범위

- 프로젝트 기본정보의 선택형 `LSE TASK NO`와 migration `0076_project_lse_task_number.sql`.
- 전체 Pending의 `우리 부서 + 오픈`, 프로젝트 Pending의 `전체 + 오픈` 기본 범위와 오픈·종결 상태 식별.
- 관리자 홈의 조치 가치가 낮은 KPI 제거와 승인 대기 사용자 전용 목록.
- 일반 Item 설계의 패널별 도번, 포장방식별 필수 입력값 안내, 2면 이상 패널 열반과 migration `0077_panel_design_drawing_groups.sql`.
- 열반 재구성 때마다 현재 열반을 패널 순서 기준 `1..N`으로 다시 부여하고, 설계 탭에 `W 합계 × H 최댓값 × D 최댓값`을 표시한다.
- 관련 계약·구현·검수 문서와 Roadmap 상태 동기화.

## 제외·보존 범위

- UL891은 패널 열반 대상에서 제외하고 기존 세트 설계를 유지한다.
- 기존 프로젝트·패널·Pending·알림·delivery·구독 데이터는 삭제하거나 재작성하지 않는다.
- 현재 운영 중인 Microsoft 365 사전 인증, PWA Web Push, Teams Activity와 메일 설정·secret을 변경하지 않는다.
- Azure resource 사양, 도메인, Front Door, 앱 등록과 Teams manifest를 변경하지 않는다.
- 흑백 wireframe, 일반 검정 테두리와 강조선 금지 원칙을 유지한다.

## 통합 기준선

- 최신 `origin/main` 위에서 세 승인 후보를 하나의 통합 branch에 순서대로 이식한다.
- migration 번호는 기존 운영 최신 `0075` 다음의 `0076`, `0077`로 고정한다.
- Pull Request의 최신 head에서 필수 `CI Gate`를 통과한 뒤에만 `main`에 병합한다.
- 병합된 exact `main` SHA만 승인형 Azure release에 전달한다.

## 완료 Gate

1. 통합 diff·migration catalog·Backend·Frontend 계약을 검증한다.
2. Ready PR 최신 head의 변경 분류, Frontend, Backend, Full-Stack E2E와 `CI Gate`를 통과한다.
3. 직접 `main` push 없이 PR로 병합하고 exact merge SHA를 확인한다.
4. Azure release가 migration `0076`·`0077`을 적용한 뒤 Backend와 Frontend를 교체한다.
5. 새 revision readiness, 공개 health, 익명 root·API 차단을 확인한다.
6. Open P0/P1/P2가 `0/0/0`일 때 완료로 판정한다.
