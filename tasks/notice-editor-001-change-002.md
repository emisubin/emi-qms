# TASK-NOTICE-EDITOR-001 Change 002 — 사용자 검수 승인과 운영 게시

## 승인과 범위

- 승인일: 2026-08-11
- 승인 source: 사용자가 지금까지의 공지사항 굵기·수정·첨부 기능과 Change 001 결과를 모두 승인하고 `main` 병합과 Azure 공개 배포를 명시했다.
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- gateStatus: `PASS_REUSE`
- mergeApproved: `true` — 이번 통합 PR의 단일 병합에 한정
- productionReleaseApproved: `true` — 병합된 정확한 최신 `main` SHA에 한정

## 확정 동작

- 공지 작성 화면에서 제한형 굵게와 최초 첨부를 제공한다.
- 작성자만 `공지 수정`에서 제목·본문·첨부 추가·제거를 수행한다.
- 상세 화면은 작성자와 다른 조회자 모두 첨부 조회·다운로드만 제공한다.
- 기존 작성자 권한, revision·soft delete·파일 형식/크기 제한과 알림 미생성 계약을 유지한다.

## 게시 Gate

- 최신 `origin/main`에 통합하고 Backend·Frontend·실제 Full-Stack E2E를 다시 실행한다.
- GitHub `main-pr-only` Ruleset에 따라 Ready PR과 필수 `CI Gate` 성공 뒤에만 병합한다.
- 병합된 정확한 `main` SHA만 승인형 Azure workflow에 전달한다.
- migration `0073` 성공 뒤 Backend와 Frontend를 교체하고 공개 health·익명 인증 차단을 확인한다.
