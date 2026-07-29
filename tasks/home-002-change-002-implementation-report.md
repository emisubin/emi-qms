# TASK-HOME-002 Change 002 Implementation report — 전 부서 조회 메뉴·참고 디자인 정돈

## 1. 요약과 상태

- 목적: 부서별 입력 권한은 유지하면서 모든 내부 부서가 전체 운영 메뉴를 조회할 수 있게 하고, 사용자 참고 이미지의 밝은 고정 sidebar·얇은 선·compact 목록·낮은 그림자를 EMI 색감으로 재해석한다.
- 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL` — 구현·자동 검증·합성 브라우저 검증 완료, 사용자 검수는 마지막 일괄 대기
- 계약: [Change 002](home-002-change-002.md)
- Branch/base: `experiment/task-home-002-personalized-shell` / `8f1ce8ad86acc66f0db7cfc891f80539ee313a71`
- Task type: `P2_REMEDIATION`; 완료된 HOME-002를 새 Task로 복제하지 않고 다음 change로 재개했다. Fable 신규 기능 기획은 적용하지 않았다.
- 대표 repo·`main`·Persistent UAT·actual provider·push·PR·merge: 미변경
- `main` merge 승인: `0/3`

## 2. 구현 범위

### Navigation·권한

- 모든 내부 부서에 `홈, 내 업무, 프로젝트, Pending, 생산관리, 구매, 자재, 제조, 품질, 물류, 알림` 11개 운영 메뉴를 동일 순서로 표시한다.
- `관리자`는 사용자·권한·감사 개인정보를 포함하므로 System Administrator와 기존 관리자 조회 권한 역할에만 유지한다.
- `GET /api/materials/receipts`, `GET /api/quality/iqc`를 `projects.read` + 기존 project access scope로 분리해 담당 부서 외 사용자도 안전하게 조회할 수 있다.
- 자재 등록·IQC 요청·입고 확정·검사 판정 등 `POST/PUT/PATCH/DELETE` 권한은 기존 담당 permission을 유지한다. 타 부서 화면에는 조회 전용 안내와 비활성 입력 control을 표시한다.
- 자재 선택 Excel export도 호출 사용자의 project scope를 그대로 사용한다. `Project.Read.All`, 판매금액, 삭제 프로젝트, 전체 감사 조회 범위는 확대하지 않았다.

### 디자인

- 흰색 full-height 208px sidebar, 얇은 divider, soft-red 활성 메뉴, 작은 line icon과 고정 footer 개발 사용자 selector를 적용했다.
- 상단 header·입력·버튼·카드·table row 높이를 줄이고 shadow·radius·외곽 card 중첩을 낮췄다.
- Home은 부서 지표 3개, 내 업무·Pending·알림 compact card, 넓은 프로젝트 병목 목록 순서로 재배치했다.
- 모바일은 독립 좌상단 drawer와 task-first Home 구성을 유지하고 브랜드 lockup·작은 menu card·여러 shape icon을 적용했다.
- 개발 연결 상태는 기본 화면을 차지하지 않는 접힌 disclosure로 바꿨으며 모바일에서는 숨겼다.

## 3. 변경 파일

- `frontend/src/App.tsx`: 전 부서 운영 navigation, line icon, 상태 disclosure, mobile drawer 브랜드
- `frontend/src/HomePage.tsx`: compact widget 순서
- `frontend/src/MaterialsWorkspace.tsx`: 자재·IQC 조회 전용 안내와 입력 gate
- `frontend/src/styles.css`: 참고 이미지 기반 shell·Home·목록·모바일 시각 override
- `frontend/tests/App.test.tsx`: 전체 메뉴·Pending·자재 조회 전용 회귀
- `frontend/e2e/mock-ui/*`: 개인화 shell 이후 synthetic identity와 dev 전환 재진입 계약 동기화
- `backend/.../MaterialsEndpointExtensions.cs`, `MaterialsStore.cs`: 자재·IQC read policy와 project scope
- `backend/.../SelectedExcelExportService.cs`: 자재 export scope 전달
- `backend/tests/.../ProcurementApiTests.cs`: 10개 내부 역할 조회 성공·비담당 mutation 403

## 4. 검증 결과

| 검증 | 결과 |
| --- | --- |
| Backend Debug build | 성공, warning/error 0 |
| Backend targeted 권한 통합 | `1/1` 성공 |
| Backend 전체 | `395/395` 성공 |
| Frontend typecheck·unit | 성공, `103/103` |
| Frontend lint | error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend build | 성공, 기존 500kB chunk warning 유지 |
| Mock UI E2E | panel kitting·project registration `2/2` 성공 |
| Browser desktop/mobile | 합성 Home·account popover·390px Home·drawer 4개 screenshot, 운영 메뉴 11개 확인 |
| Mobile overflow | `390px` viewport에서 document scroll width `390px`; Home full-page capture width `375px`(세로 scrollbar 제외) |

전용 Docker Full-Stack runner는 현재 실행 정책이 container 시작 승인을 요구하지만 approval policy가 `never`여서 시작이 거부됐다. 대표 5081/5174나 Persistent UAT를 대체 사용하지 않았다. 이를 보완해 실제 PostgreSQL을 사용하는 Backend 전체 `395/395`, 역할별 endpoint 통합, Frontend 전체 unit, mock browser E2E와 분리 합성 browser 검증을 완료했다. promotion 전에는 정상 실행 권한이 있는 격리 runner에서 누적 Full-Stack `38/38` 기준을 다시 확인한다.

## 5. 스크린샷

- [영업 Home PC](home-002-change-002-screenshots/home-desktop-sales.png)
- [PC 계정 팝업](home-002-change-002-screenshots/account-popover-desktop.png)
- [영업 Home 모바일](home-002-change-002-screenshots/home-mobile-sales.png)
- [모바일 전체 메뉴](home-002-change-002-screenshots/navigation-drawer-mobile.png)

모든 화면은 `dev-sales`와 `Synthetic` 프로젝트만 사용하는 임시 read-only API preview에서 촬영했다. 실제 사용자·고객·프로젝트·email·UPN·credential·provider payload는 포함하지 않았다.

## 6. Finding gate

| Finding | Severity | 상태 | 해소·후속 |
| --- | --- | --- | --- |
| `OPERATIONAL_MENU_PERMISSION_HIDING` | P2 | RESOLVED | 메뉴 visibility를 업무 read와 분리하고 자재·IQC GET 정책을 project scope 조회로 수정 |
| `HOME_REFERENCE_DENSITY_DRIFT` | P2 | RESOLVED | oversized card·shadow·header를 compact white workspace로 재구성하고 desktop/mobile 시각 검증 |
| `MOCK_E2E_IDENTITY_DRIFT` | P2 | RESOLVED | HOME-002 identity projection과 dev 사용자 전환 후 route 재진입을 fixture에 반영 |
| `FULLSTACK_CONTAINER_POLICY_BLOCKED` | P3 | FOLLOW_UP_BEFORE_PROMOTION | 정상 권한의 격리 runner에서 누적 Full-Stack 재실행; 대표 runtime fallback 금지 |

Open P0/P1/P2: `0/0/0`. Risk acceptance 없음.

## 7. 종료 산출물·rollback

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 |
| SOP | Change 002 반영 | [운영 SOP](../docs/29-personalized-home-profile-sop.md) |
| User manual | Change 002 반영 | [사용자 안내](../docs/30-personalized-home-profile-user-manual.md) |
| Roadmap·완료 원장 | Change 002 반영 | [Roadmap](../docs/00-product-roadmap.md), [완료 원장](../docs/27-experiment-task-ledger.md) |
| User validation checklist | 사용자 검수 대기 | [체크리스트](home-002-user-validation-checklist.md) |

- 코드 rollback은 이 experiment commit의 revert commit으로 수행한다.
- schema/migration 추가는 없다.
- 기존 담당 mutation 권한과 project access scope는 rollback 전후 모두 보존한다.
