# 매출 Dashboard·Mobile·Design Foundation Benchmark

## 결론

기존 `월 확정 매출 막대 + 월 목표 금액 선`은 목표 높낮이만 연결해 월별 성과 차이를 직접 설명하지 못한다. EMI는 월 실적과 월 목표를 같은 금액 축의 grouped bar로 비교하고, 경과 월의 월 달성률을 별도 선으로 표시한다. 연 누계와 목표 차이는 KPI로 분리한다.

## 공식 제품 사례에서 채택한 원칙

1. Microsoft Power BI는 column chart를 시간별 revenue 같은 재무 수치 비교에 사용하고, line/column 계열에 trend·forecast·reference 분석을 결합한다.
   - <https://learn.microsoft.com/en-us/power-bi/visuals/power-bi-visualization-column-charts>
   - <https://learn.microsoft.com/en-us/power-bi/transform-model/desktop-analytics-pane>
2. Tableau는 actual과 target의 직접 비교에 bar와 reference line/bullet graph를 사용하고, running total과 year-over-year 차이를 별도 계산으로 다룬다.
   - <https://help.tableau.com/current/pro/desktop/en-gb/reference_lines.htm>
   - <https://help.tableau.com/current/pro/desktop/en-gb/calculations_tablecalculations_definebasic_runningtotal.htm>
3. Salesforce CRM Analytics의 target dashboard는 누적 actual/target 비교와 month-over-month target/actual/attainment, forecast와 previous year를 서로 구분하고, 상단에는 target·actual·current/projected attainment를 배치한다.
   - <https://trailhead.salesforce.com/content/learn/modules/crm-analytics-dashboards-for-account-manager-targets/evaluate-your-performance>

## EMI 적용·보류

| 항목 | 결정 | 이유 |
| --- | --- | --- |
| 월 actual·target grouped bar | 채택 | 현재 API만으로 월별 gap을 직접 읽을 수 있음 |
| 월 attainment line + 100% 기준 | 채택 | 단순 목표 금액선 대신 어느 달이 선전/부진했는지 보여 줌 |
| 연 누계 actual·target | KPI로 유지 | main chart와 중복을 줄이고 정확한 금액 판단 제공 |
| forecast/projected attainment | 보류 | 현재 확정 매출과 pipeline만 있고 검증된 forecast model이 없음 |
| previous-year overlay | 보류 | 동일 통화·동일 scope의 전년 비교 계약과 API가 아직 없음 |
| 모바일 4×3 month block | 제거 | 12개월 연속 추세를 끊음 |
| 모바일 실제 SVG chart | 채택 | desktop과 같은 분석 문법을 유지하되 390px용 축·label 밀도로 재구성 |

이 benchmark는 새로운 매출 인식·목표 권한·forecast 정책을 만들지 않으며 `TASK-SALES-KPI-001 Change 002`의 Frontend 표시 결정만 고정한다.
