# TASK-010A Change 003 구현 보고서 — 선택형 키팅과 생산관리 제조 투입 요청

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `895de8d8666bc588c634ac8bdcb9612f26326335`
- implementationStatus: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- instructionChainRead: `true`
- fableInvocationCount: `0` — 사용자가 Codex 계획을 명시했고, 기존 TASK-010A/011A 정책 보정으로 분류했다.
- userValidationCompleted: `false` — 마지막 일괄 검수 대기
- mainMergeApprovalCount: `0/3`
- persistentUatApplied: `false`
- actualProviderCallCount: `0`
- localCommitCreated: `false`

## 1. 사용자 결정별 완료 결과

1. **키팅을 제조 시작 필수조건에서 선택조건으로 변경**
   - `KittingCompleted` workflow stage를 optional로 전환했다.
   - 키팅 완료 전·후·미실시 상태 모두 생산관리의 제조 투입 요청과 제조 시작을 허용한다.
   - 키팅 완료 알림은 completion·참고 알림만 남기고 제조 내 업무를 만들지 않는다.
2. **제조 시작 알림의 기준을 생산관리의 명시적인 투입 요청으로 변경**
   - 생산관리 프로젝트 목록에 패널별 `제조 투입 요청` 도구를 추가했다.
   - 패널정보가 완료된 활성 패널만 선택할 수 있고, 전체선택과 선택 요청을 지원한다.
   - 요청 transaction에서 제조 정·부 담당자의 패널 내 업무와 인앱 알림을 생성한다.
3. **현장 판단에 필요한 준비 정보 제공**
   - 요청 화면과 제조 화면에 `키팅 완료/미보고`와 자재 입고 수량을 함께 표시한다.
   - 정보는 경고·참고로만 사용하고 요청·작업 시작을 차단하지 않는다.
4. **중복과 기존 데이터 정리**
   - client operation ID와 정렬된 패널 집합으로 요청을 멱등 처리한다.
   - 동일 operation·동일 payload는 성공 replay, 다른 payload 또는 이미 요청된 패널 혼합은 전체 conflict다.
   - 기존 열린 프로젝트 단위 키팅 업무를 migration에서 취소하고 관련 읽지 않은 알림을 정리한다.

## 2. Root Finding과 해소

| Finding | 심각도 | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `KITTING-REQUIRED-BEFORE-MANUFACTURING` | P1 | `RESOLVED` | 기존 `키팅 완료 → 제조 업무 생성` 결합이 패널별 선행 제조와 비키팅 현장 흐름을 막았다. | stage optional 전환, 제조 queue/start를 투입 work item 기준으로 변경했다. |
| `MANUFACTURING-RELEASE-OWNER-MISSING` | P1 | `RESOLVED` | 실제 투입 시점을 결정하는 생산관리 action이 없었다. | 생산관리 전용 후보 조회·패널 선택·투입 요청 API/UI를 추가했다. |
| `KITTING-CREATED-DUPLICATE-WORK` | P1 | `RESOLVED` | 키팅 완료가 제조 업무 생성 책임까지 가져 순서 변경 시 중복 위험이 있었다. | 키팅은 참고 알림만, 제조 투입 요청만 업무를 생성하도록 책임을 분리했다. |
| `LEGACY-WORKFLOW-AUTO-HANDOFF` | P1 | `RESOLVED` | 공통 workflow 전이표에 입고 확정→키팅 업무와 키팅→제조 업무 자동 연결이 남아 있었다. | 두 자동 연결을 제거하고 생산관리 투입 요청만 제조 업무를 만들도록 단일화했다. |
| `RELEASE-RETRY-DUPLICATION` | P1 | `RESOLVED` | 네트워크 재시도나 이중 action에 대한 제조 업무 중복 방어가 필요했다. | operation replay·payload conflict·project lock·기존 panel work conflict를 적용했다. |
| `STALE-KITTING-WORK` | P2 | `RESOLVED` | 기존 자동 생성된 키팅 업무가 새 선택형 정책과 충돌한다. | 열린 업무 취소와 연결 알림 읽음 처리를 migration에 포함했다. |
| `MANUFACTURING-ASSIGNEE-SCOPE` | P2 | `RESOLVED` | 전역 역할 fallback은 프로젝트 담당자 계약을 약화할 수 있었다. | 해당 프로젝트에 지정된 제조 정·부 담당자만 수신하도록 제한했다. |
| `PERMANENT-PURGE-RELEASE-FK` | P2 | `RESOLVED` | 새 operation 원장이 프로젝트 영구 삭제를 막을 수 있었다. | 기존 영구 삭제 transaction에 release operation 삭제를 추가했다. |

Open P0/P1/P2는 `0/0/0`이다.

## 3. 주요 구현과 불변조건

- `0051_optional_kitting_manufacturing_release.sql`에 optional stage 전환, 기존 업무 정리와 `panel_manufacturing_release_operations` 원장을 추가했다.
- 생산관리 권한 `ProductionPlanUpdate`만 투입 요청을 실행한다. 조회는 기존 project read scope를 따른다.
- 공통 workflow 전이표의 `입고 확정 → 키팅 업무`, `키팅 완료 → 제조 업무` 연결을 제거했다.
- 패널정보가 완료되지 않았거나 취소된 패널, 접근할 수 없는 프로젝트, 제조 담당자 미지정 프로젝트는 서버에서 거부한다.
- 한 요청에 이미 투입된 패널이 하나라도 섞이면 일부 성공 없이 전체를 거부한다.
- 기존 제조 work item idempotency key는 과거 데이터와 deep link 호환을 위해 유지하고 사용자 표시만 `제조 투입 요청` 의미로 바꿨다.
- 제조 실행의 4단계·중단 Pending·재개·LQC 인계 계약은 변경하지 않았다.
- 계획일 기반 자동 요청, Teams·메일, BOM·패널별 자재 allocation과 키팅 취소·정정은 범위에서 제외했다.

## 4. 주요 변경 파일

- Backend: `ManufacturingContracts.cs`, `ManufacturingEndpointExtensions.cs`, `ManufacturingStore.cs`, `PanelKittingStore.cs`, `MaterialsStore.cs`, `ProjectStore.cs`
- Migration: `database/migrations/0051_optional_kitting_manufacturing_release.sql`
- Backend test: `ProcurementApiTests.cs`, `PostgreSqlMigrationTests.cs`, `ProjectRegistrationApiTests.cs`
- Frontend: `App.tsx`, `api.ts`, `manufacturing.ts`, `PanelKittingPage.tsx`, `ManufacturingPage.tsx`, `styles.css`
- Frontend test·visual: `App.test.tsx`, `PanelKittingPage.test.tsx`, `ManufacturingPage.test.tsx`, `panel-kitting-smoke.spec.ts`
- Governance: Change 003 계약, 이 보고서, Product Roadmap, 실험 완료 원장, 사용자 검수 checklist

`MaterialsStore.cs`, `App.tsx`, `styles.css`와 일부 공통 test에는 이 Task 시작 전 존재한 TASK-WORKFLOW-CONTINUITY-001 Change 005 미커밋 변경도 함께 있다. 이 보고서는 Change 003의 제조 투입·키팅 관련 diff만 완료 범위로 주장한다.

## 5. 자동 검증 결과

- Backend Debug build: 경고 `0`, 오류 `0`
- Backend test project build: 통과
- Backend Change 003 targeted: `6/6` 통과 — 키팅 전·후·미실시, 제조 시작, 정·부 알림, 중복 방지 포함
- Backend migration targeted: `4/4` 통과
- Backend 전체 isolated PostgreSQL: `416/416` 통과
- Frontend unit 전체: `116/116` 통과
- Frontend typecheck: 통과
- Frontend lint: error `0`, 기존 `main.tsx` Fast Refresh warning `1`
- Frontend production build: 통과, 기존 large chunk warning만 존재
- Mock visual Playwright: `2/2` 통과
- 실제 Full-Stack Playwright: `2/2` 통과 — 생산관리 UI 투입 요청→키팅 미보고 제조 시작·Pending·재개·완료, 선택형 키팅 업무 0건
- `git diff --check`: 통과

Backend와 Full-Stack 검증은 실행별 전용 PostgreSQL container·network·tmpfs와 합성 data에서 수행하고 종료 뒤 DB·container·network를 모두 삭제했다. Persistent UAT와 실제 provider는 사용하지 않았다.

## 6. 화면 검증과 증빙

- [생산관리 제조 투입 요청 Desktop](010a-change-003-screenshots/01-manufacturing-release-desktop-1440.jpg)
- [선택 요청 완료 Desktop](010a-change-003-screenshots/02-manufacturing-release-success-desktop-1440.jpg)
- [생산관리 제조 투입 요청 Mobile 390](010a-change-003-screenshots/03-manufacturing-release-mobile-390.jpg)

Desktop은 프로젝트 확장 영역 최상단에서 패널 선택, 키팅·입고 참고 정보와 요청 상태를 한 화면에 표시한다. Mobile은 PC 표 축소가 아니라 패널별 카드와 단일 선택 action으로 재구성했으며 page-level horizontal overflow가 없다.

## 7. SOP — 부서별 사용 절차

1. 자재 담당자는 현장에서 패널별 키팅을 실제로 완료한 경우에만 `자재 → 키팅 완료 알림`을 등록한다. 등록하지 않아도 제조 흐름은 막히지 않는다.
2. 생산관리 담당자는 생산관리 프로젝트를 펼치고 `제조 투입 요청`에서 키팅·자재 입고 현황을 참고한다.
3. 실제 투입할 패널만 선택하고 `선택 N면 제조 투입 요청`을 누른다.
4. 제조 정·부 담당자는 인앱 알림 또는 내 업무에서 요청된 패널을 열고 `제조 시작`한다.
5. 키팅을 나중에 완료한 경우 자재 담당자는 알림만 추가한다. 기존 제조 업무는 다시 생성되지 않는다.

## 8. User manual — 화면과 오류 복구

- `키팅 미보고`는 작업 불가가 아니라 아직 키팅 완료 알림이 없다는 참고 표시다.
- `자재 입고 n/m` 역시 투입 판단 자료이며 요청 버튼을 잠그지 않는다.
- `패널정보 대기` 패널은 설계의 패널명·크기 정보가 완료된 뒤 선택할 수 있다.
- `요청 완료` 패널은 다시 선택할 수 없다. 서버 응답이 늦어 재시도하더라도 동일 요청은 중복 생성되지 않는다.
- 제조 담당자가 업무를 찾지 못하면 생산관리에서 해당 패널이 `요청 완료`인지와 프로젝트의 제조 정·부 담당자 지정을 먼저 확인한다.

## 9. 사용자 검수 체크리스트

상태: `자동 검증 완료 / 사용자 검수 대기 — 마지막 일괄 검수`

- [ ] 키팅을 하지 않은 패널을 생산관리에서 요청할 수 있는지 확인
- [ ] 키팅 완료 후 요청, 요청 후 키팅 완료의 두 순서 모두 제조 업무가 한 건인지 확인
- [ ] 제조 정·부 담당자 양쪽의 인앱 알림과 내 업무에 요청 패널이 나타나는지 확인
- [ ] 생산관리 외 사용자는 요청 도구가 조회 전용인지 확인
- [ ] 입고 미완료 정보가 보이되 요청과 제조 시작은 가능한지 확인
- [ ] 동일 선택을 재시도해 업무·알림이 중복되지 않는지 확인
- [ ] Mobile 390px에서 패널 카드·선택·요청 action이 한 화면 흐름으로 사용 가능한지 확인

## 10. 잔여 위험, 게시 경계와 Rollback

- 생산 예정일 기반 자동 제조 요청·반복 알림은 의도적으로 구현하지 않았다. 실제 투입 판단은 생산관리의 명시 action으로 남긴다.
- 자재 입고 수량은 프로젝트 구매품목 전체의 요약이며 패널별 BOM allocation은 제공하지 않는다.
- 42981 frontend와 41164 backend 같은 기존 고정 runtime은 이 변경의 source/migration으로 재시작하지 않았다. 화면 증빙은 격리 mock runtime, server 계약은 격리 test host에서 검증했다.
- local commit, push, PR, merge는 수행하지 않았다. 대표 repo·GitHub `main`, Persistent UAT, 실제 provider는 미변경이며 main merge 승인은 `0/3`이다.
- migration 적용 뒤 rollback은 destructive down migration 대신 application forward-fix를 사용한다. 기존 completion·감사 이력은 삭제하지 않는다.

| 종료 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 |
| SOP | 완료 | `7. SOP` |
| User manual | 완료 | `8. User manual` |
| Roadmap update | 완료 | `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md` |
| User validation checklist | 작성·자동 검증 완료, 사용자 검수 대기 | `tasks/010a-user-validation-checklist.md` |
