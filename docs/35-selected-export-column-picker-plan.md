# EMI QMS 선택 Excel 컬럼 선택 — Fable 2차 기획 (최종 구현 계약)

> 상태: 2차 기획 Draft (experiment fast-track, 구현 계약)
> 작성 단계: Codex 내용 review resolution 반영 완료, 구현 세션 시작 전
> 목적: 1차 기획의 유지 판정 내용과 Codex review Finding 6건 resolution을 하나의 authoritative 구현 계약으로 통합

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-EXPORT-001-COLUMN-PICKER`
- authoringModel: `FABLE_5`
- canonicalTaskId: `TASK-EXPORT-001`
- canonicalChangeId: `change-003`
- interviewSource: `tasks/export-001-column-picker-interview.md`
- firstPlanningSource: `tasks/export-001-column-picker-planning.md`
- codexReviewSource: `tasks/export-001-column-picker-review.md`
- interviewStatus: `COMPLETED_CONFIRMED`
- interviewUserConfirmed: true

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다. 이 문서는 현재 experiment branch 한정의 최종 구현 source of truth이며, 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider·게시에 대한 어떤 승인도 부여하지 않는다(main merge 승인 `0/3`). 1차 기획과 Codex review 원문은 수정하지 않고 판단 이력으로 보존한다.

## 0. 문서 지위와 반영 원칙

- 이 2차 기획은 1차 기획(`tasks/export-001-column-picker-planning.md`)의 유지 판정 내용을 보존하고, Codex review(`tasks/export-001-column-picker-review.md`)의 유지 5·추가 5·보류 4·제거/수정 3 판정과 Finding 6건 resolution을 전부 본문 계약으로 흡수한 단일 구현 계약이다. 구현 세션은 이 문서만으로 범위·권한·상태·data lifecycle·UX·검증·제외 범위를 판단할 수 있어야 하며, 1차 기획과 review는 근거 확인용으로만 참조한다.
- 사용자가 확정하지 않은 새 정책은 만들지 않았다. 비차단 선택은 사용자 standing experiment 규칙(권장안 자동 채택)에 따라 Repository 근거와 trade-off를 남기고 확정했다(18장).
- Interview 문서에 없는 사용자 답변을 추측하지 않았다. 2차 기획 작성은 게시·merge·Persistent UAT 승인이 아니다.

## 1. 한 줄 목표

사용자가 20개 선택 export 화면에서 서버가 자신에게 허용한 컬럼 중 필요한 컬럼만 고른 뒤, 기존 단일 `선택 Excel 내보내기` action으로 선택한 행과 선택한 컬럼만 담긴 안전한 `.xlsx`를 내려받을 수 있게 한다.

## 2. 배경과 해결할 업무 문제

- Change 002로 업무 12개·관리자 8개 화면이 공통 `POST /api/data-exports/selected` 하나로 선택 행 export를 제공하지만, workbook 컬럼은 서버의 화면별 고정 allowlist 전체가 항상 포함된다.
- 보고·공유 목적별로 필요한 컬럼이 다른 사용자는 매번 파일을 열어 열을 수동 삭제해야 하며, 이 과정에서 시간 손실과 편집 실수(필요 열 삭제, 서식 훼손)가 발생한다.
- 자유 입력이나 client-only 컬럼 제어는 민감 필드·권한 우회와 빈/불완전 workbook 위험을 만들므로, 서버 allowlist의 부분집합 선택만 허용하는 구조가 필요하다.
- 이 기능이 없어도 업무는 가능하지만(현재 우회: 파일 후편집), 반복 export 사용자일수록 누적 비용이 크다.

## 3. Codex review resolution 반영표

Review Finding 6건은 모두 본 계약에 다음과 같이 반영되어 종결된다. 이 표는 요약이 아니라 반영 위치의 색인이다.

| Finding | Severity | 본 문서 반영 위치와 계약 |
| --- | --- | --- |
| `COLUMN-METADATA-DRIFT` | P1 | 단일 effective column source(`GetEffectiveColumns(screen, user)` 계약)를 metadata GET·POST validation·workbook filtering·audit flag 네 지점에서 재사용(11.1장, 14장) |
| `COLUMN-REQUIRED-MATRIX-UNSPECIFIED` | P2 | 20개 screen의 필수 column key·한글 label matrix를 고정(7.2장). 구현자 임의 선택 금지 |
| `COLUMN-KEY-BOUNDARY-INCOMPLETE` | P2 | server-issued ASCII kebab-case key, ordinal exact match, trim/case-fold 보정 금지, key 64 bytes 이하, 요청 수는 화면 허용 수 이하(9장, 11.3장) |
| `COLUMN-STALE-FALLBACK-AMBIGUOUS` | P2 | metadata 실패·컬럼 422 시 stale key 미전송, 기본값 전환의 명시 안내, silent fallback 금지(8.4장) |
| `COLUMN-POPOVER-EXTRA-STEP` | P3 | "닫은 뒤에만 export" 강제를 폐기하고 현재 선택으로 즉시 export 허용, 실행 시 panel close와 focus/feedback handoff(8.2장) |
| `FABLE-FIRST-PLAN-PREFACE` | P3 | 이 2차 기획은 첫 byte가 H1이며 preface 0 (본 artifact 형식 자체) |

Review의 유지 판정 항목(서버 allowlist metadata + optional `columns`, 기존 화면 permission·scope 재사용, 화면 체류 중 임시 상태·persistence 제외, mobile simple-mode 보존, 단일 export action·formula/resource/audit 계약 보존)은 1차 기획 내용 그대로 본 계약에 보존한다. 보류 판정 4건(preset persistence, 컬럼 재정렬·이름 변경·계산 필드, form template picker, multi-sheet·CSV/PDF·async·필터 전체 선택)은 6.3장의 명시적 제외로 유지한다.

## 4. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 각 화면 조회 권한 보유 사용자 | 허용 컬럼 조회, 부분집합 선택, 선택 export 실행 | 해당 화면 selected export와 동일한 permission·scope | 없음 (read-only) |
| 매출 열 열람자 | 프로젝트 export에서 매출 관련 컬럼 선택 | 매출 read 권한 보유 시에만 해당 컬럼이 effective set에 존재 | 없음 |
| System Administrator | 관리자 8개 화면에서 동일 기능 | 기존 `UsersManage`·`AdminHistoryRead` 계열 policy 동일 | 없음 |

- 신규 permission을 만들지 않는다. 컬럼 metadata 조회와 export 실행 모두 기존 `CanExportSelectedScreen` 판정(화면별 `ProjectRead`·`MaterialReceiptUpdate`·`QualityInspect`·`PendingRead`·`UsersManage`·`AdminHistoryRead`·본인 고정)을 그대로 재사용한다.
- Frontend 컬럼 UI는 보조 수단이고, 서버가 요청 시점에 column key를 다시 검증하는 것이 최종 차단 지점이다.

## 5. 핵심 사용자 시나리오

### 시나리오 A — 자재 입고 (기본 흐름, 즉시 export)

1. 사용자가 자재 입고 화면에서 행을 선택하고 tray의 `컬럼 선택`을 연다.
2. 서버가 현재 사용자에게 허용한 컬럼 목록(한글 label, 필수 잠금 표시 포함)이 표시되고, 사용자가 수량 관련 컬럼 몇 개를 해제한다.
3. 사용자는 panel을 별도로 닫지 않고 현재 선택 그대로 `선택 Excel 내보내기`를 즉시 실행할 수 있다. 실행 시 panel은 닫히고 기존 action feedback 계약으로 진행 상태·결과가 표시되며, 파일에는 선택 행 × 선택 컬럼만 서버 고정 순서로 담긴다.

### 시나리오 B — 프로젝트 목록 (권한 컬럼 4지점 일치)

1. 매출 read 권한이 없는 사용자에게는 매출 관련 컬럼이 metadata 목록 자체에 나타나지 않는다.
2. 권한 보유 사용자가 매출 컬럼을 포함해 export하면 audit의 민감 매출 flag가 실제 포함 여부로 기록되고, 제외하고 export하면 flag는 false다.
3. 권한 없는 사용자가 조작된 요청으로 매출 column key를 보내면 같은 effective set 판정에 의해 파일·성공 audit 0건의 generic 422로 차단된다.

### 시나리오 C — stale client 복구 (fail-closed + 명시 전환)

1. 사용자가 화면을 오래 열어둔 사이 서버 배포로 컬럼 구성이 바뀌어, 보낸 column key 중 하나가 더 이상 유효하지 않다.
2. 서버가 일부만 조용히 무시하지 않고 요청 전체를 422로 차단하며 파일·audit를 만들지 않는다.
3. 화면은 컬럼 선택 재확인 안내를 action 근처에 표시하고, 사용자 컬럼 선택 상태를 기본값으로 되돌렸음을 명시한다. 다음에 picker를 열면 해당 screen cache를 폐기하고 최신 목록을 다시 불러온다. 이전의 무효 key를 다시 보내지 않는다.

### 시나리오 D — 기존 방식 그대로 쓰는 사용자 (호환)

1. 사용자가 컬럼 선택을 한 번도 열지 않고 기존처럼 행만 골라 export한다.
2. 요청에 `columns`가 없으므로 서버는 화면별 기본 컬럼(현재의 고정 allowlist 전체)으로 파일을 만든다.
3. 결과 파일은 Change 002와 동일하다 — 기존 사용자 경험과 `columns`를 모르는 기존 client 호환이 유지된다.

## 6. 기능 요구사항

### 6.1 필수

- [ ] 20개 registry 화면의 desktop tray에 공통 `컬럼 선택` picker를 제공한다.
- [ ] 서버가 화면별·사용자별 effective 컬럼 metadata(안정 key, 한글 label, 필수 여부)를 단일 source로 제공한다.
- [ ] `POST /api/data-exports/selected`가 optional `columns`를 받아 9장 경계 규칙과 allowlist 부분집합·중복 없음·필수 포함을 검증하고, 위반 시 파일·성공 audit 0건으로 전체 차단한다.
- [ ] `columns` 미전달(기존 client 포함)은 화면별 기본 컬럼으로 기존과 동일하게 동작한다.
- [ ] workbook header·셀은 선택된 허용 컬럼만, 서버 고정 순서로 포함한다. 선택 행 scope·formula-safe·row cap·resource fence·audit 계약은 불변이다.
- [ ] 프로젝트 export의 민감 매출 audit flag는 실제 포함 여부를 기록한다.
- [ ] picker에서 현재 선택으로 즉시 export를 실행할 수 있다(별도 닫기 단계 강제 금지).
- [ ] 20개 화면 registry 전체를 자동 contract test로 검증한다(공통 component 존재만으로 적용을 주장하지 않음).

### 6.2 선택

- [ ] picker 안 `전체 선택`·`기본값 복원` 일괄 action.

### 6.3 명시적 제외

- [ ] 컬럼 선택의 서버 저장·사용자별 preset·localStorage·브라우저 재접속 persistence.
- [ ] 컬럼 순서 변경(재정렬), 컬럼 이름 변경, 신규 컬럼·자유 수식·계산 필드 추가.
- [ ] multi-sheet·CSV/PDF/ZIP, 전체 filter 결과 대량 선택, 비동기 job·파일 storage.
- [ ] form template 버전 export(별도 endpoint·custom 경로)의 컬럼 선택 — 후속 optional.
- [ ] metadata version field·별도 버전 협상 프로토콜.
- [ ] mobile 화면의 컬럼 선택 노출(mobile simple-mode의 bulk export 기본 제외 유지).
- [ ] 기존 migration 수정, Persistent UAT·실제 provider·대표 repo·`main`·push·PR·merge.

## 7. 컬럼 registry와 필수 컬럼 matrix

### 7.1 컬럼 key 계약

- 모든 column key는 server-issued ASCII kebab-case(`a-z`, `0-9`, `-`)이며 화면 scope 안에서 유일하다. key 길이는 64 bytes 이하다.
- key는 내부 GUID·DB 컬럼명·claim이 아니라 export 컬럼의 안정 식별자다. 한글 label은 기존 workbook header 문자열을 그대로 사용한다.
- 비교는 ordinal exact match다. 서버는 trim·case-fold·기타 자동 보정을 하지 않는다. 형식 위반 key는 곧바로 422다.
- 비필수 컬럼의 key 이름은 기존 화면별 `ExcelColumn` header 목록과 1:1로 구현 시 확정하되(추측 금지), 형식·유일성·개수 일치는 registry contract test로 고정한다.

### 7.2 20개 screen 필수 컬럼 matrix (고정)

각 screen에서 아래 필수 컬럼은 UI에서 잠금(해제 불가)이고 서버에서 `columns` 포함 필수다. 필수 컬럼은 내부 GUID가 아니라 사용자가 파일에서 행을 식별하는 표시 코드·제목·날짜의 최소 조합이다. label은 현재 workbook header 기준이며, 구현 시 실제 header 문자열과의 일치를 test로 고정한다.

| # | screen key | 필수 column key | 한글 label |
| --- | --- | --- | --- |
| 1 | `projects` | `project-code` | PJT Code |
| 2 | `my-work` | `work-title`, `project-code` | 업무, PJT Code |
| 3 | `production-planning` | `project-code` | PJT Code |
| 4 | `procurement` | `project-code` | PJT Code |
| 5 | `material-receipts` | `project-code`, `order-item` | PJT Code, 발주 품목 |
| 6 | `material-kitting` | `project-code`, `panel-code` | PJT Code, 면 코드 |
| 7 | `manufacturing` | `project-code`, `panel-code` | PJT Code, 면 코드 |
| 8 | `material-iqc` | `project-code`, `order-item`, `attempt-number` | PJT Code, 품목, 차수 |
| 9 | `quality-inspections` | `project-code`, `panel-code` | PJT Code, 면 코드 |
| 10 | `logistics` | `project-code`, `target-code` | PJT Code, 대상 코드 |
| 11 | `pending` | `issue-number` | 번호 |
| 12 | `notifications` | `notification-title`, `created-at` | 제목, 생성일시 |
| 13 | `admin-users` | `display-name`, `email` | 이름, 업무 이메일 |
| 14 | `admin-departments` | `department-code` | 코드 |
| 15 | `admin-calendar-holidays` | `holiday-date`, `holiday-name` | 날짜, 휴일명 |
| 16 | `admin-permissions` | `permission-code` | 권한 코드 |
| 17 | `admin-master-history` | `entity-type`, `changed-at` | 대상 유형, 변경일시 |
| 18 | `admin-work-history` | `project-code`, `work-title` | PJT Code, 업무 |
| 19 | `admin-notification-deliveries` | `delivery-title`, `created-at` | 제목, 생성일시 |
| 20 | `admin-work-item-escalations` | `project-code`, `work-title` | PJT Code, 업무 |

- 필수 matrix는 registry 상수와 test로 고정한다. 구현 중 label 불일치·컬럼 부재가 발견되면 임의 대체하지 않고 blocking으로 보고한다.
- 매출 관련 컬럼(프로젝트 계열)은 필수가 아니며 권한 gate 대상이다(11.1장).

### 7.3 기본 컬럼

- 화면별 기본 컬럼 = 현재 사용자의 effective 컬럼 전체(현재 Change 002 workbook과 동일). `columns` 미전달과 `기본값 복원`이 이 집합을 사용한다.
- 1차 기획의 metadata `defaultIncluded` field는 채택하지 않는다 — 이번 계약에서 기본값은 항상 "effective 전체"이므로 항상 true인 field는 bounded metadata 원칙에 따라 제거한다(18장 4번).

## 8. 공통 UX 계약 (20개 화면 동일)

### 8.1 진입과 표시

- tray 안 `컬럼 선택` 버튼(선택 건수와 export action 사이)이 경량 popover panel을 연다. 별도 route·전체 화면 dialog는 만들지 않는다.
- panel에는 허용 컬럼 checkbox 목록(필수는 checked+disabled+잠금 안내), `전체 선택`·`기본값 복원`, 선택 수 요약이 있다.
- tray 요약: 기본 상태(전체)면 `기본 컬럼`, 부분 선택이면 `컬럼 N/M`을 export action 근처에 항상 표시한다.

### 8.2 즉시 export (review 제거·수정 1 반영)

- "panel을 닫은 뒤에만 export" 강제를 폐기한다. 사용자는 panel이 열린 상태에서도 현재 컬럼 선택으로 즉시 export를 실행할 수 있다 — panel footer의 export 실행 또는 인접한 기존 `선택 Excel 내보내기` action 중 구현 시 기존 tray 구조에 맞는 하나의 경로로 제공하되, 실행 주체는 기존 `ExcelExportAction` 계약 하나다(export action 중복 금지).
- 실행 시 panel은 닫히고 focus·진행·성공/실패 feedback은 기존 action 계약(`aria-live`, busy 중복 차단, 성공/422/429 문구)으로 넘어간다.

### 8.3 상태·잠금·접근성

- 컬럼 선택 lifecycle: 같은 화면 체류 중(필터·tab 변경, export 성공/실패 포함) 유지, 화면 이탈·새로고침 시 기본값. 서버·localStorage 저장 없음.
- export busy 중에는 행 선택 잠금과 동일하게 컬럼 선택도 잠근다.
- 접근성: checkbox마다 컬럼 한글 label, 선택 수 `aria-live`, keyboard 순회, `Esc`/바깥 click 닫기와 trigger focus 복귀, 좁은 desktop pane에서 popover overflow 0.
- mobile 390px: 기존 simple-mode대로 tray가 노출되지 않으므로 picker도 노출되지 않는다. page-level horizontal overflow 0 유지.

### 8.4 metadata 실패·stale 복구 (review 제거·수정 2, Finding P2 반영)

- 최초 open에서 metadata 조회가 실패하면 panel 안에 오류와 재시도를 표시하고, export는 기본 컬럼(`columns` 미전달)으로 계속 가능하다 — 이 경우 tray 요약은 `기본 컬럼`이므로 화면 표시와 실제 파일이 일치한다.
- custom 선택이 있는 상태에서 컬럼 422 또는 metadata refresh 실패가 발생하면: (1) stale key를 다시 보내지 않고, (2) 컬럼 선택 상태를 기본값으로 되돌리며, (3) "컬럼 선택이 초기화되어 기본 컬럼으로 내보냅니다" 계열의 명시 안내를 action 근처에 표시하고, (4) 해당 screen metadata cache를 폐기해 다음 open 시 재조회한다.
- 조용한 fallback 금지: UI가 `컬럼 N/M`을 표시하는 동안 서버로 다른 집합을 보내거나, 사용자가 고른 열과 다른 파일을 안내 없이 만들지 않는다.

## 9. 업무 규칙과 불변조건

- Client는 header·selector·cell 값을 정의할 수 없다. Client가 보낼 수 있는 것은 서버가 발급한 안정 column key의 집합뿐이다.
- Column key 입력 경계(고정): ASCII kebab-case·64 bytes 이하·ordinal exact match, trim/case-fold 자동 보정 금지, 요청 컬럼 수는 해당 screen의 effective 컬럼 수 이하, 중복 금지.
- 부분 성공 금지: `columns`에 하나라도 형식 위반·중복·미지원·권한 없음(effective set 밖)·필수 누락이 있으면 요청 전체를 generic 422로 차단하고 파일·성공 audit·성공 feedback을 만들지 않는다. 일부 컬럼 silent drop·silent normalization 계약을 만들지 않는다.
- 422 응답은 내부 column key·컬럼 구성 상세를 노출하지 않는 generic 한글 안내를 사용한다(구분: 선택 항목 오류와 컬럼 오류의 안내 문구는 사용자 행동이 다르므로 분리하되, key 원문은 어느 쪽에도 넣지 않는다).
- workbook에는 항상 7.2장의 필수 컬럼이 존재하므로 컬럼 0개 workbook은 구조적으로 불가능하다(기존 builder의 컬럼 0 방어도 유지).
- 컬럼 선택은 export 편의 기능이다. 업무 데이터·상태·권한·알림·workflow write 0, 컬럼 선택 상태의 서버 저장 0.
- 기존 불변조건 전체 보존: 선택 ID 전부-or-전무 scope 재검증, 최대 1,000건 선택·10,000행 cap, 2-slot no-wait fence, formula-safe text, append-only audit, 민감정보·내부 GUID·자유서술 제외 allowlist, 화면당 단일 export action, mobile simple-mode.
- 컬럼 축소는 기존 allowlist의 부분집합이므로 노출 범위를 넓힐 수 없다. 유일한 권한 민감 컬럼(프로젝트 매출 계열)은 단일 effective source에 의해 metadata·요청 검증·workbook·audit flag 네 지점에서 동일하게 gate된다.

## 10. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 화면별 컬럼 정의 registry(key·label·필수·권한 gate) | 서버 코드 상수 (DB 아님) | 신규 (기존 `ExcelColumn` 목록에 key·required 부여) | registry contract test로 고정, FE hardcode 금지 |
| `columns` 요청 필드 | 안정 key 집합, optional | 신규 (기존 POST body 확장) | 서버 재검증, 저장 없음 |
| 컬럼 선택 UI 상태 | 화면(session)별 `Set<key>` client 로컬 상태 | 신규 (client-only) | 서버 저장·감사 없음, reload 시 기본값 |
| `data_export_events` | 기존 append-only 성공 audit | 기존 (`0038`~`0040`) | schema 변경 없음. 프로젝트 매출 flag의 실제 포함 반영만 보정 |

```text
기본 컬럼(전체) → 사용자 부분 선택(필수 잠금) → export 실행(서버 재검증) → 성공(선택 유지)
                                          → 컬럼 422(선택 기본값 초기화 + 명시 안내 + cache 폐기)
```

- migration: 0건. 신규 audit 필드(예: 선택 컬럼 수)는 채택하지 않는다. audit의 목적(대량 export 추적)에는 기존 kind·row count로 충분하고 컬럼 축소는 노출을 넓히지 않는다. 필요해지면 현재 ledger 최신 번호(`0044`) 다음의 additive bounded count로 후속 처리한다.
- 실제 선택 컬럼 key 원문은 audit·log 어디에도 기록하지 않는다.

## 11. API·Backend 계약

### 11.1 단일 effective column source (review P1 resolution)

- `GetEffectiveColumns(screen, user)` 계약의 단일 판정 지점을 `DataExports`에 둔다: 화면별 registry에서 현재 사용자 권한을 적용한 effective 컬럼 목록(key·label·필수·`ExcelColumn` selector)을 반환한다.
- 다음 네 지점은 반드시 이 하나의 결과를 사용하며 각자 별도 목록을 유지하지 않는다.
  1. metadata GET 응답 목록
  2. POST `columns` validation의 allowlist·필수 판정
  3. workbook 생성 시 컬럼 filtering·순서
  4. 프로젝트 audit의 민감 매출 flag(선택된 effective 컬럼에 매출 컬럼이 실제 포함됐는지)
- 프로젝트 매출 permission은 effective set 산출 시 단 한 번 적용된다. 권한이 없으면 매출 key는 metadata에 없고, 요청에 오면 미지원 key와 동일한 generic 422이며, workbook·audit에 나타날 수 없다.
- 프로젝트 화면은 legacy selected 경로가 이 effective source를 사용하도록 확장한다(정확한 내부 명칭은 구현 시 재확인, 추측 금지).

### 11.2 metadata GET

- 신규 `GET /api/data-exports/selected/columns?screen=<key>`: 인증 + 기존 `CanExportSelectedScreen` 통과 시 `{ key, label, required }` 목록을 registry 순서로 반환한다. 미지원 screen은 기존과 같은 422 계약, 권한 없음은 403. mutation·저장·audit 없음. version field 없음.

### 11.3 POST 확장과 validation 순서

- 기존 `POST /api/data-exports/selected` body를 `{ screen, ids, filters, columns? }`로 확장한다. endpoint 분리는 하지 않는다 — 단일 공통 경로가 Change 002 확정 구조다.
- `columns` validation은 concurrency slot 획득·store 조회 전에 수행한다(순수 검증, 자원 점유 0):
  1. `null`/미전달 → 기본 컬럼(호환 경로, 이하 검증 생략)
  2. 빈 배열 → 422 (기본값으로 해석하지 않음, 의도 불명 fail-closed)
  3. 항목 형식: string, ASCII kebab-case, 64 bytes 이하 — 위반 시 422
  4. ordinal 중복 → 422
  5. 개수 > 해당 screen effective 컬럼 수 → 422
  6. effective set 밖 key(미지원·권한 없음 동일 처리) → 422
  7. 필수 key 누락 → 422
- 모든 컬럼 422는 generic 한글 안내이며 파일·성공 audit 0건이다. 검증 통과 시 기존 흐름(gate → 단일 조회 → 전부-or-전무 ID 검증 → workbook → audit → 응답)에 컬럼 filtering만 추가된다.
- workbook 컬럼 순서는 registry 순서다. client 배열 순서는 무시한다(집합 의미).
- audit: 프로젝트 계열만 매출 flag를 실제 포함 여부로 기록하고, 나머지 화면·필드는 기존 그대로다. schema 변경 없음.

### 11.4 기존 계약 보존

- `columns` 미전달 요청의 응답 bytes 의미(컬럼 구성·순서·filename·`X-Export-Row-Count`)는 Change 002와 동일해야 하며 기존 테스트로 회귀를 고정한다.
- 기존 GET export 3종, 프로젝트 legacy selected POST, 20개 audit kind, `ExcelWorkbookBuilder`·formula-safe writer·`ExcelExportConcurrencyGate`·`DataExportAuditStore`는 계약 변경 없이 재사용한다.

Repository 조사 전 내부 클래스명·컬럼명·SQL 형태를 이 문서로 확정하지 않는다(언급한 기존 명칭은 확인된 현재 코드 기준이며, 신규 명칭은 관례 표시다).

## 12. Frontend 계약

- 신규 route 없음. `SelectedExportTray`에 picker popover·컬럼 상태·metadata cache를 추가하고, 20개 화면 사용부는 가능하면 무변경으로 공통 혜택을 받게 한다. 정확한 상태 위치(tray 내부 vs hook 확장)는 기존 test 계약과 대조해 구현 시 확정한다.
- custom `exportFile`을 쓰는 tray 사용부(form template 버전 목록)는 picker를 표시하지 않고 기존 동작을 유지한다 — picker는 공통 registry export 경로에서만 활성화한다.
- `api.ts`에 metadata 조회 helper와 `columns` 전달을 추가한다. Frontend는 컬럼 목록을 hardcode하지 않는다.
- metadata는 screen별 lazy fetch·cache이며, 컬럼 422·refresh 실패 시 8.4장 계약(cache 폐기·기본값 초기화·명시 안내)을 따른다.
- 컬럼 오류 422와 선택 항목 422의 사용자 안내를 구분한다(전자는 컬럼 재확인, 후자는 목록 새로고침 후 재선택).
- 접근성·busy 잠금·390px 미노출은 8장 계약을 따른다.

## 13. 기존 기능과의 연결

- 프로젝트/업무/알림: 각 화면 조회·선택·업무 흐름 불변. 컬럼 선택은 tray 위에만 얹힌다.
- 권한/관리자: 신규 permission 0, 기존 화면별 policy·매출 gate 재사용.
- Excel/PDF/첨부: workbook builder·import 5계열·IQC/검사 PDF 계약 불변. Change 002의 20개 audit kind 불변.
- Teams/Mail: 영향 없음(발송 0).
- 삭제·복구/감사: soft-delete 제외·append-only audit 불변. audit schema 변경 없음.

## 14. 확정 구현안

1차 기획의 후보 A(서버 key registry + metadata GET 1개 + 기존 POST optional `columns`)를 유지하되, review resolution으로 **단일 effective column source 계층**을 명시한 A′로 확정한다.

| 구성 | 확정 내용 | 근거 |
| --- | --- | --- |
| 컬럼 source | `GetEffectiveColumns(screen, user)` 하나를 4지점(metadata·validation·workbook·audit)에서 재사용 | 핵심 위험인 목록 drift(권한 열 노출 또는 정상 요청 거부)를 구조적으로 차단 (review P1) |
| endpoint | metadata GET 1개 + 기존 POST 확장, 분리 endpoint 0 | Change 002 단일 공통 경로 유지, 호환 기본값 |
| 필수 컬럼 | 7.2장 matrix를 registry 상수·test로 고정 | 구현자 임의 선택 차단 (review P2) |
| key 경계 | ASCII kebab-case·ordinal exact·64 bytes·개수 상한 | silent normalization·body abuse 차단 (review P2) |
| 상태 | client 화면 체류 한정, persistence 0, migration 0 | stale drift·개인화 비용 회피 (review 유지 3) |

1차 기획의 후보 B(FE hardcode)·C(list response embed)·D(서버 preset)는 계속 보류·제거한다.

## 15. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated synthetic runtime·DB만 사용.
- migration 필요 여부: 없음(0건). 기존 `0038`~`0040` audit schema·kind 불변, 기존 migration 수정·번호 재사용 금지.
- 외부 발송/실제 데이터 영향: 없음. 실제 사용자·고객 데이터로 export를 실행하지 않는다.
- runtime 교체 여부: 없음.
- 추가 사용자 승인 필요 작업: push·PR·merge(미승인), main merge(승인 `0/3`), Persistent UAT·실제 provider. 이 문서는 어떤 게시 승인도 부여하지 않는다.

## 16. 검증 계획

- **서버 registry contract test (20개 전수)**: 각 screen의 key 형식(kebab-case·64 bytes)·화면 내 유일성·기존 `ExcelColumn` header와의 1:1 대응·7.2장 필수 matrix 일치·필수 컬럼 1개 이상을 registry 전수 순회로 고정한다. 새 screen·컬럼 추가 시 분류 누락으로 실패해야 한다.
- **metadata GET 테스트**: 화면별 권한(business·`UsersManage`·`AdminHistoryRead` 계열) 403, 미지원 screen 422, 매출 권한 유무별 목록 차이, 응답에 GUID·내부 식별자 부재.
- **POST 계약 테스트**: `columns` 미전달 기본 동작 byte-level 회귀(header 동일), 유효 부분집합 성공(header = 선택 key의 registry 순서), 11.3장 validation 순서별 422(빈 배열·형식 위반·중복·개수 초과·미지원 key·권한 없는 매출 key·필수 누락) 각각 파일·성공 audit 0건, 검증이 slot·store 조회 전에 실패하는지, 매출 포함/미포함 audit flag 정확성, formula 재파싱 0, 기존 scope·stale·1,000건 상한·429·취소 회귀.
- **Frontend unit**: picker open/lazy fetch/오류·재시도, 필수 잠금, 전체 선택·기본값 복원, `기본 컬럼`/`컬럼 N/M` 요약, 즉시 export(panel open 상태 실행·close·focus handoff), busy 잠금, 컬럼 422 시 기본값 초기화+명시 안내+cache 폐기, custom `exportFile` tray(form template) picker 미노출, 기존 tray·선택 lifecycle 회귀, lint·typecheck·build.
- **isolated Full-Stack E2E**: 업무 1개(자재 입고 계열)·관리자 1개(사용자 관리 계열)에서 부분 컬럼 선택 → 다운로드 파일 재파싱으로 선택 행 × 선택 컬럼만 존재·필수 컬럼 존재·formula 0 확인. mobile 390 대표 route에서 tray·export 미노출과 page-level overflow 0 재확인.
- **증빙(review 반영)**: 20개 화면 desktop 1440 screenshot(picker 진입 상태) + 대표 popover 상세. 실제 Microsoft Excel 시각 확인과 workbook 재파싱은 업무 1·관리자 1 대표 2종으로 제한하고 확인 후 닫았음을 기록한다. mobile은 기능 부재·overflow 0 대표 증빙으로 충분하다. synthetic data만 사용하며 raw DOM/API/DB 원문은 기록하지 않는다.
- **사용자 검수**: 자동 검증 완료 후 `사용자 검수 대기 — 마지막 일괄 검수`로 종료한다. 완료로 표시하지 않는다.

## 17. 완료 기준과 중단 조건

완료 기준:

- 기능/권한/데이터: 20개 화면 전부에서 컬럼 선택이 동작하고, `columns` 미전달 호환·필수 matrix 보장·effective set 밖 차단·silent drop 0·매출 gate 4지점 일치·audit flag 정확성·migration 0·domain write 0을 충족한다.
- UX: desktop에서 컬럼 상태가 export action 근처에 항상 보이고, 즉시 export·필수 잠금·복원·stale 복구 명시 안내·접근성 계약이 동작하며, mobile 노출 0·overflow 0이다.
- 자동 테스트: 16장 계약 전부 통과, registry 누락 0, 기존 Backend·Frontend·E2E 회귀 0.
- 증빙: 20개 desktop screenshot, 대표 workbook 2종 재파싱·Excel 확인·close 기록, mobile 대표 증빙.
- 5종 산출물: implementation report·SOP·user manual·Roadmap update·user validation checklist의 상태·위치 추적(`docs/12-task-completion-policy.md` 기준).
- 사용자 검수 상태: `대기`. Git: local experiment commit까지만(push·PR·merge 없음).

중단 조건: 권한·민감 컬럼 우회 경로, metadata·workbook 목록 불일치, 필수 컬럼 누락 workbook 성공, 일부 컬럼 silent 무시·silent fallback 파일, 필수 matrix와 실제 컬럼 구성의 충돌, 기존 선택 export·import·mobile simple-mode 회귀, Repository 계약 충돌 발견 시 구현을 중단하고 blocking으로 보고한다.

## 18. 확정한 비차단 결정 (standing rule 자동 채택, review resolution 반영본)

| 번호 | 항목 | 확정 내용 | 근거 |
| ---: | --- | --- | --- |
| 1 | 진입 UX | tray 안 popover picker + 현재 선택 즉시 export(닫기 강제 폐기), 실행 시 close·focus/feedback handoff | 단일 action 보존과 흐름 단축, review P3 resolution |
| 2 | 기본값·lifecycle | 기본 = effective 전체(Change 002 동일), 화면 체류 중 유지, 이탈·reload 시 기본값, persistence 0 | stale drift·개인화 비용 회피, review 유지 3 |
| 3 | 필수 컬럼 | 7.2장 20-screen matrix 고정, UI 잠금 + 서버 필수 검증 | workbook 이해 가능성 균일화, review P2 resolution |
| 4 | metadata 계약 | `{key,label,required}` bounded 목록, `defaultIncluded` field 제거, version field 없음 | 항상 true인 field 제거로 bounded metadata 유지, review 추가 4(무버전 복구) |
| 5 | 요청·오류 경계 | ASCII kebab-case·ordinal exact·64 bytes·개수 상한·빈 배열 422·generic 문구(key 원문 미노출) | silent normalization·body abuse 차단, review P2 resolution |
| 6 | stale 복구 | 컬럼 422/refresh 실패 시 기본값 초기화·명시 안내·cache 폐기, silent fallback 금지 | 사용자 선택과 파일 불일치 차단, review P2 resolution |
| 7 | audit | schema 변경 0·migration 0, 프로젝트 매출 flag만 실제 포함 반영, 컬럼 key 원문 미기록 | 컬럼 축소는 노출 확대가 아님, 기존 append-only 계약 보존 |
| 8 | rollout·증빙 | 20개 화면 동시 적용 + registry 전수 자동 검증, desktop 20 screenshot, workbook 재파싱·Excel 확인은 대표 2종, mobile 대표 증빙 | 공통 component 존재만으로 적용 주장 금지, review 추가 5 |

안전상 blocking 결정은 발견되지 않았다. 사용자 확인이 필요한 신규 정책은 0건이다.

## 19. 예상 변경 범위 (확정 allowlist가 아니라 조사 대상)

- Backend: `DataExports`의 effective column registry·`GetEffectiveColumns` 계약, metadata GET endpoint, `SelectedExportRequest` 확장과 validation, `SelectedExcelExportService` 컬럼 filtering, legacy 프로젝트 selected 경로의 컬럼·audit flag 확장.
- Frontend: `SelectedExcelExport`(tray+picker), `api.ts`(metadata·`columns`), 관련 styles. 20개 화면 사용부는 가능하면 무변경.
- DB/Migration: 없음.
- Tests/Scripts: 서버 registry contract·metadata·POST 계약·회귀 테스트, Frontend unit, isolated Full-Stack E2E 2종, screenshot 수집.
- Docs: Roadmap `TASK-EXPORT-001` Change 003 실험 상태, implementation report·5종 산출물.

## 20. Roadmap 연결

- 선행 Task: `TASK-EXPORT-001` Phase 1·Change 002(완료, 재구현 금지), `TASK-EXPORT-002`(프로젝트 선택 export), `TASK-MOBILE-002` Change 004 mobile simple-mode.
- 후속 Task(별도 분리): 컬럼 preset 저장, 컬럼 재정렬·계산 필드, form template export picker, multi-sheet·CSV/PDF, 전체 filter 결과 대량 선택, 대표 repo·`main` 승격·Persistent UAT 통합.
- 현재 Go/No-Go: Roadmap·실험 완료 원장의 optional next gate(`TASK-EXPORT-001` column picker)와 정확히 일치 — experiment fast-track 한정 Go. canonical queue와 게시 경계는 변경하지 않는다.

## 21. Codex 구현 지시문 (review 권장 순서 반영)

1. `DataExports`에 effective column registry(20-screen key·label·필수 matrix·프로젝트 매출 gate)와 `GetEffectiveColumns` 단일 판정 지점을 추가하고, 20-screen 전수 contract test(형식·유일성·header 1:1·필수 matrix)를 고정한다.
2. metadata GET endpoint를 기존 `CanExportSelectedScreen`·403/422 계약으로 추가하고 권한별 목록 차이를 test로 고정한다.
3. `POST /api/data-exports/selected`에 11.3장 순서의 `columns` validation(모두 generic 422, 파일·audit 0건, slot·조회 전 검증)과 registry 순서 컬럼 filtering을 추가하고, `columns` 미전달 기본 동작·기존 scope/상한/429 회귀를 고정한다.
4. legacy 프로젝트 selected 경로를 같은 effective source로 확장하고 매출 audit flag를 실제 포함 여부로 보정한다.
5. `SelectedExportTray`에 picker(잠금·복원·요약·즉시 export·busy 잠금·metadata cache·8.4장 stale 복구)를 추가하되 custom `exportFile` 사용부에는 노출하지 않고, Frontend unit 계약을 추가한다.
6. isolated Full-Stack E2E 2종(업무·관리자, workbook 재파싱)과 mobile 390 대표 검증, 20개 desktop screenshot·실제 Excel workbook 대표 2종 확인·close 기록을 synthetic data로 수집한다.
7. Backend·Frontend 전체 회귀와 Finding·privacy gate를 통과시킨 뒤 implementation report·5종 산출물·Roadmap 실험 상태를 갱신하고 local experiment commit까지만 수행한다(push·PR·merge·Persistent UAT·provider 금지).

---

- planningApproved: 조건부(본 2차 기획이 review resolution 전부 반영·blocking 0인 조건의 experiment 한정 승인, `tasks/export-001-column-picker-change-001.md`)
- implementationApproved: experiment branch 한정 true(같은 change의 조건부 계약), 대표 repo·main·게시 승인 아님
- persistentUatApproved: false
- mainMergeApprovalCount: `0/3`

openBlockingDecisionCount: 0
