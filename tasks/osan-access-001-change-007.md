# TASK-OSAN-ACCESS-001 Change 007 — 운영 총괄 접근·사용자 식별 보정

## 시작 Gate

- `instructionChainRead=true`
- `taskType=BUGFIX`
- canonical Task/change: `TASK-OSAN-ACCESS-001 Change 007`
- purpose identity: 운영 사용자 관리에서 기존 Microsoft 365 계정 식별 정보와 총괄 지정 흐름을 복구하고, 지정된 총괄이 두 사업부에서 실제 전체 권한과 전환 대상을 갖게 한다.
- `roadmapSequenceMatch=true`: 이미 배포된 Task 2 기능의 운영 결함이며 사용자가 2026-09-08 수정부터 PR·main 병합·Azure 재배포까지 명시 승인했다.
- branch/worktree baseline: `fix/task-azure-deploy-001-osan-phase1-release` / deployment worktree / base `b405a9cb653aa56b1049a7e7595a2e232044b42d`, 시작 HEAD `dc099229ba16d048f6c67e24421b7122e32016d9`
- 적용 지침: Root, Backend, Frontend, Scripts `AGENTS.md`; Product Roadmap; Task Completion Policy; Validation Matrix; Privacy-safe Evidence; `TASK-OSAN-ACCESS-001` Change 002~006·implementation report; `TASK-AZURE-DEPLOY-001 Change 032`.
- 요청 모델: `gpt-5.6-sol` xhigh 단독. 실제 모델 관측값은 `NOT_REPORTED`.

## 운영 재현과 원인

1. 최초 3-DB backfill은 기존 Cheongju 계정의 `display_name`·`email`을 Directory로 복사하지 않아 사용자 관리가 공통 fallback을 표시했다.
2. 통합 저장 계약은 부서·기본 역할·부서장·활성만 포함하며 총괄 지정은 bootstrap 입력으로만 가능했다.
3. 총괄 세 명도 Cheongju membership만 backfill되어 selector 조건인 `총괄 && active membership 2개 이상`을 충족하지 못했다.
4. Osan local profile이 없는 총괄은 Osan `users.manage`를 가질 수 없어 같은 화면에서 Osan profile을 준비할 수도 없었다.

## Implementation Direction Brief

- 사용자 목록은 Directory의 pending identity를 보존하면서, 접근 가능한 business profile의 이름과 계정 ID를 병합한다. raw Entra object ID는 화면이나 응답에 노출하지 않는다.
- 통합 저장 요청과 durable Directory operation에 `isOverallAdministrator`를 포함한다. 같은 operation ID·payload만 멱등 재시도하며 access version 충돌을 유지한다.
- 총괄 지정 시 Cheongju·Osan 두 local profile을 먼저 commit한다. 선택된 부서의 기본 역할과 기존 특수 역할을 보존하고 `system-administrator`를 추가한다. 요청에 없는 반대 사업부는 `administration` 부서와 `system-administrator`로 fail-closed 준비한다. 그 뒤 Directory에서 두 membership과 designation을 한 번에 공개한다.
- 총괄 해제 시 UI는 현재 선택한 한 사업부만 활성으로 남겨 일반 사용자 단일 membership을 지킨다. 비활성 business profile snapshot은 보존한다.
- 복수 총괄은 허용하되 마지막 활성 총괄 해제는 Directory transaction에서 거부한다.
- 기존 backfill은 이름·계정 ID를 Directory에 채우고, 승인된 기존 총괄만 Osan local profile 선행 생성 후 두 membership을 공개하도록 멱등 확장한다.

## Exact allowlist

- `database/directory-migrations/0004_overall_administrator_access.sql`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitAccessAdministrationStore.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitAccessEndpointExtensions.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitMembershipBackfillRunner.cs`
- `backend/src/Emi.Qms.Api/Security/DatabaseOperationSecurityPolicy.cs`
- 관련 Backend 집중 테스트와 migration ledger 기대값
- `frontend/src/App.tsx`, `frontend/src/identity.ts`, `frontend/src/api.ts`, 기존 compact table CSS와 관련 집중 test/smoke
- `infrastructure/azure-pilot/workloads.bicep`, 생성 JSON·parameters, Azure release workflow/validator의 backfill Osan secret-scope 변경
- 이 change, access implementation report, Azure Change 032/report/SOP/handoff, Product Roadmap와 검수 checklist

## 불변조건과 제외

- Directory/Cheongju/Osan DB와 runtime role 경계를 유지한다. business DB 사이 fallback은 금지한다.
- 기존 Cheongju 일반 사용자 membership·부서·역할·업무/G2 데이터와 실제 provider 설정은 변경하지 않는다.
- 일반 사용자는 active membership 한 곳만 허용한다. Osan 제한 shell의 관리자 왼쪽 메뉴는 계속 숨긴다.
- 기존 compact table을 유지하며 별도 화면·메뉴·큰 행 UI를 만들지 않는다.
- business identity contract `0086`, Directory identity contract `0001`은 유지한다. Directory `0004`만 additive로 추가한다.
- 진행 mutation, dashboard, Osan worker/provider, 가짜 프로젝트·사용자는 범위 밖이다.

## 완료 조건

- 운영 목록의 기존 계정 행에 계정 ID와 이름이 표시된다.
- 한 행의 한 저장으로 부서 기본 역할·부서장·활성·총괄 여부를 반영할 수 있다.
- 두 명 이상의 총괄이 동시에 유지되며 마지막 총괄 해제는 차단된다.
- 총괄은 두 active membership과 두 local System Administrator profile을 가지며 두 사업부의 허용된 전체 조회·입력이 가능하다.
- 실제 두 membership 총괄에게 desktop/mobile 우측 상단 selector가 보이고 청주↔오산 전환된다.
- 일반 사용자 selector 미노출과 단일 membership, local-first 공개, audit correlation, 재시도·version 계약을 보존한다.

## 검증 계획

- Backend compile 및 영향 test: 목록 식별 병합, 총괄 지정/해제/마지막 보호, 복수 총괄, local failure 시 Directory 미공개, 멱등 재시도, version 충돌, 일반 사용자 dual 거절, 두 DB System Administrator 권한, 감사 correlation.
- Directory `0004` fresh와 existing `0001..0003` 적용 및 이전 image가 additive schema에서 계속 기동 가능한지 확인한다.
- Frontend targeted Vitest/typecheck와 단일 business-unit mock smoke: compact row, 계정 ID+이름, 총괄 checkbox, 단일 저장 payload, selector/일반 사용자 미노출.
- 실제 3-DB Full-Stack business-unit fact와 final PR CI 한 번. 이미 exact `b405a9c`에서 통과한 전체 suite는 로컬 반복하지 않는다.
- 배포 전 기존 총괄/일반 사용자 privacy-safe count dry-run, migration→backfill→Backend→Frontend 순서와 post-deploy 공개 UI/API/DB aggregate를 확인한다.

## 승인 경계

- 사용자의 2026-09-08 운영 결함 수정 요청은 이 exact scope의 구현·검증·local commit·push·PR·필수 CI·main merge·Azure migration/backfill/app 재배포를 승인한다.
- destructive down migration, 실제 업무 record 생성, 기존 Cheongju 데이터 변경, provider 발송과 다른 제품 범위는 승인되지 않았다.

## 구현·집중 검증 결과

- Directory 목록은 동일 provider·external subject까지 일치하는 business profile에서만 표시 이름과 계정 ID를 가져온다. UUID만 같은 다른 계정은 병합하지 않고 raw external subject도 응답하지 않는다.
- 통합 저장은 `isOverallAdministrator`를 durable operation payload·hash·version에 포함한다. 총괄 지정은 두 business profile에 `system-administrator`를 local-first로 준비한 뒤 두 membership과 designation을 Directory transaction에서 공개한다. 복수 총괄을 허용하고 마지막 활성 총괄 해제는 begin·publish 양쪽에서 차단한다.
- Backfill은 기존 Cheongju profile의 이름·계정 ID를 Directory에 멱등 동기화한다. private 입력으로 지정된 기존 Cheongju System Administrator만 Osan `administration` profile과 역할을 먼저 준비한 뒤 두 membership을 공개한다. 일반 사용자는 Cheongju membership 한 곳만 유지한다.
- 배포 정의는 기존 migration identity의 exact secret-scope 권한을 재사용하며 backfill job에 Osan migration secret/env만 추가한다. 새 vault-scope 권한은 만들지 않는다.

| 검증 | 결과 |
| --- | --- |
| Backend compile | PASS, 경고 0·오류 0 |
| Directory `0004` existing 적용 | `1/1 PASS` |
| Fresh 3-DB 통합 승인 Fact | `1/1 PASS` |
| Frontend targeted component/API | `37/37 PASS` |
| Frontend typecheck | PASS |
| 단일 mock Chromium 총괄 지정·전환 | `1/1 PASS` |
| 실제 HTTP + 격리 3-DB Full-Stack Fact | `1/1 PASS` |
| Bicep compile·Portal JSON·Azure 정적 검증 | PASS |

Fresh Fact 첫 실행은 UUID만 같은 다른 provider 계정의 표시 정보가 섞일 수 있음을 찾아 provider·external subject 일치 조건으로 보정했다. 다음 실행은 test fixture가 마지막 business System Administrator 제거 방어를 건드려, 두 번째 Entra 총괄 fixture로 복수 지정→한 명 해제→Directory 마지막 총괄 차단을 분리해 검증했다. 제품 불변조건을 약화하지 않았다.

Frontend targeted 명령의 argument 전달을 잘못해 의도하지 않은 전체 Vitest `299/299 PASS`를 1회 실행했다. 사용자 지정 정책 위반으로 기록하며 로컬 전체 suite를 다시 실행하지 않았다. 이후에는 정확한 filter를 먼저 확인하고 영향 test와 exact-head 최종 typecheck만 실행했다. 최종 전체 회귀는 이 commit이 게시된 PR의 자동 CI 한 번으로 수행한다.

## Git·배포 상태

- 제품 commit `3d337c69bb225e324fc8a2339e18f68420d0c63d`를 PR #122로 게시했고 CI run `34186728030`의 필수 check가 모두 통과했다. 승인된 squash merge의 exact main은 `08c5366ff6ccfe34d4945b974e25c8ef93ee1121`이다.
- Azure release run `34188740914`에서 Directory `0004`→기존 총괄 backfill→Backend→Frontend→public security smoke가 모두 통과했다. 기존 총괄 `3`명은 두 active membership과 두 local System Administrator profile을 가지며 ordinary dual membership은 `0`이다.
- 실제 로그인된 새 공개 세션에서 사용자 이름·계정 ID `23`행, 총괄 checkbox `3`개, 우측 상단 selector와 청주↔오산 전환, 양쪽 조회·입력 화면을 확인했다. 실제 업무 record와 사용자 권한은 변경하지 않았다.
- Application rollback은 최초 phase-1 immutable digest로 가능하다. Directory `0004`는 additive로 남기고 forward-fix하며 기존 Cheongju 데이터와 일반 사용자 권한을 보존한다.
