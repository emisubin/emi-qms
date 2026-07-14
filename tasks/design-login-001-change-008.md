# TASK-DESIGN-LOGIN-001 Change 008 — 체크 아이콘 중앙 정렬

## 1. 사용자 요청

- 로그인 상태 유지 checkbox 안의 Done icon만 가운데 정렬한다.
- checkbox의 크기·색상·border·icon 크기·문구·기능과 나머지 화면은 수정하지 않는다.
- 현재 로그인 화면의 화면 단위 승격은 사용자 재검수와 별도 승인 전까지 보류한다.

## 2. Change Identity

- canonicalTaskId: `TASK-DESIGN-LOGIN-001`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- gateStatus: `PASS_REUSE`
- purpose: Variant 2 Done icon의 시각 중심 보정
- 변경 경계: icon background position과 해당 browser assertion, Task 상태 문서만 변경
- 보존: checkbox `18.75×18.75`, icon `13.5×13.5`, 기본/Variant 2 색상, preference/cache 의미, 모든 다른 화면·인증 동작·runtime

## 3. 확인된 원인과 구현 계약

- 기존 구현은 Done icon을 `3px 3px` 좌표에 고정했다.
- 고정 좌표 대신 checkbox background positioning area의 수평·수직 중앙인 `50% 50%`를 사용한다.
- icon asset과 `13.5×13.5` 크기는 변경하지 않는다.
- 미선택 variant, 문구, checkbox frame과 responsive scale은 변경하지 않는다.

## 4. 검증 계약

- 선택 상태 computed `background-position`이 `50% 50%`인지 확인한다.
- 기존 Variant 2 fill·border·Done asset·icon size·text·preference 검증을 그대로 통과한다.
- PC 6개 viewport의 기본 로그인·Loading browser matrix와 Frontend 회귀 검증을 수행한다.
- 변경 범위·privacy·generated artifact와 분리된 read-only 검증을 확인한다.

## 5. 제외 범위

- 다른 checkbox style 또는 layout 변경
- Figma 파일·asset 변경
- 인증 정책·Frontend 인증 로직·Backend·API·DB·migration·dependency·runtime configuration
- runtime 재시작, commit·push·PR·merge·worktree 정리
