# DESIGN-000 Change 001 Implementation report — Black & White Wireframe

## 1. 요약과 상태

- 목적: 판단에 필요한 상태색만 남기고 제품 전체를 흑백·무그림자·사각형 와이어프레임으로 전환한다.
- 상태: `EXPERIMENT_COMPLETE / USER_VALIDATION_PENDING`
- 계약: [DESIGN-000 Change 001](design-000-change-001.md)
- 범위: 인증 화면, desktop shell, mobile shell, 모든 기존 업무 화면의 마지막 CSS cascade layer.
- 제외: 레이아웃·문구·기능·권한·URL·API·DB·migration·workflow 변경, 대표 repo·`main`·push·PR·merge·Persistent UAT.

## 2. 구현

- `wireframe.css`를 기존 `styles.css`와 `tokens.css` 뒤에 로드해 기존 기능별 레이아웃을 보존하고 시각 표현만 교체했다.
- 비상태 token을 흰색·검정·중성 회색으로 재정의했다.
- 모든 기본 요소의 radius와 shadow를 0으로 고정하고 배경 gradient를 제거했다.
- 카드·표면·입력·선택·버튼·탭·메뉴는 1px border 기반 사각형으로 통일했다.
- primary·active는 검정 배경/흰 글자, secondary는 흰 배경/검정 글자로 통일했다.
- 차트와 이미지의 장식색은 grayscale 처리했다.
- 성공·주의·오류·진행·미읽음과 검사 적합/부적합처럼 판단에 필요한 상태 표시만 녹색·황색·적색·청색 의미색 예외를 유지했다.
- 프로필 사진은 사용자 정체성을 나타내는 예외로 원형을 유지하고, 로그인 loading indicator도 원형 motion을 유지했다.

## 3. 변경 파일

- `frontend/src/main.tsx`
- `frontend/src/design-system/wireframe.css`
- `frontend/tests/design-system-wireframe.test.ts`
- `tasks/design-000-change-001.md`
- `tasks/design-000-change-001-screenshots/*`
- `docs/00-product-roadmap.md`
- `docs/27-experiment-task-ledger.md`

## 4. 검증 결과

| 검증 | 결과 |
| --- | --- |
| DESIGN-000 focused test | `PASS` — 3/3 |
| Frontend typecheck | `PASS` |
| Frontend lint | `PASS` — error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend unit | `PASS` — 19 files, 123/123 |
| Frontend production build | `PASS` — 기존 500kB chunk warning 유지 |
| Desktop visual projection | `PASS` — 1440×1000, overflow `false`, primary `#111111`, radius `0`, shadow `none` |
| Mobile visual projection | `PASS` — 390×844, overflow `false`, unexpected rounded element `0` |
| Semantic status projection | `PASS` — 비상태 color sample은 미읽음 배지를 제외하고 0, `Active`는 green semantic color 유지 |
| Frontend runtime | `PASS` — `http://127.0.0.1:42983` HTTP 200 |
| Backend runtime | `REACHABLE` — `http://127.0.0.1:41166/api/projects`가 인증 경계 HTTP 401 반환 |

## 5. Screenshot

- [Desktop Home 1440](design-000-change-001-screenshots/01-home-desktop-1440.jpg)
- [Mobile Home 390](design-000-change-001-screenshots/02-home-mobile-390.jpg)
- [Desktop Pending status](design-000-change-001-screenshots/03-pending-status-desktop-1440.jpg)

증빙은 개발용 합성 사용자·데이터만 사용하며 실제 개인정보·secret을 포함하지 않는다.

## 6. Finding gate

| Finding | Severity | 상태 | 해소·후속 |
| --- | --- | --- | --- |
| `D000-C001-HARDCODED-BLUE` | P2 | `RESOLVED` | Pending 단계 번호·선택 원·프로젝트 순번의 하드코딩 색을 전역 비상태 흑백 규칙으로 제거했다. |
| `D000-C001-ACTIVE-CONTRAST` | P2 | `RESOLVED` | 활성 메뉴 자식이 검정색을 재상속하던 문제를 active descendant white rule로 보정했다. |
| `D000-C001-MOBILE-GRADIENT` | P2 | `RESOLVED` | 모바일 메뉴 trigger의 고우선 gradient를 검정 단색으로 강제했다. |
| `D000-C001-CHUNK-SIZE` | P3 | `BACKLOG` | 기존 production bundle 500kB 경고는 기능 분할 housekeeping 후보로 유지한다. |
| `D000-C001-FAST-REFRESH` | P3 | `BACKLOG` | 기존 `main.tsx` Fast Refresh warning 1건은 별도 housekeeping 후보로 유지한다. |

Open P0/P1/P2는 `0/0/0`이다.

## 7. 사용자 검수 체크리스트

- [ ] Desktop에서 Home·프로젝트·Pending·부서별 업무 화면이 흑백·사각형·무그림자로 보인다.
- [ ] Mobile에서 메뉴·카드·버튼·입력 요소가 사각형이고 화면 가로 넘침이 없다.
- [ ] 활성 메뉴와 주요 버튼이 검정 배경/흰 글자로 명확히 보인다.
- [ ] 성공·주의·오류·진행·미읽음 상태만 의미색으로 구분된다.
- [ ] 프로필 사진 원형 예외와 로그인 loading 원형 예외가 사용자 식별·진행 상태에 적합하다.

상태: `자동 검증 완료 · 사용자 검수 대기`.

## 8. SOP·User manual

- 새 UI는 구조적 강조에 색을 추가하지 않고 검정/흰색/회색과 1px border를 사용한다.
- radius와 shadow를 새로 추가하지 않는다. 원형 예외는 사용자 정체성 또는 진행 motion처럼 의미가 있는 경우에만 허용한다.
- 의미색은 성공·주의·오류·진행·미읽음 등 사용자의 판단이 필요한 상태 표시 안에서만 사용한다.
- 사용자는 검정 배경을 현재 위치·주요 action으로, 색 있는 작은 표식을 업무 상태로 해석한다.

## 9. Rollback

`main.tsx`의 `wireframe.css` import와 해당 stylesheet를 revert하면 기존 DESIGN-000 색·도형 체계로 즉시 돌아간다. Backend·DB·migration·data rollback은 없다.

## 10. 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 본 문서 |
| SOP | 완료 | 본 문서 8장 |
| User manual | 완료 | 본 문서 8장 |
| Roadmap update | 완료 | Product Roadmap DESIGN-000·Decision Log |
| User validation checklist | 자동 완료·사용자 대기 | 본 문서 7장 |
