# TASK-QR-001 Change 001 — experiment fast-track과 2차 기획 승인

## 실행 기준

- canonicalTaskId: `TASK-QR-001`
- taskType: `NEW_FEATURE`
- branch: `experiment/task-home-002-personalized-shell`
- startHead: `0b5b40be2b1967ec14a9eab0f05a6f2db4e969b2`
- representativeMain: `b8f3e2104074d05c2e71999c08a7374e8729f68f`
- interviewSource: `tasks/qr-001-interview.md`
- firstPlanningSource: `tasks/qr-001-planning.md`
- codexReviewSource: `tasks/qr-001-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/34-qr-scan-landing-plan.md`

사용자는 누적 실험 작업을 커밋한 뒤 QR 생성 작업을 바로 시작하라고 명시했다. 이 branch의 standing rule에 따라 사용자-facing interview·채택·중간 확인을 생략하고, Fable 1차 기획과 Codex review를 입력으로 Fable 2차 기획을 확정한 뒤 구현·검증·desktop/mobile screenshot·local commit까지 진행한다. 1차 Fable 원문과 2차 Fable 원문은 Codex가 수정하지 않는다.

## 승인·안전 경계

- planningApproved: `true` — 2차 기획 blocking decision 0 조건
- implementationApproved: `true` — 최종 2차 기획의 local experiment 범위
- commitApproved: `true` — 종료 산출물·검증·screenshot 완료 뒤 local commit만
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## Codex review 자동 채택 resolution

- QR은 256-bit CSPRNG opaque public token이며 인증수단이 아니다. 원문은 동일 QR 재출력을 위해 QR record에 보존하지만 log·audit·error에는 남기지 않는다.
- audit event는 서버가 관찰한 발급·rotation·image/print-sheet render·성공 resolve로 제한하고 unknown random token은 저장하지 않는다.
- current stage 담당 부서·사용자 부서·기존 write policy가 모두 맞을 때만 수정 화면으로 이동하며 나머지는 종합현황이다.
- revoked/deleted/restricted QR은 scope 밖 identity를 노출하지 않는다.
- 일괄 인쇄는 같은 project의 기발급 active QR 최대 50개이며 stale 대상 발견 시 전체 실패다.
- 로그인 return path는 same-origin `/q/` path만 1회 복원한다.
- SVG/PNG는 no-store·nosniff·안전 파일명과 exact decode test를 갖춘다.
- 현장 부착 상태, 완료 후 비활성, label template/프린터, Pending 상세는 이번 범위에서 제외한다.

## Claude 사용량 기록

Claude `/usage`는 `bash scripts/report-claude-usage.sh`의 privacy-safe projection만 기록한다. raw TUI와 계정 식별자는 저장하지 않는다.

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 0% 사용 / 100% 잔여 / 16:39 KST 초기화 | 27% 사용 / 73% 잔여 / 07-25 07:59 KST 초기화 | 53% 사용 / 47% 잔여 / 07-25 07:59 KST 초기화 |
| 1차 planning 직후 | 0% 사용 / 100% 잔여 / 16:39 KST 초기화 | 27% 사용 / 73% 잔여 / 07-25 07:59 KST 초기화 | 53% 사용 / 47% 잔여 / 07-25 07:59 KST 초기화 |
| 2차 planning 직전 | 16% 사용 / 84% 잔여 / 16:39 KST 초기화 | 28% 사용 / 72% 잔여 / 07-25 07:59 KST 초기화 | 55% 사용 / 45% 잔여 / 초기화 parse 불가 |
| 2차 planning 직후 | 16% 사용 / 84% 잔여 / 16:39 KST 초기화 | 28% 사용 / 72% 잔여 / 07-25 07:59 KST 초기화 | 55% 사용 / 45% 잔여 / 초기화 parse 불가 |

1차 runner는 `CREATED_FULL_BASELINE`, `baselineReused=false`, `driftStatus=NO_PRIOR_SESSION`으로 성공했다. model 456초, stdout 26,470 bytes, stderr 0이며 원문은 `tasks/qr-001-planning.md`에 byte-for-byte 저장됐다.

2차 runner는 `RESUMED_ARTIFACT_PREFLIGHT`, `baselineReused=true`, `driftStatus=UNCHANGED`로 성공했다. model 197초, stdout 21,047 bytes, stderr 0이며 최종 원문은 `docs/34-qr-scan-landing-plan.md`에 byte-for-byte 저장됐다.
