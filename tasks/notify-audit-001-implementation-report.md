# TASK-NOTIFY-AUDIT-001 구현 보고서

## 1. 상태

- Task: `TASK-NOTIFY-AUDIT-001`
- 유형: `NEW_FEATURE` → 실험 branch Codex 2차 기획 대체 → 구현
- 구현 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- 사용자 검수: `사용자 검수 대기 — 마지막 일괄 검수`
- 구현 기준: [Codex 2차 기획](../docs/35-notification-preference-audit-plan.md)
- Git 경계: 현재 experiment local commit만 허용. 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider 제외

## 2. 해결한 업무 문제

기존 `user_notification_preference_audit_events`는 변경 사실을 append-only로 보존했지만 관리자가 DB 없이 확인할 화면이 없었다. 이제 System Administrator가 기간·행동·알림 종류·사용자/부서로 조회하고, 동일 필터의 요약과 현재 사용자·부서 명칭을 확인하며 필요한 행만 Excel로 보존할 수 있다.

## 3. 포함·제외 범위

포함 범위는 관리자 전용 목록/요약 API, KST 날짜 필터, 이름·부서 검색, fixed option 검증, 20/50/100 pagination, desktop table, 390px 카드, 현재 계정 정보 표시 기준 안내, checkbox 전체선택과 선택 Excel, 조회 인덱스와 export audit다.

일반 사용자 본인 이력 화면, 과거 이름/부서 snapshot 추가, 원장 수정·삭제, Persistent UAT 적용, 실제 사용자 데이터 검수는 제외했다.

## 4. 구현 구조

| 영역 | 구현 |
| --- | --- |
| DB | `0048_notification_preference_audit_query.sql`: 최신순 조회 index와 선택 Excel export kind 허용 |
| Backend | 목록과 요약이 하나의 SQL predicate를 공유하는 store, admin authorization, KST half-open UTC 범위, 검색 escaping, 선택 ID 최대 500 |
| API | `GET /api/admin/notification-preference-audit` |
| Excel | `admin-notification-preference-audit` screen allowlist, 고정 필수 컬럼, 선택행만 `.xlsx` 생성 |
| Frontend | `/admin/system/notification-preference-audit`, 요약 4개·compact filter·desktop table·mobile card·pagination |
| 권한 | `AdminUsersRead`가 있는 System Administrator만 조회·export |
| 개인정보 | 조회 결과는 현재 계정의 표시명·부서만 사용하며 구현 산출물에는 synthetic 사용자와 `example.invalid`만 기록 |

주요 파일은 `NotificationPreferenceAuditStore.cs`, `NotificationPreferenceAuditEndpointExtensions.cs`, `NotificationPreferenceAuditPage.tsx`, `notificationPreferenceAudit.ts`, migration `0048`이다. 선택 Excel 공통 서비스·registry와 `Program.cs`, `App.tsx`, `api.ts`, 공통 style을 연결했다.

## 5. 기술적 결정과 검토한 대안

- 목록과 요약을 별도 조건으로 만들면 숫자가 어긋날 수 있어 동일 SQL predicate를 공유했다.
- `from/to`는 화면의 KST 달력 날짜를 UTC 반개구간으로 바꿔 날짜 경계 누락을 막았다.
- audit table의 과거 조직 snapshot 확장은 별도 migration·개인정보 정책이 필요하므로, 이번에는 “현재 계정 정보”임을 UI와 Excel에 명시했다.
- 전체 Excel 버튼을 다시 만들지 않고 기존 선택 export tray와 checkbox 전체선택을 재사용했다.

## 6. 시행착오 및 폐기한 접근

첫 격리 E2E는 Release 바이너리가 새 API를 포함하지 않아 이전 라우트가 응답했다. Persistent UAT로 우회하지 않고 Release를 다시 빌드한 뒤 disposable PostgreSQL에서 재검증했다. 화면 검증에서 동일 문구가 select option과 table/mobile card에 동시에 존재해 strict locator가 충돌했으며 desktop table scope로 좁혔다.

## 7. 검증 결과

| 검증 | 결과 |
| --- | --- |
| Backend 기능 테스트 | admin-only, 실제 preference 변경→필터/요약/한글 label, 선택 Excel, 잘못된 page size 검증 PASS |
| Frontend unit | 신규 페이지 loading/data/export 계약 포함 PASS |
| Full-Stack E2E | disposable PostgreSQL·외부 provider disabled에서 desktop 1440·mobile 390 PASS |
| Frontend 전체 | `111/111` PASS, typecheck/build PASS, lint error 0·기존 warning 1 |
| Backend 전체 | `410/410` PASS, 실패·skip 0 |
| Migration | fresh isolated DB에서 `0001 → 0049` 적용 PASS |

화면 증거는 [desktop](notify-admin-controls-screenshots/01-notification-preference-audit-desktop-1440.png), [mobile](notify-admin-controls-screenshots/02-notification-preference-audit-mobile-390.png)이다.

## 8. SOP

1. System Administrator로 `관리자 → 알림 설정 변경 이력`을 연다.
2. 기본 최근 30일 또는 최대 366일 범위, 행동, 알림 종류, 사용자·부서를 입력해 조회한다.
3. 요약 카드와 행의 변경 주체→대상, 변경 결과, 버전을 확인한다.
4. 증빙이 필요하면 행 또는 현재 목록 전체를 checkbox로 선택하고 `선택 Excel 내보내기`를 실행한다.
5. 과거 당시 조직명이 아니라 현재 계정 정보가 표시된다는 안내를 함께 해석한다.

장애 시 API 422는 날짜·page size·filter 값을, 403은 관리자 권한을 확인한다. DB 원장은 수정하지 않는다.

## 9. 사용자 매뉴얼

- `사용자 직접`은 본인이 저장/복원한 변경, `관리자 대리`는 관리자가 특정 사용자를 대신해 바꾼 변경이다.
- `켬 → 끔`은 해당 선택 알림이 차단되는 전환이며 필수 알림 잠금 정책은 기존대로 유지된다.
- 모바일은 핵심 관계와 결과만 카드로 보여주고 Excel tray는 화면 정책에 따라 desktop에서 사용한다.

## 10. 사용자 검수 체크리스트

- [x] 자동: 관리자만 접근 가능
- [x] 자동: 날짜·행동·알림 종류·사용자/부서 필터와 요약 일치
- [x] 자동: 선택 Excel 1행과 필수 컬럼
- [x] 자동: 1440px table과 390px card, horizontal overflow 없음
- [ ] 사용자: 실제 운영 용어와 요약 카드 우선순위 확인
- [ ] 사용자: 실제 preference 변경 1건의 표시·Excel 내용 확인

상태: `사용자 검수 대기 — 마지막 일괄 검수`.

## 11. Finding·잔여 위험·rollback

- Open P0/P1/P2: 없음.
- P3: 과거 당시 조직 snapshot이 필요해지면 별도 audit schema change로 다룬다.
- 기존 `main.tsx` Fast Refresh warning과 production chunk warning은 이번 기능 결함이 아니며 완료 원장의 조건부 backlog를 유지한다.
- Rollback은 route/menu/API wiring을 이전 application으로 되돌리되 additive `0048` index와 export event enum은 유지하는 forward-fix 방식이다. audit 원장 데이터는 삭제하지 않는다.

## 12. 종료 산출물

| 산출물 | 상태·위치 |
| --- | --- |
| Implementation report | 완료 — 이 문서 |
| SOP | 완료 — 이 문서 8장 |
| User manual | 완료 — 이 문서 9장 |
| Roadmap update | 완료 — `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md` |
| User validation checklist | 작성됨·자동 검증 완료·사용자 검수 대기 — 이 문서 10장 |
