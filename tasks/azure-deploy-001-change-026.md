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
- 상태: `MAIN_MERGED / AZURE_RELEASE_COMPLETE / USER_VALIDATION_PENDING`

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

## 실제 게시 결과

- PR: `#108`, squash merge 완료
- PR source: `25bcfcd97d0da24d659e6841aeb6e3ff902595ff`
- PR CI: run `32150934607` 최종 `PASS`
  - Backend·Frontend·Full-Stack·Workflow Validation은 최초 실행에서 통과했다.
  - 결제 제한으로 시작되지 않았던 `CI Gate`만 Repository 공개 전환 뒤 재실행해 통과했다.
- exact main SHA: `51aba7e97a2d1fee0f9ee4b82a3f89d514171acf`
- main CI: run `32197258001` `PASS`
  - 검증된 PR tree 재사용으로 Backend·Frontend·Full-Stack·Workflow Validation을 중복 실행하지 않았다.
- Azure 운영 release: run `32197298425` `PASS`
  - Backend·Frontend image를 병렬 생성하고 두 운영 revision을 교체했다.
  - migration 변경이 없어 DB migration은 실행하지 않았다.
  - public security smoke와 익명 root `401`을 확인했다.
- 사용자 검수: `대기` — 공개 프로젝트 전체 흐름의 상태 전용 표시를 직접 확인한다.

## 검증 기준

- PR 필수 CI와 main CI
- Backend·Frontend image build·immutable digest
- migration skip
- Backend → Frontend revision 교체
- public security smoke
- Open P0/P1/P2 `0/0/0`
