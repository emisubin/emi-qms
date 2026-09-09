# Frontend 작업 계약

[Root 지침](../AGENTS.md)을 적용한다. React·TypeScript의 기존 route·component·API client를 먼저 찾는다.

## 기존 UI와 사용자 동작

- 청주 화면과 같게 하라는 요청은 공용 목록·상세·탭·표 구성을 실제로 재사용한다. 비슷하게 새로 그린 화면을 동일하다고 판정하지 않는다.
- 해당 화면의 같은 데이터 형태·상태·viewport에서 비교한다. 행 높이, 열 정렬, toolbar, 탭, 여백과 선택 동작을 직접 확인한다.
- 범위 밖 UI/UX 개편·dependency 갱신을 섞지 않는다. 오산에 별도 관리 메뉴·사업부 선택 페이지·Pending 기능을 임의 추가하지 않는다.
- 사업부 전환은 서버가 허용한 대상만 제공한다. 단일 소속은 해당 페이지로 진입하고, 계정 승인 대기를 사업부 전용 새 흐름으로 복제하지 않는다.

## 상태와 계약

- 서버 권한·runtime mode·validation이 최종 기준이다. API type/response를 바꾸면 실제 소비자와 fixture를 함께 확인한다.
- 저장 중 중복 제출을 막고 action 근처에 성공·실패와 다음 행동을 표시한다. loading·empty·error·denied·not-found를 구분한다.
- 필드 label, 키보드 접근, focus, 읽을 수 있는 오류 안내를 유지한다. raw enum·내부 ID·개발자 진단을 제품 UI에 노출하지 않는다.
- 사업부/프로젝트 전환 시 이전 요청의 늦은 응답이 새 화면·입력 대상에 섞이지 않게 한다.

## 확인

- [검증표](../docs/development/validation-matrix.md)에 따라 관련 테스트와 필요한 lint/typecheck/build만 선택한다. 작은 스타일 변경에 모든 suite를 자동 추가하지 않는다.
- 화면 변경은 관련 상태를 desktop과 필요한 좁은 폭에서 실제로 연 뒤 눈으로 확인한다. 모바일 영향이 있으면 390px 및 관련 Teams pane에서 표 내부 scroll과 page overflow를 구분한다.
- 정상 경로의 blank page, console error, 실패 요청과 접근성을 확인한다. 화면을 열지 못하면 시각 검증 미완료로 기록한다.
- 실제 화면 관찰·합성 screenshot·비식별 결과 보관은 [증거 정책](../docs/development/privacy-safe-evidence.md)을 따른다.
