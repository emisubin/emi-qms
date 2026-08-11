# TASK-PWA-PUSH-001 — Task Identity Gate

- proposedTaskId: `TASK-PWA-PUSH-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `AZURE_RELEASE_IN_PROGRESS`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-PWA-PUSH-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: 설치된 EMI PMS PWA가 기존 인앱 알림과 같은 알림을 운영체제 모바일 푸시로 수신하고, 사용자가 선택하면 해당 인앱 알림 또는 업무 화면으로 이동하게 한다.
- Root Finding 또는 정책 결정: 현재 PWA에는 설치 경험만 있고 Service Worker, Push API 구독 lifecycle, Backend Web Push delivery channel과 기기별 구독 저장소가 없다. 사용자는 이번 알림 정비에 PWA 푸시를 포함하고 인앱 알림과 일치시키도록 결정했다.
- 변경·검증 경계: 푸시 전용 최소 Service Worker, 브라우저 권한·기기 구독 UX, Backend 구독·발송·실패 처리, additive migration과 자동·실기기 검수를 포함한다. 오프라인 cache와 background sync는 포함하지 않는다. 실제 운영 provider key·발송·migration·runtime 교체는 별도 승인 경계로 유지한다.
- 보존할 불변조건: Backend 권한이 authoritative하며 인앱 알림이 알림 내용·수신자·상태의 source of truth다. 푸시 실패가 업무 transaction이나 인앱 알림 생성을 되돌리지 않는다. token·subscription secret과 개인정보는 산출물에 기록하지 않는다. 기존 Teams·메일 채널과 과거 delivery 이력은 보존한다.
- 예상 산출물: deep-interview, Fable planning, Codex review, 승인 후 구현·migration·tests, Implementation report, SOP, 사용자 설명서, Roadmap 갱신과 사용자 검수 checklist.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 검색 결과와 판정 근거

- `TASK-TEAMS-PWA-001`은 Web Push를 명시적으로 제외하고 후속 `NEW_FEATURE`로 이관했으므로 같은 목적의 구현 Task가 아니다.
- `TPWA-PUSH-001`은 기존 Implementation report의 backlog Finding 식별자일 뿐 canonical Task가 아니다.
- Web Push·PWA Push 목적의 local/remote branch와 open/merged PR은 없다.
- Roadmap의 현재 운영 Gate는 Azure release이지만, 사용자가 기존 알림 정책 정비와 PWA 푸시 신규 기능을 동시에 진행하도록 명시적으로 승인했다.
- 따라서 신규 canonical Task를 만드는 `PASS_CREATE`로 판정한다.
