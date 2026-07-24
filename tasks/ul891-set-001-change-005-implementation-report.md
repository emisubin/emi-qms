# TASK-UL891-SET-001 Change 005 구현 보고 — 프로젝트 상세 탭 정렬

## 구현 결과

1. 프로젝트 상세의 `전체 흐름·생산관리·설계·구매·제조·품질·물류·영업` 본문을 `project-detail-tab-content` 공통 컨테이너로 묶고 선택 section을 `data-section`으로 고정했다.
2. 각 탭의 첫 section에 같은 좌우 inset, 상단 간격, 제목 경계선과 콘텐츠 폭을 적용했다. 표·카드·빈 상태는 부모 폭 안에서 줄어들며 긴 내용이 바깥 폭을 만들지 않는다.
3. 390px에서는 간격을 줄이고 제목·설명·액션을 세로 우선으로 배치해 desktop 폭 축소형이 되지 않게 했다.

## 검증

- 고정 검수 runtime `http://127.0.0.1:42983`에서 실제 UL891 38면 프로젝트의 8개 탭을 차례로 전환했다.
- 각 탭은 요청 탭과 선택 탭이 일치했고 공통 container의 첫 콘텐츠 시작선 편차가 `0~1px`, 문서 horizontal overflow가 모두 `0px`였다.
- 실제 역할 18단계 isolated E2E가 desktop·390px에서 8개 프로젝트 탭을 캡처하고 각 탭 overflow `0px`를 통과했다.
- 대표 증빙: `/tmp/emi-qms-lifecycle-evidence/19-project-tab-workflow.jpg`부터 `26-project-tab-sales-mobile.jpg`까지.

## Finding·경계

- Open P0/P1/P2: `0/0/0`.
- 탭 내부 업무 데이터·권한·상태 전이·패널 상세 deep link는 변경하지 않았다.
- 사용자 직접 검수는 대기한다. 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge는 변경하지 않았다. main merge 승인 `0/3`이다.

## 사용자 검수 후 시각 결함 보정

- 증상: 행의 숫자 진행률은 `100%`인데 막대 채움이 보이지 않았다.
- 원인: `wireframe.css`의 전역 `span/i/em/b` 투명 배경 규칙이 진행률의 `<i><b /></i>` 트랙과 채움에도 `!important`로 적용됐다. 계산값과 inline width는 정상이다.
- 수정: 프로젝트 진행률 트랙과 채움을 의미 기반 상태 표현 예외로 등록했다. 기본은 흑백, success·danger·warning·info는 기존 상태색만 사용한다.
- 회귀 방지: 전체 수명주기 E2E가 `100%` meter의 실제 채움 폭과 불투명 배경을 검사하도록 보강했다.
- 검증: 보강된 실제 역할 18단계 E2E `1/1 PASS`, desktop·390px 품질 화면에서 `100%` 채움 확인, typecheck·build·lint error 0.
