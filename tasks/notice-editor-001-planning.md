# TASK-NOTICE-EDITOR-001 — 공지 서식·수정·첨부 기획안

> 상태: 사용자 직접 승인 / Codex 구현 기준
> 작성 모델: Codex (사용자 명시 요청으로 Fable 기획 owner 예외 적용)
> taskType: `NEW_FEATURE`
> implementationApproved: `true`

## 1. 목표

공지 작성자는 본문의 중요한 문장을 굵게 표시하고 등록한 글을 안전하게 수정하며 업무 파일을 첨부할 수 있다. 공지를 볼 수 있는 사내 사용자는 첨부파일을 내려받을 수 있다.

## 2. 포함 범위

- 작성·수정 화면의 선택 영역 `굵게` 처리와 안전한 상세 렌더링
- 작성자 본인만 가능한 공지 수정
- 수정 전 원문 append-only 이력과 version CAS 동시성 보호
- 작성자 본인이 공지당 최대 5개 첨부 추가·제거
- 등록 후 첨부 추가·제거 control은 `공지 수정` 화면에서만 노출
- 조회 가능한 모든 인증 사용자의 첨부 다운로드
- PDF, JPEG, PNG, DOCX, XLSX, PPTX의 실제 내용 기반 형식 검사
- 파일당 최대 10MB, 안전한 파일명, SHA-256, DB `bytea` 저장
- 기존 Upload Security middleware의 악성코드 검사와 이미지 metadata 차단 계약 재사용
- desktop·390px의 작성·수정·첨부·다운로드 UX

## 3. 제외 범위

- 임의 HTML, 글꼴 종류·크기·색상·밑줄·이미지 본문 삽입
- 타인 공지 수정, 관리자 moderation, 공지 복구 UI, 수정 이력 조회 UI
- 실행 파일·압축 파일·구형 Office binary 파일 업로드
- object storage/CDN, 실제 운영 scanner·Persistent UAT handover
- 공지 작성·수정에 따른 알림·내 업무·Teams·메일 발송
- `main` 반영, commit, push, PR, merge

## 4. 사용자 흐름

1. 작성자는 본문에서 문장을 선택하고 `굵게`를 눌러 제한형 굵게 표시를 적용하거나 해제한다.
2. 등록 전 선택한 파일은 공지 생성 직후 순서대로 업로드하며, 일부 실패 시 공지는 유지되고 실패 파일을 다시 추가할 수 있다.
3. 상세 화면은 첨부 목록과 다운로드만 제공한다. 작성자는 `공지 수정`을 눌러 제목·본문·첨부 목록을 편집하고 파일을 추가·제거한다.
4. 저장 시 서버가 작성자와 `version`을 확인하고, 기존 제목·본문·서식과 변경자를 revision에 보존한 뒤 새 version을 반환한다.
5. 다른 사용자는 상세의 첨부 목록에서 파일명·크기를 확인하고 내려받는다.

## 5. 핵심 정책

### 본문 서식

- 저장 형식은 `PlainTextV1` 또는 `BoldMarkupV1`이다.
- `BoldMarkupV1`은 `**굵게**`만 해석한다. HTML은 어떤 경우에도 해석하지 않고 React text node로 렌더링한다.
- 목록 preview는 굵게 marker를 제거한 평문으로 표시한다.
- 기존 공지는 `PlainTextV1` 기본값으로 무변경 표시한다.

### 수정과 감사

- `PUT /api/notices/{noticeId}`는 작성자 본인만 가능하다.
- 요청의 `expectedVersion`과 현재 version이 다르면 `409`로 중단하고 새로고침을 안내한다.
- 수정 직전 제목·본문·본문 형식·version을 `notice_post_revisions`에 남긴다.
- 상세에 최초 작성 시각과 최근 수정 시각을 구분해 표시한다.

### 첨부

- 공지당 active 첨부 최대 5개, 개별 10MB 이하.
- 서버는 확장자나 browser MIME을 신뢰하지 않고 signature와 OOXML package 구조로 형식을 판정한다.
- 원본 파일명은 basename만 사용하고 control character를 제거하며 180자로 제한한다.
- 첨부 삭제는 작성자 본인의 soft delete다. binary와 hash는 감사 근거로 보존한다.
- 다운로드는 공지 조회와 같은 인증 범위이며 삭제된 공지·첨부는 `404`다.
- 응답은 `Content-Disposition: attachment`, `Cache-Control: private, no-store`, `X-Content-Type-Options: nosniff`를 사용한다.

## 6. 데이터·API

- migration `0073_notice_editor_and_attachments.sql`
  - `notice_posts`: `body_format`, `version`, `updated_at_utc`, `updated_by_user_id`
  - `notice_post_revisions`: 수정 전 snapshot과 변경자·시각
  - `notice_attachments`: 원본 파일명, normalized MIME, byte size, SHA-256, content, 생성·soft delete metadata
- API
  - 기존 `POST /api/notices`: `bodyFormat` 추가
  - 신규 `PUT /api/notices/{noticeId}`: 제목·본문·format·expectedVersion 수정
  - 신규 `POST /api/notices/{noticeId}/attachments`: multipart 첨부 추가
  - 신규 `DELETE /api/notices/{noticeId}/attachments/{attachmentId}`: 작성자 soft delete
  - 신규 `GET /api/notices/{noticeId}/attachments/{attachmentId}/content`: 인증 사용자 다운로드

## 7. 오류·복구

- field validation은 기존 한글 `ValidationProblem`을 유지한다.
- 타인 수정·첨부 변경은 `403`, 없는/삭제된 대상은 `404`, version 충돌·첨부 상한은 `409`다.
- 등록 직후 첨부 일부가 실패하면 성공한 파일과 공지 원문은 유지하고 상세 화면에서 실패 파일명을 알려 재시도할 수 있게 한다.
- 다운로드 실패는 상세 화면을 유지하고 action 근처에 오류를 표시한다.

## 8. 검증 기준

- Backend: 작성·조회 하위 호환, 작성자 수정 성공, 타인 수정 403, stale version 409, revision snapshot, 허용·금지 파일, 개수·용량, 타인 다운로드, 작성자 제거, 삭제 후 404, 알림/내 업무 미생성.
- Migration: 기존 `0072 → 0073` 적용과 fresh 전체 적용, 기존 migration 무수정.
- Frontend: 굵게 선택/해제, 안전 렌더링, 수정 저장·충돌, 첨부 선택·추가·제거·다운로드, 작성 직후 일부 업로드 실패 안내.
- UI: desktop·390px, loading/empty/error/success, page-level horizontal overflow 0, keyboard focus와 `aria-live`.

## 9. 완료 조건

- 자동 테스트와 격리 browser 검증 통과, Open P0/P1/P2 0.
- implementation report, SOP, user manual, Roadmap update, user validation checklist의 상태·위치를 추적한다.
- 사용자 검수 서버 주소를 제공하고 결과를 `사용자 검수 대기`로 기록한다.
- 게시·운영 적용은 사용자 결과 확인과 별도 Git/배포 승인 전 수행하지 않는다.
