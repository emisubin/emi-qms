# TASK-PRIVACY-NOTICE-001 Change 007 — 터치 대상 회귀 보정

## 확인된 증상과 원인

- 최신 main 통합 Full-Stack E2E에서 모바일 logo 홈 버튼과 footer 개인정보 안내 텍스트가 44px 터치 대상 기준을 충족하지 못했다.
- Change 003의 투명 텍스트·logo 버튼 CSS가 공통 `data-touch-optimized` 최소 크기보다 높은 우선순위로 `min-width/min-height: 0`을 지정한 것이 원인이다.

## 최소 변경

- pointer가 coarse인 `data-touch-optimized` shell과 860px 이하 모바일 shell에서 두 버튼의 클릭 영역만 최소 44×44px로 복구한다.
- 글꼴·글자 크기·투명 배경·테두리 없음·logo 이미지 크기와 desktop 표현은 유지한다.
- 사용자 요청의 “버튼형식이 아닌 회사 정보와 같은 글씨” 시각 계약은 바꾸지 않는다.

## 검증

- 실패한 모바일 compact workspace와 coarse-pointer desktop 시나리오를 집중 재실행한다.
- Frontend lint·typecheck·unit·build와 Full-Stack 전체 회귀에서 새 규칙을 확인한다.
