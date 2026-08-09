# TASK-TEAMS-PWA-001 Change 001 — Teams 알림·외부 실행 구조 승인

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- planningApproved: `true`
- implementationApproved: `true`
- approvalSource: `USER_EXPLICIT_RECOMMENDED_OPTION`
- approvalDate: `2026-08-09`
- canonicalTaskId: `TASK-TEAMS-PWA-001`
- branch: `feat/task-teams-pwa-001-experience`
- baseSha: `914a109e170f4e1c3ce34fb1faa4216c1b4fcf1c`
- roadmapSequenceMatch: `true`

## 사용자 승인

사용자는 Codex가 공식 Microsoft 문서와 현재 Repository·Azure 인증 구조를 대조해 제시한 권장안을 승인했다.

승인한 권장안은 Teams를 Activity Feed 알림과 EMI PMS 실행 진입점으로 사용하고, 실제 업무 화면은 기존 Entra 사전 인증으로 보호된 웹 또는 설치형 PWA에서 여는 방식이다.

## Planning·Review resolution

Fable planning의 브랜드·PWA·권한·알림 불변조건은 유지하되 Teams 인증 구현 범위를 다음과 같이 정정한다.

1. Teams 개인 tab은 전체 React SPA나 NAA를 실행하지 않는다.
2. 개인 tab에는 핵심 bundle과 업무 구조를 포함하지 않는 작은 정적 실행 화면만 제공한다.
3. 실행 화면의 사용자 동작으로 보호된 EMI PMS 웹/PWA를 새 창에서 연다.
4. Azure Easy Auth는 핵심 shell·bundle·API의 익명 접근 차단을 계속 유지한다.
5. 기존 Teams Activity Feed provider, activity type 10개, recipient·event 시점과 `webApplicationInfo`의 Activity app identity는 변경하지 않는다.
6. NAA, `getAuthToken`+OBO, token prefetch, 신규 Entra app registration과 Teams 전용 인증 session은 이번 구현에서 제외한다.
7. Web Push·Service Worker·subscription DB와 모바일 push 정책은 확정대로 별도 `NEW_FEATURE`로 유지한다.

### Finding resolution

| ID | 기존 상태 | Resolution |
| --- | --- | --- |
| `TPWA-R01` | `OPEN_BLOCKING` | Teams tab에서 SPA를 시작하지 않는 launcher 구조로 해소한다. 익명 예외는 launcher HTML·소형 script·브랜드 icon에 한정하고 핵심 app bundle은 계속 차단한다. |
| `TPWA-R02` | `OPEN_BLOCKING` | NAA·prefetch·manifest SSO metadata를 추가하지 않아 Activity app registration과 SPA registration 충돌을 만들지 않는다. |
| `TPWA-R03` | `RESOLVED_IN_REVIEW` | NAA auth adapter가 범위에서 제거되어 현재 웹 MSAL bootstrap을 보존한다. |
| `TPWA-R04` | `RESOLVED_IN_REVIEW` | 새 manifest가 SPA root를 Teams tab으로 열지 않으므로 iframe heuristic을 Teams 인증에 사용하지 않는다. |
| `TPWA-R05` | `RESOLVED_IN_REVIEW` | PWA 설치 event 기반 button과 Android·iPhone·PC 수동 안내 fallback을 구현한다. |
| `TPWA-R06` | `RESOLVED_IN_REVIEW` | Teams color 192×192와 outline 32×32 플랫폼 규격을 유지한다. |

## 구현 포함 범위

- Teams manifest 이름·설명·tab URL을 `EMI PMS` launcher 계약으로 변경
- 핵심 bundle을 참조하지 않는 정적 Teams launcher와 제한된 Easy Auth 예외 경로
- `EMI PMS` 공식 사용자 표시명, 한국어 전체 설명과 영문 의미 적용
- 기존 PWA manifest·아이콘을 유지한 설치 prompt·iPhone 수동 추가 안내·설치 후/Teams 숨김
- 로그인 화면과 계정 영역의 PWA 설치 재진입점
- 메일 기본 표시명·본문, Teams 대체 제목, PDF metadata와 Excel 사용자 머리글의 이름 통일
- manifest·PWA·브랜드·Frontend·Backend 회귀 검증과 사용자 검수 checklist

## 구현 제외 범위

- 실제 Azure·Entra·Teams Admin Center·catalog·운영 app revision 변경
- NAA, OBO, 신규 token·session·cookie bridge
- Teams Activity event·수신자·발송 시점 변경과 실제 provider 발송
- Web Push, Service Worker, DB migration
- 내부 `Emi.Qms` namespace·solution과 개발자 전용 식별자의 일괄 rename
- 과거 알림·감사 이력 원문의 소급 수정

## 변경 allowlist

- `frontend/`의 PWA 설치 UX·브랜드 metadata·정적 launcher·관련 tests/styles
- `backend/`의 사용자-facing 브랜드 출력·관련 tests
- `infrastructure/teams/` manifest template
- `infrastructure/azure-pilot/`의 launcher-only 인증 예외·검증 artifact
- `scripts/`의 Teams manifest·PWA·Azure artifact 검증
- `docs/00-product-roadmap.md`
- `tasks/teams-pwa-001-*`

## 완료 Gate

- 익명 launcher와 허용 icon 외 핵심 root·asset·manifest·API 차단 계약 유지
- Teams manifest activity/RSC/Activity app identity 불변
- Android·iPhone·PC 설치 안내 상태 자동 검증과 390px browser 검증
- Frontend lint·typecheck·unit·build, Backend Release build·전체 test, manifest/PWA/Azure artifact script 통과
- 실제 catalog·Azure rollout과 사용자 실제 기기 검수는 별도 승인 대기로 기록
