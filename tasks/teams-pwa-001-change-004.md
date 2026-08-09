# TASK-TEAMS-PWA-001 Change 004 — Git 게시·Azure·Teams 공개 rollout 승인

- taskType: `UAT_RUNTIME`
- approvalSource: `USER_EXPLICIT_PUBLIC_DEPLOY`
- approvalDate: `2026-08-09`
- canonicalTaskId: `TASK-TEAMS-PWA-001`
- branch: `feat/task-teams-pwa-001-experience`
- baseSha: `914a109e170f4e1c3ce34fb1faa4216c1b4fcf1c`
- roadmapSequenceMatch: `true`

## 사용자 승인

사용자는 Change 001~003의 Teams launcher, PWA 설치 안내, `EMI PMS` 브랜드 통일 구현을 공개 배포하도록 명시 승인했다.

이 승인은 다음의 기존 승인형 운영 절차를 한 번 실행하는 범위다.

1. 현재 Task 변경만 feature branch에 commit·push한다.
2. Ready PR의 CI와 Finding Gate를 확인한 뒤 원격 `main`에 병합한다.
3. 병합 시점 최신 `main` full SHA로 `Azure Pilot Release (Manual)`을 실행한다.
4. 새 migration은 없으므로 기존 migration ledger가 Exact로 유지되는지 확인하고 Backend 다음 Frontend 순서의 revision 교체를 검수한다.
5. launcher-only Easy Auth 예외와 보호된 root·asset·manifest·API, 공개 health를 확인한다.
6. 운영 host와 기존 Activity app identity로 새 Teams package를 만들고 기존 10개 Activity type·RSC·deep link 계약을 검증한다.
7. 기존 조직 앱을 새 package로 갱신하고 운영 Teams launcher·Activity deep link를 확인한다.

## 보존 경계

- canonical clone의 사용자 WIP, HTTPS 5174와 Persistent UAT는 변경하지 않는다.
- Azure DB 업무 데이터는 삭제·초기화·수정하지 않는다.
- 신규 Entra app registration, NAA, OBO, Web Push, Service Worker와 알림 수신자·발송 시점 변경은 포함하지 않는다.
- 실제 secret·tenant/client identifier·사용자 계정·알림 원문은 Git, PR, workflow input과 보고에 기록하지 않는다.
- PR CI, Azure release 또는 Teams package 검증이 실패하면 다음 단계로 진행하지 않고 기존 운영 revision과 package를 유지하거나 승인된 rollback을 수행한다.

## 완료 Gate

- Ready PR CI 성공과 Open P0/P1/P2 `0/0/0`
- 원격 `main` 병합 및 main CI 성공
- Azure release의 image·migration·Backend·Frontend·공개 보안 검사 성공
- 운영 launcher 익명 접근과 핵심 앱 사전 인증 경계 확인
- 새 Teams package의 schema·아이콘·Activity 계약 확인 및 catalog 갱신 결과 확인
- 실제 Android·iPhone 설치와 Teams client 표시처럼 운영 적용 후에만 가능한 사용자 항목은 성공으로 추정하지 않고 별도 검수 상태로 남김
