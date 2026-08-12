# TASK-PANEL-DESIGN-001 구현 보고서 — 설계 도번·필수값·패널 열반

## 1. 요약과 상태

- 목적: 일반 Item 설계 담당자가 패널별 도번과 최외곽 W/H/D를 입력하고, 함께 출하되는 패널을 열반해 개별 크기와 열반 전체 W/H/D를 확인할 수 있게 한다.
- 상태: 로컬 구현·자동 검증·사용자 검수 완료 / 통합 게시 승인
- Task 유형: `NEW_FEATURE` — 사용자 명시 예외에 따른 Codex 기획·구현
- Branch/base: `feat/task-panel-design-001-grouping` / `origin/main` `af796547ffb260ae427932a4734894af23c21ae6`
- Git 게시·운영: 사용자가 우선순위 1·2 및 관리자 화면 검수본과 단일 통합 PR·main 병합·Azure 공개배포를 승인했다. 실행 결과는 통합 게시 Change에서 기록한다.

## 2. 해결한 업무 문제

기존 설계정보 입력은 패널명과 W/H/D만 저장할 수 있어 도번을 별도로 관리해야 했다. 또한 함께 붙여 출하하는 패널의 관계를 기록할 수 없어서 설계 탭에서 개별 패널만 보였고, 포장 업무에 필요한 결합 W를 사용자가 직접 계산해야 했다. 프로젝트 포장방식에 따라 무엇을 반드시 입력해야 하는지도 화면 진입 시 바로 드러나지 않았다.

이번 구현은 기존 패널 행·모바일 카드·흑백 wireframe 규격을 그대로 확장했다. 별도 강조 카드나 왼쪽 강조선을 만들지 않고, 필수값 안내는 평면 구분선, 열반 조작은 기존 버튼과 1px 일반 테두리, 저장된 열반은 2px 일반 검정 테두리로 표현한다.

## 3. 포함·제외 범위

### 포함

- 패널별 도번 직접 입력·수정·비움과 Excel 양식·미리보기·적용
- 포장방식별 프로젝트 설계 필수값 안내
- 일반 Item의 2면 이상 패널 열반·열반 전체 해제와 매 수정 시 1번부터 재번호화
- 설계 탭의 열반별 2px 일반 테두리, 구성 패널과 개별 W/H/D, `W 합계 × H 최댓값 × D 최댓값` 표시
- 도번·열반 변경 감사 이력, 동시성 버전 검사와 단일 transaction rollback
- Desktop·390px 입력/조회 UX, Backend·Frontend·migration·격리 Full-Stack 검증

### 제외

- UL891 세트·code·설계 구조 변경 및 UL891 패널 열반
- 도번·열반을 설계 완료 필수값으로 승격
- 패널 간 간격·프레임·포장 여유 계산
- 열반 단위 제조·품질·물류 처리
- 신규 알림·첨부·PDF
- 운영 DB·Azure runtime 변경과 Git 게시

## 4. 아키텍처와 영향

### DB·Migration

- migration `0077_panel_design_drawing_groups.sql`에 `panel_placeholders.drawing_number`과 `panel_group_number`를 additive로 추가했다.
- 도번은 trim 기준 1~200자 또는 `null`, 열반 번호는 양의 정수 또는 `null`만 허용한다.
- 활성 패널의 프로젝트·열반·순번 조회 index를 추가했다.
- 기존 데이터는 도번·열반 모두 `null`로 보존하며 자동 추정하거나 소급 열반하지 않는다.
- 우선순위 1·2의 LSE TASK NO migration을 `0076`, 본 migration을 `0077`로 확정했다. 이미 공개된 migration을 수정하는 작업은 아니다.
- rollback은 공개 전 branch 폐기, 공개 후 다음 additive migration의 forward-fix를 사용한다.

### Backend·API·동시성

- 패널정보 응답에 `drawingNumber`, `panelGroupNumber`, 프로젝트 수준 `supportsPanelGrouping`을 추가했다.
- 기존 PATCH update-mask에 도번과 열반 변경 mask를 추가했다.
- 열반 변경 시 프로젝트의 패널 전체를 같은 transaction에서 잠그고, 변경 전후 영향을 받는 활성 열반이 2면 이상인지 검사한다.
- 패널 하나라도 stale version, 권한, 상태 또는 열반 규칙을 통과하지 못하면 도번·패널명·사이즈·열반 변경 전체를 rollback한다.
- UL891의 열반 mutation은 화면 숨김과 별개로 서버가 validation으로 차단한다.
- 감사 이력에 `DrawingNumber`, `PanelGroupNumber` 변경을 남기며 화면에는 `도번`, `패널 열반`으로 표시한다.

### Excel

- 기존에 실사용하지 않던 `도번` 열을 실제 현재값 다운로드, upload parser, preview와 apply에 연결했다.
- 열반은 여러 행을 함께 선택하는 맥락이 필요하므로 Excel에서는 변경하지 않고 웹 화면에서만 설정한다.
- 기존 패널명·W/H/D, 단위 변환, preview validation과 부분 입력 계약은 유지한다.

### Frontend·UI·UX

- 입력 표는 `선택 / No / 패널명 / 도번 / W / H / D / 열반 / 패널정보 / QR` 순서다. 모바일 카드는 같은 정보를 세로로 표시한다.
- 목포장은 `패널명 · W · H · D`, 일반 포장은 `패널명`을 필수값으로 화면 진입 즉시 보여 준다. 도번과 일반 Item의 열반은 선택값이다.
- W 제목 옆 keyboard-focus 가능한 `i`는 `포장 업무에 필요한 패널의 최외곽 사이즈를 기재해주세요.`를 표시한다.
- 체크한 패널 2면 이상을 새 열반으로 만들고, 열반 구성원 하나를 선택해도 그 열반 전체를 해제한다.
- 열반 생성·재구성·해제 때 현재 활성 열반을 구성 패널 순서 기준 `1, 2, 3…`으로 다시 번호 매겨 번호가 누적되지 않게 한다.
- 저장 전부터 열반 전체 크기를 보여 주며 구성 패널의 W/H/D가 하나라도 비어 있으면 숫자를 추정하지 않고 `사이즈 입력 필요`로 표시한다.
- 상세 설계 탭은 열반별 구성 No, `W 합계 × H 최댓값 × D 최댓값`, 도번, 패널명과 개별 W/H/D를 함께 보여 준다. 취소 패널 또는 활성 구성원이 1면뿐인 legacy 열반은 열반 block으로 표시하지 않는다.
- 새 UI는 흰 배경, 기존 회색 header와 일반 실선만 사용한다. AI식 왼쪽 강조 rail·색상 강조 box·그림자는 추가하지 않았다.

### 권한·Workflow·기존 회귀

- 기존 `PanelInfo.Update` 권한, 활성 프로젝트/패널 제한과 수정사유 요구를 그대로 사용한다.
- 설계 완료, QR 가능, 제조·품질·물류 상태, UL891 세트·code 의미는 변경하지 않는다.
- 기존 패널명 중복 허용 확인, 단위 전환, Excel import와 설계 상세 route를 함께 회귀 검증했다.
- PDF·첨부·실제 외부 provider 영향은 없다.

## 5. 기술적 결정과 검토한 대안

- 단순한 열반 문자열 대신 패널별 양의 정수 group number를 저장했다. 관계형 별도 table은 현재 `패널 하나당 열반 하나` 요구에 비해 구조와 운영 비용이 크므로 선택하지 않았다.
- Frontend 계산만 저장하는 방식은 API·Excel·다른 소비자가 관계를 알 수 없어 제외했다. Backend가 열반 규칙을 최종 강제하고 Frontend는 즉시 미리보기만 담당한다.
- 합성 W/H/D를 DB에 중복 저장하지 않고 현재 활성 구성원의 W 합계와 H/D 최댓값에서 계산한다. 개별 크기 수정이나 패널 취소 뒤 값이 어긋나는 문제를 막기 위한 결정이다.
- 도번과 열반을 완료 필수값으로 넣는 안은 기존 프로젝트의 완료율·QR 의미를 소급 변경하므로 제외했다.

## 6. 시행착오 및 폐기한 접근

- 전체 mock UI를 4개 worker로 동시에 실행할 때 이번 변경과 무관한 로고 이미지 자연 크기 검사가 한 번 `0 × 0`으로 관찰됐다. 해당 검사를 단독 실행해 통과했고, 전체 8개를 1 worker로 재실행해 모두 통과시켜 병렬 이미지 로딩의 일시적 timing 문제로 확정했다.
- 열반 border는 기존 application skin의 후행 selector가 색만 덮는 현상이 있었다. 컴포넌트 재설계 대신 최종 panel-design override에서 2px `#202020` 일반 실선을 고정하고 browser CSS assertion으로 검증했다.
- Full-Stack 첫 실행은 Release Backend binary가 없어 기동 전에 중단됐다. Release build를 생성한 뒤 동일한 격리 검사를 재실행해 통과했다. 제품 결함이나 데이터 mutation은 없었다.
- 상세 화면에는 Desktop·Mobile markup이 동시에 존재해 전역 locator가 열반 block을 중복 계산했다. 제품을 바꾸지 않고 검사를 Desktop table scope로 제한해 실제 표시 계약을 검증했다.

## 7. 변경 파일

- `database/migrations/0077_panel_design_drawing_groups.sql`: 도번·열반 schema, constraint와 index
- `backend/src/Emi.Qms.Api/PanelInformation/PanelInformationContracts.cs`: request/response/Excel 계약
- `backend/src/Emi.Qms.Api/PanelInformation/PanelInformationDomain.cs`: 도번 normalization과 update validation
- `backend/src/Emi.Qms.Api/PanelInformation/PanelInformationExcelParser.cs`: 도번 parse
- `backend/src/Emi.Qms.Api/PanelInformation/PanelInformationStore.cs`: 조회·저장·잠금·열반 검증·감사·Excel
- `backend/src/Emi.Qms.Api/Projects/ProjectStore.cs`: 설계 상세 projection의 도번·열반 조회
- `frontend/src/projects.ts`: Frontend API type
- `frontend/src/App.tsx`: 필수값 안내, 도번·열반 입력, 순번 정규화, 전체 크기 표시와 감사 한글명
- `frontend/src/styles.css`: 기존 wireframe에 맞춘 표·모바일·도움말·열반 일반 테두리
- `tasks/panel-design-001-change-001.md`: 사용자 검수에서 확인된 번호·용어·전체 크기 보정 계약
- Backend/Frontend unit, migration, mock UI와 Full-Stack test 파일
- planning, identity gate, 이 보고서, 사용자 검수 checklist와 Product Roadmap

## 8. 실행한 검증과 결과

| 검증 | 적용 여부 | 결과 | 근거/미실행 이유 |
| --- | --- | --- | --- |
| `git diff --check` | 적용 | 통과 | whitespace 오류 0 |
| Backend Release build | 적용 | 통과 | warning/error `0/0` |
| Backend 패널정보 영향 test | 적용 | 통과 | `43/43` |
| Migration fresh/existing catalog | 적용 | 통과 | `51/51` |
| Backend 전체 Release test | 적용 | 통과 | `522/522`, 약 14분 51초 |
| Frontend lint | 적용 | 통과 | error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend typecheck | 적용 | 통과 | error 0 |
| Frontend unit | 적용 | 통과 | 29 files, `212/212` |
| Frontend production build | 적용 | 통과 | 기존 500KB chunk warning 유지 |
| mock UI 전체 Chromium | 적용 | 통과 | 직렬 재검증 `8/8`; Desktop·390px와 무가로 overflow 포함 |
| 패널 설계 mock visual/CSS | 적용 | 통과 | 필수값 평면 안내, 도움말, 1px toolbar, 열반 2px 검정 일반 실선과 `1700 × 1800 × 400 mm` 확인 |
| 격리 Full-Stack Chromium | 적용 | 통과 | `TASK-PANEL-DESIGN-001` `1/1`, 실제 DB 저장·재조회·전체 열반 크기 확인 |
| Change 001 재번호 unit | 적용 | 통과 | 열반 1·2 생성 뒤 1 해제 시 남은 구성원이 열반 1로 저장됨 |
| Change 001 로컬 검수 runtime | 적용 | 통과 | 격리 DB 저장값 `1=No.1,No.2`, `2=No.3,No.5`; PC 결과 화면에서 전체 W/H/D와 2px 테두리 확인 |
| Persistent UAT·Azure | 미적용 | N/A | 사용자 승인 범위 밖이며 우선순위 검수본 통합 뒤 시행 |
| 사용자 직접 검수 | 적용 | 통과 | 2026-08-12 사용자가 열반 재번호화·용어·전체 W/H/D 보정을 포함한 화면 검수를 완료 |

## 9. 개인정보·Secret 검토

- 자동 검증은 합성 project·GUID와 격리 PostgreSQL만 사용했다.
- 실제 사용자 이름·이메일·전화번호, tenant/client/object id, token·connection string을 새 문서나 diff에 기록하지 않았다.
- 실제 Teams·메일·Web Push 발송과 운영 DB 조회·수정은 0건이다.
- browser screenshot은 합성 화면의 임시 파일로만 육안 확인했고 tracked artifact에 포함하지 않았다.

## 10. Finding gate

| Finding | Severity | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `PANEL-DESIGN-GROUP-ATOMICITY` | P2 | RESOLVED | 선택 패널만 검사하면 기존 열반에 1면이 남거나 일부 저장될 수 있음 | 프로젝트 패널 전체 잠금, 영향 열반 최종 구성 validation, 기존 PATCH transaction/CAS rollback 적용 |
| `PANEL-DESIGN-UL891-MODEL-COLLISION` | P2 | RESOLVED | 일반 열반이 UL891 세트 모델과 중복 의미를 만들 수 있음 | response capability false, UI 미표시와 서버 mutation 차단을 함께 적용 |
| `PANEL-DESIGN-GROUP-BORDER-CASCADE` | P2 | RESOLVED | 기존 skin selector가 새 열반 외곽선 색을 회색으로 덮을 수 있음 | 최종 2px 검정 일반 실선 override와 browser computed-style 검증 |
| `PANEL-DESIGN-GROUP-NUMBER-DRIFT` | P2 | RESOLVED | 재구성할 때마다 최댓값+1이 실제 번호가 되어 사용자 번호가 계속 증가 | 매 변경 뒤 구성 패널 첫 순번 기준 1부터 재번호화하고 변경된 모든 행을 함께 저장 |
| `PANEL-DESIGN-GROUP-SIZE-INCOMPLETE` | P2 | RESOLVED | W 합계만 표시해 열반의 실제 최외곽 크기를 파악할 수 없음 | W 합계 × H 최댓값 × D 최댓값을 입력·상세 PC·모바일에 동일 표시 |
| `PANEL-DESIGN-MOCK-LOGO-TIMING` | P3 | RESOLVED | 4-worker 전체 mock에서 기존 logo natural size가 한 차례 0으로 관찰 | 단독 통과 후 전체 1-worker `8/8` 통과, 제품 변경 불필요 |
| `PANEL-DESIGN-E2E-RELEASE-MISSING` | P3 | RESOLVED | 첫 Full-Stack 실행 전 Release binary 부재 | Release build 뒤 동일 격리 시나리오 통과 |
| `PANEL-DESIGN-E2E-DUAL-MARKUP-SCOPE` | P3 | RESOLVED | 반응형 Desktop·Mobile markup 동시 존재로 전역 test locator가 중복 집계 | 사용자 표시를 바꾸지 않고 Desktop table로 검증 scope 고정 |

Open P0/P1/P2: `0/0/0`. 기존 Fast Refresh warning과 production chunk-size warning은 이번 diff에서 새로 만들거나 악화시키지 않았다.

## 11. SOP — 운영 적용과 복구

1. 사용자 검수에서 일반 Item 입력·저장·재조회와 UL891 미노출을 확인한다.
2. LSE TASK NO migration `0076` 뒤 본 Task migration `0077`이 순서대로 적용되는지 확인한다.
3. migration filename, migration test 기대값과 문서 번호를 함께 바꾼 뒤 fresh/existing migration suite를 다시 실행한다.
4. 통합 branch에서 Backend→Frontend→Full-Stack과 CI Gate를 통과시킨다.
5. 게시 승인 후 migration을 Backend보다 먼저 적용하고 Backend→Frontend 순서로 교체한다.
6. 공개 환경에서 일반 Item 도번·열반 조회와 UL891 설계 회귀를 확인한다.
7. 문제가 있으면 공개된 migration 파일을 수정·삭제하지 않는다. 코드는 승인 commit을 revert하고 schema/data는 다음 additive migration 또는 앱 forward-fix로 복구한다.

## 12. User manual — 설계 담당자 사용법

1. 프로젝트 상세의 `설계` 탭에서 `패널명·사이즈 수정`을 누른다.
2. 상단 `이 프로젝트의 필수 입력`에서 현재 포장방식에 필요한 값을 확인한다.
3. 패널명 오른쪽 `도번`에 도면 번호를 입력하고 W/H/D에는 각 패널의 최외곽 크기를 입력한다.
4. 크기 기준이 궁금하면 W 옆 `i`에 마우스를 올리거나 keyboard로 focus한다.
5. 함께 붙여 출하할 패널을 2면 이상 체크한 뒤 `선택 패널 열반`을 누른다.
6. 행의 `열반 N · W × H × D` 미리보기를 확인하고 `직접 입력 저장`을 누른다. 열반을 다시 구성해도 현재 번호는 1부터 다시 정리된다.
7. 기존 정보를 바꿀 때는 표시되는 수정사유를 입력한다.
8. 설계 탭으로 돌아오면 굵은 일반 테두리 안에서 개별 도번·크기와 열반 전체 W/H/D를 함께 확인할 수 있다.
9. 열반을 풀려면 구성 패널 중 하나를 체크하고 `선택 열반 해제` 후 저장한다.
10. UL891은 세트 구조를 사용하므로 일반 열반 선택 UI가 나오지 않는다.

## 13. 사용자 검수 상태

- 자동 검증 상태: `완료`
- 사용자 직접 검수 상태: `사용자 검수 완료`
- 상세 항목: `tasks/panel-design-001-user-validation-checklist.md`

## 14. 사용자 검수 결과와 남은 항목

- 자동·격리 browser 검증과 사용자 직접 검수를 완료했다.
- 사용자가 우선순위 1·2, 관리자 홈 변경과 함께 통합 branch에서 migration 번호 보정·필수 CI·단일 PR·공개배포를 승인했다.
- 통합 게시 결과는 `TASK-AZURE-DEPLOY-001`의 다음 Change에서 기록한다.
- 사용자 검수에서 결함이 확인되면 신규 Task를 만들지 않고 `TASK-PANEL-DESIGN-001`의 bugfix/change로 보정한다.

## 15. Rollback·forward-fix

- 게시 전: 이 branch 변경을 폐기하면 원격 main과 운영에는 영향이 없다.
- 게시 후 코드: 해당 통합 commit을 revert한다.
- 게시 후 DB: 적용된 migration을 수정·삭제하거나 DB를 초기화하지 않는다. 다음 additive migration으로 column/constraint/index를 forward-fix한다.
- 기존 프로젝트: 도번·열반 기본값이 `null`이므로 기능을 사용하지 않으면 기존 표시와 workflow를 유지한다.

## 16. 5종 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 1~10장 |
| SOP | 완료 | 이 문서 11장 |
| User manual | 완료 | 이 문서 12장 |
| Roadmap update | 완료 | `docs/00-product-roadmap.md` TASK-PANEL-DESIGN-001 행 |
| User validation checklist | 사용자 검수 완료 | `tasks/panel-design-001-user-validation-checklist.md` |
