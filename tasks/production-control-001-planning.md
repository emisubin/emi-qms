Baseline verification is complete. I have confirmed: the interview is `COMPLETED_CONFIRMED` (Round 8 confirmed source), the legacy production plan model (`production_plan_templates`/steps, single `planned_date`, check-mark calendar), the form template management baseline (Draft→Active→Archived versioning, Quality/Manufacturing domains, manager bindings), the single global manufacturing step template snapshotted by name/sequence, department fact tables (migrations 0030/0034/0035/0036, latest 0057), and the business-day calendar infrastructure. I will now produce the single first planning draft.

# TASK-PRODUCTION-CONTROL-001 — Item별 생산계획·자동 실적·가로 막대 일정 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/production-control-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 생산계획 항목마다 단일 예정일과 비고만 입력할 수 있고, 각 부서가 업무를 끝내도 생산관리 담당자가 부서 화면을 오가며 진행 상황을 직접 확인해 옮겨야 한다.
- 대상 사용자·역할: 생산관리 정·부 담당자(계획 입력·프로젝트별 연결 수정), 제조/생산계획 양식 관리자와 System Administrator(양식 구성), 구매·자재·제조·품질·물류 담당자(기존 부서 화면 입력만), 그 외 부서(조회 전용).
- 정상 흐름: 관리자가 Item별 생산계획·제조 양식과 실적 연결을 구성해 게시 → 새 프로젝트 생성 시 게시 version 한 세트를 프로젝트에 snapshot → 생산관리 담당자가 계획 시작·종료 입력 → 각 부서는 기존 화면에서 업무 처리 → 생산관리 탭 조회 시 부서 원본 데이터에서 실적 기간·진행률·일정 상태를 결정적으로 계산해 표와 가로 막대 일정으로 표시.
- 예외·복구 흐름: Open Pending은 과거 실적 삭제·단계 후퇴 없이 `차단` 표시, 재검사는 최초 검사 시작~최종 합격, FAT 불필요·취소 대상은 분모 제외, 부서 저장 성공 후 일정 새로고침 실패는 `저장 완료·일정 새로고침 실패`로 구분.
- 확정한 정책과 명시적 제외: **기존 프로젝트는 기존 단일 예정일·체크 캘린더 화면과 생성 당시 양식을 그대로 유지**하고, 새 양식 version으로 생성되는 프로젝트부터 새 화면·자동 실적을 적용한다(Round 7·8). 이에 따라 Round 2의 legacy `planned_date` 복사와 Round 3의 과거 기록 이름 exact-match 연결은 적용 대상이 없어 제외가 확정됐다. canonical interview 본문 §9~§10에 남아 있는 backfill 문구는 Round 8 확정 요약이 명시적으로 대체하며, 이 planning은 Round 8을 따른다(interview Round 기록표가 Round 8 원문을 확정 source로 지정).
- planning으로 넘긴 비차단 미결정 사항: 최초 기준 계획 비교 화면, 일/주/월 확대·축소, 일정표 대상별 막대, 구매 수량 불일치 경고, 기존 프로젝트 전환 이전 기능(§16).

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

새 양식으로 생성된 프로젝트의 상세 생산관리 탭 한곳에서, 관리자가 코드 수정 없이 구성한 Item별 계획 항목의 계획 기간(담당자 입력)과 실적 기간(부서 실데이터 자동 계산), 진행률, 지연·차단 상태를 표와 계획/실적 가로 막대 일정으로 비교할 수 있다.

## 2. 배경과 해결할 업무 문제

- 현재 업무: 생산관리 담당자가 Item template에서 snapshot된 계획 행마다 단일 `예정일`과 비고를 입력하고, 날짜별 체크 표시 캘린더로 확인한다(현재 구현: `backend/src/Emi.Qms.Api/ProductionPlanning/`, `frontend/src/App.tsx`의 생산계획 표·캘린더).
- 시간 손실·누락: 구매·자재·제조·품질·물류에 실제 처리 이력이 있는데도 생산계획과 연결되지 않아 담당자가 부서 탭을 오가며 수기로 대조한다. 단일 예정일·체크 표시는 기간·지연·병행 작업을 표현하지 못한다.
- 현재 우회: 부서별 화면과 전체 흐름 화면을 직접 순회하며 판단한다. 생산계획에는 실제 시작·종료 근거가 남지 않는다.
- 미해결 시 영향: 프로젝트·패널 수가 늘수록 재입력·대조 부담, 누락, 병목 오판, 계획 대비 실적 분석 불가가 누적된다. 또한 생산계획·제조 항목의 전면 교체가 예정되어 있어, 고정 catalog 방식으로는 교체 때마다 코드 수정이 필요해진다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| System Administrator | 전체 양식·연결·관리자 지정·audit 관리 | 전사 관리자 범위 | 양식 관리만. 기존 업무 입력 금지 불변조건 유지 |
| 제조 양식 관리자(사람 단위 지정) | Item별 제조 항목 구성·게시 | 양식 관리(Manufacturing 영역) | 제조 항목 초안·정렬·사용 종료·게시 |
| 생산계획 양식 관리자(사람 단위 지정) | Item별 생산계획 항목·실적 연결 구성·게시 | 양식 관리(신설 ProductionPlanning 영역) | 생산계획 항목·다중 source 연결 초안·게시 |
| 생산관리 정·부 담당자 | 프로젝트 계획 기간 입력, 프로젝트 전용 항목·연결 수정, 실적·근거 확인 | 기존 project scope | 프로젝트 snapshot 안의 계획·항목·연결만. 자동 실적 값은 수정 불가 |
| 구매·자재·제조·품질·물류 담당자 | 자기 입력이 어느 계획 항목 실적으로 반영됐는지 확인 | 기존 project scope | 기존 부서 업무 화면 입력만 |
| 영업·설계·회계 등 기타 부서 | 계획·실적 조회 | 기존 project scope | 없음(기존 `DsReadOnlyBanner` 안내 유지) |

기존 사람 단위 양식 관리자 지정(`form_template_manager_bindings`, Quality/Manufacturing domain)을 재사용해 `ProductionPlanning` domain을 추가한다. Backend가 모든 권한·불변조건의 authoritative layer다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 양식 구성과 게시 (양식 관리자)

1. 생산계획 양식 관리자가 양식 관리 페이지의 생산계획 영역에서 Item(UL891 등)을 선택하고 초안을 만든다.
2. 계획 항목을 추가·정렬하고, 각 항목에 실적 연결을 구성한다. 제조는 같은 Item의 제조 양식 항목 목록에서, 구매·자재·품질·물류는 고정 실적 사건 목록에서 여러 개 선택한다.
3. 시스템이 각 항목에 immutable identity를 자동 부여하고, 게시하면 Active version이 된다. 이후 생성되는 프로젝트부터 적용되며 기존 프로젝트는 바뀌지 않는다는 안내를 표시한다.

### 시나리오 B — 계획 입력과 자동 실적 확인 (생산관리 담당자)

1. 새 프로젝트가 생성되면 시스템이 게시 version의 생산계획 항목·제조 항목·연결 관계를 프로젝트에 snapshot한다.
2. 담당자가 `계획 수정` 화면에서 항목별 계획 시작·종료를 입력한다(같은 날 허용, 역전 시 저장 불가). 필수 항목의 계획 시작·종료가 모두 입력되면 생산계획 단계가 `계획 완료`로 판정된다.
3. 각 부서가 기존 화면에서 업무를 처리하면, 생산관리 탭 조회 시 실적 시작(최초 유효 사실)·실적 종료(모든 활성 대상 완료)·진행률(`완료 수/전체 수`)·일정 상태가 자동 계산되어 표와 가로 막대 일정에 표시된다.
4. 행을 펼치면 구매품목·개별 패널 단위의 근거 날짜·상태·Pending을 확인한다.

### 시나리오 C — 예외 (차단·재검사·항목 교체)

1. 연결 대상에 Open Pending이 생기면 해당 항목은 `차단` 상태와 근거를 우선 표시하고 과거 실적은 유지한다. 해제 후 최신 유효 결과로 재계산된다.
2. 재검사가 있으면 실적 기간은 최초 검사 시작부터 최종 합격까지로 계산된다.
3. 관리자가 새 version에서 항목을 삭제하면 그 항목을 참조하던 연결은 `연결 안 됨`으로 드러나 재연결을 안내한다. 기존 프로젝트 snapshot은 영향받지 않는다.

## 5. 기능 요구사항

### 필수

- [ ] 양식 관리에 `생산계획` 영역 신설: Item별 생산계획 항목 template(초안→사용 시작→보관 versioning, 기존 form template 계약 재사용)과 항목별 다중 실적 source 연결 편집.
- [ ] 제조 항목의 Item별 versioned template 확장(현재 단일 전역 `PANEL_MANUFACTURING` template를 Item scope로 확장)과 기존 `생산계획 단계 설정` 기능의 양식 관리 통합.
- [ ] 모든 template 항목에 version이 바뀌어도 유지되는 immutable identity 부여. 이름·순서 변경에도 연결 유지, 삭제 시 `연결 안 됨` 표시.
- [ ] 고정 실적 사건 catalog(Backend enum·registry): 발주 완료, 전체 입고 확정, IQC 합격, LQC 합격, OQC 합격, 전진검수 합격, FAT 합격, 포장 완료, 출발 처리, 납품 완료(§16-1에서 최종 구성 확정).
- [ ] 프로젝트 생성 시 게시 version의 생산계획 항목·제조 항목·연결 관계 한 세트 snapshot 자동 생성. 이 snapshot 존재 여부가 새/기존 화면 분기 기준.
- [ ] 프로젝트별 계획 시작·종료 입력(validation·optimistic concurrency·field-level audit 기존 계약 유지)과 프로젝트 전용 항목 추가·연결 수정 revision(양식 원본 불변).
- [ ] 조회 시 부서 원본 데이터에서 결정적으로 계산하는 실적 시작·종료, `완료 수/전체 수` 진행률, 일정 상태 projection(단일 Backend projection을 표·펼침·일정이 공유).
- [ ] 프로젝트 상세 생산관리 탭(새 양식 프로젝트): 6열 표(항목명/필수/계획 기간/실적 기간/진행률/일정 상태) + 행 펼침 근거 + 계획/실적 이중 가로 막대 일정(하루 축, 항목 열 고정, 오늘선·주말·공휴일, 열 때 오늘 위치).
- [ ] 390px 카드형 축약 화면(가로 스크롤 없음, 두 줄 축약 막대).
- [ ] 패널 제조 시작 시 프로젝트 snapshot의 제조 항목(immutable identity 포함)을 execution step으로 사용하도록 확장. 기존 execution 데이터는 변경하지 않는다.

### 선택

- [ ] 고정 사건 catalog에 `키팅 완료` 포함(§16-1).
- [ ] 일정 영역 keyboard 탐색 보조(좌우 이동 버튼) — 접근성 표 대체 수단이 이미 필수이므로 여유 시.

### 명시적 제외

- [ ] 기존 프로젝트의 화면 변경, 데이터 이전, legacy `planned_date` 복사, 과거 기록 이름 매칭 연결, 자동 실적 소급 적용.
- [ ] 기존 18단계 workflow 순서·전체 진행률 공식 변경, 부서 원본 업무 화면·확정 상태 전이 재구현.
- [ ] 생산계획·구매 예정일의 `work_items.due_date` 자동 동기화, 지연 인앱·Teams·메일 자동 에스컬레이션, ERP/MES/회계 외부 연동.
- [ ] 실제 회사별 양식 내용 확정·운영 template 일괄 입력.
- [ ] 최초 기준 계획 비교, 일/주/월 확대·축소, 일정표 대상별 막대, 구매 수량 불일치 경고(후속, §16).
- [ ] 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge.

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 양식 관리 — 생산계획 영역 | 기존 양식 관리 페이지(`frontend/src/FormTemplateManagementPage.tsx`) 탭/영역 추가 | Item별 계획 항목 목록, version 상태, 항목별 연결 대상, `연결 안 됨` 경고 | 초안 생성, 항목 편집·정렬, 연결 선택(제조 항목/고정 사건), 게시·보관 | 기존 form template 저장·충돌(409 새로고침 안내)·게시 확인 패턴 재사용 |
| 양식 관리 — 제조 영역(확장) | 같은 페이지 Manufacturing 영역 | Item별 제조 항목 template version | 항목 편집·게시(기존과 동일 UX, Item 선택 추가) | 동일 |
| 프로젝트 상세 생산관리 탭(신규 화면, 새 양식 프로젝트) | 프로젝트 상세 → 생산관리 탭 | KPI → 계획·실적 6열 표 → 계획/실적 가로 막대 일정 → 담당자 | 행 펼침(한 번에 한 항목), `계획 수정` 이동. 조회 화면에 input 미노출 | 부분 refresh 실패 시 `저장 완료·일정 새로고침 실패` 구분 안내, 기존 Action Feedback 계약 재사용 |
| 프로젝트 상세 생산관리 탭(기존 프로젝트) | 동일 | **현행 그대로**: 예정일 표 + 체크 캘린더 | 현행 그대로 | 현행 그대로 |
| 계획 수정 화면(기존 별도 route 확장) | 생산관리 탭 → `계획 수정` | 항목별 계획 시작·종료·비고, 프로젝트 전용 항목, 연결 수정 | 기간 입력, 항목 추가, 연결 변경, 한 번 저장 | 필드 인접 한글 validation, stale version 409, 저장 성공 feedback |

확인할 UX 항목:

- 계획/실적 막대: 계획은 흑백 와이어프레임 외곽선·패턴, 실적은 상태 의미색 채움. 상태는 색 + 채움 패턴 + 텍스트 병용(색 단독 판단 금지).
- 각 막대에 텍스트 설명(aria-label)과 지연 일수 텍스트를 제공하고, 6열 표를 접근 가능한 공식 대체 수단으로 유지.
- 날짜 축은 `BusinessDayCalculator`/`/api/calendar/business-days` 기준의 주말·공휴일·오늘 기준선 표시, 열 때 오늘 위치 자동 스크롤.
- 실적 종료 전 진행 중 막대는 최초 시작일~오늘(또는 최신 유효 처리일)로 표시하되 종료일로 저장하지 않음.
- 390px: 항목별 카드(상태·계획/실적 기간·지연 텍스트·축약 두 줄 막대), 가로 스크롤 없음.
- 권한 부족·조회 전용은 기존 `DsReadOnlyBanner` 의미 유지, 흑백 와이어프레임·사각형 기본 디자인 유지.

## 7. 업무 규칙과 불변조건

- 부서 원본 데이터가 실적의 유일한 source of truth다. 자동 실적 시작·종료는 사용자가 직접 수정할 수 없다.
- 계획 시작일 ≤ 계획 종료일(같은 날 허용). 역전이면 저장하지 않는다.
- 실적 시작 = 연결 대상 중 최초 유효 업무 사실 발생 시각. 실적 종료 = 모든 활성 대상이 완료 기준을 충족한 마지막 시각. 일부 완료 시 종료일은 비우고 `완료 수/전체 수`를 표시한다.
- 실적 날짜는 담당자가 입력한 실제 업무 날짜(도착일·출발일 등) 우선, 없으면 시스템 확정 시각의 한국(Asia/Seoul) 날짜.
- 연결·집계는 immutable identity와 고정 사건 code 기준이며 이름·표시 순서·ordinal로 추측하지 않는다.
- 확정 처리 단위 보존: 구매·자재·IQC는 구매품목·도착분 단위, 제조·LQC·OQC·전진검수·FAT·물류는 개별 패널 단위. OQC는 단계별 판정, 전진검수·FAT는 패널당 aggregate 판정 1회. FAT optional·부분 입고·부분 출하 의미 보존.
- Open Pending은 과거 실적 삭제·workflow 후퇴 없이 현재 `차단` 상태와 근거로 표시하고, 해제·재검사 합격 후 최신 유효 결과로 재계산한다. 재검사 실적 기간은 최초 검사 시작~최종 합격.
- 취소된 패널·품목은 활성 분모에서 제외하되 이력·취소 근거는 보존한다. FAT 불필요 프로젝트는 `해당 없음`으로 분모·지연 제외.
- 일정 상태·진행률은 기존 18단계 workflow 상태·전체 진행률 공식과 분리하며 그 공식을 변경하지 않는다.
- 프로젝트 snapshot은 생성 시점 version으로 고정된다. 이후 template 수정은 기존 프로젝트 의미를 바꾸지 않는다. 프로젝트별 변경은 snapshot에만 revision으로 남고 template 원본을 수정하지 않는다.
- 기존 프로젝트(snapshot 없음)는 기존 화면·판정 방식·데이터를 그대로 유지한다(회귀 0).
- 필수 항목의 계획 시작·종료가 모두 입력되면 생산계획 단계 `계획 완료` — 새 양식 프로젝트에만 적용하고 기존 프로젝트는 현행 `PlannedDate` 판정(`ProductionPlanningDomain.CalculateStatus`) 유지.
- 부서 원본 mutation 성공 후 projection 조회 실패는 원본을 롤백하지 않는다.
- System Administrator는 양식 관리만 가능하고 업무 입력 금지 불변조건을 유지한다.

일정 상태 최소 집합(권장, 기존 디자인 status vocabulary와 대조한 결과): `계획 미입력`, `착수 전`, `진행 중`, `지연`(계획 종료 초과 미완료), `계획 내 완료`, `지연 완료`, `차단`, `연결 안 됨`, `해당 없음`. 기존 `StatusChip`/semantic 상태색 token을 재사용하고 `완료`는 `계획 내 완료`/`지연 완료`로 세분해 지연 이력을 색+패턴+텍스트로 구분한다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| Item별 생산계획 항목 template + version | 관리자가 구성하는 계획 항목 목록, Draft→Active→Archived | 신규(기존 form template versioning 계약 재사용) | `form_template_audit_events` 패턴 재사용 |
| Item별 제조 항목 template + version | 현행 전역 제조 template의 Item scope 확장 | 확장(`manufacturing_step_template_*` 계열) | 동일 |
| 항목 immutable identity | version을 넘어 유지되는 항목 고유 key | 신규 | 연결 무결성의 기준 |
| 실적 사건 catalog | 부서 확정 사건의 고정 code registry | 신규(Backend 고정 enum, DB seed 아님) | code 의미 변경 금지 |
| 계획 항목–실적 source 연결 정의 | 계획 항목 1 : N (제조 항목 identity 또는 사건 code) | 신규 | version에 포함해 게시 |
| 프로젝트 생산관리 snapshot | 프로젝트 생성 시 계획 항목·제조 항목·연결 한 세트 복사 + 프로젝트 revision | 신규 | 존재 여부 = 새/기존 화면 분기 기준 |
| 계획 기간(시작·종료) | 항목별 담당자 입력 | 신규(snapshot 항목 속성) | 기존 생산계획 field-level audit 계약 재사용 |
| 파생 실적 projection | 조회 시 결정적 계산(저장하지 않음) | 신규(읽기 모델) | 원본 audit 불변, projection은 비저장 |
| 기존 `project_production_plans`/`project_production_plan_items` | 기존 프로젝트의 단일 예정일 모델 | 기존 | 변경·이전하지 않음 |

```text
[양식] Draft → Active(게시, 이후 생성 프로젝트 적용) → Archived
[프로젝트 항목] 계획 미입력 → 착수 전 → 진행 중 ⇄ 차단 → 계획 내 완료 | 지연 완료   (수동 항목: 연결 안 됨 / FAT 불필요: 해당 없음)
```

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 양식 관리 권한(domain별 manager binding), 게시 lifecycle, snapshot 생성, 계획 기간 validation, 실적 파생 계산식, 일정 상태 판정, 프로젝트 revision 권한(생산관리 정·부 담당자).
- 필요한 조회와 mutation: 양식 관리 조회/초안/항목 저장/게시(기존 form template endpoint 계약 확장), 프로젝트 생산관리 projection 조회(표·펼침·일정 공용 1개), 계획 기간·프로젝트 전용 항목·연결 revision 저장, 프로젝트 생성 transaction 내 snapshot 생성.
- 권한·validation: 기존 permission/responsibility 체계(`ProductionPlanningPrimary/Secondary`)와 form template `DemandAccess` 패턴 재사용. `ProductionPlanning` domain을 `NormalizeDomain`·manager candidate 조회에 추가.
- transaction·동시성·idempotency: 계획 revision은 기존 optimistic row_version(409 안내 문구 재사용), 양식은 기존 `FOR UPDATE`+row_version. 파생 projection은 같은 원본 facts에 대해 결정적으로 같은 결과(멱등 조회).
- audit trail: 양식은 `form_template_audit_events` 패턴, 프로젝트 계획 변경은 기존 생산계획 field-level audit(`InsertAuditAsync`) 패턴을 새 항목·연결 변경에 확장. 원본 부서 audit는 삭제·수정하지 않는다.
- 외부 provider 영향: 없음(알림·에스컬레이션 제외 확정).

Repository 조사로 확인한 재사용 대상: `FormTemplateStore`의 versioning·binding·audit 계약, `ProductionPlanningStore`의 audit·concurrency 패턴, `BusinessDayCalculator`/`BusinessCalendarStore`, 부서 fact 저장소(`Materials`, `Manufacturing`, `QualityInspections`, `Logistics`, `Procurement`). 세부 컬럼·SQL은 구현 조사에서 확정한다.

## 10. Frontend 고려사항

- route/component: 프로젝트 상세 생산관리 탭에 snapshot 유무 분기(신규 표·Gantt component vs 기존 화면 유지), `FormTemplateManagementPage`에 생산계획 영역 추가, 기존 `생산계획 단계 설정` 화면은 양식 관리로 통합(진입점은 이동 안내). 계획 수정은 기존 별도 route 확장.
- loading/empty/error/success: 기존 state 패턴(`toLoadError`, `DsEmptyState`, skeleton 문구) 재사용. 계획 미입력·항목 없음 empty 문구 제공.
- 공통 Action Feedback: 기존 계약 재사용(저장 성공, validation, 409, 부분 refresh 실패 구분).
- 접근성: 표를 공식 대체 수단으로 유지, 막대별 텍스트 설명, 색+패턴+텍스트 상태 표현, keyboard로 행 펼침 가능.
- 390px/mobile/narrow pane: 카드형 축약(가로 스크롤 없음), PC는 항목 열 고정 + 일정 영역 가로 스크롤 + 오늘 자동 이동.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 18단계 workflow·전체 진행률·`work_items`는 변경 없음. 알림 신규 발송 없음.
- 권한/관리자: 양식 관리자 지정 화면에 `ProductionPlanning` domain 추가. System Administrator 불변조건 유지.
- Excel/PDF/첨부: 기존 생산계획 Excel 양식·선택 export는 기존 프로젝트 경로에서 현행 유지. 새 표의 export 추가는 이번 범위에 넣지 않고 기존 EXPORT 계열 후속으로 둔다(신규 능력 확장 방지).
- Teams/Mail: 영향 없음(제외 확정).
- 삭제·복구/감사: 프로젝트 삭제 lifecycle에 snapshot 테이블 포함(기존 cascade·soft-delete 계약 준수). 취소 패널·품목의 이력 보존 규칙 유지.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A(권장) — 조회 시 결정적 파생 + 별도 template family 2개 | 실적은 저장하지 않고 projection 조회 API에서 원본 fact를 집계. 생산계획 항목 template와 제조 항목 template를 기존 form template 계약의 별도 family로 확장하고 연결 정의를 생산계획 version에 포함 | 원본과 파생의 불일치·재조정 문제가 구조적으로 없음(Round 2 확정과 일치). 기존 versioning·권한·audit 재사용으로 신규 표면 최소. 제조/생산계획 관리 권한 분리가 자연스러움 | 조회 시 집계 비용. 프로젝트당 패널·품목 수가 수백 단위인 현재 규모에서는 단일 프로젝트 범위 집계로 충분하며, 실측 병목 시 캐시는 P3 후속 |
| B — mutation 시 저장형 projection | 부서 저장 transaction 후 실적 테이블 갱신 | 조회 빠름 | 모든 부서 mutation 경로에 결합 추가, 누락·drift 시 재조정 backfill 필요. 사용자가 확정한 “조회 시 계산”과 불일치 |
| C — 단일 통합 ‘생산관리 양식’ family | 계획+제조+연결을 한 template로 관리 | snapshot 단순 | 제조 양식 관리자와 생산계획 양식 관리자의 권한 경계가 한 문서에 섞여 Round 6 권한 분리와 충돌. 기존 제조 template 코드 재사용 어려움 |

권장안은 A다. Gantt는 기존 흑백 와이어프레임 CSS(격자·패턴) 기반으로 구현하고 외부 chart 라이브러리를 도입하지 않는다(신규 의존성 회피).

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음(제외 확정). 검증은 격리 DB·runtime만 사용.
- migration 필요 여부: 있음 — `0058`(다음 번호) additive migration 1건: 신규 template·snapshot·연결 테이블과 domain 확장. 기존 테이블의 destructive 변경·번호 재사용 없음. fresh/existing 모두 검증. rollback은 forward-fix 원칙.
- 외부 발송/실제 데이터 영향: 없음. dry-run·실제 provider 호출 없음.
- runtime 교체 여부: 없음. Development 5174/5081 범위의 기존 규칙만 적용.
- 추가 사용자 승인 필요 작업: push·PR·merge·대표 repo 반영·Persistent UAT 적용(모두 이번 범위 밖, `mainMergeApprovalCount 0/3` 유지). local experiment commit만 기승인(`commitApproved: true`).

## 14. 검증 계획

- 최소 테스트: Backend — 양식 versioning·권한(3개 관리 주체)·snapshot 생성·immutable identity 유지·계획 validation(역전·같은 날)·실적 파생(부분 완료·재검사·Pending 차단·FAT 해당 없음·취소 제외·다중 source 합성)·기존 프로젝트 비회귀. Frontend — 새 표·Gantt·390px 카드·기존 화면 분기 unit test.
- 영향 영역 회귀: 기존 생산계획(예정일·캘린더·Excel), 제조 실행 시작(snapshot 항목 사용), 형 template 관리(Quality/Manufacturing), 프로젝트 생성·삭제 lifecycle, 전체 흐름 진행률 불변 확인. Backend 전체 test suite + Frontend lint/typecheck/unit/build + fresh/existing migration + isolated Full-Stack 시나리오.
- PR/CI: 이번 Task는 local experiment commit까지. CI·PR은 별도 승인 후.
- 사용자 검수: desktop/390px privacy-safe screenshot(양식 관리 생산계획 영역, 새 생산관리 탭 표·Gantt·펼침, 기존 프로젝트 화면 유지 증빙) 포함 user validation checklist를 작성하고 `사용자 검수 대기 — 마지막 일괄 검수`로 추적.

## 15. 완료 기준

- 기능/권한/데이터: §5 필수 항목 전부 구현, 관리자가 코드 수정 없이 항목 전면 교체 가능, 새 양식 프로젝트에서 부서 입력만으로 실적·진행률 자동 반영, 기존 프로젝트 화면·데이터 무변경.
- UX: 표·Gantt가 같은 projection으로 날짜·상태·진행률 일치, 접근성 규칙 충족, 흑백 와이어프레임 유지.
- 자동 테스트: Backend 전체·Frontend lint/typecheck/unit/build·fresh/existing migration·isolated Full-Stack 통과, Open P0/P1/P2 = 0.
- 5종 산출물: Implementation report·SOP·User manual·Roadmap update·user validation checklist의 상태·위치 추적.
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`(완료로 가장하지 않음).
- PR 상태: 적용 없음(local commit만, 게시 별도 승인).

## 16. 미결정 사항

명시적으로 deferred된 비차단 항목이며 구현 착수를 차단하지 않는다. 1번만 이번 범위 안 결정이고 2~6번은 후속 범위 결정이다.

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 고정 실적 사건 catalog 최종 구성: `키팅 완료` 포함 여부와 `발주 완료`의 기준(발주일 입력 시점) | A. Round 8 예시 10개 사건만(권장 — 사용자가 확인한 목록 그대로, 최소 표면) / B. `키팅 완료` 추가 | 대기(fast-track에서는 2차 기획이 권장안 채택 가능) |
| 2 | 최초 기준 계획(baseline) 대비 비교 화면 | 후속 Task 여부 | 대기(후속) |
| 3 | 일정표 일/주/월 확대·축소 | 후속 Task 여부 | 대기(후속) |
| 4 | 일정표의 대상별(패널별) 막대 | 후속 Task 여부 | 대기(후속) |
| 5 | 구매 수량 불일치 경고 | 후속 Task 여부 | 대기(후속) |
| 6 | 기존 프로젝트를 새 화면으로 전환하는 이전 기능 | 필요 시 별도 Task로 결정 | 대기(후속) |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `Admin/FormTemplate*`(domain·family 확장), `ProductionPlanning/*`(snapshot·계획 기간·revision·projection), `Manufacturing/*`(Item별 template·execution step identity), 신규 projection 조회 endpoint, `Projects`(생성 시 snapshot·삭제 guard).
- Frontend: `FormTemplateManagementPage.tsx`, `App.tsx`의 프로젝트 상세 생산관리 탭·계획 수정 화면·생산계획 단계 설정 통합, 신규 표·Gantt·카드 component, `api.ts`/`projects.ts` 계약.
- DB/Migration: `database/migrations/0058_*.sql`(additive).
- Tests/Scripts: Backend 신규·회귀 test, Frontend unit test, Full-Stack 시나리오, screenshot 스크립트.
- Docs: `docs/43-production-control-plan.md`(2차 기획 target), Roadmap·실험 완료 원장 update, SOP·User manual, Implementation report.

## 18. Roadmap 연결

- 선행 Task: TASK-005A(생산계획 기반), TASK-ADMIN-002(양식 관리 versioning), TASK-008A/009A(자재·IQC), TASK-010A/011A(키팅·제조), TASK-012A(품질), TASK-013A(물류), TASK-WORKFLOW-CONTINUITY-001(실데이터 인계 정합) — 모두 experiment scope 완료.
- 후속 Task: §16의 후속 항목, 지연 에스컬레이션·`due_date` 동기화 정책(기존 미확정 보존), 실제 운영 양식 내용 입력, canonical 승격·UAT Gate.
- 현재 Go/No-Go: Roadmap 기본 다음 후보(첨부 storage/운영 전환)와 다른 순서이나 `explicitRoadmapOverrideApproved: true`와 experiment standing instruction으로 기록됨(identity gate `PASS_CREATE`).
- 별도 Task로 분리할 항목: 새 표의 Excel export, 기존 프로젝트 전환 기능, projection 캐시 최적화(P3).

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-27 | Round 8 요약 `요약 확인` | 이 기획의 source of truth로 사용. 기획·구현 승인은 별도 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

experiment fast-track 계약(Codex review 1회 → 승인된 2차 기획 → 구현)을 전제로 한 초안이며, 구현은 2차 기획 확정 계약을 따른다.

1. `0058` additive migration: Item별 생산계획 항목 template·version·item(immutable identity 포함), 제조 template의 Item scope 확장, 연결 정의, 프로젝트 snapshot·revision 테이블. 기존 테이블 destructive 변경 금지, fresh/existing 검증.
2. Backend: 양식 관리 `ProductionPlanning` domain·family 확장(기존 versioning·binding·audit 계약 재사용), 프로젝트 생성 시 snapshot 생성, 계획 기간·프로젝트 revision mutation(validation·optimistic concurrency·field-level audit), 고정 사건 registry와 조회 시 결정적 실적 projection API(부분 완료·재검사·Pending 차단·FAT 해당 없음·취소 제외 규칙 포함), 제조 실행의 snapshot 항목 사용.
3. Frontend: 양식 관리 생산계획 영역과 단계 설정 통합, snapshot 유무로 분기하는 생산관리 탭(6열 표·행 펼침·계획/실적 Gantt·오늘선·휴일), 390px 카드, 계획 수정 화면 확장. 기존 프로젝트 화면 코드 경로는 유지.
4. 검증: Backend 전체, Frontend lint/typecheck/unit/build, migration fresh/existing, isolated Full-Stack, desktop/390px screenshot. 기존 프로젝트 비회귀를 명시적 테스트로 고정.
5. 경계: 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider 금지, local experiment commit만. Implementation report·5종 산출물·완료 원장 update로 종료 추적.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 6
