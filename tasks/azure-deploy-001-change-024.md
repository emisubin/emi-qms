# TASK-AZURE-DEPLOY-001 Change 024

## 상태

- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true`
- 상태: 운영 게시 승인 — PR CI, `main` 병합과 Azure 운영 검증 대기
- 기준선: `origin/main` `4520e641b98c1c464243e9988b1a373d57d49bed`
- 사용자 승인: 2026-08-14 원격 `main` 병합과 Azure 공개 배포 명시 승인
- mainMergeApprovalCount: `1`
- productionDeploymentApproved: `true`

## 게시 대상

- `TASK-PRODUCTION-CONTROL-001` Change 011
  - 프로젝트별 생산계획 시작·종료일과 실적 연결
  - 조회 화면의 계획·실적 이중 막대 일정표
  - 생산계획 행 삭제 후 저장 시 순번 충돌 보정
- `TASK-PROJECT-ASSIGNEE-DELEGATION-001`
  - 생산관리 사용자의 전체 생산계획 편집 권한 유지
  - 생산관리 외 부서장의 자기 부서 담당자 지정 권한
  - 프로젝트 생성 시 비생산관리 부서장 담당자 지정 알림

## 운영 변경 경계

- Backend와 Frontend 이미지를 병합된 정확한 `main` SHA로 생성하고 교체한다.
- 이번 변경에는 database migration이 없으므로 migration job은 실행하지 않는다.
- 기존 M365 인증, Teams·PWA·메일 알림 채널, 운영 도메인과 Azure resource 구성은 보존한다.
- 운영 비밀값, 실제 사용자 개인정보와 provider 설정값은 문서와 로그에 기록하지 않는다.

## 게시 절차와 완료 조건

1. Ready PR의 필수 CI와 `CI Gate`가 통과한다.
2. 승인된 PR을 `main`에 병합하고 병합된 정확한 SHA를 확인한다.
3. `Azure Pilot Release (Manual)`을 해당 SHA로 실행한다.
4. 변경 분류 결과가 Backend·Frontend 배포 `true`, migration `false`인지 확인한다.
5. Backend readiness 후 Frontend를 교체하고 공개 health와 익명 접근 차단 검사를 통과한다.
6. Open P0/P1/P2 Finding이 `0/0/0`일 때만 운영 게시 완료로 기록한다.

## 임시 작업공간 경계

- 운영 게시 후 실제 PR·SHA·release 결과를 source of truth 문서에 동기화해야 할 경우, 현재 5174 검수 runtime의 source를 바꾸지 않기 위해 최신 `origin/main` 기준의 bounded documentation worktree를 사용할 수 있다.
- owner는 현재 Codex 게시 작업이며 목적은 Change 024의 실제 운영 결과 기록으로 제한한다.
- 문서 PR 병합 후 clean, process 미사용과 commit reachable을 확인하고 승인된 범위 안에서 worktree만 제거한다.

## PR #101 첫 CI 결과와 보정

- 첫 Ready PR CI run `31770698395`에서 Change Classification, Backend, Frontend와 Workflow Validation은 통과했으나 Full-Stack E2E가 `59/61`로 실패해 `main` 병합과 Azure 운영 배포를 시작하지 않았다.
- 첫 실패는 삭제된 날짜별 체크형 생산계획표를 여전히 기대한 장기 사용자 흐름 검수의 오래된 계약이었다. 새 `생산계획표`와 `계획·실적 일정표`가 보이고 과거 캘린더 표는 없어야 한다는 현재 제품 계약으로 갱신했다.
- 두 번째 실패는 1280px에서 9열 생산계획표의 최소 폭이 상위 페이지까지 확장된 반응형 결함이었다. 9열 규격은 보존하고 해당 표 컨테이너 안에서만 가로 스크롤하도록 범위를 제한했다.
- 로컬 1280px 실제 화면에서 문서 폭과 화면 폭이 일치하고 표 내부 스크롤만 남는 것을 확인했다.
- 실패했던 생산계획 Full-Stack 시나리오를 포함한 연관 파일 회귀는 `17/17`, 영업 등록부터 세금계산서까지 18단계 장기 흐름은 `1/1`로 통과했다.
- 보정 commit의 새 Ready PR 전체 `CI Gate`를 다시 통과하기 전에는 `main` 병합과 Azure 운영 배포를 진행하지 않는다.
