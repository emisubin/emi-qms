# TASK-AZURE-DEPLOY-001 Change 025

## 상태

- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true`
- 상태: 운영 게시 완료 — PR #103, exact `main` SHA와 Azure 공개 검증 완료
- 구현 기준선: `origin/main` `a5f13cf64bc09d0840e11ea6be0e5a507f185d0c`
- 사용자 승인: 2026-08-14 원격 `main` 병합과 Azure 공개배포 명시 승인
- mainMergeApprovalCount: `1`
- productionDeploymentApproved: `true`

## 게시 대상

- `TASK-ADMIN-003 Change 002`
  - 품질 부서장에게 IQC·LQC·OQC·구매품별 IQC와 LQC 운영 변경 권한 부여
  - 생산관리 부서장에게 생산계획·실적 연결과 Item별 제조 양식 권한 부여
  - 제조 부서장·일반 품질 사용자·기타 부서장의 양식 관리 화면과 mutation 차단
  - additive migration `0078_department_head_form_template_scope`
- `TASK-PWA-PUSH-001 Change 002`
  - Azure 추적 배포 정의에 현재 운영 Web Push 활성·실발송 상태 반영
  - VAPID Key Vault 참조와 Backend identity의 secret resource 단위 권한 보존
  - 실제 운영 secret 원문·사용자별 구독·알림 업무 정책은 변경하지 않음

## 제외 범위

- 사용자가 미완성으로 보존하도록 지시한 공통 매뉴얼 작업과 그 worktree
- 신규 알림 정책, VAPID key 회전, 직원별 PWA 강제 설치·구독 관리
- Azure resource 용량·가격·도메인·Teams package 변경
- 기존 프로젝트 양식 snapshot과 확정 검사 이력의 소급 변경

## 검증과 게시 순서

1. 통합 branch에서 권한·migration 집중 Backend 회귀, Frontend 양식 관리 회귀·typecheck·lint·build, Azure Bicep/ARM 검증을 통과한다.
2. Ready PR의 변경 인지형 필수 CI와 `CI Gate`를 통과한다.
3. 승인된 PR을 `main`에 병합하고 exact 40자 SHA를 확인한다.
4. `Azure Pilot Release (Manual)`을 exact `main` SHA로 실행한다.
5. migration `0078` 성공 뒤 Backend와 Frontend의 새 revision을 적용한다.
6. 두 앱 readiness, 공개 health, 익명 root·API 차단과 Web Push 설정·secret reference 보존을 privacy-safe projection으로 확인한다.
7. Open P0/P1/P2가 `0/0/0`일 때만 운영 게시 완료로 기록한다.

## 통합 전 자동 검증

| 검증 | 결과 |
| --- | --- |
| Backend 권한·migration·공개배포 집중 회귀 | `47/47 PASS` |
| Frontend 양식 관리 연관 단위 회귀 | `217/217 PASS` |
| Frontend typecheck·lint·production build | `PASS` — 오류 0, 기존 warning만 유지 |
| Azure Bicep compile·portal template·static validation | `PASS/PASS/PASS` |
| tracked conflict marker·diff whitespace | `0`, `PASS` |

## Finding

- `PUBLICATION-GITHUB-AUTH-PROJECTION-001` / P2 / `RESOLVED`: 게시 도구 사전 확인에서 원격 인증 상태의 계정 metadata가 현재 로컬 terminal 출력에 포함됐다. tracked 파일·Git diff·원격 PR에는 기록되지 않았고 credential 값 노출은 없었다. 이후 인증 확인은 성공 여부만 반환하는 privacy-safe projection으로 제한하며 실제 계정 식별정보를 산출물에 복사하지 않는다.
- `PUBLICATION-GITHUB-CONNECTOR-MERGE-001` / P3 / `RESOLVED`: GitHub connector의 PR merge가 현재 private repository를 찾지 못해 mutation 없이 종료됐다. 같은 승인 범위와 expected head SHA를 고정한 authenticated GitHub API로 병합했고 PR #103의 squash merge 결과를 다시 읽어 확인했다.
- `PUBLICATION-AZURE-ACR-TAG-PROJECTION-001` / P3 / `RESOLVED`: 첫 ACR read-only tag 조회가 tag 없는 manifest의 null 값을 처리하지 못해 실패했다. null tag를 제외하는 projection으로 재실행해 exact source tag와 두 운영 digest의 일치를 확인했으며 resource mutation은 없었다.
- `GHA-AZURE-RUNNER-WARNINGS-001` / P3 / `BACKLOG`: 성공한 release에 기존 Node.js action runtime 전환 안내와 Azure CLI version parse 경고가 남았다. 배포 결과에는 영향이 없으며 기존 Azure runner 유지보수 backlog에서 추적한다.
- Open P0/P1/P2: `0/0/0`.

## 운영 게시 결과

| Gate | 결과 |
| --- | --- |
| Ready PR | PR #103 squash merge 완료 |
| PR 필수 CI | run `31784473124` `PASS` — Backend·Frontend·Full-Stack·Workflow Validation·`CI Gate` 통과 |
| 원격 `main` | `58c089993587deea30513cb6edee0b8396a1d474` |
| main push CI | run `31786026056` `PASS` |
| Azure 운영 release | run `31786040822` `PASS` |
| 변경 분류 결과 | migration `PASS`, Backend `PASS`, Frontend `PASS`, public security `PASS` |
| migration execution | 최신 실행 `Succeeded`, manual trigger 유지 |
| 운영 revision | Backend `backend--0000027`, Frontend `frontend--0000018`; latest=ready·Running |
| exact image source | Backend·Frontend 운영 digest가 exact `main` SHA tag와 각각 일치 |
| 공개 검증 | health `200`, 익명 root·`/api/me` `401/401` |
| Web Push 보존 | `Enabled=true`, `DryRun=false`, 공개키·비밀키 Key Vault secret reference 유지 |
| Open Finding | P0/P1/P2 `0/0/0` |

- 사용자가 미완성으로 남기도록 지시한 공통 매뉴얼 worktree는 변경하지 않았다.
- 운영 secret 원문, 사용자별 구독과 업무 data를 조회·출력·변경하지 않았다.
