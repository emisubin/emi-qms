# DESIGN-000 Change 001 — Black & White Wireframe Theme

## Task Identity Gate

- taskType: `HOUSEKEEPING`
- instructionChainRead: `true`
- canonicalTaskId: `DESIGN-000`
- reuseExistingTask: `true`
- purposeIdentity: 상태를 구분하는 의미색만 유지하고 전체 제품 UI를 흑백·무그림자·사각형 중심의 와이어프레임 시각 체계로 전환한다.
- roadmapSequenceMatch: `false`
- explicitRoadmapOverrideApproved: `true` — 사용자가 현재 실험 브랜치에서 전체 디자인 전환과 구현을 직접 지시함
- samePurposeMatchCount: `1`
- gateStatus: `PASS_REUSE`

## 변경 계약

- 인증 화면과 애플리케이션 전체에 동일한 흑백 토큰을 적용한다.
- 캔버스·표면·제어·내비게이션·차트·장식 요소의 비상태 색을 흰색, 검정, 중성 회색으로 제한한다.
- 그림자와 장식성 그라디언트를 제거하고 1px 선으로 계층을 표현한다.
- 카드, 입력, 버튼, 탭, 메뉴는 사각형을 기본으로 하고 상태 배지와 프로필 사진처럼 의미가 있는 예외만 별도로 둔다.
- 성공·주의·오류·보류·진행처럼 사용자가 판단해야 하는 상태 표시는 기존 의미색을 유지한다.
- 레이아웃, 문구, 기능, URL, 권한, API, DB, 업무 상태 전이는 변경하지 않는다.
- 기존 기능별 CSS를 직접 재작성하지 않고 마지막 cascade layer인 `wireframe.css`에서 전역 규칙을 통제한다.

## 검증 계약

- wireframe stylesheet가 기존 token stylesheet 뒤에 로드되는지 정적 계약으로 확인한다.
- 비상태 token이 무채색이고 radius·shadow가 wireframe 값인지 검사한다.
- semantic status token은 성공·주의·오류 의미색을 유지하는지 검사한다.
- Frontend lint, typecheck, unit test, production build를 실행한다.
- Desktop 1440px와 Mobile 390px에서 대표 화면의 레이아웃, 가로 넘침, 상태색 예외를 확인하고 screenshot을 남긴다.

## 게시 경계

- 현재 `experiment/task-home-002-personalized-shell` worktree의 frontend·test·Task 문서만 변경한다.
- 대표 repo, `main`, Backend, DB, migration, push, PR, merge, Persistent UAT는 변경하지 않는다.
- local experiment commit만 허용되며 main merge 승인 수는 `0/3`이다.
