# TASK-FRONTEND-SEC-001 Implementation Report

## 1. 목적

Frontend 개발 도구의 공개 취약점을 최소 호환 patch로 제거하고 HTTP/HTTPS Development server, build, test와 Full-Stack E2E의 회귀가 없음을 확인한다.

## 2. 배경

TASK-UAT-001 완료 후 현재 5174 HTTPS UAT가 사용하는 Vite 7.3.0에 공개된 file read/deny bypass advisory가 확인됐다. 최신 main 기반 독립 worktree에서 조사했으며 원래 UAT worktree와 5081/5174 session은 변경하지 않았다.

## 3. 취약점과 exploit 조건

Initial `pnpm audit --json` 결과는 Critical 1, High 3, Moderate 2, Low 1이었다.

- Vite High 3: network에 노출한 dev server에서 deny query, WebSocket fetch와 Windows alternate path를 통한 file read 가능성
- Vite Moderate 2: optimized source map traversal과 Windows UNC path를 통한 credential hash disclosure 가능성
- Vitest Critical 1: network UI/API 또는 Windows Browser Mode 노출 시 file read/write/exec 가능성
- esbuild Low 1: Windows development server의 path normalization 우회 가능성

Repository UAT는 `--host`를 사용하므로 Vite의 network exposure 조건과 관련된다. Windows 전용 조건은 macOS에서 exploit 성공으로 표시하지 않았다.

## 4. 변경 전 dependency graph

- Node.js 24.18.0, pnpm 11.8.0
- Vite 7.3.0: direct dependency이자 React plugin/Vitest의 단일 peer path
- esbuild 0.27.7: Vite 하위 단일 transitive version
- Vitest 4.0.15: direct dev dependency
- React Vite plugin 5.1.2와 Playwright 1.57.0은 변경하지 않음

## 5. 검토한 대안

- Vite 8 major: 범위가 크고 필요하지 않아 제외
- Vite 7.3.5: Vite advisory는 해결하지만 esbuild range가 `^0.27.0`이라 0.28.1과 직접 호환되지 않아 제외
- Vite 7.3.6: 동일 major patch이며 esbuild `^0.27.0 || ^0.28.0`을 공식 허용해 선택
- pnpm transitive update: 기존 lock의 0.27.7을 유지해 효과 없음
- root `package.json` legacy override: pnpm 11이 무시하고 경고하므로 즉시 폐기
- `pnpm-workspace.yaml` override: allowlist 확대 대신 frontend direct security floor를 선택

## 6. 최종 선택 version

- Vite 7.3.6
- esbuild 0.28.1
- Vitest 4.1.0

Vite 7 major와 exact version style을 유지했다. Node/peer engine은 현재 Node 24와 호환된다.

## 7. Package/lockfile 변경

- `frontend/package.json`: Vite와 Vitest exact patch update, esbuild 0.28.1 dev security floor 추가
- `pnpm-lock.yaml`: Vite, Vitest 내부 packages, esbuild platform packages와 관련 transitive graph만 갱신
- `package.json`: 최종 변경 없음

무관한 direct dependency, script와 package formatting은 변경하지 않았다. Frozen install이 통과했다.

## 8. esbuild 처리

Vite 7.3.6은 esbuild 0.28 line을 허용하지만 기존 lock은 0.27.7을 유지했다. Frontend에 0.28.1 exact dev dependency를 두어 Vite도 같은 단일 version을 사용하도록 dedupe했다. Vite가 취약 0.27 line을 제거하면 이 direct floor를 삭제한다.

## 9. Audit 전후

| 단계 | Critical | High | Moderate | Low | 합계 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 변경 전 | 1 | 3 | 2 | 1 | 7 |
| 변경 후 | 0 | 0 | 0 | 0 | 0 |

취약 Vite 7.3.0, esbuild 0.27.7과 Vitest 4.0.15 lock entry는 0건이다.

## 10. Synthetic regression

`/tmp` synthetic fixture에 가짜 env/certificate canary와 허용 root 밖 source map을 생성했다. Direct deny, query 변형과 outside-root 7개 case는 non-200으로 차단됐고 한 traversal-shaped SPA fallback은 index만 반환했으며 canary는 포함하지 않았다. HMR client는 200이었다. Fixture와 log는 종료 후 삭제했다.

WebSocket advisory exploit과 Windows 전용 alternate path/UNC/esbuild exploit은 실행하지 않았다. 실제 secret을 다루는 위험을 피하고 official patched threshold, HMR, HTTP deny와 전체 회귀로 대체했다.

## 11. HTTP/HTTPS 회귀

- Backend 5091, HTTP Vite 5184, HTTPS Vite 5185 사용
- HTTP/HTTPS root, Teams Activity, admin, Vite client, health와 API proxy 200
- 5184 occupied 상태에서 두 번째 Vite가 non-zero로 실패하고 5186 fallback listener는 생성되지 않음
- HTTP를 HTTPS로, HTTPS를 HTTP로 오판하지 않음
- HTTPS는 기존 trusted localhost certificate의 경로만 사용했고 내용을 출력하지 않음
- 역할별 3 route browser smoke: console error 0, non-aborted request failure 0, 390px overflow 0

## 12. Frontend 전체 테스트

- `git diff --check`, actionlint, frozen install 통과
- Lint: error 0, 기존 Fast Refresh warning 1
- Typecheck: 통과
- Unit: 57/57
- Production build: 통과, 기존 500 kB chunk warning 유지
- Mock UI: 1/1

## 13. Backend/E2E 회귀

- Backend Release build: warning 0, error 0
- Backend 전체: 295/295
- Migration targeted: 16/16
- Full-Stack E2E: 16/16
- E2E container/network/volume residue: 0
- E2E external provider: disabled/dry-run, actual call 0

## 14. Persistent UAT 보호

Corrected snapshot 기준 E2E 전후 다음 값이 동일했다.

- Migrations 28, latest `0027_notification_access_scope_and_manual_work_items`
- Projects 22, work items 37, notifications 89, recipients 163, deliveries 92
- Escalations 2, users 14, departments 12, holidays 6
- Deliveries: Disabled 1, DryRunSent 6, Failed 20, Sent 59, Suppressed 6
- PostgreSQL container ID 동일, healthy, restart count 0, persistent volume 유지

첫 snapshot helper는 `docker exec -i`가 빠져 빈 결과를 비교했다. 이를 성공 근거에서 제외하고 stdin 전달을 보정한 뒤 Full-Stack E2E를 재실행해 실제 값을 검증했다.

## 15. 보안/secret

- `.env.notify-local`을 로드하지 않음
- 실제 credential, token, webhook, certificate/private key 내용 출력 또는 commit 없음
- Synthetic fixture만 사용하고 종료 후 삭제
- 실제 TeamsActivity, TeamsChannel, Mail 호출 0
- Secret/PII scan은 게시 전 최종 실행

## 16. Rollback

`frontend/package.json`과 `pnpm-lock.yaml`을 같은 commit 단위로 revert한다. DB/migration rollback은 N/A다. 기존 취약 version 복귀가 장기화되지 않도록 regression은 forward-fix를 우선한다.

## 17. 제한사항

- 현재 실행 중인 5174는 Vite 7.3.0 runtime이며 patched 증빙이 아니다.
- PR merge 후 `TASK-UAT-HANDOVER-001`에서 controlled restart와 patched runtime 확인이 필요하다.
- Windows 전용 exploit은 macOS에서 미실행했다.
- 실제 external provider smoke는 범위 밖이다.
- Excel/PDF/첨부, API contract, 권한, workflow와 migration 영향은 N/A다. Dependency 개발 도구만 변경했다.

## 18. 후속 Task

1. `TASK-UAT-HANDOVER-001`
2. `TASK-UAT-002`
3. `UAT-VERIFY-001`
4. `TASK-NOTIFY-REL-001`
5. `TASK-NOTIFY-ESC-001`
6. `TASK-AUTH-HARDEN-001`
7. `TASK-GOV-002`

전체 신규 기능 No-Go는 유지한다.

## 19. 해결한 업무 문제

개발 서버가 local file과 개발자 credential을 노출할 수 있는 알려진 위험을 제거하면서 기존 HTTPS Teams 검수와 빌드·테스트 흐름을 유지했다.

## 20. 기술적 결정과 검토한 대안

Vite 8 전면 upgrade 대신 7.3.6을 선택했다. 이 patch가 Vite advisory와 esbuild 0.28 공식 range를 함께 만족해 major migration 없이 보안 floor를 닫는다. Vitest Critical은 최소 patched 4.1.0으로 별도 보정했다.

## 21. 시행착오 및 폐기한 접근

- Transitive update는 기존 esbuild lock을 유지해 폐기
- pnpm 11이 무시하는 root legacy override를 즉시 제거
- Synthetic traversal-shaped URL의 SPA fallback 200을 file exposure로 잘못 판정한 초기 assertion을 canary/content 기준으로 교정
- Alternate browser smoke에서 proxy base URL 미설정으로 발생한 connection refused를 dependency 오류와 분리
- Admin route를 일반 사용자로 열어 발생한 권한 오류를 역할별 독립 page 검수로 교정
- 첫 UAT snapshot의 stdin 전달 누락을 발견하고 E2E를 재실행

실패를 재실행으로 숨기지 않고 각 원인과 대체 검증을 기록했다.

## 22. 사용자 검수 결과와 남은 항목

- Checklist 작성됨
- 자동 검증 완료
- 사용자 검수 완료
- 검수 증빙: 2026-07-10, HTTPS Development UAT 비교 환경(기존 5174 / patched Preview 5185), 역할 기반 화면·SOP·User manual 확인, 결과 승인

사용자는 화면 회귀, SOP/User manual 이해도와 audit 전체 0을 확인하고 PR #24 병합을 승인했다. 현재 5174는 patch 전이므로 merge 후에도 `TASK-UAT-HANDOVER-001` 완료 전에는 patched UAT로 표현하지 않는다.

5종 산출물:

- Implementation report: 이 문서, 작성 완료
- SOP: [TASK-FRONTEND-SEC-001 SOP](frontend-sec-001-sop.md), 작성 완료
- User manual: [TASK-FRONTEND-SEC-001 User Manual](frontend-sec-001-user-manual.md), 작성 완료
- Roadmap update: [Product Roadmap](../docs/00-product-roadmap.md#task-frontend-sec-001-frontend-dependency-security-remediation), 작성 완료
- User validation checklist: [Task 정의 18장](frontend-sec-001.md#18-사용자-검수-체크리스트), Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료
