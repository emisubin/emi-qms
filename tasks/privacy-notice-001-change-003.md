# TASK-PRIVACY-NOTICE-001 Change 003 — 최신 main footer 진입점과 부드러운 목차 이동

## 승인 기록

- approvalSource: `USER_EXPLICIT_VALIDATION_REVISION`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- implementationApproved: `true`
- localValidationRuntimeApproved: `true`
- commitApproved: `false`
- pushApproved: `false`
- pullRequestApproved: `false`
- mergeApproved: `false`
- productionDeploymentApproved: `false`
- persistentUatApproved: `false`
- actualProviderApproved: `false`

## 사용자 수정 지시

1. `계정·조직 정보`의 보유 기간을 `퇴사 전까지`에서 `사내 규정에 따름`으로 변경한다.
2. 개인정보·이용 안내 진입점은 sidebar, mobile menu와 계정 menu에 두지 않는다. 최신 원격 `main`의 각 로그인 후 페이지 하단 회사 주소 footer 옆에 작은 링크로 표시한다.
3. 개인정보 안내 화면의 목차 항목을 누르면 대상 section으로 즉시 점프하지 않고 부드럽게 스크롤한다. 이동 뒤 대상 section에 keyboard focus를 두고 URL hash를 갱신한다. `prefers-reduced-motion` 사용자는 즉시 이동을 유지한다.

## 최신 main 통합 작업공간

- purpose: 기존 미커밋 검수본과 실행 중인 5180 runtime을 보존하면서 원격 `main`의 새 회사 footer·제품 logo 변경 위에 Change 001~003을 통합한다.
- owner: `TASK-PRIVACY-NOTICE-001 Change 003`
- initialBase: `origin/main` SHA `8ae3645d66543c0f234777cf19e8487324f21217`
- currentBase: `origin/main` SHA `9a25157f0b8d1e78ad5392acf336ebf3c0f61b64` — 운영 배포 상태 문서 3개만 추가돼 Frontend 변경과 비중첩임을 확인하고 fast-forward했다.
- expectedLifetime: Change 003 로컬 사용자 검수와 적용·수정·폐기 결정까지
- cleanupBoundary: 사용자 결정과 process ownership을 확인한 뒤 별도 승인 범위에서만 worktree를 제거한다. 기존 검수 worktree·5174·Persistent UAT는 건드리지 않는다.

## 변경 허용 범위

- 최신 `main`의 `CompanyInformationFooter` app 변형과 개인정보 안내 route 연결
- 기존 검수본의 개인정보 안내 page, 프로필 사진 선택 동의와 PWA 안내를 최신 `main`에 통합
- 목차의 smooth scroll·focus·hash UX와 관련 style/test
- Change 003 검증 결과를 기존 implementation report·사용자 checklist에 반영
- 최신 `main` 기반 별도 로컬 Frontend 검수 runtime

## 제외 범위

- 로그인 전 공개 개인정보 안내 route
- Backend·API·DB·migration·기존 사진 데이터 조회·변경
- Web Push·알림 권한·실제 Teams/메일 provider
- 기존 5174와 Persistent UAT·Azure·운영 배포
- commit·push·PR·merge·branch/worktree 정리

## 구현·검증 결과

- 원격 `main`의 공통 회사 정보 footer에만 작은 진입 버튼을 연결했다.
- 계정·조직 정보를 포함한 세 보유 항목을 `사내 규정에 따름`으로 표시했다.
- 목차는 기본 `smooth` scroll과 hash·focus·활성 탭을 동기화하고 모션 감소 환경에서는 즉시 이동한다.
- 관련 test `3 files / 92 tests`, 전체 Frontend test `27 files / 193 tests`, typecheck, lint, build와 diff check가 통과했다.
- 데스크톱·390px Browser 검수에서 footer 배치, 중복 진입점 0, 가로 overflow 0, 프로필 동의와 PWA 이용·푸시 안내를 확인했다.
- 최신 후보 Frontend를 `127.0.0.1:5180`, bounded synthetic API를 `127.0.0.1:5086`에서 유지한다.
- 사용자 적용 결정, commit·push·PR·merge와 운영 적용은 계속 미승인이다.
