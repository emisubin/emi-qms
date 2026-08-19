# TASK-G2-OPERATIONS-001 Change 013 — 생산·재고 공통 2단계 축과 휴일 header 테두리 복구

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

- 생산·납품·재고·재고목표에 하나의 공통 단조 증가 축 변환을 적용해 큰 숫자가 항상 더 높은 위치에 표시되게 한다.
- `0~60`은 plot 높이의 `70%`, `60~180`은 위쪽 `30%`를 사용한다.
- tick은 `0·20·40·60·100·140·180`이며 좌우 축의 같은 값은 같은 높이에 표시한다.
- 60 경계 양쪽 축에 break marker를 표시하고 `0~60 확대 · 60~180 압축` 안내를 제공한다.
- Change 012의 막대 폭·opacity, 선 white halo, 상시 수치와 tooltip은 유지한다.
- 휴일 header에도 평일과 동일한 아래쪽 `1px` dark border를 복구하며 휴일 열 red text·white background를 유지한다.

## 2. 보존 범위

- graph를 분리하지 않고 하나의 plot과 좌우 업무 제목을 유지한다.
- 조별 생산 graph의 `0~60/10` 선형 축은 변경하지 않는다.
- API·DB·권한·재고 계산·입력 data, 원격 `main`, Persistent UAT와 Azure 공개배포는 변경하지 않는다.

## 3. 검증 계획

- 공통 좌우 tick 위치·break marker·기존 halo와 휴일 column 회귀 test
- Frontend G2 집중·전체 unit, lint, typecheck와 production build
- local desktop 1440·mobile 390에서 생산 50보다 재고 90이 높게 표시되는지, 축 안내·휴일 header border·overflow 확인

## 4. 다음 Gate

구현·자동·browser 검증 뒤 local G2 홈을 사용자 비교 검수로 유지한다. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.

## 5. 구현·검증 결과

- 공통 `0~180` domain에서 `0~60`에 plot 높이 `70%`, `60~180`에 `30%`를 배분하는 단조 증가 scale을 생산·납품·재고·재고목표에 동일하게 적용했다.
- 좌우 tick을 `0·20·40·60·100·140·180`으로 통일하고 60 경계의 양쪽 break marker와 `0~60 확대 · 60~180 압축` 안내를 추가했다.
- 실제 화면에서 납품 `50`의 bar top보다 재고 `93` point가 위에 있고, 좌우 모든 동일 tick의 SVG y 좌표가 일치함을 확인했다.
- 휴일 header의 누락된 아래쪽 border를 평일과 같은 `1px solid` dark line으로 복구했다. white background와 열 전체 red text는 유지했다.
- G2 집중 `6/6`, Frontend 전체 `224/224`, lint error `0`, typecheck와 production build를 통과했다.
- desktop 1440에서 break path `2`, scale note, 공통 tick 위치, 휴일 header `1px solid`, page overflow `0`을 확인했다. mobile 390에서도 break marker·안내·header border와 page overflow `0`을 확인했다.
- 검수 runtime은 Frontend `http://127.0.0.1:42983/g2`, Backend `http://127.0.0.1:41166`에서 유지한다.
