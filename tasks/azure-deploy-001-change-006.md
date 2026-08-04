# TASK-AZURE-DEPLOY-001 Change 006 — Teams·PWA 브랜드 자산과 통합 main handover

## Task Identity Gate

- proposedTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `통합 source image·migration 0068 재배포 → Edge·DNS·TLS·provider 검수`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `false`
- gateStatus: `PASS_REUSE`

## 승인과 기준선

- changeApproved: `true`
- approvalSource: `USER_EXPLICIT_TEAMS_PWA_BRANDING_AND_AZURE_CONTINUATION`
- 승인일: 2026-08-04
- 기준 branch: `origin/main`
- 기준 SHA: `aac5f2766f05eb3175bead614545830f5e615ca4`
- 작업 branch: `fix/task-azure-deploy-001-brand-assets`
- 실제 provider 발송: `제외`
- public traffic 전환: `제외`

## Purpose identity

- 업무 목표: 사용자 제공 EMI PNG를 Teams 앱과 PWA 설치 표면의 공통 브랜드 자산으로 사용하고, 최신 통합 main을 Azure에 인계하기 전 배포 산출물을 완성한다.
- Root Finding: Teams package builder는 임시 사각형 도형을 아이콘으로 생성하고 있으며 Frontend에는 PWA web manifest와 설치 아이콘이 없다.
- 변경 경계: 사용자 제공 PNG의 canonical copy, Teams 192px color·32px outline 아이콘, PWA 192·512·maskable·Apple touch·favicon, web manifest, HTML 연결과 정적 검증을 포함한다.
- 보존할 불변조건: 기존 Teams app ID·권한·activity type·최종 hostname placeholder, Frontend 인증·route·API, Service Worker·오프라인 cache 제외, 실제 Teams·Gmail 발송과 public traffic 비활성.
- 예상 산출물: 동일 원본 기반 Teams package와 PWA install metadata, 이미지 규격·package·Frontend build 검증, Azure handover 기록.

## 승인된 수정 범위

1. 사용자 제공 PNG를 비식별 브랜드 원본으로 Repository에 보존한다.
2. Teams color icon은 192x192와 중앙 120x120 safe area, outline icon은 32x32 투명 배경·흰색 심볼 규격으로 생성한다.
3. Teams package builder가 임시 도형 대신 추적된 브랜드 아이콘을 그대로 패키징하게 한다.
4. PWA `manifest.webmanifest`와 192·512·maskable 512·Apple touch 180·favicon 32 아이콘을 추가한다.
5. `index.html`에 web manifest, theme color, favicon과 Apple touch icon을 연결한다.
6. Azure artifact validation에서 Teams·PWA 자산 규격과 연결을 검사한다.
7. 자동 검증 완료 뒤 최신 main image·migration `0068` Azure handover Gate로 이어간다.

## 제외 범위

- Service Worker, offline cache, background sync와 push notification
- 앱 내부 로고·로그인 화면·Graphite UI 재디자인
- Teams manifest ID·Entra identifier·권한·activity type 변경
- 실제 Teams 조직 catalog 게시·설치·activity 발송
- Gmail 실제 발송과 public traffic 전환
- DB migration 또는 Azure runtime mutation의 이번 브랜드 자산 구현 단계 실행

## 변경 Allowlist

- `assets/branding/emi-logo.png`
- `infrastructure/teams/assets/color.png`
- `infrastructure/teams/assets/outline.png`
- `infrastructure/teams/manifest.template.json`
- `frontend/public/manifest.webmanifest`
- `frontend/public/icons/*`
- `frontend/index.html`
- `scripts/build-teams-manifest-package.sh`
- `scripts/test-teams-manifest-package.sh`
- `scripts/test-pwa-assets.sh`
- `scripts/validate-azure-pilot-artifacts.sh`
- `tasks/azure-deploy-001-change-006.md`
- `tasks/azure-deploy-001-implementation-report.md`
- `tasks/azure-deploy-001-sop.md`
- `tasks/azure-deploy-001-user-validation-checklist.md`
- `docs/00-product-roadmap.md`

## 완료 기준

- Teams package가 `manifest.json`, 사용자 제공 원본 기반 `color.png`, `outline.png` 세 파일을 포함한다.
- Teams icon 규격 192x192·중앙 120x120 safe area·32x32와 outline 투명 배경 계약을 통과한다.
- PWA manifest가 name·short name·start URL·standalone display·theme/background color·any/maskable icon을 선언한다.
- Frontend build 산출물에 web manifest와 모든 icon이 포함된다.
- `pnpm lint`, `typecheck`, `test`, `build`, Teams package test, PWA asset test와 Azure artifact static validation이 통과한다.
- Service Worker와 offline cache 구현은 0건이다.
- 실제 identifier·hostname·email·secret과 실제 provider 발송·public traffic 변경은 0건이다.
