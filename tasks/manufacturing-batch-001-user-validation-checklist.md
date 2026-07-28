# TASK-MANUFACTURING-BATCH-001 사용자 검수 체크리스트

- 환경: 현재 `experiment/task-home-002-personalized-shell` local validation runtime
- 최신 계약: `Change 003 — 모든 제조 단계 일괄 완료`
- 데이터: synthetic project·panel·role
- 자동 검증 상태: `완료`
- 사용자 검수 상태: `대기 — 마지막 일괄 검수`
- 대표 repo·GitHub main 반영: `없음`
- main merge 승인: `2/3`

## 자동 검증

- [x] 제조 양식에서 `일반/조립` 입력·조회·중복 validation 제거
- [x] 신규 양식 항목은 과거 DB 호환을 위해 내부 `General` 기본값만 사용
- [x] queue가 project 상세에서 패널별 전체 제조 단계·완료 여부 제공
- [x] 제조 양식의 첫·중간·마지막 단계를 모두 일괄 완료 대상으로 선택 가능
- [x] 2패널의 선택 단계 2건만 한 transaction에서 확인
- [x] 선택 단계 앞뒤의 다른 제조 단계 미완료 유지
- [x] 대상 단계 순번과 이름이 다른 실행은 전체 rollback
- [x] 이미 완료·Pending·stale·scope 밖·권한 없음 차단
- [x] event correlation, 패널별 version `+1`, replay 중복 없음
- [x] batch 뒤 execution `InProgress`, 제조 완료·LQC/OQC 조기 인계 없음
- [x] Backend Release build warning/error `0/0`
- [x] Backend 집중 `3/3`, 전체 `427/427`
- [x] Frontend 집중 `7/7`, 전체 `140/140`
- [x] Frontend lint error `0`, typecheck·production build 성공
- [x] isolated Full-Stack `1/1`, Desktop·390px horizontal overflow `0`
- [x] `git diff --check` 통과

## 사용자 직접 검수

- [ ] 제조 양식의 헤더가 `No. / 제조 항목 / 관리`로 보이고 `일반/조립` 선택이 없는지 확인
- [ ] 제조 화면에서 `패널 선택 작업` 뒤 checkbox·전체선택이 선택 Excel과 일괄 완료에 같이 반영되는지 확인
- [ ] `완료할 단계 선택`에서 제조 양식의 모든 단계가 dropdown에 보이는지 확인
- [ ] [Desktop 1440 확인 sheet](manufacturing-batch-001-screenshots/04-step-batch-confirm-desktop-1440.png): 단계 선택·대상·제외 수 확인
- [ ] [Mobile 390 확인 sheet](manufacturing-batch-001-screenshots/05-step-batch-confirm-mobile-390.png): 한 열 구성·dropdown·대상 확인
- [ ] [Mobile 390 성공 화면](manufacturing-batch-001-screenshots/06-step-batch-success-mobile-390.png): 성공 안내와 선택 자동 해제 확인
- [ ] 중간 제조 단계를 선택해 완료한 뒤 앞뒤 단계가 그대로 미완료인지 확인
- [ ] 같은 단계를 다시 선택한 패널은 `이미 완료` 사유로 제외되는지 확인
- [ ] Pending·완료·단계 구성이 다른 패널의 제외 사유가 이해되는지 확인
- [ ] 실패 시 선택이 유지되고 새로고침·재시도할 수 있는지 확인

## 게시·운영 Gate

- [x] Change 003 local commit — 누적 checkpoint `e6f3fa6`
- [ ] push 승인 — 현재 없음
- [ ] PR 승인 — 현재 없음
- [ ] Persistent UAT migration·runtime handover 승인 — 현재 없음
- [x] main merge 1차 승인
- [x] main merge 2차 승인
- [ ] main merge 3차 승인

사용자 검수와 세 번의 main merge 승인은 서로 다른 Gate다. 자동 검증 완료는 게시·merge 승인이 아니다.
