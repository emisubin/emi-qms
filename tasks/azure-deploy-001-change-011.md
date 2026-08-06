# TASK-AZURE-DEPLOY-001 Change 011 — migration 0069와 최신 앱 교체

## Task gate

- instructionChainRead: `true`
- taskType: `UAT_RUNTIME`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `migration 0069·Backend/Frontend revision → DNS/TLS → provider 검수`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- gateStatus: `PASS_REUSE`

## 승인과 범위

- approvalSource: `USER_EXPLICIT_DB_AND_APP_REPLACEMENT`
- 승인일: 2026-08-05
- 기준 원격 main: `b73356c`
- 포함: 최신 Backend image로 migration job 교체·실행, migration `0069 Exact` 확인, Backend·Frontend 최신 image 교체, readiness와 알림 비활성 상태 확인
- 제외: Front Door·DNS·managed TLS, Entra 운영 주소, public traffic, Teams·Gmail actual provider 발송

## 실행 전 기준선과 rollback

- migration job 실행 중 인스턴스: `0`, 마지막 실행: `Succeeded`, DB: `0068 Exact`
- Backend·Frontend·ClamAV: `3/3 Running`, latest revision과 latest ready revision 일치
- Backend·Frontend serving image는 직전 검증 digest로 고정되어 있었고 rollback 기준으로 보존했다.
- 새 Backend·Frontend ACR image는 기준 main SHA tag와 유효한 digest가 각각 존재했다.

## 실행 결과

1. migration job image를 최신 Backend digest로 교체하고 manual execution을 시작했다.
2. migration `0069_teams_activity_event_source_kinds` 적용과 전체 69개 migration `Exact`를 확인했다. 실행은 `Succeeded`, 실행 중 인스턴스는 `0`이다.
3. Backend를 최신 digest로 교체했다. 첫 image-only revision은 준비됐지만 기존 runtime 설정을 상속해 `Notifications__TeamsActivity__PersonalChannelStrategy`가 없었다.
4. 외부 알림 `Enabled=false`, `DryRun=true`를 보존하면서 누락된 값만 `TeamsActivity`로 추가했다. 최종 Backend revision은 `Healthy`, latest revision과 latest ready revision이 일치한다.
5. Frontend를 최신 digest로 교체했다. 최종 Frontend revision은 `Healthy`, latest revision과 latest ready revision이 일치한다.
6. ClamAV image와 revision은 변경하지 않았고 세 workload는 `3/3 Running`, single revision 100% 상태다.
7. Front Door domain은 `Pending`, deployment는 `NotStarted`로 유지했다. public traffic과 actual provider는 활성화하지 않았다.

## Finding

| ID | 등급 | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `AZURE-TEAMS-STRATEGY-CONFIG-001` | P2 | `RESOLVED_RUNTIME` | image-only update가 Change 009 이전 runtime env를 보존해 개인 Teams Activity 전략 값이 누락됐다. 외부 알림은 비활성·dry-run이라 실제 발송 영향은 없었다. | 누락된 값 하나만 추가하고 새 Backend revision의 Healthy·ready와 `Enabled=false`, `DryRun=true`, `PersonalChannelStrategy=TeamsActivity`를 재확인했다. |
| `PRIVACY-RUNTIME-LOG-PROJECTION-002` | P2 | `RESOLVED` | Azure streaming job log 명령이 JMESPath projection을 적용하지 않고 임시 도구 출력에 system log와 execution alias를 표시했다. PII·secret·업무 원문과 tracked/staged artifact는 없었다. | 원문을 폐기하고 문서·보고에 복사하지 않았다. 이후 증빙은 job status, migration count·Exact boolean과 fixed runtime 상태만 사용하며 raw streaming log 명령을 금지한다. |

Open P0/P1/P2는 `0`이다.

## 검증과 다음 Gate

- migration: `0069`, expected/applied `69/69`, `Exact`, execution `Succeeded`
- Backend: 최신 main digest, `Healthy`, latest=ready, replica `1`
- Frontend: 최신 main digest, `Healthy`, latest=ready, replica `1`
- workload: `3/3 Running`, ClamAV unchanged
- 알림: Teams Activity 전략 준비 완료, actual provider 비활성 유지
- 미실행: direct origin HTTP smoke는 실행 환경 정책상 재실행하지 않았다. 기존 origin 보호 설정은 변경하지 않았고 Front Door·public traffic은 계속 닫혀 있다.
- 다음 Gate: Front Door validation·managed TLS → Entra 운영 주소 → Teams 승인·설치와 Teams/Gmail actual provider 검수
