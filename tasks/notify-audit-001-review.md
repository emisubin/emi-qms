# TASK-NOTIFY-AUDIT-001 — Codex 내용 Review

## 1. Review 범위와 결론

- reviewSource: `tasks/notify-audit-001-planning.md`
- reviewer: `CODEX`
- taskType: `NEW_FEATURE`
- 결론: `APPROVE_WITH_RESOLUTIONS`
- openBlockingDecisionCount: `0`

Fable 1차 기획은 `TASK-NOTIFY-005`의 저장 원장을 실제 운영 가시성으로 연결하면서 preference mutation·provider delivery·일반 사용자 권한을 건드리지 않는다. 최근 30일 기본, 단일 list+summary 응답, 관리자 전용, 선택 Excel과 모바일 카드형 UX는 사용자 문제와 Repository 공통 패턴에 맞는다. 아래 P2 가능성을 2차 기획에서 명시적으로 해소하는 조건으로 구현 source로 승인한다.

## 2. 사용자 문제·제품 방향 Review

### 유지

- System Administrator 전용 read-only 감사 조회.
- 기존 fixed-field append-only audit를 authoritative source로 사용.
- 기본 최근 30일, preset 7/30/90일과 최대 366일 사용자 지정.
- 같은 filter builder를 쓰는 list+summary 단일 응답.
- 행동·알림 종류·채널·사용자 통합 검색과 최신순 고정 정렬.
- desktop table, 390px 의미 재배치 카드와 선택 Excel.
- 현재 표시명·부서·활성 상태만 join하고 이메일·UPN·provider payload를 추가하지 않는 최소 projection.

### 추가

- 날짜는 KST 화면 입력을 명시적인 UTC `[fromInclusive, toExclusive)`로 정규화해 자정 경계 누락·중복을 막는다.
- 사용자 검색어는 trim 후 최대 100자, empty→null, SQL wildcard를 parameterized `ILIKE`로 처리하고 page/pageSize/filter enum을 fail-closed한다.
- actor/target 표시명·부서는 audit 시점 snapshot이 아니라 현재 표시정보임을 화면과 Excel metadata에 명시한다. historical identity를 추정하지 않는다.
- list와 summary는 하나의 normalized filter object와 공통 SQL predicate builder를 사용하고, test에서 summary 합계와 전체 filtered count 관계를 검증한다.
- 선택 Excel은 audit ID 최대 500개, 중복 제거, 존재·admin scope 전부-or-전무 검증을 사용한다. 화면 filter에 보이지 않는 조작 ID도 server query로 검증한다.
- current page 전체선택과 전체 검색결과 선택을 혼동하지 않게 `현재 페이지 전체 선택` 문구를 사용한다.
- list response의 내부 user ID는 frontend에 필요하지 않으면 노출하지 않고 audit event ID만 selection key로 제공한다.

### 보류

- immutable 사용자명·부서 snapshot migration: 기존 `0041` 원장에 snapshot이 없고 이번 read UI가 과거 identity를 사후 생성할 수 없다. current projection임을 명확히 하고 전체 field-level audit 후속에서 다룬다.
- keyset pagination: append-only 대량 원장에 이상적이지만 현재 UI·API precedent와 구현 비용을 고려해 page 방식으로 시작한다. 실측 drift/성능이 확인되면 후속 최적화한다.
- provider delivery attempt 감사 통합·일반 사용자 본인 audit·조직 default/taxonomy 관리.

### 제거

- 없음. 선택 기능인 요약 카드 shortcut과 사용자 설정 화면의 사전 필터 link도 작은 범위이며 사용자 가치가 분명해 유지한다.

## 3. Repository 경계 Review

### Backend·DB

- `0041`은 target/actor+time index는 있지만 전체 기간 최신순 index가 없어 `(occurred_at_utc desc, id desc)` additive `0048`이 합리적이다.
- 기존 `QmsPolicies.AdminUsersRead`가 관리자 사용자·알림 mutation에 이미 쓰이므로 신규 permission은 필요 없다.
- read endpoint·store를 `Notifications` namespace에 두고 selected Excel registry/service는 기존 `DataExports` 패턴을 확장한다.
- audit query가 read-only라도 Excel export는 기존 export event mutation이므로 ReviewSafe에서 POST 차단을 유지한다.

### Frontend

- 대형 `App.tsx`에 route·navigation wiring만 추가하고 페이지 본체는 별도 `NotificationPreferenceAuditPage.tsx`로 분리한다.
- existing `SelectionCheckbox`, `SelectedExportTray`, `useActionFeedback`을 재사용한다.
- 모바일은 desktop 열 숨김만으로 처리하지 않고 변경 결과·주체→대상·알림·시각을 카드 순서로 재배치한다.

## 4. Finding과 Resolution

| ID | Severity | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `AUDIT_CURRENT_IDENTITY_AS_HISTORY` | P2 | `RESOLVED_IN_REVIEW` | current user join을 audit 시점 snapshot처럼 보이면 과거 책임 정보가 왜곡됨 | UI·Excel에 현재 표시정보임을 명시하고 immutable snapshot은 후속 보류 |
| `AUDIT_FILTER_DRIFT` | P2 | `RESOLVED_IN_REVIEW` | list·summary·Excel이 다른 predicate/label을 쓰면 운영 판단이 불일치 | normalized filter·공통 predicate·server label registry 강제 |
| `AUDIT_DATE_BOUNDARY` | P2 | `RESOLVED_IN_REVIEW` | local date의 inclusive end 처리 오류로 자정 행 누락/중복 | KST 입력→UTC half-open range 계약과 boundary test |
| `AUDIT_SELECTION_SCOPE` | P2 | `RESOLVED_IN_REVIEW` | 조작·stale ID가 Excel에 섞이거나 일부만 silent export될 수 있음 | 최대 500·중복 제거·존재/scope 전부-or-전무 검증 |
| `AUDIT_QUERY_SELF_NOISE` | P3 | `RESOLVED_IN_REVIEW` | 조회를 같은 원장에 기록하면 목록이 스스로 증가 | 조회 audit 미생성, 선택 Excel만 기존 export audit 사용 |

Open P0/P1/P2는 `0/0/0`이다.

## 5. 권장 개발 순서

1. `0048` 정렬 index와 migration 검증.
2. normalized filter·label registry·list+summary store와 관리자 endpoint.
3. selected export screen/column registry와 전부-or-전무 tests.
4. 별도 frontend page, route/navigation, desktop/mobile state UX.
5. isolated Full-Stack에서 실제 preference save/reset으로 감사 생성 후 조회·필터·선택 Excel 검증.
6. 전체 회귀·screenshot·5종 산출물·local commit.

## 6. 2차 기획 지시

Fable은 1차 원문과 이 review를 읽고 다음을 최종 구현 계약에 반영한다.

- recent 30일/page 50/max 366일과 KST→UTC half-open date 규칙
- current identity projection 표시와 snapshot 아님 안내
- 공통 normalized filter·predicate·label registry
- audit ID max 500 selected export 전부-or-전무 검증
- 현재 페이지 전체선택 문구와 모바일 카드 정보 순서
- `0048` additive global time+id index
- blocking decision 0과 local experiment 경계
