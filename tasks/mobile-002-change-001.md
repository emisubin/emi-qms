# TASK-MOBILE-002 Change 001 — 실험 권장안 채택과 구현 승인 경계

## 1. 사용자 요청과 승인 source

- 모바일 화면을 반응형 PC 축소판이 아니라 접속 환경의 능력에 맞춘 적응형·모바일 전용 화면으로 재구성한다.
- 기기 판별 방식과 모바일 화면 구성을 Fable 기획부터 진행한다.
- 이 실험 branch에서는 인터뷰·채택·확인을 다시 묻지 않고 Fable 권장안, review, 구현, 검증과 screenshot까지 연속 진행한다.
- 대표 repo와 GitHub `main`에는 반영하지 않는다. main merge 승인은 현재 `0/3`이다.

## 2. 승인 상태

- planningApproved: `true` — experiment branch 한정
- implementationApproved: `true` — 본 change와 `tasks/mobile-002-review.md`의 최소 계약 한정
- userValidationCompleted: `false`
- commitApproved: `true` — 구현·검증 완료 뒤 local experiment commit
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false` (`0/3`)

## 3. 자동 채택한 결정

- layout: `≤860px mobile composition`, `≥861px desktop composition`
- touch 보정: `any-pointer: coarse || pointer: coarse || hover: none`
- mode toggle: 이번 Task 제외
- mobile filter: Project·Pending full-screen sheet
- 1차 route: Home, My Work, Project list/detail, Pending list/detail, Notifications
- 전역 shell: compact mobile app bar와 status sheet 포함
- Desktop·URL·API·권한·state transition·audit: 무변경

## 4. Fable 사용량 기준선

Claude `/usage` 주간 퍼센트는 정수 반올림 값이다.

| 측정 시점 | 전체 모델 사용 | 전체 모델 잔여 | Fable 사용 | Fable 잔여 |
| --- | ---: | ---: | ---: | ---: |
| Fable interview·planning 전 | 8% | 92% | 15% | 85% |
| Fable interview 2회·planning 1회 후 | 10% | 90% | 19% | 81% |
| 변화 | +2%p | -2%p | +4%p | -4%p |

후속 Fable Task는 `bash scripts/report-claude-usage.sh`의 privacy-safe fixed projection으로 호출 전후를 측정하고 결과 보고에 포함한다.

## 5. 구현·검증 경계

### 포함

- Frontend adaptive provider, mobile app bar/status surface, 핵심 7개 route mobile presentation
- Project·Pending accessible mobile filter sheet
- desktop/mobile action parity와 capability matrix
- synthetic isolated unit·E2E·screenshot
- privacy-safe usage reporter와 Task 종료 산출물

### 제외

- Backend·API·DB·migration·Persistent UAT·provider·runtime handover
- 별도 모바일 URL·앱·인증/session
- 생산관리·구매·자재·관리자 route 전용 mobile composition
- 사진·offline·upload retry
- 대표 repo·Roadmap canonical 문서·push·PR·merge
