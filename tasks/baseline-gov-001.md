# BASELINE-GOV-001 개인정보 및 Task 거버넌스 기준선 정비

## 1. 목적

tracked 문서의 사용자 개인정보를 비식별화하고, 모든 Task에 적용할 종료 산출물·Finding gate·검수 상태 기준을 확립한다. 기존 동일 목적 branch를 read-only로 비교해 유효한 원칙을 통합하고, Product Roadmap의 Teams Activity Feed 상태와 후속 remediation 순서를 실제 기준에 맞춘다.

## 2. 범위

- `docs/task-close-process-guidelines` branch를 ref에서 read-only로 비교
- NOTIFY-003 foundation/research 문서의 확인된 사용자 display name 비식별화
- 전체 tracked checkout과 Git history의 개인정보·secret 후보 재검사
- canonical [Task 종료 및 산출물 정책](../docs/12-task-completion-policy.md) 확정
- `AGENTS.md`와 Product Roadmap에서 canonical policy 연결
- Activity Feed provider/capability와 event coverage 상태 분리
- 전역 No-Go P2와 후속 Task ID·순서 등록
- BASELINE-GOV-001의 5종 종료 산출물 작성
- allowlist staging, commit, push와 draft PR

## 3. 제외 범위

- 기존 branch checkout, merge, cherry-pick 또는 local/remote 삭제
- backend/frontend runtime code와 dependency 변경
- migration, DB, container, seed, master data 변경
- HTTP/HTTPS UAT와 worker 실행
- Teams, Mail, Activity Feed 실제 발송
- 테스트 데이터 정리
- 후속 P2 runtime 구현
- Git history rewrite, force push와 merge

## 4. 기존 branch 비교 결과

비교 대상은 `docs/task-close-process-guidelines`의 main 대비 유일 commit이다. branch를 checkout하지 않고 Git ref에서 `AGENTS.md`와 Product Roadmap diff를 확인했다.

| 정책 항목 | 기존 branch | 현재 작업 | 최종 채택 |
| --- | --- | --- | --- |
| Task 종료 절차 | 구현→리뷰→테스트→Roadmap/report→allowlist→CI→merge 순서 | 범위·검수 상태·5종 산출물 포함 | 기존 순서를 확장해 canonical 절차에 통합 |
| Implementation report | 독립 파일명과 20개 상세 항목 | 실제 범위·검증·위험·블로그 섹션 | 기본 파일명과 적용 가능한 상세 항목을 통합, 비적용은 N/A |
| SOP | 명시 없음 | 초안에서 N/A | 이번 Task부터 독립 SOP 작성 |
| User manual | 명시 없음 | 초안에서 N/A | 이번 Task부터 독립 user manual 작성 |
| Roadmap update | 종료 시 필수, 갱신 후보 열거 | 5종 산출물 중 하나 | 실제 구현·Task·추적·Decision Log 기준을 통합 |
| User validation checklist | 문서 검증 checklist 중심 | 자동/사용자 항목 분리 | 상태 모델과 함께 필수 적용 |
| N/A 처리 | 명시 없음 | 이유 포함 N/A | 구체적 비적용 근거가 있을 때만 허용 |
| checklist 존재/완료 구분 | 명시 없음 | 구분 | 6개 검수 상태로 확정 |
| P0/P1/P2/P3 gate | P0/P1/P2가 없을 때만 게시 | P2 승인형 예외 포함 | 사용자 지시의 P2 예외 요건으로 대체 |
| 사용자 검수 필요 여부 | UAT 단계만 언급 | 별도 상태 | checklist와 완료 상태를 분리 |
| 개인정보·secret 금지 | secret/token/env/email 등 금지 | 사용자·조직 식별자와 민감정보까지 확대 | 현재의 더 엄격한 범위 채택 |
| Commit/PR/Merge 조건 | allowlist, `git add .` 금지, CI | 사용자 승인과 gate | 기존 allowlist 원칙을 canonical에 통합 |
| 블로그 연계 기록 | 없음 | 4개 고정 섹션 | 현재 정책 채택 |
| 기존 branch 정리 절차 | 없음 | 자동 삭제 금지 | 새 PR merge 후에도 별도 승인 전 보존 |

판정은 B다. 양립 불가능한 충돌은 없으며 기존 branch의 유효한 원칙을 현재 정책에 수동 통합했다. 기존 branch는 이 Task의 canonical 정책으로 대체되지만 이번 작업에서 삭제하지 않는다.

## 5. 개인정보 정리

- 변경 전: 2개 파일, 46개 line, 51 occurrences
- 변경 후: 확인된 사용자 이름 0 occurrences
- 같은 사용자는 두 문서 전체에서 같은 `검수 사용자 A/B` 익명 식별자를 사용한다.
- 회사 이메일/UPN과 tenant/client/object id 원문 후보는 자동 검사 결과로 별도 확인한다.
- Git history는 read-only로 검사하고 rewrite하지 않는다.

## 6. Task 종료 정책과 5종 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성 | [BASELINE-GOV-001 Implementation Report](baseline-gov-001-implementation-report.md) |
| SOP | 작성 | [BASELINE-GOV-001 SOP](baseline-gov-001-sop.md) |
| User manual | 작성 | [BASELINE-GOV-001 User Manual](baseline-gov-001-user-manual.md) |
| Roadmap update | 작성 | [Product Roadmap 6.5.2, 21, 23~25, 27장](../docs/00-product-roadmap.md) |
| User validation checklist | Checklist 작성됨 / 사용자 검수 대기 | 아래 10장 |

## 7. Roadmap 정합성

- Activity Feed provider/capability 완료와 자동 event coverage를 분리한다.
- 수동 개인·업무 배정은 적용, L0~L2는 설정 선택형 부분 적용으로 기록한다.
- 재검사·프로젝트 완료 등 연결 근거가 없는 event는 미확인 또는 후속으로 둔다.
- `TASK-NOTIFY-004`, `TASK-UX-001`, `TASK-NOTIFY-005`를 정식 후속 Task로 등록한다.
- 전역 No-Go remediation을 `TASK-UAT-001`, `TASK-SEC-001`, `TASK-NOTIFY-004`, `TASK-AUTH-001`로 추적한다.

## 8. 검증 결과

자동 검증의 명령·결과와 미실행 이유는 [Implementation Report 9장](baseline-gov-001-implementation-report.md#9-검증)에 기록한다. 문서 전용 Task이므로 backend/frontend 전체 테스트와 DB/UAT는 실행하지 않는다.

## 9. 남은 P2와 개발 상태

| 분류 | 상태 | 후속 Task |
| --- | --- | --- |
| Governance/PII current checkout | 이 Task에서 수정 | BASELINE-GOV-001 |
| Git history 개인정보 | risk decision 필요 | TASK-GOV-002 |
| Safe UAT review mode | 미해결 | TASK-UAT-001 |
| Frontend dependency security | 미해결 | TASK-SEC-001 |
| Notification concurrency/retry | 미해결 | TASK-NOTIFY-004 |
| Escalation starvation | 미해결 | TASK-NOTIFY-004 |
| Last System Administrator concurrency | 미해결 | TASK-AUTH-001 |

Governance 문서 PR은 가능하지만 전체 신규 기능 개발은 남은 P2가 해결되기 전까지 No-Go다.

## 10. User validation checklist

체크리스트 작성과 사용자 검수 완료를 분리한다. 아래 항목은 사용자가 직접 확인하기 전까지 완료로 바꾸지 않는다.

### 10.1 자동 검증

- [x] `git diff --check` 통과
- [x] Markdown local link와 anchor 검사 통과
- [x] 수정 문서 heading 중복 검사 통과
- [x] Task ID 정의 중복 없음
- [x] Activity Feed stale 표현과 정책 정합성 검사 완료
- [x] `AGENTS.md` canonical link와 5종 산출물 상태 검사 완료
- [x] 확인된 실제 사용자 이름이 current checkout에서 0건
- [x] 회사 이메일/UPN 및 tenant/client/object id 원문 후보 0건
- [x] private key/webhook/bearer/token/password/secret 후보 0건
- [x] runtime/dependency/migration/DB/UAT 변경 없음
- [x] allowlist cached file 검증 통과

### 10.2 사용자 확인

- [ ] 기존 branch와 현재 정책 비교 내용이 정확함
- [ ] 기존 branch의 유효한 규칙이 누락되지 않음
- [ ] 현재 정책을 canonical로 사용하는 데 동의
- [ ] 기존 branch는 새 PR merge 전까지 보존됨
- [ ] 5종 산출물 정책이 이해 가능함
- [ ] SOP가 실제 순서대로 수행 가능함
- [ ] User manual이 비개발 참여자도 이해 가능함
- [ ] checklist 존재와 완료 상태가 구분됨
- [ ] Finding gate가 명확함
- [ ] 개인정보가 current checkout에서 제거됨
- [ ] Git history 개인정보 처리 방침은 별도 결정으로 남음
- [ ] Activity Feed provider/event 상태가 구분됨
- [ ] 후속 Task 순서가 명확함
- [ ] 전체 개발 No-Go가 유지됨

## 11. 완료 판정

자동 검증과 allowlist 게시가 성공해도 10.2가 미체크이면 상태는 `사용자 검수 대기`다. draft PR은 생성할 수 있지만 Task 완료 또는 merge로 간주하지 않는다.
