# TASK-MOBILE-002 Change 004 Implementation report

## 1. 요약과 상태

- 목적: PC 기능을 전부 복제하지 않고 모바일에서 지금 판단·현장 처리·이동에 필요한 기능만 우선 배치한다.
- 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- 계약: [Change 004](mobile-002-change-004.md)
- 범위: 공통 mobile presentation, Home·Sales·20개 선택 export 화면·대표 운영/관리 workspace.
- 제외: Backend·API·DB·migration·권한 변경, desktop 기능 삭제, Persistent UAT, 대표 repo·`main`, push·PR·merge.

## 2. 해결한 업무 문제

모바일에 desktop의 Excel·대량 관리·보조 설명·반복 Home widget까지 그대로 노출돼 핵심 현장 정보가 아래로 밀렸다. mobile simple-mode를 공통화해 현장 action과 판단 정보는 보존하고 PC 관리 기능은 기본 화면에서 제외했다.

## 3. 구현 결과와 영향

- Home: 긴급 Pending·알림을 `지금 먼저 확인하세요`에 합치고 중복 widget 두 개를 제거했다.
- Sales: 실제 12개월 graph, 연 매출·달성률·잔여 목표 3개 KPI, month evidence disclosure만 우선 노출하고 목표 관리는 desktop에 유지했다.
- Export 20개 route: mobile 선택 tray·checkbox·Excel action을 숨기고 desktop의 전체선택 checkbox·선택 export는 그대로 유지했다.
- 생산계획·구매: mobile은 프로젝트 목록과 현장 카드·action을 유지하고 설정·template download/upload를 제외했다.
- 관리자 사용자: mobile의 전체 field toggle·bulk selection을 제외하고 사용자·상태·알림 설정을 우선했다.
- 공통 header: mobile eyebrow·긴 설명을 줄이고 제목·오류·권한·feedback은 보존했다.
- 서버 기능·권한은 제거하지 않았으며 desktop에서 기존 관리 기능을 계속 사용할 수 있다.
- Excel/PDF/첨부 영향: mobile 진입점을 숨겼을 뿐 workbook·PDF·upload 계약은 변경하지 않았다.

## 4. 변경 파일

- `frontend/src/design-system/tokens.css`: mobile simple-mode visibility·density rule.
- `frontend/src/App.tsx`: 생산계획·구매 mobile 관리 action 제외.
- `frontend/src/HomePage.tsx`, `SalesKpiPage.tsx`, `SalesKpiChart.tsx`: mobile 핵심 정보 재배치.
- export·mobile Full-Stack specs 5개와 `frontend/tests/App.test.tsx`: desktop 보존·mobile 생략 회귀.
- `tasks/mobile-002-change-004-screenshots/*.png`: mobile 12개·desktop reference 1개 증빙.

## 5. 실행한 검증

| 검증 | 결과 |
| --- | --- |
| Frontend typecheck | `PASS` |
| Frontend lint | `PASS` — error 0, 기존 warning 1 |
| Frontend unit | `PASS` — 104/104 |
| Frontend build | `PASS` — 기존 chunk warning 유지 |
| Mobile compact workspace E2E | `PASS` — 1/1, 390px 대표 route·desktop reference |
| 20개 화면 선택 export E2E | `PASS` — desktop export 유지, mobile export 0·overflow 0 |
| Excel·선택 프로젝트 E2E | `PASS` — 2/2, desktop workbook 유지·mobile bulk 생략 |
| Sales·Design E2E | `PASS` — 1/1 |
| Persistent UAT·대표 runtime | 미실행 — 승인 범위 밖 |

E2E는 disposable PostgreSQL을 사용하고 실행 뒤 container·network·DB를 제거했다. screenshot은 합성 사용자·프로젝트만 사용한다.

## 6. Finding gate

| Finding | Severity | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `M2C4-DESKTOP-FEATURE-CROWDING` | P2 | `RESOLVED` | Excel·설정·대량 작업이 좁은 화면의 핵심 action을 밀어냄 | mobile simple-mode에서 PC 관리 기능 제외 |
| `M2C4-HOME-DUPLICATE-SUMMARY` | P2 | `RESOLVED` | 긴급 panel과 Pending·알림 widget이 같은 수치를 반복 | mobile에서는 우선 확인 panel 하나로 통합 |
| `M2C4-SALES-TREND-FRAGMENT` | P2 | `RESOLVED` | 월 block이 연속 추세를 끊음 | 실제 12개월 SVG graph 적용 |
| `M2-APP-MODULE-SIZE` | P3 | `BACKLOG` | 대형 `App.tsx` 유지보수 비용 | 기존 `BACKLOG-MOBILE-002-APP-SPLIT` 유지 |

Open P0/P1/P2는 `0/0/0`이다.

## 7. SOP

1. 390×844에서 Home·Sales·Project·Production·Procurement·Materials·Quality·Teams·Admin을 연다.
2. 왼쪽 위 drawer로 전체 메뉴에 접근하고 현재 화면의 핵심 조회·현장 action을 확인한다.
3. Excel·대량 편집·설정이 mobile 기본 화면에 없고 desktop 1440px에는 유지되는지 확인한다.
4. page-level horizontal overflow가 0이고 주요 button이 44px 이상인지 확인한다.
5. `bash scripts/e2e-full-stack.sh e2e/full-stack/mobile-compact-workspaces.full-stack.spec.ts`와 export 3개 spec으로 회귀 검증한다.

## 8. User manual

- 모바일은 왼쪽 위 메뉴로 전체 업무 화면을 이동한다.
- 첫 화면에는 상태·기한·차단·다음 action이 먼저 나온다.
- Excel 내보내기, 대량 선택, 상세 설정과 template 관리는 desktop에서 수행한다.
- 모바일 Home은 긴급 항목을 한 panel에 모아 보여 주고 같은 Pending·알림 요약을 반복하지 않는다.
- 영업 graph는 한 화면에서 12개월을 비교하고 아래 selector로 월 근거를 확인한다.

## 9. User validation checklist

### 자동 검증

- [x] mobile 20개 export route에서 대량 export action 0
- [x] desktop 20개 export route에서 기존 기능 유지
- [x] 생산계획·구매 mobile 설정·template action 제외
- [x] 관리자 mobile bulk·전체 field toggle 제외
- [x] Home 중복 widget 제거와 Sales 실제 graph
- [x] 390px overflow 0·대표 screenshot 생성

### 사용자 직접 검수

- [ ] Home 첫 화면의 긴급·내 업무·프로젝트 순서 확인
- [ ] 생산·구매·자재·품질·관리 화면에서 빠진 기능이 mobile 현장 업무에 필요하지 않은지 확인
- [ ] desktop에서 Excel·관리 기능이 그대로 동작하는지 확인
- [ ] 글자·button·card 밀도와 한 손 조작성을 확인

상태: `자동 검증 완료 · 사용자 검수 대기 — 마지막 일괄 검수`.

## 10. Rollback·복구

experiment commit revert로 presentation rule과 테스트·증빙을 되돌린다. Backend·DB·migration 변경이 없어 data rollback은 없다. 숨긴 기능은 desktop에 계속 존재한다.

## 11. 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 본 문서 |
| SOP | 완료 | 본 문서 7장 |
| User manual | 완료 | 본 문서 8장 |
| Roadmap update | 완료 | [Product Roadmap](../docs/00-product-roadmap.md) TASK-MOBILE-002·Decision Log |
| User validation checklist | 자동 완료·사용자 대기 | 본 문서 9장 |

## 12. 시행착오 및 폐기한 접근

- 화면마다 개별 CSS로 action을 숨기는 대신 공통 semantic selector와 명시적 route 조건을 조합해 desktop 회귀를 한곳에서 검증했다.
- 영업 mobile graph를 월 card로 더 압축하는 안은 추세를 잃어 폐기했다.
- 관리자 기능 전체를 mobile에서 제거하는 안은 알림 설정 같은 즉시 필요한 action까지 잃으므로 선택적으로 단순화했다.

## 13. 사용자 검수 결과와 남은 항목

- 자동 검증: 완료
- 사용자 직접 검수: 대기
- P3: `BACKLOG-MOBILE-002-APP-SPLIT`
- 대표 repo·main·Persistent UAT·게시: 미반영
