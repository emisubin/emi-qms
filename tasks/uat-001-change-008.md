# TASK-UAT-001 Change 008 — 원격 main 기준 Persistent UAT 인계

## Task Identity Gate

- proposedTaskId: `TASK-UAT-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `INTEGRATED_SOURCE_MIGRATION_0068_HANDOVER`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UAT-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

## 승인과 기준선

- approvalSource: `USER_EXPLICIT_NEXT_STEP_APPROVAL`
- 승인일: 2026-08-04
- 기준 branch: `origin/main`
- 기준 commit: `36f9c0a015b65f207cef56d37bf4517d60b28f03`
- 작업 branch: `fix/task-uat-001-current-main-handover`
- 대상 runtime: HTTPS Frontend `5174`, Backend `5081`, 기존 Persistent UAT DB

## Purpose identity

- 업무 목표: 원격 `main`을 로컬 UAT의 실제 source of truth로 사용하도록 Backend 5081과 Persistent UAT schema를 인계한 뒤 승인된 실제 업무 입력을 가능하게 한다.
- Root Finding: Frontend 5174는 최신 원격 `main`이지만 Backend 5081은 다른 과거 source에서 실행 중이고 DB migration ledger도 source보다 네 건 뒤처져 있다.
- 변경·검증 경계: 기존 DB·volume과 업무 데이터를 보존한 채 migration `0065`~`0068`만 적용하고, 실제 provider를 비활성으로 둔 최신 `main` Backend 5081을 기동해 live/ready·schema·aggregate를 검증한다.
- 보존할 불변조건: DB drop/truncate/reset·volume 삭제·seed 초기화 금지, 기존 업무·감사 이력 보존, 외부 알림 발송 금지, 5174/5081 strict port와 loopback 고정, 실패 시 기존 source로 runtime rollback하고 migration은 forward-fix한다.
- 예상 산출물: controlled migration 결과, Backend 5081 handover, privacy-safe before/after projection, 실제 업무 입력 가능 판정과 Task 문서 상태 갱신.

## 승인된 실행 범위

1. 기존 PostgreSQL container·DB·named volume·aggregate와 5174/5081 process ownership을 값 비노출로 확인한다.
2. 기존 Backend 5081을 정확한 PID·cwd·command 확인 후에만 정상 종료한다.
3. migration `0065`~`0068`을 각각 transaction과 `ON_ERROR_STOP`으로 적용하고 ledger를 `68 Exact`로 확인한다.
4. 실제 notification provider 설정을 로드하지 않은 최신 `main` Backend를 `127.0.0.1:5081`에 기동한다.
5. Backend live/ready, Frontend health proxy, DB aggregate·restart·volume·loopback listener와 UL891 migration 불변조건을 확인한다.
6. 위 Gate가 통과한 뒤에만 사용자가 제공한 입력안의 확인된 값부터 실제 화면 입력을 시작한다.

## 제외 범위

- Azure image·revision·DNS·TLS·Front Door·Entra 운영 설정 변경
- Teams·Gmail 등 실제 외부 provider 발송
- DB reset·기존 데이터 삭제·과거 migration 수정
- 입력안에서 미확정으로 표시된 가격·중량·날짜를 임의 확정하거나 증빙 없는 파일을 업로드하는 행위
- Git push·PR·merge와 branch/worktree 정리

## 검증 계획

- before/after migration ledger count와 exact 여부
- before/after 프로젝트·패널·열린 Pending·대기 delivery aggregate 일치
- PostgreSQL health·restart·mount·loopback listener 보존
- migration `0068`의 활성 UL891 패널 위치 누락 0과 활성 projection 보존
- Backend `/health/live`, `/health/ready`, Frontend `/health/live` proxy 성공
- 실제 provider worker 비활성, 신규 외부 delivery 생성·발송 0
- 5174와 5081 source commit 일치

## 현재 실행 상태

- 전환 전 DB: health 정상, restart `0`, mount `2`, ledger `64/68`.
- 전환 전 aggregate: 프로젝트 `7`, 패널 `109`, 열린 Pending `0`, 보존할 대기 delivery `2`.
- 5174: 원격 `main` 기준 source에서 계속 실행 중.
- 5081: 사용자가 확인된 과거 source listener를 정상 종료해 현재 listener `0`.
- migration `0065`~`0068`: 적용 완료. ledger `68/68 Exact`, 기존 프로젝트·패널·Pending·delivery aggregate 보존.
- blocker `UAT-HANDOVER-008-A`: `RESOLVED` — 사용자가 확인된 listener를 정상 종료했고 DB·5174는 보존됐다.
- blocker `UAT-HANDOVER-008-B`: `RESOLVED` — 사용자가 공식 migration mode를 실행했고 additive migration 네 건과 ledger 기록이 완료됐다.
- finding `UAT-HANDOVER-008-C`: `OPEN` — migration mode의 compose 실행이 PostgreSQL container 환경을 인증용 env 기준으로 갱신했지만 named volume의 기존 DB role password는 바꾸지 않았다. 그 결과 container env와 실제 DB credential이 달라 보존 모드도 Backend 인증에 실패했다. 기존 DB용 `.env`로 PostgreSQL container definition만 data-preserving 재정렬하고, 인증용 env는 5174·5081 기동에만 사용해야 한다.

## Migration 검증 결과

- PostgreSQL: health 정상, restart `0`, mount `2`, loopback listener 유지.
- 업무 aggregate: 프로젝트 `7`, 패널 `109`, 열린 Pending `0`, 보존할 대기 delivery `2`로 전환 전과 동일.
- UL891: 활성 패널의 현재 설계 위치 연결 누락 `0`; 취소 이력은 삭제되지 않음.
- 신규 schema: Pending 조치 사진 table 존재, 자재 category `5`, UL891 기본계획 header `2`.
- 5174·5081: DB 인증 실패 뒤 둘 다 내려간 상태이며 DB 추가 mutation은 중단됨.
- 복구 불변조건: `docker compose down`, volume 삭제, DB 재생성, migration 재적용을 사용하지 않고 기존 named volume을 그대로 부착한 PostgreSQL container definition만 원래 DB env에 맞춘다.

## 사용자 최종 runtime 결정

- 2026-08-04 사용자는 기존 입력이 모두 테스트 데이터임을 확인하고, HTTPS 5174만 사용자 검수 주소로 사용하는 빈 DB 재시작과 실제 문서 입력을 승인했다.
- Repository의 Persistent UAT 보호 규칙에 따라 기존 container와 named volume은 삭제하지 않고 비활성 격리한다. Active runtime은 최신 원격 `main` source 전용 새 PostgreSQL volume을 사용하며 migration `0001`~`0068`과 허용된 기본 seed만 적용한다.
- 5081은 5174의 내부 API proxy 대상으로만 기동한다. 사용자가 직접 사용할 주소는 HTTPS 5174 하나다.
- 실제 provider는 계속 비활성화한다. 전달 문서의 확인된 값만 입력하고, 미제공 첨부·실중량·금액·조건부 항목은 임의 생성하지 않는다.

## 후속 게시 승인

- 2026-08-04 사용자는 실제 문서 입력 과정에서 확인·수정한 현재 코드와 Task 기록을 원격 `main`에 병합하도록 명시적으로 승인했다.
- 승인 범위는 Change 008 runtime 인계 기록, 생산계획 진행률 막대 표시 수정과 Change 009의 프로젝트 진행률·현재 단계 수정이다.
- DB 입력값·증빙 파일·runtime log·환경 설정과 credential은 Git 게시 대상에 포함하지 않는다.
