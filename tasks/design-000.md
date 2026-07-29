# DESIGN-000 — EMI Design Foundation

## 1. Task Identity Gate

- proposedTaskId: `DESIGN-000`
- taskType: `HOUSEKEEPING`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `DESIGN-000`
- roadmapNextGate: `DEFERRED_HOUSEKEEPING`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `0`
- canonicalTaskId: `DESIGN-000`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true` — 사용자가 이번 turn에 DESIGN-000 시작을 명시함
- gateStatus: `PASS_CREATE`

검색 범위는 Task·Roadmap·Decision Log·실험 완료 원장, local/remote branch와 worktree다. 기존 DESIGN-001은 화면 통일 구현이며 CSS token·공통 component foundation이라는 본 Task 목적과 다르다. GitHub PR API는 실행 정책상 호출하지 못했으며 local/remote ref에는 동일 목적 branch가 없다.

## 2. Purpose identity

- 업무 목표: 제공된 reference의 시각 규칙을 EMI semantic token과 재사용 가능한 React primitive로 고정해 화면별 임의 CSS 증가를 막는다.
- Root Finding: 현재 `styles.css`에 task별 color·radius·shadow·spacing이 누적되고 동일한 page header, surface, toolbar, badge가 서로 다른 값으로 구현돼 있다.
- 변경·검증 경계: CSS variables, 공통 component kit, shell·Home·Sales의 우선 adoption, token contract test와 visual regression.
- 보존할 불변조건: EMI logo·업무 문구·기능·권한·URL·API·DB는 유지한다. reference의 타사 logo와 고유 content는 복제하지 않는다.
- 예상 산출물: token stylesheet, common React primitives, adoption examples, component catalog 문서, desktop/mobile screenshot, implementation report.

## 3. Reference projection

- Canvas: 매우 옅은 cool gray, content는 넓은 white workspace.
- Shell: 44~52px top rail, 약 180~200px full-height left navigation, 1px neutral divider, shadow 최소화.
- Type: 11~13px body/control, 18~22px page title, 짧은 한글 label과 높은 정보 밀도.
- State: blue primary/active, pale blue active fill, neutral border, red/green은 semantic danger/success에만 사용.
- Shape: 6~10px control/card radius를 기본으로 하고 pill은 status/filter, circle은 count/avatar에만 사용.
- Layout: page heading → compact toolbar/tabs → table/card data surface. 불필요한 hero와 장식성 gradient를 공통 foundation에서 제외한다.
- Mobile: 같은 token을 사용하되 compact app bar, 한 열 content, short header, progressive disclosure, 44px hit area를 적용한다.

## 4. 구현 범위

- `frontend/src/design-system/tokens.css`: color, typography, spacing, radius, shadow, layer, layout variables
- `frontend/src/design-system/components.tsx`: `DsPageHeader`, `DsSurface`, `DsToolbar`, `DsTabs`, `DsBadge`
- shell·Home·Sales의 token/component adoption
- mobile simple-mode utility와 desktop/mobile visual QA

## 5. 제외·승인

- Figma file write·component library publish: 제외 — 현재 요청은 code foundation 구현이다.
- feature behavior, Backend, DB, migration, Persistent UAT, actual provider: 제외
- planningApproved: `true`
- implementationApproved: `true`
- local commitApproved: `true`
- push/pr/mergeApproved: `false`; main merge `0/3`
