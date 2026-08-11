# TASK-NOTIFY-POLICY-001 Change 001 — 구현 승인

- changeStatus: `APPROVED_FOR_IMPLEMENTATION`
- changeSource: `USER_EXPLICIT_REQUEST`
- changeDate: `2026-08-11`
- planningSource: `tasks/notify-policy-001-planning.md`
- reviewSource: `tasks/notify-policy-001-review.md`
- planningApproved: true
- implementationApproved: true
- actualProviderApproved: false
- persistentUatApproved: false
- pushApproved: false
- pullRequestApproved: false
- mergeApproved: false
- publicDeploymentApproved: false

## 사용자 지시

사용자는 Fable 5 기획 초안과 Codex 내용 검토 보고 뒤 `시작해`라고 지시하여, 기획안과 review resolution에 따른 제품 구현과 자동 검증을 승인했다.

## 승인 범위

- 이벤트별 인앱·Teams Activity·메일 수신자와 채널 정합화
- Teams 공용 채널 신규 delivery 생성 중단과 기존 이력 보존
- Pending 등록·종결·재검사·재조치와 제조 중단 통합 알림
- 17단계 납품 완료와 18단계 최종 완료 알림 분리
- 담당자 미지정 시 해당 부서 활성 부서장 전원 fallback과 동기화 종료
- 생산계획 종료일·구매품 입고예정일 기반 미완료 업무 `due_date` 동기화
- D-1·하루 초과 알림, Daily Digest와 사용자 알림 설정 정합화
- 일괄 업무 인앱 원본 묶음
- `TASK-PWA-PUSH-001` local commit과 최종 인앱 원본 통합 검증
- additive migration, 자동 테스트, 문서와 사용자 검수 checklist

## 제외·별도 승인 범위

- 실제 Teams·메일·PWA provider 발송
- Persistent UAT write·migration·runtime 교체
- 원격 push·PR·merge·branch 정리
- Azure 공개 배포
- 후속 알림 문구 고도화와 확대 에스컬레이션 신규 기획

## 구현 계약

- Fable planning은 원문 그대로 보존한다.
- 충돌하거나 미결정으로 남은 항목은 `tasks/notify-policy-001-review.md`의 resolution을 우선 적용한다.
- 18단계는 사용자 화면의 인앱·Teams·PWA 없이 영업부서 전체 Mail delivery만 생성한다.
- 기존 완료·취소 업무와 정확한 일정 원본이 없는 업무의 `due_date`를 수정하지 않는다.
- 외부 발송 실패가 원업무를 되돌리지 않고 기존 idempotency·claim/lease·attempt lineage를 보존한다.
