# TASK-ADMIN-002 Implementation report — 무코드 양식 관리

## 1. 요약과 상태

- 목적: 시스템 관리자와 지정된 부서장이 code 수정 없이 자기 부서의 검사·제조 양식 항목과 순서를 version으로 관리하게 한다.
- 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL` — 구현·필수 자동 검증·격리 브라우저 검증 완료, 사용자 검수는 마지막 일괄 대기
- 최종 계약: [Fable 2차 기획](../docs/33-form-template-management-plan.md)
- Branch/base: `experiment/task-home-002-personalized-shell` / `c4b999f`
- 대표 repo·`main`·Persistent UAT·actual provider: 미변경
- Merge 승인: `0/3`

## 2. 해결한 업무 문제

IQC·LQC·OQC·전진검수·FAT·제조 단계의 항목을 바꿀 때 code와 migration을 직접 수정해야 했고, 누가 어느 부서 양식을 관리할지 명확한 위임 수단이 없었다. 이번 구현은 고정된 6개 양식 catalog, Draft→Active→Archived lifecycle, 부서장 지정과 부서 경계를 서버에서 강제해 운영 양식 변경을 code 배포와 분리한다.

## 3. 구현 범위와 아키텍처

### DB·Migration

- `0044_form_template_management.sql`은 additive migration이다.
- 기존 IQC·후속 품질 version에 lifecycle·row version·audit actor를 추가하고 제조 단계 template/version/item을 신설했다.
- 제조 실행은 시작 시점의 template version을 FK로 기록하고 항목 snapshot을 유지한다.
- 부서장 binding과 append-only form audit를 추가했다.
- DB trigger가 Draft에서만 항목 편집, Active 불변, Archived terminal lifecycle을 강제한다.

### Backend·API·권한

- 현재 사용자의 관리 scope, 6종 catalog, version 목록, Draft 생성·저장·활성화·보관을 제공한다.
- 시스템 관리자는 전체 양식과 부서장 지정을 관리할 수 있다.
- 지정된 부서장은 현재 사용자 부서와 binding 부서가 일치할 때 자기 domain 양식만 관리한다. 부서 이동·binding 해제는 다음 요청부터 즉시 차단한다.
- 이미 Active인 version은 수정하지 않으며 새 작업만 새 Active를 사용한다. 기존 검사·제조 snapshot은 바뀌지 않는다.
- 선택 checkbox와 전체선택 checkbox, 단일 `선택 Excel 내보내기`를 적용해 기존 export UX와 일치시켰다.

### Frontend·UI/UX

- Desktop은 좌측 catalog, 중앙 version 목록, 우측 Draft editor의 3열 작업면으로 구성했다.
- 항목 추가·삭제·순서 이동·필수 여부·입력 형식을 code 없이 편집하고 저장·활성화·보관할 수 있다.
- 시스템 관리자만 부서장 지정 panel을 본다.
- Mobile은 3열을 축소하지 않고 catalog→version→editor 순서의 단일 열로 재배치한다.

### Excel/PDF/첨부·Workflow 영향

- Excel: 선택한 version만 workbook으로 내보내고 서버가 관리 scope를 다시 검증하며 audit를 남긴다.
- PDF·첨부: 기존 성적서 PDF와 사진 계약을 변경하지 않는다. PDF 양식 자체 편집은 제외 범위다.
- Workflow: 기존 실행 snapshot을 보존하고 새 검사·제조 시작에만 활성 version이 적용된다.

## 4. 기술적 결정과 검토한 대안

- 자유로운 새 양식 종류 생성 대신 기존 workflow와 연결된 6개 catalog를 고정해 임의 domain 확장을 차단했다.
- Active 직접 편집 대신 새 Draft 복제·활성화 방식을 사용해 진행 중 기록을 보호했다.
- 일반 role 이름을 부서장으로 간주하지 않고 관리자가 명시적으로 사용자 binding을 부여하게 했다.
- UI 숨김에 의존하지 않고 endpoint와 DB trigger가 scope·불변성을 각각 강제한다.

## 5. 시행착오 및 폐기한 접근

- 제조 단계가 code 상수에 고정되어 있어 양식만 만들면 실제 실행과 분리되는 문제가 있었다. 시작 transaction에서 활성 version을 lock하고 실행 snapshot을 생성하도록 store를 교체했다.
- 부서장 권한을 영구 role로 부여하는 접근은 부서 이동 뒤 과권한 위험이 있어 폐기했다. 매 요청마다 현재 부서와 active binding을 함께 확인한다.
- Desktop 3열을 그대로 좁히는 방식은 Mobile 편집이 어려워 catalog→version→editor 적응형 순서로 분리했다.

## 6. 변경 파일과 역할

- `database/migrations/0044_form_template_management.sql`: lifecycle·제조 template·binding·audit·trigger
- `backend/.../Admin/FormTemplate*`: scope·catalog·version lifecycle·부서장 관리·선택 Excel
- `backend/.../Manufacturing/ManufacturingStore.cs`: 활성 제조 양식 lock·snapshot
- `frontend/src/FormTemplateManagementPage.tsx`, `formTemplates.ts`: 관리 화면·type·editor
- `frontend/src/App.tsx`, `api.ts`, `styles.css`: route·API·desktop/mobile layout
- `PostgreSqlMigrationTests.cs`, Full-Stack spec: lifecycle·부서 drift·snapshot·UI 회귀

## 7. 실행한 검증과 결과

| 검증 | 결과 |
| --- | --- |
| Backend Debug/Release build | 성공, warning/error 0 |
| 관련 PostgreSQL 통합 | fresh schema와 기존 `0042 → 0044` upgrade, Draft 저장·활성화·Active 불변·binding·부서 drift·export 재검증 4/4 성공 |
| Backend 전체 | 398/398 성공 |
| Frontend lint | error 0, 기존 `main.tsx` Fast Refresh warning 1만 유지 |
| Frontend typecheck/unit | 성공, 104/104 |
| Frontend production build | 성공, 기존 chunk-size warning 유지 |
| 격리 Full-Stack E2E | Sales·ADMIN 결합 시나리오 1/1 성공 |
| Browser desktop/mobile | 양식 관리 2개 synthetic screenshot, 390px overflow 0 |

Persistent UAT migration/runtime과 실제 운영 양식 content는 승인·입력 범위 밖이라 적용하지 않았다. E2E의 임시 runtime과 DB는 검증 뒤 종료했다.

## 8. 개인정보·secret 검토

- 합성 사용자 역할·부서·양식만 사용했고 실제 사용자 이름이나 업무 내용을 기록하지 않았다.
- 부서장 목록 API와 screenshot에는 synthetic fixture만 사용했다.
- secret, token, raw DB/API body와 external provider 호출은 0이다.

## 9. Finding gate

| Finding | Severity | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `MANUFACTURING_TEMPLATE_RUNTIME_DRIFT` | P2 | RESOLVED | 양식 관리와 제조 실행이 분리되면 활성화가 실제 작업에 반영되지 않음 | 시작 transaction에서 active version lock·FK·snapshot 저장 |
| `DEPARTMENT_MANAGER_STALE_SCOPE` | P2 | RESOLVED | 부서 이동 뒤 기존 위임이 남을 수 있음 | current department와 active binding을 매 요청 함께 검증 |
| `ACTIVE_VERSION_MUTABILITY` | P2 | RESOLVED | 활성 양식 수정 시 과거/진행 기록 의미가 변경됨 | API lifecycle과 DB trigger로 Draft-only edit 강제 |
| `FORM_EXPORT_SCOPE_TOCTOU` | P2 | RESOLVED | 목록 조회 뒤 위임 해제 시 export audit 전 scope가 달라질 수 있음 | export audit transaction에서 current department·active binding 재검증, drift test 추가 |
| `FORM_TEMPLATE_FORMATTING` | P3 | RESOLVED | 신규 C# 파일의 공백·import 순서가 formatter 기준과 불일치 | 변경 C# allowlist formatter 적용·재검증 |

Open P0/P1/P2: `0/0/0`. Risk acceptance 없음.

## 10. SOP — 관리자 운영 절차

1. 시스템 관리자는 `양식 관리`에서 필요 시 부서장을 사용자·부서 단위로 지정한다.
2. 관리자 또는 지정 부서장은 자기 scope의 양식을 선택하고 활성 version에서 새 Draft를 만든다.
3. Draft 항목·순서·필수 여부·입력 형식을 수정하고 저장한다.
4. 검토가 끝난 Draft를 활성화한다. 기존 Active는 자동으로 Archived가 되고 진행 중 snapshot은 유지된다.
5. 잘못 만든 Draft는 보관하고 Active version은 직접 수정하지 않는다.
6. 운영 적용 시 `0044`를 순서대로 적용하고 6종 catalog의 active version과 제조 snapshot FK를 확인한다.

## 11. User manual — 부서장 사용법

- `양식 관리` menu가 보이면 현재 부서에 위임된 양식을 관리할 수 있다.
- 왼쪽에서 양식 종류, 가운데에서 version, 오른쪽에서 Draft 항목을 편집한다.
- 활성 양식은 읽기 전용이다. 변경하려면 `새 Draft`를 만든다.
- 항목은 추가·삭제·위/아래 이동할 수 있고 입력 형식과 필수 여부를 지정할 수 있다.
- `활성화` 이후 새 검사·제조 작업부터 적용된다. 이미 시작된 작업은 원래 양식을 유지한다.
- version checkbox와 상단 전체선택 checkbox로 고른 항목만 Excel로 내보낼 수 있다.

## 12. 사용자 검수 결과와 남은 항목

- 자동 검증·격리 browser 검증: 완료
- 사용자 validation checklist: 작성됨
- 사용자 직접 검수: `사용자 검수 대기 — 마지막 일괄 검수`
- 실제 회사 양식의 세부 항목·사진 위치·PDF layout 입력은 후속 content change이며 본 관리 기능을 재구현하지 않는다.
- 대표 repo·main 승격, Persistent UAT migration/runtime handover, push·PR·merge: 별도 승인 전 금지
- `main` merge 승인: `0/3`

## 13. Rollback·forward-fix

- 코드: experiment commit을 revert한다. 대표 branch에는 반영되지 않았다.
- DB: `0044`를 수정·삭제하거나 DB를 reset하지 않고 새 migration으로 forward-fix한다.
- 잘못 활성화한 양식은 이전 version을 편집하지 않고 그 version을 기반으로 새 Draft를 만들어 재활성화한다.

## 14. 5종 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 |
| SOP | 완료 | 이 문서 10장 |
| User manual | 완료 | 이 문서 11장 |
| Roadmap update | 완료 | [Product Roadmap](../docs/00-product-roadmap.md) TASK-ADMIN-002·Decision Log |
| User validation checklist | 사용자 검수 대기 | [체크리스트](admin-002-user-validation-checklist.md) |

## 15. Fable 사용량·session

- 1차 planning 전/후: 5시간 18%/18%, 주간 전체 17%/17%, Fable 34%/34% 사용
- 2차 planning 전/후: 5시간 42%/42%, 주간 전체 19%/19%, Fable 38%/38% 사용
- 구현 종료 최신: 5시간 50% 사용·50% 잔여, 주간 전체 20% 사용·80% 잔여, Fable 39% 사용·61% 잔여
- Fable private state: `FABLE_TASK_SESSION_CLEANED`, session·transcript 각 2개 제거
