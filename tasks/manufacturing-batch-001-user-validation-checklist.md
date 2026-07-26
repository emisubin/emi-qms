# TASK-MANUFACTURING-BATCH-001 사용자 검수 체크리스트

- 환경: 현재 `experiment/task-home-002-personalized-shell` local validation runtime
- 데이터: synthetic project·panel·role
- 자동 검증 상태: `완료`
- 사용자 검수 상태: `대기 — 마지막 일괄 검수`
- 대표 repo·GitHub main 반영: `없음`
- main merge 승인: `0/3`

## 자동 검증

- [x] `0056` fresh migration과 operation/event FK·constraint
- [x] 제조 queue가 immutable template version에서 조립 의미 단계를 식별
- [x] 영업 역할 batch 요청 `403`
- [x] 2패널의 조립 단계 2건만 한 transaction에서 확인
- [x] 조립 전 단계와 조립 후 자체검사는 모두 미완료로 유지
- [x] batch event 2건 correlation, 각 execution version `1→2`, replay 중복 없음
- [x] 이미 조립 완료+신규 패널 혼합 요청 전체 rollback
- [x] batch 뒤 execution `InProgress`, 자체검사 미완료, LQC 조기 인계 없음
- [x] 패널별 자체검사·제조 완료 뒤 LQC 업무 2건 생성
- [x] 전체 흐름 네 표시명 exact text와 `KittingCompleted`의 `(선택)` 숨김
- [x] Backend Release 전체 `424/424`
- [x] Frontend lint·typecheck·unit 전체·production build
- [x] isolated Full-Stack `1/1`, Desktop·390px horizontal overflow `0`
- [x] 고정 검수 runtime `42983/41166` health와 `0056` 적용 확인, server 유지

## 사용자 직접 검수

- [ ] 프로젝트 전체 흐름에서 `자재 / 제조 요청` 표시 확인
- [ ] 프로젝트 전체 흐름에서 `물류 / 포장` 표시 확인
- [ ] 프로젝트 전체 흐름에서 `물류 / 납품` 표시 확인
- [ ] 프로젝트 전체 흐름에서 `영업 / 세금계산서` 표시 확인
- [ ] 제조 화면에서 패널 checkbox와 전체선택이 선택 Excel·조립 action에 같이 반영되는지 확인
- [ ] [Desktop 1440 확인 sheet](manufacturing-batch-001-screenshots/01-assembly-batch-confirm-desktop-1440.png): 대상·제외·조립 처리 단계 수 확인
- [ ] [Mobile 390 확인 sheet](manufacturing-batch-001-screenshots/02-assembly-batch-confirm-mobile-390.png): 한 열 구성·버튼·대상 목록 확인
- [ ] [Mobile 390 성공 화면](manufacturing-batch-001-screenshots/03-assembly-batch-success-mobile-390.png): 성공 안내와 선택 자동 해제 확인
- [ ] 제조 시작 전·중단·완료·이미 조립 완료 패널의 제외 사유가 이해되는지 확인
- [ ] batch 성공 뒤 조립 단계만 확인되고 조립 전 단계·자체검사·제조 완료는 남아 있는지 확인
- [ ] 실패 시 선택이 유지되고 새로고침·재시도할 수 있는지 확인

## 게시·운영 Gate

- [x] local experiment commit — 선행 Task와 함께 exact allowlist·충돌·privacy 검토 후 누적 checkpoint에 포함
- [ ] push 승인 — 현재 없음
- [ ] PR 승인 — 현재 없음
- [ ] Persistent UAT migration·runtime handover 승인 — 현재 없음
- [ ] main merge 1차 승인
- [ ] main merge 2차 승인
- [ ] main merge 3차 승인

사용자 검수와 세 번의 main merge 승인은 서로 다른 Gate다. 자동 검증 완료는 게시·merge 승인이 아니다.
