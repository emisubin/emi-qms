# TASK-PRIVACY-NOTICE-001 Implementation report

## 상태

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- 구현 상태: Change 006 최종 문안·화면 승인 / 최신 원격 `main` 통합 게시 승인
- 자동 검증 상태: 완료
- 사용자 검수 상태: `완료`
- 운영 적용: `GO` — Change 006과 TASK-AZURE-DEPLOY-001 Change 021의 통합 검증·PR·Azure Gate 적용
- Git 게시: 단일 통합 PR·merge·공개 배포 승인 / 실행 결과 대기

## 해결한 업무 문제

사내 시범 운영 사용자가 개인정보 처리 범위, 문의 방법과 서비스 이용 기준을 상시 확인할 화면이 없고, 프로필 사진 선택 수집과 PWA 설치·알림 안내가 분리돼 있던 문제를 하나의 조회 흐름으로 정리했다.

## 포함·제외 범위

포함 범위는 로그인 후 정적 `개인정보·이용 안내` 화면, 모든 로그인 후 페이지 하단 회사 정보와 같은 글꼴·크기의 우측 텍스트 진입점, 선택 강조 없는 부드러운 목차 이동, EMI PMS logo의 홈 이동, 프로필 사진 선택 동의 dialog/bottom sheet, PWA 설치 안내의 설명·설치 방법·이용 안내다. 계정·조직 정보, 업무 파일·기록과 알림·접속·보안 정보의 보유 문구는 사용자 결정대로 `사내 규정에 따름`으로 표시한다.

Backend·API·DB·migration·기존 프로필 사진 데이터·Web Push·Service Worker·알림 권한 요청·실제 Teams/메일 provider·Persistent UAT·Azure·운영 배포는 변경하지 않았다. Excel·PDF·첨부 처리 동작도 변경하지 않았다.

## 아키텍처와 영향

- Frontend: 정적 페이지 component와 인증 후 route를 추가했다. 최신 `main`의 공통 회사 정보 footer를 재사용해 맨 우측 아래에 회사 정보와 같은 글꼴·크기의 텍스트 진입점을 배치하고 sidebar, mobile menu와 account panel에는 중복 진입점을 두지 않았다.
- 목차 이동: 기본 환경은 `smooth` scroll, URL hash 갱신과 대상 section focus만 사용한다. 클릭 뒤 검은 배경·선택 marker·도착 outline은 남기지 않으며 모션 감소 환경은 즉시 이동으로 바꾼다.
- 강조 표현: 개인정보 안내 상단의 굵은 검은 선과 시범 운영 안내 왼쪽의 굵은 빨간 선을 제거하고, 정보 구획용 얇은 중립 테두리는 유지한다.
- Logo navigation: desktop sidebar, mobile top bar와 mobile drawer의 EMI PMS logo를 기존 `홈` 메뉴와 같은 route action에 연결했다.
- 프로필 사진: 기존 업로드 권한·파일 형식·크기 검증을 유지하고 파일 선택기 앞에 비저장형 선택 동의를 추가했다. 사진 제거 흐름은 유지한다.
- PWA: 설치 경험의 기존 상태·설치 event 계약을 보존하고 안내 문구만 확장했다.
- Backend/API/DB/Migration: 영향 없음.
- 권한·Workflow·외부 발송: 영향 없음.
- 기존 기능 회귀: Frontend 전체 unit test와 build로 확인했다.

## 실제 변경 파일

- `frontend/src/PrivacyNoticePage.tsx`: 개인정보·이용 안내 검수본
- `frontend/src/App.tsx`: route, footer 텍스트 진입점, logo 홈 이동, 프로필 사진 선택 동의
- `frontend/src/PwaInstallExperience.tsx`: 설치 설명·방법·이용 안내
- `frontend/src/styles.css`: 안내 화면, 무선택 목차 이동과 desktop dialog/mobile bottom sheet 반응형 표현
- `frontend/src/design-system/wireframe.css`: 최신 `main` 회사 정보 footer의 우측 텍스트 link와 logo 클릭 표면
- `frontend/tests/App.test.tsx`: route·진입점·동의 전 파일 선택 차단 회귀
- `frontend/tests/privacy-notice.test.tsx`: 문안·보유 기준·연락 창구 렌더 회귀
- `frontend/tests/pwa-install.test.tsx`: 설치·알림 안내 회귀
- `tasks/privacy-notice-001-*.md`, `docs/00-product-roadmap.md`: 결정·검증·handoff 추적

## 기술적 결정과 검토한 대안

정적 단일 페이지를 선택해 Backend·DB 변경과 별도 문안 관리 권한을 만들지 않았다. 프로필 사진 동의는 전역 동의 기록 대신 사용자가 파일 선택을 시도할 때마다 안내하는 최소 방식으로 구현했다. 현재 처리방침에는 실제 알림 채널만 표시하고, 미래 모바일 푸시 가능성과 현재 권한 불필요 안내는 PWA 설치 팝업에만 둔다.

## 시행착오 및 폐기한 접근

기존 Backend를 그대로 연결한 첫 로컬 후보는 새 개발 포트의 Microsoft 인증 redirect가 없어 `401`이 발생했다. 운영 인증·DB·provider를 변경하지 않기 위해 이 후보를 종료하고, 사용자·업무 원문이 없는 bounded synthetic API를 별도 포트에 연결했다. 기존 5174 runtime은 재시작하거나 변경하지 않았다. 사용자의 Change 003 지시 뒤에는 기존 미커밋 후보를 보존하고 원격 `main` SHA `8ae3645d66543c0f234777cf19e8487324f21217`에서 임시 통합 worktree를 만들어 새 회사 footer·제품 logo 위에 검수 후보를 다시 통합했다. 검수 서버 교체 중 원격 `main`이 운영 상태 문서만 바꾼 `9a25157f0b8d1e78ad5392acf336ebf3c0f61b64`로 진행돼 비중첩을 확인하고 현재 후보 기준선을 해당 SHA로 fast-forward했다.

## 검증 결과

- Change 005 후 전체 Frontend 회귀: lint 오류 0·기존 경고 1, typecheck 통과, `27 files / 194 tests` 통과, production build 통과
- Change 005 실제 브라우저: desktop·390px 모두 굵은 상단·왼쪽 강조선 0, page-level horizontal overflow 0
- Change 004 관련 test: `2 files / 83 tests` 통과
- Frontend 전체 test: `27 files / 194 tests` 통과
- TypeScript typecheck: 통과
- ESLint: 오류 0, 기존 `src/main.tsx` Fast Refresh 경고 1건
- Production build: 통과, 기존 대형 bundle 경고 유지
- `git diff --check`: 통과
- Desktop 실제 브라우저: 목차 black background 0·선택 marker 0, smooth scroll·hash·focus, footer link의 회사 정보 동일 font family·size·border 0·투명 배경·우측 하단 정렬, sidebar logo 홈 이동 확인
- 390px 실제 브라우저: page-level horizontal overflow 0, 목차 black background·선택 marker 0, footer 텍스트 우측 정렬, mobile top·drawer logo 홈 이동과 drawer 닫힘 확인
- 로컬 runtime: Frontend `127.0.0.1:5180`, synthetic API `127.0.0.1:5086` health 확인

Persistent UAT, 실제 Microsoft 로그인, 실제 기존 프로필 사진 count, PWA standalone, Teams embedded, 실제 provider와 운영 Azure는 승인 범위 밖이라 실행하지 않았다. 자동 검증과 로컬 화면 검증은 live 운영 검증을 의미하지 않는다.

## 개인정보·secret 검토

사용자가 제공한 실제 업무 연락 값은 승인된 제품 화면 source 한 파일에만 존재한다. Task 문서·test·검증 증빙·Roadmap에는 전사하지 않았고 secret·credential·tenant identifier는 추가하지 않았다. 검수 API는 synthetic persona와 고정 aggregate만 사용하며 실제 사내 데이터에 연결하지 않는다.

## Finding과 잔여 위험

| ID | 심각도 | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `PRIV-PLAN-001` | P1 | RESOLVED | 회사가 제공한 보유 기준과 현재 정적 문안의 운영 적용을 Change 006에서 최종 승인했다. | 규정·처리 목적 변경 시 재검토한다. |
| `PRIV-PLAN-004` | P2 | RESOLVED | 로그인 후 전용 route를 현재 사내 서비스 운영 범위로 승인했다. | 로그인 전 공개가 필요해질 때 별도 change로 검토한다. |
| `PRIV-PLAN-005` | P2 | RESOLVED_FOR_CURRENT_SCOPE | 기존 Teams·메일 외부 서비스는 회사 검토가 완료됐고 새 provider·개인정보 전송을 추가하지 않는 현재 정적 문안을 승인했다. | 계약·provider·전송 범위 변경 시 재검토한다. |
| `FRONTEND-BUNDLE-001` | P3 | BACKLOG | 기존 production bundle 크기 경고가 유지된다. 이번 정적 화면의 기능 실패는 아니다. | Roadmap 기존 성능 backlog에서 실제 로딩 측정 후 판단한다. |
| `PRIV-LEGAL-REVIEW-001` | P3 | PERIODIC_REVIEW | Change 006은 회사의 현재 운영 문안 승인이지 법률 자문이나 영구적 적합성 보증이 아니다. | 법령·사내 규정·Microsoft 계약·provider·처리 목적 변경 시 담당자가 문안을 재검토한다. |

Open P0/P1/P2는 0이며 운영 게시·merge 품질 판정은 `GO`다. 통합 후보·PR CI·Azure 공개 검증은 TASK-AZURE-DEPLOY-001 Change 021에서 별도로 통과해야 한다.

## 사용자 검수 결과와 남은 항목

자동·브라우저 검증 뒤 사용자가 화면·문안과 지금까지의 수정 결과를 모두 승인했다. 현재 상태는 `사용자 검수 완료 / 운영 적용 승인`이다.

기존 프로필 사진은 강제 삭제하지 않고 다음 변경 시 같은 선택 안내를 적용한다. 실제 인증 환경·PWA/Teams 표면, 통합 PR과 Azure release는 TASK-AZURE-DEPLOY-001 Change 021의 게시 Gate에서 확인한다.

## Rollback·복구

현재 변경은 미커밋 로컬 후보이므로 적용하지 않기로 결정하면 이 전용 worktree의 변경을 폐기하면 된다. 운영에는 반영되지 않아 DB·migration·외부 발송 rollback이 없다. 향후 게시 뒤 문안 문제가 발견되면 이전 검증 Frontend revision으로 되돌리고 기존 공지 기능으로 정정 사실을 알린다.

## 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성 완료 | 이 문서 |
| SOP | 작성 완료 | `tasks/privacy-notice-001-sop.md` |
| User manual | 작성 완료 | `tasks/privacy-notice-001-user-manual.md` |
| Roadmap update | 작성 완료 | `docs/00-product-roadmap.md` |
| User validation checklist | 완료 | `tasks/privacy-notice-001-user-validation-checklist.md` |
