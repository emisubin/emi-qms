# TASK-PRIVACY-NOTICE-001 — Task Identity Gate

- proposedTaskId: `TASK-PRIVACY-NOTICE-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `제품 다음 Gate는 별도 승인 Task`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-PRIVACY-NOTICE-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: 사내 시범 운영 사용자가 EMI PMS에서 처리하는 개인정보, 권리 행사 방법과 내부 이용수칙을 앱 안에서 쉽게 확인하고, 실제로 동의가 필요한 선택 항목은 일반 고지와 분리해 판단할 수 있는 사용자 안내 체계를 기획한다.
- Root Finding 또는 정책 결정: 실제 임직원 계정과 업무 데이터를 사용하는 시범 운영에는 별도 면제가 없으며 개인정보 처리방침 공개와 정보주체 권리 행사 절차가 필요하다. 반면 서비스 전체에 대한 포괄 동의와 별도 이용약관은 사내 전용이라는 이유만으로 자동 필수가 아니므로, 법적 근거·선택 처리·외부 제공/국외 이전 여부에 따라 범위를 나눠야 한다.
- 변경·검증 경계: 개인정보 처리방침, 권리 행사·문의 안내, 사내 시스템 이용수칙의 정보 구조·진입점·버전/변경 고지·모바일 UX와 조건부 동의 흐름을 기획한다. 실제 법률 자문, 회사 규정 승인, Web Push 신규 채널, 운영 데이터 변경, 외부 provider 호출과 구현은 포함하지 않는다.
- 보존할 불변조건: 기존 Microsoft 365 인증·서버 권한·업무 workflow·알림 수신자와 발송 시점을 변경하지 않는다. 앱이 현재 제공하지 않는 Web Push를 제공한다고 표시하지 않는다. 불필요한 포괄 동의를 받지 않고, 실제 개인정보·회사 계정·tenant/client identifier·secret을 기획 산출물에 기록하지 않는다.
- 예상 산출물: Fable deep-interview 원문과 canonical interview, Fable primary planning, Codex 내용·법적 최소요건·Repository 정합성 review, 후속 구현 범위·검증·rollout 결정 항목.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## Gate 근거

- 최신 Roadmap에서 Azure 시범 운영과 PWA 설치 경험은 운영 반영됐고 제품 다음 Gate는 사용자가 별도로 승인하는 Task다.
- `TASK-AZURE-PILOT-001`은 배포·migration 사전점검, `TASK-AZURE-DEPLOY-001`은 Azure 운영, `TASK-TEAMS-PWA-001`은 Teams 실행·PWA 설치 경험을 다룬다. 개인정보 처리방침·권리 행사·내부 이용수칙을 사용자가 조회하는 신규 화면과 정책 흐름은 어느 Task의 완료 scope에도 포함되지 않는다.
- `tasks/`, Roadmap·Decision Log·추적 항목, local/remote branch·worktree와 GitHub PR의 privacy/legal/terms/pilot/PWA 후보를 확인했으며 같은 purpose identity는 0건이다.
- 사용자는 사내 시범 운영 공개 전에 필요한 페이지를 구체적으로 조사한 뒤 Fable 비교 검토 기획을 명시 요청했다. 따라서 별도 승인 제품 Task라는 현재 Roadmap Gate와 일치한다.
- canonical clone의 사용자 소유 이미지 WIP를 보존하기 위해 최신 `origin/main` 기준 bounded planning worktree를 사용한다. 이 worktree는 Fable interview·planning·Codex review 전용이며 runtime·DB·provider를 변경하지 않는다.
