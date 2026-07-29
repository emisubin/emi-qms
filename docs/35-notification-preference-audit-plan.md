# 알림 설정 변경 이력 — Codex 2차 기획

- Task: `TASK-NOTIFY-AUDIT-001`
- 작성자: `CODEX_SECOND_PLANNING`
- 근거: `tasks/notify-audit-001-planning.md`, `tasks/notify-audit-001-review.md`
- 대체 승인: `USER_EXPLICIT_FABLE_SUBSTITUTION_2026_07_20`
- blockingDecisionCount: `0`

## 구현 목표

System Administrator가 사용자 알림 설정 변경 원장을 최근 30일 기준으로 조회하고, 행동·알림 종류·사용자·기간으로 검색하며, 같은 필터의 요약과 선택 Excel을 사용할 수 있게 한다.

## 확정 계약

- `QmsPolicies.AdminUsersRead`를 목록과 Excel 모두의 서버 권한으로 사용한다.
- KST 날짜 입력을 UTC `[fromInclusive, toExclusive)`로 변환하고 최대 366일만 허용한다.
- 기본 30일, page size 50, 허용 page size 20/50/100, 최신순 `(occurred_at_utc desc, id desc)`이다.
- 목록과 요약은 하나의 normalized filter를 공유한다. 요약은 전체·사용자 직접·관리자 대리·알림 끔 전환 네 값이다.
- 사용자 검색은 trim한 최대 100자의 현재 표시명·부서명에 적용한다. 현재 표시정보는 변경 당시 snapshot이 아님을 UI와 Excel에 표시한다.
- 행동·알림 종류·채널·변경 결과 label은 서버 registry를 공통 사용한다.
- 선택 Excel은 audit event ID만 받고 최대 500개, 중복 제거, 존재 여부 전부-or-전무 검증을 수행한다.
- Desktop은 compact table, 390px는 주체→대상·알림 종류·변경 결과·시각 중심 카드로 구성한다.
- 감사 원장과 preference 저장 동작은 변경하지 않으며 조회 자체를 같은 원장에 기록하지 않는다.
- migration `0048`은 전역 최신순 조회 index만 additive로 추가한다.

## 완료 조건

- 관리자 200, 일반 사용자 403, 비인증 401과 필터·날짜 경계·pagination·summary 일치·선택 Excel 검증을 자동화한다.
- Desktop/390px에 loading·empty·error·선택 상태가 있고 horizontal overflow가 없어야 한다.
- Persistent UAT, 실제 사용자 데이터, 실제 provider, push·PR·merge는 제외한다.
