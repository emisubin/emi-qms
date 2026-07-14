# TASK-GOV-FINDING-GATE-001 SOP

## 1. 목적

신규 기능 Gate를 결정하기 전에 현재 Finding을 중복 없이 재분류하고 외부 blocker를 과장 없이 유지하는 절차다.

## 2. 재평가 순서

1. Root instruction chain과 현재 main을 다시 확인한다.
2. 동일 목적 branch/worktree/PR을 확인하고 canonical Task ID를 사용한다.
3. 최근 merge의 changed files와 표준 CI를 fixed-field projection으로 확인한다.
4. Roadmap·Task·Implementation report의 Open Finding을 실제 코드·merge와 대조한다.
5. Runtime·Persistent UAT는 read-only aggregate만 확인한다.
6. 각 Finding에 severity, status, owner, mitigation, review trigger와 blocker를 기록한다.
7. Open P0/P1/P2가 하나라도 있으면 신규 기능 Gate를 `NO_GO`로 유지한다. 0건이면 `GO_FOR_USER_DECISION`으로 사용자 결정을 요청하며 자동 시작하지 않는다.

## 3. History Support 처리

- Support 상태는 `REMOVED`, `SUPPORT_PENDING`, `SUPPORT_REJECTED`, `UNKNOWN`만 사용한다.
- `SUPPORT_PENDING`에서 published ref 정리만으로 P2를 Closed 처리하지 않는다.
- Support 완료 회신 뒤 private fresh clone과 fixed web projection을 다시 확인한다.
- 실제 개인정보, commit 원문, raw Support 화면과 GitHub 개인 metadata를 보고에 포함하지 않는다.
- Public 전환, residual risk acceptance와 backup 삭제는 각각 별도 승인 대상이다.

현재 fixed 결과는 Support completion/follow-up/closed `1/1/1`, cached reference `REMOVED`다. Reference 재노출 시 history P2를 다시 열고 Support 후속 확인을 수행한다.

## 4. 금지사항

- 승인 범위 밖 history backup·old clone·worktree 변경
- Support 대기 중 public 전환
- Open P2를 근거 없이 P3 또는 risk accepted로 하향
- Runtime 재시작, Persistent write, provider 호출
- Finding gate와 신규 기능 planning을 같은 승인으로 처리

## 5. 재실행 조건

Support internal reference 제거 완료 회신을 반영해 Phase A와 closure matrix 재실행을 완료했다. 이후 reference 재노출, 신규 P0/P1/P2, runtime 이상 또는 main 변경이 있으면 다시 실행한다.
