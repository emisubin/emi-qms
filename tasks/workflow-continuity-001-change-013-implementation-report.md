# TASK-WORKFLOW-CONTINUITY-001 Change 013 구현 보고 — 품질 재검사 최종 합성·물류 1회 확정

## 해결한 업무 문제

LQC·OQC 재검사는 부적합 항목만 다시 검사하도록 정상 제한됐지만, 재검사 합격 뒤 최종 조회에도 그 제한이 남아 최초 검사에서 이미 적합했던 항목이 보이지 않았다. 그 결과 프로젝트 상세 품질 탭과 품질 검사 화면이 전체 OQC 결과 대신 마지막 재검사 1항목만 표시했다.

포장·출발·납품은 대상 선택 뒤 draft 생성, 증빙 등록, 최종 확정을 각각 눌러야 했다. draft를 만든 뒤 다른 화면으로 이동하면 원래 대기 대상은 queue에서 빠지고 URL에만 복구 정보가 남아 작업이 삭제된 것처럼 보였다. 물류 화면은 누적 디자인과 export checkbox 배치가 맞지 않아 목록 폭과 모바일 정보 구조도 불안정했다.

## 요청별 구현 결과

1. LQC·OQC 재검사 진행 중에는 기존대로 직전 부적합 항목만 표시·저장·확정할 수 있다.
2. 재검사가 합격으로 최종 확정되면 같은 Pending 검사 계보의 전체 양식을 복원하고, 항목별 가장 최근 확정 응답을 최종 유효 결과로 합성한다.
3. 최초 검사에서 적합했던 항목은 유지되고, 부적합이었던 항목은 재검사 적합 응답으로 교체되어 한 화면에 함께 표시된다.
4. 같은 항목을 여러 번 재검사해도 가장 최근 확정 응답을 표시하며 각 attempt·report 원본과 사진은 변경하거나 삭제하지 않는다.
5. 프로젝트 상세 품질 진행률은 합성된 전체 항목을 사용하므로 마지막 재검사만 `1/1`로 축소되지 않는다.
6. 포장·출발·납품 입력폼에 필수 증빙 선택을 처음부터 배치했다. 대상·증빙을 준비한 뒤 `저장 및 확정` 한 번으로 draft 생성→증빙 등록→단계 확정을 연속 처리한다.
7. 저장 도중 실패해 draft가 남으면 해당 draft를 즉시 다시 불러오고 URL을 유지해 같은 화면에서 재시도하거나 취소할 수 있다.
8. 물류 queue가 현재 사용자가 이어서 처리할 수 있는 미완료 draft 목록을 반환한다. URL에 draft ID가 없어도 최신 미완료 draft를 자동 복구한다.
9. 고정 검수 DB에서 사라진 것으로 보였던 출발 작업은 삭제가 아니라 증빙 0개인 draft 1건으로 확인됐고, 실제 화면에서 `복구됨` 상태와 비활성 저장 버튼으로 다시 표시된다.
10. 물류 화면은 블랙앤화이트·사각형 공통 방향으로 정리하고 export checkbox와 대상 카드의 grid 폭, 모바일 KPI 2열과 안내 전폭 배치를 고정했다.

## 기술적 결정과 검토한 대안

- 채택: 재검사 write scope와 final read projection을 분리했다. 재검사 중에는 부적합 항목만 유지하고, `Finalized + Passed`에서만 전체 유효 결과를 합성한다.
- 채택: 유효 응답은 동일 Pending·동일 stage의 finalized report를 `item_code`로 현재 양식에 매핑하고 attempt 번호가 가장 큰 응답을 사용한다. report 원본을 덮어쓰는 방식은 감사 이력을 훼손하므로 적용하지 않았다.
- 채택: 최종 조회의 사진은 같은 검사 계보의 확정 report 사진을 전체 항목에 다시 매핑한다.
- 채택: 물류 Backend의 기존 CAS·멱등 endpoint와 draft 지속성을 유지하고 Frontend가 한 사용자 행동 안에서 create→evidence→finalize를 직렬 실행한다. 중간 실패는 draft를 숨기지 않고 자동 복구한다.
- 채택: queue 응답에 복구 가능한 draft summary를 추가했다. local storage만으로 복구하는 안은 다른 기기·새 창·브라우저 초기화에서 복구되지 않으므로 사용하지 않았다.
- 채택: 물류 상태·오류 외 색을 제거하고 단색 사각형 구조로 맞췄다. 인앱 브라우저 데스크톱·390px 검수에서 overflow와 console error를 함께 확인했다.

## 아키텍처와 영향

- Quality: 재검사 완료 상세의 전체 양식·항목별 최신 확정 응답·사진 합성 projection을 추가했다.
- Logistics Backend: queue에 현재 사용자가 이어서 처리할 수 있는 stage별 draft summary를 추가했다.
- Logistics Frontend: 증빙 선첨부·단일 저장 확정, 중간 실패 복구, URL 없는 draft 자동 복구로 입력 상태를 단순화했다.
- Project detail: 기존 품질 탭 계산 코드를 바꾸지 않고 Backend 전체 결과 projection을 사용해 진행률이 정상화된다.
- DB schema·migration: 변경 없음.
- 권한·CAS·멱등성·Pending 차단·증빙 형식과 다음 단계 인계: 기존 계약 유지.
- 실제 provider·Persistent UAT·대표 repo·`main`: 변경하거나 호출하지 않았다.

## 변경 파일

- Backend: `LogisticsContracts.cs`, `LogisticsStore.cs`, `QualityInspectionStore.cs`
- Frontend: `LogisticsPage.tsx`, `QualityInspectionsPage.tsx`, `logistics.ts`, `styles.css`
- Tests: `ProcurementApiTests.cs`, `ProjectRegistrationApiTests.cs`, `App.test.tsx`, `LogisticsPage.test.tsx`, `logistics-execution.full-stack.spec.ts`, `quality-inspections.full-stack.spec.ts`
- Task·governance: Change 013 계약, 본 구현 보고, 사용자 검수 체크리스트, Product Roadmap, 실험 완료 원장

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `WF-013-F01` | P1 | Resolved | 재검사 편집용 항목 필터가 합격 확정 뒤 최종 조회에도 적용돼 최초 적합 항목이 숨고 OQC가 `1/1`로 축소됐다. | 합격 완료 재검사는 전체 양식과 동일 Pending 계보의 항목별 최신 finalized 응답을 합성한다. |
| `WF-013-F02` | P1 | Resolved | 물류 draft가 대기 queue에서 대상을 점유한 뒤 URL을 잃으면 다시 찾을 수 없어 작업이 사라진 것처럼 보였다. | queue에 복구 가능 draft summary를 추가하고 URL 없이도 자동 복구한다. |
| `WF-013-F03` | P1 | Resolved | 대상 시작·증빙 등록·확정이 세 번의 분리된 사용자 행동이라 화면 이탈과 미확정 draft를 만들기 쉬웠다. | 필수 증빙을 처음부터 받고 한 번의 저장 행동으로 세 server 단계를 직렬 완료하며 중간 실패 draft를 재시도 가능 상태로 보존한다. |
| `WF-013-F04` | P2 | Resolved | 물류 대상 카드와 선택 Excel checkbox wrapper에 폭 계약이 없고 모바일 KPI가 3열이라 좁은 화면 정렬이 깨졌다. | 대상 행 grid·카드 100% 폭, 모바일 KPI 2열·안내 전폭, 단색 사각형 토큰을 적용했다. |
| `WF-013-F05` | P2 | Resolved | 물류 확정 성공 뒤 route reload가 성공 feedback을 바로 초기화해 사용자가 확정 여부를 확인하기 어려웠다. | 성공 feedback을 queue reload에서 유지하고 다음 직접 행동 때만 초기화한다. |

Open P0/P1/P2: `0/0/0`.

## 검증

- Backend Release build: 경고 0, 오류 0.
- Backend 집중 회귀: LQC·OQC 재검사 2건과 물류 실행 1건, `3/3` 통과.
- Backend 전체 회귀: `423/423` 통과.
- Frontend typecheck: 통과.
- Frontend 집중 unit: Logistics·Quality 2 files, `5/5` 통과.
- Frontend 전체 unit: 19 files, `128/128` 통과.
- Frontend lint: error 0, 기존 `src/main.tsx` Fast Refresh warning 1.
- Frontend production build: 통과, 기존 500kB 초과 chunk warning 유지.
- Isolated Full-Stack 물류: 포장·출발·납품 증빙 선첨부 1회 확정, URL 없는 draft 자동 복구, 패널별 정산 인계 `1/1` 통과.
- Isolated Full-Stack 품질: 제조/LQC/OQC 병행, Aggregate Pending, OQC·FAT 병행, 원자 확정, 재검사 전체 결과 합성 `5/5` 통과.
- 고정 검수 완료 OQC 비식별 projection: `Finalized / Passed`, 전체 항목 5, 유효 응답 4, 재검사 대상 1, 응답 적합성 `true`.
- 고정 검수 물류 비식별 projection: 출발 queue 0, 복구 draft 1, 증빙 0. 화면에서 선택 대상 1·`복구됨`·증빙 필수·저장 비활성 상태 확인.
- 인앱 브라우저 visual: desktop·390px horizontal overflow 0, action panel 표시, mobile 단일 column, console error 0.
- 고정 runtime: Frontend root HTTP 200, Backend `/health/ready` status `ok`, database reachable.
- Full-Stack E2E 임시 PostgreSQL·network: 각 실행 뒤 제거 확인.
- `git diff --check`: 통과.

분리 Codex 검증 session은 현재 사용자 요청 없는 agent 생성을 금지하는 실행 규칙 때문에 만들지 않았다. 대신 최종 diff·품질 원본 보존·물류 권한·CAS·중간 실패 복구·고정 runtime projection을 같은 session에서 read-only 재검토했다.

## 개인정보·secret 검토

- 고정 runtime 검증은 단계, 건수, 항목 수와 boolean만 출력했다.
- 보고서에는 실제 프로젝트명·고객명·사용자명·UUID·업무 원문·증빙 파일명을 기록하지 않았다.
- 실제 출발 draft를 확정·취소하거나 증빙을 업로드하지 않았다.
- 실제 Teams·메일 provider를 호출하지 않았다.

## 사용자 검수 결과와 남은 항목

- 자동 검증과 고정 runtime 반영 완료.
- 사용자 검수 상태: `사용자 검수 대기`.
- 현재 복구된 출발 draft는 사용자가 증빙을 첨부하고 직접 확정할 수 있도록 그대로 보존했다.
- 대표 repo·`main`·Persistent UAT·실제 provider 검증은 승인 범위 밖이다.

## Rollback·forward-fix

- 코드·문서 변경은 Change 013 변경분을 되돌린다.
- DB schema 변경이 없어 migration rollback은 없다.
- 기존 검사 attempt·report·응답·사진과 물류 draft·증빙은 삭제하거나 변환하지 않는다.
- queue `drafts` 필드를 되돌릴 때는 Frontend 자동 복구 참조도 함께 되돌린다.

## 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 본 문서에 포함 | `기술적 결정`, `Rollback·forward-fix` |
| User manual | 본 문서·체크리스트에 포함 | `요청별 구현 결과`, Change 013 사용자 검수 체크리스트 |
| Roadmap update | 갱신됨 | `docs/00-product-roadmap.md` |
| User validation checklist | 작성됨 / 사용자 검수 대기 | `tasks/workflow-continuity-001-change-013-user-validation-checklist.md` |

## 변경·게시 경계

- local experiment 구현·검증과 고정 검수 runtime의 read-only 확인만 수행했다.
- 사용자가 이번 요청에서 commit을 지시하지 않아 변경은 미커밋 상태다.
- push·PR·대표 repo·`main`·Persistent UAT·실제 provider는 수행하지 않았다.
- `main` merge 승인: `0/3`.
