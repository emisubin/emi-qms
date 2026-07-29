# TASK-MOBILE-001 Change 001 — 실험 권장안 자동 채택과 구현 경계

- changeStatus: `APPROVED_FOR_LOCAL_EXPERIMENT`
- approvalSource: `USER_STANDING_EXPERIMENT_DIRECTIVE`
- approvalDate: `2026-07-17`
- experimentalImplementationApproved: true
- localCommitApproved: true
- pushApproved: false
- pullRequestApproved: false
- mainMergeApprovalCount: 0/3

## 사용자 결정

- Fable Round 1 권장안 `1-A · 2-A · 3-A · 4-A`와 planning 16장의 비차단 권장안 `1-A · 2-A · 3-A · 4-A`를 현재 실험 branch 기본값으로 채택한다.
- interview 추가 왕복 없이 Codex review resolution, 구현, 자동 검증, screenshot과 local commit까지 연속 진행한다.
- 대표 repo, GitHub main, Persistent UAT, 실제 provider와 canonical runtime은 변경하지 않는다.

## 확정 구현 범위

- 기존 권한 필터 `navigationItems`에서 파생한 모바일 핵심 tab bar와 더보기 sheet
- safe-area, `viewport-fit=cover`, content bottom reservation, 44px 이상 touch target
- sheet focus containment·Esc·배경 닫기·trigger focus 복귀
- 기존 내 업무·Pending·프로젝트·알림 route와 배지 재사용
- Frontend tests, 기존 007A/007B full-stack 회귀, synthetic screenshot

## 제외·후속

- 사진 촬영·저장·압축·재시도는 storage·보안·보존·backup 정책 확정 뒤 별도 NEW_FEATURE다.
- Home형 요약은 `TASK-HOME-001`, QR landing은 Roadmap 후속 Task에 남긴다.
- canonical Roadmap 문구는 실험 branch에서 수정하지 않고 implementation report에 차이와 후속 Gate를 기록한다.
- push·PR·merge는 실행하지 않는다. 같은 main merge 대상에 대한 사용자 승인 3회가 충족되기 전 merge 금지이며 현재 `0/3`이다.
