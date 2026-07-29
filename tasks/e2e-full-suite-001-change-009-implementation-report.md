# TASK-E2E-FULL-SUITE-001 Change 009 구현 보고서

## 1. 해결한 업무 문제

고정 검수 주소의 process가 종료되면 사용자가 Codex에 다시 server 기동을 요청해야 했다. 기존 Backend와 Frontend script는 각각 Terminal에서 실행해야 하고, Docker·고정 port·readiness·중복 process 상태를 사용자가 직접 판단하기 어려웠다.

이번 변경은 macOS에서 `사용자-검수-서버-실행.command`를 더블클릭하면 기존 실험 DB를 보존한 채 Frontend와 Backend를 함께 시작하고, 준비가 끝난 경우에만 browser를 여는 직접 실행 경로를 제공한다.

## 2. 범위와 영향

### 포함

- 사용자-facing macOS `.command` launcher
- Docker Desktop·기존 PostgreSQL container·Frontend dependency preflight
- Backend 우선, Frontend 후속 시작과 health/readiness 확인
- PID file, process start fingerprint, Repository cwd, listener command와 process ancestry를 함께 확인하는 ownership gate
- strict fixed port와 unknown listener fail-closed
- private temp runtime state/log와 중복 실행 방지

### 제외

- Backend/Frontend 제품 코드, API, DB schema, migration, 권한과 workflow 변경
- 검수 DB 생성·reset 또는 data mutation
- Persistent UAT, 실제 provider, 대표 repo와 `main`
- 종료 자동화, commit, push, PR, merge

### 시스템 영향

| 영역 | 영향 |
| --- | --- |
| Backend | 제품 코드 변경 없음. 기존 validation script를 detached process로 실행 |
| Frontend | 제품 코드 변경 없음. 기존 Vite validation script를 detached process로 실행 |
| DB/Migration | 변경 없음. 기존 `emi-qms-postgres`와 experiment validation DB를 재사용 |
| API/UI·UX | 제품 UI 변경 없음. launcher Terminal 안내와 browser open만 추가 |
| 권한/Workflow | 변경 없음 |
| Excel/PDF/첨부 | 변경 없음 |
| 외부 알림 | provider와 mutation worker 기존 비활성 설정 유지 |

## 3. 기술적 결정과 검토한 대안

- 두 Terminal에서 script를 각각 실행하는 방식 대신 한 번의 더블클릭으로 순서·readiness를 관리한다.
- Terminal 수명에 server를 묶는 foreground 방식 대신 `nohup` detached process를 사용해 launcher 창을 닫아도 검수를 계속할 수 있게 했다.
- port만 보고 기존 process를 종료하는 방식은 사용하지 않았다. PID file과 시작 시각 fingerprint, Repository cwd, command, listener ancestry가 모두 일치할 때만 이 launcher가 소유한 정상 runtime으로 인정한다.
- port 충돌 시 자동으로 다른 주소를 쓰지 않는다. 고정 주소 계약을 보존하고 미소유 process를 건드리지 않은 채 실패한다.
- 검수 DB가 없을 때 자동 생성·초기화하지 않는다. 기존 검수 data 보존을 우선하고, 정확한 container가 없으면 초기 구성 필요로 중단한다.

## 4. 시행착오 및 폐기한 접근

- 제품 Repository 안에 PID와 log를 저장하는 방식은 working tree 오염과 log 추적 위험 때문에 폐기했다. macOS private temp directory를 사용한다.
- listener PID만 기록하는 방식은 `dotnet watch`와 package runner가 child listener를 만들 수 있어 폐기했다. owner PID와 child ancestry를 함께 검증한다.
- 실패 시 port listener를 일괄 종료하는 방식은 다른 사용자 process 손상 위험 때문에 폐기했다. 이번 실행에서 만든 owner와 그 descendant만 `TERM` 대상으로 제한한다.

## 5. 변경 파일과 역할

| 파일 | 역할 |
| --- | --- |
| `사용자-검수-서버-실행.command` | Finder 더블클릭 진입점과 성공·실패 pause |
| `scripts/start-experiment-validation.sh` | 통합 preflight, ownership, 시작, readiness, browser open |
| `tasks/e2e-full-suite-001-change-009.md` | 승인 범위와 Task Identity Gate |
| `tasks/e2e-full-suite-001-change-009-implementation-report.md` | 구현·검증·rollback·5종 산출물 추적 |
| `docs/27-experiment-task-ledger.md` | 고정 검수 runtime의 직접 실행 경로 |
| `docs/00-product-roadmap.md` | 기존 E2E Task Change 009와 결정 이력 |

## 6. 검증 결과

| 검증 | 결과 |
| --- | --- |
| Bash syntax | `bash -n` 4개 launcher/개별 script PASS |
| Shell 정적 분석 | `shellcheck` 4개 script warning/error 0 |
| 실제 Finder 경로 최초 실행 | `open 사용자-검수-서버-실행.command` PASS — Backend 우선·Frontend 후속 준비 |
| 통합 `.command` 실행 | 표준 입력으로 pause를 종료한 smoke PASS, 종료 code 0 |
| 중복 실행 | 기존 owned runtime으로 판정하고 재기동 없이 PASS |
| Backend live/ready | `/health/live` 200, `/health/ready` 200 |
| Frontend root/proxy ready | `/` 200, `/health/ready` 200 |
| 단일 listener·ownership | Backend 1, Frontend 1; owner session·cwd·command·ancestry 확인 PASS |
| Terminal 수명 분리 | owner process가 Terminal shell 밖으로 re-parent되고 health 200 유지 PASS |

미실행 제품 test: 제품 코드·API·DB schema를 변경하지 않는 launcher 전용 HOUSEKEEPING이므로 Backend/Frontend 전체 unit·E2E는 적용 대상이 아니다. 실제 고정 runtime의 기동·readiness·중복 실행을 직접 검증한다.

미실행 충돌 주입: 고정 port에 의도적으로 다른 process를 점유시키는 검증은 정상 검수 runtime을 중단해야 하므로 실행하지 않았다. `start_component`가 port listener를 먼저 확인하고 ownership state가 일치하지 않으면 `fail`하며, port 종료·fallback 코드는 포함하지 않는 것을 정적 검토했다.

## 7. 개인정보·secret 검토

- 새 파일에 실제 사용자·고객·프로젝트 정보, token, webhook, 인증서 private key를 기록하지 않는다.
- 기존 validation Backend script의 환경 값은 launcher 출력에 표시하지 않는다.
- runtime log와 PID/session state는 Repository 밖 mode `700/600` private temp path에 둔다.
- 완료 보고에는 raw log나 process command 전문을 포함하지 않는다.

## 8. Rollback과 복구

- 제품 code·DB·migration 변경이 없어 launcher 파일과 문서만 제거하면 source rollback이 된다.
- 실행 실패 시 이번 실행이 만든 owner PID와 Repository descendant에만 `TERM`을 보내고 다른 port process는 종료하지 않는다.
- 이미 실행된 server를 수동 종료해야 할 때는 PID/session ownership을 다시 확인한 별도 승인된 운영 절차를 사용한다.

## 9. 사용자 검수 결과와 남은 항목

- 상태: `사용자 검수 대기 — 마지막 일괄 검수`
- 사용자 확인: Finder에서 `사용자-검수-서버-실행.command` 더블클릭, browser가 `http://127.0.0.1:42983`으로 열리는지, 페이지 새로고침과 API 조회가 정상인지 확인
- 대표 repo·`main`·Persistent UAT·실제 provider 반영: 없음

## 10. Finding

- `LAUNCHER-LOCALE-DEPENDENT-SESSION-FINGERPRINT` / P2 / `RESOLVED`: Finder Terminal과 Codex shell의 locale 차이로 같은 process 시작 시각 문자열이 달라 owned runtime을 unknown listener로 오판했다. `LC_ALL=C` fingerprint를 고정하고, PID·repo marker·listener ancestry·cwd·command·health가 모두 일치하는 기존 session만 canonical fingerprint로 승격하도록 보정했다.
- `CODEX-EXEC-DETACHED-CHILD-CLEANUP` / P3 / `RESOLVED`: Codex command runner는 부모 명령 종료 시 detached child도 test sandbox에서 정리해 일반 shell 경로의 지속 실행을 증명할 수 없었다. 실제 macOS Finder→Terminal `.command` 경로로 재검증하고 listener 1/1·health 200·owner re-parenting을 확인했다. 제품 또는 launcher 결함은 아니다.
- Open P0/P1/P2: `0/0/0`

## 11. 5종 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 포함됨 | 본 문서 2·3·8절과 launcher 화면 안내 |
| User manual | 포함됨 | 본 문서 9절과 `사용자-검수-서버-실행.command` |
| Roadmap update | 작성됨 | `docs/00-product-roadmap.md` Change 009와 Decision Log |
| User validation checklist | 포함됨 / 사용자 검수 대기 | 본 문서 9절 |
