# TASK-PRIVACY-NOTICE-001 Change 002

## 승인 기록

- approvalSource: `USER_EXPLICIT_IMPLEMENT_AND_OPEN_VALIDATION_SERVER`
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

## 구현 기준

- Source of truth: `privacy-notice-001-planning.md`, `privacy-notice-001-review.md`, `privacy-notice-001-change-001.md`
- 업무 관련 파일·기록의 보존 문구는 사용자 승인에 따라 이번 검수본에서 `사내 규정에 따름`으로 표시한다.
- 프로필 사진은 업로드·변경 직전에 선택 동의를 받고, 거부해도 기본 이니셜로 모든 업무 기능을 이용할 수 있다.
- 사진 삭제는 동의 없이 허용하며, 동의 상태를 별도로 저장하거나 DB·migration을 추가하지 않는다.
- PWA 설치 안내는 현재 인앱·Teams Activity·메일 알림을 설명하고, 모바일 푸시는 준비 중이며 현재 알림 권한이 필요하지 않다고 명시한다.
- 회사 연락처의 실제 값은 제품 화면 source에만 두고 Task 문서·테스트 출력·검증 증빙에는 기록하지 않는다.

## 변경 허용 범위

- 개인정보·이용 안내 정적 화면과 인증 후 route
- Desktop sidebar·mobile footer·계정 메뉴 진입점
- 프로필 사진 선택 동의 dialog와 반응형 bottom sheet 표현
- 기존 PWA 설치 popup의 설명·설치 방법·이용 안내 문구
- 관련 Frontend test와 style
- Task implementation report·사용자 검수 checklist·Roadmap 상태 동기화
- 기존 5174와 분리된 로컬 Frontend 사용자 검수 runtime

## 제외 범위

- Backend·API·DB·migration·기존 프로필 사진 데이터 조회·변경
- Web Push·Service Worker·새 알림 채널·알림 권한 요청
- 실제 Teams·메일 provider 호출
- Persistent UAT·Azure·운영 배포·공개 traffic 변경
- 기존 runtime 재시작 또는 종료
- commit·push·PR·merge·branch/worktree 정리

## 검수 후 결정

이번 결과는 로컬 사용자 검수 후보이며 운영 적용 결정이 아니다. 사용자가 화면과 문구를 확인한 뒤 적용·수정·폐기를 별도로 결정한다.
