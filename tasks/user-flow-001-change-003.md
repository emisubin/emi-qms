# TASK-USER-FLOW-001 Change 003 — 승인 의미와 대표 폴더 이식 경계

## 1. 정정 목적

과거 interview·planning·technical review의 `implementationApproved: true`는 Phase A 유저플로우 문서 작성 실행을 승인한 당시의 표기다. 이 값이 제품 기능 구현, Fable 재작성, 기존 문서 정렬 또는 외부 게시까지 승인한 것으로 확대 해석되지 않도록 최신 승인 의미를 고정한다.

## 2. 최신 승인 상태

- documentAuthoringApproved: true
- contentReviewComplete: true
- finalUserContentConfirmed: false
- productImplementationApproved: false
- fableRedraftApproved: false
- phaseBDocumentAlignmentApproved: false
- publishingApproved: false
- pushApproved: false
- prApproved: false
- mergeApproved: false

이 Change가 승인 범위 해석의 최신 source다. 과거 산출물의 상태 필드는 실행 이력으로 보존하며 Fable 원문과 planning을 Codex가 사후 수정하지 않는다.

## 3. 대표 폴더 선별 이식

- Fable 원문 `docs/13-user-flow-baseline.md`는 보존 커밋과 byte-identical하게 이식한다.
- Interview·planning·technical review·내용 review·Change 001~002·implementation report를 함께 보존한다.
- USER-FLOW 브랜치에서 중복된 Fable Repository 정책과 runner는 제외한다. 해당 정책은 `TASK-GOV-CODEX-002 Change 012`의 거버넌스 브랜치가 소유한다.
- 일반 Task는 대표 폴더에서 기존 Task 브랜치를 전환해 이어가며 별도 영구 checkout을 만들지 않는다.
- 5174는 대표 폴더의 현재 branch를 자동 갱신으로 반영하고, 환경변수·의존성·Vite 실행 설정 변경 또는 자동 갱신 실패가 있을 때만 재시작한다.

## 4. 보존·제외 범위

### 보존

- 개인 개발 판단용 Fable 원문과 Codex 내용 review
- TASK-007A보다 먼저 유저플로우를 확인하는 승인된 순서
- Frontend·Backend·DB·runtime 무변경
- 디자인 실험 worktree와 5176 runtime

### 제외

- Fable 원문 수정 또는 자동 재작성
- 제품 기능·화면·API·DB·migration 구현
- `docs/02-business-flow.md`·`docs/04-permission-matrix.md` 정렬
- runtime 재시작, 실제 provider 발송, Persistent UAT write
- push·PR·merge·branch 삭제

## 5. 다음 Gate

사용자가 내용 review와 개인 유저플로우 자료를 최종 확인하면 `TASK-USER-FLOW-001`의 문서 검수를 닫는다. 그 확인은 후속 `TASK-007A`의 planning 또는 제품 구현 승인을 대신하지 않는다.
