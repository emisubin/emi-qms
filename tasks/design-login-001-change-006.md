# TASK-DESIGN-LOGIN-001 Change 006 — Loading control 제거와 회전 indicator

## 1. 사용자 결정

- 화면 단위 디자인 승격 방식을 Repository 운영 기준으로 문서화한다.
- Loading 화면은 일반 로그인 화면의 logo, 제목, Microsoft logo, 안내, 배경과 responsive canvas를 유지한다.
- Loading에서는 `LOGIN` button과 `로그인 상태 유지` checkbox를 표시하지 않는다.
- 두 control이 있던 영역에는 브랜드 빨간색 원형 loading indicator 1개를 표시하고 계속 회전시킨다.
- 현재 로그인 화면의 main 승격은 이 Change의 구현·자동 검증·독립 검증·사용자 재검수와 별도 승격 승인 전까지 `NO_GO`다.

## 2. Change Identity

- canonicalTaskId: `TASK-DESIGN-LOGIN-001`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- gateStatus: `PASS_REUSE`
- purpose: 승인된 로그인 shell의 Loading 상태 시각·interaction 보정과 화면 단위 승격 SOP 고정
- Change 005와 관계: 계정 선택 provider 위임과 안내 문구는 보존한다. Loading control·spinner 0 계약만 이 Change가 대체한다.

## 3. 구현 계약

- 일반 로그인 state의 `LOGIN`과 로그인 상태 유지 control은 변경하지 않는다.
- Loading state에서는 button과 checkbox를 disabled 상태로 남기지 않고 DOM에서 렌더링하지 않는다.
- Indicator는 이전 button+checkbox 결합 영역 중앙에 48.75×48.75 reference geometry로 배치한다.
- Indicator 색상은 Figma frame base와 같은 `#DA2127`, stroke 5.25px, 회전 주기 800ms linear infinite로 한다.
- PC viewport scale을 그대로 적용해 모든 지원 크기에서 위치와 원 비율을 유지한다.
- Visible text는 기존 승인 문구 `Microsoft 365 로그인 정보를 확인하고 있습니다.`만 유지한다.
- 접근성 tree에는 `role=status`, `aria-label=로그인 확인 중`, panel `aria-busy=true`를 제공한다.
- 설정 누락·재인증·오류·접근 제한 화면과 인증 request/cache 정책은 변경하지 않는다.

## 4. 승격 운영 기준

- [디자인 화면 단위 승격 운영 기준](../docs/development/design-screen-promotion.md)을 canonical SOP로 추가한다.
- 5176은 디자인 preview이며 source of truth는 최신 main이다.
- 사용자 검수와 별도 승격 승인 뒤 최신 main 기반 clean promotion branch에 화면 allowlist만 이식한다.
- 실험 branch 전체 merge, 기능 코드 덮어쓰기, 승인 없는 commit·push·PR·merge를 금지한다.

## 5. 검증 계약

- Frontend lint, typecheck, unit 전체와 production build
- 기본 로그인 6개 PC viewport와 Loading 6개 PC viewport browser matrix
- Loading button 0, checkbox 0, indicator 1, red/animation/style/geometry, `aria-busy` 검증
- panel coverage 100%, inner canvas visible, overflow 0, console/request failure 0
- privacy-safe allowlist·secret·generated artifact·문서 link 검사
- 구현 session과 분리된 read-only 검증

## 6. 제외 범위

- Figma node 수정 또는 새 Code Connect mapping
- Mobile/390px
- 인증 정책·Backend·API·DB·migration·dependency·runtime configuration
- 5174·5176·Backend runtime 재시작
- commit·push·PR·merge·worktree 정리
