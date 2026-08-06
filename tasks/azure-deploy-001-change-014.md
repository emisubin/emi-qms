# TASK-AZURE-DEPLOY-001 Change 014 — 공개 API Host allowlist와 로그인 Gate 종결

## Task gate

- instructionChainRead: `true`
- taskType: `UAT_RUNTIME`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `공개 API·로그인 검증 → Teams 승인·설치·provider 검수`
- roadmapSequenceMatch: `true`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- gateStatus: `PASS_REUSE`
- executionStatus: `VALIDATED_FOR_PUBLICATION`

## 승인과 범위

- approvalSource: `USER_EXPLICIT_AZURE_MAIN_MERGE_AND_ADMIN_IDENTITY_VERIFICATION`
- 승인일: 2026-08-06
- 포함: Change 013 원격 main 병합과 immutable Frontend image 교체, 공개 API·TLS·origin 보호 smoke, Backend exact internal host allowlist의 runtime·배포 원본 동기화, 현재 비상 관리자 계정의 실제 로그인·관리자 화면 접근 확인
- 제외: DB·migration 변경, bootstrap 관리자 secret 순서 변경, 실제 Teams·Gmail 발송, 알림 escalation 정책 변경, QOM 기능 branch 반영

## 확인된 원인과 최소 보정

1. Change 013의 Nginx 보정으로 내부 Backend route에는 도달했지만, Backend `AllowedHosts`가 공개 hostname만 허용해 Container Apps 내부 Backend hostname 요청을 `400`으로 거부했다.
2. 운영 runtime에는 공개 hostname과 Backend의 정확한 내부 FQDN 두 개만 허용했다. wildcard, 전체 Container Apps domain과 임의 hostname은 허용하지 않는다.
3. 배포 원본은 managed environment의 `defaultDomain`에서 `backend.internal.<defaultDomain>`을 결정적으로 구성해 다음 workload 재배포에서도 runtime 설정을 보존한다.
4. bootstrap 관리자 목록은 순서가 아니라 포함 여부로 판정한다. 현재 계정이 목록에 포함되고 실제 관리자 전용 화면까지 접근하므로 순서만 바꾸는 secret mutation은 하지 않는다.

## 검증 계획

1. Bicep compile·tracked ARM JSON 구조 동등성과 Azure artifact 정적 검증
2. Backend public deployment security 집중 test
3. 공개 HTTP redirect, HTTPS root·PWA·readiness·익명 보호 API, 직접 origin 차단과 TLS hostname 검증
4. 실제 Entra 로그인 뒤 관리자 메뉴·사용자 관리 화면 접근 확인
5. PR·main CI 성공 뒤 원격 main 반영

## Finding

| ID | 등급 | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `AZURE-BACKEND-HOST-FILTER-001` | P1 | `RESOLVED_RUNTIME` | 내부 route 도달 뒤 HTTP Host가 Backend 내부 FQDN인데 `AllowedHosts`는 공개 hostname만 허용해 readiness와 API가 `400`이었다. | 공개 hostname과 exact 내부 Backend hostname만 runtime에 허용해 readiness `200`, 익명 보호 API `401`을 확인했다. 동일 계약을 Bicep·ARM JSON·test에 고정한다. |
| `CI-FULLSTACK-FLAKE-001` | P2 | `RESOLVED` | Change 013 PR·main CI에서 서로 다른 기존 E2E 한 건이 각 실행의 시간 제한으로 간헐 실패했다. | 두 집중 spec이 각각 통과했고 최종 전체 main CI가 성공해 제품 회귀가 아님을 확인했다. 범위 밖 test 변경은 하지 않는다. |
| `PRIVACY-EVIDENCE-DOM-001` | P2 | `RESOLVED` | 실제 로그인 화면 확인 중 일시적 도구 출력에 표시 이름이 포함됐으나 tracked·staged artifact에는 기록되지 않았다. | 원문을 재사용하지 않고 이후 증빙을 역할·메뉴·접근 성공 여부의 fixed boolean projection으로 제한했다. |

Open P0/P1/P2 Finding은 `0`이다. actual Teams·Gmail provider는 disabled·dry-run을 유지하며 별도 Gate 전에는 활성화하지 않는다.

## Rollback

- application 문제가 있으면 직전 Backend revision으로 traffic을 되돌린다.
- host allowlist rollback은 public API `400`을 재발시키므로, exact 내부 host가 잘못 산출된 경우에만 새 exact host로 forward-fix한다.
- DB schema·data·migration 변경이 없으므로 DB rollback은 없다.
