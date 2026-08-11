# TASK-CI-COST-001 Implementation Report

## 1. 목적과 상태

- Task: `TASK-CI-COST-001`
- Task type: `P2_REMEDIATION`
- 기준 branch: `fix/task-ci-cost-001-actions-minutes`
- 기준 SHA: `5300b4646b2ea8bba0a43e953fea58e66caa2016`
- 상태: `MAIN_MERGED / GITHUB_VALIDATION_COMPLETE / USAGE_OBSERVATION_PENDING`
- 사용자 승인: 권장 최소안 구현·기존 실제 기기 검수와의 병렬 진행·Git 게시·PR·`main` merge 명시 승인 완료
- Git 게시: PR #89 Ready 전환·squash merge 완료, merge SHA `f50be6afc6896176bc2bdf32fa7ffbd59b1fd13b`, main CI 성공. closure PR #90 첫 실행에서 문서 전용 heavy 3개 job skip·`CI Gate` 성공

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

Git rename은 post-image 경로만 검사하지 않는다. `--no-renames`로 이전 경로 삭제와 새 경로 추가를 모두 분류하고 `-z` NUL 구분을 사용한다. 따라서 코드·설정 파일을 allowlisted 문서 경로로 rename해도 이전 코드 경로가 남아 전체 CI를 실행하며, 공백·개행을 포함한 유효한 Git 파일명도 한 경로로 안전하게 읽는다.

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
| exact classifier matrix | 적용 | PASS `7/7` | 실제 repository의 문서 commit·코드 commit으로 docs PR, code PR, code main, missing-base fail-safe와 code→docs·docs→docs·code→code rename 실행 |
| exact `CI Gate` matrix | 적용 | PASS `6/6` | docs pass, PR pass, main pass, classifier failure, Backend failure, Full-Stack failure |
| workflow contract read-only assertions | 적용 | PASS `19/19` | permission·concurrency·job topology·needs·timeout·40자 action pin·PostgreSQL digest와 rename/NUL 경계 |
| pnpm store path | 적용 | PASS | pnpm 11 store path 단일 값 확인 |
| pinned cache action readback | 적용 | PASS | 지정 SHA 존재와 `node24` runtime 확인 |
| Azure release diff | 적용 | PASS | `origin/main` 대비 변경 0 |
| 제품/runtime/dependency diff | 적용 | PASS | Backend·Frontend·infrastructure·scripts·package·lockfile 변경 0 |
| Markdown local link | 적용 | PASS `167/167` | 새 Task 산출물과 Product Roadmap의 local target 존재 |
| 새 문서 heading duplicate | 적용 | PASS | 중복 heading 0 |
| privacy/secret pattern | 적용 | PASS | 새 workflow·Task 산출물의 이메일·private key·대표 token pattern 0 |
| Backend·Frontend 전체 test | N/A | 미실행 | 제품 코드·dependency·runtime diff 0인 workflow scheduling Task |
| 실제 GitHub PR/main run | 적용 | PASS | PR #89 최신 head 분류·Backend·Frontend·Full-Stack·CI Gate `5/5` 성공. merge SHA main은 분류·Backend·Frontend·CI Gate 성공, Full-Stack skip |
| 1주 Actions 사용량 비교 | 적용 | 운영 관찰 대기 | 배포 후 표본이 필요함 |

임시 validation harness는 exact YAML `run` block을 실행하기 위해 Task 소유 경로에 생성했고 rename 3종을 포함한 classifier `7/7`, Gate `6/6` 확인 직후 제거했다. tracked·untracked 잔여 artifact는 없다.

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
| `REPOSITORY-VISIBILITY-ROADMAP-DRIFT-001` | P2 | `RESOLVED` | 실제 원격은 `PRIVATE`인데 Roadmap 일부 current status가 `PUBLIC`으로 남아 CI 과금 원인과 source of truth 충돌 | actual readback 기준 현재 `PRIVATE`를 실행 큐·Task·추적 항목·Decision Log에 동기화하고 과거 public 상태는 당시 이력으로 보존 |
| `CI-CLASSIFIER-RENAME-PREIMAGE-001` | P2 | `RESOLVED` | 기본 rename 감지가 post-image allowlisted 경로만 출력하면 code→docs rename을 문서 전용으로 오분류할 수 있음 | `--no-renames --name-only -z`와 NUL read로 이전·새 경로를 모두 검사하고 rename 3종 회귀를 classifier matrix에 추가 |
| `CI-MINUTES-SAVINGS-OBSERVATION-001` | P3 | `BACKLOG` | 실제 절감률은 GitHub-hosted 표본 없이는 확정 불가 | PR/main 실제 run과 최소 1주 사용량을 사용자 검수 checklist에서 관찰 |
| `GHA-AZURE-RUNNER-WARNINGS-001` | P3 | `BACKLOG / OUT_OF_SCOPE` | Azure release action/CLI 경고 | 기존 Azure Task backlog를 유지하며 이 Task에서 release workflow를 변경하지 않음 |

Open P0/P1/P2는 0건이다.

## 11. 시행착오 및 폐기한 접근

- workflow-level `paths-ignore`는 required check가 Pending으로 남는 공식 동작 때문에 폐기했다.
- 기본 rename 감지의 post-image 경로만 읽는 방식은 code→docs 우회가 가능해 폐기하고 delete+add 양쪽 경로 검사로 교체했다.
- 로컬 정책이 YAML shell block의 직접 동적 pipe 실행을 차단해 검증을 생략하지 않고 Task-owned 임시 harness로 exact block을 실행한 뒤 제거했다.
- 세밀한 Backend/Frontend path 분리는 절감 효과보다 분류 오류 위험이 커 이번 최소안에서 보류했다.
- Azure release warning 정리와 NuGet cache는 purpose가 달라 범위에 섞지 않았다.

## 12. 사용자 검수 결과와 남은 항목

- 자동 검증: 완료
- 사용자 검수: 구현·게시·`main` merge 승인과 코드 PR·main·문서 전용 closure PR 실제 동작 검수 완료
- GitHub PR/main 실제 실행: 완료 — PR #89 `5/5`, main 성공 4·skip 1
- 동일 PR 이전 run cancellation 확인: 완료 — PR #89 새 commit에서 이전 실행 `cancelled` 3건 확인
- 최소 1주 사용량 추세: 대기
- Azure 실제 기기 검수·운영 관찰: 별도 기존 Task로 병렬 유지

## 13. Rollback과 복구

workflow 문제가 발견되면 `.github/workflows/ci.yml`만 기준 SHA의 이전 버전으로 되돌린다. 이 Task는 제품 image, Azure runtime, DB, migration과 사용자 데이터를 변경하지 않아 provider rollback이나 data restore는 필요하지 않다. rollback PR에서도 현재 branch protection과 사용자 Git 게시 승인을 따른다.

## 14. 종료 산출물 추적

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성 완료 / GitHub 실제 검수 완료 / 1주 관찰 대기 | 이 문서 |
| SOP | 작성 완료 | [Task의 개발 SOP](ci-cost-001.md#개발-sop) |
| User manual | N/A 기록 완료 | [Task의 User manual](ci-cost-001.md#user-manual) — 제품 UI 변경 없음 |
| Roadmap update | 작성 완료 | [Product Roadmap](../docs/00-product-roadmap.md#task-ci-cost-001-github-actions-minute-최적화) |
| User validation checklist | 코드 PR·main·문서 전용 closure PR 자동 검수 완료 / 1주 관찰 대기 | [Task checklist](ci-cost-001.md#사용자-검수-checklist) |

## 15. Change 001 — 변경 영향 기반 CI와 선택적 Azure release

### 15.1 목적과 상태

- 기준 branch: `fix/task-ci-cost-001-change-001-latency-release`
- 기준 SHA: `9a25157f0b8d1e78ad5392acf336ebf3c0f61b64`
- 상태: `LOCAL_IMPLEMENTATION_COMPLETE / AUTOMATED_VALIDATION_COMPLETE / PR_DRAFT_VALIDATION_COMPLETE / MERGE_NOT_APPROVED`
- 승인: 사용자가 기존 CI·Azure release 검사와 소요시간 분석을 확인하고 권장 적용 순서의 local 구현을 명시 승인했다.
- 원격 설정: `main-pr-only` Ruleset에서 GitHub Actions 출처 `CI Gate`를 선택하고 사용자 재인증 뒤 저장했다. Ruleset readback에서 enforcement `active`, required check `CI Gate`, integration ID `15368`을 확인했다.

### 15.2 해결한 업무 문제

최근 성공 코드 PR 3건은 평균 약 38분 42초가 걸렸다. Backend 평균 약 18분 52초가 끝난 뒤 Full-Stack 평균 약 19분 18초가 시작돼 두 heavy 검사가 거의 직렬이었다. 동일 Git tree를 가진 PR head와 squash merge main commit도 확인됐지만 main이 Backend·Frontend를 평균 약 19분 09초 다시 실행했다. Azure 성공 release 5건은 평균 약 5분 59초였고 Backend·Frontend image build와 migration·두 revision 교체를 변경 범위와 무관하게 매번 수행했다.

Change 001은 테스트 수를 일괄 축소하지 않고 scheduling과 변경 영향 판정을 보정했다.

| 상황 | 기존 평균 | Change 001 예상 wall time | 예상 절감 |
| --- | ---: | ---: | ---: |
| 고위험 코드 PR | 약 38분 42초 | 약 22~23분 | 약 16분 |
| Backend 일반 변경 PR | 약 38분 42초 | 약 19분 | 약 20분 |
| Frontend 일반 화면 변경 PR | 약 38분 42초 | 약 3~4분 | 약 35분 |
| 검증된 동일 tree main | 약 19분 09초 | 분류+Gate 소수 초~1분 | 약 18분 |
| Azure 두 image 변경 | 약 5분 59초 | 두 build 병렬화로 약 1분 이내 단축 예상 | 실제 run 확인 필요 |
| Azure 한 component 변경 | 약 5분 59초 | 미변경 image·불필요 migration 생략 | 실제 run 확인 필요 |

실제 시간은 GitHub queue, cache와 Azure revision 준비 시간에 따라 달라지므로 PR·main·release 게시 뒤 재측정한다.

### 15.3 구현 구조와 영향

```text
PR changed files
  ├─ docs only ───────────────────────────────────────┐
  ├─ Backend ─────────────── Backend tests ───────────┤
  ├─ Frontend ── Frontend checks ─┐                  │
  │                               └─ high risk E2E ──┤
  ├─ workflow/Azure ── Workflow Validation ──────────┤
  └─ unknown ── all required jobs ───────────────────┤
                                                    CI Gate

Azure approved main source
  └─ diff since last successful main release
       ├─ Backend changed ── Backend image ─┐
       ├─ Frontend changed ─ Frontend image ├─ selected revision release
       └─ migration changed ─ migration ────┘
```

- Backend/Frontend: 제품 source는 변경하지 않았다. CI job 실행 조건만 분리했다.
- DB/Migration: schema와 SQL은 변경하지 않았다. migration 파일 diff가 있을 때만 기존 one-shot job을 실행한다.
- API·권한·Workflow: contract·인증·권한·workflow 변경은 Backend·Frontend·Full-Stack 전체 검증으로 분류한다.
- UI·UX: 변경 없음. Frontend 일반 변경은 기존 lint·typecheck·unit·build·mock UI E2E를 유지한다.
- Azure: resource·도메인·secret·image 내용은 변경하지 않았다. build job을 component별로 분리하고 release script가 선택된 component만 교체한다.
- Excel/PDF/첨부파일: 변경 없음.

### 15.4 주요 변경 파일

| 파일 | 역할 |
| --- | --- |
| `.github/workflows/ci.yml` | 영역별 job routing, Backend test와 Full-Stack 병렬화, Workflow Validation, always-run Gate |
| `.github/workflows/azure-pilot-images.yml` | 마지막 성공 main release 기준 누적 diff, component image 병렬 build, 선택 release/no-op |
| `scripts/classify-change-scope.sh` | privacy-safe changed-file 영향 분류와 unknown fail-safe |
| `scripts/verify-main-pr-ci.sh` | 활성 Ruleset·required `CI Gate`·merged PR·동일 tree·CI trust source 불변 확인 |
| `scripts/verify-ci-gate.sh` | 선택된 필수 job 결과의 단일 Gate 판정 |
| `scripts/deploy-azure-pilot-release.sh` | migration·Backend·Frontend 선택 실행과 component별 rollback |
| `scripts/validate-azure-image-publish-inputs.sh` | scope job의 source-only 검증과 기존 full secret shape 검증 분리 |
| `scripts/test-change-scope.sh`, `test-main-pr-ci.sh`, `test-ci-gate.sh` | 변경 분류·main trust·Gate positive/negative 회귀 |
| `scripts/test-azure-pilot-release.sh`, `test-azure-image-publish-inputs.sh` | 전체·선택·no-op Azure release와 source-only 입력 회귀 |
| `scripts/validate-azure-pilot-artifacts.sh` | 새 workflow·script contract와 기존 Azure artifact 검증 |
| `tasks/ci-cost-001-change-001.md`, Task·report·Roadmap | 승인 경계, SOP, 검수 checklist와 실제 상태 동기화 |

### 15.5 기술적 결정과 검토한 대안

| 대안 | 결정 | 근거 |
| --- | --- | --- |
| 모든 코드 변경에 전체 suite | 제거 | Validation Matrix의 실제 영향 경계로 Backend·Frontend·고위험 통합을 구분할 수 있다. |
| Full-Stack이 Backend 전체 test를 기다림 | 제거 | Frontend 빠른 Gate 뒤 Full-Stack과 Backend heavy test를 병렬화하고 최종 Gate에서 둘 다 확인한다. |
| main 검사를 항상 제거 | 제거 | Ruleset, merged PR, 동일 tree, 성공 `CI Gate`를 모두 확인한 경우만 재사용한다. |
| main을 PR tree와 이름만 비교 | 제거 | GitHub Actions integration ID, active default Ruleset과 CI trust source 자체 변경 제외를 함께 확인한다. |
| Azure에 이전 SHA 수동 입력 추가 | 제거 | 마지막 성공한 `main` 수동 release의 head SHA를 read-only로 조회하고 불명확하면 전체 release로 fallback한다. |
| Azure build 한 job에서 순차 실행 | 제거 | Backend·Frontend를 별도 job으로 분리해 둘 다 필요할 때 동시에 실행한다. 현재 environment에는 required reviewer/wait timer가 없어 승인 횟수 증가가 없다. |
| Backend test parallelization 활성화 | 보류 | 493개 테스트의 공유 DB·상태 격리 계약을 별도 분석하지 않고 변경하면 flaky 위험이 있어 scheduling 개선과 분리했다. |

### 15.6 실행한 검증

| 검증 | 결과 | 근거 |
| --- | --- | --- |
| `actionlint` CI·Azure workflow | PASS | YAML·expression·embedded shell 정적 검증 |
| 전체 `scripts/*.sh`, `scripts/lib/*.sh` syntax | PASS | shell syntax 오류 0 |
| change-scope matrix | PASS | docs, Backend test/store, contract, Frontend style/API, migration, CI/Azure workflow, unknown, rename, no-change, invalid 기준 13종 |
| main PR trust matrix | PASS | 성공, Ruleset 부재, PR 부재, tree mismatch, 실패 Gate, CI trust source 변경, API 실패 |
| `CI Gate` matrix | PASS | docs, Backend/Frontend/Full-Stack/Workflow 성공과 분류·job 실패/취소/skip 10종 |
| Azure release matrix | PASS | 기존 baseline·failure·rollback과 전체, Backend-only, Frontend-only, migration 포함, no-op 총 15종 |
| Azure input matrix | PASS | 기존 approval/SHA/secret/resource 9종 + source-only 1종 |
| Azure static artifact | PASS | Teams/PWA/image/release contract 유지 |
| Bicep compile·tracked JSON equality | PASS | foundation·identity-access·workloads·edge 4종 |
| `git diff --check` | PASS | whitespace 오류 0 |
| Backend·Frontend 제품 전체 test | N/A | 제품 source·dependency·DB·migration 내용 diff 0 |
| 실제 GitHub PR/main run | PR PASS / main 대기 | Draft PR #96 run `31458760784`: Change Classification 9초, Workflow Validation 17초, CI Gate 8초 성공. Backend·Frontend·Full-Stack은 workflow-only 변경으로 0초 skip. main은 merge 승인 전 미실행 |
| 실제 Azure release | 미실행 | 공개배포 승인 전 local 상태 |

### 15.7 개인정보·secret 검토

- changed path 원문, PR 번호·head SHA와 Ruleset 응답 원문을 Actions summary에 출력하지 않는다.
- summary는 fixed enum, boolean, changed file count와 승인 source SHA만 기록한다.
- 실제 사용자·이메일·tenant/client/object ID·secret·token·password를 tracked source에 추가하지 않았다.
- GitHub 재인증 비밀번호는 Codex가 입력·읽지 않았고, 사용자가 직접 재인증을 완료했다.

### 15.8 Finding

| Finding | Severity | 상태 | 영향·완화 |
| --- | --- | --- | --- |
| `CI-REQUIRED-GATE-REAUTH-001` | P2 | `RESOLVED` | 사용자가 GitHub 재인증을 완료했고, 활성 Ruleset의 GitHub Actions `CI Gate` required check와 integration ID `15368`을 readback으로 확인했다. |
| `CI-WORKFLOW-CHECKOUT-CONTEXT-001` | P2 | `RESOLVED` | PR #96 첫 run에서 isolated `CI Gate` job의 checkout 누락과 shallow policy checkout의 `origin/main` 누락을 확인했다. Gate checkout과 policy `fetch-depth: 0`을 추가한 뒤 다음 run 전체가 성공했다. |
| `ACTIONS-LIVE-ROUTING-VALIDATION-001` | P2 | `OPEN / MAIN_VALIDATION_PENDING` | Draft PR #96에서 workflow-only 분류, 제품 job 3개 skip, Workflow Validation과 CI Gate 성공을 확인했다. 코드 변경의 Backend/Full-Stack 병렬 시작과 동일-tree main skip은 후속 실제 run에서 확인한다. |
| `AZURE-SELECTIVE-RELEASE-LIVE-001` | P2 | `OPEN / DEPLOY_APPROVAL_PENDING` | 실제 component 선택·병렬 build·revision 교체 시간은 공개배포 승인 뒤 확인한다. local release·rollback matrix와 Bicep은 PASS다. |

Open P0/P1은 0건이다. 실제 GitHub runner·Azure release 검수에 해당하는 Open P2 2건 때문에 Change 001 완료·merge Gate는 아직 `NO-GO`다.

### 15.9 시행착오 및 폐기한 접근

- GitHub Ruleset REST 변경은 로컬 실행 정책이 원격 mutation 승인을 허용하지 않아 중단하고, 로그인된 GitHub 설정 UI에서 exact `CI Gate`를 선택했다. 사용자가 계정 재확인을 완료한 뒤 readback으로 설정을 검증했다.
- Azure workflow run API는 과거 `workflow_dispatch.inputs`를 제공하지 않아 source input 재사용 방식을 폐기했다. 현재 workflow가 main에서 실행된다는 기존 계약을 이용해 마지막 성공 main run의 `head_sha`를 기준으로 삼고 ancestry 실패는 전체 fallback으로 처리했다.
- Azure 두 image를 한 job에서 병렬 shell build로 바꾸는 방식은 기존 pinned build action·SBOM·provenance·cache 계약을 잃으므로 폐기하고 component별 job으로 분리했다.
- main의 성공 check 이름만 신뢰하는 방식은 다른 app spoof와 CI self-change 위험이 있어 폐기했다.

### 15.10 사용자 검수 결과와 남은 항목

- 자동 검증: 완료.
- 사용자 검수: workflow 변경이라 local 제품 화면 검수는 N/A. 실제 PR/main job 선택·병렬 실행 확인 대기.
- Git 게시: commit·push와 Draft PR #96 생성 완료. merge는 별도 승인 대기.
- 운영 적용: 미실행·별도 공개배포 승인 필요.
- 다음 순서: 별도 Codex read-only 독립 검증 → 사용자 merge 승인 → main 동일 tree 검수 → 별도 공개배포 승인 시 Azure 선택 release.

### 15.11 Rollback과 종료 산출물

- 일반 CI 문제: `.github/workflows/ci.yml`, change-scope/main-trust/Gate script와 tests를 함께 이전 버전으로 되돌린다.
- Azure 문제: `.github/workflows/azure-pilot-images.yml`, source validator, release script와 tests를 함께 이전 버전으로 되돌린다.
- 일부 revision 교체 뒤 실패: release script가 실제 변경된 component만 이전 immutable image로 되돌린다.
- DB schema·제품 data·Azure resource 사양 rollback은 N/A다.

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | Change 001 local 결과 기록 완료 | 이 절 |
| SOP | Change 001 작성 완료 | [Task Change 001 절](ci-cost-001.md#change-001--영향-영역별-ci와-azure-선택-release) |
| User manual | N/A | 제품 사용자 UI 변경 없음. 운영 확인은 같은 SOP·checklist 사용 |
| Roadmap update | 완료 | [Product Roadmap](../docs/00-product-roadmap.md#task-ci-cost-001-github-actions-minute-최적화) |
| User validation checklist | 자동 항목 완료 / 실제 GitHub·Azure 대기 | [Task checklist](ci-cost-001.md#change-001-사용자-검수-checklist) |
