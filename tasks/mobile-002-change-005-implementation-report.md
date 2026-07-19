# TASK-MOBILE-002 Change 005 Implementation report

## 1. 요약과 상태

- 목적: 모바일 화면의 도형을 배열 순서가 아니라 정보·조작·상태 의미에 맞춰 통일한다.
- 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- 계약: [Change 005](mobile-002-change-005.md)
- 범위: mobile navigation, KPI, project·panel·workflow card, badge·count·warning·success marker와 DESIGN-000 semantic shape token.
- 제외: 업무 데이터·상태·권한·URL·API·DB·migration, desktop 기능 삭제, Persistent UAT, 대표 repo·`main`, push·PR·merge.

## 2. 해결한 업무 문제

동일한 메뉴·KPI·프로젝트 상태가 배열 index와 CSS `nth-child`에 따라 원·타원·각진 도형으로 바뀌었다. 사용자는 도형을 상태 신호로 학습할 수 없었고, 차단·완료·선택처럼 구분해야 하는 상태도 우연히 같은 모양으로 보였다. 이번 변경은 의미→도형 계약을 한곳에 고정하고 순서 기반 변형을 제거했다.

## 3. 구현 결과와 영향

- `surface`: 정보·업무 묶음은 8px 둥근 직사각형으로 통일했다.
- `control`: 이동·일반 조작은 6px 직사각형을 사용한다.
- `active`: 현재 선택·진행은 blue rounded square로 표시한다.
- `status`: 짧은 상태·분류 badge는 타원형을 사용한다.
- `count`: 개수·순번·compact avatar는 원형을 사용한다.
- `warning`: 차단·Pending·실패는 우상단 절단형으로 구별한다.
- `success`: 완료·확정은 원형 marker와 안정적인 surface를 조합한다.
- App mobile menu와 키팅·제조·품질 project/panel card는 배열 index 대신 실제 active·progress·blocked·completed 상태에서 역할을 계산한다.
- 영업 KPI와 정산 조건 card는 임의 모양을 제거하고 surface·warning·success 의미를 사용한다.
- 긴 문장·여러 field를 담는 content container는 원·타원으로 만들지 않는다.
- Excel·PDF·첨부와 server contract 영향은 없다.

## 4. 변경 파일

- `frontend/src/design-system/shapes.ts`, `tokens.css`, `index.ts`: semantic role·workflow mapping·geometry token.
- `frontend/src/App.tsx`, `PanelKittingPage.tsx`, `ManufacturingPage.tsx`, `QualityInspectionsPage.tsx`: 실제 상태 기반 shape role.
- `frontend/src/SalesKpiPage.tsx`, `SalesSettlementPage.tsx`: KPI·정산 의미 기반 surface.
- `frontend/src/styles.css`: mobile `nth-child`·임의 shape 제거와 semantic override.
- Frontend unit·Full-Stack E2E 5개: role·computed geometry·상태 전이 회귀.
- `tasks/mobile-002-change-005-screenshots/*.png`: mobile 12개·desktop reference 1개 증빙.

## 5. 실행한 검증

| 검증 | 결과 |
| --- | --- |
| Frontend lint | `PASS` — error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend typecheck | `PASS` |
| Frontend unit | `PASS` — 106/106 |
| Frontend build | `PASS` — 기존 chunk-size warning 유지 |
| Mobile compact workspace E2E | `PASS` — 1/1, 390px 12개 route·desktop reference |
| 키팅·제조·품질 상태 전이 E2E | `PASS` — 3/3 |
| 과거 Task screenshot 보존 | `PASS` — E2E가 재생성한 기존 증빙을 tracked 기준으로 복구 |
| Persistent UAT·대표 runtime | 미실행 — 승인 범위 밖 |

E2E는 disposable PostgreSQL을 사용하고 실행 뒤 container·network·DB를 제거했다. screenshot은 합성 사용자·프로젝트만 사용한다.

## 6. Finding gate

| Finding | Severity | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `M2C5-INDEX-DRIVEN-SHAPES` | P2 | `RESOLVED` | 같은 상태가 배열 순서에 따라 다른 도형으로 표시됨 | semantic role과 workflow state mapping 적용 |
| `M2C5-WARNING-KPI-ASSERTION` | P3 | `RESOLVED` | 최초 E2E가 warning까지 일반 KPI와 같은 radius라고 잘못 가정 | 일반 surface 동일성·warning clip을 각각 검증하도록 수정 |
| `M2-APP-MODULE-SIZE` | P3 | `BACKLOG` | 대형 `App.tsx`·production chunk 유지보수 비용 | 기존 `BACKLOG-MOBILE-002-APP-SPLIT` 유지 |

Open P0/P1/P2는 `0/0/0`이다.

## 7. SOP

1. 390×844에서 Home·Project·Production·Procurement·Materials·Quality·Teams·Admin을 연다.
2. 일반 card·menu·tab이 역할별 동일 geometry인지 확인한다.
3. 선택 항목은 `active`, 짧은 상태는 `status`, 개수는 `count`, 차단은 `warning`, 완료는 `success`인지 확인한다.
4. project/panel 상태를 진행→차단→완료로 바꿔 도형이 index가 아닌 상태를 따라가는지 확인한다.
5. 1440px desktop 기능·layout과 390px overflow 0·44px 주요 target을 확인한다.

## 8. User manual

- 둥근 직사각형은 일반 정보·업무 묶음이다.
- 파란 선택 도형은 현재 위치나 선택된 작업을 뜻한다.
- 타원형은 짧은 상태·분류, 원형은 개수·순번을 뜻한다.
- 우상단이 잘린 도형은 차단·주의·실패 상태다.
- 완료는 원형 성공 marker와 일반 card 조합으로 표시된다.

## 9. User validation checklist

### 자동 검증

- [x] menu·project·panel shape가 index가 아닌 semantic state로 결정됨
- [x] 임의 mobile `nth-child` card geometry 제거
- [x] active·status·count·warning·success 대표 geometry 검증
- [x] 키팅·제조·품질 실제 상태 전이 3/3
- [x] Frontend 106/106·build 통과
- [x] 390px 대표 12개 화면·desktop reference screenshot 생성

### 사용자 직접 검수

- [ ] 같은 의미가 모든 모바일 화면에서 같은 도형으로 읽히는지 확인
- [ ] 차단형의 절단 모서리가 과하거나 부족하지 않은지 확인
- [ ] status pill·count circle이 내용 길이와 충돌하지 않는지 확인
- [ ] 글자·도형 밀도와 한 손 조작성을 확인

상태: `자동 검증 완료 · 사용자 검수 대기 — 마지막 일괄 검수`.

## 10. Rollback·복구

이번 experiment commit을 revert하면 semantic shape role·CSS·test·증빙을 함께 되돌릴 수 있다. Backend·DB·migration 변경이 없어 data rollback은 없다.

## 11. 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 본 문서 |
| SOP | 완료 | 본 문서 7장 |
| User manual | 완료 | 본 문서 8장 |
| Roadmap update | 완료 | [Product Roadmap](../docs/00-product-roadmap.md) TASK-MOBILE-002·Decision Log |
| User validation checklist | 자동 완료·사용자 대기 | 본 문서 9장 |

## 12. 시행착오 및 폐기한 접근

- 상태를 모르고 card 순서만으로 모양을 순환하는 기존 접근을 폐기했다.
- 모든 KPI를 같은 radius로 검사하던 최초 E2E는 warning 의미를 없애므로, 일반 surface와 warning geometry를 분리해 검증했다.
- 완료 card 전체를 원형으로 만드는 안은 긴 content를 담지 못해 원형 marker와 surface 조합으로 제한했다.

## 13. 사용자 검수 결과와 남은 항목

- 자동 검증: 완료
- 사용자 직접 검수: 대기
- P3: `BACKLOG-MOBILE-002-APP-SPLIT`
- 대표 repo·main·Persistent UAT·게시: 미반영
