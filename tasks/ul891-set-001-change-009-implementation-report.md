# TASK-UL891-SET-001 Change 009 구현 보고 — 버전 없는 현재 설계와 위치 기반 반복 사양

상태: `사용자 검수 완료 / main 병합 승인`

## 목적·배경·범위

- 목적: UL891 설계를 일반 아이템처럼 현재 값 하나로 계속 수정하고, 한 세트 안의 서로 다른 위치에 같은 패널명과 치수를 반복 입력할 수 있게 한다.
- 포함: 현재 설계 저장 계약, 위치 identity, 반복 사양, 위치 추가·삭제, 활성 패널 projection, 프로젝트 생성·세트 사양 추가 입력, 제조 시작 기준, additive migration과 회귀 테스트.
- 제외: 비-UL891 설계 흐름, 제조·품질·물류의 기존 상태 전이, QR 정책, 기존 감사 이력 삭제, Persistent UAT, 5174/5081 runtime handover, 실제 provider, commit·push·PR·merge.

## 해결한 업무 문제

1. 사용자 화면에 `V1`, Draft/Published와 새 수정본 같은 내부 version 개념이 노출돼 일반적인 `수정 → 저장 → 다시 수정` 흐름을 방해했다.
2. 구성 `code`가 사용자 입력값이자 고유값으로 취급돼 `A-B-C-D-C-B-F`처럼 같은 사양이 여러 위치에 반복되는 실제 UL891 구성을 저장할 수 없었다.
3. 기존 검수 데이터에서 현재 활성 42면과 과거 취소 이력 12면이 함께 투영돼 제조 화면이 54면처럼 보였다.
4. 저장 직후 화면을 다시 읽는 과정에서 완료 안내가 사라져 사용자가 저장 성공 여부를 판단하기 어려웠다.

## 구현 결과

1. UL891 사용자 화면을 단일 `현재 설계`로 바꾸고 version·상태·내부 code를 제거했다. 저장 뒤에도 같은 화면에서 계속 수정한다.
2. 세트 구성 identity를 사양 문자열이 아닌 안정적인 위치 ID로 분리했다. 패널명·치수가 같아도 서로 다른 위치면 정상 저장된다.
3. 값 수정과 순서 변경은 기존 물리 패널 ID를 유지한다. 위치를 추가·삭제할 때만 모든 활성 세트에 대응 패널을 생성·취소하고 패널 번호는 재사용하지 않는다.
4. 이미 제조가 시작된 위치 삭제는 기존 이력 보호를 위해 거부한다.
5. 현재 화면과 제조 대상에는 활성 위치·활성 패널만 표시하고 취소 패널은 감사 이력으로 보존한다.
6. UL891 프로젝트 생성과 사양 추가는 `세트당 패널 수`를 입력받고 내부 위치 code는 시스템이 생성한다.
7. 저장 완료 안내를 workspace 상태로 유지해 재조회 뒤에도 사용자가 결과를 확인할 수 있게 했다.

## 기술적 결정과 검토한 대안

- 채택: `ul891_set_design_slots`를 추가해 위치를 영구 identity로 사용했다. 기존 component/version 테이블은 과거 데이터와 기존 API 호환을 위해 유지한다.
- 폐기: 패널명·치수 또는 사용자 code를 identity로 사용하는 방식. 반복 사양을 구분하지 못하고 값 수정이 물리 패널 교체로 오인될 수 있다.
- 채택: 활성 패널에 `design_slot_id`를 연결하고 `(세트, 위치)` 활성 유일성을 DB에서 보장한다.
- 폐기: 취소 패널을 삭제하거나 번호를 재사용하는 방식. 제조·검사·QR·출하 감사 추적이 깨진다.
- 채택: migration은 현재 Draft를 우선하고 없으면 최신 Published 구성을 현재 위치로 backfill한다. 활성 UL891 패널이 위치에 연결되지 않으면 fail-closed한다.
- 채택: 기존 version API는 호환 경계로 남기되 새 Frontend 정상 흐름에서는 호출하거나 노출하지 않는다.

## 아키텍처·영향

| 영역 | 영향 |
| --- | --- |
| DB/Migration | `0068` additive. 현재 설계 위치 테이블, 활성 패널 위치 FK·index와 backfill 추가. 기존 version·취소 패널 이력 삭제 없음 |
| Backend/API | 현재 설계 `PUT` 계약, 위치별 검증·CAS·audit·패널 생성/취소, 활성 current projection 추가 |
| Frontend/UI·UX | 단일 현재 설계 수정 화면, 위치 추가·삭제, 반복 사양 입력, 저장 완료 feedback. desktop 기존 상세 구조와 390px 1열 구조에 맞춤 |
| 권한/Workflow | 기존 설계 권한, 프로젝트 완료 차단, 제조 시작 위치 삭제 차단, audit와 물리 패널 ID 불변 유지 |
| 제조 | Published version 유무가 아니라 현재 활성 위치 설계를 제조 시작 기준으로 사용 |
| Excel/PDF/첨부 | N/A — 이 변경은 파일 입출력·첨부 계약을 변경하지 않음 |
| 비-UL891 회귀 | 기존 일반 프로젝트 생성·설계 흐름은 분기 밖에 유지 |

## 주요 변경 파일

- `database/migrations/0068_ul891_current_design_and_plan_defaults.sql`: 위치 identity와 활성 패널 연결/backfill.
- `backend/src/Emi.Qms.Api/Ul891Sets/Ul891SetStore.cs`: 현재 설계 저장, 위치 추가·삭제와 활성 projection.
- `backend/src/Emi.Qms.Api/Ul891Sets/Ul891SetContracts.cs`, `Ul891SetEndpointExtensions.cs`: 현재 설계 API 계약과 route.
- `backend/src/Emi.Qms.Api/Projects/ProjectInputNormalizer.cs`: 사용자 code 대신 패널 수 기반 내부 위치 생성.
- `backend/src/Emi.Qms.Api/Manufacturing/ManufacturingStore.cs`: 현재 활성 설계 기준 제조 gate.
- `frontend/src/Ul891SetWorkspace.tsx`, `ul891Sets.ts`, `api.ts`, `projects.ts`, `App.tsx`: version/code 없는 조회·수정·생성 흐름.
- `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs`, `Ul891SetApiTests.cs`: migration·반복 사양·identity·활성 수 회귀.
- `frontend/tests/Ul891SetWorkspace.test.tsx`, `App.test.tsx`와 `frontend/e2e/full-stack/ul891-user-corrections.full-stack.spec.ts`: 단위·실제 브라우저 회귀.

## 시행착오 및 폐기한 접근

1. 최초 Full-Stack 동선이 프로젝트 상세 URL 직접 진입을 가정했으나 실제 앱은 프로젝트 목록에서 선택하는 구조였다. 실제 페이지 구조에 맞춰 목록 검색·선택 동선으로 검증을 교정했다.
2. 저장 API는 성공했지만 component 재조회로 완료 안내가 사라졌다. 일시적인 내부 message가 아니라 workspace 수준의 완료 feedback으로 수정했다.
3. version 테이블을 즉시 제거하는 방안은 기존 데이터·호환 API·감사 이력을 훼손하므로 사용하지 않았다. 사용자 정상 흐름만 현재 설계 계약으로 전환했다.

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `UL891-C009-F01` | P1 | RESOLVED | version/code 중심 흐름이 같은 사양 반복과 계속 수정을 막았다. | 위치 ID 기반 현재 설계 API와 단일 수정 UI로 전환했다. |
| `UL891-C009-F02` | P1 | RESOLVED | 활성 42면에 취소 이력 12면이 현재 패널처럼 합쳐져 54면으로 표시됐다. | 현재 projection은 활성 위치·활성 패널만 사용하고 취소 12면은 감사 이력으로 보존한다. |
| `UL891-C009-F03` | P2 | RESOLVED | 저장 후 재조회 때 완료 안내가 사라졌다. | workspace 상태에 완료 feedback을 유지하고 E2E로 확인했다. |

Open P0/P1/P2: `0/0/0`.

## 자동 검증

| 검증 | 결과 |
| --- | --- |
| .NET Release build | PASS — warning/error `0/0` |
| Backend 전체 격리 PostgreSQL 회귀 | PASS — `482/482` |
| Backend UL891 안전 불변조건 집중 회귀 | PASS — `1/1`, 권한·stale CAS·착수 위치 삭제 차단 포함 |
| Migration 0068 실제 backfill | PASS — 활성 42면 유지, 취소 이력 12면 current projection 제외 |
| Frontend 전체 unit | PASS — 22 files, `145/145` |
| Frontend typecheck | PASS |
| Frontend lint | PASS — error 0, 기존 `src/main.tsx` Fast Refresh warning 1 |
| Frontend production build | PASS — 기존 500kB 초과 chunk warning 유지 |
| 격리 Full-Stack Chromium | PASS — `1/1`, 반복 사양 저장·재조회·desktop current design·390px 회귀 |
| Git whitespace 검사 | PASS — `git diff --check` |

## 개인정보·secret 검토

- 자동 검증은 격리 PostgreSQL과 명백한 개발 역할 계정을 사용했고 종료 시 DB/container/network를 제거했다.
- 문서에는 실제 고객·프로젝트·사용자 식별자, token, secret, 실제 tenant/provider 값을 기록하지 않았다.
- 실제 Persistent UAT 데이터에는 migration이나 저장을 수행하지 않았다.

## SOP — 적용·복구

1. 배포 승인을 받은 경우 Backend/Frontend보다 먼저 additive migration `0068`을 적용한다.
2. migration 뒤 UL891 프로젝트별 활성 패널 수와 현재 설계 위치 연결 누락이 0인지 확인한다.
3. 대표 검수 프로젝트는 현재 활성 42면만 표시되고 취소 12면은 current API에 포함되지 않는지 확인한다.
4. migration은 기존 이력을 삭제하지 않으므로 운영 적용 후 문제는 테이블을 강제 제거하지 않고 forward-fix migration으로 보정한다.
5. 아직 운영 적용 전이면 코드와 `0068` 적용을 보류해 기존 runtime을 그대로 유지한다.

## User manual — 사용자 사용 방법

1. 프로젝트 상세 `설계` 탭에서 `수정`을 누른다.
2. 세트 사양명과 위치별 패널명·치수를 입력한다. 같은 패널명과 치수를 여러 위치에 반복해도 된다.
3. 필요할 때 `패널 위치 추가` 또는 해당 위치의 `삭제`를 사용한다. 제조가 시작된 위치는 삭제할 수 없다.
4. `저장`을 누른 뒤 완료 안내를 확인한다.
5. 이후 다시 `수정`을 눌러 같은 현재 설계를 계속 변경한다. version이나 code를 선택할 필요가 없다.

## 사용자 검수 결과와 남은 항목

- 자동 검증과 desktop·390px 시각 검토는 완료했다.
- 2026-08-04 사용자가 누적 수정 화면을 확인하고 main 병합을 승인했다.
- Persistent UAT migration, 기존 5174/5081 runtime, 실제 provider는 승인 범위 밖이라 미실행이다.
- commit·push·PR·merge는 사용자 승인을 받았으며 게시 절차에서 실행한다.

## 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 작성됨 | 본 문서 `SOP — 적용·복구` |
| User manual | 작성됨 | 본 문서 `User manual — 사용자 사용 방법` |
| Roadmap update | 갱신됨 | `docs/00-product-roadmap.md` |
| User validation checklist | 사용자 검수 완료 / main 병합 승인 | `tasks/ul891-set-001-change-009-user-validation-checklist.md` |
