# TASK-NOTICE-BOARD-001 Implementation report — Home 공지사항 게시판

## 1. 요약과 상태

- 목적: Home 상단·중앙은 유지하고 하단 `프로젝트 병목`만 모든 승인된 active 사용자가 작성·조회하는 공지사항으로 교체한다.
- 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL` — 구현·전체 자동 검증·격리 브라우저 검증 완료, 사용자 검수는 마지막 일괄 대기
- 최종 계약: [Fable 2차 기획](../docs/38-home-notice-board-plan.md)
- Branch/start HEAD: `experiment/task-home-002-personalized-shell` / `895de8d8666bc588c634ac8bdcb9612f26326335`
- 대표 repo·`main`·Persistent UAT·actual provider: 미변경
- Merge 승인: `0/3`

## 2. 구현 결과

- additive migration `0052_home_notice_board`로 전용 `notice_posts`를 추가했다.
- `/api/notices` 목록·상세·작성·삭제 API를 만들고 default operational authorization을 적용했다.
- client는 작성자 identity를 보낼 수 없고 서버가 effective user의 이름·현재 부서를 snapshot으로 저장한다.
- `(author_user_id, request_id)` unique 계약으로 네트워크 재시도와 중복 클릭을 멱등 처리한다.
- 목록은 원문을 반환하지 않고 whitespace를 정리한 100자 preview만 제공한다.
- 작성자 본인만 soft delete할 수 있고, 동일 작성자의 반복 삭제는 성공으로 처리한다.
- Home 하단은 최신 공지 5건으로 교체했다. 상단 부서 KPI, 중앙 내 업무·Pending·알림, 프로젝트 목록·상세 병목 집계는 변경하지 않았다.
- `/notices`, `/notices?compose=1`, `/notices/{id}`에 PC·모바일 목록·작성·상세를 구현했다.
- plain text 렌더링, 제목 100자·내용 2,000자, client/server validation과 오류 시 입력 유지·focus를 적용했다.
- 공지로 notification, work item, delivery와 외부 provider를 생성하지 않는다.

## 3. 변경 파일

- `database/migrations/0052_home_notice_board.sql`
- `backend/src/Emi.Qms.Api/Notices/*`, `backend/src/Emi.Qms.Api/Program.cs`
- `backend/tests/Emi.Qms.Api.Tests/NoticeApiTests.cs`, `PostgreSqlMigrationTests.cs`
- `frontend/src/notices.ts`, `api.ts`, `NoticeBoardPage.tsx`, `HomePage.tsx`, `App.tsx`, `styles.css`
- `frontend/tests/NoticeBoardPage.test.tsx`, `App.test.tsx`, `auth.test.tsx`
- `frontend/e2e/full-stack/home-dashboard.full-stack.spec.ts`

## 4. 검증 결과

| 검증 | 결과 |
| --- | --- |
| Backend Debug/Release build | 성공, warning/error 0 |
| Backend 전체 | `418/418` 성공 |
| migration + Notice API 표적 통합 | `36/36` 성공 |
| Frontend lint | error 0, 기존 `main.tsx` fast-refresh warning 1 |
| Frontend 전체 unit | `119/119` 성공 |
| Frontend production build | 성공, 기존 chunk-size warning 유지 |
| Isolated Full-Stack E2E | `1/1` 성공, 전용 tmpfs PostgreSQL·외부 provider disabled |
| Mobile overflow | 390px Home·목록·상세 `scrollWidth-clientWidth=0` |
| Desktop/mobile screenshot | 7개 synthetic 증빙 완료 |

초기 모바일 촬영에서는 mobile override가 shared base CSS보다 앞에 위치해 목록 meta가 카드 밖으로 밀렸다. override를 shared rule 뒤로 이동하고 grid area를 명시한 뒤 Full-Stack E2E와 390px 재촬영으로 해결했다.

실험 사용자 검수용 runtime은 기존 42982/41165 process를 종료하지 않고 `http://127.0.0.1:42983`/`41166`에 별도로 열었다. 같은 실험 DB에 additive `0052`를 적용했고 외부 알림과 mutation worker는 비활성화했다.

## 5. 스크린샷

- [PC Home 공지](notice-board-001-screenshots/01-home-notice-desktop-1440.png)
- [PC 공지 목록](notice-board-001-screenshots/02-notice-list-desktop-1440.png)
- [PC 공지 작성](notice-board-001-screenshots/03-notice-compose-desktop-1440.png)
- [PC 공지 상세](notice-board-001-screenshots/04-notice-detail-desktop-1440.png)
- [모바일 Home 공지](notice-board-001-screenshots/05-home-notice-mobile-390.png)
- [모바일 공지 목록](notice-board-001-screenshots/06-notice-list-mobile-390.png)
- [모바일 공지 상세](notice-board-001-screenshots/07-notice-detail-mobile-390.png)

## 6. Finding gate

| Finding | Severity | 상태 | 해소 |
| --- | --- | --- | --- |
| request retry duplicate | P2 | RESOLVED | author/request id unique와 기존 row 반환 |
| client author spoof | P2 | RESOLVED | actor claim 기반 server snapshot |
| list body overexposure | P2 | RESOLVED | preview-only list DTO, detail에서만 원문 |
| unauthorized delete | P2 | RESOLVED | author 조건부 update와 403/404 분리 |
| mobile card overflow | P2 | RESOLVED | post-base mobile grid area와 390px overflow 검증 |

Open P0/P1/P2: `0/0/0`. Risk acceptance 없음.

## 7. 개인정보·외부 연동

- screenshot은 개발 seed 사용자와 합성 공지만 사용했다.
- 실제 사용자·고객·email/UPN, credential, token과 provider payload를 기록하지 않았다.
- 외부 provider 호출은 0이며 공지는 알림 전달 pipeline과 독립적이다.

## 8. 사용자 검수·게시 경계

- 사용자 직접 검수: `사용자 검수 대기 — 마지막 일괄 검수`
- 대표 repo·main 승격, Persistent UAT migration/runtime handover, push/PR/merge: 별도 승인 전 금지
- `main` merge 승인: `0/3`
- local commit: 보류. 시작 전부터 동일 worktree에 migration `0050`·`0051`과 `App.tsx`·`api.ts`·`styles.css`·공통 tests의 다른 Task 미커밋 변경이 함께 존재해, 공지 변경만 재현 가능한 commit으로 분리하면 migration 기준선과 shared-file diff가 깨진다. 기존 WIP를 임의 포함하거나 정리하지 않았다.
- DB rollback은 migration 삭제가 아니라 새 additive migration의 forward-fix로 수행한다.

## 9. 5종 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 |
| SOP | 완료 | [docs/39-home-notice-board-sop.md](../docs/39-home-notice-board-sop.md) |
| User manual | 완료 | [docs/40-home-notice-board-user-manual.md](../docs/40-home-notice-board-user-manual.md) |
| Roadmap update | 완료 | [docs/00-product-roadmap.md](../docs/00-product-roadmap.md) |
| User validation checklist | 사용자 검수 대기 | [notice-board-001-user-validation-checklist.md](notice-board-001-user-validation-checklist.md) |

## 10. Fable 사용량·session

- 1차·2차 기획 전후 값은 [Change 001](notice-board-001-change-001.md)에 원문 projection으로 기록했다.
- 구현 종료: 5시간 22% 사용/78% 잔여(00:29 KST 초기화), 주간 전체 34% 사용/66% 잔여(07-25 07:59 KST), Fable 67% 사용/33% 잔여(초기화 parse 불가).
- Fable private session: `FABLE_TASK_SESSION_CLEANED`, session·transcript 각 2개 제거.
