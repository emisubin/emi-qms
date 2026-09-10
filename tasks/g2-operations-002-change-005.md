# G2 Change 005 — 수리량과 불량재고, 홈 합계 펼치기

## 후속 수정 진행 — 2026-09-10

- 상태: 후속 수정 로컬 구현·집중 검증·독립 검토 완료, 검수 서버 반영. 이번 수정의 사용자 검수·원격 반영·공개배포는 미완료다.
- 사용자 승인: 9월 10일 재고부터 수리 반영, 9월 9일까지 기존 수식 보존. 불량재고 실사는 해당 날짜 마감값으로 확정하고 이후 신규 불량/수리를 누적하되 납품가능재고를 직접 변경하지 않는다. 사용자가 구현 및 최신 v2 규칙 적용을 명시 승인했다.
- 이해한 범위: 납품가능재고의 세 시기(8/27까지 당일 생산·불량·납품, 8/28~9/9 전일 생산·불량 및 당일 납품, 9/10부터 전일 수리 추가)를 서버 전체/부분 조회와 홈 임시 계산에서 일치시킨다. 아래 과거 계약의 8/28 수리 반영 문구는 배포 당시 이력이며 이번 승인으로 보정한다.
- 불량 실사: 기존 재고 실사 권한·과거/오늘 날짜·버전 충돌 보호를 재사용한다. 0도 유효하며 수정/삭제와 과거 입력 정정은 이후 불량재고 음수 여부를 원자적으로 검사한다. 실사일의 일일 입력 수량은 삭제하지 않는다.
- 기준선: 기존 task branch `codex/g2-repair-inventory`, HEAD `bf96c24`, clean. Root/Backend/Frontend/Scripts v2 및 검증/완료/증거/모델 정책을 재확인했다. 기본 clone WIP와 검수 DB는 보존한다. 기존 task를 재사용하며 신규 원격 게시·배포는 승인 범위에 없다.
- 검증 계획: 날짜 경계·실사·전체/부분 조회·과거 정정/경쟁/CAS/권한의 집중 테스트, 홈 임시값·실사 저장/삭제 UI, desktop/390px 합성 화면 및 독립 검토. 제품 전체 회귀는 사용자 검수 후 최종 병합 후보에서 수행한다.

### 후속 점검 기록

- readchk: 승인된 불량 마감 실사와 납품가능재고의 독립성을 확인했다. sip의 shower 냉독 결과에 따라 당일 불량/수리가 포함된 마감 수량, 삭제 후 이전 실사(없으면 0) 기준 재계산 안내를 보완했다.
- ssotize는 읽기 전용으로 날짜 문자열/수식 명칭 및 실사 필드/상수의 두 검색을 대조했다. 현재 계약은 이 후속 수정 절, 구현은 Backend 계산/시작잔액과 Frontend 임시 계산이 각각 소유한다. Change003·004와 기존 implementation-report는 역사 기록이며 재작성하지 않는다. 전역 문서 통합은 하지 않는다. 관리 화면의 날짜 제한 없는 수리 반영 설명은 승인된 9/10 기준으로 보정했다.
- mandela: 같은 잘못된 시작일을 구현과 테스트가 공유할 위험을 확인했다. 9/8의 재고 100, 생산20·불량3 및 9/9 납품4로 9/9 재고113; 9/9 생산10·수리6·불량2 및 9/10 납품6으로 9/10 재고121이라는 손계산 기대값을 사용한다. 단순 FE/BE 상호 일치만 성공 근거로 쓰지 않는다.
- re0는 이 진행 절의 현재 상태와 이전 완료 기록을 구분하는 데 한정했다. factchk/ detool은 외부 사실·도구 중립성 주장 작업이 아니므로 생략했다. 별도 요구사항/품질 reviewer는 코드와 테스트를 검토한다.
- aside-browser에 따라 사용자 승인으로 Aside CLI 설치 후 guide/repl 지침을 읽었다. 기존 검수 주소를 Aside로 열어 불량 실사 모달을 직접 확인했다. Aside의 직접 viewport 변경은 미지원이므로 모바일 검증은 기존 E2E의 390px 실제 브라우저를 사용한다. 합성 화면 증거는 비추적 경로에만 보관한다.

### 후속 수정 결과·재개

- 구현: 수리량의 납품가능재고 반영 시작일을 2026-09-10으로 제한했다. 서버 계산/부분 조회 시작잔액/홈 임시 계산을 함께 보정했다. 이전 8/28 시점 전환과 재고 실사 우선을 유지한다.
- 신규 `0092_g2_defect_inventory_counts.sql` 및 불량 마감 실사 PUT/DELETE, 기존 재고 관리 권한과 감사 대상 경로 등록을 추가했다. `0093_g2_defect_inventory_audit.sql`은 기존 공용 변경 이력 trigger를 연결한다. 실사·수리·일일 불량의 경쟁 입력은 동일 잠금으로 직렬화하며 불량재고 음수 변경은 전체 취소한다. 기준점별 구간 합산을 사용해 미래 달력 전체 생성과 날짜별 중복 누적 조회를 피했다.
- 홈 생산 현황 위 `불량재고 실사 입력`에서 날짜·마감 수량 입력, 기존 실사 수정·삭제가 가능하다. 합성 0대 저장/재조회/수정/삭제와 버전 충돌·권한 거부·미래 날짜 거부를 검증했다. 홈/월간 불량재고의 실사 표시는 공용 표를 사용한다.
- 검증: Release build warning/error 0; G2OperationsTests 17/17, 최종 isolated PostgreSqlMigrationTests.G2 12/12(10초), AuditInfrastructureTests 12/12, AuditMutationCoverageTests 4/4, G2Pages 23/23, 최종 full-stack G2 1/1(시나리오16초/전체23.2초) PASS. 관련 eslint/typecheck/build 및 diff check PASS. 기존 Vite 큰 chunk 안내만 남는다. 제품 전체 suite/원격 CI는 아직 실행하지 않았다.
- 발견·해결: 새 PUT/DELETE의 공통 감사 registry 등록 누락으로 최초 서버 기동이 실패했다. 두 경로를 감사 포함 목록에 추가한 뒤 재빌드·기동 단위검사·full-stack을 재실행해 통과했다. 제외 목록으로 우회하지 않았다.
- 추가 발견·해결: 새 테이블에 row 감사 trigger가 빠져 있었다. 이미 검수 DB에 적용된 `0092`는 원래 정의로 보존하고 추가 `0093`으로 보정했다. 실제 Insert/Update/Delete 3건의 감사 이벤트와 수량 기록, 새/기존 schema 적용을 검증했다. 늦은 변경은 최초 GO와 구분해 추가 독립 검토와 영향 테스트를 다시 통과했다. 검수 DB 초기화는 하지 않았다.
- 화면: full-stack 합성 `frontend/test-results/g2-operations/05-defect-count-dialog-mobile-390.png`, `06-defect-count-table-mobile-390.png`, `07-defect-count-dialog-desktop-1440.png` 및 Aside 검수 모달을 직접 열어 확인했다. 모바일 버튼·날짜/수량 입력·실사 표기·표 내부 가로 스크롤과 페이지 넘침 없음 확인. 실행 이미지/report는 commit하지 않는다.
- 독립 검토: 최종 요구사항 GO / 품질 GO, Open P0/P1/P2=0. 기준 bf96c245 대비 최초 누적 18개 파일 manifest digest `2fccf70419973bcc733ad94a630f7fd6bd2f225406baa7b2f7dae7994437fe65`와 추가 감사 변경을 검토했다. 추가 고정 hash: 0093 `4536f771825c0570372e7bc9e34531a0f4e4318cac4c67f6a8c4ebecc12b3093`, AuditInfrastructureTests `436184293115da59b5a9a557adb20b4f1083d55da7602c0046d7c29224304cbf`, PostgreSqlMigrationTests `a599f2dba4b513f80348b81a5ab500d02c5a154dc5e1f54ea8e318ad9a5f1bf9`. 이후 변경은 이 결과 기록뿐이다. 구현 요청 Sol High, reviewer GPT-6 High; 관측 모델은 모두 NOT_REPORTED. 동일 suite를 reviewer가 반복하지 않았다.
- 검수 runtime: 기존 5178 frontend 유지, 이 작업 소유 backend만 갱신(최종 session `29109`, 이전 `22040`·중간 `64187` 종료). `0092` 후 `0093`을 기존 합성 검수 DB에 추가 적용했으며 reset/reseed하지 않았다. 30일 원본 metric·기존 재고 실사 digest `c75ae015eb2eda0a4bbe8ed0bbb6ac3f97f7b6e9511ffa32cc497ee8cb06847d`가 최종 갱신 후에도 동일했다. 합성 9/9 재고56, 9/10 재고59로 손계산과 일치. 준비상태/홈 HTTP200.
- 서버 재개 보조파일 `/private/tmp/g2-repair-review-resume.sh`는 기존 전용 tmpfs DB 소유권을 검사하고 초기화 없이 실행한다. Frontend session77618은 유지한다. Aside는 task 소유 합성 검수 탭으로만 사용했고 공개 PMS 데이터 입력/배포는 하지 않았다.
- 다음: 사용자 검수 후 별도 원격 병합·배포 승인 시 최신 main 및 migration 번호 충돌을 재확인한다. 이번 결과는 local commit만 보존한다. 원격 branch/main·운영 DB·공개배포는 미변경이며 이전 배포 완료 승인을 재사용하지 않는다.

## 이전 구현·게시 이력

- 상태: 구현·사용자 검수·전체 회귀·원격 main 병합·Azure 공개배포 완료
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

## 검수 후 입력 화면 보정 — 2026-09-10

- 사용자 확인 후 수정 승인: 불량 입력 명칭을 `일일 불량 수량`으로 변경하고 오전 생산/수리 → 오후 생산/수리 → 일일 납품/불량 순으로 배치했다. 2열 desktop과 1열 mobile의 DOM/키보드 순서를 동일하게 유지했다.
- 월간 입력 현황도 홈 합계 컴포넌트를 재사용해 생산·수리·납품·불량 및 재고를 먼저 표시한다. 헤더/숫자 클릭 시 상세가 펼쳐지고 월간 상세는 읽기 전용이다. 날짜 필터, 예상/휴일 색상, 기존 수량과 저장/권한/API 계약은 보존했다.
- G2Pages 19/19 PASS: 입력 순서·명칭·불량 저장·월간 4개 펼침/접힘·상세 읽기 전용·기간 필터 검증. typecheck 및 변경 파일 eslint PASS. 테스트 옵션의 타입 오류 1건은 지원하지 않는 `exact` 제거로 수정 후 통과했다.
- 기존 합성 검수 runtime에서 실제 입력 배치와 수리/불량 상세를 직접 확인했다. desktop 및 390px에서 페이지 가로 넘침이 없고, 상세 입력창 없이 저장된 수량만 표시됨을 확인했다. 작은 가역 Frontend 표시 변경으로 직접 검토/집중 검증을 적용했고 Backend/DB/전체 회귀는 반복하지 않았다.
- 공개배포/운영 데이터 변경 없음. 같은 branch에 local commit으로 보존하며 사용자 검수 대기를 유지한다.

## 사용자 검수 수락·게시 승인 — 2026-09-10

- 입력 화면 보정 후 사용자가 `원격메인에 병합하고 공개배포까지 완료해`라고 요청했다. 현재 G2 수리·불량재고 및 월간 표/입력 배치 결과의 사용자 검수 수락과 해당 main 병합·Azure 공개배포 실행 승인으로 기록한다.
- 게시 후보는 `06d2aab`, `8edc6a0` 및 이 승인 기록이다. 원격 main 기준 `0a12b819cc7622b8afc55ae485c420f8f01b2ad9` 이후 타 변경이 없음을 fetch로 확인했다. PR의 required CI가 Backend/Frontend/일반 및 사업부 전용 full-stack 전체 회귀를 책임 실행한다.
- 운영 변경은 기존 Azure 수동 release workflow의 exact main SHA, additive migration `0091`, Backend/Frontend 이미지 교체와 기존 보안/인증/provider 설정 보존으로 제한한다. DB reset·bootstrap·membership backfill·시험 알림 발송은 승인 범위에 포함하지 않는다.

## 게시·배포 결과 — 2026-09-10

- PR #131: https://github.com/emisubin/emi-qms/pull/131, squash main `977fb35301446813394dccda8421c3e998fe0d8b`, merged `2026-09-10T01:34:35Z`.
- PR CI `34423812056`: 모든 필수 항목 PASS. Backend 602/602, Frontend 41개 test file, mock browser 14/14, 일반 full-stack 64/64, 사업부 접근 1/1, 오산 등록 1/1 통과. Main CI `34426028588`도 성공했다.
- Azure manual release `34426075806`: https://github.com/emisubin/emi-qms/actions/runs/34426075806. Exact source는 위 merge SHA. Migration/Backend/Frontend/PublicSecurity 모두 PASS (`2026-09-10T01:43:50Z`). DB bootstrap/membership backfill/inspection은 모두 SKIPPED이며 실제 실행하지 않았다.
- 배포 후 공개 HTTP 직접 확인: `/health/live` 200, 익명 `/` 401, 익명 `/api/me` 401. 기존 인증 경계를 유지했다. 이번 turn에서는 인증된 운영 계정의 G2 입력/수정 smoke는 하지 않아 운영 업무 데이터 mutation이 없다.
- 운영 배포 결과는 PR #131 댓글에도 남긴다. 이 사후 결과 기록은 local branch commit으로 보존하며 제품 source 추가 변경·추가 배포는 없다. 검수용 5178/5088과 합성 DB는 기존 상태로 유지한다.
