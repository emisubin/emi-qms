# G2 Change 005 — 수리량과 불량재고, 홈 합계 펼치기

- 상태: 구현·집중 검증·독립 검토 완료 / 사용자 검수 대기 / 원격 미게시
- 승인: 2026-09-10 사용자가 신규 불량의 1회 차감 수식을 확인하고 구현·검수 화면 제공을 요청했다.
- 기준: origin/main `0a12b819cc7622b8afc55ae485c420f8f01b2ad9`, branch `codex/g2-repair-inventory`
- 작업공간: `/private/tmp/emi-qms-g2-repair-20260910`. 기존 clone의 WIP와 실행환경을 보존하고 별도 검수 runtime을 제공하기 위한 임시 격리. 검수 종료 후 정리는 별도 요청 시 수행.
- 적용 지침: 최신 main 하네스 v2 Root/Backend/Frontend/Scripts 및 검증·완료·증거 정책. GPT-6 직접 구현.

## 계약과 완료 조건

- 생산/출하 관리에 오전 수리·오후 수리를 각각 또는 동시에 저장. 기존 생산 권한, 필드별 version/CAS, 빈 값/0, 미래 예상값 만료를 재사용한다.
- 불량은 일별 신규 발생 수량이다. 불량재고는 누적 신규 불량에서 누적 수리 완료량을 뺀 그날 종료 시점 수량이다. 기존 기록 전체를 계산하며 초기값은 0이다. 납품가능재고 실사는 불량재고를 초기화하지 않는다.
- 2026-08-28부터 납품가능재고(D) = 재고(D-1) + 생산(D-1) + 수리(D-1) - 신규불량(D-1) - 납품(D). 누적 불량재고를 반복 차감하지 않는다. 이전 날짜의 기존 시점과 실사 우선 규칙은 보존한다.
- 수리는 누적 발생 불량을 초과해 완료 처리할 수 없다. 과거 정정도 이후 날짜의 불량재고를 음수로 만드는 경우 거부하고 원본을 보존한다.
- 홈 생산 현황표는 생산·수리·납품·불량 합계 행이 기본이며 행의 헤더/숫자를 클릭하거나 키보드로 상세를 펼친다. 생산/수리 상세는 오전·오후, 납품 상세는 일일 납품 입력·목표, 불량 상세는 신규 불량 입력·불량재고다. 기존 납품가능재고 행과 목표 입력은 유지한다.
- 홈 상세 입력은 메모리 내 시뮬레이션이며 표·그래프·KPI를 즉시 갱신한다. 새로고침/재조회/초기화로 폐기한다.
- desktop/390px에서 새 입력·펼침·가독성과 overflow를 확인하고 실제 저장/재조회·CAS·권한·미래 만료·월 경계·실사·불량 중복 차감 방지를 검증한다.

## 변경 범위와 검증

기존 G2 계약·store·계산·endpoint와 additive migration, Frontend G2 type/관리/홈/preview, 관련 unit·PostgreSQL·browser 검증을 변경한다. 운영 데이터/배포/원격 게시 요청은 이번 범위에 없다. 그래프 디자인·출근·타 업무는 유지한다.

## 구현과 검증 결과

- Backend: 오전/오후 수리 metric과 API request/response, additive `0091_g2_repairs.sql`을 추가했다. 제조/영업의 기존 생산 입력 권한, 필드별 CAS, 미래 예상 만료를 재사용한다. 수리/신규불량 저장은 전 기간 불량재고의 음수를 검사하고 실패 시 transaction 전체를 되돌린다. 일별 수량·실사 저장 및 조회가 같은 잠금 순서를 사용해 경쟁 입력과 읽기 중 재고 기준 혼합을 방지한다.
- Frontend: 생산/출하 관리의 수리 입력·불량재고, 홈의 4개 합계 펼침·임시 수리 입력을 구현했다. 생산 그래프의 생산량에는 수리량을 섞지 않고 납품가능재고 계산에만 반영한다. 출근·그래프 디자인은 변경하지 않았다.
- 예상값 주의: 예정일이 된 불량 예상은 기존 계약대로 삭제된다. 그 불량을 전제로 입력했던 이후 날짜의 수리 예상이 남으면 미래 불량재고가 일시적으로 음수로 표시될 수 있다. 화면에 경고하며, 불량/수리 정정은 음수가 해소돼야 저장된다. 관련 없는 생산/납품 저장을 차단하지 않는다. 홈 임시 시뮬레이션도 음수 경고를 표시하지만 저장하지 않는다.
- Release solution build: PASS, warning/error 0. G2OperationsTests: 15/15 PASS.
- `scripts/e2e-backend-tests.sh --no-build --filter FullyQualifiedName~PostgreSqlMigrationTests.G2`: 7/7 PASS, skip 0. 새 DB/기존 schema migration, 월·전환 경계, full/partial/day 계산, 수리 초과·과거 정정 rollback·동시 수리·예상값 만료와 CAS를 실제 일회용 PostgreSQL에서 검증했다.
- `vitest run tests/G2Pages.test.tsx`: 19/19 PASS. 관련 eslint, typecheck, production build PASS. Vite의 기존 대형 chunk 안내만 남는다.
- `scripts/e2e-full-stack.sh g2-operations.full-stack.spec.ts`: 최종 1/1 PASS (14.1초). 실제 API 저장·재조회·권한·CAS·다음 날 재고·미저장 preview·키보드 펼침·1440/390px·overflow·browser console error 0을 확인했다. 각 실행 소유 DB/container/network는 harness가 정리했다.
- 합성 screenshot `frontend/test-results/g2-operations/01-g2-home-desktop-1440.png`, `02-g2-operations-mobile-390.png`, `03-g2-home-mobile-390.png`을 직접 열어 확인했다. 실행 screenshot/report는 Git에 넣지 않는다.
- P2 `G2-REPAIR-ROW-KEY`: 합계와 상세의 React key 충돌로 반복 펼침 시 불량 행이 중복됐다. `summary-` prefix 분리, 3회 반복 토글 행 개수 검사와 console 오류 검사로 보정했다. 최종 모바일 screenshot에 중복이 없음을 재확인했다.
- 별도 reviewer의 요구사항·품질/회귀·DB 검토: local GO, Open P0/P1/P2=0. 전체 allowlist와 보정분을 확인했으며 동일 suite를 재실행하지 않았다. 구현 위임 요청 Sol High, 독립 검토 요청 GPT-6 High; actual model은 도구가 보고하지 않아 둘 다 `NOT_REPORTED`다.
- 전체 제품 회귀/사용자 검수/원격 반영/공개배포는 이번 집중 검증과 구분하며 미실행이다.

## 검수 환경과 재개

- URL: `http://127.0.0.1:5178/g2`, 관리 입력 `http://127.0.0.1:5178/g2/operations`. Backend `http://127.0.0.1:5088`, readiness 및 frontend HTTP 200 확인. 앱 브라우저에서 실제 홈과 수리 상세를 열어 확인했다.
- Source: 이 change의 임시 worktree/branch. Backend Release build, Vite Dev, 개발용 영업 계정. 외부 provider 모두 disabled/dry-run.
- 소유 container: `emi-qms-e2e-g2-repair-review-20260910-e2e-postgres-1`; DB: `emi_qms_e2e_g2_repair_review_20260910`; tmpfs 저장소, 영구 volume 없음. 운영/Persistent UAT와 연결하지 않는다.
- 2026년 9월 30일분 합성 생산/수리/납품/불량과 목표/실사 예시를 API로 입력했다. 운영 데이터 복사·변경 없음. 출근 예시 데이터는 입력하지 않았다. tmpfs라 container 종료 시 데이터가 유실될 수 있다.
- 실행 session: Backend `22040`, Frontend `77618`. 시작/예시 입력 보조 파일은 Task 소유 `/private/tmp/g2-repair-review-start.sh`, `/private/tmp/g2-repair-review-seed.mjs`이며 기존 자원을 재설정하지 않도록 방어한다. 검수 후 종료/정리는 별도 요청에서 ownership을 확인하고 진행한다.
- 사용자 확인 항목: 생산/수리/납품/불량 합계의 헤더와 숫자를 눌러 펼침, 홈 임시 입력의 즉시 재고 반영과 초기화, 생산/출하 관리에서 오전·오후 수리 저장 후 재조회.
- Git: 승인된 범위만 local commit 대상으로 확인했다. push/PR/merge/Azure 배포는 하지 않는다. 되돌리기는 해당 local 변경 revert와 검수 runtime 종료로 제한하고 기존 데이터/migration을 임의 삭제하지 않는다.
