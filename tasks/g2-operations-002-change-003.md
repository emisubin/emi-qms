# TASK-G2-OPERATIONS-002 Change 003 — 전일 실적 기반 출하 가능 재고

- taskType: `BUGFIX`
- status: `PUBLIC_RELEASE_COMPLETE`
- approvedSource: `USER_EXPLICIT_REQUEST`
- 작성일: 2026-09-02
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-G2-OPERATIONS-002`
- reuseExistingTask: `true`
- gateStatus: `PASS_REUSE`
- baseBranch: `origin/main`
- baseSha: `a8bb000dbbbe7d307bf1de96259917b750460497`

## 문제와 확정 정책

기존 재고는 같은 날짜의 생산·납품·불량을 반영해 당일 마감 재고로 계산한다. 사용자는 재고를 해당 날짜가 시작될 때 언제든 출하할 수 있는 가용 재고로 사용해야 한다고 확정했다.

2026-08-28부터 실사 없는 날짜의 재고는 다음 수식을 사용한다.

`오늘 재고 = 전일 재고 + 전일 오전 생산 + 전일 오후 생산 - 전일 납품 - 전일 불량`

2026-08-28 이전은 기존 같은 날짜 실적 수식을 유지한다. 2026-08-28은 공개 PMS 기준 전일 재고 `2`, 전일 생산 `34`, 전일 납품 `30`, 전일 불량 `0`을 반영해 `6`이 되어야 한다.

## 포함 범위

- Backend 순수 재고 계산과 조회 시작일 이전 잔액 계산
- 월 첫날·부분 범위 조회에 필요한 전일 실적 조회
- 홈 저장 없는 임시 입력이 입력일의 다음 날 재고부터 반영되도록 Frontend 계산 변경
- 실사 checkpoint, 음수 재고, 빈 값의 계산상 `0`, 목표·권한·CAS·forecast lifecycle 보존
- Backend·Frontend·격리 Full-Stack 회귀, 원격 `main` 병합과 exact SHA Azure 공개배포

## 제외 범위

- G2 원본 생산·납품·불량·실사 데이터 변경
- migration 또는 schema 변경
- 2026-08-28 이전 재고 수식 변경
- 재고 직접 입력, 관리자 이력 또는 손익관리 추가

## 게시 승인

사용자는 수정과 공개배포를 같은 요청에서 명시 승인했다. 이번 승인은 검증된 변경의 commit, push, Ready PR, 필수 CI 통과 뒤 원격 `main` 병합, exact main SHA의 Backend·Frontend Azure 공개배포와 공개 G2 read-only 확인을 포함한다. 운영 G2 원본 데이터 mutation은 포함하지 않는다.

## 독립 검증

분리된 Codex 검증 세션이 기준 SHA 대비 diff, 절단일·실사·부분 조회와 Frontend 임시 계산을 read-only로 검토했다. Backend G2 `12/12`, 실제 PostgreSQL 경계 `1/1`, Frontend G2 `15/15`와 diff check가 통과했으며 Open P0/P1/P2는 `0/0/0`, 게시 판정은 `GO`다. Full-Stack은 구현 세션의 격리 실행 `1/1` 결과와 assertion을 대조했다.

## 공개배포 결과

- PR #117, exact main `58daf6d8bfe333cb00e343a3fcc13ee4f3358183`
- PR CI `33573894506`, main CI `33575957041` `PASS`
- current-main 전체 통합 Azure release `33577473523` `PASS`
- workflow migration `0085`·Backend·Frontend·public security `PASS`
- 인증된 공개 G2 2026-08-28 재고 `6대` 확인
- Persistent UAT와 G2 원본 데이터 mutation 미실행
