# TASK-G2-OPERATIONS-002 Change 004 — 당일 납품 기반 출하 후 가용재고

- taskType: `BUGFIX`
- status: `LOCAL_VALIDATION_COMPLETE_PUBLICATION_APPROVED`
- approvedSource: `USER_EXPLICIT_REQUEST`
- 작성일: 2026-09-02
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapSequenceMatch: `false`
- explicitRoadmapOverrideApproved: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-G2-OPERATIONS-002`
- reuseExistingTask: `true`
- gateStatus: `PASS_REUSE`
- baseBranch: `origin/main`
- baseSha: `c3bf047a07be262840d30ce98311f4e3b983b7bf`

## 이해한 요청과 확정 수식

사용자는 2026-08-28부터 재고를 당일 납품 후 남은 출하 가능 재고로 표시하고, 검증된 변경을 원격 `main`에 병합해 Azure 공개배포까지 한 번에 진행하도록 승인했다.

실사 없는 날짜 `D`의 확정 수식은 다음과 같다.

`재고(D) = 재고(D-1) + 생산(D-1) - 불량(D-1) - 납품(D)`

따라서 오늘 생산·불량은 다음 날짜 재고부터 반영하고, 오늘 납품은 오늘 재고에서 바로 차감한다. 실사 날짜는 기존처럼 실사 수량이 그 날짜 재고를 고정하며, 그 날짜 생산·불량은 다음 날짜에 반영되고 그 날짜 납품은 실사값에 다시 차감하지 않는다.

- `D-1`은 주말·공휴일을 건너뛰지 않는 직전 달력일이며 날짜는 기존 서울 업무 날짜 계약을 사용한다.
- 2026-08-28은 2026-08-27의 기존 표시 재고를 기준점으로 사용하고 새 수식을 처음 적용한다.
- 실사 수량은 해당 날짜 납품까지 반영한 확인 재고로 취급한다. 실사일의 납품은 다시 빼지 않고 생산·불량만 다음 날짜 계산에 사용한다.
- 미입력 생산·납품·불량은 계산에서 `0`이고 실제 `0` 입력과 빈 값의 저장 의미는 기존 계약을 유지한다. 음수 계산 재고도 그대로 표시한다.
- 계산 단위는 현재 G2 전체 대수 하나이며 품목·창고·lot 차원은 추가하지 않는다.

## 포함 범위

- Backend 순수 재고 계산과 부분 기간 조회 시작 잔액 계산
- 홈 저장 없는 임시 입력에서 당일 납품은 당일 재고, 당일 생산·불량은 다음 날짜 재고에 반영
- 절단일·실사·전체/부분 조회·홈 임시값·격리 Full-Stack 회귀
- Implementation report·Roadmap·사용자 검수 기록 동기화
- 검증된 변경의 commit, push, Ready PR, 필수 CI, 원격 `main` 병합과 exact main SHA Azure 공개배포

## 제외·보존 범위

- G2 원본 생산·납품·불량·실사 데이터 mutation 없음
- migration·schema·권한·CAS·forecast lifecycle 변경 없음
- 2026-08-27까지의 기존 같은 날짜 계산식 유지
- 목표·출근·그래프 디자인·관리자 이력·손익관리 변경 없음
- Persistent UAT와 실제 외부 알림 시험 발송 제외

홈 임시값은 화면에 조회된 저장값을 필드별로 대체하는 브라우저 메모리 값이다. API 저장은 호출하지 않고 초기화·재조회·월 이동·새로고침 때 폐기한다.

## 검증 및 게시 계약

Backend 영향·전체 회귀, Frontend lint·typecheck·unit·build, 격리 Full-Stack, diff·개인정보·allowlist 검사와 분리된 Codex 독립 검증에서 Open P0/P1/P2가 없어야 게시한다. 사용자 요청은 이번 변경의 PR·main 병합과 동일 SHA Azure 공개배포를 명시적으로 포함한다.

## Local 검증 결과

- Backend G2 `12/12`, 실제 PostgreSQL 전체·부분·하루 단독 조회 `1/1`
- Backend 전체 격리 회귀 `573/573`, Release build warning/error `0/0`
- Frontend 33 files `250/250`, lint error `0`·기존 warning `1`, typecheck·production build `PASS`
- G2 isolated Full-Stack `1/1`, diff check `PASS`
- migration·운영 G2 원본 데이터·Persistent UAT mutation `0`

분리된 Codex 독립 검증과 게시 allowlist gate는 local commit 후보를 기준으로 수행한다.

변경 allowlist는 계산·부분 조회·홈 임시계산 3개 source, 관련 Backend·Frontend·Full-Stack test, 이 Change, Implementation report와 Roadmap이다. 배포 후에는 public health·익명 차단과 인증된 G2 재고 수식을 privacy-safe projection으로 확인한다. 실패하면 직전 검증 Backend·Frontend image로 함께 rollback하고 재고 판단을 중지한다.
