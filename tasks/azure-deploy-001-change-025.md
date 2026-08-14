# TASK-AZURE-DEPLOY-001 Change 025

## 상태

- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true`
- 상태: 사용자 검수·게시 승인 완료 — Ready PR 필수 CI 대기
- 구현 기준선: `origin/main` `a5f13cf64bc09d0840e11ea6be0e5a507f185d0c`
- 사용자 승인: 2026-08-14 원격 `main` 병합과 Azure 공개배포 명시 승인
- mainMergeApprovalCount: `1`
- productionDeploymentApproved: `true`

## 게시 대상

- `TASK-ADMIN-003 Change 002`
  - 품질 부서장에게 IQC·LQC·OQC·구매품별 IQC와 LQC 운영 변경 권한 부여
  - 생산관리 부서장에게 생산계획·실적 연결과 Item별 제조 양식 권한 부여
  - 제조 부서장·일반 품질 사용자·기타 부서장의 양식 관리 화면과 mutation 차단
  - additive migration `0078_department_head_form_template_scope`
- `TASK-PWA-PUSH-001 Change 002`
  - Azure 추적 배포 정의에 현재 운영 Web Push 활성·실발송 상태 반영
  - VAPID Key Vault 참조와 Backend identity의 secret resource 단위 권한 보존
  - 실제 운영 secret 원문·사용자별 구독·알림 업무 정책은 변경하지 않음

## 제외 범위

- 사용자가 미완성으로 보존하도록 지시한 공통 매뉴얼 작업과 그 worktree
- 신규 알림 정책, VAPID key 회전, 직원별 PWA 강제 설치·구독 관리
- Azure resource 용량·가격·도메인·Teams package 변경
- 기존 프로젝트 양식 snapshot과 확정 검사 이력의 소급 변경

## 검증과 게시 순서

1. 통합 branch에서 권한·migration 집중 Backend 회귀, Frontend 양식 관리 회귀·typecheck·lint·build, Azure Bicep/ARM 검증을 통과한다.
2. Ready PR의 변경 인지형 필수 CI와 `CI Gate`를 통과한다.
3. 승인된 PR을 `main`에 병합하고 exact 40자 SHA를 확인한다.
4. `Azure Pilot Release (Manual)`을 exact `main` SHA로 실행한다.
5. migration `0078` 성공 뒤 Backend와 Frontend의 새 revision을 적용한다.
6. 두 앱 readiness, 공개 health, 익명 root·API 차단과 Web Push 설정·secret reference 보존을 privacy-safe projection으로 확인한다.
7. Open P0/P1/P2가 `0/0/0`일 때만 운영 게시 완료로 기록한다.

## 통합 전 자동 검증

| 검증 | 결과 |
| --- | --- |
| Backend 권한·migration·공개배포 집중 회귀 | `47/47 PASS` |
| Frontend 양식 관리 연관 단위 회귀 | `217/217 PASS` |
| Frontend typecheck·lint·production build | `PASS` — 오류 0, 기존 warning만 유지 |
| Azure Bicep compile·portal template·static validation | `PASS/PASS/PASS` |
| tracked conflict marker·diff whitespace | `0`, `PASS` |

## Finding

- `PUBLICATION-GITHUB-AUTH-PROJECTION-001` / P2 / `RESOLVED`: 게시 도구 사전 확인에서 원격 인증 상태의 계정 metadata가 현재 로컬 terminal 출력에 포함됐다. tracked 파일·Git diff·원격 PR에는 기록되지 않았고 credential 값 노출은 없었다. 이후 인증 확인은 성공 여부만 반환하는 privacy-safe projection으로 제한하며 실제 계정 식별정보를 산출물에 복사하지 않는다.
- Open P0/P1/P2: `0/0/0`.

## 완료 기록

PR·exact `main` SHA·CI run·Azure release·migration·revision과 공개 검증 결과는 실행 후 이 문서와 implementation report·Roadmap에 동기화한다.
