# TASK-ADMIN-002 — 검사·제조 양식 무코드 관리 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5_EXPERIMENT_FAST_TRACK`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 Roadmap `TASK-ADMIN-002`를 현재 `experiment/*` branch에서 진행하기 위한 interview source of truth다. 사용자는 관리자가 코드 수정 없이 양식을 관리하고, System Administrator뿐 아니라 각 부서 부서장도 자기 부서 양식을 관리하도록 요청했다. 사용자-facing interview·중간 승인은 생략하고 비차단 제품 선택은 Fable 권장안을 자동 채택한다. 대표 repo, GitHub `main`, Persistent UAT, 실제 provider와 push·PR·merge는 제외한다.

## Task Identity Gate

- proposedTaskId: `TASK-ADMIN-002`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `OPTIONAL_EXPERIMENT_FOLLOW_UP`
- roadmapNextGate: `OPTIONAL_EXPERIMENT_FOLLOW_UP`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-ADMIN-002`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: System Administrator와 지정된 부서장이 코드 배포 없이 자기 관리 범위의 검사·제조 양식을 조회하고 새 버전으로 편집·활성화한다.
- Root Finding 또는 정책 결정: IQC와 panel quality는 version/item table이 있지만 전용 관리 UI·draft/publish lifecycle이 없고, 제조 시작 단계 이름 4개는 `ManufacturingStore`에 hard-code되어 있다. 현재 사용자/role에는 generic department-head 개념이 없다.
- 변경·검증 경계: 기존 IQC/panel-quality 구조와 snapshot 불변을 보존하면서 additive template/version/manager model, Backend authoritative permission·API, adaptive manager UI, existing/fresh migration, isolated synthetic tests만 포함한다.
- 보존할 불변조건: 이미 사용 중인 template version·report·execution snapshot 불변, 새 작업에만 active version 적용, 부서장은 자기 부서 범위만 mutation, System Administrator 전 범위, 다른 사용자는 mutation 불가, audit·concurrency·server validation, main·Persistent UAT 불변.
- 예상 산출물: Fable 1차 planning, Codex review, Fable 2차 planning, migration·API·UI·tests, desktop/mobile screenshots, 종료 문서와 local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

같은 목적은 Product Roadmap의 canonical `TASK-ADMIN-002` 한 건이다. 별도 Task artifact·branch·worktree·open/merged PR은 확인되지 않았다. 기존 `TASK-ADMIN-001`은 프로젝트 Master Data 관리로 목적이 다르다. Roadmap의 선행조건이던 IQC, panel quality, manufacturing 실제 model은 현재 experiment 계보에서 구현됐고, 실제 운영 양식 내용은 아직 외부 입력이므로 manager shell과 안전한 template lifecycle까지만 구현한다. 사용자의 이번 명시 요청을 순서 override로 기록한다.

## 사용자 실행 지시

- 사용자 요청일: 2026-07-19
- 요청: 관리자와 각 부서 부서장이 코드 수정 없이 양식을 관리한다.
- 승인 대체: 비차단 선택은 Fable 권장안을 자동 채택한다.
- 게시 경계: local experiment commit만 승인. `main` merge 승인 `0/3`.

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: IQC/LQC/OQC/고객검수/FAT 양식 item과 제조 4단계가 migration 또는 C# code에 들어 있어 문구·필수 여부·순서를 바꾸려면 개발이 필요하다.
- 해결할 문제: 운영자가 draft를 만들고 항목을 편집·검증한 뒤 활성화하며, 이미 사용 중인 보고서·제조실행은 원래 양식으로 재현되어야 한다.
- 성공 결과: 관리자 또는 지정 부서장이 관리 화면에서 자기 범위 template/version을 관리하고 새 업무는 활성 버전을 snapshot으로 사용한다.
- 하지 않을 경우 영향: 현장 양식 변경이 코드 배포와 migration에 묶이고, 직접 DB 수정 시 감사·재현성이 깨진다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| System Administrator | 모든 template/version 조회·부서장 지정·draft 편집·활성화 | 전 부서 | 전 범위 | actor/time/version/audit |
| 품질 부서장 | 품질 양식 조회·draft·활성화 | 자기 부서 소유 양식 | IQC/LQC/OQC/CustomerInspection/FAT | 서버 department binding |
| 제조 부서장 | 제조 양식 조회·draft·활성화 | 자기 부서 소유 양식 | 제조 작업 단계 | 서버 department binding |
| 일반 사용자 | 운영 화면에서 active version 사용 | 기존 업무 scope | 관리 mutation 없음 | manager API 403 |

현재 generic 부서장 role이 없으므로 System Administrator가 사용자를 부서별 양식 관리자/부서장으로 지정하는 최소 additive binding이 필요하다. 명칭·중복 지정·해제·자기지정 제한은 Fable 권장안으로 정한다.

## 3. 정상·예외·복구 흐름

- 정상 흐름: 양식 관리 진입 → 종류/상태 선택 → active version 상세 → 새 draft 복제 → 항목명·안내·응답형식·필수·사진·길이·순서 편집 → validation → 활성화 → 이후 새 report/execution에서 사용.
- 이미 사용 중인 version: 직접 수정·삭제하지 않는다. 새 draft/version으로만 변경한다.
- 동시 처리: expected version 또는 row version으로 stale update/activate를 409 처리한다. template별 active version은 최대 한 개다.
- validation: 빈 이름, duplicate item code/order, unsupported response type, 단계 수/길이 초과, 잘못된 department ownership을 서버가 차단한다.
- 복구: draft는 저장·재시도 가능하고, 활성화 실패는 기존 active version을 유지한다. 활성화된 version의 rollback은 이전 immutable version을 복제해 새 version으로 다시 활성화한다.

## 4. Data·integration·lifecycle

- 기존 quality data: `iqc_report_templates`, `iqc_report_template_versions/items`, `panel_quality_template_versions/items`; report가 version을 참조하므로 historical reconstruction 가능.
- 기존 manufacturing data: execution row와 step snapshot은 있으나 template/version source가 없고 시작 시 C# static 4단계 배열을 복제한다.
- 신규 data 후보: generic template catalog/ownership, manufacturing template/version/items, draft/active/archived lifecycle, department manager binding, audit/version. 기존 quality table의 최소 additive 확장과 통합 API를 우선 검토한다.
- activation: 새 업무에만 적용. 이미 생성된 report/execution에는 영향 없음.
- 삭제: used/active version hard delete 금지. unused draft 취소/보관 정책은 Fable 권장안.
- Excel: 사용자의 전역 선택 내보내기 계약에 따라 template 목록·version 목록의 선택 내보내기 필요성을 Fable이 검토한다. 가져오기/import와 외부 양식 파일 parsing은 이번 범위에서 제외한다.

## 5. UX와 운영 적용

- 진입: 일반 운영 navigation에 `양식 관리`를 권한 기반 노출해 부서장도 접근 가능하게 한다. System Administrator는 전체 범위와 부서장 지정 control을 본다.
- desktop: 왼쪽 template 종류 목록, 가운데 version/항목 목록, 오른쪽 detail/edit panel 또는 명확한 단계형 flow. 사용자 제공 WITHUS 이미지처럼 흰 바탕·얇은 divider·compact tab/search/filter·낮은 그림자·파란 active state를 따른다.
- mobile: template 선택 → version → item edit의 순차 drill-in 화면, PC 3-column 축소 금지, sticky가 아닌 좌상단 숨김 메뉴와 390px overflow 0.
- feedback: loading/empty/error, draft 저장 중·성공·실패, field error focus, 활성화 확인과 stale recovery를 action 가까이에 표시한다.
- 접근성: keyboard/focus, label, status text, destructive-looking action 구분, 44px 핵심 touch target.

## 6. 포함·제외 범위

### 포함

- 품질(IQC/LQC/OQC/고객검수/FAT)·제조 양식 catalog/version/item 관리
- System Administrator 전 범위와 부서장 자기 부서 범위의 서버 권한
- 부서장/양식관리자 지정·해제의 최소 admin flow
- immutable used version, draft copy/edit, atomic activation, concurrency/audit
- 제조 hard-coded 단계 제거와 active template snapshot 사용
- 기존 quality report·manufacturing execution historical snapshot 보호
- adaptive desktop/mobile manager UI와 권한/수명주기/마이그레이션 tests
- 필요 시 선택 내보내기와 전체선택의 기존 UX 계약 유지

### 제외

- Word/PDF/Excel 양식 import, drag-and-drop form builder, 조건식/계산식/전자서명
- 실제 운영 양식 내용의 확정·대량 입력, 기존 완료 report/execution rewrite
- 사용자 계정·조직 전체 IAM 재설계
- 외부 저장소/provider, Persistent UAT migration·runtime handover
- 대표 repo·main·push·PR·merge

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 요청 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 공통 data model | 기존 table 개별 확장은 중복, 전면 통합 migration은 위험 | 기존 역사 참조를 보존하는 최소 catalog/adapter 권장 | Fable 권장안 자동 채택 | No |
| 2 | 부서장 표현 | 새 role은 고정적, user-department binding은 유연 | admin이 관리하는 explicit binding 권장 | Fable 권장안 자동 채택 | No |
| 3 | 편집 lifecycle | active 직접 수정은 역사 훼손, 매번 신규 version은 안전 | draft copy → validate → activate 권장 | Fable 권장안 자동 채택 | No |
| 4 | activation 범위 | 기존 실행 갱신은 위험 | 이후 새 report/execution에만 적용 권장 | Fable 권장안 자동 채택 | No |
| 5 | item 종류 | 무제한 builder는 과도 | 현재 Check/Text/photo/required/length 계약 우선 권장 | Fable 권장안 자동 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: 코드/migration에 고정된 양식을 안전한 version 관리 UI로 전환한다.
- 권장 범위: 품질+제조 template catalog, immutable version, draft/activation, department manager binding, manufacturing snapshot source 전환.
- 확정 정책: 관리자 전 범위, 부서장 자기 부서, 서버 authoritative, used version 불변, 새 업무에만 activation 적용.
- Deferred 비차단 결정: catalog adapter shape, unused draft archive, item 최대 수, 선택 export 상세.
- Fable 판정: `COMPLETED_CONFIRMED`.

## 9. 성공 기준

- 관리자와 지정 부서장이 허용 범위의 양식을 code change 없이 새 version으로 편집·활성화한다.
- 일반 사용자와 다른 부서장은 mutation 403이며, 부서장은 자기 부서 밖 template을 얻거나 바꾸지 못한다.
- 기존 report/execution은 원 version/snapshot을 유지하고 새 실행만 새 active version을 사용한다.
- fresh/existing migration, Backend build·권한/lifecycle/concurrency tests, Frontend lint·typecheck·unit·build, isolated E2E, desktop/390px screenshot이 통과한다.

## 10. 사용자 확인

- [x] experiment 사용자-facing interview를 생략한다.
- [x] 비차단 선택은 Fable 권장안을 자동 채택한다.
- [x] open blocking decision 0이다.
- [x] 2차 기획 뒤 구현·검증·screenshot·local commit까지 승인한다.
- [x] main·대표 repo·Persistent UAT·provider·게시를 제외한다.
