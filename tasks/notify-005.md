# TASK-NOTIFY-005 — 사용자별 알림 설정

## 상태

- Task type: `NEW_FEATURE`
- Branch: `experiment/task-notify-005-preferences`
- Stage: experiment 구현·자동 검증·격리 UI 검증·local commit 완료 / 사용자 검수 대기
- Canonical planning: [Fable 2차 기획](../docs/26-notification-preferences-plan.md)
- Implementation report: [tasks/notify-005-implementation-report.md](notify-005-implementation-report.md)
- 대표 repo·`main`·Persistent UAT·실제 provider: 미반영
- Push·PR·merge: 미수행
- `main` merge 승인: `0/3`

## 자동 검증 체크리스트

- [x] Backend Release build
- [x] 사용자 설정 API·권한·잠금·no-op·stale version·첫 저장 동시성 검증
- [x] WorkItemCreated·DailyDigest·L0 delivery opt-out와 Suppressed 원장 검증
- [x] Migration catalog·fresh DB 전체 적용 검증
- [x] Backend 전체 391건 회귀
- [x] Frontend lint(오류 0, 기존 fast-refresh warning 1), typecheck, 101건 test, production build
- [x] 격리 Full-Stack에서 본인/관리자 화면, 관리자 대리 저장·복원 검증
- [x] 1440 desktop·390 viewport screenshot과 모바일 page-level overflow 0 확인
- [x] 외부 provider disabled, Persistent UAT·대표 runtime 미접촉, 격리 DB·runtime cleanup

## 사용자 검수 체크리스트

상태: `사용자 검수 대기`

- [ ] 내 알림 설정 PC 화면의 정보 밀도와 필수/선택 구분이 이해하기 쉽다.
- [ ] 내 알림 설정 모바일 화면이 PC 축소판이 아니라 한 열 카드 흐름으로 사용하기 편하다.
- [ ] 관리자 지원 화면에서 대상 사용자와 대리 변경 범위가 명확하다.
- [ ] “자동 단계 업무 생성”과 관리자 직접 업무 배정이 서로 다른 범위임을 이해할 수 있다.
- [ ] `설정 저장`·`기본값 복원` 위치와 결과 안내가 충분하다.
- [ ] 필수 알림 4개가 잠겨 있고 해제할 수 없는 이유가 적절하다.

## 검수 증빙

- [내 알림 설정 desktop](notify-005-screenshots/my-preferences-desktop.png)
- [내 알림 설정 390px](notify-005-screenshots/my-preferences-mobile-390.png)
- [관리자 지원 desktop](notify-005-screenshots/admin-preferences-desktop.png)
- [관리자 지원 390px 저장 상태](notify-005-screenshots/admin-preferences-mobile-390.png)

## Git·게시 경계

- Local experiment commit: 완료(이 문서를 포함하는 Task commit)
- Push/PR: 승인 없음
- Merge: 금지, 분리된 승인 3회 필요
- Persistent UAT migration/runtime handover: 별도 승인 필요
- 실제 Teams/Mail provider: 별도 승인 필요
