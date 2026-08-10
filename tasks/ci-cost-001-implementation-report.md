# TASK-CI-COST-001 Implementation Report

## 1. 목적과 상태

- Task: `TASK-CI-COST-001`
- Task type: `P2_REMEDIATION`
- 기준 branch: `fix/task-ci-cost-001-actions-minutes`
- 기준 SHA: `5300b4646b2ea8bba0a43e953fea58e66caa2016`
- 상태: `LOCAL_IMPLEMENTATION_COMPLETE / AUTOMATED_VALIDATION_COMPLETE / PUBLICATION_APPROVED / GITHUB_VALIDATION_PENDING`
- 사용자 승인: 권장 최소안 구현·기존 실제 기기 검수와의 병렬 진행·Git 게시·PR·`main` merge 명시 승인 완료
- Git 게시: local 구현 commit 완료, push·PR·merge 실행 대기

## 2. 해결한 업무 문제

GitHub Actions 월 사용량이 90% 경고 뒤 100%에 도달해 남은 개발·게시 검증이 quota에 막혔다. 사용자가 결제 경계를 복구한 뒤 이 Task의 Git 게시와 `main` merge를 명시 승인했다. 최근 일반 CI는 Backend·Frontend·Full-Stack을 매번 동시에 실행했고, 문서 변경·같은 PR의 이전 commit·PR 검증 뒤 `main` merge에도 동일한 비용이 반복됐다.

`CI-MINUTES-OVERCONSUMPTION-001` P2의 root cause는 테스트 자체가 아니라 event와 변경 종류를 구분하지 않는 scheduling이었다. 제품 품질 검증을 줄이는 대신 다음 중복만 제거했다.

- 문서·Task 증빙만 바뀐 run의 제품 build/test
- 같은 PR의 이미 오래된 commit run
- 코드 PR에서 통과한 Full-Stack을 `main`에서 다시 수행하는 중복
- Backend·Frontend 실패가 확정됐는데도 시작되는 Full-Stack
- 매 Node job의 pnpm store 재다운로드
- 비정상적으로 장시간 멈춘 runner

## 3. 구현 결과

### 3.1 Workflow 구조

일반 CI는 다음 의존 관계를 사용한다.

```text
Change Classification
  ├─ documentation-only ──────────────────────────────┐
  └─ code/config ── Backend ─┐                        │
                 └─ Frontend ├─ PR: Full-Stack E2E ──┤
                              └─ main: skip ──────────┤
                                                     └─ CI Gate
```

- `Change Classification`: 두 commit SHA와 changed file을 검사한다. 파일 원문을 summary에 출력하지 않고 classification과 count만 출력한다.
- `Backend`·`Frontend`: 코드·설정 변경에서 기존 전체 build/test를 그대로 수행한다.
- `Full-Stack E2E`: 코드 PR이면서 Backend·Frontend가 모두 성공한 뒤에만 수행한다.
- `CI Gate`: `always()`로 실행해 필요한 job의 실패·취소·예상 밖 skip을 실패로 집계한다.

### 3.2 Fail-safe 분류

workflow-level `paths-ignore`는 사용하지 않았다. GitHub 공식 동작상 path filter로 workflow 전체가 생략되면 required check가 Pending으로 남을 수 있기 때문이다.

분류용 checkout은 최근 commit 두 단계만 가져온다. PR fork, 다중 commit push 또는 shallow history 차이로 기준 SHA를 확인할 수 없으면 `fail-safe`로 분류해 전체 CI를 실행한다. 분류 실패가 문서 전용 skip으로 바뀌는 경로는 없다.

### 3.3 Concurrency

- PR: `workflow + PR number`를 group으로 사용하고 새 commit에서 이전 in-progress run을 취소한다.
- `main`: `run_id`를 group fallback으로 사용하므로 서로 다른 push가 pending/in-progress 상태에서 취소되지 않는다.
- Azure 수동 release: 파일과 `cancel-in-progress: false` 계약 모두 변경하지 않았다.

### 3.4 Dependency cache와 timeout

- 공식 `actions/cache` Node 24 action을 full commit SHA로 고정했다.
- pnpm content-addressable store만 OS와 `pnpm-lock.yaml` hash 기준으로 cache한다.
- `node_modules`는 cache하지 않으며 `corepack pnpm install --frozen-lockfile`을 항상 실행한다.
- NuGet cache는 lockfile이 없어 이번 범위에서 보류했다.
- 정상 관찰 시간보다 여유 있는 timeout을 적용했다: 분류·Gate 5분, Backend 35분, Frontend 20분, Full-Stack 45분.

## 4. 영향 범위

| 영역 | 영향 |
| --- | --- |
| GitHub 일반 CI | scheduling·cache·timeout 변경 |
| Azure 수동 release | 변경 없음 |
| Backend/Frontend 제품 코드 | 변경 없음 |
| API·권한·workflow domain | 변경 없음 |
| DB·migration·seed | 변경 없음 |
| Runtime·DNS·Azure resource | 변경 없음 |
| Excel/PDF/첨부파일 | 변경 없음 |
| 사용자 UI·UX | 변경 없음 |

## 5. 변경 파일과 역할

| 파일 | 역할 |
| --- | --- |
| `.github/workflows/ci.yml` | 변경 분류, PR cancellation, job dependency, cache, timeout, `CI Gate` 구현 |
| `tasks/ci-cost-001-identity-gate.md` | purpose identity와 Roadmap 병렬 진행 승인 기록 |
| `tasks/ci-cost-001.md` | 승인 계약, 실행 매트릭스, 개발 SOP와 사용자 검수 checklist |
| `tasks/ci-cost-001-implementation-report.md` | 실제 구현·검증·Finding·rollback 원장 |
| `docs/00-product-roadmap.md` | 실행 큐·Task 상세·추적 항목·Decision Log 동기화 |

## 6. 기술적 결정과 검토한 대안

| 대안 | 결정 | 근거 |
| --- | --- | --- |
| workflow trigger의 `paths-ignore` | 제거 | workflow 미생성 시 required check Pending 위험이 있다. |
| 항상 실행되는 내부 분류 job | 채택 | 문서 전용 비용을 줄이면서 `CI Gate` 결론을 항상 남긴다. |
| 모든 `main` 검증 제거 | 제거 | direct/bypass 또는 merge 결과의 compile 회귀를 잡을 Backend·Frontend smoke는 유지한다. |
| PR Full-Stack 제거 | 제거 | 코드 PR의 통합 품질 Gate는 보존한다. |
| Backend/Frontend별 세밀한 path skip | 보류 | 초기 절감보다 분류 복잡도와 false-negative 위험이 커서 코드 변경에서는 둘 다 유지한다. |
| NuGet cache | 보류 | repository에 package lock이 없어 cache key의 결정성을 이번 Task에서 확대하지 않는다. |
| pnpm store cache | 채택 | lockfile·frozen install·content-addressable store로 dependency 검증을 보존한다. |
| self-hosted runner·유료 증액 | 제외 | 현재 과소비의 root cause인 중복 scheduling을 먼저 제거한다. |

## 7. 실행한 검증

| 검증 | 적용 여부 | 결과 | 근거/미실행 이유 |
| --- | --- | --- | --- |
| `actionlint .github/workflows/ci.yml` | 적용 | PASS | workflow syntax·expression·shell block 정적 검사 |
| `git diff --check` | 적용 | PASS | whitespace 오류 0 |
| exact classifier matrix | 적용 | PASS `4/4` | 실제 repository의 문서 commit·코드 commit으로 docs PR, code PR, code main, missing-base fail-safe 실행 |
| exact `CI Gate` matrix | 적용 | PASS `6/6` | docs pass, PR pass, main pass, classifier failure, Backend failure, Full-Stack failure |
| workflow contract read-only assertions | 적용 | PASS `16/16` | permission·concurrency·job topology·needs·timeout·40자 action pin·PostgreSQL digest |
| pnpm store path | 적용 | PASS | pnpm 11 store path 단일 값 확인 |
| pinned cache action readback | 적용 | PASS | 지정 SHA 존재와 `node24` runtime 확인 |
| Azure release diff | 적용 | PASS | `origin/main` 대비 변경 0 |
| 제품/runtime/dependency diff | 적용 | PASS | Backend·Frontend·infrastructure·scripts·package·lockfile 변경 0 |
| Markdown local link | 적용 | PASS `167/167` | 새 Task 산출물과 Product Roadmap의 local target 존재 |
| 새 문서 heading duplicate | 적용 | PASS | 중복 heading 0 |
| privacy/secret pattern | 적용 | PASS | 새 workflow·Task 산출물의 이메일·private key·대표 token pattern 0 |
| Backend·Frontend 전체 test | N/A | 미실행 | 제품 코드·dependency·runtime diff 0인 workflow scheduling Task |
| 실제 GitHub PR/main run | 적용 | 사용자 검수 대기 | commit·push·PR 미승인이라 GitHub-hosted 실행 전 |
| 1주 Actions 사용량 비교 | 적용 | 운영 관찰 대기 | 배포 후 표본이 필요함 |

임시 validation harness는 exact YAML `run` block을 실행하기 위해 Task 소유 경로에 생성했고 `4/4`, `6/6` 확인 직후 제거했다. tracked·untracked 잔여 artifact는 없다.

## 8. 예상 비용 효과

실제 billable minute는 queue, cache hit와 GitHub 반올림에 따라 달라지므로 다음은 기존 최근 job duration에 근거한 방향성 추정이다.

| 상황 | 변경 전 추정 | 변경 후 방향 |
| --- | --- | --- |
| 문서 전용 run | 약 38분 | 분류 + Gate의 소수 분만 사용 |
| 코드 `main` run | 약 38분 | Full-Stack 약 16분 중복 제거 |
| 선행 job 실패 PR | Full-Stack도 약 16분 사용 | Full-Stack 시작 안 함 |
| 같은 PR 연속 push | 이전 run과 최신 run 동시 소모 | 이전 run 즉시 취소 |
| Node dependency 설치 | job마다 store 재다운로드 | cache hit 시 다운로드 감소 |

코드 PR의 전체 품질 검증은 유지되므로 최신 PR run 자체의 기본 비용은 크게 줄지 않을 수 있다. 주요 절감원은 문서 run, 오래된 PR run, `main` Full-Stack과 실패 후 E2E다.

## 9. 개인정보·secret 검토

- changed file 원문은 CI summary에 기록하지 않고 aggregate count와 fixed enum만 기록한다.
- 실제 사용자, GitHub actor, 이메일, tenant/client/object ID, token과 credential을 문서 또는 workflow에 추가하지 않았다.
- workflow의 PostgreSQL 값은 기존과 동일한 non-secret disposable CI credential이다.
- cache key에는 OS와 tracked lockfile hash만 사용하며 secret을 포함하지 않는다.
- GitHub readback은 action commit SHA와 runtime fixed projection만 사용했다.

## 10. Finding

| Finding | Severity | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `CI-MINUTES-OVERCONSUMPTION-001` | P2 | `RESOLVED` | 모든 변경과 event에 3개 heavy job을 반복해 quota 소진 위험 발생 | 변경 인지·PR 취소·main E2E 중복 제거·선행 dependency·cache·timeout 구현, local matrix 통과 |
| `CI-MINUTES-SAVINGS-OBSERVATION-001` | P3 | `BACKLOG` | 실제 절감률은 GitHub-hosted 표본 없이는 확정 불가 | PR/main 실제 run과 최소 1주 사용량을 사용자 검수 checklist에서 관찰 |
| `GHA-AZURE-RUNNER-WARNINGS-001` | P3 | `BACKLOG / OUT_OF_SCOPE` | Azure release action/CLI 경고 | 기존 Azure Task backlog를 유지하며 이 Task에서 release workflow를 변경하지 않음 |

Open P0/P1/P2는 0건이다.

## 11. 시행착오 및 폐기한 접근

- workflow-level `paths-ignore`는 required check가 Pending으로 남는 공식 동작 때문에 폐기했다.
- 로컬 정책이 YAML shell block의 직접 동적 pipe 실행을 차단해 검증을 생략하지 않고 Task-owned 임시 harness로 exact block을 실행한 뒤 제거했다.
- 세밀한 Backend/Frontend path 분리는 절감 효과보다 분류 오류 위험이 커 이번 최소안에서 보류했다.
- Azure release warning 정리와 NuGet cache는 purpose가 달라 범위에 섞지 않았다.

## 12. 사용자 검수 결과와 남은 항목

- 자동 검증: 완료
- 사용자 검수: 구현·게시·`main` merge 승인 완료, 실제 GitHub 동작 검수 대기
- GitHub PR/main 실제 실행: 대기
- 동일 PR 이전 run cancellation 확인: 대기
- 최소 1주 사용량 추세: 대기
- Azure 실제 기기 검수·운영 관찰: 별도 기존 Task로 병렬 유지

## 13. Rollback과 복구

workflow 문제가 발견되면 `.github/workflows/ci.yml`만 기준 SHA의 이전 버전으로 되돌린다. 이 Task는 제품 image, Azure runtime, DB, migration과 사용자 데이터를 변경하지 않아 provider rollback이나 data restore는 필요하지 않다. rollback PR에서도 현재 branch protection과 사용자 Git 게시 승인을 따른다.

## 14. 종료 산출물 추적

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성 완료 / GitHub 실제 검수 대기 | 이 문서 |
| SOP | 작성 완료 | [Task의 개발 SOP](ci-cost-001.md#개발-sop) |
| User manual | N/A 기록 완료 | [Task의 User manual](ci-cost-001.md#user-manual) — 제품 UI 변경 없음 |
| Roadmap update | 작성 완료 | [Product Roadmap](../docs/00-product-roadmap.md#task-ci-cost-001-github-actions-minute-최적화) |
| User validation checklist | 작성 완료 / 실제 GitHub 검수 대기 | [Task checklist](ci-cost-001.md#사용자-검수-checklist) |
