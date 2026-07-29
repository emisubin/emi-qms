# TASK-SALES-KPI-001 Change 002 Implementation report

## 1. 요약과 상태

- 목적: 의미가 약한 월 목표 금액선을 실제·목표·달성률을 함께 판단하는 연간 영업 graph로 교체한다.
- 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- 계약: [Change 002](sales-kpi-001-change-002.md), [benchmark](../docs/34-sales-dashboard-mobile-design-benchmark.md)
- 범위: Frontend 표시·접근성·desktop/mobile 증빙만 변경했다.
- 제외: Backend, API, DB, migration, 매출 인식, 목표 권한·CAS, Persistent UAT, 대표 repo, `main`, push·PR·merge.
- 사용자 검수: 마지막 일괄 검수 대기. `main` merge 승인 `0/3`.

## 2. 해결한 업무 문제

기존 목표 금액선은 월별 목표의 크기만 연결해 실적 gap이나 달성 여부를 직접 보여 주지 못했다. 모바일 4×3 월 block은 연속 추세를 끊었다. 공식 BI·CRM 사례를 비교한 뒤 actual·target은 같은 금액 축의 grouped bar, attainment는 percentage line, 달성 기준은 100% reference로 분리했다.

## 3. 기술적 결정과 영향

- 경과 월 중 목표가 있는 달만 `actual / target × 100`으로 연결한다. 미래 월은 0%로 오해되지 않게 선에서 제외한다.
- 선택 월은 강조 막대와 정확한 금액·달성률 텍스트를 함께 제공한다.
- 연간 누계·목표 달성률·잔여 목표는 KPI card에 유지해 graph의 시각 부담을 줄였다.
- 모바일도 12개월 전체 SVG를 사용하고 작은 bar 대신 44px month selector로 근거를 선택한다.
- forecast·전년 비교는 검증된 API·동일 scope 계약이 없어 구현하지 않았다.
- Excel/PDF/첨부·workflow·권한 영향: `N/A` — 기존 계약과 server mutation을 변경하지 않았다.

## 4. 변경 파일

- `frontend/src/SalesKpiChart.tsx`: grouped bar, attainment line, 100% reference, desktop/mobile SVG와 접근성 설명.
- `frontend/src/SalesKpiPage.tsx`: KPI 우선순위, mobile month selector·근거 disclosure, desktop 목표 관리 보존.
- `frontend/src/HomePage.tsx`: 영업 Home의 동일 분석 graph와 mobile 핵심 KPI 2개.
- `frontend/src/design-system/*`: 공통 token·surface·badge 사용.
- `frontend/tests/App.test.tsx`, `frontend/e2e/full-stack/sales-kpi-form-templates.full-stack.spec.ts`: 계약·시각 회귀.
- `tasks/design-000-screenshots/01~04-*.png`: desktop/mobile 영업 화면 증빙.

## 5. 실행한 검증

| 검증 | 결과 |
| --- | --- |
| Frontend typecheck | `PASS` |
| Frontend lint | `PASS` — error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend unit | `PASS` — 12 files, 104 tests |
| Frontend production build | `PASS` — 기존 large chunk warning 유지 |
| Sales·Design isolated Full-Stack E2E | `PASS` — 1/1 |
| Desktop 1440px·mobile 390px screenshot | `PASS` — Home·영업 전용 4개 |
| Persistent UAT·대표 runtime | 미실행 — 승인 범위 밖 |
| Backend 전체 | 미실행 — Backend 변경 없음 |

격리 PostgreSQL container·network·tmpfs는 실행 후 제거했다. 실제 사용자·고객·프로젝트·credential은 증빙에 포함하지 않았다.

## 6. Finding gate

| Finding | Severity | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `SALES-C2-TARGET-LINE-NO-DECISION` | P2 | `RESOLVED` | 목표 높낮이 선은 월별 성과 판단에 직접 도움이 되지 않음 | actual·target bar + attainment line + 100% 기준으로 교체 |
| `SALES-C2-MOBILE-BLOCK-TREND` | P2 | `RESOLVED` | 4×3 block이 연속 추세를 분절 | 12개월 실제 SVG graph 적용 |
| `SALES-C2-FUTURE-ZERO-MISREAD` | P2 | `RESOLVED` | 미래 월을 0%로 연결하면 부진으로 오인 | 경과 월까지만 달성률 line 렌더링 |

Open P0/P1/P2는 `0/0/0`이며 risk acceptance는 없다.

## 7. SOP

1. 영업 사용자로 Home 또는 `영업`에 들어간다.
2. 연도·통화를 선택하고 blue actual bar, gray target bar, red attainment line과 100% reference를 확인한다.
3. Desktop은 월 막대를, mobile은 `근거를 확인할 월` selector를 사용해 월별 근거를 확인한다.
4. 목표 수정은 desktop의 기존 관리자 flow에서만 수행한다.
5. 회귀 검증은 `bash scripts/e2e-full-stack.sh e2e/full-stack/sales-kpi-form-templates.full-stack.spec.ts`로 실행한다.

## 8. User manual

- 파란 막대는 확정 매출, 회색 막대는 월 목표다.
- 빨간 선은 경과 월의 달성률이며 점선 100%보다 높으면 해당 월 목표를 넘긴 것이다.
- 미래 월에는 목표 막대만 남을 수 있으며 달성률 선은 표시하지 않는다.
- 모바일도 같은 12개월 흐름을 보여 주며 아래 월 선택으로 정확한 근거를 연다.
- 예상 파이프라인은 확정 매출·달성률에 포함되지 않는다.

## 9. User validation checklist

### 자동 검증

- [x] actual·target grouped bar와 attainment line 표시
- [x] 100% reference와 미래 월 line 제외
- [x] desktop 월 선택과 mobile 44px selector
- [x] Home·영업 화면 숫자·graph 문법 일치
- [x] desktop/mobile screenshot과 Frontend 회귀

### 사용자 직접 검수

- [ ] 월별 actual·target gap이 기존보다 빠르게 읽히는지 확인
- [ ] 100% 기준과 달성률 선이 영업 판단에 유용한지 확인
- [ ] 390px에서 12개월 label·범례·월 선택이 읽기 쉬운지 확인
- [ ] 금액 KPI 3개와 파이프라인 우선순위 확인

상태: `자동 검증 완료 · 사용자 검수 대기 — 마지막 일괄 검수`.

## 10. Rollback·복구

현재 experiment commit을 revert하면 Frontend와 증빙을 함께 되돌릴 수 있다. DB·migration 변경이 없어 data rollback은 없다. 대표 repo·`main`은 미변경이다.

## 11. 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 본 문서 |
| SOP | 완료 | 본 문서 7장 |
| User manual | 완료 | 본 문서 8장 |
| Roadmap update | 완료 | [Product Roadmap](../docs/00-product-roadmap.md) TASK-SALES-KPI-001·Decision Log |
| User validation checklist | 자동 완료·사용자 대기 | 본 문서 9장 |

## 12. 시행착오 및 폐기한 접근

- mobile month card를 더 작게 유지하는 안은 연속 추세를 복원하지 못해 폐기했다.
- 목표 금액을 line으로 유지하고 actual만 bar로 두는 안은 단위는 같아도 사용자가 달성률을 머릿속으로 계산해야 해 폐기했다.
- forecast·전년 비교는 업계 사례에는 있으나 현재 EMI 데이터 계약으로 정확성을 보장할 수 없어 보류했다.

## 13. 사용자 검수 결과와 남은 항목

- 자동·격리 화면 검증: 완료
- 사용자 직접 검수: 대기
- known issue: 없음
- 별도 후속 후보: forecast·전년 비교는 권위 있는 데이터 계약이 생길 때만 신규 change로 검토
- 게시·UAT·main 반영: 미승인·미실행
