# TASK-UAT-MAINTENANCE-001 Implementation Report

## 1. 목적

Mutation worker의 effective 활성 상태와 DI 등록을 일치시키고 purge worker의 maintenance disable 경로를 제공한다.

## 2. 배경

HANDOVER-003은 purge worker를 끌 수 없어 pre-migration Phase A gate에서 중단됐다.

## 3. 기존 위험

Purge worker는 non-ReviewSafe에서 무조건 등록되고 즉시 `PurgeDueAsync`를 실행했다. Due row 0은 우연한 무변경일 뿐 실행 차단 증빙이 아니었다.

## 4. 구현 범위

Option, strict policy, 공통 worker activation, 조건부 DI, 내부 방어, runtime projection, backend tests와 isolated runtime 검증을 구현했다.

## 5. 제외 범위

Persistent UAT, migration, runtime handover, frontend UI, dependency와 다른 P2는 변경하지 않았다.

## 6. Existing worker gate inventory

| Worker | 기존 내부 gate | 기존 DI gate | 변경 후 DI gate |
| --- | --- | --- | --- |
| Delivery | Dispatch.Enabled | ReviewSafe만 | effective Dispatch.Enabled |
| Escalation | Escalation.Enabled | ReviewSafe만 | effective Escalation.Enabled |
| Purge | 없음 | ReviewSafe만 | AdminDeletionPurge.Enabled |

## 7. Option 설계

`AdminDeletionPurge:Enabled`는 기본 true이며 environment override가 가능하다. Static policy가 raw 값을 엄격하게 파싱해 malformed 값을 startup 전에 거부한다.

## 8. 기본값 정책

기존 Development purge 동작을 유지하기 위해 true다. 기존 delivery/escalation option과 동일하게 운영자가 Production에서도 false를 명시할 수 있고 status projection으로 확인한다.

## 9. DI registration

`MutationWorkerActivationPolicy` 결과를 Program과 runtime status가 공유한다. ReviewSafe는 세 값 모두 false다. Development는 true인 worker만 hosted service로 등록한다.

## 10. Defense-in-depth

Purge worker는 option false이면 시작 즉시 return하고, 실행 loop 진입 전에도 다시 검사한다. Direct-construction unit test의 service call은 0이다.

## 11. Runtime status

세 worker별 boolean과 aggregate boolean을 추가했다. 추가 JSON field는 기존 frontend consumer와 호환되고 UI 변경은 필요하지 않았다.

## 12. Default enabled 회귀

Default policy와 DI에 purge worker가 포함됐다. 직접 worker는 즉시 한 번 호출됐고 isolated due holiday는 기존 정책대로 삭제됐다.

## 13. Explicit disabled 검증

세 option false에서 hosted mutation worker 0, runtime projection false, direct purge service call 0, malformed startup 거부를 확인했다.

## 14. ReviewSafe 회귀

Purge true/false 두 조합에서 delivery/escalation/purge 미등록을 확인했다. 기존 mutation/provider/read-only tests도 통과했다.

## 15. Phase A simulation

Isolated backend 5595와 tmpfs DB를 사용했다. live/ready와 인증된 대표 GET은 200이었다. 두 관찰 구간에서 due delivery, Processing, due escalation, escalation row, due purge, attempt와 delivery count가 모두 동일했다.

## 16. Isolated DB 검증

Notification REL Candidate의 synthetic DB만 task-owned tmpfs DB로 복제했다. 실제 UAT row를 fixture로 사용하지 않았고 actual provider credential을 로드하지 않았다.

## 17. Persistent UAT 보호

Persistent UAT는 read-only aggregate만 확인했다. 0028 미적용, restart 0, volume/container와 기존 runtime listener 9/9가 유지됐다.

## 18. Backup 정책

Secure pre-0028 backup은 mode 600 상태로 유지하고 내용 열람·restore·Git 추적을 하지 않았다. Handover 재개 시 fresh backup을 새로 만든다.

## 19. 자동 테스트

| 검증 | 결과 |
| --- | --- |
| git diff / actionlint | 성공 |
| Release build | warning 0 / error 0 |
| Maintenance + ReviewSafe targeted | 14/14 |
| Backend 전체 | 331/331 |
| Frontend lint/typecheck/unit/build | 성공 / 61/61 |
| Full-Stack E2E | 16/16 |
| Phase A / default enabled isolated | 성공 |

## 20. 보안/PII

실제 이름, 이메일, 업무/알림 원문, row ID, credential, raw response를 출력하거나 문서화하지 않았다. Runtime response는 boolean만 추가했다.

## 21. Rollback

이 Task는 migration이 없다. 문제 시 conditional registration, option/status field와 tests를 revert한다. Persistent UAT와 backup rollback은 적용 대상이 아니다.

## 22. 제한사항

세 worker를 끄는 것은 HTTP mutation 차단이나 DB read-only를 의미하지 않는다. Phase A에서는 사용자 mutation을 별도 maintenance 통제로 막아야 한다.

## 23. 후속 Task

본 Task 사용자 검수/merge 후 TASK-UAT-HANDOVER-003을 preflight부터 재개한다.

## 24. 해결한 업무 문제

Maintenance 중 scheduled purge가 임의 실행될 수 있던 구조를 제거하고 세 mutation worker의 effective 상태를 운영자가 확인할 수 있게 했다.

## 25. 기술적 결정과 대안

- 범용 MaintenanceMode 대신 기존 worker option과 purge 전용 option을 사용했다.
- 내부 return만 두지 않고 DI 미등록을 1차 방어로 사용했다.
- Production false 금지 대신 기존 운영 option 정책과 일관된 명시적 disable을 허용했다.

## 26. 시행착오 및 폐기한 접근

Purge worker를 등록한 채 due row 0만 확인하는 방식은 실행 차단을 증명하지 못해 폐기했다. Runtime과 DI가 별도 계산하는 방식도 drift 위험 때문에 공통 policy로 대체했다.

## 27. 사용자 검수 결과와 남은 항목

Checklist 작성과 자동 검증은 완료됐다. 사용자 검수는 대기 중이며 완료로 표시하지 않는다. Merge 전 option 의미, Phase A 불변, backup 재생성 정책을 확인해야 한다.

## 28. 주요 파일 목록

- `backend/src/Emi.Qms.Api/MutationWorkerActivationPolicy.cs`
- `backend/src/Emi.Qms.Api/Admin/AdminDeletionPurgeOptions.cs`
- `backend/src/Emi.Qms.Api/Admin/AdminDeletionPurgeWorker.cs`
- `backend/src/Emi.Qms.Api/Program.cs`
- `backend/src/Emi.Qms.Api/ReviewSafe/ReviewSafeStatusService.cs`
- 관련 tests와 5종 산출물
