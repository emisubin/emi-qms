# TASK-AZURE-DEPLOY-001 Change 012 — Front Door 기존 토큰 재검증

## Task gate

- instructionChainRead: `true`
- taskType: `UAT_RUNTIME`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `Front Door validation·managed TLS → Entra → provider 검수`
- roadmapSequenceMatch: `true`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- gateStatus: `PASS_REUSE`
- executionStatus: `WAITING_EXTERNAL`

## 승인과 범위

- approvalSource: `USER_EXPLICIT_NEXT_TASK_START`
- 승인일: 2026-08-05
- 포함: DNS TXT·CNAME exact match 확인, 기존 validation token을 유지한 Front Door empty PATCH 재검증, Approved·TLS·route 상태 감시
- 제외: validation token 재발급, 가비아 DNS 변경, custom domain 삭제·재생성, public notification·application revision 변경

## 실행 결과

1. Azure validation token이 존재하고 가비아 TXT가 `1/1 exact match`임을 fixed boolean projection으로 확인했다.
2. CNAME도 Front Door endpoint와 `1/1 exact match`다.
3. Route는 custom domain 1개와 연결되고 enabled·HTTPS only이며 provisioning은 `Succeeded`다.
4. Microsoft Learn의 기존 token revalidation 절차에 따라 custom domain에 empty PATCH를 보냈다. token과 DNS record는 변경하지 않았다.
5. 재검증 요청 뒤 직접 상태 조회에서 domain은 계속 `Pending`, managed certificate·route deployment는 `NotStarted`다.
6. Backend·Frontend·ClamAV와 external notification 상태는 변경하지 않았다.

## Finding

| ID | 등급 | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `AZURE-AFD-ENDPOINT-PROJECTION-001` | P2 | `RESOLVED` | 잘못 추정한 endpoint 이름으로 read 요청해 Azure 오류가 임시 도구 출력에 subscription identifier를 포함했다. tracked 문서·보고·staging에는 남지 않았다. | 원문을 폐기하고 endpoint list의 fixed projection으로 실제 이름을 확인했다. 이후 존재 확인 전 하위 resource read를 금지한다. |
| `AZURE-AFD-WAIT-CONDITION-001` | P2 | `RESOLVED` | CLI wait가 빈 결과로 종료된 것을 Approved로 잘못 해석할 수 있었다. 즉시 authoritative `show` 재조회에서 Pending임을 확인해 TLS mutation 전 중단했다. | 완료 판정은 direct `show`의 exact enum과 deployment state를 함께 확인하는 것으로 고정했다. |
| `AZURE-AFD-DOMAIN-VALIDATION-001` | External gate | `OPEN` | TXT·CNAME exact match와 empty PATCH 재검증 뒤에도 Azure domain validation이 Pending이다. | DNS를 유지하고 Azure 처리를 기다린다. Approved 전환 뒤 managed TLS·route를 검증하며, 장기 지속 시 token 재발급보다 Azure Support를 먼저 검토한다. |

Open P0/P1/P2는 `0`이다.

## 재개 조건과 다음 Gate

- 재개 조건: authoritative domain state가 `Approved`로 전환됨
- 재개 직후: managed certificate와 route deployment 완료 감시 → Front Door HTTP `200`·직접 origin `403` → TLS hostname 검증
- token 재발급·DNS 변경·domain 삭제는 별도 사용자 승인 없이는 실행하지 않는다.
