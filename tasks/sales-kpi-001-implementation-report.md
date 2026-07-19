# TASK-SALES-KPI-001 Implementation report — 영업 연간 매출 KPI

## 1. 요약과 상태

- 목적: 영업 사용자가 Home과 영업 전용 화면에서 월별 확정 매출과 목표를 1년 단위로 비교하고, 금액 KPI와 근거 프로젝트를 확인하게 한다.
- 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL` — 구현·필수 자동 검증·격리 브라우저 검증 완료, 사용자 검수는 마지막 일괄 대기
- 최종 계약: [Fable 2차 기획](../docs/32-sales-kpi-plan.md)
- 최신 표시 계약: [Change 002 구현 보고서](sales-kpi-001-change-002-implementation-report.md)
- Branch/base: `experiment/task-home-002-personalized-shell` / `c4b999f`
- 대표 repo·`main`·Persistent UAT·actual provider: 미변경
- Merge 승인: `0/3`

## 2. 해결한 업무 문제

기존 영업 Home은 공통 부서 지표만 제공해 12개월 매출 흐름과 월 목표의 차이를 한눈에 파악하기 어려웠다. 전용 `영업` menu도 영업 담당자의 금액 판단에 최적화되지 않았다. 이번 구현은 발행일과 금액이 확정된 세금계산서만 실적으로 집계하고, 미확정 파이프라인은 달성률에서 분리해 과장 없는 연간 판단 화면을 제공한다.

## 3. 구현 범위와 아키텍처

### DB·Migration

- `0043_sales_monthly_targets.sql`을 additive migration으로 추가했다.
- 연도·월·통화별 영업 목표와 CAS version, append-only 감사 원장을 저장한다.
- `Sales.Target.Manage` 권한은 시스템 관리자에게만 seed한다.

### Backend·API·권한

- 12개월 확정 매출·목표, 연 누계, 등록 목표 누계, 달성률, 잔여/초과 금액, 별도 파이프라인을 한 aggregate로 제공한다.
- 월을 선택하면 해당 월 확정 매출을 구성한 권한 범위 내 프로젝트 근거만 조회한다.
- 영업 화면은 기존 조회 권한과 project scope를 재사용한다. 목표 등록·수정은 서버의 `Sales.Target.Manage` 정책으로 시스템 관리자만 허용한다.
- 목표 저장은 expected version을 비교하고 transaction·audit와 함께 반영한다.

### Frontend·UI/UX

- 왼쪽 menu에 `영업`을 유지하고 전용 route에 금액 KPI 5개, 12개월 주 graph, 파이프라인, 월별 근거를 구성했다.
- 영업 부서 사용자의 Home 핵심 panel을 같은 연간 graph와 금액 KPI 3개로 교체했다.
- Desktop은 reference 이미지처럼 넓은 흰 작업면·얇은 경계·낮은 그림자·compact card를 사용한다.
- Change 002 이후 Mobile도 4×3 월 grid가 아니라 12개월 actual·target·attainment SVG graph와 핵심 카드 순서를 사용하며 가로 overflow를 허용하지 않는다.

### Excel/PDF/첨부·Workflow 영향

- Excel/PDF/첨부: `N/A` — 영업 KPI는 조회·목표 관리 범위이며 기존 선택 export와 문서 생성 계약을 변경하지 않았다.
- Workflow: 세금계산서·프로젝트 완료 상태를 변경하지 않고 `TASK-014A`의 확정 데이터를 읽기만 한다.

## 4. 기술적 결정과 검토한 대안

- 수주 예상액을 실적에 섞지 않고 `예상 파이프라인`으로 분리해 달성률 왜곡을 방지했다.
- 목표가 등록된 월만 목표 누계·달성률 분모에 포함하고 미등록 월은 `목표 미등록`으로 명시했다.
- Home과 영업 화면이 같은 Backend aggregate를 사용해 숫자 불일치를 막았다.
- 모든 부서에 금액 KPI를 노출하는 대신 기존 권한·project scope로 서버에서 제한했다.

## 5. 시행착오 및 폐기한 접근

- 첫 Full-Stack 실행은 최신 Release binary가 아닌 이전 build를 참조해 신규 endpoint가 없는 상태로 실패했다. Release를 다시 build한 뒤 같은 격리 환경에서 재검증했다.
- 초기 visual assertion은 KPI의 실제 표시 구조와 맞지 않았다. 사용자 동작 계약을 바꾸지 않고 selector를 화면의 고정 label 기준으로 보정했다.
- component 파일의 formatting helper export가 Fast Refresh warning을 만들었다. helper를 `salesKpiFormat.ts`로 분리해 신규 warning을 제거했다.

## 6. 변경 파일과 역할

- `database/migrations/0043_sales_monthly_targets.sql`: 목표·감사·권한
- `backend/.../Sales/*`: KPI 계약·집계 store·endpoint·목표 CAS
- `backend/.../Authorization/*`, `Program.cs`: 목표 관리 policy와 route 등록
- `frontend/src/SalesKpiPage.tsx`, `SalesKpiChart.tsx`, `salesKpi*.ts`: 전용 화면·적응형 graph·type/format
- `frontend/src/HomePage.tsx`, `App.tsx`, `styles.css`: 영업 Home KPI와 navigation
- `PostgreSqlMigrationTests.cs`, `App.test.tsx`, Full-Stack spec: 집계·권한·UI 회귀와 screenshot

## 7. 실행한 검증과 결과

| 검증 | 결과 |
| --- | --- |
| Backend Debug/Release build | 성공, warning/error 0 |
| 관련 PostgreSQL 통합 | fresh schema와 기존 `0042 → 0044` upgrade, 영업 KPI·목표·파이프라인·audit 4/4 성공 |
| Backend 전체 | 398/398 성공 |
| Frontend lint | error 0, 기존 `main.tsx` Fast Refresh warning 1만 유지 |
| Frontend typecheck/unit | 성공, 104/104 |
| Frontend production build | 성공, 기존 chunk-size warning 유지 |
| 격리 Full-Stack E2E | Sales·ADMIN 결합 시나리오 1/1 성공 |
| Browser desktop/mobile | 영업 Home·전용 화면 4개 synthetic screenshot, 390px overflow 0 |

Persistent UAT migration/runtime, 대표 5081/5174와 actual provider는 승인 범위 밖이라 실행하거나 변경하지 않았다. E2E의 임시 DB·Backend·Frontend는 검증 뒤 종료했다.

## 8. 개인정보·secret 검토

- screenshot과 E2E에는 합성 프로젝트·금액·역할만 사용했다.
- 실제 고객·프로젝트·사용자·email, credential, token, provider payload를 tracked 산출물에 기록하지 않았다.
- 감사 검증은 aggregate와 fixed enum으로만 수행했다.

## 9. Finding gate

| Finding | Severity | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `SALES_E2E_STALE_RELEASE` | P2 | RESOLVED | 오래된 Release binary로 신규 route가 없는 것처럼 보임 | Release 재build 후 격리 Full-Stack 재실행 |
| `SALES_VISUAL_ASSERTION_DRIFT` | P2 | RESOLVED | 테스트 selector가 실제 compact KPI 구조와 불일치 | 고정 사용자 label 기반 assertion으로 보정 |
| `SALES_FAST_REFRESH_EXPORT` | P3 | RESOLVED | component 파일의 비component helper export 경고 | 전용 format module로 분리 |

Open P0/P1/P2: `0/0/0`. Risk acceptance 없음.

## 10. SOP — 운영·관리 절차

1. 시스템 관리자가 영업 화면의 `목표 관리`를 열어 연도·통화별 월 목표를 입력한다.
2. 영업 사용자는 Home 또는 `영업` menu에서 연도와 통화를 선택해 월별 확정 매출·목표를 비교한다.
3. 월 graph를 선택해 실적 근거 프로젝트와 세금계산서 발행일을 확인한다.
4. 목표 충돌 메시지가 나오면 최신 목표를 다시 불러온 뒤 재입력한다.
5. 운영 적용 시 migration `0043`을 기존 순서대로 적용하고 새 권한 seed를 확인한다. 이미 적용된 migration은 수정하지 않고 후속 번호로 forward-fix한다.

## 11. User manual — 사용자 사용법

- 영업 부서로 로그인하면 Home의 첫 지표가 연간 매출 graph로 표시된다.
- 왼쪽 `영업` menu에서는 더 많은 금액 KPI와 월별 근거를 확인할 수 있다.
- 파란 막대는 확정 매출, 회색 막대는 월 목표, 빨간 선과 점은 경과 월 달성률이다. 100% 기준선으로 월별 달성 여부를 비교한다.
- 예상 파이프라인은 참고 금액이며 연간 달성률에는 포함되지 않는다.
- Mobile에서도 실제 12개월 graph가 표시되며 44px 월 selector로 근거를 연다.

## 12. 사용자 검수 결과와 남은 항목

- 자동 검증·격리 browser 검증: 완료
- 사용자 validation checklist: 작성됨
- 사용자 직접 검수: `사용자 검수 대기 — 마지막 일괄 검수`
- 대표 repo·main 승격, Persistent UAT migration/runtime handover, push·PR·merge: 별도 승인 전 금지
- `main` merge 승인: `0/3`

## 13. Rollback·forward-fix

- 코드: experiment commit에 revert commit을 만든다. 대표 branch에는 반영되지 않았다.
- DB: `0043`을 수정·삭제하거나 운영 DB를 reset하지 않고 새 번호의 additive forward-fix를 사용한다.
- 잘못 입력한 목표는 관리자 UI에서 CAS를 거쳐 수정하며 audit를 보존한다.

## 14. 5종 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 |
| SOP | 완료 | 이 문서 10장 |
| User manual | 완료 | 이 문서 11장 |
| Roadmap update | 완료 | [Product Roadmap](../docs/00-product-roadmap.md) TASK-SALES-KPI-001·Decision Log |
| User validation checklist | 사용자 검수 대기 | [체크리스트](sales-kpi-001-user-validation-checklist.md) |

## 15. Fable 사용량·session

- 1차 planning 전/후: 5시간 0%/0%, 주간 전체 16%/17%, Fable 32%/34% 사용
- 2차 planning 전/후: 5시간 25%/25%, 주간 전체 18%/18%, Fable 35%/35% 사용
- 구현 종료 최신: 5시간 50% 사용·50% 잔여, 주간 전체 20% 사용·80% 잔여, Fable 39% 사용·61% 잔여
- Fable private state: `FABLE_TASK_SESSION_CLEANED`, session·transcript 각 2개 제거
