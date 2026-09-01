# TASK-G2-OPERATIONS-002 — 납품 목표·불량·홈 임시 시뮬레이션 Implementation Report

## 1. 상태와 기준선

| 항목 | 결과 |
| --- | --- |
| Task | `TASK-G2-OPERATIONS-002` |
| Task 유형 | `NEW_FEATURE` — 사용자 승인에 따른 Codex 직접 기획·구현 |
| Branch | `feat/task-g2-operations-002-daily-input` |
| 기준 SHA | 최신 `origin/main` `3590c27b7281e77199c8773495e0bafe6311517a` |
| 검증 worktree | `/private/tmp/emi-qms-g2-operations-002.KbYEfk` |
| 구현 결과 식별 | 기준 SHA 대비 Task diff. 최종 commit·merge SHA는 연계 PR과 종료 handoff를 canonical source로 사용 |
| 구현 기준 | [Task Identity Gate](g2-operations-002-identity-gate.md), [Codex 기획안](g2-operations-002-codex-planning.md), [Codex review](g2-operations-002-codex-review.md) |
| 자동 검증 | 완료 |
| 사용자 검수 | `완료` — 격리 검수 서버 확인 후 Change 001 반영·원격 main 병합 승인 |
| Commit / Push / PR / Merge | 사용자 승인 완료. Ready PR·필수 CI·main merge 절차로 실행 |
| Persistent UAT / Azure | 미실행·별도 승인 대기 |

이 보고서는 격리된 local feature worktree의 구현과 자동 검증만 기록한다. 운영 DB, 기존 검수 runtime, 실제 provider와 Azure 공개 서비스는 변경하지 않았다.

## 2. 해결한 업무 문제와 범위

G2 일일 운영에서 납품 목표와 불량 수량을 정식 데이터로 관리하고, 홈에서는 저장 없이 생산·납품·불량 숫자를 바꿔 그래프와 예상 재고를 즉시 비교할 수 있게 했다.

포함 범위는 다음과 같다.

- 적용 시작일별 정식 `납품 목표`
- 날짜별 정식 `불량` 수량과 제조·영업·System Administrator 변경 권한
- `전일 재고 + 오전 생산 + 오후 생산 - 납품 - 불량` 자동 재고
- 미래 불량의 기존 예상값 날짜 도래 초기화 계약 재사용
- 홈 생산 현황표의 저장 없는 임시 입력, 즉시 그래프·KPI·재고 반영과 초기화
- 첫 그래프의 주황 점선 납품 목표와 hover 정보
- 생산 현황표를 첫 그래프 바로 아래로 이동하고 `납품 목표·불량` 행 추가
- 예상 열 파란 글자, 휴일 열 빨간 글자 우선
- 홈·생산/출하·출근 관리 모바일 날짜 입력 폭 축소

제외 범위는 홈 임시값 저장·공유·감사, 불량 사유·유형·품목 연결, 납품 목표 달성률, 관리자 통합 입력/수정 이력, 손익관리, Excel/PDF/첨부/알림, 실데이터 입력과 운영 배포다.

## 3. 아키텍처와 기술적 결정

### Database와 migration

`database/migrations/0084_g2_delivery_target_defect.sql`은 기존 테이블을 재사용하며 check constraint 허용값만 확장한다.

- `g2_daily_metrics.metric_code`에 `Defect` 추가
- `g2_targets.target_type`에 `Delivery` 추가
- 기존 row, version, 수정자·시각, audit trigger와 permission seed는 변경하지 않음
- 새 table·permission·data backfill 없음

Migration은 데이터 삭제나 변환이 없는 additive forward migration이다. 적용된 migration 파일은 수정하지 않으며 결함이 있으면 다음 번호의 forward-fix를 사용한다.

### Backend, API와 권한

기존 `/api/g2/operations/{date}` 계약에 nullable `defect` 변경을 추가했다. 불량은 생산과 역할 범위가 같아 새 permission을 만들지 않고 `G2.Production.Update`를 재사용한다. 요청에 허용되지 않은 생산·불량 field가 섞이면 저장 전에 전체 `403`으로 거부하며 기존 metric별 advisory lock·expected version·단일 transaction을 유지한다.

납품 목표는 기존 `/api/g2/targets/{targetType}/{effectiveDate}`와 `G2.Target.Manage`를 재사용한다. 조회 시 적용 시작일별 목표를 날짜별로 확장해 `deliveryTarget`으로 반환한다.

재고 계산과 조회 시작일 이전 balance SQL 모두 불량을 차감한다. 실사 날짜는 기존처럼 실사 수량이 우선하고 이전 날짜의 생산·납품·불량 정정은 다음 실사 경계를 넘지 않는다. 미래 불량은 기존 `is_forecast` lifecycle에 포함된다. G2 범위 조회 또는 일일 저장 요청이 실행될 때 서울 기준 `work_date <= today`인 예상 metric을 `quantity=null`, `is_forecast=false`로 바꾸고 version을 증가시킨다. 따라서 날짜 도래 후 첫 G2 조회에서 예상 표시는 사라지고 빈 값으로 보인다.

### Frontend와 UI·UX

홈은 서버 응답을 기준값으로 두고 별도 `G2HomePreview` 파생 모델에서만 임시 입력을 계산한다. 오전 생산·오후 생산·납품·불량을 바꾸면 생산 합계, 두 그래프, KPI와 실사 경계 기반 재고가 즉시 갱신된다. 저장 API나 mutation button은 없으며 월 이동·새 조회·새로고침·`임시값 초기화`로 폐기된다.

생산 현황표는 첫 그래프 다음에 배치하고 행을 `오전 생산 → 오후 생산 → 생산 합계 → 납품 목표 → 납품 → 불량 → 재고`로 고정했다. 납품 목표는 파스텔 주황 점선과 hover로 첫 그래프에 표시한다. 생산/출하 관리에는 불량 입력과 월간 납품 목표·불량 행을 추가했다.

미래 예상 열은 입력·합계·강조행·출근 합계 button까지 파란색으로 표시하고, 주말·공휴일 열은 빨간색을 최종 우선한다. Graphite의 마지막 공통 monochrome cascade에도 같은 semantic 예외를 추가했다.

사용자 검수 Change 001로 홈 생산 현황표의 직접 입력 박스 자체도 날짜 셀 가운데에 정렬하고, 숫자의 시각적 중심을 밀어내던 browser 기본 증감 버튼을 이 입력칸에서만 숨겼다. `number` 입력·0 이상 정수 검증과 예상·휴일 semantic 색상 우선순위는 그대로 유지한다.

390px 이하에서는 G2 시작일·종료일 입력을 각 `116px`, 목표 적용 시작일을 `132px`로 제한한다. 표와 그래프의 기존 내부 가로 탐색은 유지하고 페이지 전체 overflow를 만들지 않는다.

### 기술적 결정과 검토한 대안

- 불량 전용 table·permission은 날짜별 숫자 한 개와 동일 역할 matrix에 과도하므로 기존 metric·production permission을 재사용했다.
- 홈 임시값을 서버에 저장하는 방식은 정식 예상값·감사 의미와 충돌하므로 브라우저 파생 상태만 사용했다.
- 불량을 실사 날짜에도 다시 차감하는 방식은 확인된 실사 수량을 훼손하므로 기존 checkpoint 우선 규칙을 유지했다.
- 납품 목표 KPI·달성률은 기간·분모 정책이 정해지지 않아 이번 범위에서 제외했다.

## 4. 실제 변경 파일

| 영역 | 파일과 역할 |
| --- | --- |
| Migration | `database/migrations/0084_g2_delivery_target_defect.sql` — Defect·Delivery target 허용값 확장 |
| Backend | `backend/src/Emi.Qms.Api/G2/G2Contracts.cs`, `G2InventoryCalculator.cs`, `G2OperationsEndpointExtensions.cs`, `G2OperationsStore.cs` — 계약·권한·저장·목표 확장·재고 계산 |
| Backend 검증 | `backend/tests/Emi.Qms.Api.Tests/G2OperationsTests.cs`, `PostgreSqlMigrationTests.cs` — 불량 계산·권한·forecast expiry·fresh/existing migration |
| Frontend | `frontend/src/g2.ts`, `api.ts`, `G2HomePage.tsx`, `G2HomePreview.ts`, `G2ManagementPages.tsx`, `G2Charts.tsx`, `G2DataViews.tsx` — type/API·홈 임시 계산·관리 입력·그래프·표 |
| Frontend style | `frontend/src/styles.css`, `frontend/src/design-system/wireframe.css` — 납품 목표·예상/휴일 semantic 색과 모바일 날짜 폭 |
| Frontend 검증 | `frontend/tests/G2Pages.test.tsx`, `frontend/e2e/full-stack/g2-operations.full-stack.spec.ts` |
| 종료 산출물 | `tasks/g2-operations-002-*`, `docs/00-product-roadmap.md`, synthetic screenshot 4개 |

Excel/PDF/첨부파일 영향은 `N/A`다. 기존 프로젝트·생산계획·물류·근태 workflow와 외부 provider도 변경하지 않았다.

화면 route는 G2 홈 `/g2`, 생산/출하 관리 `/g2/operations`, 제조 인원 출근 관리 `/g2/attendance`다. 사용자 검수를 위해 별도 DB·포트의 격리 server를 열었고, synthetic 자료와 Change 001 직접 입력 중앙 정렬·증감 버튼 제거를 사용자가 확인했다. 이 local runtime은 운영 배포가 아니다.

## 5. 실행한 검증과 결과

| 검증 | 적용 | 결과 | 근거·미실행 이유 |
| --- | --- | --- | --- |
| Backend Release build | 적용 | 통과, warning/error `0/0` | `dotnet build backend/Emi.Qms.sln --configuration Release` |
| G2 Backend 영향 검사 | 적용 | `10/10` 통과 | 계산·실사 경계·생산/납품/불량 역할 거부 |
| Backend 전체·migration 회귀 | 적용 | `568/568` 통과 | isolated PostgreSQL 전체 suite, fresh·기존 `0081` 기준 forward apply 포함 |
| Frontend lint | 적용 | error `0`, 기존 `main.tsx` Fast Refresh warning `1` | 새 Fast Refresh warning은 helper 파일 분리로 제거 |
| Frontend typecheck | 적용 | 통과 | `tsc -b --noEmit` |
| Frontend 전체 unit | 적용 | 32 files, `241/241` 통과 | 홈 임시 재고·저장 없음·표 순서·불량 저장·목표 hover 포함 |
| Frontend production build | 적용 | 통과 | 기존 대형 chunk warning만 유지 |
| Isolated Full-Stack | 적용 | `1/1` 통과 | disposable PostgreSQL, 실제 HTTP·Chromium, 권한·CAS·불량 차감·납품 목표·임시값 폐기 |
| Desktop·mobile visual QA | 적용 | 통과 | synthetic 1440px·390px, date input width, 예상 파랑·휴일 빨강, page overflow `0` |
| Migration catalog | 적용 | 통과 | `0084` latest, duplicate/missing prefix 없음, 기존 migration 수정 없음 |
| Persistent UAT | 미실행 | `N/A` | 명시적 제외·미승인, 기존 DB/runtime 무변경 |
| PR CI | 재검증 중 | 최초 run에서 Linux Chromium 모바일 날짜 입력 intrinsic width `121.15625px` 1건 발견, scoped width 보정 후 재실행 | local 동일 Full-Stack과 새 PR run 결과를 최종 게시 gate로 사용 |
| Azure 공개 검증 | 미실행 | `N/A` | 배포 미승인 |

Full-Stack은 물류의 불량 변경 `403`, 제조·영업의 불량 변경 성공, 납품 목표 적용일 상속, 실사 다음 날 재고의 불량 차감, 홈 임시 불량 변경 후 재고 즉시 갱신, API mutation 없음과 새로고침 원상복구를 확인했다. 모바일에서는 홈·월간 생산·월간 출근의 시작일/종료일 폭, 목표 적용 시작일 폭, 예상 생산 입력과 예상 출근 합계의 파란 글자, 페이지 overflow `0`을 확인했다.

재현 명령은 Repository root 기준 다음과 같다. Backend 전체와 Full-Stack 명령은 disposable PostgreSQL을 생성하고 종료 시 정리하며 외부 provider를 비활성화한다.

```text
dotnet build backend/Emi.Qms.sln --configuration Release
bash scripts/e2e-backend-tests.sh
(cd frontend && corepack pnpm test)
(cd frontend && corepack pnpm lint)
(cd frontend && corepack pnpm typecheck)
(cd frontend && corepack pnpm build)
bash scripts/e2e-full-stack.sh e2e/full-stack/g2-operations.full-stack.spec.ts
git diff --check
```

검증 환경은 Repository가 고정한 .NET·Node/pnpm·Playwright 및 PostgreSQL container 계약을 사용했다. 실패 artifact와 disposable DB는 정리되며, 아래 screenshot 4개만 종료 산출물로 보존한다.

Synthetic 화면 증빙:

- [G2 홈 desktop 1440px](g2-operations-002-screenshots/01-g2-home-desktop-1440.png)
- [생산/출하 관리 mobile 390px](g2-operations-002-screenshots/02-g2-operations-mobile-390.png)
- [G2 홈 mobile 390px](g2-operations-002-screenshots/03-g2-home-mobile-390.png)
- [제조 인원 출근 관리 mobile 390px](g2-operations-002-screenshots/04-g2-attendance-mobile-390.png)

## 6. 시행착오와 Finding

| ID | Severity | 상태 | 원인·영향 | 해소·후속 위치 |
| --- | --- | --- | --- | --- |
| `G2-002-PLAN-001` | P2 | `RESOLVED` | 홈 임시값이 정식 예상값으로 오인될 수 있음 | 저장 없음 안내·저장 button 없음·초기화·GET-only 검증 |
| `G2-002-PLAN-002` | P2 | `RESOLVED` | Frontend 임시 재고가 실사 경계를 넘으면 Backend와 불일치 | 같은 checkpoint 우선 계산과 unit·Full-Stack 검증 |
| `G2-002-PLAN-003` | P3 | `RESOLVED` | 불량 전용 permission은 동일 역할 계약을 중복 | `G2.Production.Update` 재사용과 물류 `403` 검증 |
| `G2-002-FORECAST-COLOR-001` | P2 | `RESOLVED` | Graphite 마지막 cascade가 예상 input·출근 합계의 파란색을 덮음 | wireframe semantic 예외와 mobile computed-style 검증, 휴일 빨강 우선 유지 |
| `G2-002-E2E-LOCATOR-001` | P3 | `RESOLVED` | 첫 통합 검증의 재고 행 locator가 table scope를 잘못 결합 | rowheader parent로 고정한 뒤 동일 시나리오 재실행 통과 |
| `G2-002-CI-DATE-WIDTH-001` | P2 | `RESOLVED` | Linux Chromium의 date input intrinsic width 때문에 `116px` grid 안의 실제 폭이 `121.15625px`로 넘침 | G2 mobile date input에 `min-width: 0`, `box-sizing: border-box`, 명시적 max-width를 적용하고 local·PR Full-Stack 재검증 |

현재 Open P0/P1/P2/P3는 `0/0/0/0`이다. 자동 assertion과 검증 시나리오는 같은 구현 cycle에서 작성되었으므로 독립적인 현업 ground truth가 아니다. 자동 검증은 계약 회귀와 기술 동작의 근거이며, 사용자 직접 가독성·업무 적합성 검수를 대체하지 않는다.

## 7. 개인정보·secret과 artifact 검토

테스트와 screenshot은 disposable DB, 고정 dev persona와 synthetic 숫자만 사용했다. 실제 사용자·회사 계정·고객·프로젝트 원문, credential, connection string, token과 실제 운영 API/DB body를 tracked 문서나 screenshot에 기록하지 않았다.

`.env`, 인증서, dependency lockfile과 provider 설정은 변경하지 않았다. 실패 시 생성된 Playwright `test-results`, video와 build `dist`는 ignored 상태이며 tracked/staged 변경에 포함하지 않는다. 기존 canonical clone의 사용자 WIP와 기존 G2 검수 worktree/runtime은 건드리지 않았다.

## 8. SOP

### 일일 입력

1. 제조·영업 담당자는 `G2 → 생산/출하 관리`에서 날짜를 고르고 오전·오후 생산량과 불량 수량 중 필요한 값만 입력한다.
2. 영업·물류 담당자는 같은 화면에서 일일 납품량을 입력한다.
3. 미래 날짜는 예상값이다. 서울 날짜가 도래하면 기존 생산·납품·불량 예상 숫자는 자동으로 빈 값이 되므로 실제값을 다시 입력한다.
4. `다른 사용자가 먼저 변경` 충돌은 덮어쓰지 말고 최신 자료를 다시 확인해 재입력한다.

### 목표와 홈 시뮬레이션

1. `G2 홈 → 적용 시작일별 목표`에서 `납품 목표`를 선택하고 적용 시작일·수량을 저장한다.
2. 같은 적용일을 다시 저장하면 기존 납품 목표를 정정한다. 새 목표 정책은 새 적용일로 등록한다.
3. 첫 그래프 아래 `생산 현황`에서 생산·납품·불량 숫자를 바꿔 그래프·KPI·예상 재고를 비교한다.
4. 홈 입력은 저장되지 않는다. 확인이 끝나면 `임시값 초기화`를 누르거나 다시 조회한다.
5. 실사 날짜의 재고는 임시 생산·납품·불량보다 실사 수량을 우선한다.

납품 목표는 같은 유형·적용 시작일 조합을 한 row로 유지한다. 같은 적용일 저장은 수량 정정이고, 별도 삭제 기능은 없다. 최초 목표 이전 날짜에는 목표가 없고, 등록된 목표는 다음 적용 시작일 전날까지 상속된다. 날짜는 서울 업무일의 `date` 값으로 처리한다.

### 장애·복구와 운영 적용 전 절차

- `403`: 해당 역할의 생산·불량·납품·목표 권한을 확인하고 UI를 우회해 재시도하지 않는다.
- `409`: 최신 자료를 다시 읽고 현재 version 기준으로 재입력한다.
- 재고 불일치: 마지막 실사와 이후 생산·납품·불량을 순서대로 확인한다.
- migration 장애: 적용된 `0084`를 수정하지 않고 다음 additive forward-fix를 작성한다.
- 운영 적용 전: 최신 main 재기준선화, 전체 CI, migration fresh/existing, Persistent UAT snapshot·rollback과 exact SHA release 승인을 별도로 받는다.
- 안전한 적용 순서: `0084` migration → 같은 exact SHA Backend → 같은 exact SHA Frontend → G2 smoke test다. Migration 적용 뒤 이전 Backend로 rollback하면 새 불량 row를 재고에서 차감하지 못하므로 G2 입력을 중지하고 검증된 새 Backend 복구 또는 additive forward-fix를 우선한다.

## 9. User manual

### G2 홈

- 첫 그래프 아래 생산 현황표의 오전 생산·오후 생산·납품·불량 칸은 조회용 임시값이다. 입력 즉시 표·그래프·평균·재고가 바뀌지만 저장되지 않는다.
- `임시값 초기화`는 저장된 원본으로 되돌린다. 월 이동·새로고침·새 조회도 임시값을 없앤다.
- 행 순서는 오전 생산, 오후 생산, 생산 합계, 납품 목표, 납품, 불량, 재고다.
- 주황 점선은 납품 목표다. 마우스를 올리면 해당 날짜 목표를 확인한다.
- 재고는 생산을 더하고 납품과 불량을 뺀다. 실사 날짜에는 실사값이 기준이다.
- 미래 예상 날짜 값은 파란색이고, 토요일·일요일·공휴일은 빨간색이 우선한다.
- 모바일에서는 축과 그래프 틀을 유지한 채 기존처럼 안쪽 날짜를 가로로 탐색한다. 시작일·종료일과 목표 적용일 입력은 좁은 폭으로 표시된다.

### 생산/출하 관리

- 불량 수량은 생산량과 함께 정식 저장된다. 제조·영업·System Administrator가 수정할 수 있다.
- 물류는 납품량만 수정하며 불량은 수정할 수 없다.
- 빈 칸은 미입력이고 `0`은 실제 0대다.
- 월간 표에서 납품 목표·납품·불량과 불량 차감 재고를 함께 확인한다.
- 미래 불량은 예상값이며 날짜가 도래하면 자동으로 빈 값이 된다.

### 제조 인원 출근 관리

- 출근 입력·합계·EMI/도급 펼침 계약은 기존과 같다.
- 월간표의 예상 날짜 숫자는 파란색이고 휴일 열은 빨간색이다.
- 모바일 시작일·종료일은 한 화면에서 나란히 확인할 수 있는 좁은 폭이다.

## 10. User validation checklist

상태: `자동 검증 완료 / 사용자 검수 완료 / 원격 main 병합 승인`

### 자동 검증 완료

- [x] 납품 목표 저장·적용일 상속·주황 점선·hover
- [x] 불량 저장, 실제 `0`과 빈 값 구분, 제조·영업 허용·물류 거부
- [x] 불량 차감 재고와 실사 checkpoint 경계
- [x] 미래 불량 날짜 도래 초기화
- [x] 홈 임시 입력 즉시 반영·API 저장 없음·초기화/재조회 폐기
- [x] 생산 현황표 위치와 7개 행 순서
- [x] 예상값 파랑·휴일 빨강 우선
- [x] 모바일 4개 날짜 범위·목표 적용일 입력 폭과 page overflow `0`
- [x] Backend `568/568`, Frontend `241/241`, isolated Full-Stack `1/1` 회귀

### 사용자 검수와 게시 결정

- [x] 격리 검수 서버에서 synthetic 자료가 표시된 G2 홈과 새 표·그래프 구성 확인
- [x] 직접 입력 박스의 셀 중앙 정렬과 browser 증감 버튼 제거 확인
- [x] 자동 검증 결과와 남은 운영 적용 경계를 확인하고 원격 main 병합 승인

검수 결과는 실제 사용자명·업무 원문 없이 날짜, 환경, 익명 역할과 성공/실패만 기록한다.

## 11. Rollback, 남은 위험과 후속 경계

아직 migration과 code는 운영에 적용되지 않았다. 게시 전에는 이 branch 변경을 폐기해 원격 상태를 그대로 유지할 수 있다. 운영에 `0084`가 적용된 뒤에는 migration 파일을 되돌려 수정하지 않는다. 이전 app으로 잠시 rollback할 수는 있지만 새 불량 row를 이전 Backend가 재고에서 차감하지 않으므로 G2 입력을 중지하고 다음 forward-fix 또는 검증된 app 재배포를 우선한다. schema 축소가 필요하면 `Defect` metric·`Delivery` target data 보존/제거 정책과 별도 destructive 승인이 필요하다.

Git 게시 결과는 연계 PR과 종료 handoff에서 추적한다. 이후 남은 제품 Gate는 exact main SHA migration/Backend/Frontend 배포와 배포 후 검수다. Persistent UAT와 Azure는 자동으로 진행하지 않는다.

## 12. 종료 산출물 추적

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성 완료 | 이 문서 |
| SOP | 작성 완료 | 이 문서 8장 |
| User manual | 작성 완료 | 이 문서 9장 |
| Roadmap update | 작성 완료·local 미게시 | `docs/00-product-roadmap.md` 6.5·추적 97·Decision Log |
| User validation checklist | 자동 검증·사용자 검수 완료·원격 main 병합 승인 | 이 문서 10장 |

코드 구현·자동 검증·사용자 검수와 원격 main 병합 승인은 완료했다. Git 게시 결과는 연계 PR·종료 handoff로 추적하고, 운영 적용은 별도 승인 전 완료로 처리하지 않는다.
