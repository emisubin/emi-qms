# TASK-PRIVACY-NOTICE-001 Change 005 — 개인정보 안내 강조선 제거

## 승인과 기준선

- canonicalTaskId: `TASK-PRIVACY-NOTICE-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- gateStatus: `PASS_REUSE`
- sourceBaseline: 최신 `origin/main` `9a25157f0b8d1e78ad5392acf336ebf3c0f61b64` 기반 로컬 검수 후보
- userApproval: 2026-08-11 사용자가 개인정보 이용안내 페이지의 강조선을 모두 삭제하도록 명시
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- productionReleaseApproved: `false`

## 증상과 기대 동작

- 증상: 개인정보 안내 상단에 굵은 검은 선이, 시범 운영 안내 왼쪽에 굵은 빨간 선이 있어 사용자가 불필요한 강조로 인식한다.
- 기대: 두 강조선을 제거하고 기존 정보 구획용 얇은 중립 테두리·내용·간격·반응형 구조는 유지한다.

## 변경 경계

- 포함: `.privacy-notice-hero`의 굵은 상단선 제거, `.privacy-pilot-note`의 굵은 왼쪽선 제거, desktop·390px 회귀 확인.
- 제외: 개인정보 문안, 목차 이동, footer 진입점, logo 홈 이동, 프로필 사진 동의, PWA 안내, Backend·API·DB·migration·provider·운영 배포.
- 보존할 불변조건: page-level overflow 0, 일반 카드 구획 유지, 접근성과 기존 route 동작 유지.

## 검증 계획

- `git diff --check`
- Frontend lint·typecheck·전체 unit·production build
- 로컬 검수 서버 desktop·390px에서 굵은 상단·왼쪽 강조선 0과 가로 overflow 0 확인

## 결과

- 구현: 완료
- 자동 검증: Frontend lint 오류 0·기존 경고 1, typecheck 통과, 전체 test `27 files / 194 tests` 통과, production build 통과
- 브라우저 검증: desktop·390px에서 굵은 상단·왼쪽 강조선 0, page-level horizontal overflow 0
- 사용자 검수: 대기
