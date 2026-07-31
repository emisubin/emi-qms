# TASK-QUALITY-OPERATING-MODEL-001 구현 보고

상태: `IMPLEMENTED / AUTOMATED_VALIDATION_COMPLETE / USER_VALIDATION_COMPLETE / MAIN_MERGE_APPROVED / PROMOTION_VALIDATION_COMPLETE`

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

- 현재 experiment worktree에서 구현·자동 검증·사용자 검수를 완료했다.
- 2026-07-31 사용자가 local commit과 `main` 병합을 명시적으로 승인했다.
- Remote push·Persistent UAT와 실제 provider는 승인 범위에 포함되지 않는다.
