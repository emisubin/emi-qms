# TASK-QUALITY-OPERATING-MODEL-001 Change 006 — Item별 LQC·구매품별 IQC 최신 main 승격과 운영 배포

## Task Identity Gate

- proposedTaskId: `TASK-QUALITY-OPERATING-MODEL-001`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- instructionChainRead: true
- instructionConflictCount: 0
- baselineBranch: `origin/main`
- baselineSha: `12fd51947bfefe94a9abe1b4037bb6fcce6b2d81`
- applicableInstructions: `AGENTS.md`, `backend/AGENTS.md`, `frontend/AGENTS.md`, `scripts/AGENTS.md`
- samePurposeMatchCount: 1
- canonicalTaskId: `TASK-QUALITY-OPERATING-MODEL-001`
- reuseExistingTask: true
- roadmapSequenceMatch: false
- explicitRoadmapOverrideApproved: true
- gateStatus: `PASS_REUSE`

동일 목적은 Change 004의 Item별 LQC 운영 상태·양식·프로젝트 snapshot과 Change 005의 구매품 구분별 IQC 방식·양식이다. 로컬 checkpoint `5181726c85af90fd7760dbedf318b084484beae2`에 구현·자동 검증 결과가 하나뿐이고 열린 PR은 없다. 사용자는 2026-08-10 알림 후순위를 유지하면서 문서 동기화, 최신 원격 `main` 이식, 전체 검증, 사용자 검수 환경, 원격 `main` 병합과 Azure 운영 배포를 순서대로 진행하도록 명시 승인했다.

## 승인 상태

- planningApproved: true
- reviewResolutionApproved: true
- implementationApproved: true
- localCommitApproved: true
- pushApproved: true
- prApproved: true
- mainMergeApproved: true
- persistentUatApproved: true
- azureRuntimeApproved: true
- externalProviderApproved: false

게시·운영 승인은 자동 검증, 사용자 검수 상태, 열린 P0/P1/P2 0건과 GitHub `CI Gate` 성공을 모두 만족할 때만 사용한다. 실제 Teams·메일 발송, 알림 정책·에스컬레이션·Web Push 변경은 제외한다.

## Purpose identity와 Root Finding

- 업무 목표: 이미 구현된 Item별 LQC 중단·재개와 구매품별 IQC 양식을 최신 운영 제품에 안전하게 승격한다.
- Root Finding: 구현 checkpoint는 최근 Azure·Teams·PWA·CI 변경 전 `main`에서 갈라졌으므로 branch 전체 병합은 현재 디자인·인증·배포 계약을 되돌릴 수 있다.
- 변경 경계: Change 004·005의 제품 코드, additive migration `0070`·`0071`, 관련 tests·문서만 최신 `main`에 이식한다.
- 보존 불변조건: 기존 프로젝트 snapshot과 확정 검사 증빙, 18단계 workflow, Graphite UI, Easy Auth·PWA·Teams launcher, 현재 알림 provider 설정과 승인형 Azure release를 보존한다.
- 예상 산출물: 최신 main 통합 commit, 전체 자동 검증, 사용자 검수 환경, Ready PR·CI·main merge, migration `0070`·`0071`과 Backend→Frontend 운영 교체 결과.

## 새 CI 정책 적용

1. 코드 PR은 `Change Classification` 뒤 Backend·Frontend를 실행한다.
2. 두 선행 job이 성공한 최신 PR head에서만 Full-Stack E2E를 실행한다.
3. `CI Gate`가 필요한 job의 성공·취소·예상 밖 skip을 최종 판정한다.
4. 같은 PR의 이전 run 취소는 정상이며 최신 head만 게시 Gate로 사용한다.
5. `main` merge 뒤 Backend·Frontend와 `CI Gate`를 확인하고 Full-Stack skip은 정책상 정상으로 판정한다.
6. Azure 운영 release는 최신 `main`의 full SHA, image push 확인과 production deploy 확인을 모두 제출하는 수동 workflow만 사용한다.

## 이식·검증 순서

1. Change 004·005 diff를 최신 `origin/main`에 선택 이식하고 최근 변경과의 충돌을 계약 기준으로 해결한다.
2. 기존 migration `0001`~`0069`는 수정하지 않고 `0070`·`0071`을 additive로 유지한다.
3. Backend Release build·전체 tests, Frontend lint·typecheck·unit·build·mock UI, isolated Full-Stack E2E, migration fresh/existing 적용을 검증한다.
4. desktop·390px 양식 관리의 Item별 LQC와 구매품별 IQC를 사용자 검수 환경에서 확인한다.
5. allowlist staging·privacy/secret·generated artifact를 검사하고 commit·push·PR을 생성한다.
6. 최신 PR head의 새 CI `CI Gate` 성공과 사용자 검수 상태를 확인한 뒤 `main`에 병합한다.
7. 최신 `main` full SHA로 승인형 Azure release를 실행해 migration 성공 뒤 Backend→Frontend를 교체하고 공개 health·익명 인증 차단을 확인한다.

## 제외 범위

- LSE TASK NO, 부서 Pending, 설계 도번·필수값·패널 묶음
- 실제 IQC/LQC 검사 항목 content 입력과 검사 스위치 운영 활성화
- 알림 정책·수신자·에스컬레이션·Web Push 변경 또는 synthetic actual 발송
- 기존 프로젝트의 LQC/IQC snapshot 수동 변경
- 기존 확정 성적서·첨부·Pending·재검사 기록 수정·삭제
- Azure resource·DNS·Front Door·Entra 설정 변경

## Rollback·forward-fix

- 코드 게시 전에는 feature branch를 폐기하지 않고 최신 `main`을 그대로 보존한다.
- migration `0070`·`0071` 적용 뒤에는 테이블이나 snapshot을 삭제하지 않는다. 이전 image로 되돌려 신규 열을 읽지 않게 한 뒤 forward-fix한다.
- 운영 교체 실패 시 승인형 release script의 기존 revision rollback 계약을 사용하고 migration 성공 전 application 교체를 허용하지 않는다.

## 실행 현황

- 최신 main 선택 이식: 완료
- Backend Release build·전체 test: `PASS`, `491/491`
- Frontend lint·typecheck·unit·build: `PASS`, `190/190`, 기존 warning 1
- Mock UI: `PASS`, `8/8`
- LQC 선택 Full-Stack: `PASS`, `9/9`
- 전체 Isolated Full-Stack: `PASS`, `57/57`(독립 검증 Finding 보정 후 재실행)
- Open P0/P1/P2: `0/0/0`
- 독립 최종 재검증: `PASS`, 사용자 검수 runtime 개방 가능
- 사용자 검수: 완료(2026-08-11 사용자 명시)
- Commit·Push: 완료
- PR #91: 변경 분류·Backend·Frontend·Full-Stack E2E·`CI Gate` 전체 통과 후 squash merge 완료
- 원격 main: `064454d1d098e473e032ba23641beebce8892227`
- Azure release `31409582129`: migration `0070`·`0071` → Backend → Frontend 교체와 공개 보안 smoke 완료
