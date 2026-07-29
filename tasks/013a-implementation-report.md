# TASK-013A 물류 포장·출발·납품 완료 Implementation Report

## 1. 상태와 기준선

| 항목 | 결과 |
| --- | --- |
| Task | `TASK-013A` |
| Task 유형 | `APPROVED_FEATURE_IMPLEMENTATION` |
| Branch | `experiment/task-013a-logistics` |
| 구현 기준 | [Fable 2차 기획](../docs/20-logistics-plan.md) |
| 자동 검증 | 완료 |
| 사용자 검수 | `사용자 검수 대기` |
| Local commit | 완료 — 이 보고서를 포함한 local experiment commit |
| Push / PR / Merge | 미승인·미실행 |
| `main` merge 승인 | `0/3` |
| Persistent UAT / 실제 provider | 미승인·미변경 |

이 문서는 코드 구현과 isolated 자동 검증 결과를 기록한다. live UAT 적용·사용자 직접 검수·대표 저장소 반영을 완료로 표현하지 않는다.

## 2. 목적, 배경과 범위

품질 완료 패널을 현장 물류 담당자가 모바일에서 포장 단위로 묶고, 단계별 필수 증빙을 등록해 포장 → 출발 → 납품을 원자적으로 확정한 뒤 영업 정산 업무로 인계할 수 있게 했다.

포함 범위는 다음과 같다.

- 같은 프로젝트 패널의 flat Packing Unit 구성과 프로젝트별 순번
- 포장사진, 상차사진, 출발일, 거래명세서 서명본 등록
- 포장·출발·납품 draft, evidence, finalize, cancel, replay/version 계약
- `logistics.ship`·project scope·프로젝트 물류 담당 또는 선택 대상 전체 current-work 담당 권한
- open Pending, 단계 선행조건, 다른 프로젝트 혼합, generic 내 업무 완료 우회 차단
- 모든 active panel 기준 project stage event와 영업 정산 skeleton exactly-once 인계
- `/logistics` 모바일 우선 stage → 대상 → 증빙 → 확정 화면과 desktop queue/detail composition
- 패널·프로젝트 취소 및 승인된 permanent purge와 물류 draft/보존 데이터의 정합

제외 범위는 외부 운송 연동, GPS, 전자서명 생성, 부분 납품, 반품·정정, 영업 정산 상세, object storage 전환, 실제 provider 발송, Persistent UAT 적용, 대표 저장소·`main` 반영이다.

## 3. Fable 5 기획과 Claude 사용량

Fable은 read-only runner로만 실행했고 1차 원문과 2차 원문은 byte-for-byte artifact로 보존했다. 2차 기획의 `openBlockingDecisionCount`는 `0`이다.

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable | 비고 |
| --- | --- | --- | --- | --- |
| 1차 기획 직전 | 당시 정책상 미측정 | 25% 사용 / 75% 잔여 | 50% 사용 / 50% 잔여 | 정상 측정 |
| 1차 기획 직후 | 측정 불가 | 측정 불가 | 측정 불가 | TUI timeout `exit 23` 2회, 추정하지 않음 |
| 2차 기획 최초 시도 직전 | 당시 reporter에 5시간 projection 없음 | 26% 사용 / 74% 잔여 | 52% 사용 / 48% 잔여 | 최초 호출은 account session limit로 fail-closed |
| 2차 기획 재시도 직전 | 0% 사용 / 100% 잔여 / 05:20 KST 초기화 | 27% 사용 / 73% 잔여 | 54% 사용 / 46% 잔여 | 사용자 확인 후 재시도 |
| 2차 기획 직후 | 0% 사용 / 100% 잔여 / 05:20 KST 초기화 | 28% 사용 / 72% 잔여 | 56% 사용 / 44% 잔여 | 2차 기획 성공 |
| 구현 종료 최신 조회 | 0% 사용 / 100% 잔여 / 12:10 KST 초기화 | 28% 사용 / 72% 잔여 / 07-18 08:00 KST 초기화 | 56% 사용 / 44% 잔여 / 07-18 08:00 KST 초기화 | reporter의 시간 문구 변형 보정 후 측정 |

2차 기획 재시도는 `RESUMED_ARTIFACT_PREFLIGHT`, `baselineReused=true`, `driftStatus=UNCHANGED`로 성공했고 model 211초, stdout 25,370 bytes, stderr 0이었다. Fable 사용량·실행 상세의 canonical 기록은 [Change 001](013a-change-001.md)이다.

## 4. 아키텍처와 업무 계약

### Database와 migration

additive migration `0036_logistics_execution.sql`이 포장 단위·패널 membership, 출발/납품 batch·unit membership, 증빙, 납품 결과, operation receipt를 추가한다. active membership partial unique index, 단계별 unit unique, version, row lock, operation fingerprint/replay와 finalized owner·mapping·evidence immutability trigger를 함께 사용한다.

기존 migration은 수정하지 않았다. migration 적용 후 물류 데이터가 생성된 환경에서는 과거 migration을 되돌리지 않고 신규 forward-fix migration으로 수정해야 한다. 이번 실험에서는 fresh isolated DB와 기존 migration 전체 apply 경로만 검증했으며 Persistent UAT ledger는 읽거나 변경하지 않았다.

### Backend, API와 권한

`/api/logistics`는 queue, packing unit, departure/delivery batch, evidence upload/delete/download, finalize, cancel endpoint를 제공한다. 모든 mutation은 endpoint의 `logistics.ship` 정책과 store transaction 내부의 project scope·대상 전체 담당 권한을 모두 통과해야 한다. 일부 패널만 담당한 사용자의 다중 대상 처리는 전체 거부된다.

포장·출발·납품의 generic `/api/my-work/{id}/start|complete|cancel` 전이는 `409`로 차단하고 domain finalize만 record, evidence, 현재 업무 완료, 다음 업무, coarse panel stage, project event를 함께 변경한다. 다음 물류/영업 담당자를 해석하지 못하면 현재 transaction 전체를 rollback한다.

같은 operation ID와 같은 fingerprint는 기존 응답을 replay하고 다른 fingerprint는 충돌한다. 동시 membership 경쟁·순번 경쟁·stale version은 row lock·unique constraint와 안정적인 `409` 응답으로 수렴한다.

### Workflow

```text
PackingCompleted 업무
  → 포장 단위 + 포장사진 확정
  → panel coarse PackingCompleted + DepartureProcessed 업무
  → 출발 묶음 + 상차사진 + 출발일 확정
  → DeliveryCompleted 업무 (coarse stage 유지)
  → 납품 묶음 + 서명본 확정
  → panel coarse ShipmentCompleted
  → 모든 active panel 납품 시 DeliveryCompleted event + SalesSettlementCompleted 업무 1건
```

프로젝트와 패널의 열린 Pending은 확정을 막는다. 패널 감소·프로젝트 취소 시 관련 draft membership과 업무를 terminal 상태로 만들되 finalized 이력은 보존한다. 승인된 project purge에서는 purge flag 아래 FK 역순으로 물류 데이터를 삭제한다.

### Frontend와 UI·UX

`/logistics`는 포장·출발·납품 stage switch, 대상 선택, draft 생성, 증빙 등록, 최종 확정을 한 workspace에 배치한다. 모바일은 desktop을 축소하지 않고 390px에서 한 열 행동 흐름으로 재구성하며 작은 핵심 카드, 원·타원·각진/둥근 사각형을 함께 사용한다. 공통 메뉴는 기존 좌상단 숨김 메뉴를 유지하고 하단 고정 메뉴를 추가하지 않았다.

loading, empty, error, success, read-only, Pending/권한 차단, 중복 submit 차단, error focus와 `aria-live` feedback을 구분한다. deep link는 `stage`, `project`, `panel`, `unit` query를 해석한다.

### Excel, PDF와 첨부파일 영향

- Excel export/import 변경: `N/A` — 물류 조회 export는 이번 Task 범위가 아니다.
- PDF 생성 변경: `N/A` — 서명 PDF를 생성하지 않고 업로드된 PDF magic byte만 검증한다.
- 첨부파일: 사진 JPEG/PNG는 파일당 최대 5MB, 서명본 JPEG/PNG/PDF는 파일당 최대 10MB이며 record별 개수·hash·magic byte·scope·`private, no-store` download를 강제한다. 실험 MVP 저장소는 PostgreSQL `bytea`다.

## 5. 실제 변경 파일

| 영역 | 파일과 역할 |
| --- | --- |
| 정책·기획 | `AGENTS.md`, `scripts/report-claude-usage.sh`, `tasks/013a-interview.md`, `tasks/013a-planning.md`, `tasks/013a-review.md`, `tasks/013a-change-001.md`, `docs/20-logistics-plan.md` — experiment 2-pass 계약, Fable 원문/review, 5시간 usage projection |
| Migration | `database/migrations/0036_logistics_execution.sql` — 물류 schema, constraint, trigger, operation receipt |
| Backend 신규 | `backend/src/Emi.Qms.Api/Logistics/LogisticsContracts.cs`, `LogisticsEndpointExtensions.cs`, `LogisticsStore.cs` — API 계약과 transactional domain flow |
| Backend 연동 | `Authorization/AuthorizationServiceCollectionExtensions.cs`, `Authorization/QmsPolicies.cs`, `Program.cs`, `Projects/ProjectStore.cs`, `Workflow/WorkflowStore.cs` — 정책 등록, endpoint 구성, cancel/purge, generic 전이 차단과 deep link |
| Backend 검증 | `PostgreSqlMigrationTests.cs`, `ProjectRegistrationApiTests.cs` — migration schema, end-to-end domain, replay, content sniff, 권한 교집합, concurrency |
| Frontend 신규 | `frontend/src/logistics.ts`, `frontend/src/LogisticsPage.tsx`, `frontend/tests/LogisticsPage.test.tsx` — type, adaptive workspace와 unit tests |
| Frontend 연동 | `frontend/src/App.tsx`, `frontend/src/api.ts`, `frontend/src/styles.css` — route/menu/deep link, API client, mobile/desktop visual system |
| Full-Stack E2E | `frontend/e2e/full-stack/logistics-execution.full-stack.spec.ts` — 실제 HTTP/browser와 isolated PostgreSQL의 포장→출발→납품→영업 인계 |
| 종료 산출물 | `docs/00-product-roadmap.md`, `tasks/013a-implementation-report.md` — 실험 상태, SOP·manual·checklist·검증 원장 |

## 6. 실행한 검증과 결과

| 검증 | 적용 | 결과 | 근거 |
| --- | --- | --- | --- |
| Backend Release 전체 | 적용 | 380/380 통과 | `dotnet test backend/Emi.Qms.sln --configuration Release --no-restore` |
| Logistics 영향 테스트 | 적용 | 4/4 통과 | 전체 흐름·draft 상세/취소·불변 trigger, concurrent duplicate membership, permission/all-target assignment, migration schema |
| Frontend lint | 적용 | 통과, 오류 0·기존 Fast Refresh 경고 1 | `npm run lint` |
| Frontend typecheck | 적용 | 통과 | `npm run typecheck` |
| Frontend unit | 적용 | 7 files, 82 tests 통과 | `npm test -- --run` |
| Frontend production build | 적용 | 통과, 기존 500kB chunk warning 유지 | `npm run build` |
| Full-Stack E2E | 적용 | 1/1 통과 | 실제 HTTP/browser, disposable PostgreSQL, 390px, exact-once 인계 |
| Browser desktop/mobile smoke | 적용 | 통과 | desktop 1440과 mobile 390, 3개 stage, expected structure, blank=false, overflow=0 |
| Shell syntax | 적용 | 통과 | `bash -n scripts/report-claude-usage.sh` |
| Diff/PII/secret/artifact | 적용 | 통과 | exact allowlist 27개 파일, 삭제·env·certificate·lockfile·생성물 없음, synthetic `.invalid` 주소 외 개인정보·secret 없음 |
| CI | 미실행 | `N/A` | push/PR 미승인인 local experiment branch |
| Persistent UAT | 미실행 | `N/A` | 명시적으로 제외·미승인, runtime/DB 보존 |
| 실제 provider | 미실행 | `N/A` | provider 연동이 없는 Task이며 실제 발송 미승인 |

첫 Full-Stack E2E 시도는 390px에서 개발 사용자 선택 control이 의도적으로 숨겨져 setup이 timeout 됐다. 사용자 선택을 desktop setup 단계에서 수행한 뒤 390px로 전환하도록 검증 코드만 수정했고, 재실행과 최종 실행 모두 통과했다. 사용자 기능의 모바일 메뉴 정책은 바꾸지 않았다.

## 7. Finding과 resolution

| ID | Severity | 상태 | 원인·영향 | 해소 위치 |
| --- | --- | --- | --- | --- |
| `013A-MULTI-TARGET-AUTHORIZATION` | P1 | `RESOLVED` | 일부 대상 담당자가 전체 묶음을 확정할 위험 | store의 모든 panel 교집합 검사 + authorization 전용 test |
| `013A-MEMBERSHIP-RACE` | P1 | `RESOLVED` | 동시 packing 생성 시 active membership/번호 경쟁과 비안정 500 | project/panel lock, partial unique, unique violation→409 + concurrent test |
| `013A-BATCH-PROJECT-BOUNDARY` | P1 | `RESOLVED` | 다른 프로젝트/이미 처리된 unit 혼합 위험 | project lock, 동일 project prerequisite, 단계별 active unit unique |
| `013A-DIRECT-WORK-BYPASS` | P1 | `RESOLVED` | generic 업무 완료가 증빙을 우회할 위험 | `WorkflowStore` 3개 물류 stage conflict + API/E2E test |
| `013A-HANDOFF-ROLLBACK` | P1 | `RESOLVED` | 다음 담당자 없이 현재 단계만 확정될 위험 | finalize transaction의 assignee 선해석과 전체 rollback |
| `013A-DRAFT-RECOVERY` | P1 | `RESOLVED` | 새로고침 시 queue에서 빠진 draft를 다시 열 수 없어 확정·취소가 막힘 | scope 적용 상세 API, `draft` URL 상태, 복구/취소 UI와 unit/E2E test |
| `013A-COARSE-STAGE-SEMANTICS` | P2 | `RESOLVED` | 출발에서 납품 완료 coarse stage를 오용할 위험 | 출발 relation 파생, 포장/납품에서만 coarse stage 전진 |
| `013A-PROJECT-EVENT-SOURCE` | P2 | `RESOLVED` | 일부 panel 처리로 project event 조기 생성 위험 | active panel finalized relation 집계 + unique event |
| `013A-EVIDENCE-BOUNDARY` | P2 | `RESOLVED` | spoof·과대 파일·scope 밖 조회·확정 후 변조 위험 | magic/size/count/hash/scope/no-store + immutability trigger/test |
| `013A-REPLAY-CONCURRENCY` | P2 | `RESOLVED` | 재시도·경쟁 중 중복 또는 500 위험 | fingerprint receipt, replay, version, row lock, stable 409/test |
| `013A-FINALIZE-CANCEL-LOCK-ORDER` | P2 | `RESOLVED` | finalize owner→project와 프로젝트 취소 project→owner 순서가 교차하면 deadlock 가능 | finalize를 active project→owner 순서로 통일하고 취소 선행 시 안정적 conflict |
| `013A-CANCEL-PURGE` | P2 | `RESOLVED` | panel/project 취소와 purge가 draft/append-only 이력과 불일치할 위험 | 관련 draft batch/unit terminal 처리, finalized 보존, guarded reverse purge |
| `013A-IMMUTABILITY-OWNER-MOVE` | P2 | `RESOLVED` | child row owner 변경 시 새 owner만 검사하면 finalized old owner에서 이탈 가능 | trigger가 UPDATE의 old/new owner를 모두 검사 + PostgreSQL test |
| `013A-E2E-HIDDEN-DEV-SELECT` | P3 | `RESOLVED` | 모바일 E2E setup에서 hidden 개발 selector를 찾지 못함 | desktop persona setup 후 390px 전환 |
| `013A-MOBILE-ACTION-PRIORITY` | P3 | `RESOLVED` | desktop 정보 복제로 핵심 행동이 밀릴 위험 | stage→대상→증빙→확정 한 열 구성과 overflow smoke |

Open P0/P1/P2/P3는 `0/0/0/0`이다.

## 8. 개인정보·secret과 artifact 검토

테스트는 고정 dev persona와 synthetic project/evidence만 사용했다. 실제 사용자·회사 계정·고객·프로젝트·알림 원문, credential, connection string, token, raw API/DB body를 tracked 문서나 검증 보고에 기록하지 않았다. 실제 UAT browser/DB는 사용하지 않았다.

desktop/mobile screenshot은 isolated mock 화면을 `/tmp`에 생성해 채팅으로만 전달하며 Git staging 대상에서 제외한다. Playwright report/test-results와 build output도 tracked/staged 대상에서 제외한다.

## 9. SOP

### 실험 환경 처리 절차

1. 물류 권한과 해당 프로젝트 scope가 있는 사용자로 `/logistics`를 연다.
2. 포장·출발·납품 중 현재 stage를 선택한다.
3. 같은 프로젝트의 처리 대상을 선택한다. Pending 또는 권한 차단 표시는 먼저 해소한다.
4. draft를 만들고 stage 필수 증빙을 등록한다. 출발은 출발일도 확인한다.
5. 화면의 최종 경고를 읽고 확정한다. 확정 후 핵심 구성과 증빙은 정상 API에서 수정할 수 없다.
6. 성공 feedback과 다음 stage queue 또는 영업 정산 인계를 확인한다.

### 장애와 복구

- `409` 경쟁/stale 응답: 화면을 새로 불러와 최신 대상·version으로 다시 시도한다. 같은 operation ID를 임의 변경해 중복 처리하지 않는다.
- Pending 차단: 연결된 Pending을 닫은 뒤 queue를 다시 조회한다.
- 담당자 부재: 프로젝트 물류/영업 정·부 담당을 먼저 지정하고 다시 확정한다. 실패 transaction은 현재 stage를 변경하지 않는다.
- 잘못 만든 draft: 확정 전 cancel endpoint로 취소한다. finalized record 정정은 이번 Task 범위가 아니므로 DB 직접 수정하지 않고 후속 forward-fix/정정 Task로 처리한다.
- migration 장애: Persistent UAT에 이번 migration을 적용하지 않았다. 향후 승인 적용 후 결함은 기존 `0036`을 수정하지 않고 다음 additive migration으로 forward-fix한다.

### 운영 적용 전 checklist

- 별도 승인된 최신 `main` 기반 branch에서 migration 번호와 schema drift 재확인
- 기존 DB와 fresh DB apply, full-set ledger, 권한 matrix, concurrency, cleanup 재검증
- object storage/보존 기간/정정 정책 결정 여부 확인
- Persistent UAT migration·runtime handover 별도 승인
- 사용자 검수와 main merge 3회 분리 승인 확인

## 10. User manual

### 포장

`01 포장`에서 같은 프로젝트 패널을 선택하고 필요하면 포장 메모를 입력한다. `포장 묶음 시작` 후 JPEG/PNG 포장사진과 설명을 등록하고 확정한다. 성공하면 해당 패널의 출발 업무가 생긴다.

### 출발

`02 출발`에서 포장 단위를 선택하고 출발일을 확인한다. `상차 확인 시작` 후 JPEG/PNG 상차사진과 설명을 등록하고 확정한다. 출발 확정은 화면의 물류 상태만 전진하며 패널의 납품 완료 단계로 미리 올리지 않는다.

### 납품

`03 납품`에서 출발 완료 포장 단위를 선택한다. `인수 확인 시작` 후 서명된 JPEG/PNG/PDF 거래명세서를 등록하고 확정한다. 프로젝트의 모든 active panel이 납품되면 영업 정산 업무가 한 건 생성된다.

### 화면 메시지

- `Pending`: 연결 이슈를 먼저 처리한다.
- `조회 전용`: 물류 담당자에게 처리를 요청한다.
- `다른 사용자가 먼저 수정`: 새로고침 후 최신 상태로 재시도한다.
- `확정 후 수정 불가`: 등록 파일과 선택 대상을 마지막으로 확인한다.

## 11. User validation checklist

상태: `사용자 검수 대기`

### 자동 검증 완료

- [x] desktop 1440에서 queue/detail composition과 3개 stage 표시
- [x] mobile 390에서 포장·출발·납품 화면 overflow 0
- [x] 좌상단 숨김 메뉴 유지, 하단 고정 메뉴 없음
- [x] 포장→출발→납품→영업 인계 isolated E2E
- [x] 잘못된 evidence magic byte 차단
- [x] generic 업무 완료 우회 차단
- [x] 동일 operation replay와 다른 fingerprint conflict
- [x] 동시 panel membership 경쟁의 1 success / 1 conflict
- [x] 선택 대상 전체 담당 권한 교집합
- [x] 새로고침 뒤 draft 상세·증빙 수·확정·취소 복구
- [x] finalized owner의 child membership 직접 변경 차단

### 사용자 직접 확인 대기

- [ ] desktop screenshot에서 대기 목록과 우측 증빙/확정 영역이 이해하기 쉬운지 확인
- [ ] mobile 포장 화면에서 대상 선택 → 증빙 → 확정 순서가 현장 작업에 맞는지 확인
- [ ] mobile 출발 화면의 날짜·상차사진 안내와 mobile 납품 화면의 서명본 안내 확인
- [ ] 글자·도형 크기, 원·타원·각진/둥근 도형의 시각 균형 확인
- [ ] 성공·오류·Pending 안내 문구와 다음 행동이 명확한지 확인

검수 증빙에는 실제 사용자·프로젝트 원문 대신 날짜, 환경, 익명 역할명과 성공/실패만 기록한다.

## 12. Rollback, known issue와 후속 경계

local experiment commit 전후 기능 폐기는 이 branch를 대표 저장소에 merge하지 않고 보존 또는 별도 승인 아래 branch 단위로 정리하면 된다. 대표 저장소와 `main`에는 이번 변경이 없으므로 현재 원본 rollback 작업은 없다.

known issue와 deferred 범위는 다음과 같다.

- 사용자 검수 미완료
- CI 미실행(push/PR 미승인)
- Persistent UAT migration·runtime 미적용
- PostgreSQL `bytea` evidence의 운영 보존/용량 정책 미확정
- 부분 납품, finalized 정정, 반품, 외부 운송, 전자서명 생성 미구현
- TASK-014A 영업 정산 상세 미구현; 이번 Task는 skeleton 한 건만 생성
- 기존 `App.tsx` 대형 파일과 production chunk 500kB 경고는 이번 Task 밖 기존 기술 부채

이 항목들은 승인된 실험 범위의 제외 또는 사용자 검수 gate이며 현재 Open P0/P1/P2 Finding은 아니다.

## 13. 5종 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | [이 문서](013a-implementation-report.md) |
| SOP | 완료 | [이 문서의 SOP](013a-implementation-report.md#9-sop) |
| User manual | 완료 | [이 문서의 User manual](013a-implementation-report.md#10-user-manual) |
| Roadmap update | 완료 | [Product Roadmap의 TASK-013A](../docs/00-product-roadmap.md#task-013a-물류-포장--출발--납품-완료) |
| User validation checklist | 작성·자동 검증 완료·사용자 검수 대기 | [이 문서의 checklist](013a-implementation-report.md#11-user-validation-checklist) |

## 14. 개발 블로그 소재

### 1. 해결한 업무 문제

품질 완료 뒤 포장사진, 출발일·상차사진, 납품 서명본이 서로 분리되어 있던 흐름을 패널과 포장 단위 기준으로 연결했다. 모바일 현장 사용자는 현재 stage에서 필요한 대상·증빙·확정만 볼 수 있고, 영업은 모든 패널 납품 뒤 정확히 한 건의 정산 업무를 받는다.

### 2. 기술적 결정과 검토한 대안

계층형 pallet/box 모델 대신 same-project flat Packing Unit을 채택해 MVP의 추적성과 구현 비용을 맞췄다. 출발을 기존 coarse panel stage에 억지로 넣지 않고 finalized relation에서 파생해 `ShipmentCompleted`의 납품 의미를 보존했다. 외부 storage 대신 bounded `bytea`로 실험을 닫되 magic byte·hash·scope·불변성 경계는 운영형으로 구현했다.

### 3. 시행착오 및 폐기한 접근

모바일 E2E에서 숨겨진 개발 사용자 selector를 직접 조작하려던 setup을 폐기하고 desktop에서 persona를 확정한 뒤 390px로 전환했다. 동시 포장 생성은 DB unique constraint만으로 무결성은 지켰지만 한 요청이 500으로 표면화되는 문제를 경쟁 테스트가 발견했고, unique violation을 안정적인 409로 변환했다. 첫 독립 검증에서는 메모리에만 있던 draft가 새로고침 뒤 고립되는 P1과 finalized child의 owner 이동 trigger 우회 P2를 찾아 상세 복구 URL과 old/new owner 불변 검사를 추가했다.

### 4. 사용자 검수 결과와 남은 항목

자동 검증과 synthetic screenshot 생성은 완료했다. 사용자 직접 검수는 대기 중이며, 운영 반영 전에는 Persistent UAT 승인, evidence storage/보존 정책, finalized 정정·부분 납품 정책, TASK-014A 상세가 별도 Task로 남는다.
