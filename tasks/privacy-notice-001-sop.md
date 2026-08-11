# TASK-PRIVACY-NOTICE-001 SOP

## 목적과 현재 경계

개인정보·이용 안내 검수본의 문안 개정, 로컬 확인, 정정과 rollback 절차다. 현재 후보는 운영에 적용되지 않았으며 Backend·DB·migration·실제 알림 provider를 사용하지 않는다.

## 문안 개정 절차

1. 회사의 개인정보 책임 부서가 법인명, 처리 정보·목적·근거·보유 기준, 외부 서비스 계약 분류, 권리 행사 창구와 시행일을 확인한다.
2. 업무 연락 값은 제품 화면 source에서만 수정하고 Task 문서·test·검증 증빙에는 복사하지 않는다.
3. 현재 알림 채널과 미래 채널을 구분한다. 처리방침에는 현재 인앱·Teams Activity·메일만 쓰고, 미구현 모바일 푸시는 설치 안내에서 가능성과 현재 권한 불필요 상태만 설명한다.
4. `사내 규정에 따름`을 구체화하기로 결정하면 실제 규정명·기간·판단 기준을 승인받은 뒤 문안을 바꾼다.
5. 최신 공통 footer의 우측 하단 텍스트 진입점이 회사 정보와 같은 글꼴·크기이며 border·배경이 없는지 확인한다. 목차는 선택 marker 없이 smooth scroll·hash·focus·모션 감소 대체 동작을 유지하고, 개인정보 안내 상단과 시범 운영 안내 왼쪽의 굵은 강조선은 사용하지 않는다. sidebar·mobile top·mobile drawer logo는 홈으로 이동해야 하며 sidebar, mobile menu와 account panel에는 중복 개인정보 진입점을 만들지 않는다.
6. lint, typecheck, 관련·전체 test, build, desktop·390px 화면과 개인정보 안전 검사를 다시 수행한다.
7. 사용자가 문안·화면을 검수하고 별도로 적용과 Git 게시를 승인한 뒤에만 표준 release 절차로 진행한다.

## 게시 전 Gate

- Open P1/P2가 해소되거나 정책에 따른 위험 수용 기록이 있다.
- 담당 창구, 시행일과 처리 범위에 빈 값·placeholder가 없다.
- 기존 프로필 사진 보유 사용자가 있으면 재안내·재동의·삭제 중 처리 방식을 별도 승인했다.
- 실제 Microsoft 인증 뒤 route, PWA standalone과 Teams 표면을 확인했다.
- commit·push·PR·merge·운영 release 승인은 각각 현재 Task에 명시돼 있다.

## 정정과 Rollback

문안 오류가 운영에서 발견되면 새 데이터 mutation을 만들지 않고 이전 검증 Frontend revision으로 rollback하거나 정정 문안을 forward-fix한다. 기존 공지 기능으로 정정 사실과 확인 경로를 안내하고, 새 시행일·버전과 Git 이력을 남긴다. 연락처 원문이나 사용자 데이터를 Task 증빙에 복사하지 않는다.

## 현재 로컬 검수 환경 종료

사용자 검수가 끝날 때까지 Frontend 5180과 synthetic API 5086을 유지한다. 종료가 필요하면 해당 Task가 시작한 두 process만 중지하며 기존 5174와 다른 runtime은 건드리지 않는다. 임시 synthetic API는 실제 사내 데이터나 provider를 포함하지 않는다.
