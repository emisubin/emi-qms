# DESIGN-000 Change 002 Implementation report — Department Input Experience Unification

## 1. 요약과 상태

- 목적: 부서마다 달랐던 입력 시작, 값 선택, 저장 위치를 같은 순서와 조작 방식으로 통일해 사용자가 화면마다 사용법을 다시 익히지 않게 한다.
- 상태: `EXPERIMENT_COMPLETE / USER_VALIDATION_PENDING`
- 계약: [DESIGN-000 Change 002](design-000-change-002.md)
- 범위: 영업, 생산관리, 설계, 구매, 자재, 제조, 품질, 물류, 정산의 기존 입력 화면과 공통 입력 primitive.
- 제외: 기능·필수값·권한·API·DB·migration·workflow·상태 전이 변경, 대표 repo·`main`·push·PR·merge·Persistent UAT.

## 2. 구현

- 공통 입력 흐름을 `대상 확인 → 값 입력 → 저장` 3단계로 표시하는 `DsInputFlow`를 추가했다.
- 입력 영역을 번호·제목·짧은 안내로 구분하는 `DsInputSection`을 추가했다.
- FAT 여부와 공급 유형처럼 선택지가 적은 값은 기존 value와 handler를 그대로 사용하면서 한 번 눌러 선택하는 `DsChoiceGroup`으로 통일했다.
- 저장·취소·판정 action을 화면 하단의 `DsActionBar`에 모으고 기존 loading·disabled·feedback 처리를 보존했다.
- 영업 프로젝트 생성·수정, 생산계획, 설계 패널 입력, 구매품 입력, 자재 도착·IQC, 제조 단계 실행, 품질 판정, 물류 증빙 확정, 정산·세금계산서 요청에 같은 입력 구조를 적용했다.
- Desktop은 연관 필드를 두 열로 배치하고 390px에서는 한 열과 큰 조작 영역으로 전환했다.
- 검정·흰색·회색, 1px border, radius 0, shadow 없음과 의미 상태색 예외를 유지했다.
- 모든 기존 API 호출, request payload, 업무 단위, 저장 handler와 상태 전이는 변경하지 않았다.

## 3. 변경 파일

- `frontend/src/design-system/components.tsx`
- `frontend/src/design-system/index.ts`
- `frontend/src/design-system/wireframe.css`
- `frontend/src/App.tsx`
- `frontend/src/MaterialsWorkspace.tsx`
- `frontend/src/ManufacturingPage.tsx`
- `frontend/src/QualityInspectionsPage.tsx`
- `frontend/src/LogisticsPage.tsx`
- `frontend/src/SalesSettlementPage.tsx`
- `frontend/src/SalesBillingRequestPage.tsx`
- `frontend/tests/design-system-input.test.tsx`
- `tasks/design-000-change-002.md`
- `docs/00-product-roadmap.md`
- `docs/27-experiment-task-ledger.md`

## 4. Architecture 영향

| 영역 | 영향 |
| --- | --- |
| Frontend | 공통 입력 primitive 추가와 기존 입력 form 재배치 |
| Backend/API | 없음 |
| DB/migration | 없음 |
| 권한·workflow | 없음 |
| 배포/runtime 설정 | 없음 |

## 5. 검증 결과

| 검증 | 결과 |
| --- | --- |
| 공통 입력·주요 부서 focused test | `PASS` — 6 files, 85/85 |
| Frontend lint | `PASS` — error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend typecheck | `PASS` |
| Frontend unit | `PASS` — 21 files, 134/134 |
| Frontend production build | `PASS` — 기존 500kB chunk warning 유지 |
| Desktop Sales project form | `PASS` — flow 1, section 2, action bar 1, choice group 1, overflow false, radius 0, shadow none |
| Desktop Billing request form | `PASS` — flow 1, section 3, action bar 1, overflow false |
| Mobile 390px Billing request form | `PASS` — 3단계·3개 section·하단 action이 viewport 안에 표시되고 overflow false |
| Browser console | `PASS` — error 0 |
| Frontend runtime | `PASS` — `http://127.0.0.1:42983` HTTP 200 |
| Backend runtime | `PASS` — `http://127.0.0.1:41166/health/ready` HTTP 200 |
| Git whitespace | `PASS` — `git diff --check` |

실행 중인 개발 DB에는 현재 역할에서 수정 가능한 생산관리·구매 프로젝트가 없어 해당 두 입력 form의 browser mutation은 실행하지 않았다. 동일 form의 기존 저장 계약은 회귀 test에 포함했고 전체 Frontend 134개 test가 통과했다. Browser 증빙은 개인정보를 노출하지 않는 개수·computed style·overflow·console 고정 projection만 기록했다.

## 6. Finding gate

| Finding | Severity | 상태 | 해소·후속 |
| --- | --- | --- | --- |
| `D000-C002-LABEL-COMPAT` | P2 | `RESOLVED` | 최초 공통화에서 기존 action 명칭 변경으로 13개 회귀 test가 실패했다. 업무 의미를 바꾸지 않도록 기존 명칭을 복원하고 배치와 조작 방식만 변경했다. |
| `D000-C002-CHUNK-SIZE` | P3 | `BACKLOG` | 기존 production bundle 500kB 경고는 별도 code-splitting housekeeping 후보로 유지한다. |
| `D000-C002-FAST-REFRESH` | P3 | `BACKLOG` | 기존 `main.tsx` Fast Refresh warning 1건은 별도 housekeeping 후보로 유지한다. |

Open P0/P1/P2는 `0/0/0`이다.

## 7. 사용자 검수 체크리스트

- [ ] 영업 프로젝트 생성·수정이 `대상 확인 → 값 입력 → 저장` 순서로 보인다.
- [ ] 생산계획·설계·구매 입력에서 입력 묶음과 저장 위치가 서로 동일하게 느껴진다.
- [ ] 자재 도착·IQC, 제조, 품질, 물류 처리에서 현재 대상과 다음 action을 한눈에 확인할 수 있다.
- [ ] 정산·세금계산서 요청에서도 주요 action이 하단의 같은 위치에 있다.
- [ ] Mobile에서 입력 필드가 한 열로 나오고 가로 스크롤 없이 버튼을 누를 수 있다.
- [ ] 기존 저장 결과, 필수값, 상태 전이와 알림·내 업무 연결이 변경 전과 동일하다.
- [ ] 상태 표시 외 UI가 흑백·무그림자·사각형으로 유지된다.

상태: `자동 검증 완료 · 사용자 검수 대기`.

## 8. SOP·User manual

- 화면 상단의 `QUICK INPUT`에서 현재 입력의 세 단계를 확인한다.
- `01`, `02`, `03` 순서대로 대상과 값을 확인한다.
- 선택 버튼은 한 번 눌러 활성 상태를 확인한다. 입력 필드는 표시된 단위와 필수 표시에 맞춰 작성한다.
- 저장·합격·부적합·확정은 화면 하단 작업 영역에서 실행한다.
- 처리 중에는 같은 action이 잠기며, 성공·실패 안내는 해당 작업 영역 가까이 표시된다.
- Mobile에서도 같은 순서로 위에서 아래로 입력하며 별도 PC 전용 사용법을 외울 필요가 없다.

## 9. Rollback

- 기능·API·DB 변경이 없으므로 data rollback은 필요하지 않다.
- 현재 실험 branch의 복구 기준점은 commit `2247643`이다.
- 사용자가 명시적으로 요청하면 이 변경만 되돌리거나 해당 checkpoint에서 새 branch를 만들어 비교할 수 있다. 대표 repo와 `main`에는 반영되지 않았다.

## 10. 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 본 문서 |
| SOP | 완료 | 본 문서 8장 |
| User manual | 완료 | 본 문서 8장 |
| Roadmap update | 완료 | Product Roadmap DESIGN-000·Decision Log |
| User validation checklist | 자동 검증 완료·사용자 대기 | 본 문서 7장 |

## 11. 개발 기록

### 해결한 업무 문제

부서별 화면마다 입력 시작 위치, 선택 방식과 저장 버튼 위치가 달라 같은 사용자가 프로젝트 흐름을 따라갈 때 화면별 사용법을 다시 찾아야 했다. 기존 기능은 그대로 두고 입력의 읽는 순서와 action 위치를 통일했다.

### 기술적 결정과 검토한 대안

각 페이지를 새 form으로 교체하는 대신 기존 state·handler·API 호출을 감싸는 공통 presentation primitive를 선택했다. 전자는 완전한 시각 통일에는 빠르지만 업무 계약 회귀 위험이 커서 제외했고, 후자는 기능 불변조건을 지키면서도 화면 경험을 통일할 수 있었다.

### 시행착오 및 폐기한 접근

공통화 초기에 일부 기존 버튼 명칭도 단순화했지만, 이는 입력 방법만 바꾼다는 범위를 넘어 기존 사용자 계약과 test를 흔들었다. 13개 실패를 확인한 뒤 명칭은 복원하고 구조·배치·조작 방식만 변경했다.

### 사용자 검수 결과와 남은 항목

자동 test와 Desktop·390px privacy-safe browser 검증은 완료했다. 실제 사용자별 전체 업무 입력 검수는 마지막 일괄 검수에 남겨 두며, 생산관리·구매는 현재 개발 DB에 편집 가능한 대상이 없어 저장 mutation browser 검수 대신 전체 회귀 test로 확인했다.
