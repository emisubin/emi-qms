# TASK-BACKEND-FORMAT-001 Implementation Report

## 1. 결과

Backend format baseline의 `IMPORTS` 9건을 정확한 9개 C# 파일에서 정리했다. Formatter 적용 전후 `using` 집합은 동일하며 순서만 바뀌었다. 전체 format verify는 exit 0, diagnostic 0으로 전환됐고 Backend·Frontend·Full-Stack 검증을 통과했다.

## 2. 해결한 업무 문제

기존 기능 Task가 자신이 변경한 파일만 format-clean인지 확인하면서, 과거 범위 밖 import-order drift 9건은 Repository 전체 format command를 계속 실패시켰다. 이로 인해 신규 Task에서 실제 format 회귀와 기존 debt를 매번 분리 설명해야 했다.

## 3. 목적과 범위

포함 범위:

- 기존 `IMPORTS` 9건 정리
- formatter/diff guard
- Backend·Frontend·E2E 전체 검증
- 종료 산출물과 Roadmap 갱신

제외 범위:

- 제품 동작·정책·API·UI 변경
- migration·dependency·lockfile·script·runtime 변경
- Persistent UAT 접근 또는 mutation
- Git history와 외부 Support 처리

## 4. 아키텍처와 영향

- Backend: import order만 변경
- Frontend: 변경 없음
- DB/Migration: 변경 없음
- API/권한/Workflow: 변경 없음
- UI·UX/Excel/PDF/첨부: 변경 없음
- Runtime·provider: 변경 없음
- Persistent UAT: 접근·변경 없음

컴파일 결과물의 의미와 public contract는 바뀌지 않는다.

## 5. 변경 파일

제품·test C# 파일:

- `backend/src/Emi.Qms.Api/Authorization/EntraClaimsTransformation.cs`
- `backend/src/Emi.Qms.Api/Calendar/AdminCalendarHolidayStore.cs`
- `backend/src/Emi.Qms.Api/DatabaseMigrationRunner.cs`
- `backend/src/Emi.Qms.Api/Identity/DbIdentityStore.cs`
- `backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryEndpointExtensions.cs`
- `backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryStore.cs`
- `backend/tests/Emi.Qms.Api.Tests/PanelInformationApiTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/ProcurementApiTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/QmsWebApplicationFactory.cs`

Task 문서:

- `tasks/backend-format-001-planning.md`
- `tasks/backend-format-001.md`
- `tasks/backend-format-001-implementation-report.md`
- `tasks/backend-format-001-sop.md`
- `tasks/backend-format-001-user-manual.md`
- `docs/00-product-roadmap.md`

## 6. 기술적 결정과 검토한 대안

Repository formatter를 9개 파일 allowlist에 한정했다. 수동 정렬은 오정렬 가능성이 있고 `.editorconfig` 완화는 debt를 숨기므로 사용하지 않았다.

적용 뒤 다음 guard를 통과했다.

- allowlist 밖 Backend 변경 0
- `using` 집합 불일치 0
- `using` 블록 밖 내용 불일치 0
- 비-`using` diff line 0

## 7. 실제 구현

Formatter는 각 파일의 system namespace와 project namespace 순서를 `.editorconfig` 기준으로 정렬했다. Import 추가·삭제, alias 변경, namespace·type·member·statement와 test assertion 변경은 없다.

## 8. 실행한 테스트와 결과

| 검증 | 결과 |
| --- | --- |
| Baseline format probe | exit 2, `IMPORTS=9`, file 9 |
| Formatter apply | exit 0, changed file 9 |
| Diff semantic guard | using-set mismatch 0, non-using mismatch 0 |
| Full format verify | exit 0, diagnostic 0 |
| Backend Release build | PASS, warning/error 0/0 |
| Backend 전체 tests | PASS, 361/361 |
| Frontend lint | PASS, error 0·기존 warning 1 |
| Frontend typecheck | PASS |
| Frontend unit | PASS, 62/62 |
| Frontend build | PASS, 기존 chunk-size warning 유지 |
| Mock UI smoke | PASS, 1/1 |
| Full-Stack E2E | PASS, 16/16 |
| actionlint | PASS |
| `git diff --check` | PASS |

## 9. 미실행 검증

- Persistent UAT snapshot: N/A. 실행 코드·DB·migration·runtime 변경이 없고 Persistent 환경을 이 Task에서 사용하지 않았다.
- Development·Review-safe browser smoke: N/A. Frontend와 runtime artifact를 변경하지 않았다.
- Actual provider 검증: N/A. Provider source·configuration을 변경하지 않았다.
- GitHub CI: 게시 승인이 없어 PR을 만들지 않았으므로 대기다.
- 독립 Codex 검증: 구현 session과 분리된 read-only 검증을 통과했다. 변경 파일 15개, C# import-only 9개, 문서 6개, format diagnostic·allowlist 위반·삭제·staged·secret/PII 후보는 모두 0이었다.

## 10. 시행착오 및 폐기한 접근

새 worktree에 NuGet restore 산출물이 없어 최초 `--no-restore` build가 `project.assets.json` 부재로 실패했다. Dependency 선언과 lockfile을 바꾸지 않는 표준 restore를 수행한 뒤 Release build와 361개 tests를 다시 실행해 통과했다.

수동 import 재정렬과 `.editorconfig` 완화는 사용하지 않았다.

## 11. 개인정보·secret과 artifact

실제 사용자·업무·credential을 사용하지 않았다. Full-Stack E2E는 synthetic data, 전용 PostgreSQL tmpfs와 provider-disabled 설정을 사용했고 Task-owned DB·container·network를 종료 후 제거했다. Tracked secret/PII와 test artifact가 없는지 게시 전 다시 확인한다.

## 12. Rollback

9개 C# 파일의 import-order diff와 Task 문서만 revert하면 된다. DB, migration, runtime, data와 backup rollback은 없다.

## 13. Findings와 제한

- P0/P1: 0
- 신규 P2: 0
- 해결된 P3: 기존 import-order 9건
- 기존 lint warning 1과 frontend chunk-size warning은 이번 Task 이전 baseline이며 기능 실패가 아니다.
- 기존 history P2와 Support 대기는 이 Task 범위 밖이며 상태를 변경하지 않는다.

## 14. Planning 대비 변경점

승인된 권장안 A와 정확히 일치한다. 영향 파일 9개, import-only diff, 전체 검증과 5종 종료 산출물 범위를 확장하지 않았다.

## 15. 사용자 검수 결과와 남은 항목

사용자 검수 checklist 7개는 모두 확인됐다. 독립 Codex 검증과 사용자 검수가 완료됐고 Commit·Push·PR·Merge 승인을 받았다. GitHub CI는 게시 후 최신 head에서 확인하고 성공한 경우에만 squash merge한다.

## 16. 5종 종료 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 자동·독립 검증 완료 |
| SOP | `tasks/backend-format-001-sop.md` | 작성 완료 |
| User manual | `tasks/backend-format-001-user-manual.md` | 기능 변경 N/A·검수 항목 작성 |
| Roadmap update | `docs/00-product-roadmap.md` | 사용자 검수·merge 승인 상태 반영 |
| User validation checklist | `tasks/backend-format-001.md` 7장 | 사용자 검수 완료 |
