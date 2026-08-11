# TASK-NOTIFY-POLICY-001 Change 002 — 사용자 검수 완료와 공개 배포 승인

- changeType: `UAT_RUNTIME`
- changeDate: `2026-08-12`
- changeSource: `USER_EXPLICIT_REQUEST`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true`
- userValidationComplete: `true`
- pushApproved: `true`
- pullRequestApproved: `true`
- mergeApproved: `true`
- persistentMigrationApproved: `true`
- publicDeploymentApproved: `true`
- actualWebPushProviderApproved: `false`

## 사용자 승인

사용자는 구현 결과의 사용자 검수를 완료한 뒤 원격 `main` 병합과 Azure 공개 배포를 명시적으로 승인했다. Roadmap에 남은 구버전 담당자 fallback 문구는 별도 보고 후, 이미 확정·구현·검수한 부서장 fallback 규칙으로 정합화하도록 추가 승인했다.

## 게시·운영 적용 범위

- `TASK-PWA-PUSH-001`과 `TASK-NOTIFY-POLICY-001`의 승인된 전체 diff를 하나의 PR로 게시한다.
- 활성 Ruleset에 따라 직접 `main` push를 하지 않고 필수 `CI Gate`가 성공한 PR만 병합한다.
- 병합된 exact `main` SHA로 승인형 Azure workflow를 실행한다.
- migration `0074`, `0075`를 앱보다 먼저 적용하고 Exact ledger를 확인한다.
- 변경된 Backend와 Frontend image만 게시하고 Backend 다음 Frontend 순서로 revision을 교체한다.
- 기존 Teams Activity·메일 운영 설정과 이력을 보존한다.
- 새 Web Push는 운영 VAPID key와 실제 provider 검수가 별도 승인될 때까지 `Enabled=false`, `DryRun=true`를 유지한다.

## 완료 Gate

1. 최신 PR head의 `CI Gate`와 적용된 하위 검사가 모두 성공한다.
2. PR을 `main`에 병합한 뒤 정확한 merge SHA를 운영 release 입력으로 사용한다.
3. migration ledger가 `0075`까지 Exact다.
4. Backend·Frontend latest revision이 Ready·Healthy다.
5. 공개 health는 성공하고 익명 root·API는 Microsoft 365 사전 인증으로 차단된다.
6. Open P0/P1/P2가 `0/0/0`이다.
