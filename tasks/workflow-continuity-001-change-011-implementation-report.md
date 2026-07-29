# TASK-WORKFLOW-CONTINUITY-001 Change 011 구현 보고 — LQC·OQC 원자 확정과 오류 복구

## 해결한 업무 문제

고정 검수 runtime에서 품질 확정 요청은 모두 서버 응답을 마쳤지만, 체크리스트 저장 성공 뒤 최종 판정이 거절되면 화면이 오래된 version에 남았다. 이후 클릭은 같은 오래된 version으로 저장을 반복해 409가 누적됐고, 오류는 열린 판정 dialog 뒤 본문에 표시되어 사용자는 버튼이 반응하지 않는 것으로 인식했다.

검수 로그의 privacy-safe 집계는 응답 저장 `성공 6 / 충돌 49`, 최종 확정 `성공 1 / 입력 거절 4`, 미완료 요청과 품질 서버 예외 각 `0`이었다. DB report·attempt·work item·PDF 정합성 위반도 `0`으로, 데이터 손상이 아니라 확정 orchestration과 오류 UX 결함임을 확인했다.

## 요청별 구현 결과

1. `FinalizeQualityInspectionRequest`에 optional checklist 응답을 추가했다. 기존에 임시 저장을 끝낸 API client는 응답을 생략해도 호환된다.
2. Frontend 판정 확정은 별도 응답 저장 요청을 제거하고 현재 응답·판정·사유·Pending 담당을 한 번에 보낸다.
3. Backend는 report row를 잠그고 응답 validation, 기존 draft 응답 교체, snapshot, report/attempt/work item 갱신, Pending 또는 후속 인계를 같은 transaction에서 처리한다.
4. validation·CAS·담당자·인계 조건이 실패하면 응답과 version을 포함한 모든 write가 롤백된다.
5. 부적합 응답이 있으면 dialog가 부적합을 기본 선택하고 합격 버튼을 비활성화한다.
6. 필수 검사, 필수 측정값, 해당없음 사유, 부적합 항목 일치, 사진 또는 30자 근거, 조치 부서를 요청 전에 검사한다.
7. Backend field error의 첫 구체 메시지를 dialog 내부 `role=alert`에 표시한다. dialog는 닫히지 않으며 409에는 최신 검사 내용 다시 불러오기를 제공한다.
8. React disabled state와 별개로 synchronous in-flight ref를 두어 같은 tick의 빠른 중복 클릭도 한 요청으로 제한한다.

## 기술적 결정과 검토한 대안

- 채택: 확정 payload에 응답을 포함하고 Backend transaction 하나로 저장과 판정을 묶는다. Frontend에서 저장 성공 version만 갱신하는 최소 수정은 네트워크·검증 실패 때 부분 저장이 남는 문제를 유지하므로 폐기했다.
- 채택: 기존 `PUT /responses`는 현장 중간 임시저장용으로 유지한다. 확정만 원자 요청을 사용해 장시간 검사 기록 능력을 보존한다.
- 채택: 확정 payload가 제공되면 현재 Frontend draft를 authoritative snapshot으로 보고 기존 draft 응답을 transaction 안에서 교체한다. 사용자가 지운 선택값이 과거 응답으로 되살아나는 것을 막는다.
- 채택: 400은 현재 입력을 보존한 채 dialog에서 수정하고, 실제 동시 변경을 의미하는 409만 사용자가 명시적으로 최신 내용을 불러오게 한다.
- 보류: 일반 API client 전체의 request timeout 정책. 이번 검수 로그에는 미완료 요청이 없고 공통 API 정책 변경은 별도 범위다.

## 아키텍처와 영향

- Backend/API: finalize request의 optional `responses`, 기존 client 호환.
- Backend/DB: migration 없음. 기존 report row lock·transaction·operation ledger를 재사용한다.
- Frontend: 품질 판정 단일 요청, dialog-local validation/error/conflict recovery.
- Pending·알림·업무: 기존 finalize transaction 내부 writer를 그대로 사용하며 실패 시 함께 롤백한다.
- PDF·사진·첨부: 기존 snapshot과 PDF 생성 경로를 유지한다. 확정 성공 뒤에만 finalized snapshot을 생성한다.
- 권한: `Quality.Inspect`와 기존 project scope를 유지한다.
- 실제 provider·Persistent UAT: 변경하거나 호출하지 않았다.

## 변경 파일

- Backend: `backend/src/Emi.Qms.Api/QualityInspections/QualityInspectionContracts.cs`, `QualityInspectionStore.cs`
- Frontend: `frontend/src/QualityInspectionsPage.tsx`, `qualityInspections.ts`, `styles.css`
- Tests: `frontend/tests/QualityInspectionsPage.test.tsx`, `frontend/e2e/full-stack/quality-inspections.full-stack.spec.ts`
- Task·governance: `tasks/workflow-continuity-001-change-011.md`, 본 구현 보고, 사용자 검수 체크리스트, `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md`

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `QI-011-F01` | P1 | Resolved | 저장 성공 뒤 확정 거절이 server version만 증가시켜 다음 클릭이 계속 409였다. | 응답 저장과 판정을 한 finalize transaction으로 통합해 실패 write 전체를 롤백한다. |
| `QI-011-F02` | P1 | Resolved | 오류가 modal backdrop 뒤 본문에 표시돼 사용자가 원인을 볼 수 없었다. | 상세 field error를 열린 dialog의 `role=alert`에 표시한다. |
| `QI-011-F03` | P2 | Resolved | 중첩 save가 outer finalize의 loading state를 해제해 중복 클릭을 허용했다. | 별도 save를 제거하고 synchronous in-flight guard와 전체 dialog 잠금을 적용한다. |
| `QI-011-F04` | P2 | Resolved | Frontend가 부적합 응답과 합격 판정 등 Backend 불변조건을 사전 안내하지 않았다. | 판정 자동 선택·합격 차단과 동일 의미의 client validation을 추가한다. |
| `QI-011-F05` | P2 | Resolved | 자동 회귀가 정상 확정만 검증해 거절 뒤 재시도와 중복 클릭을 놓쳤다. | 단위 실패/재시도/더블클릭과 LQC·OQC transaction rollback Full-Stack 회귀를 추가한다. |

Open P0/P1/P2: `0/0/0`.

## 시행착오 및 폐기한 접근

- 처음에는 저장 성공 response의 version을 화면에 반영하는 Frontend-only 보정을 검토했다. 하지만 최종 판정 거절 전에 체크리스트 저장이 이미 commit되는 부분 성공을 남겨 원자적 사용자 동작이 아니므로 적용하지 않았다.
- 고정 검수 DB의 진행 중 OQC를 수정 재현에 사용하지 않았다. 실제 검수 데이터는 보존하고 같은 증상을 isolated PostgreSQL과 mock API에서 재현·검증했다.

## 검증

- Backend Release build: 경고 0, 오류 0.
- Backend 전체 회귀: `421/421` 통과.
- Frontend typecheck: 통과.
- Frontend 집중 unit: `2/2` 통과.
- Frontend 전체 unit: 19 files, `127/127` 통과.
- Frontend lint: error 0, 기존 `src/main.tsx` Fast Refresh warning 1.
- Frontend production build: 통과, 기존 500kB 초과 chunk warning 유지.
- Isolated Full-Stack 품질 회귀: 기존 제조/LQC/OQC·Aggregate Pending·FAT 병행과 Change 011 원자 확정 `4/4` 통과.
- Change 011 LQC·OQC 각각: 거절 후 report version 불변, 응답 row 0, 같은 version 수정 재시도 성공.
- 고정 검수 Frontend·Backend live/ready: HTTP 200.
- `git diff --check`: 통과.

## 개인정보·secret 검토

- runtime 로그는 status·aggregate count만 사용했고 request/response body, 사용자·프로젝트 식별자와 실제 업무 원문을 기록하지 않았다.
- 고정 검수 DB는 aggregate 정합성만 조회했으며 진행 중 OQC를 변경하지 않았다.
- 실제 provider·credential·Authorization header를 출력하거나 호출하지 않았다.

## 사용자 검수 결과와 남은 항목

- 자동 검증 완료.
- 사용자 검수 상태: `사용자 검수 대기`.
- 사용자는 고정 Frontend를 새로고침한 뒤 기존 진행 중 OQC에서 부적합 항목에 따라 dialog가 부적합을 기본 선택하는지 검수할 수 있다.
- 대표 repo·`main`·Persistent UAT·실제 provider 검증은 승인 범위 밖이다.

## Rollback·forward-fix

- migration이 없으므로 Change 011 코드·문서 commit을 revert하는 방식으로 rollback한다.
- 기존 finalized 성적서와 PDF는 변경하지 않는다.
- 사용자 검수에서 추가 결함이 발견되면 같은 Task의 다음 change로 forward-fix한다.

## 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 본 문서에 포함 | `기술적 결정`, `Rollback·forward-fix` |
| User manual | 본 문서·체크리스트에 포함 | `요청별 구현 결과`, Change 011 사용자 검수 체크리스트 |
| Roadmap update | 갱신됨 | `docs/00-product-roadmap.md` |
| User validation checklist | 작성됨 / 사용자 검수 대기 | `tasks/workflow-continuity-001-change-011-user-validation-checklist.md` |

## 변경·게시 경계

- local experiment 구현·검증과 사용자 요청에 따른 local commit만 수행한다.
- push·PR·대표 repo·`main`·Persistent UAT·실제 provider는 수행하지 않는다.
- `main` merge 승인: `0/3`.
