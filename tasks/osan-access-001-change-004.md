# TASK-OSAN-ACCESS-001 Change 004 — 자동 사업부 진입과 청주 전용 관리 동선

## 1. 승인·Gate·기준선

- taskType: `BUGFIX`
- changeStatus: `USER_VALIDATED_AWAITING_PR_CI`
- userValidationStatus: `COMPLETED`
- userValidationSource: `USER_EXPLICIT_2026-09-08_NEXT_TASK_APPROVED`
- canonicalTask: `TASK-OSAN-ACCESS-001`
- canonicalChange: `TASK-OSAN-ACCESS-001 Change 004`
- instructionChainRead: true
- instructionConflictCount: 0
- taskIdentityGate: `PASS_REUSE`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `TASK-OSAN-ACCESS-001_CHANGE_003_LOCAL_VALIDATION`
- roadmapSequenceMatch: false
- explicitRoadmapOverrideApproved: true
- implementationApprovalSource: `USER_EXPLICIT_2026-09-07_AUTOMATIC_BUSINESS_ENTRY_AND_CHEONGJU_ADMIN_ONLY`
- implementationOwnerRequested: `GPT_5_6_SOL_HIGH_ONLY`
- implementationOwnerObserved: `NOT_REPORTED`
- implementationBranch: `fix/task-osan-access-001-integrated-user-approval`
- implementationBaseline: `958661459786acfb564591b3c46251c9b854c50c`
- gitPublicationApproved: true
- remoteCiApproved: true
- mainMergeApproved: false
- persistentRuntimeMutationApproved: false
- providerOperationApproved: false

사용자는 Change 003을 보존하면서 전용 사업부 선택 화면을 제거하고, 단일 membership과 지정 총괄을 유효한 tab 선택 또는 deterministic fallback으로 자동 진입시키며, 오산 shell의 관리자 navigation을 전부 제거하라고 명시 승인했다. 2026-09-08 검수 안내 직후 사용자의 최신 원문 `다음작업 승인.`은 Change 003·004 결과 검수 수락, 기존 Draft PR #121의 non-force 갱신과 최종 remote CI 1회 실행 승인이다. Exact `main` 병합, Azure와 Persistent UAT mutation은 포함하지 않는다.

## 2. Purpose identity와 검색 결과

- proposedTaskId: `TASK-OSAN-ACCESS-001`
- samePurposeMatchCount: 1
- canonicalTaskId: `TASK-OSAN-ACCESS-001`
- reuseExistingTask: true
- experimentStandingInstructionApplies: false
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`
- 업무 목표: 로그인 뒤 별도 선택 화면 없이 허용된 사업부로 진입하고, 사업부 전환은 정상 shell 우측 상단 selector에서만 수행하며, 모든 관리 동선은 청주 context로 한정한다.
- Root Finding: 다중 membership 총괄의 `selection_required`가 전용 gate와 선택 button을 노출하고, 오산 제한 shell이 사용자 관리 navigation과 오산 local 관리 화면을 제공한다.
- 변경·검증 경계: tab-scoped selection resolution, access gate에서 선택 UI 제거, 오산 navigation과 admin deep-link context 처리, 관련 component/API/browser 검증과 Task 기록.
- 보존할 불변조건: membership 0·local-profile-pending 상태, 사업부/권한/데이터 격리, 마지막 유효 tab 선택, request generation invalidation, 청주 기존 관리자 기능, Change 003 통합 사용자 승인, no provider/Persistent UAT mutation.
- 예상 산출물: 자동 fallback, compact selector-only 전환, 오산 admin navigation 0건, Cheongju 전환형 admin deep-link, targeted 검증·privacy-safe 시각 증거·local commit·exact-commit review runtime.
- 검색 범위: Task·Change 001~003·Implementation report, Roadmap, local/remote branch와 worktree, 기존 PR #121 기록을 확인했다.

## 3. Implementation Direction Brief

### 구현 방식과 이유

1. `/api/me`가 지정 총괄의 `selection_required`를 반환하면 현재 tab에 선택이 없는 경우 `CHEONGJU`가 허용되면 청주를, 아니면 응답의 첫 허용 사업부를 선택하고 기존 generation invalidation으로 `/api/me`를 다시 조회한다. 유효한 tab 선택은 기존 request header로 먼저 복구된다.
2. access gate는 no-membership과 local-profile-pending 안내·통합 승인 기능만 유지하고 사업부 선택 button·선택 전용 copy를 제거한다. Header selector는 실제 두 사업부를 이동할 수 있는 총괄에게만 compact select로 표시한다.
3. 오산 navigation에서는 관리자 항목을 조건 없이 제거한다. 오산에서 admin deep-link를 열면 청주 membership이 있는 지정 총괄은 URL을 보존한 채 청주 context로 전환한다. 그 외 사용자는 홈으로 replace해 fail closed한다. 전환 전 오산 admin page의 data request는 렌더하지 않는다.
4. 청주 `관리자 > 사용자 관리`와 Change 003 통합 승인 API/UI는 그대로 사용한다.

### Exact allowlist

- `frontend/src/api.ts`
- `frontend/src/App.tsx`
- `frontend/tests/BusinessUnitAccess.test.tsx`
- `frontend/e2e/mock-ui/business-unit-access.spec.ts`
- `tasks/osan-access-001-change-004.md`
- `tasks/osan-access-001.md`
- `tasks/osan-access-001-implementation-report.md`

### 제외 범위와 반환 조건

Backend resolver/API schema, DB/migration, 관리자 권한 확대, 새 route/page/design, 오산 관리 기능, Change 003 저장 계약, 전체 Backend 582·Frontend 297·Full-Stack 66·전체 CI/lint, push·PR·merge·Azure·Persistent UAT·실제 provider는 제외한다. 기존 API로 deterministic 진입이나 fail-closed deep-link를 만들 수 없거나 권한 확대가 필요하면 해당 의존 구현을 중단해 parent에 반환한다.

## 4. 사용자 관찰 완료 조건

- 단일 청주·단일 오산 사용자는 로그인 뒤 자동 진입하며 selector·빈 label이 없다.
- 양쪽 사업부 총괄은 유효한 tab 선택을 유지하고, 선택이 없거나 유효하지 않으면 청주 우선으로 자동 진입한다. 이후 우측 상단 selector로만 전환한다.
- membership 0과 local-profile-pending은 기존 대기·등록 필요 상태를 유지한다.
- 오산 desktop/mobile navigation에는 `사용자 관리`, `사업부 소속 관리`, 기타 관리자 item과 빈 관리 group이 없다.
- 오산 선택 상태에서 admin URL을 직접 열면 청주가 허용된 총괄은 청주 context의 해당 URL로 들어가고, 청주가 허용되지 않은 사용자는 홈으로 간다.
- 청주 관리자 navigation과 통합 사용자 관리에서 청주·오산 승인 대기 사용자의 사업부·부서·자동 역할·부서장·활성을 계속 지정할 수 있다.

## 5. 검증 계획

- Frontend typecheck.
- `BusinessUnitAccess.test.tsx`의 자동 선택, tab 선택 유지, pending 상태, 오산 navigation, admin deep-link, 청주 통합 사용자 관리 관련 targeted test만 실행한다.
- `api.test.ts`에서 영향받는 request-context test가 있으면 해당 test만 실행한다.
- `business-unit-access.spec.ts` 단일 mock browser spec에서 desktop·390px 자동 진입, selector 전환, 오산 관리자 navigation 부재, 청주 admin deep-link와 통합 승인 UI, 단일 membership/no selector를 확인한다.
- 필요할 때만 exact-head 격리 3-DB single smoke를 한 번 실행하고, 실패 시 직접 영향 test만 재실행한다.
- 전체 Backend/Frontend/Full-Stack/CI/lint는 사용자 지정 검수 전 테스트 정책에 따라 실행하지 않는다.

## 6. Runtime·게시 경계

이전 5198/5098 review harness는 같은 worktree의 owned parent PID와 두 listener를 확인한 뒤 정상 종료했다. 5174/5081과 canonical clone WIP는 변경하지 않는다. 구현·targeted 검증·local commit 뒤 같은 격리 3-DB harness로 exact commit의 5198/5098 검수 runtime만 다시 열어 둔다.

## 7. 구현·검증 결과

- `/api/me`의 지정 총괄 `selection_required`를 유효한 tab 선택이 없을 때 청주 우선, 아니면 허용 목록 첫 항목으로 고정하고 기존 request generation invalidation으로 다시 확인한다.
- 선택 전용 화면의 사업부 button·선택 copy·초기화 action을 제거했다. membership 0과 local-profile-pending 안내 및 총괄의 통합 승인 화면은 유지한다.
- 오산 navigation은 홈·프로젝트·진행 관리만 만든다. 모든 admin workspace는 오산에서 렌더하기 전에 청주가 허용된 총괄이면 청주로 전환하고, 그 외 계정은 오산 홈으로 replace한다.
- 청주 통합 사용자 관리의 사업부·부서·자동 역할·부서장·활성 저장 계약은 변경하지 않았다.
- Frontend typecheck: `PASS`.
- Targeted Vitest `BusinessUnitAccess.test.tsx`: 최종 `20/20 PASS`.
- 단일 mock browser spec: single-business desktop/mobile `1/1 PASS`; 최초 dual/tab 두 사례는 반응형 DOM의 숨은 selector를 조작한 test locator가 timeout되어 실제 visible selector로 보정했고, 실패한 두 사례만 재실행해 `2/2 PASS`했다.
- Desktop·390px synthetic screenshot을 직접 확인했다. 좁은 사용자 관리 표는 기존 compact row와 수평 scroll을 유지하고, 실제 데이터·credential·provider는 사용하지 않았다.
- 전체 Backend 582, Frontend 297, Full-Stack 66, 전체 CI/lint는 사용자 정책에 따라 실행하지 않았다.

## 8. 사용자 검수 항목

- [x] 단일 청주 사용자는 자동 진입하고 selector·빈 label이 없다.
- [x] 단일 오산 사용자는 자동 진입하고 selector와 왼쪽 관리자 item·빈 관리 group이 없다.
- [x] 지정 총괄은 선택 화면 없이 마지막 유효 tab 사업부 또는 청주 fallback으로 진입하고 우측 상단 selector로 양쪽을 전환한다.
- [x] 청주 관리자 메뉴와 통합 사용자 관리가 유지되며 청주·오산 승인 대기 사용자를 한 행에서 설정할 수 있다.
- [x] membership 0과 local-profile-pending 안내가 유지된다.
- [x] 오산에서 admin URL을 직접 열면 청주 가능 총괄은 같은 admin URL의 청주 context로 전환되고, 오산 단일 사용자는 홈으로 이동한다.

사용자 검수는 `COMPLETED`다. Exact-head runtime은 검수 뒤 owned session으로 정상 종료했고 5098/5198 listener와 해당 격리 Compose container·network·volume 잔여 0건을 확인했다. 다음 단계는 기존 Draft PR #121 non-force 갱신과 최종 remote CI 1회이며 exact `main` merge 전에서 멈춘다.

## 9. 첫 PR CI와 실패 보정

사용자 검수 기록 commit `d34d372220f3cf082752690986de212e544c31ec`을 기존 Draft PR #121에 non-force push해 자동 시작된 CI run `34136633185`를 최종 head의 첫 전체 회귀로 실행했다. Change Classification과 Workflow Validation, Frontend는 PASS했다. Full-Stack은 일반 64건 중 63건이 통과하고 1건이 실패해 사업부 격리 2건을 건너뛰었으며, Backend는 584건 중 583건이 통과하고 1건이 실패했다. CI Gate는 두 필수 job 실패를 반영해 FAIL했다. 같은 run을 재시도하거나 workflow를 수동 중복 실행하지 않았다.

- Full-Stack 실패는 mobile helper가 이미 선택된 `dev-quality`를 다시 `selectOption`해 인위적인 change event를 발생시키고, Change 004의 자동 사업부 확정에 따른 두 번째 shell mount가 바로 다음에 연 품질 menu를 닫는 test interaction race였다. 현재 값이 같으면 drawer를 명시적으로 닫고, 다를 때만 실제 사용자 전환을 수행하도록 helper를 보정했다. 실패 spec 한 건만 격리 3-DB로 재실행해 `1/1 PASS`했고 owned DB·container·network를 정리했다.
- Backend 실패는 Change 003 통합 저장으로 완전한 활성 Osan local profile을 받은 Entra onboarding 사용자를 뒤의 ReviewSafe 무변경 검증에서 여전히 `ApprovalPending=True`로 기대한 legacy assertion이었다. Directory와 local row 무변경 assertion은 유지하고 현재 승인 결과에 맞게 `ApprovalPending=False`를 기대하도록 보정했다. Test project targeted build는 경고 0·오류 0, 해당 3-DB fact는 `1/1 PASS`했고 owned DB·container·network를 정리했다.

보정 allowlist는 `backend/tests/Emi.Qms.Api.Tests/BusinessUnitIsolationTests.cs`, `frontend/e2e/full-stack/iqc-digital-report.full-stack.spec.ts`와 이 CI 기록을 위한 Change 004·implementation report다. 보정 commit의 non-force push가 새 전체 PR CI를 자동 시작하는 것은 실패 수정 뒤 최종 head를 검증하기 위한 불가피한 재실행이며, 별도 수동 dispatch나 첫 run retry는 하지 않는다.

첫 보정 commit `b8c6a2dcf73341ed980e31a833c30f497c1490aa`의 자동 CI run `34140283892`에서는 Backend `584/584 PASS`, Frontend PASS, 일반 Full-Stack `64/64 PASS`로 두 보정이 확인됐다. 이어진 business-unit access 격리 spec은 Change 003이 초기 `dev-sales` fixture를 승인 전 Entra pending으로 만든 뒤에도 단일 청주 선택을 기대했고, Change 004가 제거한 dual-admin 선택 화면과 reset 뒤 선택 화면도 계속 기대한 test drift 때문에 실패했다. Osan 격리는 선행 실패로 skip됐고 CI Gate는 FAIL했다.

`frontend/e2e/full-stack/business-unit-access.full-stack.spec.ts`를 실제 흐름인 membership 0 대기, 총괄의 청주 자동 fallback, 통합 승인, 별도 tab의 청주 자동 진입과 selector 오산 전환, reset 뒤 청주 자동 복구로 갱신했다. 이 격리 3-DB spec만 로컬에서 재실행해 `1/1 PASS`했고 synthetic database·bounded role·process·Compose cleanup을 확인했다. 이 후속 test-only commit push가 자동 생성하는 다음 PR CI가 최종 전체 검증이며 수동 dispatch나 이전 run retry는 하지 않는다.

격리 spec 보정 commit `b095ad97d96dcf792501d217b8db6cf6d25a4d33`의 자동 CI run `34142984018`은 Backend `584/584 PASS`, Frontend PASS였지만 일반 Full-Stack에서 mobile project-registration test가 초기 business 자동 확정 중 열린 drawer를 shell remount가 닫아 개발 사용자 selector를 기다리다 timeout됐다. 나머지 일반 63건은 PASS했고 business-unit·Osan 격리는 선행 실패로 skip돼 CI Gate가 FAIL했다.

같은 root cause를 한 건씩 기다리지 않도록 Full-Stack과 mock browser test의 개발 사용자 selector·drawer helper를 전수 대조했다. Mobile drawer 안에서 사용자를 바꾸는 helper는 Full-Stack 6파일에 있었고 mock-ui에는 없었다. 6개 helper 모두 network idle 뒤 visible drawer·selector를 확인하고, 현재 값과 같으면 select event 없이 drawer만 닫으며, 다른 값일 때만 select한 뒤 remount로 drawer가 닫히기를 기다리도록 정렬했다. 제품 코드와 desktop 직접 selector는 변경하지 않았다. 실패한 project-registration과 IQC 대표 test는 각각 `1/1 PASS`, 나머지 4개 helper 대표 test는 `4/4 PASS`, 관련 7개 spec targeted ESLint는 PASS했다. 모든 격리 DB·container·network를 정리했고 테스트가 갱신한 기존 screenshot은 원복했으며 새 임시 screenshot 1개는 제거했다.
