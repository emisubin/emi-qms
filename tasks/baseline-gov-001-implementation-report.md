# BASELINE-GOV-001 Implementation Report

## 1. 목적

tracked 문서의 개인정보를 비식별화하고, 기존 동일 목적 branch의 유효 규칙을 포함하는 canonical Task 종료 정책을 확정한다. Product Roadmap의 Activity Feed 상태와 전역 No-Go remediation을 실제 구현 근거에 맞게 정리하고 문서 전용 draft PR을 준비한다.

## 2. 배경

main에는 TASK-NOTIFY-003까지 반영됐지만 검수 문서에 실제 사용자 display name이 남아 있었다. Task 종료 시 필요한 산출물과 검수 완료 판정도 한 곳에서 관리되지 않았고, Roadmap은 Activity Feed provider actual과 개별 event 적용 상태를 혼용했다.

main보다 6 commit 뒤이면서 1 commit 앞선 `docs/task-close-process-guidelines` branch에는 이전 Task 종료 지침이 남아 있었다. 현재 WIP를 보존하고 branch를 checkout·merge·cherry-pick하지 않은 채 ref에서 의미를 비교했다.

## 3. 기존 branch 비교

기존 branch의 유일 commit은 `AGENTS.md`와 Product Roadmap만 변경한다.

| 구분 | 기존 branch의 유효 의미 | 현재 정책과의 관계 | 최종 처리 |
| --- | --- | --- | --- |
| 종료 순서 | review, test, Roadmap/report, allowlist, CI, merge | 현재 정책의 범위·검수·5종 산출물 절차와 양립 | 확장 통합 |
| Implementation report | 기본 파일명과 상세 영향 항목 | 현재 report 표준보다 상세한 항목이 일부 존재 | 파일명·적용 항목·N/A 규칙 통합 |
| Roadmap | 갱신 후보와 Decision Log 보존 | 현재 Roadmap source-of-truth 원칙과 동일 | 통합 |
| 문서 작성 | 실행하지 않은 테스트, secret 원문 금지 | 현재 정책이 개인정보·조직 식별자까지 더 엄격 | 더 엄격한 현재 기준 채택 |
| 코드 diff | 대형 Markdown에 전체 diff 저장 금지 | 현재 초안에 없던 유효 규칙 | 통합 |
| staging | allowlist, `git add .` 금지 | 현재 초안에 없던 유효 규칙 | `git add -A` 금지까지 통합 |
| Finding | P0/P1/P2가 없을 때만 게시 | 사용자 승인형 P2 예외와 차이 | 현재 지시의 P2 예외 요건으로 대체 |
| SOP/User manual/검수 상태/블로그 | 명시 없음 | 현재 정책에 추가됨 | 현재 기준 채택 |
| branch 정리 | 명시 없음 | 자동 삭제 금지 규칙 추가 | 현재 기준 채택 |

양립 불가능한 충돌은 없다. 판정은 B이며 기존 branch의 유효한 고유 원칙을 수동 통합했다. 기존 branch는 현재 canonical 정책으로 대체되지만 새 PR merge 및 별도 삭제 승인 전까지 local/remote 모두 보존한다.

## 4. 최종 정책 결정

Canonical source는 [Task 종료 및 산출물 정책](../docs/12-task-completion-policy.md)이다. `AGENTS.md`와 Product Roadmap은 세부 정책을 복사하지 않고 해당 문서를 link한다.

최종 결정:

- 모든 Task는 Implementation report, SOP, User manual, Roadmap update, User validation checklist의 경로와 상태를 기록한다.
- 독립 파일이 아니어도 canonical section을 link할 수 있으나 단순 누락은 N/A가 아니다.
- 사용자 검수는 checklist 작성, 자동 검증 완료, 대기, 완료, 실패, 적용 대상 아님으로 구분한다.
- P0/P1은 완료·게시·merge 금지다. P2 예외는 사용자 승인과 risk 기록·후속 Task ID가 필요하다.
- implementation report는 상세 영향 항목, 검증, 미실행, 위험, 복구와 블로그 연계 4개 section을 포함한다.
- allowlist staging만 허용하고 `git add .`와 `git add -A`를 금지한다.
- 사용자 검수 대기 상태의 검토는 draft PR로 진행하며 완료 또는 merge로 간주하지 않는다.

5종 산출물:

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성 | 이 문서 |
| SOP | 작성 | [BASELINE-GOV-001 SOP](baseline-gov-001-sop.md) |
| User manual | 작성 | [BASELINE-GOV-001 User Manual](baseline-gov-001-user-manual.md) |
| Roadmap update | 작성 | [Product Roadmap](../docs/00-product-roadmap.md) |
| User validation checklist | Checklist 작성됨 / 사용자 검수 대기 | [Task 문서 10장](baseline-gov-001.md#10-user-validation-checklist) |

## 5. 수정 파일

| 파일 | 변경 목적 |
| --- | --- |
| `AGENTS.md` | canonical policy와 종료 전 확인 항목 link |
| `docs/00-product-roadmap.md` | Activity Feed 상태, governance Task, No-Go remediation, 추적·Decision Log |
| `docs/12-task-completion-policy.md` | 5종 산출물, 검수 상태, Finding, PII, 종료·게시 절차의 canonical 정의 |
| `tasks/baseline-gov-001.md` | Task 범위, branch 비교, 산출물, No-Go와 checklist |
| `tasks/baseline-gov-001-implementation-report.md` | 실제 결정·변경·검증·위험 보고 |
| `tasks/baseline-gov-001-sop.md` | Task 시작부터 branch 정리까지 실행 절차 |
| `tasks/baseline-gov-001-user-manual.md` | 비개발 참여자를 포함한 정책 사용 안내 |
| `tasks/notify-003-teams-activity-feed-foundation.md` | 확인된 사용자 display name 비식별화 |
| `tasks/notify-003-teams-activity-feed-research.md` | 확인된 사용자 display name 비식별화 |

삭제 파일은 없다.

영향 분류:

- Backend/Frontend/API/UI·UX/권한/Workflow: N/A — runtime code 변경 없음
- DB/Migration: N/A — schema와 data 변경 없음
- Excel/PDF/첨부파일: N/A — 관련 artifact 변경 없음
- UAT/외부 발송: N/A — 실행하지 않음
- Dependency/lockfile: N/A — 변경 없음

## 6. 개인정보 비식별화

- 변경 전: 2개 파일, 46개 line, 51 occurrences
- 변경 후 current checkout: 확인된 실제 사용자 이름 0 occurrences
- 같은 사용자는 두 문서에서 같은 `검수 사용자 A/B`로 매핑했다.
- email/UPN은 placeholder domain만 허용하고 actual tenant/client/object id 원문 후보를 별도 검사한다.
- current checkout의 기술적 검수 흐름은 유지했고 실제 값은 이 report와 terminal 결과에 복사하지 않았다.

Git history read-only 검사:

- 게시 전 전체 ref의 28 commits를 검사했다.
- 영향 commit: 1개
- 영향 file: 2개
- 영향 file은 비식별화한 NOTIFY-003 foundation/research 문서다.
- history rewrite, force push, tag/branch 재작성은 수행하지 않았다.
- P2 risk decision: repository owner/보안 담당을 risk owner로 지정하고 공개 범위 확인, current checkout 비식별화 유지, 공개 전 재검토를 완화책으로 삼아 `TASK-GOV-002`에서 rewrite 여부를 결정한다. 본 Task에서 rewrite하지 않는 것은 사용자의 명시적 범위 결정이다.

## 7. Roadmap 정합성

- Activity Feed provider/capability 완료와 event coverage를 분리했다.
- 관리자 수동 개인·업무 배정은 적용으로, L0~L2는 `TeamsPersonalChannelStrategy=TeamsActivity` 설정 선택형 부분 적용으로 기록했다.
- 자동 단계 핸드오프와 긴급/차단은 후속, 재검사와 프로젝트 완료 연결은 미확인으로 유지했다.
- NOTIFY-003의 완료는 provider/capability와 명시된 수동 발송 범위로 제한했다.
- 기존 `Activity Feed 후속` 결정은 역사적 기록임을 표시하고 현재 상태를 별도 표로 연결했다.
- governance 정책, 기존 branch 대체, Git history risk와 전역 No-Go remediation을 Decision Log에 추가했다.

## 8. 후속 Task

전역 No-Go remediation:

1. `TASK-UAT-001` — Safe UAT review mode
2. `TASK-SEC-001` — Frontend dependency security remediation
3. `TASK-NOTIFY-004` — delivery claim/lease, retry lineage, non-retryable, escalation starvation
4. `TASK-AUTH-001` — Last System Administrator concurrency guard

별도 risk decision:

- `TASK-GOV-002` — Git history 개인정보 처리 결정

후속 기능 후보:

- `TASK-UX-001` — Action Feedback A1/A2
- `TASK-NOTIFY-005` — 사용자별 알림 설정

전체 신규 기능 개발은 No-Go remediation P2가 해결되기 전까지 No-Go다.

## 9. 검증

실행 결과:

- `git diff --check`: 통과
- 신규 문서 trailing whitespace/final newline: 5개 파일, 오류 0건
- Markdown local link/anchor: 9개 문서, 24 links, 누락 target/anchor 0건
- 동일 heading 중복: 9개 문서, 0건
- 신규 Task ID: 7개 정의가 각각 1개이고 main 기존 정의 0건
- Activity Feed stale 표현: 2건이며 각각 NOTIFY-001의 역사적 포함 범위와 `역사적 결정` 행으로 분류, 현재 상태 충돌 0건
- canonical link, 5종 산출물, 6개 검수 상태, Finding gate와 자동/수동 checklist 정합성: 통과
- 확인된 실제 사용자 이름: current checkout 0건, 익명 사용자 A/B mapping count 일치
- email/UPN 후보: 61 occurrences 모두 placeholder domain, review 필요 0건
- GUID 후보: 8건 모두 delivery/correlation/Graph request evidence이며 tenant/client/object id 문맥 0건
- 신규 diff의 private key, actual webhook URL, bearer/token/password/secret 값: 0건
- tracked 비예제 `.env`, private key material, certificate와 generated path: 0건
- 변경 파일: allowlist 9개와 일치, runtime/dependency/migration/local env·인증서 변경 0건
- 기존 동일 목적 local/remote branch HEAD 보존: 확인
- Git history read-only scan: 게시 전 28 commits 중 영향 1 commit, 2 files
- allowlist cached file: 9개 전부 일치, 누락·범위 밖·삭제 0건
- cached whitespace/secret/runtime/dependency/migration/env·인증서: 오류 0건
- cached 검증 시 unstaged/untracked: 0건

미실행:

- backend build/test: runtime code 변경 없음
- frontend lint/typecheck/unit/build: frontend/dependency 변경 없음
- migration test: migration 변경 없음
- DB/UAT/worker/external send: 명시적 제외 범위

UAT/DB/container/worker 명령은 실행하지 않았다.

## 10. 제한사항

- 사용자 검수 checklist는 작성됐지만 아직 사용자가 완료하지 않았다. 상태는 `사용자 검수 대기`다.
- Git history의 과거 개인정보는 그대로이며 `TASK-GOV-002`의 별도 risk decision 대상이다.
- 기존 `docs/task-close-process-guidelines` branch는 대체 상태지만 삭제하지 않았다.
- 이 Task는 governance 문서만 변경하며 전역 No-Go P2의 runtime 해결을 포함하지 않는다.
- CI는 draft PR 생성 후 실행 여부와 실제 결과를 확인해야 한다.

## 11. 해결한 업무 문제

검수 문서의 실제 사용자 이름, Task별 종료 산출물 drift, checklist 존재와 검수 완료의 혼동, Activity Feed provider 완료와 event 적용 완료의 모순, 동일 목적 branch 중복을 하나의 추적 가능한 governance 기준으로 정리했다.

## 12. 기술적 결정과 검토한 대안

- 별도 canonical policy를 두고 `AGENTS.md`와 Roadmap은 link만 유지한다. 세 문서에 전체 정책을 복사하는 대안은 drift 위험 때문에 사용하지 않았다.
- 기존 branch를 merge/cherry-pick하는 대신 ref diff에서 유효 원칙만 수동 통합했다. 오래된 Roadmap 전체를 가져와 현재 구현 상태를 되돌리지 않기 위한 결정이다.
- 5종 산출물은 상태·위치 추적을 강제하되 5개 파일을 항상 강제하지 않는다. 이번 governance Task는 교육·운영 기준을 남기기 위해 SOP와 user manual을 실제 독립 문서로 작성했다.
- Activity Feed renderer capability를 event 연결 증거로 간주하지 않고 delivery 생성 경로가 확인된 범위만 적용으로 표시했다.
- Git history rewrite는 current checkout 비식별화와 분리해 `TASK-GOV-002`로 관리한다.

## 13. 시행착오 및 폐기한 접근

- 동일 Task ID branch만 충돌로 보는 접근은 동일 목적 branch를 놓칠 수 있어 폐기했다. branch 이름뿐 아니라 unique commit의 의미를 비교하도록 SOP를 보강했다.
- 기존 branch 전체 cherry-pick은 현재 Roadmap보다 6 commits 오래된 내용을 되살릴 수 있어 사용하지 않았다.
- P2가 하나라도 있으면 모든 문서 갱신을 막는 기존 문구는 risk를 Roadmap에 기록할 수 없으므로 채택하지 않았다. 게시 예외에는 더 엄격한 사용자 승인·영향·완화책·후속 Task ID를 요구한다.
- 개인정보 원문을 검색 결과와 report에 출력하는 접근을 사용하지 않고 count와 영향 file만 기록했다.
- 코드 전체 diff를 구현 보고서에 붙여넣지 않았다.

## 14. 사용자 검수 결과와 남은 항목

Checklist는 작성됐고 자동 검증 결과는 최종 검증 후 기록한다. 사용자 직접 확인 항목은 모두 미체크이며 상태는 `사용자 검수 대기`다. 따라서 draft PR 생성은 가능하지만 Task 완료와 merge로 간주하지 않는다.

남은 사용자 확인:

- 기존 branch 비교와 canonical 대체 판단
- 5종 산출물, SOP와 user manual의 이해 가능성
- Finding gate와 checklist 상태 구분
- current checkout 비식별화와 Git history 별도 결정
- Activity Feed provider/event 상태와 후속 Task 순서
- 전체 신규 기능 개발 No-Go 유지

복구: DB/runtime 변경이 없어 데이터 rollback은 없다. 문서 수정 복구는 승인된 비파괴적 Git 작업으로 이 Task diff만 역적용한다. 개인정보 원문 복원은 rollback 대상으로 권장하지 않는다.
