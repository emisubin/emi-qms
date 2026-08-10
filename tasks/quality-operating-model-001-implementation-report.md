# TASK-QUALITY-OPERATING-MODEL-001 구현 보고

상태: `BASE TASK USER_VALIDATION_COMPLETE / CHANGE 004·005 LATEST_MAIN_INTEGRATED / AUTOMATED_VALIDATION_COMPLETE / USER_VALIDATION_PENDING / PUBLICATION_PENDING`

## 해결한 업무 문제

1. 품목명 문자열만으로 외함을 알아낼 수 없어 IQC 대상이 잘못 연결될 수 있었다.
2. 모든 구매품 도착분이 IQC로 넘어가 실제 현장의 “외함만 IQC” 운영과 달랐다.
3. 협력사 종이 검사서에 품질팀이 확인·서명하는 현장 절차를 시스템의 상세 체크리스트로 다시 입력해야 했다.
4. 이미 생성된 프로젝트의 기존 IQC 흐름과 새 정책을 안전하게 구분할 기준이 없었다.

## 구현 결과

1. 양식 관리에 `구매품 구분·IQC 연결`을 추가했다.
   - 초기 구분은 `외함`, `판금류`, `부스바`, `명판`, `기타`다.
   - 외함만 `IQC 필요`, 나머지는 `IQC 없음`으로 시작한다.
   - 품질팀 사용자와 시스템 관리자가 이름·정렬·사용 여부·IQC 필요 여부를 관리한다.
   - 이미 사용한 구분은 삭제하지 않고 사용 중지하며 변경 이력을 남긴다.
2. 신규 프로젝트의 구매 직접 입력과 Excel 입력에 `구분`을 필수로 추가했다.
   - 선택한 구분의 이름·코드·IQC 필요 여부를 구매품에 snapshot으로 저장한다.
   - 이후 양식 관리의 구분 설정이 바뀌어도 이미 저장된 구매품의 판단은 바뀌지 않는다.
   - 도착분이 생긴 구매품의 구분 변경은 차단한다.
3. 프로젝트 단위 전환을 적용했다.
   - 기능 업데이트 전에 존재하던 프로젝트는 `AllReceipts`로 남아 기존처럼 모든 도착분이 상세 IQC로 간다.
   - 정상 화면과 Excel로 새로 만드는 프로젝트만 `CategoryBased`로 고정된다.
   - 프로젝트 생성 후 정책을 바꾸거나 기존 프로젝트에 소급 적용할 수 없다.
4. 신규 프로젝트의 도착 등록을 구매품 snapshot 기준으로 분기한다.
   - 외함: `ScanBased` IQC 업무와 알림을 품질팀에 생성한다.
   - 비외함: IQC 업무를 만들지 않고 `검사 대상 아님` 상태와 자재 입고 확정 업무·알림을 생성한다.
   - 도급·사급 구분은 이 판단에 영향을 주지 않는다.
5. 외함 스캔형 IQC를 구현했다.
   - 품질팀은 PDF·JPEG·PNG를 회차당 최대 10개, 파일당 최대 10MB로 첨부한다.
   - 실제 파일 signature를 확인해 확장자만 바꾼 파일은 차단한다.
   - 첨부가 없으면 적합·부적합 모두 확정할 수 없다.
   - 확정 전에는 파일을 추가·삭제할 수 있고, 확정 뒤 판정과 파일은 API와 DB trigger 양쪽에서 수정·삭제를 차단한다.
   - 첨부 내용의 SHA-256까지 확정 snapshot에 포함한다.
6. 외함 부적합은 기존 Pending 흐름에 연결했다.
   - 조치 완료 후 재검사는 기존 판정을 고치지 않고 새 IQC 회차를 만든다.
   - 재검사 회차에도 새 서명 스캔본이 필수다.
   - 이전 회차의 판정 사유·조치 내용·첨부를 계속 조회하고 다운로드할 수 있다.
7. 비외함 도착분은 자재 담당자가 `입고 확정`을 눌러야 최종 입고된다.
   - 입고 전에는 취소할 수 있다.
   - 자재 준비 집계에는 포함하지만 IQC 합격·대기 집계에는 포함하지 않는다.
   - 입고 확정 이후의 기존 키팅·제조 투입·생산계획 실적 연결은 `Confirmed` 기준을 그대로 사용한다.

## 최종 기획 대비 차이

Fable 2차 기획은 구분 관리 권한을 기존 양식 관리 `CanManage` 사용자로 제안했다. 이후 사용자가 “품질팀 모두”로 명시해 최신 사용자 결정이 우선하므로, 실제 구현은 모든 활성 품질팀 사용자와 시스템 관리자에게 구분 관리 권한을 부여했다. 외함 IQC routing·기존 프로젝트 보존·불변 증빙 등 나머지 계약은 최종 기획과 같다.

## Fable·Claude 5시간 사용량

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 기획 직전 | 0% 사용 / 100% 잔여 / 20:39 KST 초기화 | 14% 사용 / 86% 잔여 | 26% 사용 / 74% 잔여 |
| 1차 기획 직후 | 0% 사용 / 100% 잔여 / 20:40 KST 초기화 | 14% 사용 / 86% 잔여 | 27% 사용 / 73% 잔여 |
| 2차 기획 직전 | 0% 사용 / 100% 잔여 / 20:40 KST 초기화 | 14% 사용 / 86% 잔여 | 27% 사용 / 73% 잔여 |
| 2차 기획 직후 | 9% 사용 / 91% 잔여 / 20:40 KST 초기화 | 27% 사용 / 73% 잔여 | 21% 사용 / 79% 잔여 |

2차 기획 직후 표시된 주간 Fable 사용률이 직전보다 낮아졌지만 임의 보정하지 않고 Claude `/usage`가 표시한 값을 그대로 기록했다. 초기화 시각을 포함한 원문 projection은 `tasks/quality-operating-model-001-change-002.md`에 보존했다.

## 데이터·보안

- migration `0067_material_category_scan_iqc.sql`
  - 구매품 구분 catalog·감사 이력
  - 프로젝트 IQC routing snapshot과 불변 trigger
  - 구매품 구분 snapshot
  - `InspectionNotRequired`, `ScanBased`
  - 스캔형 IQC report·attachment와 확정 불변 trigger
- 파일 본문은 bounded DB storage에 저장하며 응답 JSON이나 내부 경로로 노출하지 않는다.
- 다운로드는 프로젝트 접근 범위와 품질 읽기 권한을 확인하고 `nosniff`와 안전한 파일명을 사용한다.
- 확정 증빙은 수정 API가 없고 DB update/delete도 거부한다.
- 구현·자동 검증 단계에서는 대표 repository, `main`, Persistent UAT와 실제 외부 알림 provider를 변경하지 않았다.

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `QOM-001-F01` | P1 | Resolved | 품목명으로 IQC 대상을 추정하면 오탈자·다른 명칭 때문에 누락 또는 오검사가 발생한다. | 관리형 구분과 구매품 snapshot을 추가했다. |
| `QOM-001-F02` | P1 | Resolved | 신규 정책이 기존 프로젝트에 소급되면 진행 중인 도착분과 품질 이력이 바뀐다. | 프로젝트 생성 시점의 불변 routing 정책을 추가하고 기존 row를 `AllReceipts`로 보존했다. |
| `QOM-001-F03` | P1 | Resolved | 종이 검사서를 다시 시스템 체크리스트로 입력하는 이중 작업이 있었다. | 외함은 한 건 적합/부적합 + 다중 서명 스캔본 방식으로 분리했다. |
| `QOM-001-F04` | P1 | Resolved | 확정 검사서와 재검사 근거가 덮어써질 수 있었다. | 회차별 report·attachment와 이중 불변 제어, 새 회차 재검사를 적용했다. |
| `QOM-001-F05` | P2 | Resolved | 비검사품을 IQC 합격으로 표현하면 실제 검사 여부가 왜곡된다. | `InspectionNotRequired` 상태와 별도 문구·집계를 추가했다. |
| `QOM-PROMO-F01` | P1 | Resolved | 최신 `main` 승격 검증에서 Backend 성적서 PDF 테스트가 Repository root를 Backend 하위 root로 해석해 Frontend 증빙 파일을 찾지 못했다. 제품 경로에는 영향이 없지만 전체 회귀가 실패했다. | 테스트 경로를 Backend root의 상위 Repository에 명시적으로 맞췄다. |
| `QOM-PROMO-F02` | P1 | Resolved | 기존 Full-Stack fixture가 신규 프로젝트의 필수 구매품 구분과 항목 바로 아래 사진 첨부 UX를 반영하지 않아 18개 회귀가 실패했다. | 신규 프로젝트 fixture에는 구분 snapshot을 넣고, 과거 상세 IQC 회귀는 격리 DB에서 명시적으로 `AllReceipts` 프로젝트로 고정했으며, 사진 필수 항목의 inline 첨부 계약으로 E2E를 갱신했다. |

Open P0/P1/P2: `0/0/0`.

## 실행한 검증

- Backend Release build: 오류 0, 경고 0.
- Backend 격리 집중 회귀: 신규 routing·migration·기존 상세 IQC `4/4` 통과.
- Backend 전체 격리 회귀: `465/465` 통과.
- Backend 스캔형 IQC 부적합 → Pending → 재검사 회차·서명본 보존 집중 회귀: `1/1` 통과.
- Frontend 전체 unit: 22 files, `144/144` 통과.
- Frontend 스캔형 IQC 집중 unit: `3/3` 통과.
- Frontend typecheck: 통과.
- Frontend lint: error 0, 기존 `frontend/src/main.tsx` Fast Refresh warning 1.
- Frontend production build: 통과. 기존 큰 chunk 안내만 남았다.
- Mock UI smoke: `4/4` 통과.
- Isolated Full-Stack E2E: `55/55` 통과.
  - 일반 역할별 프로젝트 등록→세금계산서 요청 18단계 완료.
  - 12면·사급 분할입고 6회·제조 Pending 6건·전체 흐름 18단계·열린 Pending 0건·프로젝트 완료.
  - 신규 `CategoryBased` 구분 fixture와 기존 `AllReceipts` 상세 IQC 회귀를 분리해 함께 검증했다.
- migration `0067`: fresh 적용과 기존 migration 계약 검사를 격리 PostgreSQL에서 통과했다.
- Browser desktop/mobile:
  - 품질팀 일반 사용자에게 구매품 구분 관리가 노출되고 편집 가능한 것을 확인했다.
  - 구매 입력의 구분 선택, 외함 스캔형 IQC, 비외함 `IQC 비대상 · 확정 대기`를 실제 화면에서 확인했다.
  - 확인한 모바일 화면은 문서 가로 overflow가 없었다(`scrollWidth == clientWidth`).
  - 확인 종료 시 브라우저 warning/error log는 `0건`이었다.

## 미실행 검증과 이유

- Persistent UAT migration·runtime handover: 사용자 승인 범위 밖이다.
- 실제 Teams·메일 provider 발송: 이번 기능은 기존 인앱 업무·알림 경로만 사용하며 실제 provider는 제외 범위다.
- 악성코드 scanner·OCR·외부 object storage: 명시적 후속 운영 범위다.
- 사용자 검수: 2026-07-31 사용자가 체크리스트 전체의 검수 완료를 명시했다.

## Rollback·forward-fix

- 화면과 routing 코드는 experiment branch에서만 되돌릴 수 있다.
- migration `0067`은 additive다. 적용 후 테이블이나 확정 증빙을 자동 삭제하지 않고 구형 코드가 읽지 않는 상태로 둔 뒤 forward-fix한다.
- 이미 생성된 `CategoryBased` 프로젝트를 `AllReceipts`로 변경하지 않는다. routing 정책과 확정 스캔 증빙은 감사 근거다.

## 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| 1차 Fable planning | 작성됨 | `tasks/quality-operating-model-001-planning.md` |
| Codex review | 작성됨 | `tasks/quality-operating-model-001-review.md` |
| 2차 Fable planning | 작성됨 | `docs/48-enclosure-iqc-routing-plan.md` |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 작성됨 | `tasks/quality-operating-model-001-sop.md` |
| User manual | 작성됨 | `tasks/quality-operating-model-001-user-manual.md` |
| Desktop/mobile 화면 증빙 | 작성됨 | `tasks/quality-operating-model-001-screenshots/` |
| Roadmap·실험 원장 | 갱신됨 | `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md` |
| 사용자 검수 체크리스트 | 작성됨 / 사용자 검수 완료 | `tasks/quality-operating-model-001-user-validation-checklist.md` |

## 변경·게시 경계

- experiment worktree에서 구현·자동 검증·사용자 검수를 완료했다.
- 2026-07-31 사용자가 local commit과 `main` 병합을 명시적으로 승인했고, 최신 `origin/main` 기준 promotion 검증 뒤 local `main` fast-forward 병합을 완료했다.
- Remote push·Persistent UAT와 실제 provider는 승인 범위에 포함되지 않는다.

## Change 004 — Item별 LQC 운영 상태·검사 양식

### Change 004 구현 결과

1. 전역 LQC 스위치를 제거하고 `양식 관리 > Item별 LQC 검사`에 Item 선택, Item별 `운영 중/운영 중지` 스위치와 검사 항목 편집을 통합했다.
2. 운영 상태는 시스템 관리자만 변경한다. 품질 양식 관리자는 상태를 조회하고 Item별 검사 항목을 수정할 수 있다.
3. 각 프로젝트는 생성 transaction에서 Item의 LQC 운영 상태와 현재 양식 version을 snapshot하며, DB trigger로 두 값을 생성 후 변경할 수 없게 했다.
4. migration 전 기존 프로젝트는 `운영 중 + 기존 공통 LQC 양식`으로 보존하고, 전체 Item의 신규 설정은 `운영 중지 + 기존 공통 양식 복제본`으로 초기화했다.
5. 운영 중지로 생성된 프로젝트는 제조 시작 시 LQC 담당자와 업무를 요구하거나 생성하지 않고, 제조 완료 뒤 LQC 합격 없이 OQC 업무를 정확히 한 번 생성한다.
6. 운영 중으로 생성된 프로젝트는 이후 Item 설정이 바뀌어도 기존 제조+LQC joint gate와 생성 당시 양식 version을 유지한다.
7. 자동·수동 인계 근거에는 실제 제조 execution과 `ManufacturingOnly` 또는 `ManufacturingAndLqc`를 기록한다.
8. 전체 흐름, 프로젝트 진행률·현재 단계·필수 담당자, LQC 대기열과 누락 업무 복구는 현재 Item 설정이 아니라 프로젝트 snapshot을 기준으로 처리한다.
9. 운영 중지 프로젝트의 기존 생산계획 `LQC_PASSED` 연결은 같은 제조 단계 완료 실적으로 대체해 진행하고, 새 연결은 Frontend와 Backend에서 차단한다.
10. 새 Item을 만들면 LQC 기본 양식을 자동 복제하고 운영 중지 상태로 Item별 설정을 함께 생성한다.
11. 기존 LQC 확정 성적서·사진·PDF·Pending·재검사 이력·담당자는 취소·삭제하지 않는다.

### 데이터·감사

- migration `0070_lqc_operating_suspension.sql`은 Item별 설정·양식 scope·append-only 변경 audit, 프로젝트 불변 snapshot과 제조완료확인의 `manufacturing_execution_id`·`handoff_basis`를 추가한다.
- backfill할 제조 완료 execution이 없는 기존 확인 row가 있으면 migration을 중단해 감사 근거 없는 데이터를 만들지 않는다.
- Item 상태·양식 변경에는 변경자·시각·이전값·새값과 optimistic concurrency를 적용한다.
- 운영 중지 프로젝트의 인계는 LQC attempt, 합격 판정 또는 LQC 완료 event를 생성하지 않는다.
- migration과 코드 변경은 additive·forward-fix 기준이며 기존 확정 품질 기록을 삭제하지 않는다.

### 검증 현황

- Backend 집중 회귀: migration, Item별 상태·양식 권한과 audit, 신규 Item 초기화, 프로젝트 상태·양식 snapshot 불변, 운영 중지 제조 시작·직접 OQC 인계, 운영 중 joint gate, 누락 인계 복구, 생산계획 source 차단·기존 연결 fallback, workflow 단계·진행률을 검증했다.
- Frontend 전체 unit: 25 files, `177/177` 통과.
- Frontend typecheck: 통과.
- Frontend lint: error 0, 기존 `frontend/src/main.tsx` Fast Refresh warning 1.
- Frontend production build: 통과. 기존 큰 chunk 안내만 남았다.
- Backend Release build: 경고 0, 오류 0.
- Backend 최종 전체 회귀: `487/487` 통과, 실패 0, 건너뜀 0.

### Change 004 남은 경계

- 사용자 화면 검수는 아직 수행하지 않았다.
- commit·push·PR·`main` 병합, Persistent DB migration, Azure runtime과 실제 provider는 승인 범위 밖이다.
- 구매품별 IQC 양식은 아래 Change 005에서 구현했으며, LSE TASK NO·부서 Pending·설계 도번·필수값·패널 묶음은 후속 3~5번 작업이다.

## Change 005 — 구매품 구분별 IQC 운영 방식·검사 양식

### Change 005 구현 결과

1. `양식 관리 > 구매품별 IQC 양식`에 구매품 구분 검색·선택, 검사 스위치, 스캔형/상세형 방식 선택과 상세 검사 항목 편집을 추가했다.
2. IQC 설정은 지정된 활성 품질 domain 양식 관리자와 시스템 관리자만 변경할 수 있다. 일반 품질 사용자는 기존 구분 이름·순서·활성 상태 관리만 유지하며 IQC 설정 mutation은 403으로 차단된다.
3. 기존 구매품 구분 관리의 IQC 토글을 제거하고 신규 설정을 유일한 쓰기 source로 만들었다. 화면에는 신규 설정에서 계산한 `IQC 없음/스캔형/상세형` 상태만 조회 표시한다.
4. 새 구매품 구분은 항상 `검사 없음`으로 생성하고, 비활성 구분도 양식 관리에서 badge와 함께 준비·수정할 수 있다.
5. 상세형은 검사 항목 1개 이상을 저장한 뒤에만 켤 수 있고, 운영 중인 상세형의 마지막 항목 삭제는 Backend와 Frontend에서 차단한다.
6. 구매품 저장 시 검사 여부와 방식을 snapshot한다. 이후 구분 설정을 바꿔도 이미 저장된 구매품의 도착 분기는 바뀌지 않는다.
7. 도착 등록은 snapshot에 따라 `검사 없음`, `ScanBased`, `Detailed`로 분기한다. CategoryBased의 유효한 snapshot이나 구분 양식이 없으면 전역 양식으로 우회하지 않는다.
8. CategoryBased 상세 성적서는 품질 사용자가 성적서를 최초 생성하는 순간 해당 구분의 현재 양식 version을 고정한다. 이후 구분 양식 변경은 열린 성적서에 소급되지 않는다.
9. 기존 `AllReceipts` 프로젝트는 전역 `MATERIAL_IQC` 상세 양식을 유지한다. Detailed 재검사는 직전 실패 성적서 양식을, ScanBased 재검사는 새 스캔본을 사용하는 기존 계약을 보존한다.
10. 설정·양식 변경에는 row version 기반 optimistic concurrency와 append-only 감사 이력을 적용했다.

### Change 005 데이터·보안

- migration `0071_material_category_iqc_templates.sql`
  - 구분별 IQC 설정과 현재 양식 연결
  - 구매품 검사 여부·방식 snapshot
  - 설정·양식 변경 append-only 감사
  - 기존 외함=`ScanBased` 운영, 나머지=`검사 없음` backfill
- 기존 `material_categories.requires_iqc`는 앱 교체 호환을 위한 DB 자동 동기화 조회값으로만 남기고 직접 변경 trigger를 차단해 독립적인 두 번째 쓰기 source가 되지 않게 했다.
- migration 전 구매품 snapshot은 기존 검사 여부와 `ScanBased`로 보존하며 기존 프로젝트 routing 정책과 확정 증빙은 수정하지 않는다.
- 구현·검증은 격리 PostgreSQL과 임시 Full-Stack runtime에서만 수행했다. Persistent DB·Azure runtime·실제 provider·기존 5174/5081 process는 변경하지 않았다.

### Change 005 Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `QOM-C005-F01` | P1 | Resolved | 구분 catalog의 기존 `requires_iqc`와 신규 설정을 함께 쓰면 구매 저장과 도착 분기가 서로 다른 값을 읽을 수 있다. | 신규 설정을 유일한 쓰기 source로 두고 기존 컬럼은 직접 변경 불가한 호환 projection으로 자동 동기화했다. |
| `QOM-C005-F02` | P1 | Resolved | 일반 품질 사용자가 기존 구분 수정 API로 IQC 상태를 바꾸면 지정 부서장 권한을 우회할 수 있다. | 구분 metadata와 IQC 설정 API를 분리하고 관리자 binding을 Backend에서 강제했다. |
| `QOM-C005-F03` | P1 | Resolved | 설정 변경이 저장된 구매품에 소급되면 동일 발주품의 도착 처리 방식이 중간에 바뀐다. | 구매품에 검사 여부·방식을 snapshot하고 도착분은 snapshot만 읽게 했다. |
| `QOM-C005-F04` | P2 | Resolved | 390px에서 공통 input 높이 규칙이 검사 스위치에 적용돼 label과 방식 선택이 비정상 줄바꿈됐다. | 해당 workspace의 checkbox 크기와 mobile 한 열 배치를 명시하고 overflow 검증을 추가했다. |
| `PRIVACY-RUNTIME-LOG-PROJECTION-003` | P2 | Resolved | 임시 검수 Backend 세션 종료 때 누적 HTTP 요청 로그가 도구 출력에 함께 표시됐다. 응답 본문·개인정보·secret은 없었고 tracked/staged artifact에도 남지 않았다. | 원문을 보고·문서에 복사하지 않고 폐기했다. 이후 runtime 종료·상태 증빙은 세션 출력 재개가 아니라 PID·port·fixed status projection만 사용한다. |

Open P0/P1/P2: `0/0/0`.

### Change 005 실행한 검증

- Backend Release build: 오류 0, 경고 0.
- Backend 집중 회귀: 권한, 상세형 항목 최소 조건, stale 저장 409, 설정 비소급, 검사 없음/스캔형/상세형 분기, 성적서 최초 생성 시 양식 고정을 통과했다.
- Backend 최종 전체 회귀: `489/489` 통과, 실패 0, 건너뜀 0.
- migration `0071`: fresh 적용, 초기 설정·구분 양식·snapshot column·append-only audit guard를 격리 PostgreSQL에서 통과했다.
- Frontend unit: 25 files, `178/178` 통과.
- Frontend lint: 오류 0, 기존 `frontend/src/main.tsx` Fast Refresh warning 1.
- Frontend typecheck와 production build: 통과. 기존 큰 chunk 안내만 남았다.
- Isolated Full-Stack E2E: 양식 관리 desktop·390px `1/1` 통과, page-level horizontal overflow 0.
- 화면 증빙: `tasks/quality-operating-model-001-change-005-screenshots/`의 desktop·390px 2장.

### Change 005 남은 경계

- Change 004·005 사용자 검수는 아직 수행하지 않았다. 2026-08-06 사용자는 현재 상태의 local checkpoint commit만 승인했다.
- 판금류·부스바·명판의 실제 검사 항목 입력과 검사 활성화는 품질팀 운영 입력으로 남겼다.
- local checkpoint commit은 승인됐다. push·PR·`main` 병합, Persistent DB migration, Azure runtime과 실제 provider 반영은 승인 범위 밖이다.
- LSE TASK NO, 부서 Pending, 설계 도번·필수값·패널 묶음은 승인된 후속 3~5번 작업이다.

## Change 006 — 최신 main 통합·전체 검증·게시 준비

### 통합 결과

1. 최신 `origin/main` `12fd51947bfefe94a9abe1b4037bb6fcce6b2d81`에서 승격 branch를 만들었다.
2. Change 004·005 checkpoint `5181726c85af90fd7760dbedf318b084484beae2`의 제품 코드, migration `0070`·`0071`, tests·문서만 선택 이식했다.
3. 최신 main의 EMI PMS 브랜드, 모바일 PWA, Teams launcher, Easy Auth, Azure 승인형 release와 변경 인지형 CI 정책은 유지했다.
4. 신규 CI 정책에 맞춰 코드 PR의 Backend·Frontend 선행 검증, 둘 다 통과한 최신 head의 Full-Stack, 최종 `CI Gate`를 게시 관문으로 고정했다.

### Change 006 Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `QOM-C006-F01` | P1 | Resolved | 최초 Full-Stack의 기존 LQC 회귀 9건이 migration `0070`의 신규 Item 기본값 `운영 중지`를 반영하지 않아, LQC 생성·18단계를 기대하며 실패했다. 제품 정책을 되돌리면 신규 프로젝트 스냅샷 계약이 깨진다. | LQC를 검사하는 회귀 fixture가 프로젝트 생성 전 관리자 API로 RPP LQC를 명시적으로 켜고, 실제 생성 snapshot을 통해 검증하도록 수정했다. 선택 9/9 및 전체 57/57이 새 격리 DB에서 통과했다. |
| `QOM-C006-F02` | P1 | Resolved | 독립 검증에서 기존 구매품을 같은 구분 ID로 일반 수정하면 Backend가 최신 구분 IQC 설정을 다시 읽어 저장 시점 snapshot을 덮어쓰는 비소급 계약 위반을 확인했다. Excel 재입력도 같은 구분 이름을 최신 master로 다시 해석할 수 있었다. | 신규 구매품 또는 실제 구분 변경일 때만 활성 master를 읽고, 같은 구분의 화면 수정·Excel 재입력은 저장된 검사 여부·방식 snapshot을 유지한다. 비활성화된 기존 구분도 일반 정보 수정은 허용한다. 설정 변경 뒤 화면 수정·Excel 수정·도착 분기 회귀를 추가했다. |
| `QOM-C006-F03` | P2 | Resolved | LQC 비소급 회귀가 UL67을 켠 뒤 끌 때 새로 만든 다른 Item을 선택해, UL67 상태 변경 후 기존 프로젝트 snapshot 보존을 실제로 검증하지 않았다. | 끄기 대상도 UL67로 고정하고 응답에서 UL67이 실제 운영 중지인지 확인한 뒤 두 기존 프로젝트의 서로 다른 snapshot이 유지되는지 검증했다. |
| `QOM-C006-F04` | P1 | Resolved | 독립 재검증에서 구분 master 이름과 IQC 설정을 함께 바꾼 뒤 Excel이 새 이름을 보내면, 같은 구분 ID인데도 이름 비교 때문에 최신 IQC 설정이 기존 구매품 snapshot을 덮어쓸 수 있었다. | Excel preview·apply 모두 활성 master 조회 결과의 구분 ID와 저장 ID를 비교한다. ID가 같으면 이름과 무관하게 저장 snapshot을 유지하고 구분 변경 audit도 만들지 않는다. 이름 변경+IQC 변경+일반 정보 수정 뒤 기존 snapshot과 도착 분기를 확인하는 회귀를 추가했다. |

Open P0/P1/P2: `0/0/0`.

### 최신 main 기준 검증

- Backend Release build: 경고 0, 오류 0.
- Backend 전체 test: `491/491` 통과, 실패 0, 건너뜀 0.
- Frontend lint: 오류 0, 기존 `frontend/src/main.tsx` Fast Refresh warning 1.
- Frontend typecheck: 통과.
- Frontend 전체 unit: 26 files, `190/190` 통과.
- Frontend production build: 통과. 기존 대형 chunk 안내만 남았다.
- Mock UI: `8/8` 통과.
- 초기 Full-Stack: `48/57` 통과, 기존 LQC fixture drift 9건 확인.
- 보정 후 LQC 선택 Full-Stack: `9/9` 통과.
- 보정 후 전체 Isolated Full-Stack: `57/57` 통과. 독립 검증 Finding 보정 뒤에도 새 격리 DB에서 `57/57`을 재통과했다. migration fresh 적용, 1면·12면 18단계, LQC/IQC, Pending, 출하·정산을 확인했고 임시 DB·container를 정상 삭제했다.
- Git diff check: 통과.

### 남은 gate

- Change 004·005 사용자 화면 검수가 남아 있다.
- 검수 통과 후 allowlist·privacy·secret·generated artifact 검사, commit·push·PR, 최신 PR head `CI Gate`, main 병합 순으로 게시한다.
- 최신 main full SHA로 승인형 Azure release를 실행해 migration `0070`·`0071` → Backend → Frontend를 교체한 뒤 health·익명 인증 차단·DB ledger를 확인한다.
- 실제 Teams·메일 발송, 알림 정책·Web Push, 후속 3~5번 제품 기능은 제외한다.
