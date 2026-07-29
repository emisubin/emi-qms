The full baseline is verified: the interview is `COMPLETED_CONFIRMED` with zero blocking decisions, the Roadmap's next optional gate is exactly this column picker, and I've re-validated the current 20-screen selected-export implementation (single POST endpoint, server-fixed column allowlists, all-or-nothing scope revalidation, 2-slot fence, append-only audit, mobile simple-mode tray hiding) and its tests/migration ledger (latest `0044`). Below is the single first planning draft.

# TASK-EXPORT-001 Change 003 — 선택 Excel 컬럼 선택 1차 기획안

> 상태: Draft (experiment fast-track 1차 기획)
> 작성 단계: Codex 내용 review 전
> 목적: 20개 선택 export 화면에 서버 allowlist 기반 사용자 컬럼 선택을 추가하는 계약 확정

- taskType: `NEW_FEATURE`
- canonicalTaskId: `TASK-EXPORT-001`
- canonicalChangeId: `change-003`
- authoringModel: `FABLE_5`
- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/export-001-column-picker-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다. 이 문서는 현재 experiment branch 한정이며 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider·게시 승인을 부여하지 않는다(main merge 승인 `0/3`).

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 현재 선택 export는 화면별 고정 컬럼만 제공해 사용자가 필요 없는 컬럼을 내려받은 파일에서 수동 삭제해야 한다.
- 사용자 실행 지시: 완료된 Change 002의 20개 화면 선택 export를 보존하면서, 사용자가 내보낼 컬럼을 고르는 optional 후속 기능만 Change 003으로 진행한다. 실험 branch의 비차단 선택은 Fable 권장안을 자동 채택한다.
- 대상 사용자·역할: 각 화면 조회 권한 보유 사용자. 신규 permission 없음.
- 정상 흐름: 행/카드 선택 → tray에서 컬럼 선택(선택하지 않으면 기본 컬럼) → 단일 `선택 Excel 내보내기` → 선택 행 × 선택 컬럼만 담긴 `.xlsx` 다운로드.
- 예외·복구 흐름: 미지원·중복·권한 상실·필수 누락 column key는 파일·성공 audit 0건의 fail-closed 422, stale client는 컬럼 선택 재확인 안내.
- 확정한 정책과 명시적 제외: 기존 checkbox 전체선택·단일 export action·전부-or-전무 scope 재검증·formula-safe·resource fence·audit 불변, mobile simple-mode의 bulk export 기본 제외 유지, 사용자 검수는 마지막 일괄 대기, local experiment commit까지만.
- planning으로 넘긴 비차단 항목: interview 4장의 8개 항목(진입 UX, 기본값·lifecycle, 필수 컬럼, 권한·민감 컬럼 contract, 요청·오류, 접근성·밀도, audit, rollout)은 사용자 standing rule에 따라 본 문서 16장의 권장안으로 확정한다.

Interview 문서에 없는 사용자 답변을 추측하지 않았다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

사용자가 20개 선택 export 화면에서 서버가 자신에게 허용한 컬럼 중 필요한 컬럼만 고른 뒤, 기존 단일 `선택 Excel 내보내기` action으로 선택한 행과 선택한 컬럼만 담긴 안전한 `.xlsx`를 내려받을 수 있게 한다.

## 2. 배경과 해결할 업무 문제

- Change 002로 업무 12개·관리자 8개 화면이 공통 `POST /api/data-exports/selected` 하나로 선택 행 export를 제공하지만, workbook 컬럼은 서버의 화면별 고정 allowlist 전체가 항상 포함된다.
- 보고·공유 목적별로 필요한 컬럼이 다른 사용자는 매번 파일을 열어 열을 수동 삭제해야 하며, 이 과정에서 시간 손실과 편집 실수(필요 열 삭제, 서식 훼손)가 발생한다.
- 자유 입력이나 client-only 컬럼 제어로 해결하면 민감 필드·권한 우회와 빈/불완전 workbook 위험이 생기므로, 서버 allowlist의 부분집합 선택만 허용하는 구조가 필요하다.
- 이 기능이 없어도 업무는 가능하지만(현재 우회: 파일 후편집), 반복 export 사용자일수록 누적 비용이 크다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 각 화면 조회 권한 보유 사용자 | 허용 컬럼 조회, 부분집합 선택, 선택 export 실행 | 해당 화면 selected export와 동일한 permission·scope | 없음 (read-only) |
| 매출 열 열람자 | 프로젝트 export에서 매출 관련 컬럼 선택 | 매출 read 권한 보유 시에만 해당 컬럼이 metadata·요청에서 유효 | 없음 |
| System Administrator | 관리자 8개 화면에서 동일 기능 | 기존 `UsersManage`·`AdminHistoryRead` 계열 policy 동일 | 없음 |

- 신규 permission을 만들지 않는다. 컬럼 metadata 조회와 export 실행 모두 기존 `CanExportSelectedScreen` 판정(화면별 `ProjectRead`·`MaterialReceiptUpdate`·`QualityInspect`·`PendingRead`·`UsersManage`·`AdminHistoryRead`·본인 고정)을 그대로 재사용한다.
- Frontend 컬럼 UI는 보조 수단이고, 서버가 요청 시점에 column key를 다시 검증하는 것이 최종 차단 지점이다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 자재 입고 (기본 흐름)

1. 사용자가 자재 입고 화면에서 행을 선택하고 tray의 `컬럼 선택`을 연다.
2. 서버가 현재 사용자에게 허용한 컬럼 목록(한글 label, 필수 표시 포함)이 표시되고, 사용자가 수량 관련 컬럼 몇 개를 해제한다. 필수 식별 컬럼은 잠겨 있어 해제할 수 없다.
3. tray에 `컬럼 8/12` 같은 요약이 표시되고, `선택 Excel 내보내기` 실행 시 선택 행 × 선택 컬럼만 담긴 파일이 내려온다. 컬럼 순서는 화면 기본 순서와 동일하다.

### 시나리오 B — 프로젝트 목록 (권한 컬럼)

1. 매출 read 권한이 없는 사용자에게는 매출 관련 컬럼이 컬럼 목록 자체에 나타나지 않는다.
2. 권한 보유 사용자가 매출 컬럼을 포함해 export하면 audit의 민감 컬럼 flag가 실제 포함 여부로 기록된다.
3. 권한 없는 사용자가 조작된 요청으로 매출 column key를 보내면 파일·성공 audit 0건의 generic 422로 차단된다.

### 시나리오 C — stale client 복구

1. 사용자가 화면을 오래 열어둔 사이 서버 배포로 컬럼 구성이 바뀌어, 보낸 column key 중 하나가 더 이상 유효하지 않다.
2. 서버가 일부만 조용히 무시하지 않고 전체를 422로 차단하며 파일·audit를 만들지 않는다.
3. 화면은 "컬럼 선택을 다시 확인해 주세요" 계열 안내를 action 근처에 표시하고, 컬럼 선택을 다시 열면 최신 목록을 다시 불러온다.

### 시나리오 D — 기존 방식 그대로 쓰는 사용자

1. 사용자가 컬럼 선택을 한 번도 열지 않고 기존처럼 행만 골라 export한다.
2. 요청에 `columns`가 없으므로 서버는 화면별 기본 컬럼(현재의 고정 allowlist 전체)으로 파일을 만든다.
3. 결과 파일은 Change 002와 동일하다 — 기존 사용자 경험과 기존 client 호환이 유지된다.

## 5. 기능 요구사항

### 필수

- [ ] 20개 registry 화면의 desktop tray에 공통 `컬럼 선택` UI를 제공한다.
- [ ] 서버가 화면별·사용자별 허용 컬럼 metadata(안정 key, 한글 label, 필수 여부, 기본 포함 여부)를 제공한다.
- [ ] `POST /api/data-exports/selected`가 optional `columns`를 받아 allowlist 부분집합·중복 없음·필수 포함을 검증하고, 위반 시 파일·성공 audit 0건으로 전체 차단한다.
- [ ] `columns` 미전달(기존 client 포함)은 화면별 기본 컬럼으로 기존과 동일하게 동작한다.
- [ ] workbook header·셀은 선택된 허용 컬럼만, 서버 고정 순서로 포함한다. 선택 행 scope·formula-safe·row cap·resource fence·audit 계약은 불변이다.
- [ ] 프로젝트 export의 민감 매출 audit flag는 실제 포함 여부를 기록한다.

### 선택

- [ ] 컬럼 선택 popover 안 `전체 선택`·`기본값 복원` 일괄 action.

### 명시적 제외

- [ ] 컬럼 선택의 서버 저장·사용자별 preset·브라우저 재접속 persistence.
- [ ] 컬럼 순서 변경(재정렬), 컬럼 이름 변경, 신규 컬럼·자유 수식·계산 필드 추가.
- [ ] multi-sheet·CSV/PDF/ZIP, 전체 filter 결과 대량 선택, 비동기 job·storage.
- [ ] form template 버전 export(별도 endpoint·custom 경로)의 컬럼 선택 — 후속 optional.
- [ ] mobile 화면의 컬럼 선택 노출(mobile simple-mode의 bulk export 기본 제외 유지).
- [ ] 기존 migration 수정, Persistent UAT·실제 provider·대표 repo·`main`·push·PR·merge.

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 20개 registry 화면 공통 tray | tray 내 `컬럼 선택` 버튼(선택 건수와 export action 사이) | 허용 컬럼 checkbox 목록, 필수 컬럼 잠금 표시, `컬럼 N/M` 요약 | 개별 toggle, 전체 선택, 기본값 복원, 닫기 | export action의 기존 `aria-live` feedback + 컬럼 오류 시 재확인 안내 |

- 진입 UX는 tray 안 popover panel(경량 layer)로 확정한다. 별도 route·전체 dialog는 만들지 않는다. popover는 `Esc`/바깥 click으로 닫히고 focus를 trigger로 되돌리며, 열려 있는 동안에도 export 실행은 허용하지 않고 닫은 뒤 실행하게 한다(단일 action 원칙 유지).
- 기본 상태(전체 컬럼)에서는 tray 요약을 `기본 컬럼`으로, 부분 선택 시 `컬럼 N/M`으로 표시해 현재 상태가 항상 export action 근처에 보이게 한다.
- 필수 컬럼은 checked+disabled와 잠금 안내 문구로 표시해 "왜 해제가 안 되는지"를 화면에서 설명한다.
- export 진행 중(busy)에는 기존 선택 잠금과 동일하게 컬럼 선택도 잠근다.
- 접근성: checkbox마다 컬럼 한글 label, 선택 수 `aria-live`, keyboard 순회와 popover focus 관리. 좁은 desktop pane에서 popover가 화면 밖으로 나가지 않게 max-width·wrap을 적용한다.
- mobile 390px: 기존 simple-mode대로 tray 자체가 노출되지 않으므로 컬럼 선택도 노출되지 않는다. page-level horizontal overflow 0을 유지한다.
- metadata 조회 실패 시 popover 안에 오류와 재시도를 표시하고, export는 기본 컬럼으로 계속 가능하게 한다(기능 저하이지 차단이 아님).

## 7. 업무 규칙과 불변조건

- Client는 header·selector·cell 값을 정의할 수 없다. Client가 보낼 수 있는 것은 서버가 발급한 안정 column key의 집합뿐이며, 서버 allowlist 밖 key는 전체 거부한다.
- 부분 성공 금지: 하나라도 미지원·중복·권한 없음·필수 누락이면 요청 전체를 422로 차단하고 파일·성공 audit·성공 feedback을 만들지 않는다. 일부 컬럼만 조용히 무시하는 계약을 만들지 않는다.
- workbook에는 항상 화면별 필수 식별 컬럼이 1개 이상 존재한다(컬럼 0개 workbook 불가 — 기존 builder의 컬럼 0 방어도 유지).
- 컬럼 선택은 export 편의 기능이다. 업무 데이터·상태·권한·알림·workflow에 어떤 write도 발생시키지 않으며, 컬럼 선택 상태는 화면 로컬 임시 상태로 서버에 저장하지 않는다.
- 기존 불변조건 전체 보존: 선택 ID 전부-or-전무 scope 재검증, 최대 1,000건 선택·10,000행 cap, 2-slot no-wait fence, formula-safe text, append-only audit, 민감정보·내부 GUID·자유서술 제외 allowlist, 단일 export action, mobile simple-mode.
- 컬럼 축소는 기존 allowlist의 부분집합이므로 노출 범위를 넓힐 수 없다. 유일한 권한 민감 컬럼(프로젝트 매출 계열)은 metadata 제공·요청 검증·audit flag 세 지점 모두에서 권한으로 gate한다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 화면별 컬럼 정의(key·label·필수·권한 gate) | 서버 코드 상수 registry (DB 아님) | 신규 (기존 `ExcelColumn` 목록에 key·required 부여) | 코드·test로 고정, FE/BE drift 금지 |
| `columns` 요청 필드 | 안정 key 집합, optional | 신규 (기존 POST body 확장) | 서버 재검증, 저장 없음 |
| 컬럼 선택 UI 상태 | 화면(session)별 `Set<key>` client 로컬 상태 | 신규 (client-only) | 서버 저장·감사 없음, reload 시 기본값 |
| `data_export_events` | 기존 append-only 성공 audit | 기존 (`0038`~`0040`) | schema 변경 없음. 프로젝트 매출 flag의 실제 포함 반영만 보정 |

```text
기본 컬럼(전체) → 사용자 부분 선택(필수 잠금) → export 실행(서버 재검증) → 성공(선택 유지) | 컬럼 422(선택 유지 + 재확인 안내)
```

- 컬럼 선택 lifecycle: 같은 화면에 머무는 동안(필터·tab 변경, export 성공/실패 포함) 유지하고, 화면 이탈·새로고침 시 기본값으로 돌아간다. localStorage·서버 persistence는 제외한다 — 서버 컬럼 구성 변경과의 stale drift, 사용자별 상태 관리 비용 대비 편익이 낮다.
- migration: 0건. 신규 audit 필드(예: 선택 컬럼 수)는 채택하지 않는다. audit의 목적(대량 export 추적)에는 기존 kind·row count로 충분하고, 컬럼 축소는 노출을 넓히지 않으므로 새 감사 차원이 필요하지 않다. 필요해지면 다음 번호(현재 ledger 최신 `0044` 다음)의 additive bounded count로 후속 처리한다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 컬럼 allowlist·필수 컬럼·권한 gate·중복/최소 개수 검증·컬럼 순서. Client 선택은 힌트일 뿐이다.
- 필요한 조회와 mutation:
  - 신규 `GET /api/data-exports/selected/columns?screen=<key>` — 인증 + 해당 화면 `CanExportSelectedScreen` 통과 시, 현재 사용자에게 허용된 컬럼의 `{ key, label, required, defaultIncluded }` 목록을 반환한다. 미지원 screen은 기존과 같은 422 계약. mutation 없음.
  - 기존 `POST /api/data-exports/selected` body를 `{ screen, ids, filters, columns? }`로 확장한다. 신규 endpoint 분리는 하지 않는다 — 단일 공통 경로 유지가 Change 002 확정 구조다.
- 권한·validation: `columns` 존재 시 trim → 비어 있음/중복/미지원 key/권한 없는 key/필수 key 누락을 하나의 generic 422 계약(파일·audit 0건)으로 차단한다. `columns` null/미전달은 기본 컬럼. 빈 배열은 기본값이 아니라 422다(의도 불명 요청을 fail-closed).
- 구현 구조: 화면별 `ExcelColumn` 목록에 안정 ASCII key·required·(프로젝트 한정) 권한 gate를 부여한 컬럼 정의 registry를 `DataExports`에 추가하고, `SelectedExcelExportService`가 선택 key 집합으로 filtering한 컬럼 목록을 기존 `ExcelWorkbookBuilder`에 그대로 전달한다. builder·formula-safe writer는 수정하지 않는다. 프로젝트 화면은 legacy `ExportSelectedProjectsAsync` 경로에 동일 계약을 확장하고 매출 audit flag를 실제 포함 여부로 기록한다(정확한 내부 명칭은 구현 시 재확인, 추측 금지).
- transaction·동시성·idempotency: 기존 계약 불변. 컬럼 filtering은 조회·workbook 생성 사이의 순수 계산이다.
- audit trail: schema 불변. 실제 선택 컬럼 원문(key 목록)은 기록하지 않는다.
- 외부 provider 영향: 없음.

Repository 조사 전 내부 클래스명·컬럼명·SQL 형태를 이 문서로 확정하지 않는다(언급한 기존 명칭은 확인된 현재 코드 기준이다).

## 10. Frontend 고려사항

- route/component: 신규 route 없음. `SelectedExportTray`에 컬럼 선택 popover와 상태를 추가하고, 20개 화면은 기존 tray 사용부 변경 없이 공통으로 혜택을 받는 구조를 우선한다. tray가 컬럼 상태를 내부에서 screen key 기준으로 관리하면 20개 화면 개별 수정을 최소화할 수 있다 — 정확한 상태 위치(트레이 내부 vs `useSelectedRows` 확장)는 구현 시 기존 test 계약과 대조해 확정한다.
- custom `exportFile`을 쓰는 tray 사용부(form template 버전 목록)는 컬럼 선택 UI를 표시하지 않고 기존 동작을 유지한다 — picker는 공통 registry export 경로에서만 활성화한다.
- loading/empty/error/success: popover 최초 open 시 metadata lazy fetch, 화면별 cache, 실패 시 popover 내 오류+재시도, export는 기본 컬럼으로 계속 가능.
- 공통 Action Feedback: 기존 `ExcelExportAction` 계약 유지. 컬럼 오류 422는 서버 메시지가 그대로 action 근처에 표시되며 컬럼 재확인 안내를 포함한다.
- 접근성: 6장 계약(popover focus, `aria-live` count, 잠금 필수 컬럼 label).
- 390px/mobile/narrow pane: mobile은 tray 미노출로 변화 없음. 좁은 desktop pane에서 popover overflow 0.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 각 화면 조회·선택·업무 흐름 불변. 컬럼 선택은 tray 위에만 얹힌다.
- 권한/관리자: 신규 permission 0, 기존 화면별 policy·매출 gate 재사용.
- Excel/PDF/첨부: workbook builder·import 5계열·IQC/검사 PDF 계약 불변. Change 002의 20개 audit kind 불변.
- Teams/Mail: 영향 없음(발송 0).
- 삭제·복구/감사: soft-delete 제외·append-only audit 불변. audit schema 변경 없음.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | 서버 컬럼 key registry + metadata GET 1개 + 기존 POST의 optional `columns` | 단일 source로 FE/BE drift 0, 기존 client 호환, 검증·audit 계약 최소 변경 | 컬럼 key 부여를 위한 registry 정비 필요 |
| B | Frontend가 컬럼 목록을 hardcode하고 POST만 검증 | metadata endpoint 불필요 | FE/BE 이중 정의 drift, 권한 컬럼 노출 판단이 client로 새는 구조적 위험 |
| C | 화면 list response에 허용 컬럼 embed | 요청 1회 절약 | 20개 list endpoint 오염, 기존 response 계약 광범위 변경 |
| D | 사용자별 컬럼 preset 서버 저장 | 재접속 유지 | 신규 저장소·migration·audit 부담, interview 범위(편의 기능·저장 없음) 초과 |

권장안은 A다. B·C·D는 위 근거로 보류·제거한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated synthetic runtime·DB만 사용.
- migration 필요 여부: 없음(0건). 기존 `0038`~`0040` audit schema·kind 불변.
- 외부 발송/실제 데이터 영향: 없음. 실제 사용자·고객 데이터로 export하지 않는다.
- runtime 교체 여부: 없음.
- 추가 사용자 승인 필요 작업: push·PR·merge(미승인), main merge `0/3`, Persistent UAT·실제 provider. 이 문서는 어떤 게시 승인도 부여하지 않는다.

## 14. 검증 계획

- Backend 계약 테스트: metadata GET의 화면별 권한(관리자 3계열 포함)·미지원 screen 422·매출 권한 유무별 컬럼 목록 차이; POST의 `columns` 미전달 기본 동작 회귀, 유효 부분집합 성공(header가 선택 key의 서버 순서와 일치), 빈 배열·중복·미지원 key·권한 없는 매출 key·필수 누락 각각 422+파일·audit 0건, 매출 포함/미포함 시 audit flag 정확성, formula 재파싱 0, 기존 scope·stale·상한·429 회귀.
- Frontend unit: popover open/lazy fetch/오류·재시도, 필수 잠금, 전체 선택·기본값 복원, `컬럼 N/M` 요약, busy 잠금, custom `exportFile` tray(form template)에 picker 미노출, 기존 tray 계약 회귀.
- isolated Full-Stack E2E: 업무 1개(예: 자재 입고)·관리자 1개(예: 사용자 관리) 화면에서 부분 컬럼 선택 → 다운로드 파일 재파싱으로 선택 행 × 선택 컬럼만 존재·formula 0 확인. mobile 390 대표 route에서 tray·export 미노출과 overflow 0 재확인.
- 증빙: 20개 화면 desktop 1440 screenshot(컬럼 선택 진입 상태), 대표 popover 상세 screenshot, 실제 Excel workbook 2종(업무 1·관리자 1, 부분 컬럼) 확인·close 기록, mobile 390 대표 증빙. synthetic data만 사용한다.
- PR/CI: 이번 fast-track은 local experiment commit까지만이며 push·PR·CI는 미승인 범위다.
- 사용자 검수: 자동 검증 완료 후 `사용자 검수 대기 — 마지막 일괄 검수`로 종료한다.

## 15. 완료 기준

- 기능/권한/데이터: 20개 화면에서 컬럼 선택이 동작하고, `columns` 미전달 호환·필수 컬럼 보장·allowlist 밖 차단·부분 무시 0·매출 gate 3지점 일치·audit flag 정확성·migration 0을 충족한다.
- UX: desktop에서 컬럼 상태가 export action 근처에 보이고, 필수 잠금·복원·접근성 계약이 동작하며, mobile 노출 0·overflow 0이다.
- 자동 테스트: 14장 계약 전부 통과, 기존 Backend·Frontend·E2E 회귀 0.
- 5종 산출물: implementation report·SOP·user manual·Roadmap update·user validation checklist 상태·위치 추적.
- 사용자 검수 상태: `대기`. Git: local experiment commit까지만.

중단 조건: 권한·민감 컬럼 우회 경로, 필수/전체 컬럼 0개 workbook 성공, 일부 컬럼 silent 무시, 기존 선택 export·import·mobile simple-mode 회귀, 문서·구현 충돌 발견 시 구현을 중단하고 blocking으로 보고한다.

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| — | 없음 — interview 4장의 비차단 8개 항목은 standing rule에 따라 본 문서 권장안으로 확정(진입 popover UX, session 한정 lifecycle·persistence 제외, 화면별 필수 컬럼 잠금, 서버 metadata+재검증 contract, fail-closed 422, 접근성·밀도 계약, audit schema 불변, 20개 화면 동시 rollout·호환 기본값) | — | 자동 채택 |

안전상 blocking 결정은 발견되지 않았다. 사용자 확인이 필요한 신규 정책은 0건이다.

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `DataExports`의 컬럼 정의 registry(key·required·권한 gate), `SelectedExcelExportService` 컬럼 filtering, metadata GET endpoint, `SelectedExportRequest` 확장, legacy 프로젝트 selected 경로의 컬럼·audit flag 확장.
- Frontend: `SelectedExcelExport`(tray+popover), `api.ts`(metadata 조회·`columns` 전달), 관련 styles. 20개 화면 사용부는 가능하면 무변경.
- DB/Migration: 없음.
- Tests/Scripts: Backend 계약·회귀, Frontend unit, isolated Full-Stack E2E, screenshot 수집.
- Docs: Roadmap TASK-EXPORT-001 Change 003 상태, implementation report·5종 산출물.

## 18. Roadmap 연결

- 선행 Task: `TASK-EXPORT-001` Phase 1·Change 002(완료, 재구현 금지), `TASK-MOBILE-002` Change 004 mobile simple-mode.
- 후속 Task(별도 분리): multi-sheet 보고서, 컬럼 preset 저장, form template export 컬럼 선택, 전체 filter 결과 대량 선택.
- 현재 Go/No-Go: Roadmap·실험 완료 원장의 optional next gate(`TASK-EXPORT-001` column picker)와 정확히 일치 — experiment fast-track 한정 Go.
- 별도 Task로 분리할 항목: 위 후속 목록과 대표 repo·`main` 승격·Persistent UAT 통합.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-19 | 실험 fast-track에서 "다음 작업" 즉시 진행, interview·중간 승인 생략과 권장안 자동 채택 | Change 003 fast-track interview 확정, 본 1차 기획 작성 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

(fast-track에서는 위 항목이 Codex review와 2차 기획 절차로 대체되며, 사용자 직접 검수는 마지막 일괄 대기로 유지된다.)

## 21. Codex 구현 지시문 초안

1. `DataExports`에 화면별 컬럼 key·label·required·(프로젝트) 권한 gate registry를 추가하고, key 유일성·필수 1개 이상·기존 컬럼 목록과의 일치를 test로 고정한다.
2. `GET /api/data-exports/selected/columns` metadata endpoint를 기존 `CanExportSelectedScreen`·422 계약으로 추가한다.
3. `POST /api/data-exports/selected`에 optional `columns` 검증(빈 배열·중복·미지원·권한·필수 누락 → generic 422, 파일·audit 0건)과 컬럼 filtering을 추가하고, 미전달 기본 동작을 회귀 test로 고정한다.
4. 프로젝트 legacy selected 경로에 같은 계약을 확장하고 매출 audit flag를 실제 포함 여부로 보정한다.
5. `SelectedExportTray`에 popover picker(잠금·복원·요약·접근성·busy 잠금·metadata cache/오류)를 추가하되, custom `exportFile` 사용부에는 노출하지 않는다.
6. Backend·Frontend 전체 회귀, isolated Full-Stack E2E 2종(workbook 재파싱 포함), 20개 desktop screenshot·mobile 대표 증빙·실제 workbook 2종 확인을 수행한다.
7. implementation report·5종 산출물·Roadmap 상태를 갱신하고 local experiment commit까지만 수행한다(push·PR·merge·Persistent UAT·provider 금지).

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 0
