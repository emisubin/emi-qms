# TASK-WORKFLOW-CONTINUITY-001 Change 018 구현 보고

상태: `IMPLEMENTED / AUTOMATED_VALIDATION_COMPLETE / MAIN_MERGED / AZURE_RELEASED / USER_VALIDATION_PENDING`

## 해결한 업무 문제

프로젝트 전체 흐름의 `내 업무` 문구와 건수는 로그인한 사용자의 실제 업무가 아니라 프로젝트 전체 업무 기록을 표시했다. 완료·취소 업무와 다른 담당자의 업무까지 포함될 수 있어 사용자가 개인 업무 건수로 오해했고, 단계 진행을 확인하는 화면에 불필요한 정보였다.

사용자는 전체 흐름에서 업무 건수를 모두 제거하고 단계 상태만 표시하기로 확정했다.

## 구현 결과

1. 전체 흐름 상단에서 `내 업무 N` 칩을 제거했다.
2. 18단계 카드에서 `· 내 업무 N건`을 제거했다.
3. `Requested` 상태 표시를 `내 업무 생성됨`에서 `업무 요청됨`으로 변경했다.
4. `/my-work`의 개인 업무 화면, 업무 생성·상태 전이·알림과 권한은 변경하지 않았다.
5. API의 `generatedWorkItemCount`, `workItemCount`는 기존 소비자 호환을 위해 유지하지만 프로젝트 전체 흐름 UI에서는 사용하지 않는다.

## 기술적 결정과 검토한 대안

- 생성 업무 수를 `생성 업무 N건`으로 바꾸는 대안은 프로젝트 진행 판단에 가치가 낮아 제거했다.
- 미완료 업무 수만 다시 집계하는 대안도 개인별 `내 업무` 화면과 역할이 겹쳐 적용하지 않았다.
- Backend 상태 label을 함께 변경해 mock과 실제 API가 동일한 한글 문구를 제공하도록 했다.
- API 필드 제거는 별도 소비자의 호환성을 깨뜨릴 수 있어 이번 표시 결함 범위에서는 보류했다.

## 영향 범위

| 영역 | 영향 |
| --- | --- |
| Frontend | 프로젝트 전체 흐름의 상단 칩·단계별 건수 제거 |
| Backend | `Requested` 상태의 한글 표시명만 변경 |
| DB·Migration | N/A — schema와 저장 데이터 변경 없음 |
| API | 필드 구조·상태 enum 불변, `statusLabel` 문구만 변경 |
| 권한·Workflow | N/A — 업무 생성·배정·상태 판정·진행률 불변 |
| 알림·Teams·메일·PWA | N/A — 발송 원본과 채널 처리 불변 |
| Excel·PDF·첨부 | N/A — 관련 소비 경로 없음 |

## 변경 파일

- `frontend/src/App.tsx`: 전체 흐름 업무 건수 표시 제거
- `backend/src/Emi.Qms.Api/Workflow/WorkflowStore.cs`: 중립적인 요청 상태 문구 적용
- `frontend/tests/App.test.tsx`: 상태만 표시하고 `내 업무`가 없는 계약 검증
- `backend/tests/Emi.Qms.Api.Tests/ProductionPlanningApiTests.cs`: 실제 API의 Requested 한글 label 검증
- `frontend/e2e/full-stack/workflow-continuity.full-stack.spec.ts`: desktop·390px 전체 흐름 표시와 overflow 검증
- Change 018 계약·구현 보고·사용자 검수 체크리스트·Product Roadmap

## 실행한 검증

| 검증 | 결과 |
| --- | --- |
| Backend Release build | 통과, 경고 0·오류 0 |
| Backend 격리 PostgreSQL 집중 회귀 | `1/1` 통과 |
| Frontend 전체 unit | `29 files / 218 tests` 통과 |
| Frontend lint | error 0, 기존 `src/main.tsx` Fast Refresh warning 1 |
| Frontend typecheck | 통과 |
| Frontend production build | 통과, 기존 chunk-size warning 유지 |
| 격리 Full-Stack desktop·390px | `1/1` 통과, `내 업무` 미표시·390px overflow 0 |
| `git diff --check` | 통과 |

## 시행착오 및 폐기한 접근

- 첫 Backend test는 새 프로젝트의 생산계획 단계가 `Requested`일 것으로 가정했지만 기본 생산계획 skeleton 때문에 실제 상태가 `InProgress`였다. 테스트를 제품 상태 계산에 맞춰 실데이터 영향이 없는 영업 정산 요청 업무 fixture로 바꾸고 다시 통과시켰다.
- 개발용 5432 PostgreSQL에 직접 연결하는 첫 실행은 DB가 꺼져 있어 실패했다. 운영·수동 UAT DB를 시작하지 않고 Repository의 tmpfs 격리 PostgreSQL runner로 재실행했다.

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution 또는 후속 위치 |
| --- | --- | --- | --- | --- |
| `WF-018-F01` | P2 | RESOLVED | 프로젝트 전체 업무 기록이 개인의 `내 업무`처럼 표시됐다. | 상단·단계별 업무 건수를 모두 제거했다. |
| `WF-018-F02` | P2 | RESOLVED | `Requested`가 `내 업무 생성됨`으로 표시돼 다른 담당자의 요청도 내 업무로 오해됐다. | `업무 요청됨`으로 변경했다. |
| `WF-018-F03` | P3 | BACKLOG | 호환용 업무 수 API 필드는 UI에서 더 이상 쓰지 않지만 향후 소비자가 다시 잘못 사용할 수 있다. | Product Roadmap 4.5 운영 관찰 backlog에서 API 정리 필요 시 별도 change로 처리한다. |

Open P0/P1/P2: `0/0/0`.

## 개인정보·secret 검토

- 자동 검증은 synthetic 데이터와 실행별 tmpfs PostgreSQL만 사용했다.
- 실제 사용자·고객·프로젝트 데이터, connection string과 provider secret을 산출물에 기록하지 않았다.
- Persistent UAT와 실제 알림 provider는 호출하지 않았다.

## 사용자 검수 결과와 남은 항목

- 자동 검증 완료.
- 사용자 검수 상태: `사용자 검수 대기`.
- 실제 검수 화면에서 전체 흐름 상단과 18단계 카드에 `내 업무` 건수가 없고 Requested 단계가 `업무 요청됨`으로 보이는지 확인하면 된다.
- 사용자가 2026-08-18 원격 `main` 병합과 Azure 공개배포를 승인했다. Git 게시와 운영 결과는 `TASK-AZURE-DEPLOY-001 Change 026`에서 추적한다.
- PR #108과 main CI가 통과했고 exact main SHA `51aba7e97a2d1fee0f9ee4b82a3f89d514171acf`의 Azure release run `32197298425`가 Backend·Frontend 교체와 공개 보안 검사를 완료했다. Migration 변경은 없어 실행하지 않았다.

## Rollback·forward-fix

- Frontend의 제거한 두 표시와 Backend label 한 줄만 이전 상태로 되돌릴 수 있다.
- DB·migration 변경이 없어 데이터 rollback은 없다.
- 업무·알림 이력은 수정하거나 삭제하지 않았다.

## 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | N/A | runtime·migration·운영 절차 변경이 없는 표시 보정 |
| User manual | 본 문서에 포함 | `사용자 검수 결과와 남은 항목` |
| Roadmap update | 갱신됨 | `docs/00-product-roadmap.md` 4.5 및 Decision Log |
| User validation checklist | 작성됨 / 사용자 검수 대기 | `tasks/workflow-continuity-001-change-018-user-validation-checklist.md` |
