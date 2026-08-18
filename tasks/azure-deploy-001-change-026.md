# TASK-AZURE-DEPLOY-001 Change 026

## 상태

- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapSequenceMatch: `true`
- gateStatus: `PASS_REUSE`
- sourceTask: `TASK-WORKFLOW-CONTINUITY-001 Change 018`
- 사용자 승인: 2026-08-18 원격 `main` 병합과 Azure 공개배포 명시 승인
- mainMergeApprovalCount: `1`
- productionDeploymentApproved: `true`
- 상태: `PUBLICATION_APPROVED / CI_AND_RELEASE_PENDING`

## 게시 대상

1. 프로젝트 전체 흐름 상단과 단계별 개인화되지 않은 업무 건수 표시 제거
2. workflow `Requested`의 한글 표시를 `업무 요청됨`으로 변경
3. 관련 Backend·Frontend·Full-Stack 회귀와 Task 산출물

## 보존 경계

- `/my-work` 개인 업무, 업무 생성·상태 전이·알림·권한과 진행률을 변경하지 않는다.
- DB schema·migration·업무 데이터·외부 알림 설정을 변경하지 않는다.
- 운영 Web Push·Teams·메일 활성 상태와 Key Vault 참조를 보존한다.
- 호환용 `generatedWorkItemCount`, `workItemCount` API 필드를 제거하지 않는다.

## 게시 순서

1. 승인된 파일만 명시적으로 commit하고 Ready PR을 만든다.
2. 변경 인지형 필수 `CI Gate`를 통과한다.
3. PR을 `main`에 병합하고 exact 40자 merge SHA를 확인한다.
4. `Azure Pilot Release (Manual)`을 exact latest `main` SHA와 두 confirmation으로 실행한다.
5. 변경 분류가 Backend·Frontend `true`, migration `false`인지 확인한다.
6. Backend·Frontend ready, 공개 health와 익명 인증 차단을 확인한다.

## 검증 기준

- PR 필수 CI와 main CI
- Backend·Frontend image build·immutable digest
- migration skip
- Backend → Frontend revision 교체
- public security smoke
- Open P0/P1/P2 `0/0/0`
