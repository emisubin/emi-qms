# TASK-G2-OPERATIONS-001 Change 019 — 재고 부족분 카드 내부 안내 보정

- taskType: `BUGFIX`
- changeStatus: `IMPLEMENTED_VALIDATED_USER_REVIEW_PENDING`
- userInstructionDate: 2026-08-19
- canonicalTaskId: `TASK-G2-OPERATIONS-001`
- implementationApproved: true
- commitApproved: false
- pushApproved: false
- pullRequestApproved: false
- mergeApproved: false
- persistentUatApproved: false
- azureDeploymentApproved: false

## 1. 사용자 지정 화면 계약

- 재고 부족분 `i` 안내가 그래프나 다른 KPI를 덮지 않게 한다.
- 안내는 재고 부족분 KPI 카드 안에서만 열리게 한다.
- hover와 keyboard focus에서 같은 내용을 제공한다.

## 2. 구현 결정

- 재고 부족분 카드만 `i`의 위치 기준을 카드 전체로 올리고, 안내를 카드 안쪽 `6px` 여백의 overlay로 표시한다.
- 안내는 불투명한 pastel violet 배경·보라색 테두리·짙은 보라색 글씨를 사용해 원래 KPI 값과 명확히 구분한다.
- 안내 자체는 pointer event를 받지 않아 hover 진입점과 주변 graph interaction을 방해하지 않는다.
- 공통 Graphite의 monochrome·inline normalization이 G2 안내의 배경과 위치 기준을 바꾸지 않도록 G2 전용 semantic exception을 적용한다.

## 3. 보존 범위

- 오늘 기준 재고 부족분 산식, 수량, `i`의 접근 가능한 이름·설명 연결을 유지한다.
- 다른 KPI tooltip, graph 크기·위치·hover, 날짜 filter와 mobile 내부 drag는 변경하지 않는다.
- Backend·DB·migration·권한·입력 화면은 변경하지 않는다.
- 원격 `main`, Persistent UAT와 Azure 공개배포는 변경하지 않는다.

## 4. 검증 결과

- local Desktop에서 KPI 카드는 `208×74`, 열린 안내는 카드 안쪽 `194×60`으로 측정됐고 네 방향 모두 카드 경계 안에 포함됐다.
- 안내 배경 `rgb(245, 243, 255)`, 테두리 `rgb(167, 139, 250)`, 글씨 `rgb(76, 29, 149)`, page 가로 overflow `0`을 확인했다.
- G2 targeted unit `8/8`, Frontend 전체 unit `226/226`, typecheck, lint error `0`, production build를 통과했다. 기존 Fast Refresh warning 1건과 대형 chunk warning은 유지된다.

## 5. 다음 Gate

자동 검증과 local 검수 runtime 반영을 완료했으며 사용자 검수를 받는다. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.
