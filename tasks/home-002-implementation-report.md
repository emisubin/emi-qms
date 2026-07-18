# TASK-HOME-002 Implementation report — 개인화 홈·프로필 shell

## 1. 요약과 상태

- 목적: 참고 이미지의 간결한 고정 navigation 구조를 EMI 색감으로 재해석하고, 모든 페이지에 실제 로그인 사용자 정보와 본인 프로필 사진을 제공하며 Home을 부서 핵심 지표 중심으로 개인화한다.
- 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL` — 구현·필수 자동 검증·격리 브라우저 검증 완료, 사용자 검수는 마지막 일괄 대기
- 최종 계약: [Fable 2차 기획](../docs/28-personalized-home-profile-plan.md)
- Branch/base: `experiment/task-home-002-personalized-shell` / `1235d5e`
- 대표 repo·`main`·Persistent UAT·actual provider: 미변경
- Merge 승인: `0/3`

## 2. 해결한 업무 문제

기존 공통 shell은 로그인 사용자의 부서와 이름을 모든 페이지에서 명확히 보여주지 않았고, 개발 사용자 전환과 중복 자재 바로가기가 상단 공간을 차지했다. Home도 공통 widget 위주라 부서 사용자가 가장 먼저 확인할 지표가 약했다. 이번 구현은 actual identity와 effective work context를 분리하고, 공통 계정 표면·부서별 최대 3개 지표·고정 sidebar/mobile drawer를 한 계약으로 묶었다.

## 3. 구현 범위와 아키텍처

### DB·Migration

- Migration: `0042_user_profile_photos.sql`
- additive table 2개: 사용자당 현재 사진 1개, fixed-field append-only audit
- 사진 원문은 현재 사진 테이블에만 보관하고 audit에는 hash·크기·MIME·행동만 기록한다.
- 5MB DB constraint, self actor constraint, user purge cascade와 transaction-local audit purge scope를 적용했다.

### Backend·API·권한

- `/api/me`에 `departmentName`, actual/effective `profilePhotoVersion`을 추가했다.
- 본인 전용 `GET/PUT/DELETE /api/me/profile-photo`를 추가했다. actual claim만 사용해 테스트 사용자 전환으로 타인 사진에 접근할 수 없다.
- JPEG/PNG signature·구조·가로세로 1~8192px·5MB를 검증하고 cache/ETag를 적용했다.
- `GET /api/home/department-metrics`는 effective identity·permission·project scope로 최대 3개 allowlisted 지표만 반환한다.
- 관리·영업·설계·생산관리·구매·자재·제조·품질·물류의 실제 schema query를 구현했다.

### Frontend·UI/UX

- 모든 업무 페이지 PC 상단에 actual 사용자 사진·부서·이름을 표시하고 account popover를 추가했다.
- 프로필 사진 또는 `사진 변경`에서 업로드하고 `사진 제거`로 이니셜 avatar를 복원한다.
- 개발·검수 사용자 전환을 full-height sidebar 하단과 mobile drawer 하단으로 옮겼다.
- 우측 상단 중복 `자재` 바로가기를 제거했다.
- Home에 부서 지표 panel을 추가하고 기존 내 업무·프로젝트 병목·Pending·알림 widget의 독립 failure 계약을 유지했다.
- 모바일은 하단 고정 menu가 아니라 좌상단 drawer, 우상단 계정 sheet, 3열 compact 부서 지표와 우선 확인 block을 사용한다.
- 흰 배경에서 보이지 않던 기존 흰색 EMI logo는 브랜드 red lockup으로 보정했다.

### Excel/PDF/첨부·Workflow 영향

- Excel/PDF: `N/A` — 기존 export·PDF 계약을 변경하지 않았다.
- 첨부: 프로필 사진만 신규이며 업무 첨부 storage 계약과 분리했다.
- Workflow: 상태 전이·업무 생성·알림 delivery는 변경하지 않았다. Home은 read-only aggregate다.

## 4. 기술적 결정과 검토한 대안

- shell 계정은 actual 사용자, Home 지표는 effective 사용자로 분리해 검수 전환 중 계정 오인을 막았다.
- 사진을 범용 attachment에 넣는 대신 bounded 1-row store로 제한해 사용자당 저장량과 lifecycle을 단순화했다.
- 브라우저 UA 문자열로 모바일을 고정하지 않고 기존 adaptive layout의 viewport·input capability 판정을 재사용했다.
- 모든 PC 정보를 모바일에 축소 복제하지 않고 핵심 지표·긴급/차단·주요 widget 순으로 재배치했다.
- 지표별 다중 API 대신 부서별 aggregate endpoint 하나를 사용해 권한·scope와 최대 3개 계약을 서버에서 고정했다.

## 5. 시행착오 및 폐기한 접근

- 영업 지표 SQL의 CTE 뒤 `from visible` 누락은 새 fresh-schema 9부서 통합 테스트가 발견했다. SQL을 수정하고 같은 테스트에서 9개 부서를 모두 실행해 해결했다.
- 흰색 EMI logo를 흰 sidebar에 그대로 놓자 시각적으로 사라졌다. logo asset을 바꾸지 않고 red gradient lockup으로 대비를 확보했다.
- 첫 모바일 full-page 촬영은 viewport 전환 직후 reflow 전에 캡처돼 일부가 잘려 보였다. DOM card 폭·page scroll width를 확인한 뒤 안정된 reflow에서 재촬영했고 3개 지표가 375px 폭에 모두 들어오는 것을 확인했다.
- 첫 Full-Stack 전체 실행은 `23/38`이었다. 사진이 없는 사용자에 대한 불필요한 404 요청, 이전 모바일 `상태` sheet를 가리키던 E2E, 변경 전 Home 제목·모호한 품질 탭 selector와 UTC/KST 자정 경계 날짜를 각각 수정했다. 실패했던 15개 표적 재검증 뒤 전체 `38/38`을 통과했다.
- 실제 5081/5174를 재시작하는 접근은 대표 runtime을 건드리므로 사용하지 않았다. Task 전용 5082/5175 synthetic preview만 사용하고 종료했다.

## 6. 변경 파일과 역할

- `database/migrations/0042_user_profile_photos.sql`: profile current row·audit·constraint·purge guard
- `backend/.../Identity/ProfileImageValidator.cs`, `UserProfilePhotoStore.cs`, `IdentityEndpointExtensions.cs`: 사진 검증·저장·본인 API·identity projection
- `backend/.../Home/*`: 부서 지표 contract·store·endpoint
- `AdminScheduledDeletionService.cs`, `Program.cs`: profile lifecycle purge와 DI/routing
- `PostgreSqlMigrationTests.cs`, `ProfileImageValidatorTests.cs`: migration·사진 lifecycle·9부서 query·image validation
- `frontend/src/App.tsx`, `HomePage.tsx`, `styles.css`: 공통 shell, account UI, Home·mobile 재구성
- `frontend/src/api.ts`, `identity.ts`, `home.ts`: API와 type contract
- `frontend/tests/App.test.tsx`: 계정·sidebar·모바일 drawer·부서 Home 회귀
- `frontend/e2e/full-stack/*`: 새 Home 제목·drawer 하단 사용자 전환·계정 sheet 계약과 서울 날짜 경계 안정화

## 7. 실행한 검증과 결과

| 검증 | 결과 |
| --- | --- |
| Backend Debug/Release build | 성공, warning/error 0 |
| Profile·9부서 fresh-schema 통합 | 1/1 성공 |
| Backend 전체 | 395/395 성공 |
| Frontend lint | error 0, 기존 `main.tsx` fast-refresh warning 1 |
| Frontend typecheck/unit | 성공, 102/102 |
| Frontend build | 성공; 기존 chunk-size warning 유지 |
| Full-Stack E2E | 38/38 성공, 전용 tmpfs PostgreSQL·외부 provider disabled |
| Browser desktop/mobile | 6개 privacy-safe synthetic screenshot, actual/effective identity·계정 UI·영업/품질 전환 확인 |
| Mobile overflow | 375px client/scroll width 동일, 부서 지표 3개 grid 폭 337px |
| Runtime isolation | 5082/5175 종료, 대표 5081/5174 PID 보존 |

스크린샷용 preview는 `http://127.0.0.1:5175`/`5082`였으며 보고 시점에는 종료됐다. 대표 5174/5081은 읽기만 하고 재시작하지 않았다. Persistent UAT migration/runtime과 actual provider 검증은 승인 범위 밖이라 실행하지 않았다.

## 8. 개인정보·secret 검토

- screenshot은 합성 이름·고정 테스트 프로젝트만 사용한다.
- 실제 사용자·고객·프로젝트·email/UPN, credential, token, provider payload를 tracked 산출물에 기록하지 않았다.
- 프로필 binary와 raw API/DB body는 보고서·로그에 기록하지 않았다.
- 외부 provider 호출은 0이며 browser preview는 synthetic API만 사용했다.

## 9. Finding gate

| Finding | Severity | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `HOME_SALES_CTE_SOURCE_MISSING` | P2 | RESOLVED | 영업 집계 CTE 결과의 FROM 누락으로 실제 DB에서 42703 발생 | fresh-schema 9부서 통합 테스트로 발견·수정 |
| `WHITE_LOGO_ON_WHITE_SIDEBAR` | P2 | RESOLVED | 흰색 logo asset이 새 흰 sidebar에서 보이지 않음 | EMI red lockup 배경으로 대비 확보·재촬영 |
| `PROFILE_ACTUAL_EFFECTIVE_SCOPE` | P2 | RESOLVED | 검수 사용자 전환 중 타 사용자 사진 접근·계정 오인 위험 | actual claim 사진 scope, effective Home 지표 분리 |
| `PROFILE_BINARY_AUDIT_DUPLICATION` | P2 | RESOLVED | 감사 원장에 binary 중복 시 저장·privacy 부담 | fixed-field hash/size/MIME audit만 저장 |
| `EMPTY_PROFILE_PHOTO_404_NOISE` | P2 | RESOLVED | 사진이 없는 사용자도 binary endpoint를 호출해 브라우저 error signal 증가 | `/api/me`의 photo version이 있을 때만 binary 요청 |

Open P0/P1/P2: `0/0/0`. Risk acceptance 없음.

## 10. 사용자 검수 결과와 남은 항목

- 자동 검증·격리 브라우저 검증: 완료
- 사용자 validation checklist: 작성됨
- 사용자 직접 검수: `사용자 검수 대기 — 마지막 일괄 검수`
- 대표 repo·main 승격, Persistent UAT migration/runtime handover, push/PR/merge: 별도 승인 전 금지
- `main` merge 승인: `0/3`

## 11. Rollback·forward-fix

- 코드: 현재 experiment commit을 기준으로 revert commit을 만든다. 대표 branch에는 반영되지 않았다.
- DB: 이미 적용한 `0042`를 수정·삭제하지 않고 새 번호의 additive forward-fix를 사용한다.
- 사진 제거는 API를 사용하며 audit 원장을 임의 삭제하지 않는다.

## 12. 5종 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 |
| SOP | 완료 | [docs/29-personalized-home-profile-sop.md](../docs/29-personalized-home-profile-sop.md) |
| User manual | 완료 | [docs/30-personalized-home-profile-user-manual.md](../docs/30-personalized-home-profile-user-manual.md) |
| Roadmap update | 완료 | [docs/00-product-roadmap.md](../docs/00-product-roadmap.md) TASK-HOME-002·Decision Log |
| User validation checklist | 사용자 검수 대기 | [tasks/home-002-user-validation-checklist.md](home-002-user-validation-checklist.md) |

## 13. Fable 사용량·session

- 1차 planning 전/후: 5시간 사용 43%/43%, 주간 전체 12%/12%, Fable 24%/24%
- 2차 planning 전/후: 5시간 사용 52%/52%(잔여 48%, 23:59 KST 초기화), 주간 전체 13%/13%(잔여 87%, 7월 25일 07:59 KST 초기화), Fable 26%/26%(잔여 74%, 초기화 시각 parse 불가)
- 2차 runner: READY, resumed session, 147초, 17,869 bytes
- 구현 종료 최신 조회: 5시간 0% 사용/100% 잔여(05:50 KST 초기화), 주간 전체 14% 사용/86% 잔여, Fable 28% 사용/72% 잔여(모두 7월 25일 07:59 KST 주간 초기화)
- Fable private session: `FABLE_TASK_SESSION_CLEANED`, session·transcript 각 1개 제거
