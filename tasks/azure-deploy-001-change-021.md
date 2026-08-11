# TASK-AZURE-DEPLOY-001 Change 021 — 공지 편집·개인정보 안내 통합 운영 배포

## Gate projection

- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true` — 사용자의 이번 통합 게시·병합·공개배포 명시 승인
- gateStatus: `PASS_REUSE`
- sourceBaseline: `origin/main` `c0e756eb598700fb55992af824280810e05d83da`
- sourceTasks: `TASK-NOTICE-EDITOR-001`, `TASK-PRIVACY-NOTICE-001`
- mainMergeApprovalCount: `1`
- productionDeploymentApproved: `true`

## 새 main 병합 규칙

- GitHub 활성 Ruleset `main-pr-only`를 live readback했다.
- 기본 branch는 Pull Request로만 변경하며 GitHub App의 필수 상태 검사 `CI Gate` 성공이 필요하다.
- 허용 merge 방식은 merge·squash·rebase이고 별도 승인 리뷰 수는 0이다.
- 저장소 내부 규칙의 해당 병합 사용자 명시 승인 1회는 2026-08-11 현재 요청으로 충족했다.
- 따라서 직접 `main` push 없이 하나의 통합 Ready PR을 검증한 뒤 병합한다.

## 포함 범위

- 공지사항 제한형 굵게, 작성자 수정, 첨부 등록·수정 화면 관리·인증 사용자 다운로드와 migration `0073`.
- 로그인 후 개인정보·이용 안내, 공통 footer 텍스트 진입점, 부드러운 무선택 목차 이동, EMI PMS logo 홈 이동, 프로필 사진 선택 안내와 PWA 설치·알림 안내.
- 관련 Task 계약·변경·구현·검수 문서와 Roadmap 상태 동기화.
- 승인형 Azure workflow의 변경 분류, migration→Backend→Frontend, public security smoke.

## 제외·보존 범위

- 새 알림 channel, Web Push 구현, Teams·메일 provider 변경, 개인정보 DB 동의 이력, 권한 확대와 Azure resource 사양 변경은 제외한다.
- 사용자 연락 값은 승인된 제품 화면 source 외 증빙·문서·로그에 복제하지 않는다.
- 운영 secret·tenant·resource identifier와 실제 사용자·업무 원문을 Git·명령 출력·검증 증빙에 남기지 않는다.

## 임시 통합 worktree

- 목적: 사용자 소유 WIP가 있는 canonical clone을 건드리지 않고 두 승인 후보를 최신 `origin/main`에 통합·검증·게시한다.
- owner: `TASK-AZURE-DEPLOY-001 Change 021`
- 기준 SHA: `c0e756eb598700fb55992af824280810e05d83da`
- 예상 종료: 통합 PR 병합과 Azure 공개 검증 완료 시점.
- cleanup 경계: process 미사용·clean·commit reachable을 확인한 뒤 별도 승인 범위에서만 제거한다. 자동 branch 삭제는 하지 않는다.

## 완료 Gate

1. 최신 main 통합 diff·migration 순서·개인정보 예외 경계를 확인한다.
2. 적용 Validation Matrix의 Backend·Frontend·Full-Stack·build·lint·privacy/secret 검사를 통과한다.
3. Ready PR 최신 head의 `CI Gate`와 하위 검사를 통과한다.
4. PR 병합 뒤 exact `main` SHA를 승인형 Azure workflow에 전달한다.
5. migration과 changed image 교체, 공개 health, 익명 root·API 차단을 모두 확인한다.
6. Open P0/P1/P2가 0일 때만 완료로 판정한다.

## 로컬 통합 검증 결과

- 기준: 최신 `origin/main` `c0e756eb598700fb55992af824280810e05d83da` 위 단일 통합 branch.
- Backend Release 전체: `496/496` PASS.
- Frontend: lint 오류 0·기존 경고 1, typecheck PASS, `27 files / 197 tests` PASS, production build PASS.
- isolated PostgreSQL Full-Stack: 최초 `55/58`에서 터치 대상 2건과 E2E locator 1건을 확인해 Change 007·Change 003으로 원인 보정, 집중 검증 PASS, 최신 commit 전체 `58/58` PASS.
- Open P0/P1/P2: `0/0/0`.
- 다음 Gate: privacy/secret·allowlist 확인 → Ready PR → required `CI Gate` → exact main SHA Azure release.
