# TASK-NOTIFY-POLICY-001 — Implementation report

> 상태: 원격 main·Azure 운영 적용·사용자 화면·실제 PWA 수신 검수 완료
> branch: `feat/task-notify-policy-001-policy-alignment`
> 기준선: `origin/main` `30a0c2970611f76cee0c96ebb8f0e6472d7e7aee`

## 해결한 업무 문제

기존 인앱·Teams·메일·에스컬레이션 코드가 사건마다 서로 다른 수신자와 채널을 사용하고, 담당자 부재·제조 중단·일괄 작업에서 중복 또는 추측 배정이 생길 수 있던 문제를 사용자 확정 정책으로 통일했다. PWA push는 별도 사건 목록 없이 실제 인앱 가시성을 그대로 따른다.

## 포함·제외 범위

- 포함: `tasks/notify-policy-001-planning.md`, Codex review와 `change-001` 승인 계약, `TASK-PWA-PUSH-001` local 통합.
- 최초 구현 제외: 실제 Teams·메일·PWA provider 발송, 운영 VAPID key, Persistent UAT migration·runtime, 원격 push·PR·merge·Azure 배포, L2·L3 확대 에스컬레이션 신규 운영. 게시·운영 적용과 PWA 실발송 검수는 후속 승인으로 완료했으며 L2·L3는 계속 제외한다.

## 구현 결정

- 새 자동 TeamsChannel delivery 생성을 중단하고 기존 handler·이력·관리자 조회는 보존했다.
- 일반 업무는 인앱+Teams Activity, Pending·재검사·재조치는 인앱+Teams Activity+메일 필수로 맞췄다.
- Pending 종결은 등록 시점 수신자 snapshot에 인앱+Teams Activity만 보낸다.
- 제조 중단의 별도 참고 알림을 제거하고 긴급 Pending 한 건으로 통합했다.
- 프로젝트 생성은 활성 사용자 전체 3채널, 납기·상태·17단계는 영업담당자+지정 담당자 인앱+Teams, 18단계는 활성 영업부서 전체 메일 전용으로 분리했다.
- 담당자 미지정 시 해당 부서 활성 부서장 전원에게 같은 fallback group 업무를 만들고 첫 처리자가 나머지를 원자적으로 종료한다.
- 부서장 0명은 부서별 409 안내로 차단하며 System Administrator·영업·일반 역할 fallback을 제거했다.
- 패널 업무 행은 유지하되 같은 operation·프로젝트·단계·수신자 알림을 묶었다.
- 생산계획 항목 종료일, 구매품 입고예정일과 구매 집계 원본에서 미완료 업무 due date를 동기화했다.
- 평일 07:30 Digest와 L0·L1만 신규 운영하며 L2·L3 schema·과거 이력은 보존했다.
- PWA 구독·delivery·현재/전체 기기 해제·Service Worker를 인앱 수신자와 연결했다.

## 실제 변경 영역

- DB: `0074_web_push_subscriptions.sql`, `0075_notification_policy_alignment.sql`
- Backend: notification delivery/matrix, Pending·프로젝트 lifecycle, workflow fallback completion, 생산·구매 due date synchronizer, PWA provider·subscription, 개발용 부서장 seed
- Frontend: 알림 설정 taxonomy·필수 표시, 부서장 공유/자동 종료 표시, PWA 설정·Service Worker·로그아웃
- Tests: 채널·수신자·Digest·L0/L1·fallback·묶음·due date·PWA·migration·회귀
- Docs: Roadmap, SOP, 사용자 안내, 이 report와 검수 checklist

## 검증 결과

| 검증 | 상태 | 결과/경계 |
| --- | --- | --- |
| Backend build | PASS | warning/error 0 |
| 알림 정책·PWA·migration 집중 검증 | PASS | 실제 provider 호출 0 |
| 관련 업무 4영역 회귀 | PASS after remediation | 기본 개발 부서장 seed와 자재→생산관리 fallback 중복을 보정 |
| 복수 생산관리 부서장 자재 인계 | PASS | 정확히 부서장 수만큼 업무·알림 생성 |
| Frontend 전체 단위 회귀 | PASS | 210/210 |
| Frontend lint·typecheck | PASS | lint error 0, 기존 `main.tsx` Fast Refresh warning 1, type error 0 |
| Frontend production build | PASS | 기존 large bundle warning만 유지 |
| Backend 전체 회귀 | PASS | 516/516, 22분 31초, skip 0 |
| TeamsChannel 자동 생성 제거 후 알림 전달 회귀 | PASS | 102/102, build warning/error 0 |
| Isolated Full-Stack | PASS | 실제 provider 차단 환경 2/2 |
| Desktop·mobile 화면 | PASS | 1440px·390px screenshot, 가로 overflow 0 |
| PR 첫 Full-Stack 정책 회귀 | FAIL → REMEDIATION | `56/60`; 제품 결함 없이 구버전 idempotency·TeamsChannel·수신자·18단계 delivery 기대값 네 곳을 Change 003으로 동기화 |
| Change 003 집중 재검증 | PASS | 제조 작업 `2/2`; 현장 부서장 필수 정책과 Pending 생성 완료 대기 기준 동기화 |

## 개인정보·secret 검토

- 테스트는 synthetic 사용자·프로젝트·endpoint만 사용한다.
- 실제 이름·회사 이메일·전화번호·UPN과 provider secret을 Task 산출물에 기록하지 않는다.
- 기존 개인정보 안내의 승인된 문의 연락처는 변경하지 않았으며, 이번 diff에 실제 연락처를 추가하지 않았다.
- PWA endpoint와 암호화 key는 API·화면·로그·문서에 노출하지 않는다.
- 최초 local 구현 검증에서는 실제 Teams·메일·push provider를 호출하지 않았다. 후속 운영 검수는 secret·endpoint 원문 없이 채널 결과와 실기기 수신만 확인했다.

## Finding과 잔여 위험

- Open P0/P1/P2: `0/0/0`.
- 실제 Teams·PWA 수신과 PWA 알림 선택 시 인앱 상세 이동을 운영 기기에서 확인했다.
- 직원별 PWA 설치·알림 허용은 자율이며 중앙 등록률을 운영 완료 조건으로 사용하지 않는다.
- 부서장 fallback은 운영 적용 전 모든 업무 부서에 활성 부서장이 있는지 확인해야 한다.

## Rollback·복구

- 게시 전에는 이 branch를 게시하지 않으면 운영 영향이 없다.
- 운영 후 외부 발송 문제는 채널별 `Enabled=false` 또는 dry-run으로 중지하고 인앱·원업무를 유지한다.
- migration `0074`·`0075`는 additive이며 schema 역삭제 대신 application rollback 또는 forward-fix를 사용한다.

## 5종 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성 | 이 문서 |
| SOP | 완료 | `tasks/notify-policy-001-sop.md` |
| User manual | 완료 | `tasks/notify-policy-001-user-manual.md` |
| Roadmap update | 완료 | `docs/00-product-roadmap.md` 3.3G·6.5·Decision Log |
| User validation checklist | 자동 검증·사용자 화면·실제 PWA provider 수신 완료 | `tasks/notify-policy-001-user-validation-checklist.md` |
