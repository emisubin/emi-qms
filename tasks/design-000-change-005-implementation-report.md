# DESIGN-000 Change 005 구현 보고서

## 1. 결과와 상태

- Task 유형: `P2_REMEDIATION`
- 기준 HEAD: `a7651b5c266d73be48e76861a02910435c1371fe`
- 상태: `EXPERIMENT_COMPLETE / USER_VALIDATION_PENDING`
- 변경 계약: [DESIGN-000 Change 005](design-000-change-005.md)
- 대표 repo·`main`·Persistent UAT·실제 provider 영향: 없음
- main merge 승인: `0/3`

물류 입력과 영업 정산의 공통 입력 header가 좁은 열에서 찌그러지는 두 P1 UI 결함을 해소했다. 검은 배경을 사용하는 활성·강조 요소는 배경과 내부 글자색을 하나의 명시적 계약으로 묶어 검은 글자가 검은 면 위에서 사라지는 문제도 함께 보정했다. API·DB·권한·업무 상태와 저장 동작은 변경하지 않았다.

## 2. 수정 내용

| Finding | 원인 | 구현 결과 |
| --- | --- | --- |
| `DESIGN-INPUT-FLOW-NARROW-COLLAPSE` | 공통 입력 header가 `제목 1fr + 최소 290px 단계 안내`를 강제해 약 310px 물류 열과 정산 2열 한 칸에서 제목 폭이 소멸 | `DsInputFlow`에 container query를 적용해 620px 이하에서는 제목과 3단계를 세로 배치했다. 물류 입력 열은 최소 340px로 넓히고 action panel은 항상 compact header를 쓴다. 정산의 입력 흐름은 form 2열 전체를 차지한다. |
| `DESIGN-WIREFRAME-DARK-SURFACE-TEXT-CONTRAST` | 전역 wireframe cascade가 검은 배경과 자식 글자색을 서로 다른 selector에서 처리 | 활성 양식·생산계획·물류·IQC·Excel·프로필의 검은 표면에 흰색 foreground를 함께 강제했다. 물류 현재 단계의 흰 내부 표식은 검은 글자를 유지한다. |

## 3. 변경 파일

| 파일 | 역할 |
| --- | --- |
| `frontend/src/design-system/wireframe.css` | container-aware 입력 header, 검은 표면 foreground 계약 |
| `frontend/src/styles.css` | 물류 action panel 폭·header, 정산 입력 흐름 전체 열 배치 |
| `frontend/tests/design-system-wireframe.test.ts` | 좁은 입력과 검은 표면 CSS 계약 회귀 |
| `frontend/e2e/full-stack/project-lifecycle-user-validation.full-stack.spec.ts` | desktop·390px 실제 물류·정산 폭/overflow·검은 표면 대비 검증 |

## 4. 자동·시각 검증

| 검증 | 결과 |
| --- | --- |
| Frontend lint | `PASS` — error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend typecheck | `PASS` |
| Frontend unit | `PASS` — 22 files, `142/142` |
| Frontend production build | `PASS` — 기존 large chunk warning 유지 |
| 집중 test | `PASS` — design wireframe·물류·정산 `8/8` |
| 일반 실제 역할 lifecycle | `PASS 1/1` — 18단계·프로젝트 완료·open Pending 0 |
| 12면 stress lifecycle | `PASS 1/1` — 분할 입고 6회·Pending 6건·18단계 완료 |
| 물류 desktop·390px | 제목 영역 120px 초과, header overflow 0, 문서 수평 overflow 0 |
| 정산 desktop·390px | 제목 영역 120px 초과, header overflow 0, 문서 수평 overflow 0 |
| 지정 검은 표면 | visible text 대비 4.5:1 미만 `0건` |
| 고정 runtime | Frontend root·proxy ready `200`, Backend live·ready `200` |
| `git diff --check` | `PASS` |

검증 screenshot은 disposable E2E 합성 데이터로 `/tmp/emi-qms-lifecycle-evidence/`에 생성했다. Repository에는 개인정보 또는 실행별 browser artifact를 추가하지 않았다.

## 5. Finding gate

| Finding | Severity | 상태 |
| --- | --- | --- |
| `DESIGN-INPUT-FLOW-NARROW-COLLAPSE` | P1 | `RESOLVED` |
| `DESIGN-WIREFRAME-DARK-SURFACE-TEXT-CONTRAST` | P2 | `RESOLVED` |

Open P0/P1/P2: `0/0/0`.

## 6. 사용자 검수

- [ ] Frontend `http://127.0.0.1:42983`에서 물류 포장·출발·납품 입력 제목과 3단계가 겹치지 않는지 확인
- [ ] 세금계산서 발행 확인 입력에서 제목·3단계·입력값이 정상 정렬되는지 확인
- [ ] 양식 관리, 생산계획, 물류, IQC 등 검은 강조 면의 글자가 모두 보이는지 확인
- [ ] 390px에서 물류·정산 입력이 좌우로 넘치지 않는지 확인

상태: `자동 검증 완료 · 사용자 검수 대기`.

## 7. Rollback

Change 005의 CSS·회귀 test와 문서만 되돌린다. 데이터 rollback과 migration은 없다.

## 8. 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 포함됨 | 2·4·7절 |
| User manual | 포함됨 | 6절 |
| Roadmap update | 작성됨 | `docs/00-product-roadmap.md` |
| User validation checklist | 포함됨 / 대기 | 6절 |
