All baseline evidence is gathered: the interview is confirmed complete, TASK-ADMIN-002 is the canonical deferred task in both the roadmap and the experiment ledger, the IQC (`0032`) and panel-quality (`0035`) template schemas already carry version/item structures with active-version partial unique indexes, the manufacturing 4 steps are a hard-coded C# array copied into per-execution snapshots (`0034` constrains sequence 1–4), users already have `department_id`, and admin master-data endpoints/policies provide reusable patterns. Below is the single first planning draft.

# TASK-ADMIN-002 — 검사·제조 양식 무코드 관리 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/admin-002-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- sourceTask: `TASK-ADMIN-002`
- authoringModel: `FABLE_5`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 검사(IQC/LQC/OQC/고객검수/FAT) 양식 item과 제조 시작 4단계가 migration seed 또는 C# 코드에 고정되어 있어, 문구·필수 여부·순서 변경에 코드 배포가 필요하다.
- 대상 사용자·역할: System Administrator(전 범위), 지정된 품질 부서장(품질 양식), 지정된 제조 부서장(제조 단계 양식). 일반 사용자는 관리 mutation 불가.
- 정상 흐름: 양식 관리 진입 → 종류/상태 선택 → active version 상세 → 새 draft 복제 → 항목 편집 → 서버 validation → 활성화 → 이후 새 report/execution에 적용.
- 예외·복구 흐름: 사용 중 version 직접 수정·삭제 금지, stale update/activate 409 처리, 활성화 실패 시 기존 active 유지, rollback은 이전 immutable version을 복제해 새 version으로 재활성화.
- 확정한 정책과 명시적 제외: 관리자 전 범위·부서장 자기 부서 범위·서버 authoritative·used version 불변·새 업무에만 activation 적용을 확정. 양식 import/form builder/조건식/전자서명, 실제 운영 양식 내용 확정·대량 입력, IAM 재설계, Persistent UAT·provider·대표 repo·`main` 게시는 제외.
- planning으로 넘긴 비차단 미결정 사항: 공통 catalog adapter 형태, 부서장 binding 세부 규칙, unused draft 보관 정책, item 최대 수, 선택 Excel export 상세. 실험 fast-track standing rule에 따라 비차단 항목은 본 문서의 권장안을 자동 채택한다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

System Administrator와 지정된 부서 양식 관리자(부서장)가 코드 배포 없이 관리 화면에서 검사·제조 양식을 새 version으로 편집·활성화하고, 기존 보고서·제조실행은 원래 양식 그대로 재현된다.

## 2. 배경과 해결할 업무 문제

- 현재 IQC 양식 item은 migration `0032` seed, 패널 품질(LQC/OQC/고객검수/FAT) 양식은 migration `0035` seed로만 존재하고, 전용 관리 UI와 draft/publish lifecycle이 없다.
- 제조 시작 4단계 이름은 `ManufacturingStore`의 static 배열에 hard-code되어 실행 시작 시 `panel_manufacturing_execution_steps`로 복제된다. 단계 문구를 바꾸려면 코드 수정·배포가 필요하다.
- 현재 우회 방식은 개발자가 migration 또는 코드를 수정하는 것뿐이며, 직접 DB 수정은 감사·재현성·불변조건을 깨뜨린다.
- 이 기능이 없으면 현장 양식 개선이 배포 주기에 묶이고, 실제 운영 양식 회신(추적 항목 4·11·13·14)이 도착해도 반영 경로가 코드 변경뿐이다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| System Administrator | 전체 양식 조회, draft 편집·활성화, 부서 양식 관리자 지정·해제 | 전 부서 전 양식 | 전 범위 |
| 품질 양식 관리자(품질 부서장) | 품질 양식 조회, draft 편집·활성화 | 품질 소유 양식 | IQC, LQC, OQC, CustomerInspection, FAT |
| 제조 양식 관리자(제조 부서장) | 제조 단계 양식 조회, draft 편집·활성화 | 제조 소유 양식 | 제조 작업 단계 template |
| 일반 사용자 | 운영 화면에서 active version 기반 업무 수행(기존과 동일) | 기존 업무 scope | 관리 API 전체 403 |

권한 판정은 서버가 authoritative다. UI 노출 여부와 무관하게 관리 API는 System Administrator 또는 해당 부서의 활성 양식 관리자 binding이 있는 사용자만 허용한다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 품질 부서장이 OQC 양식 문구를 수정

1. 품질 양식 관리자로 지정된 사용자가 `양식 관리` 메뉴에 진입해 OQC template의 active version을 연다.
2. `새 draft 만들기`로 active version 항목이 복제된 draft version이 생성된다.
3. 항목 문구·필수 여부·순서를 수정하고 저장하면 서버가 항목 규칙을 검증한다.
4. `활성화`를 확인하면 서버가 transaction 안에서 기존 active를 보관 처리하고 draft를 active로 전환한다.
5. 이후 새로 요청되는 OQC 검사부터 새 version이 적용되고, 기존 성적서는 원래 version으로 계속 표시된다.

### 시나리오 B — System Administrator가 제조 부서장을 지정하고 제조 단계를 변경

1. System Administrator가 양식 관리 화면의 관리자 지정 영역에서 제조 부서 사용자 한 명을 제조 양식 관리자로 지정한다.
2. 지정된 사용자가 `양식 관리`에서 제조 작업 단계 template의 draft를 만들어 단계 이름을 수정하고 활성화한다.
3. 이후 새로 시작하는 제조 작업은 hard-code 배열이 아니라 활성 template version의 단계를 snapshot으로 복제해 사용한다. 진행 중·완료된 제조실행의 단계 이름은 바뀌지 않는다.

### 시나리오 C — 동시 활성화 충돌 복구

1. 두 관리자가 같은 template의 draft를 각각 활성화하려 한다.
2. 먼저 도착한 요청이 성공하고, 나중 요청은 expected version 불일치로 409와 함께 최신 상태 재조회 안내를 받는다.
3. 사용자는 최신 active를 확인한 뒤 필요하면 새 draft로 다시 작업한다.

## 5. 기능 요구사항

### 필수

- [ ] 품질 5종(IQC/LQC/OQC/CustomerInspection/FAT)과 제조 작업 단계를 하나의 관리 화면에서 조회하는 통합 template 목록
- [ ] 제조 작업 단계의 template/version/item DB model 신설과 현행 4단계의 v1 seed
- [ ] `ManufacturingStore`의 static 단계 배열 제거, 실행 시작 시 활성 제조 template version snapshot 사용
- [ ] draft 생성(active 복제) → 항목 편집 → 서버 validation → 원자적 활성화 lifecycle
- [ ] 한 번이라도 활성화된 version과 그 item의 서버 측 불변 보장, used/active version hard delete 금지
- [ ] System Administrator의 부서 양식 관리자 지정·해제 flow와 서버 측 department scope 강제
- [ ] 일반 사용자·타 부서 관리자의 관리 API 403과 부서 밖 template 접근 차단
- [ ] expected version 기반 동시성 제어(409)와 template당 active version 최대 1개 불변 유지
- [ ] 양식 관리 mutation의 audit 기록(행위자, 행위, 대상 template/version, 시각)
- [ ] adaptive desktop/mobile 관리 UI와 loading/empty/error/success·field error focus·활성화 확인 feedback

### 선택

- [ ] template version 목록의 checkbox 선택 Excel 내보내기(기존 전역 선택 export 계약 재사용)
- [ ] unused draft의 취소(보관) 처리

### 명시적 제외

- [ ] Word/PDF/Excel 양식 import, drag-and-drop form builder, 조건식·계산식·전자서명
- [ ] 실제 운영 양식 내용의 확정·대량 입력, 기존 완료 report/execution의 소급 변경
- [ ] 사용자 계정·조직 전체 IAM 재설계와 generic 부서장 role 신설
- [ ] 외부 저장소/provider 발송, Persistent UAT migration·runtime handover, 대표 repo·`main`·push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 양식 관리 목록 | 운영 navigation의 `양식 관리`(권한 기반 노출) | template 종류(품질 5종·제조), 소유 부서, active version 번호·활성일, draft 존재 여부 | template 선택, (관리자) 관리자 지정 영역 이동 | 권한 없음 시 메뉴 미노출 + API 403 안내 |
| version 목록·상세 | 목록에서 template 선택 | version 번호, 상태(Draft/Active/Archived), 활성일, 항목 수 | active 상세 조회, 새 draft 생성, draft 선택 | draft 생성 성공/실패를 action 근처에 표시 |
| draft 항목 편집 | version 목록에서 draft 선택 | 항목명, 안내문, 응답형식(Check/Text), 필수, 사진 필요, 텍스트 길이, 순서 | 항목 추가·수정·삭제·순서 변경, 저장, 활성화 | 저장 중·성공·실패, field 단위 오류와 첫 오류 focus, 활성화 확인 dialog, 409 시 최신 재조회 안내 |
| 관리자 지정(관리자 전용) | 양식 관리 화면 내 System Administrator 전용 영역 | 부서별 현재 양식 관리자, 지정 이력 | 사용자 검색·지정·해제 | 지정/해제 성공·실패 feedback과 audit 반영 |

확인할 UX 항목:

- desktop은 좌측 template 종류, 가운데 version/항목, 우측 편집 panel의 3열 구성을 기본으로 하되, 기존 화면들과 같은 흰 바탕·얇은 divider·compact 필터·파란 active state를 따른다.
- mobile(390px)은 template 선택 → version 선택 → 항목 편집의 순차 drill-in으로 전환하고 PC 3열 축소·page-level horizontal overflow를 금지하며 핵심 touch target 44px을 지킨다.
- Draft/Active/Archived 상태와 "이 version은 사용 중이라 수정할 수 없습니다" 안내를 항목 편집 진입 시점에 명확히 보여준다.
- 활성화는 파괴적으로 보이는 행동과 구분된 확인 절차를 가진다(기존 active는 삭제가 아니라 보관됨을 문구로 안내).
- keyboard 접근, label/`aria-live` 상태 안내, 순서 변경의 비-드래그 대체 수단(위/아래 이동 버튼)을 제공한다.

## 7. 업무 규칙과 불변조건

- 한 번이라도 활성화된 template version과 그 item은 수정·삭제할 수 없다. 변경은 항상 새 draft version으로만 한다.
- template당 active version은 항상 최대 1개다(기존 partial unique index 계약 유지).
- 활성화는 이후 새로 생성되는 report/attempt/execution에만 적용된다. 기존 데이터의 version 참조와 snapshot은 절대 바뀌지 않는다.
- 부서 양식 관리자는 자기 부서 소유 template만 조회 범위 밖 mutation이 불가하며, 소유 판정은 서버가 department binding으로 강제한다.
- System Administrator만 양식 관리자를 지정·해제할 수 있다.
- 모든 관리 mutation은 행위자·시각·대상과 함께 감사 가능해야 한다.
- 제조실행의 단계 snapshot 복제 방식(실행 시점 고정)은 유지한다. 활성 제조 template이 없으면 실행 시작을 안전하게 차단하는 대신, seed로 활성 v1을 항상 보장한다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| IQC template/version/item | `iqc_report_templates`·versions·items, 성적서가 version을 참조 | 기존 | 활성화 이력·기존 참조 불변 |
| 패널 품질 template version/item | stage_code(LQC/OQC/CustomerInspection/FAT)별 version·item | 기존 | 동일 |
| 제조 단계 template version/item | 제조 시작 단계의 version·단계 item(이름·순서) | 신규 | v1 seed는 현행 4단계와 동일 문구 |
| version lifecycle 상태 | Draft/Active/Archived 구분 | 신규(기존 table 최소 additive 확장) | 기존 row는 활성 여부에 따라 backfill |
| 부서 양식 관리자 binding | 사용자–부서(양식 domain) 지정, 지정자·시각·해제 이력 | 신규 | 지정·해제 모두 이력 보존 |
| 양식 관리 감사 기록 | 관리 mutation의 actor/행위/대상 기록 | 신규(기존 admin change log 패턴 준용) | append-only |

```text
Draft(생성·편집 가능) → Active(불변, 운영 적용) → Archived(불변, 이력 조회)
Draft → (취소/보관 시) Archived
```

기존 version row 중 `is_active=true`는 Active, 활성화 이력이 있는 비활성 row는 Archived로 backfill한다. 활성화 constraint(`is_active`와 `activated_at_utc` 동시 충족)는 유지한다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 관리자/부서장 scope 판정, version 불변, 활성화 원자성, 항목 validation, active 최대 1개.
- 필요한 조회와 mutation: 통합 template 목록 조회, template별 version 목록·항목 조회, draft 생성(active 복제), draft 항목 일괄 저장, draft 활성화, draft 취소(보관), 양식 관리자 목록·지정·해제. 품질 2계보(IQC·panel quality)와 신규 제조 계보는 table을 통합하지 않고 관리 API 계층의 family discriminator(예: IQC / 품질 stage / MANUFACTURING)로 통합 노출한다(미결정 1의 권장 형태).
- 권한·validation: 신규 관리 권한 경계(System Administrator 전 범위 + 활성 binding 사용자)를 endpoint에서 강제하고, 빈 항목명·duplicate item code/order·미지원 response type·길이 초과·단계 수 초과·부서 범위 밖 접근을 안정적 status와 한글 메시지로 차단한다. 항목 규칙은 기존 DB check 계약(Check/Text, requires_photo는 Check만, Text 길이 1–2000 등)과 일치시킨다.
- transaction·동시성·idempotency: draft 저장·활성화는 단일 transaction, expected version 불일치와 활성화 경쟁은 409, active 유일성은 기존 partial unique index가 최종 방어선. check-then-write 경쟁 구간은 row lock 또는 atomic update를 사용한다.
- audit trail: 관리 mutation마다 감사 row를 같은 transaction에서 기록한다(기존 admin master-data change log 패턴 준용).
- 외부 provider 영향: 없음. 알림·Teams·메일 발송을 추가하지 않는다.
- 제조 특이사항: `panel_manufacturing_execution_steps`의 sequence 1–4 check constraint와 "네 가지 단계" 고정 문구·완료 판정이 존재하므로, 단계 수 가변화 시 constraint 완화(additive)와 count 기반 문구·판정 일반화가 함께 필요하다(미결정 4 참조).

Repository 조사 전 내부 클래스명, 컬럼명과 SQL 형태를 확정하지 않는다. 위 명칭은 제안이며 구현 시 기존 convention에 맞춘다.

## 10. Frontend 고려사항

- route/component: `App.tsx`의 기존 route kind 패턴에 양식 관리 화면을 추가하고, 전용 API module과 page component를 신설한다. 관리자 지정 영역은 같은 화면 내 권한 조건부 영역으로 둔다.
- loading/empty/error/success: 목록·version·편집·활성화 각각에서 4상태를 구분하고 409는 최신 재조회 유도 문구로 처리한다.
- 공통 Action Feedback: 기존 `useActionFeedback` 계약을 재사용해 저장·활성화·지정/해제의 처리 중·성공·실패를 action 근처에 표시한다.
- 접근성: label 연결, 첫 오류 focus, `aria-live` 상태 안내, keyboard 순서 변경 수단.
- 390px/mobile/narrow pane: drill-in 화면 전환, page-level horizontal overflow 0, 핵심 touch target 44px, 좌상단 숨김 메뉴 유지.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 운영 화면(IQC 성적서, 품질 검사, 제조 작업)은 계속 활성 version만 소비한다. 새 알림 event는 만들지 않는다.
- 권한/관리자: 사용자·부서 master는 기존 admin 기능(TASK-ADMIN-001 계보)을 재사용하고 재구현하지 않는다. 양식 관리자 binding만 추가한다.
- Excel/PDF/첨부: 기존 IQC/품질 PDF snapshot·사진 계약은 변경하지 않는다. 선택 export는 기존 전역 선택 내보내기 계약을 재사용한다(미결정 5).
- Teams/Mail: 영향 없음.
- 삭제·복구/감사: hard delete를 도입하지 않고 보관(Archived)과 감사 기록으로 처리한다.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A(권장) | 기존 품질 table 보존 + 제조 template table 신설 + 관리 API 계층의 통합 family adapter + 최소 additive 상태·binding 확장 | 기존 참조·snapshot 불변, migration 위험 최소, 기존 계약 재사용 | 관리 API가 3계보를 adapter로 다뤄야 해 서버 코드가 다소 늘어남 |
| B | 3계보를 하나의 generic template catalog table로 통합 migration | 장기적으로 단일 model | 기존 report/attempt 참조 재작성이 필요해 역사 훼손·회귀 위험이 크고 experiment 범위를 초과 |
| C | 제조 단계만 우선 template화하고 품질 관리 UI는 후속 | 범위 최소 | 인터뷰가 확정한 "품질+제조" 범위 미충족, 부서장 권한 모델을 다시 만들게 됨 |

권장안은 A다. 인터뷰 확정 정책(기존 역사 참조 보존, 최소 catalog/adapter)과 일치하며, standing rule에 따라 자동 채택한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated PostgreSQL과 disposable E2E DB에서만 검증한다.
- migration 필요 여부: 있음. 다음 번호(현재 최신 `0042` 이후)의 additive migration 1건 — 제조 template table 신설·seed, version 상태 컬럼 backfill, 관리자 binding·감사 table, 제조 단계 sequence constraint 완화. fresh/existing 모두 검증하고 rollback은 forward-fix 원칙을 따른다.
- 외부 발송/실제 데이터 영향: 없음. 실제 운영 양식 내용은 입력하지 않고 seed는 현행 문구 복제만 한다.
- runtime 교체 여부: 없음. Development runtime 검증만 수행한다.
- 추가 사용자 승인 필요 작업: push·PR·merge, 대표 repo·`main` 반영(`0/3`), Persistent UAT 적용, 실제 운영 양식 내용 입력. 모두 이번 범위 밖이다.

## 14. 검증 계획

- 최소 테스트: Backend build와 신규 store/endpoint 단위 — 권한(관리자/부서장/타부서/일반 403), lifecycle(draft 생성·편집·활성화·불변), 동시성(중복 활성화 409), migration(fresh/existing, backfill, seed) 테스트.
- 영향 영역 회귀: IQC 성적서 생성, 품질 검사 attempt 생성, 제조 시작 snapshot(신규 template 경로) 회귀와 기존 Backend/Frontend 전체 suite. Frontend lint·typecheck·unit·build.
- PR/CI: 이번 experiment 범위는 local commit까지이며 PR/CI는 수행하지 않는다(승인 경계 기록).
- 사용자 검수: 마지막 일괄 검수(`BATCHED_FINAL`) 계약에 따라 desktop·390px synthetic screenshot과 user validation checklist를 남기고 `사용자 검수 대기`로 추적한다.

## 15. 완료 기준

- 기능/권한/데이터: 관리자·지정 부서장이 코드 변경 없이 자기 범위 양식을 새 version으로 편집·활성화하고, 일반 사용자·타부서 관리자는 403이며, 기존 report/execution snapshot이 불변임을 테스트로 증명.
- UX: desktop 3열과 mobile drill-in에서 4상태·오류 focus·활성화 확인·44px target·overflow 0 충족.
- 자동 테스트: Backend 전체 회귀, Frontend lint·typecheck·unit·build, isolated Full-Stack E2E, migration fresh/existing 통과.
- 5종 산출물: implementation report·SOP·user manual·Roadmap/원장 update·user validation checklist의 상태와 위치 추적.
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`.
- PR 상태: 없음(local experiment commit만, push·PR·merge 미승인).

## 16. 미결정 사항

standing instruction에 따라 아래 비차단 항목은 아래 권장안을 자동 채택하며, 사용자가 달리 결정하면 change로 반영한다.

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 통합 catalog 형태 | (a) API 계층 family adapter로 3계보 통합 노출(권장) / (b) 단일 generic table 통합 migration | 권장안 자동 채택 |
| 2 | 양식 관리자 binding 규칙 | (a) 부서당 복수 지정 허용, System Administrator만 지정·해제, 해제는 이력 보존 soft revoke, 자기 자신 지정 허용하되 감사 기록(권장) / (b) 부서당 1인 제한 | 권장안 자동 채택 |
| 3 | unused draft 처리 | (a) 취소 시 Archived로 보관, hard delete 없음(권장) / (b) draft hard delete 허용 | 권장안 자동 채택 |
| 4 | 항목·단계 수 상한 | (a) 검사 항목 template당 최대 50개, 제조 단계 1–10개로 constraint 완화(권장) / (b) 제조 단계 4개 고정 유지 | 권장안 자동 채택 |
| 5 | 선택 Excel export | (a) template version 목록에 기존 선택 export 계약 적용(권장) / (b) 이번 범위 제외 | 권장안 자동 채택 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: 제조 store의 static 단계 배열 제거와 template 조회 전환, 양식 관리 feature(endpoints/store/contracts) 신설, 신규 policy/권한 등록, composition root 등록.
- Frontend: 양식 관리 route·page·API module 신설, navigation 권한 노출, 선택 export registry 등록, 공통 feedback·adaptive layout 재사용.
- DB/Migration: 다음 번호 additive migration 1건(제조 template/version/item·seed, version 상태 backfill, 관리자 binding·감사, 제조 단계 constraint 완화).
- Tests/Scripts: Backend 권한·lifecycle·동시성·migration 테스트, Frontend unit, isolated Full-Stack E2E, screenshot 스크립트 범위.
- Docs: Roadmap `TASK-ADMIN-002` 상태, 실험 완료 원장, 종료 5종 산출물.

## 18. Roadmap 연결

- 선행 Task: `TASK-009A`·`TASK-011A`·`TASK-012A`(실제 template model과 snapshot 계약) — 현재 실험 계보에서 완료.
- 후속 Task: 실제 운영 양식 내용 입력(외부 회신 도착 시 해당 Task change), 관리자 기준정보 후속(ADMIN 계보), 승격·UAT 통합 Task.
- 현재 Go/No-Go: Roadmap상 `DEFERRED / EXTERNAL_INPUT`이나, 사용자의 2026-07-19 명시 요청과 experiment fast-track standing rule로 순서 override가 interview에 기록됨. 실제 운영 양식 내용은 계속 외부 입력 대기로 두고 관리 shell과 안전한 lifecycle까지만 구현한다.
- 별도 Task로 분리할 항목: 양식 import·form builder, Pending 유형 관리자 화면, 승격·Persistent UAT 적용.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-19 | 관리자와 부서 부서장이 코드 수정 없이 양식 관리, fast-track으로 결과물까지 진행 | 본 기획안 작성, 비차단 선택은 권장안 자동 채택 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 5
