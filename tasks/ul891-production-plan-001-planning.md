All baseline research is complete. Here is what I confirmed before drafting:

- **Interview gate**: `COMPLETED_CONFIRMED`, `userConfirmed: true`, 0 blocking decisions, with a standing instruction to auto-adopt Fable recommendations for 8 named non-blocking choices.
- **Data model**: one production plan per project (`project_production_plans` unique on project), LinkedV1 items with `definition_key`, 1:1 actual-source connections, staffing fields from migration `0063`; UL891 sets as `ul891_set_specs → ul891_set_instances → panel_placeholders.set_instance_id`. After migration `0060`, master template versions are edited in place, so the project's own snapshot rows are the only immutable structure source — this drives the snapshot-clone recommendation.
- **Actuals**: derived at read time in `ProductionPlanningStore.ReadLinkedEvidenceAsync`; panel-attributed sources already query `panel_placeholders` per project, so a set filter is a narrow additive change; purchase/material/IQC sources are project-common and cannot be set-attributed.
- **Frontend**: `ProductionControlLinkedPlanReadOnly` + `ProductionControlGantt` render the 계획표/일정표; the edit flow and the UL891 structure fetch (`getUl891SetStructure`) already exist.

Below is the single first-pass planning draft for the runner to persist (I make no repository writes).

---

# TASK-UL891-PRODUCTION-PLAN-001 — UL891 세트별 생산계획 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/ul891-production-plan-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- authoringModel: `FABLE_5`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: UL891 세트형 프로젝트는 `세트 사양 → 실물 세트 인스턴스 → 개별 패널` 계층을 갖지만 생산계획은 프로젝트에 한 벌만 존재해, 세트별 착수·완료·출하 시점 차이를 계획 대비 실적으로 비교할 수 없다.
- 대상 사용자·역할: 생산관리 정·부 담당자만 수정하고 다른 부서는 조회만 한다. 서버 권한이 최종 기준이다.
- 정상 흐름: 프로젝트 생성 transaction에서 활성 세트별 계획 snapshot 생성 → 생산관리 탭에서 전체/세트 scope 전환 → 세트별 계획 기간·담당자·필요 인원·코멘트 저장 → 부서는 기존 화면에서 패널 업무 처리 → 선택 세트의 자동 실적과 일정 막대 갱신.
- 예외·복구 흐름: 세트 추가 시 기존 세트 불변·새 세트만 snapshot 생성, 세트 취소 시 이력 보존·집계 왜곡 금지, 서버 field-level validation, 세트별 revision/CAS로 같은 세트 stale 저장만 409, 계획·revision·audit 단일 transaction 저장과 실패 시 rollback.
- 확정한 정책과 명시적 제외: 신규 UL891 세트형(`structure_mode='Ul891Set'`)만 대상. 비-UL891·기존 평면 UL891은 프로젝트 단위 계획 유지. 패널 원자 실행·master 양식 정책·판매단가/납기/원가/BOM·신규 알림/외부 연동·대표 repo/`main`/Persistent UAT/실제 provider는 제외.
- planning으로 넘긴 비차단 미결정 사항: interview 3장 표의 8개 선택(세트 계획 원자, 전체 scope 의미, 초기값·추가 세트, 실적 source scope, 취소 세트, 기존 세트형 프로젝트, 많은 세트 tab UX, workflow 완료 판정). experiment standing instruction에 따라 이 문서의 Repository 근거 권장안을 자동 채택한다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

생산관리 담당자가 UL891 세트형 프로젝트의 생산관리 탭에서 전체와 실물 세트별 scope를 전환하며 같은 생산계획 항목을 세트별로 계획하고, 해당 세트 패널의 자동 실적·일정 막대와 바로 비교할 수 있게 한다.

## 2. 배경과 해결할 업무 문제

- 현재 `TASK-PRODUCTION-CONTROL-001`의 LinkedV1 모델은 프로젝트당 계획 한 벌(`project_production_plans`는 `project_id` unique)이며, `TASK-UL891-SET-001`의 세트 계층과 연결되어 있지 않다.
- 여러 세트가 서로 다른 시기에 제조·품질·출하되어도 계획표와 계획·실적 일정표는 프로젝트 전체 값 하나만 보여, 어느 세트가 예정 대비 빠르거나 늦는지 시스템에서 알 수 없다.
- 현재 우회 방식은 생산관리 담당자가 별도 문서로 세트 일정을 수기 관리하는 것이며, 패널 수가 커질수록 누락·중복 위험이 커진다.
- 이 기능이 없으면 세트별 진척 비교·지연 감지가 불가능하고, 부분출하·세트 취소가 있는 UL891 프로젝트의 계획 관리가 사실상 시스템 밖으로 나간다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 생산관리 정·부 담당자 | scope 전환, 세트별 계획 기간·담당자·필요 인원·코멘트 저장 | 전체 aggregate와 모든 세트 scope | 활성 세트 scope의 계획 항목 값만 (`ProductionPlanUpdate` 정책 유지) |
| 다른 부서 사용자 | scope 전환과 조회 | 전체 aggregate와 모든 세트 scope | 없음 (기존 read-only 계약 유지) |
| 시스템(파생) | 세트 패널 원본 실적 projection | 선택 세트의 active 패널 | 없음 (자동 실적은 저장하지 않고 조회 시 파생) |

신규 권한 능력은 만들지 않는다. 기존 `ProductionPlanUpdate` 정책과 프로젝트 read 권한을 그대로 재사용한다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 세트별 계획 수립과 비교

1. 생산관리 담당자가 UL891 세트형 프로젝트 상세의 생산관리 탭에 진입한다. 기본 scope는 `전체`다.
2. scope selector에서 세트(예: `사양명 #2`)를 선택하면 생산계획표와 계획·실적 일정표가 함께 그 세트 scope로 바뀌고, 선택 세트의 label·사양·active 패널 수가 표시된다.
3. 담당자가 그 세트의 항목별 계획 기간·담당자·필요 인원·코멘트를 입력하고 저장하면, 해당 세트의 revision·audit이 한 transaction으로 기록되고 다른 세트와 master 양식은 변하지 않는다.
4. 제조·품질·물류 부서가 기존 화면에서 그 세트의 패널 업무를 처리하면, 세트 scope의 자동 실적·진행률·실적 막대가 조회 시 갱신된다.

### 시나리오 B — 세트 추가·취소와 전체 확인

1. 발주 증가로 세트 인스턴스가 추가되면 시스템이 프로젝트의 계획 구조 snapshot을 복제해 새 세트 scope를 만들고(일정 값은 빈 상태), 기존 세트 계획은 바꾸지 않는다.
2. 어떤 세트가 취소되면 그 세트 scope는 read-only 이력으로 남고, 전체 aggregate·완료 판정에서 제외된다.
3. 담당자가 `전체` scope를 선택하면 활성 세트 계획의 read-only aggregate(항목별 계획 기간 envelope, 전체 패널 실적, 세트별 계획 건수)를 확인하고, 계획 상태는 모든 활성 세트가 완료일 때만 `계획 완료`가 된다.

## 5. 기능 요구사항

### 필수

- [ ] UL891 세트형(LinkedV1) 프로젝트에서 활성 실물 세트 인스턴스별 독립 생산계획 scope의 persistence·조회·수정·audit
- [ ] 프로젝트 생성 transaction에서 활성 세트별 계획 scope 생성, 이후 세트 추가 시 그 세트에만 snapshot 복제
- [ ] 생산관리 탭의 생산계획표·계획/실적 일정표에 공유 scope selector(전체 + 세트) — 두 영역이 항상 같은 scope로 전환
- [ ] 세트 scope의 자동 실적: 패널 귀속 source는 선택 세트의 active 패널만 집계
- [ ] 구매·자재·IQC 등 프로젝트 공통 source는 세트 scope에서도 `프로젝트 공통` 표시로 구분 (특정 세트 실적으로 가장하지 않음)
- [ ] 세트별 revision/CAS: 다른 세트 저장을 막지 않고 같은 세트 stale 수정만 409
- [ ] 세트 취소 시 read-only 이력 보존, 전체 aggregate·계획 상태·workflow 판정에서 제외
- [ ] 기존 Ul891Set LinkedV1 프로젝트의 migration backfill (데이터 손실 없음)
- [ ] 비-UL891·평면 UL891·Legacy 모델 프로젝트의 기존 프로젝트 단위 계획 비회귀
- [ ] desktop·390px 대응과 privacy-safe 시각 증빙

### 선택

- [ ] scope selector에서 세트가 많을 때(기준 초과 시) 검색형 선택 UI

### 명시적 제외

- [ ] 비-UL891·기존 평면 UL891 생산계획 UX 변경
- [ ] master 생산계획·제조 양식 version 정책 변경
- [ ] 패널 제조·품질·물류 처리 단위 변경, 세트별 판매단가·납기·원가·BOM 입력
- [ ] 신규 알림·내 업무·Teams·메일·ERP/MES 연동
- [ ] 신규 세트별 Excel export (기존 프로젝트 단위 Excel 계약만 보존; 필요성 미충족으로 후속 판단)
- [ ] 대표 repo·`main`·Persistent UAT·push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 생산관리 탭 조회 (desktop) | 프로젝트 상세 → 생산관리 | scope chip tab(`전체` + 사양별 group의 세트), 선택 scope의 8열 생산계획표·계획/실적 일정표, 선택 세트 label·사양·패널 수 | scope 전환, 항목 펼침으로 근거 확인 | scope별 loading·empty·error 구분, 취소 세트는 read-only 배지 |
| 생산관리 탭 조회 (390px) | 동일 | scope select(한 열), 세트 요약, 계획 카드, 실적 가로 막대 | scope 전환, 카드 근거 펼침 | page-level overflow 0, 동일 상태 구분 |
| 생산계획 입력 | 생산관리 탭 → 수정 흐름 | 선택한 세트 scope의 항목별 기간·담당자·필요 인원·코멘트 입력, `전체` scope는 read-only 안내 | 세트 선택 후 값 입력·저장 | 기존 Action Feedback 패턴: 처리 중 잠금, field-level 오류 focus, stale(409) 안내와 다시 불러오기 |

확인할 UX 항목:

- 현재 어떤 scope를 보고 있는지 항상 명확한가 (선택 세트 label·사양명·인스턴스 번호·active 패널 수 표시)
- 계획표와 일정표가 반드시 같은 scope로 함께 바뀌는가
- `전체`가 read-only aggregate임을 사용자가 이해할 수 있는가 (편집은 세트 선택 안내)
- 프로젝트 공통 실적이 세트 실적과 시각적으로 구분되는가 (`프로젝트 공통` 배지)
- 취소 세트가 별도 group에 read-only로 구분되는가
- 세트가 많아도 desktop chip tab 가로 스크롤과 390px select로 핵심 행동이 가능한가

## 7. 업무 규칙과 불변조건

- 세트 계획의 원자는 실물 세트 인스턴스다. 같은 사양의 인스턴스라도 계획은 독립이다.
- `전체` scope는 활성 세트 계획의 read-only aggregate이며 독립 편집 대상이 아니다.
- 계획 구조(항목 구성·정의 key·실적 연결)는 프로젝트 생성 시 snapshot된 프로젝트 소유 구조 한 벌을 모든 세트가 공유한다. master 양식 변경은 기존 프로젝트·기존 세트에 영향을 주지 않는다 (`0060` 이후 master 현재 양식은 제자리 수정되므로 clone source는 반드시 프로젝트 snapshot이어야 한다).
- 한 세트의 계획 저장은 다른 세트·프로젝트 snapshot·master 양식을 변경하지 않는다.
- 패널 귀속 실적(제조 단계·LQC·OQC 최종 합격·전진검수·FAT·포장·출발·납품)은 선택 세트의 active 패널 원본에서만 파생하며 수동 수정하지 않는다.
- 구매·자재·IQC처럼 세트 귀속이 없는 source를 특정 세트의 독립 실적으로 표시하지 않는다.
- 취소 세트의 계획·이력은 hard delete하지 않고 read-only로 보존하며 활성 집계·완료 판정에서 제외한다.
- 프로젝트 계획 상태(`계획 완료`)는 활성 세트 scope 전부가 기존 완료 기준(필수 항목 계획 기간 입력)을 충족할 때만 성립한다.
- 세트 scope 저장은 계획 row·scope revision·audit을 한 transaction으로 기록하고 실패 시 전체 rollback한다.
- 비-UL891·평면 UL891·Legacy 모델 프로젝트의 기존 계약(단일 프로젝트 계획, Excel 흐름, workflow sync)은 회귀하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| `project_production_plans` | 프로젝트당 1 row, LinkedV1 template version 참조 | 기존 유지 | 변경 없음 |
| 프로젝트 scope 계획 항목 (`project_production_plan_items`, set 미지정) | 프로젝트 소유 구조 snapshot이자 세트 복제의 clone source | 기존 유지 | 세트형 프로젝트에서는 일정 값 직접 편집 금지 |
| 세트 scope 계획 항목 (`project_production_plan_items` + 신규 nullable 세트 인스턴스 참조 컬럼) | 세트별 계획 기간·담당자·필요 인원·코멘트 | 신규(additive 컬럼) | field-level audit, is_active 보존 |
| 실적 연결 (`project_production_plan_connections`) | 항목별 1:1 source 연결. 세트 항목은 같은 definition key의 프로젝트 snapshot 연결을 재사용(복제하지 않음) | 기존 유지 | 변경 없음 |
| 세트 scope revision | 세트별 CAS 단위 (plan id + set instance id, row_version) | 신규 테이블 | 저장마다 증가, 삭제 없음 |
| `ul891_set_instances.status` | Active/Cancelled — scope 활성 판정의 원본 | 기존 유지 | 변경 없음 |
| 자동 실적 | 조회 시 파생 projection (저장하지 않음) | 기존 확장 | 세트 필터만 추가 |

```text
프로젝트 생성(Ul891Set + LinkedV1)
  → 프로젝트 구조 snapshot + 활성 세트별 계획 scope 생성(빈 일정)
  → [세트 추가] 새 세트에만 구조 복제(빈 일정)
  → [계획 입력] 세트 scope 저장 → revision 증가 + audit
  → [세트 취소] scope read-only·집계 제외 (이력 보존)
  → 모든 활성 세트 계획 완료 → 프로젝트 계획 상태 '계획 완료'
```

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: scope 소속 검증(다른 프로젝트/세트 identity 거부), 취소·비활성 scope 쓰기 거부, 날짜 역전·담당자·필요 인원 field-level validation, 세트별 CAS, 세트형 프로젝트의 프로젝트 scope 일정 값 편집 거부(Excel apply 경로 포함), 계획 상태 aggregate 판정.
- 필요한 조회와 mutation (기존 경로 확장, 신규 route family 없음):
  - `GET /api/projects/{projectId}/production-planning`에 선택적 세트 scope query — 미지정 시 세트형 프로젝트는 `전체` aggregate와 함께 사용 가능한 scope 목록(세트 label·사양·인스턴스 번호·상태·active 패널 수·세트별 계획 상태)을 반환. 비-UL891은 기존 응답과 동일(additive 필드만 추가).
  - `PATCH` 동일 경로에 세트 scope 식별자와 scope revision 기대값 추가. 세트형 프로젝트에서 scope 없는 항목 수정은 validation 오류. 부서별 담당자(assignee) 수정은 기존대로 프로젝트 scope 유지.
  - `GET .../history`는 세트 label을 구분해 표시.
- 권한·validation: 기존 `ProductionPlanUpdate` 정책·프로젝트 read 권한 재사용. 신규 정책 없음.
- transaction·동시성·idempotency: 세트 scope 저장은 항목·revision·audit 단일 transaction. 같은 세트 stale이면 409, 다른 세트 저장과 무간섭. plan header row_version은 legacy 경로용으로 보존.
- audit trail: 기존 `project_audit_events` 재사용, 세트 scope 변경임을 식별할 수 있게 기록(허용 entity type의 additive 확장 또는 세트 label 포함 — 구현 시 확정하되 세트 식별이 history에 드러나야 한다).
- 자동 실적: `ReadLinkedEvidenceAsync` 계열 SQL에 선택적 세트 필터 추가(패널 귀속 source만). 프로젝트 공통 source는 필터하지 않고 `프로젝트 공통` flag를 응답에 추가.
- 외부 provider 영향: 없음. 신규 알림·업무 종류를 만들지 않고 기존 `SyncStageWorkItemsAfterSaveAsync` 프로젝트 단위 sync를 유지한다.

Repository 조사 전 내부 클래스명, 컬럼명과 SQL 형태를 확정하지 않는다 (위 명칭은 현재 구현 확인 결과이며, 신규 명칭은 구현 시 기존 규약에 맞춘다).

## 10. Frontend 고려사항

- route/component: 기존 프로젝트 상세 생산관리 탭 내부만 변경. `ProductionControlLinkedPlanReadOnly`·`ProductionControlGantt`·생산계획 입력 흐름(`frontend/src/App.tsx`)에 공유 scope selector를 추가하고, scope 목록은 planning 응답의 additive 필드로 수신(별도 set-structure 재호출 불필요).
- loading/empty/error/success: scope 전환마다 구분. scope 전환 중 이전 세트 데이터 잔상 금지(프로젝트/scope key로 state 재초기화 — DESIGN-000 Change 003의 비동기 혼입 차단 패턴 재사용).
- 공통 Action Feedback: UX-001 A2의 구조화 feedback·오류 focus·중복 submit 잠금·stale 안내 패턴 재사용.
- 접근성: scope selector는 tab/select 역할과 선택 상태를 aria로 표기, 표·막대의 기존 role 유지.
- 390px/mobile/narrow pane: scope는 select 한 열, 세트 요약·계획 카드·가로 막대 유지, page-level overflow 0.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 프로젝트 workflow의 생산계획 단계 완료 판정만 aggregate 기준으로 확장. 신규 알림·내 업무 종류 없음.
- 권한/관리자: 변경 없음 (기존 정책 재사용).
- Excel/PDF/첨부: 기존 프로젝트 단위 생산계획 Excel 계약 보존. 세트형 LinkedV1 프로젝트를 대상으로 한 Excel 일괄 적용이 프로젝트 scope 일정 값을 변경하지 않도록 서버가 row 오류로 차단. 신규 세트별 export 없음.
- Teams/Mail: 영향 없음.
- 삭제·복구/감사: 세트 취소는 기존 UL891 취소 계약을 따르고 계획 이력은 보존. hard delete 없음.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A | `project_production_plans`에 세트 참조를 추가해 세트별 plan header row를 생성 (project unique를 partial unique로 교체) | scope마다 완전한 header·CAS | plan header를 단일 row로 가정한 모든 기존 경로(목록 상태, Excel, history, workflow sync)에 영향 — 회귀 반경이 가장 큼 |
| B (권장) | `project_production_plan_items`에 nullable 세트 인스턴스 참조 컬럼 추가. 프로젝트 scope row는 구조 snapshot 겸 clone source로 유지, 세트별 CAS는 소형 scope revision 테이블로 분리 | additive·최소 반경. 비-UL891 경로는 컬럼 null로 기존과 동일. 실적 연결 중복 없음(세트 항목이 snapshot 연결 재사용) | 기존 항목 unique 제약(순번·정의 key·활성 이름)을 scope 인지 index로 교체하는 forward-fix migration 필요 |
| C | 완전 분리된 세트 계획 신규 테이블 세트 | 기존 테이블 무변경 | 항목·연결·staffing·audit 구조 중복, 조회·수정 로직 이중화, 프로젝트/세트 응답 모델 분열 |

권장안은 B다. 근거: 기존 계약의 단일 plan header 불변을 유지해 Legacy·비-UL891 경로의 회귀 위험을 최소화하고, `0063`까지 누적된 항목 스키마(기간·담당자·필요 인원·코멘트)를 재사용하며, 세트별 CAS 요구(interview 4장)를 scope revision으로 정확히 충족한다. 8개 비차단 선택의 권장 확정은 16장에 기록한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated DB와 고정 experiment 검수 runtime만 사용.
- migration 필요 여부: 있음. additive 다음 번호(`0064` 예상) 1건 — 세트 참조 컬럼, scope 인지 unique index 교체(forward-fix), scope revision 테이블, 기존 Ul891Set LinkedV1 프로젝트 backfill. fresh DB와 기존 `0063` DB upgrade를 모두 검증.
- 외부 발송/실제 데이터 영향: 없음. 실제 provider·worker 비활성 유지.
- runtime 교체 여부: 없음. 고정 검수 runtime의 HMR/`dotnet watch` 갱신만.
- 추가 사용자 승인 필요 작업: local experiment commit 이외 없음. push·PR·merge·대표 repo·`main` 제외, `main` merge 승인 `0/3` 유지.
- worktree 주의: 현재 worktree에 생산계획·제조·양식 관련 미커밋 WIP가 존재한다. 구현 세션은 identity gate 기록대로 기존 WIP를 reset·정리하지 않고 이번 Task allowlist만 추가하며, 파일 overlap이 안전하지 않으면 commit을 보류하고 사유를 기록한다.

## 14. 검증 계획

- 최소 테스트: 세트 scope CRUD·CAS 독립성(세트 A 저장이 세트 B revision을 바꾸지 않음, 같은 세트 stale 409), scope 소속·취소 세트·날짜 역전·담당자·필요 인원 validation, 세트 필터된 자동 실적(한 세트 패널의 제조 완료가 그 세트 scope에만 집계), 프로젝트 공통 source 표시, 세트 추가·취소 lifecycle, aggregate 계획 상태·workflow 판정.
- 영향 영역 회귀: Backend 전체(`ProductionPlanningApiTests`, `ManufacturingAssemblyBatchApiTests` 포함), Frontend 전체(vitest), `PostgreSqlMigrationTests`의 fresh + 기존 `0063` upgrade, 비-UL891·평면 UL891·Legacy 프로젝트 비회귀.
- PR/CI: 해당 없음 (local experiment only). typecheck·lint·production build는 수행.
- 사용자 검수: isolated Full-Stack E2E 1본(UL891 세트 프로젝트 생성 → 세트 scope 계획 저장 → 한 세트 패널 실적 발생 → scope별 표시 확인) 후, 고정 검수 runtime에서 desktop·390px privacy-safe screenshot과 함께 `BATCHED_FINAL` 일괄 검수 checklist로 handoff.

## 15. 완료 기준

- 기능/권한/데이터: interview 8장 성공 기준 6항 전부 충족 — scope 동시 전환, 세트 간·master 불변, 세트 필터 실적과 공통 source 구분, 추가·취소·stale·권한·upgrade 무손실, 기존 프로젝트 비회귀.
- UX: desktop 8열 표·일정 날짜 축 유지, 390px 한 열 구성·page-level overflow 0, loading/empty/error/stale/취소 read-only 구분.
- 자동 테스트: Backend·Frontend 전체 회귀, migration fresh/upgrade, isolated Full-Stack 통과. Open P0/P1/P2 `0/0/0`.
- 5종 산출물: `docs/12-task-completion-policy.md`에 따라 추적 (interview·planning·review·2차 기획·implementation report·검수 checklist 위치 기록).
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`로 명시 (완료로 가장하지 않음).
- PR 상태: N/A — local experiment commit only.

중단 조건: 문서·구현의 의미 있는 충돌 발견(예: Excel 일괄 적용이 세트형 프로젝트에 Legacy 계획을 생성하는 등 기존 계약과의 비호환), migration이 기존 데이터에서 index 교체에 실패, fast-track 제외 경계(대표 repo·`main`·Persistent UAT·실제 provider·destructive 작업)를 넘어야 하는 경우 — 임의 우회 없이 blocking으로 보고하고 중단한다.

## 16. 미결정 사항

Standing instruction에 따라 아래 8개 비차단 선택은 이 문서의 권장안을 자동 채택하며, Codex review와 2차 기획에서 재검토된다. 최종 일괄 사용자 검수에서 번복될 수 있다.

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 세트 계획 원자 | 사양별 한 계획 vs **실물 세트 인스턴스별 독립 계획(권장)** — 같은 사양의 세트도 시기가 다르다는 것이 문제의 본질 | 자동 채택 (standing rule) |
| 2 | 전체 scope 의미 | 독립 프로젝트 계획 유지 vs **활성 세트 aggregate read-only(권장)** — 이중 관리 제거, 파생 가능 | 자동 채택 |
| 3 | 초기값·추가 세트 | master 재조회 vs **프로젝트 구조 snapshot 복제·일정 빈 값(권장)** — `0060` 이후 master는 제자리 수정되므로 snapshot만 불변 | 자동 채택 |
| 4 | 실적 source scope | **패널 귀속 source는 세트 필터, 구매·자재·IQC는 `프로젝트 공통` 배지(권장)** | 자동 채택 |
| 5 | 취소 세트 | 숨김 vs **선택기 별도 group의 read-only 이력·집계 제외(권장)** | 자동 채택 |
| 6 | 기존 세트형 프로젝트 | lazy 생성 vs 신규만 vs **migration backfill(권장)** — 기존 프로젝트 scope 값을 각 활성 세트에 복사해 보이는 데이터 무손실. Legacy 모델 Ul891Set 프로젝트는 프로젝트 단위 계획 유지 | 자동 채택 |
| 7 | 많은 세트 tab UX | **desktop: `전체`+사양 group chip tab(가로 스크롤), 임계 초과·390px: 검색형 select(권장)** | 자동 채택 |
| 8 | workflow 완료 판정 | **모든 활성 세트 계획 완료 = 프로젝트 계획 완료(권장)**, 취소 세트 제외, 기존 프로젝트 단위 sync 유지 | 자동 채택 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `backend/src/Emi.Qms.Api/ProductionPlanning/`(Store·Contracts·Endpoint의 scope 확장), `backend/src/Emi.Qms.Api/Projects/ProjectStore.cs`(생성 시 세트 scope snapshot), `backend/src/Emi.Qms.Api/Ul891Sets/Ul891SetStore.cs`(세트 추가·취소 hook)
- Frontend: `frontend/src/App.tsx` 생산관리 탭 영역(scope selector·표·Gantt·입력 흐름), `frontend/src/api.ts`, 관련 type 파일, `frontend/src/styles.css`
- DB/Migration: `database/migrations/0064_*.sql` (additive + backfill + index forward-fix)
- Tests/Scripts: `backend/tests/Emi.Qms.Api.Tests/ProductionPlanningApiTests.cs`·`PostgreSqlMigrationTests.cs`·신규 세트 계획 테스트, `frontend/tests/` 해당 spec, `frontend/e2e/full-stack/` 신규 1본
- Docs: Roadmap·실험 완료 원장 update, Task 산출물(`tasks/ul891-production-plan-001-*`)

## 18. Roadmap 연결

- 선행 Task: `TASK-PRODUCTION-CONTROL-001`(LinkedV1 모델·`0063`), `TASK-UL891-SET-001`(세트 계층) — 모두 `EXPERIMENT_COMPLETE`이며 재구현하지 않는다.
- 후속 Task: 세트별 Excel export 필요성 재평가(후속 판단), 대량 세트 성능 실측 최적화(P3 후보), canonical 승격·UAT는 별도 Task.
- 현재 Go/No-Go: Roadmap 기본 Next Gate(운영 전환)와 다르지만 identity gate에 `explicitRoadmapOverrideApproved: true`와 사용자 exact 요청이 기록되어 진행 가능.
- 별도 Task로 분리할 항목: 첨부 storage·운영 전환 등 기존 원장 항목 (이 Task에서 다루지 않음).

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-28 | UL891 세트별 생산계획 요청, fast-track standing rule 적용, 비차단 선택 Fable 권장안 자동 채택 | interview `COMPLETED_CONFIRMED` 기준으로 본 1차 기획 작성 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

### Codex 구현 지시문 초안 (2차 기획 확정 후 사용)

1. migration `0064`: 세트 참조 컬럼·scope 인지 unique index 교체·scope revision 테이블·기존 Ul891Set LinkedV1 backfill. fresh와 기존 `0063` DB에서 검증.
2. ProjectStore: Ul891Set LinkedV1 생성 transaction에서 활성 세트별 계획 scope 생성. Ul891SetStore: 세트 추가 시 snapshot 복제, 취소 시 계획 불변 확인.
3. ProductionPlanningStore/Contracts/Endpoints: scope query·scope 목록 응답·세트 scope PATCH·세트별 CAS·세트 필터 실적·`프로젝트 공통` flag·aggregate 계획 상태. 세트형 프로젝트의 프로젝트 scope 일정 값 편집(직접·Excel)을 field/row 오류로 차단.
4. Frontend: 공유 scope selector, scope별 표·Gantt·입력·state 재초기화, 취소 세트 read-only, 390px select, Action Feedback 재사용.
5. 검증: Backend·Frontend 전체 회귀, migration 테스트, isolated Full-Stack 1본, desktop·390px privacy-safe screenshot, Implementation report와 원장·Roadmap 동기화, local experiment commit (WIP overlap 안전 확인 후).

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 0
