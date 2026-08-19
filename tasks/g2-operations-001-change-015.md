# TASK-G2-OPERATIONS-001 Change 015 — 그래프 고정 틀·모바일 내부 탐색과 홈 납품표 보정

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

- 생산·납품·재고 그래프에서 `0~60 확대 · 60~180 압축` 문구와 `//` 축 압축 marker를 제거하되, 큰 수량이 더 높게 보이는 공통 단조 scale 계산은 유지한다.
- 생산·납품 막대 위 수량은 더 작은 글자로 표시하고 두 값이 가까울 때 세로 위치를 분리해 서로 겹치지 않게 한다.
- 파란 실사 점은 넓은 투명 hit area를 가지며, 실사값이 `0`이어도 hover 정보에 `실사 0대`로 표시한다.
- 모바일에서는 그래프 카드·높이·왼쪽 축·오른쪽 축·plot frame을 한 화면에 고정한다. 가운데 날짜 데이터 영역만 가로로 움직이며 첫 화면에는 5일분이 보인다.
- 모바일 5일 window에 맞춰 생산·납품 막대와 오전·오후 누적 막대를 넓힌다. Desktop은 기존 전체 월 표시 폭을 유지한다.
- 모바일의 축 숫자·날짜·생산/납품 수량·재고 수량·조별 막대 내부 수량은 5일 window에 맞춰 Desktop보다 크게 표시한다.
- 새 내부 탐색 layer도 G2 pastel color 예외에 포함해 공통 monochrome filter가 생산 파랑·납품 주황·재고 빨강과 조별 파랑을 제거하지 않게 한다.
- 홈 `생산 현황` 표에 `납품` 행을 추가하고 기존 날짜 filter·휴일 열 red text를 그대로 적용한다.
- 생산/출하 관리와 제조 인원 출근 관리 월간표는 날짜 header가 미래 날짜에 `예상`을 표시하므로 중복 `구분` 행을 모두 제거한다.

## 2. 신규 기능 경계

- 관리자 전용 입력·수정 이력 조회는 현재값과 마지막 수정 정보만 보존하는 승인 기획을 넘어서는 별도 `NEW_FEATURE`다.
- 과거값 append-only 저장, 조회 대상·before/after·사유·보존 기간·필터, 관리자 permission·API·화면을 새로 확정해야 하므로 Change 015에서 임의 구현하지 않는다.
- 현재 Roadmap의 G2 다음 Gate는 사용자 검수 → 별도 Git 게시 승인이고 작업공간도 미커밋 상태이므로, 신규 Task Identity/Roadmap Sequence Gate와 Fable interview는 현재 변경을 안전하게 보존한 뒤 별도 승인으로 시작한다.

## 3. 보존 범위

- G2 API·DB·migration·권한·실사 저장·재고 계산과 입력 화면은 변경하지 않는다.
- 공통 2단계 scale의 수치 mapping과 좌우 tick 값은 유지한다.
- 원격 `main`, Persistent UAT와 Azure 공개배포는 변경하지 않는다.

## 4. 검증 결과

- Frontend 전체 unit `226/226`, typecheck, lint error `0`, production build를 통과했다. 기존 Fast Refresh warning 1건과 chunk-size warning은 유지된다.
- mobile layout unit에서 31일 기준 fixed frame과 좌우 축 layer를 별도 유지하고 내부 content width `620%`, 첫 고정 plot viewport의 날짜 중심점 `5`개를 확인했다.
- desktop live 화면에서 SVG `720×340` 두 개 유지, scale 안내·axis break `0/0`, 막대 숫자 겹침 `0`, G2 color filter 해제, page overflow `0`, 생산표 `납품` row `1`을 확인했다.
- 파란 실사 point가 line hover layer보다 위에 있고 실사 원본값 `0`을 `실사 0대`로 표시하는 unit 회귀를 통과했다.

## 5. 다음 Gate

사용자는 실행 중인 local 검수 화면에서 모바일 fixed frame·5일 내부 drag·막대 폭과 Desktop 표시를 확인한다. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.
