# TASK-PENDING-TYPE-001 — Pending 유형 관리 구현 보고서

## 1. 상태와 기준선

- taskType: `NEW_FEATURE`
- branch: `experiment/task-home-002-personalized-shell`
- startHead: `35af25abfa4adebec5929071d55c2703faefc74f`
- finalImplementationSource: [2차 기획](../docs/34-pending-type-management-plan.md)
- experimentStatus: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- userValidation: `사용자 검수 대기 — 마지막 일괄 검수`
- Git scope: local experiment commit만 허용. push·PR·merge 미승인, main merge 승인 `0/3`
- runtime scope: isolated PostgreSQL·disposable Full-Stack E2E만 변경. Persistent UAT와 실제 provider 미적용

## 2. 해결한 업무 문제

기존 Pending은 `Nonconformance`, `Punch`, `ManufacturingStop`, `Other` 네 코드를 코드와 UI에 함께 고정해 표시명 변경이나 사용자 정의 유형 추가에 코드 수정이 필요했다. 이번 구현은 자동 업무 의미를 나타내는 네 시스템 코드를 보존하면서 관리자가 표시명·설명·수동 등록 노출·순서를 관리하고 사용자 유형을 추가할 수 있게 했다. 일반 Pending의 목록·상세·필터·수동 등록·선택 Excel은 모두 같은 catalog 표시명을 사용한다.

## 3. 포함·제외 범위

포함 범위는 additive `0045` catalog·audit·권한·FK, `PendingType.Manage` 관리자 API, server-generated custom code, CAS 편집·활성화·원자적 전체 순서 변경, Pending 조회·생성·선택 Excel label 통합, desktop 관리 표와 390px 조회 전용 카드다.

제외 범위는 TASK-007A 상태 workflow 재구현, system code·semantic 변경, hard delete, 임의 role editor, catalog 자체 Excel export, 첨부 storage, 대표 repo·`main`, Persistent UAT, provider, push·PR·merge다.

## 4. 전체 아키텍처와 영향

| 영역 | 구현·영향 |
| --- | --- |
| DB/Migration | `0045_pending_issue_type_catalog.sql`: 네 system row seed, custom type lifecycle, row version, append-only audit, 기존 `pending_issues.issue_type` check를 catalog FK로 승격 |
| 권한 | `PendingType.Manage`를 system administrator에게만 부여. 기존 system administrator의 업무 `Pending.Manage` 우회 금지는 유지 |
| Backend | `/api/pending-types` catalog·create·update·activate/deactivate·reorder와 manual/filter option API. custom code는 서버 UUID 기반 `CUSTOM_<32 hex>`로 생성 |
| Concurrency | 편집·활성 상태는 row version CAS, 정렬은 현재 전체 row와 버전을 모두 받는 단일 transaction으로 처리하며 한 행이라도 stale이면 전체 `409` |
| Pending workflow | 생성 시 active+manual-enabled catalog row를 서버에서 확인. 기존 자동 생성은 고정 system code를 계속 사용하고 FK가 유효 code만 허용 |
| Frontend | system administrator에게만 `Pending 유형` navigation 제공. desktop은 관리 action, mobile은 핵심 상태·사용량 조회 전용 카드 |
| Excel | catalog 자체 export는 제외. 기존 Pending 선택 Excel의 `유형` column은 Pending list의 catalog label을 재사용 |
| PDF/첨부 | N/A — 이 Task는 PDF·binary attachment 계약을 변경하지 않음 |
| 기존 기능 회귀 | TASK-007A 생성·배정·상태 전이 contract 유지. system semantic code 비교와 자동 생성은 변경하지 않음 |

## 5. 기술적 결정과 검토한 대안

- system code를 label과 분리했다. code rename/remap은 자동 workflow와 과거 의미를 깨뜨리므로 제거했다.
- custom code 입력 UI를 제공하지 않고 서버가 생성한다. 관리자 오타·semantic 충돌·URL 조작 가능성을 줄인다.
- hard delete 대신 custom active/inactive를 사용한다. 과거 Pending FK와 audit을 보존한다.
- 과거 issue별 label snapshot 대신 현재 catalog label과 immutable code·audit 조합을 선택했다. 사용자 조회는 최신 용어로 통일하고 변경 근거는 audit에 남긴다.
- 부분 reorder 대신 전체 목록 CAS를 선택했다. 경쟁 순서 변경의 부분 적용과 중복 sort order를 차단한다.
- mobile mutation을 축소해 조회 전용 카드로 제공했다. 관리 변경은 넓은 화면에서만 노출하지만 서버 권한이 최종 방어선이다.

## 6. 실제 변경 파일

- DB: `database/migrations/0045_pending_issue_type_catalog.sql`
- Backend: `backend/src/Emi.Qms.Api/PendingTypes/*`, `Pending/PendingStore.cs`, `Identity/QmsPermissions.cs`, `Identity/SeedIdentityData.cs`, `Program.cs`
- Backend test: `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs`
- Frontend: `PendingTypeManagementPage.tsx`, `pendingTypes.ts`, `PendingPage.tsx`, `pending.ts`, `api.ts`, `App.tsx`, `styles.css`
- Full-Stack E2E: `frontend/e2e/full-stack/pending-type-management.full-stack.spec.ts`
- Planning·governance: `tasks/pending-type-001-*`, `docs/34-pending-type-management-plan.md`, Product Roadmap, experiment ledger
- Visual evidence: [desktop/mobile screenshot 폴더](pending-type-001-screenshots/)

## 7. 검증 결과

| 검증 | 적용 | 결과 | 근거 |
| --- | --- | --- | --- |
| Backend Release build | Yes | PASS | warning 0, error 0 |
| Migration fresh DB | Yes | PASS | system seed·least privilege·FK·DB trigger test |
| Migration existing DB | Yes | PASS | `0044 → 0045`, 기존 `Punch` issue code·label 보존 |
| Backend 관련 filtered test | Yes | `4/4` PASS | catalog fence·existing DB upgrade·migration count 영향 회귀 |
| Backend 전체 test | Yes | `403/403` PASS | Release 전체 suite, 9m 37s |
| Frontend lint | Yes | PASS | 기존 `main.tsx` Fast Refresh warning 1, 신규 error 0 |
| Frontend typecheck/build | Yes | PASS | type error 0, production build complete |
| Frontend unit | Yes | `109/109` PASS | 14 files |
| Full-Stack E2E | Yes | `2/2` PASS | permission·CAS·activation·options·atomic reorder·목록/상세 label·선택 Excel·audit·desktop/mobile |
| Desktop/390px visual | Yes | PASS | 5개 synthetic screenshot, mobile horizontal overflow 0 |
| Docs/privacy/diff | Yes | PASS | local link target `4/4`, missing·secret 후보·whitespace error 0 |
| Persistent UAT | No | N/A | 사용자 미승인·실험 경계로 적용 금지 |
| 실제 provider | No | N/A | provider 변경 없음 |
| CI | No | N/A | push·PR 미승인 local experiment commit |

## 8. Finding gate

| Finding | Severity | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `PT-F-001 AUTOMATION_FK_FENCE` | P1 | `RESOLVED` | custom catalog가 자동 code를 덮으면 자동 Pending이 깨질 수 있음 | system code/is_system/is_active DB trigger와 Pending FK 적용 |
| `PT-F-002 CUSTOM_CODE_INPUT` | P2 | `RESOLVED` | 사용자 입력 code는 오타·semantic 충돌 가능 | server-generated immutable `CUSTOM_<32 hex>` 적용 |
| `PT-F-003 PARTIAL_REORDER` | P2 | `RESOLVED` | 경쟁 reorder가 일부 행만 적용될 위험 | 전체 row/version CAS와 deferrable unique transaction 적용 |
| `PT-F-004 LABEL_DRIFT` | P2 | `RESOLVED` | 목록·상세·filter·Excel label이 다를 위험 | Backend catalog join을 단일 label source로 사용 |
| `PT-F-005 OPTION_FALLBACK` | P2 | `RESOLVED` | option API 실패 시 hardcoded 값으로 잘못 등록할 위험 | filter·create control을 fail-closed 처리 |
| `PT-F-006 CATALOG_EXPORT` | P3 | `BACKLOG` | catalog 설정 자체의 Excel 백업 UI는 미구현 | Product Roadmap 조건부 backlog `Pending 유형 catalog export`에 연결. 현재 업무 Pending 선택 Excel은 구현 완료 |

Open P0/P1/P2는 `0/0/0`이다.

## 9. 시행착오 및 폐기한 접근

- 첫 Full-Stack 실행은 E2E backend가 Release `--no-build`를 사용해 이전 binary를 기동했고 새 route가 `404`였다. Release build를 명시적으로 갱신해 해결했다.
- Development persona 권한은 DB seed만으로 충분하지 않았다. dev authentication이 사용하는 in-memory permission catalog에도 같은 system-administrator 전용 권한을 추가했다.
- system administrator는 의도적으로 업무 Pending mutation 권한이 없으므로 dynamic option 화면 검증은 품질 persona로 전환했다. 관리자 업무 권한을 확대하는 접근은 폐기했다.
- dynamic option screenshot에서 닫힌 select가 기본값만 보여 주는 문제를 확인해 custom type을 실제 선택한 상태로 증빙하도록 보정했다.

## 10. SOP — 운영 승격·복구

1. 이 실험 commit을 대표 repo로 복사하거나 Persistent UAT에 직접 적용하지 않는다. 별도 승격/UAT Task와 main merge 3회 승인부터 확인한다.
2. 승인된 승격에서는 현재 운영 DB backup·migration ledger·open Pending type code 분포를 privacy-safe projection으로 기록한다.
3. disposable clone DB에서 `0044 → 0045` rehearsal을 먼저 실행하고 네 system row·FK·system administrator 단일 권한·기존 issue count를 확인한다.
4. Persistent UAT maintenance window에서 additive `0045`를 적용하고 `/health/ready`, catalog read, 일반 역할 `403`, system administrator read를 확인한다.
5. rollback SQL은 제공하지 않는다. FK·catalog·audit이 생긴 뒤 down migration은 의미를 잃을 수 있으므로 문제 발생 시 새 migration 번호의 forward-fix를 작성한다.
6. runtime rollback이 필요하면 이전 application binary로 되돌리되 DB `0045`는 유지한다. 이전 binary의 네 system code는 catalog FK와 호환된다.
7. custom type을 잘못 만든 경우 hard delete하지 않고 사용 중지하고 audit·연결 usage를 확인한다.

## 11. User manual

### PC 관리자

1. 왼쪽 메뉴에서 `Pending 유형`을 연다.
2. `+ 사용자 유형 추가`에서 표시명과 선택 설명을 입력한다. 내부 code는 서버가 자동 생성한다.
3. 각 행의 `편집`에서 표시명·설명·수동 등록 표시 여부를 변경한다. `기타`는 수동 등록에서 숨길 수 없다.
4. 사용자 유형은 `사용 중지`할 수 있다. 사용 중지해도 과거 Pending 조회와 필터에는 남고 신규 수동 등록에서만 제외된다.
5. 위·아래 화살표로 전체 순서를 만든 뒤 `순서 저장`을 누른다. 다른 관리자가 먼저 변경했으면 새로고침 안내가 나오며 일부만 저장되지 않는다.
6. system 유형은 code·활성 상태를 변경하거나 삭제할 수 없다.

### 모바일 관리자

`Pending 유형`에서 전체·사용 중·사용자 유형·연결 건수와 각 유형의 상태를 확인한다. 모바일은 조회 전용이며 추가·편집·정렬·활성 변경은 PC 관리자 화면에서 수행한다.

### 일반 Pending 사용자

Pending filter에는 활성·비활성 과거 유형이 모두 표시되고, 신규 등록에는 현재 활성·수동 등록 허용 유형만 표시된다. 관리자가 표시명을 바꾸면 목록·상세·선택 Excel에서 같은 최신 표시명이 보인다.

## 12. 사용자 검수 체크리스트

자동 검증 상태는 `완료`, 사용자 직접 검수 상태는 `사용자 검수 대기 — 마지막 일괄 검수`다.

- [x] isolated DB에서 fresh `0045`와 `0044 → 0045` 적용
- [x] system administrator allow, 업무 역할 deny, 일반 Pending 업무 권한 불변 확인
- [x] 사용자 유형 추가·편집·사용 중지·재활성·stale write·전체 reorder 확인
- [x] 목록·상세 response·filter/manual option·선택 Excel label 일치 자동 확인
- [x] desktop 관리 표·추가 modal·Pending 등록 dialog screenshot 확인
- [x] 390px 조회 전용 카드와 horizontal overflow 0 확인
- [ ] 사용자가 최종 일괄 검수에서 PC 관리자 흐름 확인
- [ ] 사용자가 최종 일괄 검수에서 모바일 정보 밀도·용어 확인
- [ ] 승격을 선택할 경우 별도 UAT Task에서 Persistent DB·runtime 검증

## 13. 개인정보·secret 검토

Screenshot과 E2E data는 synthetic persona·synthetic 유형만 사용했다. 실제 사용자·고객·프로젝트 정보, token, secret, tenant/client/object id, provider payload는 문서·artifact에 기록하지 않았다. `.env`, 인증서, 실제 provider와 Persistent UAT는 변경하지 않았다.

## 14. Known issue·잔여 위험·후속

- catalog 설정 자체 Excel export는 `PT-F-006` P3 backlog다.
- 사용자 직접 검수는 마지막 일괄 검수로 대기한다.
- 대표 repo·main·Persistent UAT 승격은 별도 Task이며 이번 완료에 포함되지 않는다.
- Product Roadmap 기준 다음 canonical 후보는 QR 스캔 landing이지만 Task ID와 공개/인증 landing 정책이 없어 `DEFERRED / POLICY_INPUT`이다.

## 15. 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 |
| SOP | 완료 | 이 문서 `10. SOP` |
| User manual | 완료 | 이 문서 `11. User manual` |
| Roadmap update | 완료 | [Product Roadmap](../docs/00-product-roadmap.md), [experiment ledger](../docs/27-experiment-task-ledger.md) |
| User validation checklist | 작성·자동 검증 완료·사용자 검수 대기 | 이 문서 `12. 사용자 검수 체크리스트` |

## 16. 코드 구현과 live 상태 구분

코드·migration·자동 검증·synthetic screenshot은 현재 experiment branch에 구현됐다. Persistent UAT·대표 runtime·GitHub main에는 적용하거나 검증하지 않았고, push·PR·merge도 수행하지 않았다.

## 17. Claude 사용량 최신 상태

구현 종료 privacy-safe projection은 5시간 현재 세션 `22% 사용 / 78% 잔여 / 22:49 KST 초기화`, 주간 전체 모델 `24% 사용 / 76% 잔여 / 07-25 07:59 KST 초기화`, 주간 Fable `48% 사용 / 52% 잔여 / 초기화 parse 불가`다. 1·2차 planning 전후 기록은 [Change 001](pending-type-001-change-001.md)에 보존했다.
