# TASK-TEAMS-PWA-001 Change 011 — 웹 로고·보안 안내·회사 정보 통일

## Task Identity Gate

- proposedTaskId: `TASK-TEAMS-PWA-001`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `운영 관찰·남은 표면 검수`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-TEAMS-PWA-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

## 기준선과 작업 격리

- branch: `fix/task-admin-003-user-departments`
- baseSha: `7c05175001d9e0beb23a161639c846f98e05dbb7`
- 사용자 지시: 사용자 검수 뒤 `TASK-ADMIN-003` 부서 변경과 이 Change를 한 번에 main 병합할 예정이다.
- 기존 canonical clone과 `5174/5081` runtime은 변경하지 않는다. 현재 task-owned 검수 runtime `5175/5082`만 사용한다.

## 승인된 변경 범위

1. 사용자가 제공한 `EMI_PMS_final_with_project_management_system.png`를 웹 내부 공통 로고로 사용한다.
2. 로그인 화면 왼쪽의 기존 EMI Electric Modular Innovation 로고는 유지한다.
3. 로그인·로그인 확인 화면의 텍스트 `EMI PMS` 제목을 새 제품 로고 이미지로 교체한다.
4. 로그인 화면 아래에 중립적인 정보 보안 안내를 추가한다.
5. 웹 공통 shell과 인증 화면 아래에 회사명 `(주) 이엠아이`와 오산·청주 캠퍼스 주소를 표시한다.
6. 기존 흑백 wireframe 구조와 로고 원본 색상 예외를 유지하고 Desktop·390px에서 검증한다.

## 변경 제외 범위

- `frontend/public/icons/*` PWA 아이콘
- `infrastructure/teams/assets/*` Teams 앱 아이콘
- Teams manifest, Teams launcher 로고·화면
- PWA manifest, 설치 정책·팝업·Web Push
- 로그인 인증, Microsoft 365, Backend·API·DB·migration·권한
- commit·push·PR·merge·Azure 공개배포

## 사용자 표시 문구

- 정보 보안 안내: `본 시스템은 EMI 임직원용 업무 시스템입니다. 계정 및 화면 정보를 외부에 공유하지 마시고, 공용 기기에서는 사용 후 반드시 로그아웃해 주세요.`
- 회사 정보: `(주) 이엠아이`
- 오산 주소: `경기도 오산시 세남로길 14-11 (세교동 63-1)`
- 청주캠퍼스: `이엠아이 청주캠퍼스 / 충북 청주시 청원구 오창읍 서오창산단3로 110`

## 검증 기준

- 새 웹 로고 source와 repository asset의 SHA-256 byte equality
- 로그인 왼쪽 기존 EMI logo source·PWA icon·Teams icon hash 변경 0
- 공통 desktop/sidebar·mobile app bar·drawer와 로그인 제품명 영역에 새 로고 표시
- 로그인 security 안내와 모든 웹 shell의 회사 정보 표시
- Desktop·390px page-level overflow 0, login control 접근 가능, footer가 업무 action을 가리지 않음
- Frontend lint·typecheck·unit·build, browser smoke, `git diff --check`
