# TASK-UAT-001 Change 006 — Entra 통합 검수 실행기 복구

## 1. Task Identity Gate

- proposedTaskId: `TASK-UAT-001 Change 006`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `운영 전환 Scope Review`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UAT-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

## 2. Purpose identity

- 업무 목표: HTTPS 5174/Backend 5081 통합 검수 실행기가 기존의 분리된 Entra SPA·API app registration을 안전하게 읽고, Frontend proxy도 실제로 5081을 사용하게 복구한다.
- Root Finding:
  - `UAT-ENTRA-006-A`: 실행기가 `AzureAd__ClientId`와 `VITE_AZURE_CLIENT_ID`의 일치를 강제해 정상적인 API·SPA 분리 구성을 거절한다.
- `UAT-ENTRA-006-B`: Vite가 `.env.entra-local`을 발견하면 명시된 `VITE_DEV_PROXY_TARGET`을 무시하고 5084를 강제해 통합 실행기의 5081 계약과 어긋난다.
- `UAT-ENTRA-006-C`: 분리 후보 Backend를 띄운 screen session 종료가 하위 listener를 정리하지 못해 5084가 고아 runtime으로 남을 수 있다.
- 변경 경계: UAT Entra identifier 해석, Frontend Development proxy 선택, candidate Backend의 소유권 확인 종료, 회귀 script, UAT 문서와 구현 보고를 포함한다.
- 보존할 불변조건: 실제 Entra identifier·token·credential 원문 비노출, HTTPS 5174·Backend 5081 고정, 기존 단일 app registration 호환, Persistent UAT DB·schema·data 보존, 실제 알림 provider 비활성 상태를 유지한다. HTTPS wrapper는 기존 UAT container·DB를 기본으로 보존하며 명시적 override 없이는 container recreation, migration·master-data setup을 실행하지 않는다.
- 예상 산출물: 단일·분리 app registration 성공 회귀, tenant·역할별 alias 충돌과 placeholder 거절, 명시적 5081 proxy 우선, 통합 실행기 runtime 검증.

## 3. 사용자 승인과 게시 경계

- 사용자는 2026-07-30에 통합 실행기 문제 해결과 사용자 검수 안내를 명시했다.
- 이 지시는 기존 `TASK-UAT-001`의 확인된 검수 실행 결함 구현·검증을 승인한다.
- 실제 Entra registration, credential, Persistent UAT migration·seed·reset과 실제 알림 발송은 포함하지 않는다.
- 기존 PR #58은 사용자 검수 완료 전 Draft와 미병합 상태를 유지한다.

## 4. 완료 기준

- 기존 `ENTRA_CLIENT_ID` 단일 app 구성이 계속 성공한다.
- `AzureAd__ClientId` API app과 `VITE_AZURE_CLIENT_ID` SPA app이 서로 달라도 성공한다.
- tenant alias, API 역할 alias, SPA 역할 alias와 legacy 단일 client alias의 모순은 역할별로 fail-closed한다.
- placeholder tenant/API/SPA identifier는 거절한다.
- HTTPS wrapper가 Backend 5081과 Frontend 5174를 실행하고 `/health/live` proxy가 200을 반환한다.
- HTTPS wrapper 기본 실행이 기존 UAT container를 필요하면 다시 시작하고 현재 container credential을 사용하며 recreation·migration·seed·reset을 수행하지 않는다.
- Candidate 종료는 5084 listener의 Repository cwd와 API command를 모두 확인하고, 불일치하면 어떤 프로세스도 종료하지 않는다.
- 로그인 shell이 Microsoft 365 로그인 action을 표시하며 실제 사용자 로그인은 사용자가 직접 검수한다.

## 5. Rollback

- schema/data migration이 없으므로 실행기·Vite proxy·회귀·문서 변경만 이전 commit으로 되돌릴 수 있다.
- 실제 identifier를 하나로 덮어쓰거나 별도 5084 Backend를 상시 유지하는 방식은 rollback으로 사용하지 않는다.

## 6. 구현 결과

- API·SPA client ID를 역할별로 해석하고 단일 app legacy 설정과 분리 app 설정을 모두 지원했다.
- 명시적 `VITE_DEV_PROXY_TARGET`을 Vite의 candidate 기본값보다 우선했다.
- HTTPS wrapper 기본값을 기존 healthy UAT DB 보존 mode로 고정했다.
- Candidate Backend에 Repository ownership을 확인하는 `stop` mode를 추가했다.

## 7. 검증·검수·게시 상태

- Bash syntax와 UAT auth config 단일·분리·충돌 회귀 12/12 통과
- Frontend lint error 0·기존 warning 1, typecheck 통과, unit 143/143, build 통과
- 통합 wrapper로 Backend 5081·Frontend 5174 기동, root·health proxy·Backend health 200
- 익명 `/api/me` 401, 로그인 shell Microsoft 365 action 표시, console error 0
- 기존 UAT DB setup·migration·seed 미실행, 실제 identifier·provider 미변경
- 사용자 검수: 완료 — 2026-07-30
- PR #58: 변경 반영·사용자 merge 승인 완료
- 실제 PR·`main` 상태: GitHub 실행 상태를 authoritative source로 사용
