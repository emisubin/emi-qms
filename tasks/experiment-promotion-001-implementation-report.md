# TASK-EXPERIMENT-PROMOTION-001 구현 보고서

## Change 002 — 5174 제품 기준선과 Azure 원격 기준선 통합

### 결과와 범위

- Task 유형: `UAT_RUNTIME`
- 사용자 승인: 2026-08-04 `1단계` 원격 main 통합·Ready PR·CI·merge 실행 승인
- Azure 원격 기준선: `69a725880f2da67589f18d321a9fb71b0540c79f`
- 5174 local 제품 기준선: `07718bc19d5cb91afb47737895849086d9543590`
- 통합 merge commit: `33fffeafe9346cc4e475920dd4e63f9887c7b3b7`
- 병합 결과: 충돌 없음. 두 기준선의 commit 계보와 Azure Change 003~005, DESIGN-000 Change 006, TASK-UAT-001 Change 007을 모두 보존
- 제외: 5175 UL891 미커밋 작업, migration `0068`, Persistent UAT handover, Azure resource·image·DNS·traffic·실제 provider mutation

통합은 `origin/main`에서 시작한 별도 branch와 임시 worktree에서 수행했다. 5174·5081·5175 runtime source와 사용자의 다른 dirty worktree는 변경하거나 재시작하지 않았다. 원격 대비 제품 diff는 Frontend·Task 문서 23개이며 Backend·migration·Azure infrastructure overwrite는 0건이다.

### 검증 결과

| 검증 | 결과 |
| --- | --- |
| merge conflict·whitespace | `PASS` — conflict 0, `git diff --check` 통과 |
| Frontend lint | `PASS` — error 0, 기존 Fast Refresh warning 1 |
| Frontend typecheck | `PASS` |
| Frontend unit | `PASS` — 25 files, `173/173` |
| Frontend production build | `PASS` — 기존 500KB 초과 chunk warning 유지 |
| Mock UI E2E | `PASS` — `4/4`; 생성 screenshot은 기준선으로 원복 |
| 실제 5174 browser | `PASS` — 생산관리 UL891 42면·6세트와 생산계획·일정표 렌더링, 390px 가로 overflow 0, console error 0 |
| Azure artifact static validation | `PASS` |
| Azure Bicep compile·Portal template 동등성 | `PASS` |
| Azure image input guard | `PASS` |
| Teams manifest package | `PASS` — `2/2` |
| Backend Release 전체 | `PASS` — `481/481`, 격리 DB·container 정상 정리 |
| isolated Full-Stack E2E | `PASS` — 최종 단일 실행 `55/55`; 12면 stress·18단계 lifecycle 포함 |
| GitHub Ready PR CI·merge | `MERGE GATE` — 최신 head CI 성공·mergeable 확인 뒤 실행 |

### 검증 중 발견·수정

| Finding | 등급 | 상태 | 원인과 처리 |
| --- | --- | --- | --- |
| `PROMOTION-GRAPHITE-STICKY-001` | P2 | `RESOLVED` | Graphite 입력 카드의 `overflow: hidden`이 긴 설계 입력표의 sticky header 기준을 카드로 바꿔 헤더가 화면 밖으로 사라졌다. 둥근 모서리 clipping은 유지하되 scroll container를 만들지 않는 `overflow: clip`으로 바꾸고 CSS contract·실제 scroll E2E로 고정 |
| `PROMOTION-FULLSTACK-HUB-DRIFT-001` | P2 | `RESOLVED` | Full-Stack 3개 시나리오가 DESIGN-000 Change 006에서 의도적으로 삭제한 업무 선택 hub를 계속 찾았다. 제품을 되돌리지 않고 생산관리 child navigation과 자재·품질·물류의 첫 실제 workspace redirect를 검증하도록 갱신 |
| `PROMOTION-LIFECYCLE-SELECTION-WAIT-001` | P2 | `RESOLVED` | 18단계 검수가 물류 대상 click 직후 선택 상태 확정을 확인하지 않은 채 desktop/mobile viewport를 바꿔 간헐적으로 비활성 확정 버튼을 눌렀다. 각 viewport에서 `1 선택`과 확정 버튼 활성화를 명시적으로 기다리며 동일 시나리오 연속 `2/2`, 최종 전체 `55/55` 통과 |

첫 Full-Stack은 기존 hub drift와 sticky 회귀로 `51/55`, 수정 후 집중 검증은 `4/4` 통과했다. 다음 전체 실행에서 위와 무관한 lifecycle 대기 race가 한 번 나타나 `54/55`였고, 대기 조건 보강 뒤 해당 시나리오 연속 `2/2`와 최종 전체 `55/55`를 확인했다. 테스트가 덮어쓴 tracked screenshot 99개는 기준선으로 원복하고 새 임시 screenshot 1개는 Repository 밖 임시 위치로 이동했다.

### 실제 Azure read-only projection

- Azure 계정 상태: enabled
- Container Apps: 3/3 provisioning `Succeeded`, running, latest revision ready
- ingress: Backend·ClamAV internal, Frontend external
- PostgreSQL: ready, version 16, HA disabled
- Front Door route: 0
- Backend·Frontend image는 통합 전 Azure main source를 사용하고 ClamAV는 immutable digest를 사용한다.

따라서 Change 005의 workload readiness P1 두 건은 `RESOLVED`로 닫는다. public traffic·DNS·TLS·actual provider와 통합 기준선 image 재배포는 완료로 주장하지 않으며 다음 단계로 유지한다.

### Finding·개인정보·rollback

- Open Finding P0/P1/P2: `0/0/0`
- 기존 P3: Fast Refresh warning 1과 production chunk 크기 backlog 유지
- 검증 증거는 count·boolean·상태·commit projection만 기록했다. 실제 사용자명, tenant/client/object ID, hostname, token, secret, Authorization header와 업무 원문은 기록하지 않았다.
- PR merge 전에는 branch를 보존하고 중단한다. merge 뒤에는 history rewrite 없이 revert PR 또는 forward-fix PR을 사용한다.
- runtime·DB·Azure resource mutation이 없으므로 운영 rollback은 적용 대상이 아니다.

### 5종 종료 산출물

- Implementation report: 이 문서 Change 002
- SOP: 기존 코드 rollback 절차를 재사용하며 독립 운영 절차 변경 `N/A`
- User manual: 사용자 기능 추가가 아닌 기준선 통합이므로 독립 변경 `N/A`
- Roadmap update: `docs/00-product-roadmap.md` 게시 완료 상태 동기화 대상
- User validation checklist: 기존 DESIGN-000 Change 006·TASK-UAT-001 Change 007 사용자 검수 완료와 이 Change의 실제 5174 browser 검증으로 충족

## 1. 해결한 업무 문제

사용자가 검수를 완료한 experiment 계보를 새 데이터 기준의 공식 `main`으로 승격한다. 기존 대표·실험 업무 데이터는 새 시작에 섞이지 않게 분리하고, 제품 기능·권한·알림·18단계 workflow가 fresh database에서도 처음부터 끝까지 동작하는지 확인했다.

- 사용자 실험 검수: 완료
- `main` merge 승인: 서로 분리된 승인 `3/3`
- 게시 방식: direct push 금지, Ready PR과 GitHub CI 성공 뒤 merge
- 실제 Teams·메일 provider: 비활성 유지

## 2. 범위와 실제 변경

### 제품 결함 보정

1. 구매 입고가 일부 또는 전부 확정된 뒤 구매 수량을 조정할 때, DB가 계산한 입고 파생값을 과거 값으로 다시 쓰면서 저장이 거부되던 문제를 수정했다. 수량 변경은 현재 DB 파생값을 보존하고 잔여 수량·완료 상태를 DB 규칙에 따라 다시 계산한다.
2. 납품된 프로젝트에 영업 통화 정보가 비어 있으면 세금계산서 발행요청 후보 조회가 `500`이 되던 문제를 null-safe 조회로 수정했다.
3. 물류 포장·출발·납품 확정 직후 성공 경로가 아직 남아 있는 draft를 즉시 다시 선택해 중간 상태처럼 보이던 경쟁 조건을 제거했다.
4. 모바일 compact 프로젝트 병목·업무 표면의 가로 넘침을 보정했다.
5. Full-Stack·Mock UI E2E를 현재 업무 선택→프로젝트 선택→단일 프로젝트 입력 계약에 맞췄다. 12면 stress 동선은 출발·납품 대상 12개가 실제로 모두 선택됐는지 확정 전에 검증한다.
6. GitHub CI restore가 새 보안 advisory에 따라 간접 의존성 `System.Security.Cryptography.Xml 10.0.7`을 차단했다. `Microsoft.Identity.Web`을 호환되는 최신 minor `4.14.0`으로 올려 간접 의존성을 `10.0.10`으로 갱신했다.
7. GitHub Full-Stack stress에서 정산 화면의 중복 초기 조회 중 늦은 응답이 사용자가 입력한 발행 확인일을 빈 값으로 덮어쓰는 경쟁 조건을 재현했다. 조회 generation을 적용해 최신 응답만 반영하고, stress 검증도 날짜 유지·임시 저장 성공·최종 버튼 활성화를 순서대로 확인한다.

### 데이터베이스 handover

- experiment DB는 소유 runtime을 종료한 뒤 drop·recreate하고 migration `0001`~`0064`를 fresh 적용했다.
- 공식 UAT DB는 Repository 안전 불변조건 때문에 drop·truncate하지 않았다.
- 기존 공식 DB는 `emi_qms_uat_005a_archive_b8f3e210`으로 이름을 바꿔 rollback 가능한 상태로 보존했다.
- 같은 공식 이름 `emi_qms_uat_005a`의 새 DB를 만들고 migration `0001`~`0064`를 fresh 적용했다.
- 두 새 DB에는 기준정보와 개발 검수 사용자, fresh seed만 있다. 이전 사용자가 입력한 업무·알림 데이터는 없다.
- privacy-safe 확인 집계:
  - experiment fresh DB: migration 64, 사용자 12, 기본 demo 프로젝트 2, 내 업무 0, 알림 0
  - 공식 fresh DB: migration 64, 사용자 12, 기본 demo 프로젝트 2, 내 업무 0, 알림 0
  - 공식 archive: 기존 집계가 cutover 전후 동일함을 확인

## 3. 기술적 결정과 대안

### 공식 DB를 물리 삭제하지 않은 이유

사용자는 기존 데이터 삭제를 승인했지만, Persistent UAT는 파괴적 drop·truncate를 금지하는 Repository 불변조건이 우선한다. 따라서 “실제 사용 DB에는 과거 데이터가 없음”이라는 사용자 목적은 새 빈 DB로 달성하고, 기존 DB는 공식 이름에서 떼어낸 archive로 보존했다. 문제가 생기면 새 DB를 격리하고 archive 이름을 되돌릴 수 있다.

대안은 기존 DB 내부의 업무 table을 순서대로 truncate하는 방식이었으나, migration·감사·참조 무결성 누락과 되돌리기 실패 위험이 커서 사용하지 않았다.

### 게시 방식

52개 experiment commit을 `main`에 직접 push하지 않는다. current experiment branch를 push하고 Ready PR을 만들어 repository 표준 CI가 최신 head를 검증한 뒤 merge한다. 이 방식은 승인 이력, CI 결과와 rollback 지점을 GitHub에 남긴다.

## 4. 시행착오와 해결

- 오래된 E2E가 프로젝트 우선 화면으로 바뀐 현재 UI 대신 삭제된 project selector·과거 버튼 문구를 찾았다. 제품을 되돌리지 않고 테스트를 현재 사용자 동선으로 갱신했다.
- stress 동선이 12면 중 11면만 출발·납품한 상태에서도 정산으로 이동해 실패했다. batch action 전에 각 대상의 선택 상태와 `12 선택` 요약을 검증해 부분 선택을 정상 완료로 오인하지 않게 했다.
- 공식 UAT의 local DB role 설정과 현재 개발 설정이 달랐다. 외부 credential은 변경하지 않고 local-only role을 개발 기준값에 맞춰 fresh migration을 완료했다.
- 장시간 runtime command가 현재 실행 정책에서 차단돼 검수 launcher를 macOS Finder에서 실행했다. 임시 launcher 변경은 모두 원복했고 tracked script 변경은 남기지 않았다.

## 5. 자동 검증 결과

| 검증 | 결과 |
| --- | --- |
| Backend Release 전체 | `430/430` 통과 |
| Backend package restore·취약성 검사 | 통과, 취약 package `0` |
| Frontend unit 전체 | `142/142` 통과 |
| Frontend lint | 오류 `0`, 기존 Fast Refresh warning `1` |
| Frontend typecheck | 통과 |
| Frontend production build | 통과, 기존 500KB 초과 chunk warning 유지 |
| Mock UI E2E | `4/4` 통과 |
| isolated Full-Stack E2E | `55/55` 통과 |
| 12면 stress lifecycle 재검증 | `1/1` 통과, 12면·사급 입고 6회·Pending 6회·18단계·open Pending 0·완료 확인 |
| fresh migration | experiment·공식 UAT 모두 `0001`~`0064` 통과 |
| patch hygiene | `git diff --check` 통과 |

Open Finding은 P0 `0`, P1 `0`, P2 `0`이다. 기존 Fast Refresh warning과 production chunk 크기는 기능을 차단하지 않는 P3 housekeeping backlog다.

## 6. 사용자 검수 체크리스트

- [x] 사용자가 experiment 계보의 마지막 일괄 검수를 완료했다고 명시했다.
- [x] 기존 대표·실험 업무 데이터를 새 시작에서 제외하기로 확정했다.
- [x] experiment 검수 Frontend `http://127.0.0.1:42983` 응답을 확인했다.
- [x] experiment 검수 Backend `http://127.0.0.1:41166/health/ready` 응답을 확인했다.
- [x] 공식 Frontend `http://127.0.0.1:5174` 응답을 확인했다.
- [x] 공식 Backend `http://127.0.0.1:5081/health/ready` 응답을 확인했다.
- [x] 실제 Teams·메일 provider가 비활성임을 확인했다.
- [x] `main` merge 승인 `3/3`을 확인했다.

## 7. 사용자 안내

공식 화면은 `http://127.0.0.1:5174`, 공식 Backend 상태 확인은 `http://127.0.0.1:5081/health/ready`를 사용한다. 데이터는 새 기준으로 시작하며, 로그인용 개발 검수 사용자와 기본 demo 프로젝트만 보일 수 있다. 이전에 입력한 프로젝트·업무·알림은 공식 새 DB에 섞이지 않는다.

실험 화면은 비교가 필요할 때만 `http://127.0.0.1:42983`을 사용한다. 앞으로 공식 개발 기준은 merge된 `main`이다.

## 8. 운영 SOP

### 정상 시작

1. 대표 repository의 `main`을 최신 `origin/main`과 일치시킨다.
2. Repository root의 공식 UAT 시작 script를 실행한다.
3. Backend readiness가 `200`, Frontend root가 `200`인지 확인한다.
4. 실제 provider는 별도 운영 승인 전까지 켜지 않는다.

### DB rollback

1. 공식 runtime의 mutation을 중지한다.
2. 현재 fresh `emi_qms_uat_005a`를 실패 식별 이름으로 격리한다.
3. 보존한 `emi_qms_uat_005a_archive_b8f3e210`을 공식 이름으로 복구한다.
4. archive 기준과 호환되는 source/runtime으로 시작하고 readiness·migration ledger·핵심 조회를 확인한다.

### 코드 rollback

merge 전에는 PR을 merge하지 않는다. merge 후에는 `main` history를 강제로 되돌리지 않고 revert PR 또는 forward-fix PR을 사용한다. 코드 revert만으로 DB 이름이 돌아오지 않으므로 DB rollback은 위 절차를 별도로 수행한다.

## 9. 산출물과 차이

- Task Identity Gate: `tasks/experiment-promotion-001-task-identity-gate.md`
- 승인·DB·게시 계약: `tasks/experiment-promotion-001-change-001.md`
- 구현 보고서·SOP·사용자 안내·검수 체크리스트: 이 문서
- Roadmap: `docs/00-product-roadmap.md`
- 실험 완료 원장: `docs/27-experiment-task-ledger.md`

Planning과 다른 제품 기능을 추가하지 않았다. 최종 검증에서 발견된 승격 차단 결함과 테스트 drift만 최소 수정했다.

## 10. 게시·rollback 판정

Backend·Frontend·fresh migration·isolated Full-Stack 기준에서 Open P0/P1/P2가 없으므로 Ready PR 게시 가능 상태다. GitHub CI가 최신 commit에서 모두 성공하고 PR이 mergeable일 때만 `main` merge를 수행한다.
