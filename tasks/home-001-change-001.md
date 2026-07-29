# TASK-HOME-001 Change 001 — 실험 권장안 자동 채택과 구현 경계

- changeStatus: `APPROVED_FOR_LOCAL_EXPERIMENT`
- approvalSource: `USER_STANDING_EXPERIMENT_DIRECTIVE`
- approvalDate: `2026-07-17`
- experimentalImplementationApproved: true
- localCommitApproved: true
- pushApproved: false
- pullRequestApproved: false
- mainMergeApprovalCount: 0/3

## 사용자 결정

- Fable Round 1 권장안 `1-A · 2-A · 3-A · 4-A · 5-A`를 채택한다.
- deferred 권장안은 병목 Top 5, widget 순서 내 업무→프로젝트 병목→Pending→알림, 자동 polling 없이 진입 조회+widget별 재시도로 채택한다.
- interview 추가 왕복 없이 Codex review resolution, 구현, 자동 검증, screenshot과 local commit까지 연속 진행한다.
- 대표 repo, GitHub main, Persistent UAT, 실제 provider와 canonical runtime은 변경하지 않는다.

## 확정 구현 범위

- `/`·`/home` Home과 `/projects` 프로젝트 목록 경로
- 기존 API 기반 read-only widget 4종과 widget별 독립 상태·재시도
- 권한 없는 widget 숨김과 원본 workspace deep link
- Desktop sidebar·모바일 첫 Home tab, 390px·44px·safe-area 검증
- Frontend tests, 기존 007A·007B·MOBILE full-stack 회귀와 synthetic screenshot

## 제외·후속

- Backend·DB·migration·provider 변경 금지
- 예측·추천·사용자 설정·자동 polling·신규 dashboard aggregate 제외
- canonical Roadmap은 실험 branch에서 수정하지 않는다.
- push·PR·merge는 실행하지 않는다. 같은 main merge 대상에 대한 사용자 승인 3회가 충족되기 전 merge 금지이며 현재 `0/3`이다.
