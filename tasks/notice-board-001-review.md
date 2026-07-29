# TASK-NOTICE-BOARD-001 — Home 공지사항 게시판 1차 기획 Codex 내용 Review

- reviewOwner: `CODEX`
- reviewSource: `tasks/notice-board-001-planning.md`
- reviewStatus: `RESOLVED_FOR_EXPERIMENT_SECOND_PLANNING`
- canonicalMainApproval: false

## 1. 결론

Fable 권장안 A를 유지한다. 신규 `notice_posts`와 전용 `/api/notices`를 두어 Home 하단만 게시판으로 교체하는 방향이 사용자 문제를 가장 직접적으로 해결한다. 기존 `ChannelNotice`를 재사용하면 게시글이 unread 알림·Teams delivery와 결합되므로 명시적 제외 범위를 위반한다.

v1은 최신 목록·전체 게시판·상세·작성과 작성자 본인 soft delete까지로 한정한다. 수정(edit)은 게시된 공지가 조용히 바뀌는 감사 위험이 있어 보류하고, 오작성은 본인 soft delete 후 새 글 작성으로 정정한다. 관리자 moderation·고정·검색·댓글·첨부·읽음 통계·공지 알림은 별도 후속 기능으로 남긴다.

## 2. 기능 판단

### 유지

- Home 상단 부서 KPI와 중앙 내 업무·Pending·알림의 데이터·배치·deep link를 그대로 보존하고 하단 wide slot만 교체한다.
- 독립 `notice_posts` table과 `/api/notices` 목록·상세·작성·본인 삭제 API를 사용한다.
- 승인 완료 active 내부 사용자 전체에 조회·작성 권한을 주고 신규 role/permission은 만들지 않는다. 현재 default authorization policy가 `OperationalUserRequirement`를 포함함을 Repository에서 확인했다.
- Home 최신 5건, 전체 게시판 20건 pagination, 제목·본문 preview·작성자 이름/부서 snapshot·작성 시각을 표시한다.
- 제목 1~100자, 본문 1~2000자, trim 후 빈 값 거부, 개행 보존 plain text, HTML 미해석·auto-link 없음 정책을 사용한다.
- 작성자 본인 soft delete를 제공하고 타인 글 삭제와 물리 삭제를 서버에서 차단한다.
- 공지 경로는 `notifications`, `notification_recipients`, `work_items`, delivery와 provider를 호출하지 않는다.
- Desktop·390px 모두 table 축소가 아닌 card/list 구조로 구현한다.

### 추가

- 작성 요청에 client-generated `requestId`를 포함하고 DB에서 `(author_user_id, request_id)` unique로 중복 submit·network retry를 멱등 처리한다. Frontend 버튼 잠금만으로는 요청 완료 직전 연결이 끊긴 경우 동일 공지가 두 번 생길 수 있다.
- 목록 정렬은 `created_at_utc DESC, id DESC`로 고정하고 page/pageSize는 서버에서 `page >= 1`, `1 <= pageSize <= 100`으로 검증한다. Home은 `pageSize=5`, 게시판은 `20`을 사용한다.
- 작성자 snapshot은 insert 시점에 `qms_users`와 현재 부서를 서버에서 조회해 같은 transaction/statement로 저장한다. 부서가 없는 허용 계정은 nullable snapshot으로 저장하고 화면에는 `부서 미지정`을 표시한다. client가 작성자·부서를 보내거나 덮어쓸 수 없다.
- soft delete는 `deleted_at_utc`와 `deleted_by_user_id`를 원문과 함께 보존한다. 같은 작성자의 반복 DELETE는 멱등 성공, 다른 사용자는 403, 존재하지 않는 글은 404로 고정한다.
- 목록 응답은 본문 전문을 반복하지 않고 server-side preview를 제공하고, 상세 API만 전문을 반환한다. 긴 게시글이 20개 목록 payload와 모바일 렌더링을 불필요하게 키우지 않게 한다.
- Home 공지 조회 실패는 기존 widget failure isolation을 유지하고 상단·중앙을 차단하지 않는다. 작성 성공 뒤 게시판 목록과 Home 재진입 시 최신 데이터가 보이도록 refresh 경계를 명시한다.
- ReviewSafe의 공통 mutation 차단을 우회하지 않는다. 현재 실험 검수는 기존 owned validation runtime 또는 isolated E2E만 사용하고 canonical 5174/5081·Persistent UAT handover를 수행하지 않는다.

### 보류

- 작성 후 edit와 revision history는 보류한다. 수정이 필요하면 soft delete 후 정정 공지를 새로 작성하며, 향후 edit를 추가할 때는 row version/CAS와 append-only revision이 함께 필요하다.
- System Administrator의 타인 글 삭제·복구·moderation은 보류한다. 전사 작성 권한과 moderation 권한은 같은 정책이 아니며 별도 사용자 결정이 필요하다.
- 공지 고정, 검색, 분류, 댓글, 반응, 첨부, 읽음 확인 통계와 공지 알림 opt-in은 모두 별도 `NEW_FEATURE`다.
- 게시판 자체 Excel 내보내기와 PDF는 현재 사용자 문제에 필요하지 않아 제외한다.

### 제거

- 기존 `ChannelNotice`/`notifications` 재사용안을 제거한다.
- Home widget 안에 작성·pagination·상세를 모두 넣는 inline-only 안을 제거한다. 공지가 쌓일 때 병목 widget과 같은 대표성 문제를 반복한다.
- Frontend만의 권한 판단, client-supplied 작성자 snapshot과 HTML/Markdown 렌더링을 제거한다.
- 별도 audit table·append-only trigger는 v1에서 제거한다. 행 원문과 작성·삭제 identity/time을 보존하고 edit·hard delete가 없으므로 현재 lifecycle에는 과도하다.

## 3. Finding과 Resolution

| ID | Severity | Finding | 2차 기획 Resolution |
| --- | --- | --- | --- |
| `NOTICE-CREATE-RETRY-DUPLICATE` | P2 | 1차 기획은 Frontend 중복 submit 차단만 두어 응답 손실 뒤 재시도 시 같은 공지가 중복 생성될 수 있다. | author별 request id unique와 멱등 POST 응답을 최종 계약에 추가한다. |
| `NOTICE-AUTHOR-SNAPSHOT-TRUST` | P2 | 작성자 snapshot을 저장한다는 설명만 있고 server-side source와 atomicity가 명확하지 않으면 client 조작 또는 부서 이동 race가 생길 수 있다. | server가 current effective user와 department를 읽어 insert하며 request에서 identity field를 받지 않는다. |
| `NOTICE-LIST-PAYLOAD` | P2 | 목록 20건에 본문 전문을 실으면 모바일 payload와 DOM이 커지고 preview 정책이 Frontend마다 달라질 수 있다. | 목록은 server-generated plain-text preview, 상세만 전문 반환으로 분리한다. |
| `NOTICE-DELETE-SEMANTICS` | P2 | soft delete 반복·타인·미존재 결과가 미정이면 Frontend feedback과 동시 요청 결과가 흔들린다. | 본인 반복 삭제 멱등 성공, 타인 403, 미존재 404를 고정한다. |
| `NOTICE-EDIT-AUDIT` | P3 | 수정 없이 삭제·재작성은 불편할 수 있다. | v1에서는 silent edit를 피하고 후속 edit는 revision audit+CAS를 포함한 별도 기능으로 추적한다. |
| `NOTICE-MODERATION-GAP` | P3 | 작성자가 퇴사·비활성화된 뒤 부적절 공지를 운영자가 내릴 수 없다. | 현재 실험 v1 제외를 유지하고 운영 승격 전 moderation 정책을 별도 Task 후보로 기록한다. |

## 4. 권장 개발 순서

1. additive `0052` notice table·author/request unique·index·FK와 fresh/`0051→0052` migration 검증.
2. `Notices` contract/store/endpoints에서 active default policy, server-side snapshot, validation, idempotent create/delete, pagination을 구현한다.
3. Frontend API/type, `/notices` route와 목록·상세·작성·본인 삭제를 구현한다.
4. `HomePage` 하단 widget의 data source와 presentation만 공지로 교체하고 상단·중앙 회귀를 고정한다.
5. Backend 전체·Frontend 전체·migration·isolated Full-Stack·desktop/390px screenshot·종료 산출물을 완료한다.

## 5. 2차 기획 판정

- 권장안 자동 채택: `GO`
- openBlockingDecisionCount 기대값: `0`
- 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge: 제외
