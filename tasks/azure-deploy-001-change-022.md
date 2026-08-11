# TASK-AZURE-DEPLOY-001 Change 022 — PWA 구독·알림 정책 운영 배포

## Gate projection

- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true`
- gateStatus: `PASS_REUSE`
- sourceBaseline: `origin/main` `30a0c2970611f76cee0c96ebb8f0e6472d7e7aee`
- sourceTasks: `TASK-PWA-PUSH-001`, `TASK-NOTIFY-POLICY-001`
- mainMergeApprovalCount: `1`
- productionDeploymentApproved: `true`
- actualWebPushProviderApproved: `false`

## 사용자 승인과 배포 경계

사용자는 통합 기능의 사용자 검수를 완료하고 원격 `main` 병합과 Azure 공개 배포를 명시적으로 승인했다. 이 승인은 additive migration과 앱 교체를 포함하지만 운영 VAPID key 발급이나 실제 Web Push provider 활성화는 포함하지 않는다.

## 포함 범위

- migration `0074_web_push_subscriptions.sql`, `0075_notification_policy_alignment.sql`
- Web Push 구독·기기별 delivery·현재/전체 기기 해제 구조
- 알림 사건별 인앱·Teams Activity·메일 수신자와 채널 정합화
- 부서장 fallback 공유 업무·첫 처리자 동기화 종료
- 생산계획·구매 일정 원본 기반 미완료 업무 due date 동기화
- 알림 설정 화면과 PWA 최소 Service Worker
- 승인형 Azure workflow의 migration → Backend → Frontend와 공개 보안 smoke

## 보존 범위

- 새 Web Push 설정은 `Enabled=false`, `DryRun=true`를 유지한다.
- 기존 Teams Activity·메일 provider 설정과 과거 delivery 이력을 변경하지 않는다.
- migration은 additive이며 적용 뒤 schema 역삭제 대신 application rollback 또는 forward-fix를 사용한다.
- 공개 Frontend의 Microsoft 365 사전 인증, origin 차단, Backend private ingress와 exact-host 계약을 유지한다.

## 완료 Gate

1. 통합 PR 최신 head의 필수 `CI Gate`가 성공한다.
2. 병합된 exact `main` SHA만 운영 release에 입력한다.
3. migration ledger가 `0075`까지 Exact인 뒤에만 새 앱 revision을 활성화한다.
4. 변경된 Backend와 Frontend를 각각 immutable image로 게시하고 순서대로 교체한다.
5. 두 앱의 latest revision readiness와 public health를 확인한다.
6. 익명 root·API 차단과 인증 전 shell·bundle 비노출을 확인한다.
7. Open P0/P1/P2가 `0/0/0`일 때 완료로 판정한다.
