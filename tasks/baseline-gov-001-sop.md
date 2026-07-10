# BASELINE-GOV-001 Task 종료 거버넌스 SOP

## 1. 목적

이 SOP는 [Task 종료 및 산출물 정책](../docs/12-task-completion-policy.md)을 실제 Task에서 일관되게 수행하기 위한 순서와 중단 조건을 정의한다. repository별 `AGENTS.md`와 사용자 지시가 더 엄격하면 그 기준을 우선한다.

## 2. Task 시작 전 기준선 확인

1. repository path, current branch와 HEAD를 확인한다.
2. local main과 origin/main의 HEAD 및 ahead/behind를 확인한다.
3. staged, unstaged, untracked, stash를 확인한다.
4. `git fetch origin --prune`이 허용된 경우 remote ref를 갱신한다.
5. 동일 Task ID, 동일 목적 branch와 open PR을 확인한다.
6. dirty working tree가 있으면 소유 범위와 보존 방법을 확정하기 전 변경하지 않는다.
7. baseline 불일치가 중단 조건이면 reset/rebase/checkout으로 임의 해결하지 않는다.

## 3. 조사와 기획

1. Product Roadmap과 관련 Task 문서를 source of truth로 읽는다.
2. 목적, 포함 범위, 제외 범위, 선행조건과 완료 기준을 Task 문서에 기록한다.
3. runtime, migration, DB, UAT, 외부 발송 등 영향 범위를 분류한다.
4. 예상 Finding, 개인정보·secret 위험과 사용자 검수 항목을 정의한다.
5. 기존 동일 목적 작업이 있으면 branch를 checkout하지 않고 ref diff로 의미를 비교한다.

## 4. 구현

1. 승인된 branch와 범위에서만 변경한다.
2. 기존 사용자 변경과 main 반영 migration을 보존한다.
3. 범위 밖 수정이 필요하면 구현을 확장하지 않고 Finding 또는 후속 Task로 기록한다.
4. 코드 전체 diff를 보고서에 복사하지 않고 repository와 Git diff를 source of truth로 유지한다.
5. 개인정보, secret, 실제 회사 이메일/UPN과 조직 식별자를 문서·로그에 남기지 않는다.

## 5. 자동 테스트

1. 변경 유형에 맞는 `AGENTS.md` 테스트 기준을 실행한다.
2. 관련 targeted test와 회귀 테스트를 구분해 기록한다.
3. 문서 전용 Task는 link, anchor, heading, 정책 정합성, PII/secret과 변경 범위를 검사한다.
4. 실행하지 않은 테스트는 미실행 이유를 implementation report에 기록한다.
5. 실패를 재실행 성공만으로 숨기지 않고 원인과 수정 근거를 기록한다.

## 6. 사용자 검수

1. user validation checklist를 자동 검증과 사용자 확인 항목으로 분리한다.
2. 환경, 날짜, 역할명 또는 익명 사용자 A/B, 결과와 증빙 유형만 기록한다.
3. 외부 알림 발송이나 데이터 변경은 사용자 승인 여부를 먼저 기록한다.
4. checklist 작성, 자동 검증 완료, 사용자 검수 대기/완료/실패/N/A 상태를 구분한다.
5. 미체크 항목을 완료로 바꾸지 않는다.

## 7. Finding 분류와 gate

- P0/P1: 수정 전 Task 완료, 게시와 merge를 중단한다.
- P2: 기본적으로 수정한다. 예외 수용은 사용자 승인, risk owner, 근거, 영향, 완화책, 재검토 시점과 후속 Task ID가 필요하다.
- P3: 후속 Task 또는 명시적인 backlog와 연결한다.
- 미검증 상태는 성공이 아니다.

Finding은 심각도, 위치, 문제, 발생 조건, 영향, 근거, 최소 수정과 필요한 테스트를 기록한다.

## 8. 5종 산출물 작성

Task 종료 전 다음 경로와 상태를 implementation report 또는 Roadmap에서 추적한다.

1. Implementation report
2. SOP
3. User manual
4. Roadmap update
5. User validation checklist

독립 파일이 아니면 포함된 문서와 section을 link한다. 정말 적용 대상이 없을 때만 구체적인 이유를 적은 `N/A`를 사용한다.

## 9. Allowlist staging

1. 사용자 지시 또는 Task 문서의 allowlist를 확정한다.
2. `git add .`와 `git add -A`를 사용하지 않는다.
3. 허용된 파일 경로를 개별적으로 stage한다.
4. `git diff --cached --name-status`와 `git diff --cached --check`를 확인한다.
5. runtime, dependency, migration, `.env`, 인증서, secret과 삭제 파일이 섞이면 즉시 unstage하고 원인을 확인한다.
6. staged diff가 검증한 working tree diff와 같은 범위인지 확인한다.

## 10. Commit, push와 PR

1. Commit/Push/PR에 대한 사용자 승인을 확인한다.
2. Task 범위를 설명하는 commit message를 사용한다.
3. force push 없이 현재 branch를 origin에 push한다.
4. PR에는 기존 작업 비교, canonical 결정, 5종 산출물, Finding gate, 개인정보 처리, 검증과 남은 문제를 기록한다.
5. 사용자 검수 대기이면 draft PR과 checklist에 그 상태를 명시한다.
6. Merge는 별도 승인 전 수행하지 않는다.

## 11. CI와 merge

1. 실행된 CI check 이름과 상태를 확인한다.
2. CI가 실행되지 않았으면 실행되지 않았다고 보고한다.
3. 실패 check를 성공으로 표시하거나 생략하지 않는다.
4. P0/P1, 승인되지 않은 P2, 사용자 검수 gate와 CI가 모두 해소된 뒤에만 merge 후보가 된다.
5. 실제 merge는 사용자 승인과 repository 절차를 따른다.

## 12. Branch 정리

1. PR merge와 배포/보존 요구를 확인한다.
2. 현재 branch와 대체된 기존 branch를 자동 삭제하지 않는다.
3. local/remote branch 삭제는 별도 사용자 승인을 받는다.
4. history rewrite, force push와 tag 재작성은 독립 승인 없이 수행하지 않는다.

## 13. 예외와 N/A 처리

- 적용 대상이 없는 산출물이나 report 항목은 이유와 canonical 위치를 기록한다.
- “작은 변경”, “문서 변경” 또는 “시간 부족”만으로 N/A 처리하지 않는다.
- 정책 충돌이 있으면 어느 기준을 우선할지 사용자에게 확인한다.
- 사용자 검수 대기는 N/A가 아니다.

## 14. 개인정보·secret 검사

- current checkout의 실제 이름, 회사 이메일/UPN, tenant/client/object id와 고객·프로젝트 민감정보를 검사한다.
- private key, webhook URL, bearer/token/password/secret과 tracked `.env`를 검사한다.
- placeholder와 역할명은 false positive로 분리한다.
- history 검사는 read-only로 수행하고 원문 값 대신 commit/file 수만 보고한다.
- history rewrite는 별도 승인 없이는 수행하지 않는다.

## 15. 중단 조건

다음 조건에서는 Commit/Push/PR 또는 merge를 중단한다.

- 통합할 수 없는 정책 충돌
- actual secret 또는 current checkout 개인정보 잔존
- 허용되지 않은 runtime/dependency/migration 변경
- 깨진 필수 문서 link 또는 중대한 정책 모순
- staged allowlist 위반이나 의도하지 않은 삭제
- P0/P1 또는 승인되지 않은 P2

## 16. 실행 체크리스트

- [ ] 시작 기준선과 동일 목적 작업을 확인했다.
- [ ] 범위·제외 범위·완료 기준을 기록했다.
- [ ] 필요한 자동 테스트와 사용자 검수를 구분했다.
- [ ] Finding gate를 적용했다.
- [ ] 5종 산출물의 경로와 상태를 기록했다.
- [ ] Roadmap을 실제 결과에 맞게 갱신했다.
- [ ] PII/secret과 문서 link를 검사했다.
- [ ] allowlist 파일만 stage했다.
- [ ] Commit/Push/PR 승인과 검증을 확인했다.
- [ ] CI와 사용자 검수 상태를 사실대로 보고했다.
- [ ] merge와 branch 삭제는 별도 승인 범위인지 확인했다.
