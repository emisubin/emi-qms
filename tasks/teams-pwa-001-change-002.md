# TASK-TEAMS-PWA-001 Change 002 — 설치·Teams 안내 Graphite 재디자인

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- planningApproved: `true`
- implementationApproved: `true`
- approvalSource: `USER_EXPLICIT_DESIGN_CHANGE`
- approvalDate: `2026-08-09`
- canonicalTaskId: `TASK-TEAMS-PWA-001`
- branch: `feat/task-teams-pwa-001-experience`
- baseSha: `914a109e170f4e1c3ce34fb1faa4216c1b4fcf1c`
- roadmapSequenceMatch: `true`

## 사용자 요청과 확인된 원인

- 기존 Teams launcher의 넓은 빨간 왼쪽 면과 안내 상자의 빨간 왼쪽 강조선이 현재 Graphite 규격과 충돌한다.
- 기존 PWA 안내의 빨간 텍스트 로고, 빨간 그림자와 빨간 왼쪽 강조선이 제품 전체의 흑백 wireframe보다 독립적인 AI 도구형 화면처럼 보인다.
- 사용자는 Teams 안내, iPhone 설치 안내와 Android 설치 안내를 현재 EMI PMS 디자인 규격으로 다시 만들도록 명시했다.

## 구현 계약

1. 흰 표면, 검정 본문·주요 버튼, 중성 회색 보조정보와 1px 경계를 사용한다.
2. 장식용 왼쪽 강조선, 넓은 색상 면, 색상 그림자와 장식 gradient를 제거한다.
3. Teams launcher는 단일 wireframe surface와 header/body/action 구조로 정리한다.
4. PWA 안내는 동일한 modal shell 안에서 iPhone과 Android 제목·절차만 구분한다.
5. 기존 승인대로 실제 EMI 빨간 로고 이미지는 브랜드 자산 예외로 유지한다.
6. Teams deep link, 새 창 실행, PWA 설치 event, iPhone 수동 설치, dismissal·standalone·embedded 조건과 접근성 계약은 변경하지 않는다.

## 포함 범위

- `frontend/public/teams-launcher.html`
- `frontend/src/PwaInstallExperience.tsx`
- `frontend/src/styles.css`
- 관련 Frontend unit·mock browser test
- Task change·implementation report·사용자 검수 checklist

## 제외 범위

- Backend, DB, migration, API, 권한, 알림 event·수신자·발송 시점
- Azure·Entra·Teams Admin Center·실제 catalog와 운영 배포
- PWA Service Worker·Web Push
- EMI logo·Teams icon 자산 교체

## 검증 계약

- Teams launcher desktop·390px 구조, 가로 overflow 0과 console error 0
- iPhone·Android 390px 안내 제목·절차·버튼, 가로 overflow 0과 console error 0
- PWA focused unit, Frontend lint·typecheck·unit·build
- Teams manifest/PWA asset script와 `git diff --check`
