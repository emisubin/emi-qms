# TASK-NOTICE-EDITOR-001 — Implementation report

> 상태: 사용자 검수·latest main 통합 전체 검증 완료 / 통합 게시 승인
> branch: `feat/task-notice-editor-001`
> 통합 기준선: `origin/main` `c0e756eb598700fb55992af824280810e05d83da`

## 해결한 업무 문제

작성 후 정정할 수 없고 평문만 입력되며 업무 파일을 함께 전달할 수 없던 공지사항을, 제한형 굵게 서식·작성자 수정·첨부 다운로드가 가능한 사내 게시판으로 확장했다. 사용자 Change 001에 따라 상세는 첨부 조회·다운로드만 제공하고 파일 추가·제거는 `공지 수정` 화면에 모았다.

## 포함·제외 범위

- 포함: `tasks/notice-editor-001-planning.md`와 `tasks/notice-editor-001-change-001.md`.
- 제외: 실제 malware scanner/provider와 새 외부 알림 연동. 대표 `main`과 Azure 공개 배포는 Change 002·TASK-AZURE-DEPLOY-001 Change 021의 승인형 Gate에서 진행한다.

## 기술적 결정과 검토한 대안

- 본문은 임의 HTML editor 대신 `PlainTextV1`·`BoldMarkupV1`만 저장한다. `**...**`만 React text node와 `<strong>`으로 해석해 script/HTML 실행 경계를 만들지 않았다.
- 공지 수정은 작성자 본인·version CAS로 제한하고 수정 직전 snapshot을 append-only revision에 저장한다.
- 첨부는 공지당 5개, 파일당 10MB로 제한한다. PDF/JPEG/PNG와 DOCX/XLSX/PPTX의 실제 signature·OOXML package를 검사한다.
- Local 후보에서는 기존 Upload Security middleware를 재사용하고 binary를 PostgreSQL `bytea`에 저장한다. object storage와 실제 scanner handover는 운영 범위에서 제외했다.
- 상세 화면의 첨부는 모든 조회자에게 다운로드만 제공한다. 추가·제거는 작성자가 편집 화면에 들어온 동안에만 노출하며 서버 권한도 작성자로 강제한다.
- 공지 변경으로 알림·내 업무·Teams·메일을 생성하지 않는다.

## 전체 아키텍처와 영향

- DB: `notice_posts`에 body format·version·수정 metadata를 추가하고, append-only `notice_post_revisions`와 soft-delete `notice_attachments`를 추가했다.
- Backend: 작성자 수정, 첨부 추가·제거, 인증 사용자 다운로드 API와 파일 내용 검증을 추가했다.
- Frontend: 안전한 굵게 편집·렌더링, 작성자 수정 화면, 작성/편집 첨부, 조회자 다운로드 UI를 추가했다.
- 기존 공지 목록·Home widget·프로젝트 workflow·알림/내 업무 계산은 변경하지 않았다.

## 실제 변경 파일

- Migration: `database/migrations/0073_notice_editor_and_attachments.sql`
- Backend: `backend/src/Emi.Qms.Api/Notices/NoticeContracts.cs`, `NoticeEndpointExtensions.cs`, `NoticeStore.cs`, `NoticeAttachmentValidator.cs`
- Backend tests: `backend/tests/Emi.Qms.Api.Tests/NoticeApiTests.cs`, `PostgreSqlMigrationTests.cs`
- Frontend: `frontend/src/NoticeBoardPage.tsx`, `api.ts`, `notices.ts`, `styles.css`
- Frontend tests: `frontend/tests/NoticeBoardPage.test.tsx`, `frontend/e2e/full-stack/notice-editor.full-stack.spec.ts`
- Governance: Task identity/planning/change/report/checklist와 `docs/00-product-roadmap.md`

## 검증 결과

| 검증 | 적용 여부 | 결과 | 근거/미실행 이유 |
| --- | --- | --- | --- |
| Backend Release build | 적용 | PASS | warning/error 0 |
| Backend 공지·migration 집중 검증 | 적용 | PASS | 5/5 |
| Backend 전체 회귀 | 적용 | PASS | 496/496 |
| Frontend 공지 집중 검증 | 적용 | PASS | 6/6 |
| Frontend 전체 회귀 | 적용 | PASS | 27 files, 197/197 |
| Frontend typecheck/build | 적용 | PASS | typecheck 0, production build 성공 |
| Frontend lint | 적용 | PASS with existing warning | error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Browser desktop·390px | 적용 | PASS | 굵게 렌더링, 작성자 수정, 상세/편집 첨부 control 분리, 조회자 권한, 수정 시각, overflow 0, console error 0 |
| 실제 Docker Full-Stack E2E | 적용 | PASS | isolated PostgreSQL fresh DB·migration `0073`, 전체 58/58 |
| 사용자 검수 | 적용 | PASS | 2026-08-11 사용자가 지금까지의 수정 결과를 모두 승인 |

## 개인정보·secret 검토

- tracked 산출물은 synthetic 역할명·파일명·UUID만 사용한다. 실제 사용자·업무 원문·credential은 기록하지 않았다.
- 첨부 download 응답은 `private, no-store`, `nosniff`, attachment disposition을 적용한다.
- 파일명은 basename·control character 제거·180자 상한으로 정규화하고, 삭제는 감사 근거 보존을 위해 soft delete한다.

## 시행착오 및 폐기한 접근

- Browser plugin은 blob anchor 방식의 다운로드 artifact event를 반환하지 않아 UI click의 실제 저장 파일을 자동 증빙하지 못했다. API integration test로 권한·본문·header·삭제 후 404를 확인하고 수동 다운로드 항목을 checklist에 유지했다.
- 최초 UI는 작성자 상세에 첨부 관리 control을 바로 노출했다. 사용자 Change 001에 따라 상세는 다운로드 전용, 편집 화면은 추가·제거 가능 구조로 정리했다.
- 기존 후보에서는 실행 정책에 막혔던 Docker Full-Stack을 승인된 통합 후보에서 실제 실행했다. 최초 실패 3건은 제품 터치 대상 2건과 test locator 1건으로 분리해 Change 007·Change 003으로 보정하고 전체를 재실행했다.

## Finding과 잔여 위험

- Open P0/P1/P2: 0.
- `NOTICE-E2E-ENV-001` P3 / RESOLVED: 통합 후보의 isolated PostgreSQL Full-Stack 전체 58/58 통과.
- `NOTICE-BASE-DRIFT-001` P3 / RESOLVED: 최신 `origin/main` `c0e756e` 통합과 전체 회귀 완료.
- `NOTICE-E2E-SELECTOR-001` P2 / RESOLVED: 수정 완료 뒤 넓은 부분 문자열 locator가 네 요소와 일치한 test-only 실패를 Change 003의 정확 상태 문구 단언으로 보정하고 집중·전체 Full-Stack 통과.
- 기존 bundle 500KB warning과 `main.tsx` lint warning은 이번 변경에서 새로 발생하지 않았다.

## Rollback·복구

- 아직 commit·push·운영 적용 전이므로 Task worktree 변경을 게시하지 않으면 제품에는 영향이 없다.
- migration `0073`은 additive이며 운영 미적용이다. 운영 적용 뒤에는 schema 강제 삭제보다 새 migration을 통한 forward-fix를 원칙으로 한다.
- 첨부 soft delete와 revision snapshot은 복구·감사 근거로 남는다.

## 운영·검수 SOP

1. 게시 후보를 latest `origin/main`에 동기화하고 migration 충돌을 확인한다.
2. 전체 Backend·Frontend·실제 Full-Stack E2E를 실행한다.
3. 배포 시 migration `0073` → Backend → Frontend 순서를 지킨다.
4. 작성자 수정·첨부 추가/제거와 타 사용자 다운로드를 검수한다.
5. 오류 시 새 첨부 등록을 중단하고 API/DB 상태를 확인한다. 이미 적용한 additive schema는 제거하지 않고 application rollback 또는 forward-fix한다.

## 사용자 안내

1. 새 공지에서 중요한 문장을 선택하고 `굵게`를 누른다.
2. 필요하면 작성 화면에서 첨부파일을 선택하고 공지를 등록한다.
3. 상세에서는 첨부파일명을 눌러 내려받는다.
4. 작성한 공지의 제목·본문·첨부를 바꾸려면 `공지 수정`을 누른다.
5. 수정 화면에서 기존 파일을 제거하거나 새 파일을 선택해 추가한 뒤, 제목·본문 변경은 `수정 저장`으로 완료한다.

## 사용자 검수 결과와 남은 항목

- 상태: `사용자 검수 완료 / 통합 게시 승인`
- 2026-08-11 사용자가 굵게·수정·첨부와 편집 화면 전용 첨부 관리 결과를 모두 승인했다.
- latest main 동기화·실제 Full-Stack은 완료했다. PR CI·Azure 공개 검증은 Change 002와 TASK-AZURE-DEPLOY-001 Change 021에서 완료한다.

## 5종 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 |
| SOP | 완료 | 이 문서 `운영·검수 SOP` |
| User manual | 완료 | 이 문서 `사용자 안내` |
| Roadmap update | 완료 | `docs/00-product-roadmap.md` |
| User validation checklist | 완료 | `tasks/notice-editor-001-user-validation-checklist.md` |
