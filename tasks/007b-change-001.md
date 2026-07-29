# TASK-007B change-001 — 실험 구현 자동 진행 결정

- changeType: `EXPERIMENT_IMPLEMENTATION_RESOLUTION`
- source: `USER_EXPLICIT_REQUEST`
- planningSource: `tasks/007b-planning.md`
- reviewSource: `tasks/007b-review.md`
- planningApprovedForExperiment: true
- reviewResolutionApprovedForExperiment: true
- implementationApprovedForExperiment: true
- planningApprovedForCanonicalMain: false
- implementationApprovedForCanonicalMain: false
- pushApproved: false
- prApproved: false
- mainMergeApprovalCount: 0/3

## 사용자 실행 지시

사용자는 현재 실험 branch에서 신규 기능 interview 선택을 Fable 권장안으로 자동 채택하고, Fable planning·Codex review·구현·검증·screenshot을 별도 승인·채택·확인 왕복 없이 완료하도록 명시했다.

## 확정 결정

- Fable planning 16절 결정 1은 권장안 A `계산형 조회`를 채택한다.
- 신규 migration·snapshot table·trigger를 만들지 않는다.
- Codex review의 유지·추가·보류·제거 resolution을 실험 구현 계약으로 사용한다.
- 구현 범위는 프로젝트 목록·상세 병목 aggregate, Pending project filter, 권한-aware server 정렬, Desktop·390px UX와 isolated tests다.
- 대표 repo, Persistent UAT, 실제 provider, push, PR과 main merge는 제외한다.
