# 디자인 화면 단위 승격 운영 기준

## 1. 목적과 적용 범위

이 문서는 Figma에서 승인된 화면을 5176 디자인 실험 환경에서 구현·검수한 뒤 기능 기준선인 최신 `main`에 화면 단위로 승격하는 절차를 정의한다. 장기 실험 branch 전체를 병합하지 않고, 검수가 끝난 화면과 그 화면에 직접 필요한 코드·asset·test·문서만 승격한다.

이 기준은 디자인 변경에만 적용한다. 인증 정책, Backend, API, DB, migration, dependency와 runtime configuration 변경은 각각 별도 Task·승인·검증을 거쳐야 한다.

## 2. 환경별 역할

| 환경 | 역할 | Source of truth 여부 |
| --- | --- | --- |
| 최신 `main`과 5174 | 기능 구현·통합·실사용 검수 기준선 | Yes |
| 5176 bounded worktree | Figma 화면 구현·시각 비교·사용자 디자인 검수 | No — 승격 전 실험 환경 |
| Figma 승인 node | 해당 화면의 시각 계약 | 디자인 범위에 한해 Yes |

5176은 실제 기능 기준선을 대체하지 않는다. 5176에서 완성된 화면도 사용자 재검수와 별도 승격 승인 전에는 `main`의 일부가 아니다.

## 3. 화면 단위 정의

승격 단위는 하나의 route 또는 사용자가 독립적으로 검수할 수 있는 하나의 상태 화면이다. 다음 항목을 fixed allowlist로 기록한다.

- 화면 component와 그 화면 전용 style
- 승인된 image·SVG·font 등 asset
- 해당 화면의 responsive·accessibility·browser test
- Task, Change, implementation report와 Roadmap 상태
- 공용 component를 바꾸는 경우 영향을 받는 모든 소비 화면과 회귀 test

Backend나 여러 화면에 영향을 주는 공용 기능 변경이 섞이면 같은 승격 단위로 간주하지 않고 별도 범위와 승인을 받는다.

## 4. 구현과 검수 절차

1. 현재 filesystem의 instruction chain, Roadmap 순서, canonical Task와 동일 목적 branch/worktree/PR을 확인한다.
2. Figma node, variables, screenshot, asset과 가능한 경우 Code Connect를 확인하고 승인된 화면 계약을 Change 문서에 고정한다.
3. 5176 bounded worktree에서 승인 화면만 구현한다. 기존 5174·Backend·DB runtime은 종료·재시작하지 않는다.
4. Frontend 최소·영향·전체 검증과 privacy-safe PC browser 비교를 수행한다.
5. 구현 session과 분리된 read-only 검증에서 계약, diff, test와 Finding gate를 확인한다.
6. 사용자가 5176에서 화면과 상태 전이를 검수한다.
7. 사용자가 화면 단위 승격을 별도로 승인한 뒤에만 최신 `origin/main` 기반 clean promotion branch를 만든다.
8. 실험 branch 전체가 아니라 fixed allowlist의 화면 diff만 이식하고, 최신 main 기능 코드와 수동으로 조정한다.
9. 승격 branch에서 전체 영향 검증과 5174 실제 기능 검수를 다시 수행한다.
10. commit·push·PR·merge는 각 게시 경계를 사용자가 명시 승인한 경우에만 수행한다.

## 5. Main 변경을 5176에 반영하는 기준

- 검수 완료 화면이 이미 main에 승격됐다면 다음 디자인 작업은 그 최신 main 기준선을 5176에 반영한 뒤 시작한다.
- 진행 중인 미승격 디자인이 있으면 main 전체를 기계적으로 덮어쓰지 않는다. 먼저 main 변경을 디자인 소유 영역과 비디자인 영역으로 분류한다.
- 비디자인 영역은 최신 main을 그대로 반영한다.
- 디자인 소유 영역의 기능 변경은 동작·state·접근성 계약만 가져오고 승인된 layout·style·asset을 유지하도록 수동 조정한다.
- 같은 줄이나 component에서 기능과 디자인이 충돌하면 어느 쪽도 임의 선택하지 않는다. 충돌 위치, 기능 영향, 시각 영향을 보고하고 사용자 결정을 받는다.
- Dirty worktree, 미도달 commit, 실행 중 process 소유가 있으면 branch 전환·rebase·worktree 제거를 하지 않는다.

Git은 “디자인 부분만 자동 보존”을 의미적으로 판단할 수 없으므로, fixed allowlist와 화면별 test가 보존 경계다.

## 6. 승격 Gate

다음을 모두 만족해야 승격 가능 상태다.

- 승인된 Figma 화면과 사용자 결정이 Change 문서에 기록됨
- 자동 검증과 독립 검증 완료
- P0/P1/P2 Finding 0, P3는 후속 Task에 연결
- 사용자 5176 검수 완료
- privacy/secret/generated artifact와 범위 밖 diff 0
- 최신 `origin/main` 기반 clean promotion branch 사용
- 화면 allowlist와 rollback 경계 확정
- 기존 기능·권한·인증·접근성 계약 보존 확인

Gate 통과는 commit·push·PR·merge 승인을 대신하지 않는다.

## 7. 승격 보류와 Rollback

사용자가 미완성 상태를 선언하거나 검수 중 잔여 범위를 발견하면 현재 화면은 즉시 `NO_GO`로 되돌리고 새 Change로 계약과 test를 갱신한다. 기존 승격 요청은 재사용하지 않는다.

승격 후 회귀가 발견되면 화면 allowlist의 diff만 되돌린다. 인증 정책, Backend, DB 또는 다른 화면까지 넓혀 되돌려야 한다면 별도 영향 분석과 승인을 받는다. 5176 preview는 원인 비교용으로 보존하되 runtime 소유를 확인하지 않고 종료하거나 worktree를 제거하지 않는다.

## 8. 현재 적용

`TASK-DESIGN-LOGIN-001`은 이 기준의 첫 적용 대상이다. Change 008까지의 구현·자동·독립 검증과 사용자 전체 검수, Change 009 최신 main fixed allowlist 이식·자동·독립 검증을 완료했다. 2026-07-15 사용자는 stage·commit·push·PR·merge와 5174 Frontend 반영을 승인했고, 5176 실험본과 Backend·DB runtime은 보존한다.
