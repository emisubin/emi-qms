# TASK-OSAN-ACCESS-001 Change 008 — 접근 상태·부서 역할·총괄 권한 정합성 보정

## 시작 Gate

- `instructionChainRead=true`
- `taskType=BUGFIX`
- `taskIdentityGate=PASS_REUSE`
- `canonicalTaskId=TASK-OSAN-ACCESS-001`
- `reuseExistingTask=true`
- `samePurposeMatchCount=1`
- `roadmapSequenceMatch=false`
- `explicitRoadmapOverrideApproved=true`
- 승인 출처: 사용자의 2026-09-08 세 운영 결함 전체 수정, 운영 데이터 보정, 원격 `main` 병합과 Azure 공개 재배포 명시 승인
- branch/worktree 기준선: `fix/task-osan-access-001-access-consistency` / bounded deployment worktree / exact `origin/main` `08c5366ff6ccfe34d4945b974e25c8ef93ee1121`
- 적용 지침: Root, Backend, Frontend, Scripts `AGENTS.md`; Product Roadmap; Task Completion Policy; Validation Matrix; Privacy-safe Evidence; Access Change 003~007·implementation report; Azure Change 032·SOP·report·handoff
- 구현 모델 요청: `gpt-5.6-sol` xhigh 단독. 실제 모델 관측값은 `NOT_REPORTED`.

## Purpose identity와 운영 Finding

- 업무 목표: 사용자 부서 변경과 승인 상태를 실제 권한 상태에 맞추고, 총괄 관리자가 두 사업부의 전체 permission catalog를 사용하도록 복구한다.
- Root Finding:
  1. 부서 기본 역할의 생성 근거를 저장하지 않아 관리부서의 `system-administrator`와 부서장 상태가 다른 부서로 이동한 뒤에도 남을 수 있다.
  2. 통합 목록은 Directory membership 존재만으로 승인 완료를 표시하지만 로그인과 기존 집계는 로컬 역할 유무를 사용해 동일 사용자를 다르게 표시한다.
  3. `system-administrator` 역할이 후속 업무 permission 일부를 받지 않아 총괄의 전체 입력 계약을 충족하지 못한다.
- 보존할 불변조건: Directory/Cheongju/Osan DB 분리와 no-fallback, 기존 Cheongju 업무·G2·provider, 일반 사용자 단일 membership, 복수·마지막 총괄 보호, local-first 공개, 낙관적 version·멱등 operation·공통 audit correlation, Osan 관리자 navigation 미노출.

## 구현 방향과 exact allowlist

1. Business additive migration `0088`에 사용자 역할 배정 출처(`explicit`, `department-default`, `overall-administrator`)와 System Administrator 전체 permission mapping을 추가한다. 과거 migration과 identity contract `0086`은 변경하지 않는다.
2. 통합 저장은 기존 `explicit` 역할만 보존하고 현재 부서 기본 역할과 총괄 역할을 서버에서 재계산한다. 부서가 바뀌면 부서장 여부는 확인 marker가 있을 때만 다시 설정한다.
3. 승인 readiness는 active membership, active local profile, 유효 부서와 해당 부서 기본 역할을 모두 요구한다. 로그인, 기존 관리자 집계와 통합 목록이 같은 정책을 사용한다.
4. Directory publish 실패도 `RetryRequired`로 남기며, 오래된 `Preparing`을 식별자 없는 boolean으로 투영해 같은 operation ID 재시도를 허용한다.
5. Pending 행에서 사업부를 선택하면 일반 사용자의 target membership 활성 의도를 함께 만들고 다른 사업부는 비활성으로 유지한다. compact table과 기존 menu 구조는 유지한다.
6. 운영 보정은 migration 뒤 privacy-safe dry-run count가 예상 범위일 때만 기존 backfill job의 멱등 repair 단계로 실행한다.

허용 파일:

- `database/migrations/0088_system_administrator_access_consistency.sql`
- `backend/src/Emi.Qms.Api/Identity/ApprovalReadinessPolicy.cs`
- `backend/src/Emi.Qms.Api/Identity/DepartmentIdentityPolicy.cs`
- `backend/src/Emi.Qms.Api/Identity/SeedIdentityData.cs`
- `backend/src/Emi.Qms.Api/Identity/DbIdentityStore.cs`
- `backend/src/Emi.Qms.Api/Identity/UserAdministrationStore.cs`
- `backend/src/Emi.Qms.Api/Identity/IdentityEndpointExtensions.cs`
- `backend/src/Emi.Qms.Api/Authorization/EntraClaimsTransformation.cs`
- `backend/src/Emi.Qms.Api/Home/HomeMetricsEndpointExtensions.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitAccessAdministrationStore.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitAccessEndpointExtensions.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitMembershipBackfillRunner.cs`
- 변경에 직접 대응하는 Backend migration·identity·3-DB tests
- `frontend/src/App.tsx`, `frontend/src/identity.ts`와 관련 targeted tests
- Azure release의 migration/backfill 정적 계약과 생성 artifact(실제 필요가 확인된 경우만)
- 이 change, Access/Azure implementation report·SOP·handoff·checklist와 Product Roadmap

## 완료 조건과 검증

- 일반 사용자의 부서 이동 후 이전 부서 기본 역할과 묵시적으로 남은 부서장 상태가 같은 저장에서 제거된다. 명시 역할은 보존하고 overall의 양 DB System Administrator는 유지된다.
- Directory membership과 local readiness가 불일치하면 승인 완료로 표시하지 않는다. 저장 중단은 `Preparing` 또는 `RetryRequired`로 보이며 같은 operation을 안전하게 재시도할 수 있다.
- System Administrator permission 수와 전체 permission catalog 수가 두 business DB에서 일치하며 API 정책을 우회하지 않고 전체 조회·입력을 허용한다.
- 복수 overall과 마지막 overall 보호, ordinary 단일 membership·selector 미노출, 총괄 selector, cross-DB deny가 유지된다.
- 로컬에서는 Backend build, 영향 filtered tests, migration fresh/existing, Frontend targeted/typecheck/build와 격리 3-DB/UI smoke를 수행한다. 전체 회귀는 exact PR head의 원격 CI 한 번만 수행한다.
- 운영에서는 migration → repair dry-run/count → repair apply → Backend → Frontend → privacy-safe DB/API/UI smoke 순서를 지킨다. 예상 밖 대상·삭제·권한 확대는 해당 mutation에서 중단한다.

## Rollback

- `0088`은 additive로 남기고 down migration을 수행하지 않는다. 이전 image는 추가 column·permission row를 무시할 수 있어 image rollback과 호환된다.
- 앱 결함은 직전 Backend revision `39`, Frontend revision `28`의 immutable digest로 되돌린다. 데이터 보정은 audit와 assignment source를 기준으로 forward-fix한다.
- 기존 업무 record, 일반 사용자 명시 역할과 provider 설정은 삭제하지 않는다.

## 구현 결과

- `ApprovalReadinessPolicy`로 로그인 claim, `/api/me`, 관리자 사용자 집계, 홈 집계와 통합 사용자 목록의 승인 준비 판정을 통일했다. 활성 membership만 있어도 local profile·활성 부서·그 부서 기본 역할이 하나라도 빠지면 승인 대기로 남는다.
- 통합 저장은 `user_roles.assignment_source`를 읽어 `explicit` 역할만 보존하고, 현재 부서 기본 역할과 overall System Administrator를 서버에서 다시 만든다. 부서가 바뀌면 부서장 flag는 같은 요청의 명시 확인 marker가 있을 때만 true가 된다.
- 기존 backfill job은 대상의 현재 membership을 잠그고 일반 사용자 다중 membership을 거부한다. 일반 사용자의 현재 부서 기본 역할 provenance를 정규화하고, 현재 부서와 다른 `department-default` 역할 및 ordinary의 잘못 남은 System Administrator를 제거한다. 그 보정이 발생하면 과거 부서장 flag도 해제하며 privacy-safe aggregate 보정 수를 job log에 남긴다. 활성 Cheongju local readiness가 없는 membership은 Directory audit와 함께 차단하고 이미 Osan으로 이동한 일반 사용자와 ready profile은 그대로 둔다. Overall과 비기본 `explicit` 특별 역할은 보존한다.
- `0088_system_administrator_access_consistency.sql`은 기존 역할 source를 보수적으로 분류하고 System Administrator에 현재 permission catalog 전체를 매핑한다. 새 permission insert에도 같은 역할을 자동 매핑하는 DB invariant를 추가했다. identity contract 상수 `Directory 0001` / `Business 0086`은 변경하지 않았다.
- Compact 사용자 관리 표는 pending 사업부 선택을 활성 의도와 연결하고, `RetryRequired` 또는 2분 이상 진행이 멈춘 `Preparing`을 같은 operation ID로 재시도할 수 있게 표시한다. 기존 표·selector·Osan navigation 계약은 유지했다.

## 집중 검증 결과

- Backend Release compile: PASS, warning `0`, error `0`.
- Frontend TypeScript typecheck: PASS.
- Migration `0088` existing 적용·2회 멱등, assignment source, 전체 permission와 future insert invariant: `1/1 PASS`.
- 격리 3-DB 통합 사용자 접근 단일 Fact: `1/1 PASS`. Roleless active membership 회수와 허용된 `AccessRevoked` audit, 현재 부서 default provenance 정규화, 부서 이동 시 stale System Administrator/head 회수와 비기본 explicit 역할 보존, 승인 readiness, 복수 overall·양 DB 전체 permission, local 실패 membership `0`, 멱등 retry, version 충돌, ordinary dual 거부와 회수를 포함한다.
- Compact 사용자 관리 mock Chromium desktop/mobile와 selector: `3/3 PASS`.
- 첫 migration test 시도는 기존 localhost 개발 DB의 인증 상태가 fixture와 달라 제품 코드 도달 전 실패했다. Owned 임시 PostgreSQL fixture로 전환한 뒤 통과했고 임시 container를 정리했다. 제품 test failure는 없다.
- 사용자 지시대로 local 전체 Backend/Frontend/Full-Stack suite는 실행하지 않았다. 전체 회귀는 exact PR head의 원격 CI 한 번으로 수행한다.
- 최초 PR CI run `34195929283`에서 기존 component fixture가 새 `explicitRoles` provenance를 투영하지 않아 explicit System Administrator 보존 기대가 실패했다. Runtime 로직이 아니라 fixture 계약 누락으로 분류해 projection을 정렬했고, 실패한 단일 test `1/1`과 영향받은 `BusinessUnitAccess.test.tsx` `20/20`이 통과했다. 새 exact head의 원격 CI를 최종 회귀로 사용한다.
- 두 번째 CI run `34196291730`은 fixture의 빈 `explicitRoles`가 TypeScript에서 `never[]`로 추론된 정적 오류를 찾았다. Fixture 값을 `string[]`으로 명시하고 frontend typecheck와 같은 targeted file만 재검증한다.
- 세 번째 CI run `34196560187`은 일반 Full-stack 64건 중 61건 PASS, 3건 FAIL, Backend 585건 중 563건 PASS, 22건 FAIL이었다. 실패는 System Administrator를 감사 전용으로 가정한 기존 API/E2E 기대, 새 approval readiness에 필요한 부서가 빠진 identity fixture, migration 이후 달라진 permission 배정 수치가 Change 008 계약과 충돌한 테스트 계약 drift였다. Pending 생성 201과 프로젝트·생산계획·구매·패널 편집 권한을 permission catalog 계약으로 정렬하고, 승인된 identity fixture에는 유효 부서와 기본 역할을 함께 준비하며, 테스트 데이터 상태를 바꾸는 중복 hold 요청은 제거한다. 제품 runtime 로직 추가 변경은 없다.
- 세 번째 CI에서 실패한 Backend 메서드만 theory를 포함해 격리 PostgreSQL에서 재실행했고 `39/39 PASS`했다. Full-stack 실패 3건도 같은 환경에서 각각 재실행해 `3/3 PASS`했으며, 접힌 `추가 기능` 안의 Excel controls는 실제 사용자 interaction 순서로 펼친 뒤 확인했다. Owned 임시 PostgreSQL은 종료·정리했다.
- exact head `981aafe3c8f05477b8f162e06865dac6043e594f`의 네 번째 CI `34200870327`은 Backend `585/585`, Frontend, Full-stack `64/64`, Workflow Validation과 CI Gate가 전부 PASS했다. 이후 운영 repair self-review에서 기존 ordinary stale managed role/head 보정이 backfill에 빠진 것을 발견해 위 단일 3-DB Fact로 보정했으며, 이 후속 변경은 새 exact head의 원격 CI 한 번으로 다시 검증한다.

## 현재 상태와 다음 Gate

- `LOCAL_IMPLEMENTED_TARGETED_VALIDATION_PASS_AWAITING_FIXTURE_COMMIT_PR_CI`
- 최신 `origin/main`은 구현 기준선 `08c5366ff6ccfe34d4945b974e25c8ef93ee1121`과 일치한다.
- Local commit 뒤 non-force push와 새 PR을 만들고 required CI가 전부 통과한 exact head만 승인된 squash merge·Azure migration/repair/app handover에 사용한다.
- 운영 mutation 전 privacy-safe dry-run에서 active membership인데 local readiness가 없는 수, ordinary System Administrator·부서장 후보, overall과 permission 누락 수, 진행 중 operation 수를 확인한다. 예상 밖 identity·삭제·권한 확대가 있으면 해당 repair만 중단한다.
