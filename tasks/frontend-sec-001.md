# TASK-FRONTEND-SEC-001 Frontend dependency 보안 보정

## 1. 목적

Frontend 개발 도구 dependency의 공개 취약점을 재현하고 Vite 7 major를 유지하는 최소 호환 patch로 보정한다. 현재 persistent UAT와 외부 notification provider를 건드리지 않고 별도 port와 isolated E2E에서 회귀를 검증한다.

## 2. 발견 배경

`origin/main` 기준 initial audit에서 Vite `7.3.0`, esbuild `0.27.7`, Vitest `4.0.15`가 사용됐다. `pnpm audit --json`은 Critical 1, High 3, Moderate 2, Low 1을 보고했다. 현재 실행 중인 5174 HTTPS UAT는 이 취약 version을 계속 사용하므로 이 Task의 patched runtime 증빙으로 간주하지 않는다.

## 3. Advisory 목록

| Package | Advisory | Severity | 영향 version | Patched 기준 | 주요 조건 |
| --- | --- | --- | --- | --- | --- |
| Vite | `GHSA-v2wj-q39q-566r` | High | 7.1.0~7.3.1 | 7.3.2 | network에 노출된 dev server의 deny file query bypass |
| Vite | `GHSA-p9ff-h696-f583` | High | 7.0.0~7.3.1 | 7.3.2 | network에 노출된 dev server WebSocket file read |
| Vite | `GHSA-fx2h-pf6j-xcff` | High | 7.0.0~7.3.4 | 7.3.5 | Windows alternate path deny bypass |
| Vite | `GHSA-4w7w-66w2-5vf9` | Moderate | 7.0.0~7.3.1 | 7.3.2 | optimized dependency source map path traversal |
| Vite | `GHSA-v6wh-96g9-6wx3` | Moderate | 7.0.0~7.3.4 | 7.3.5 | Windows UNC path를 통한 NTLMv2 hash disclosure |
| Vitest | `GHSA-5xrq-8626-4rwp` | Critical | 4.0.0~4.0.x | 4.1.0 | network UI/API 또는 Windows Browser Mode의 file read/write/exec |
| esbuild | `GHSA-g7r4-m6w7-qqqr` | Low | 0.27.3~0.28.0 | 0.28.1 | Windows development server path traversal |

## 4. 영향 범위

- Vite dev server와 HMR, HTTP/HTTPS startup, certificate path loading, API/health proxy
- Vite를 peer 또는 transitive dependency로 사용하는 React plugin과 Vitest
- esbuild 기반 transform과 production build
- Frontend unit, mock UI와 Full-Stack E2E
- Windows 전용 exploit 조건은 macOS에서 재현 완료로 표시하지 않고 patched threshold와 회귀로 확인

## 5. 변경 전 version

- Node.js: `24.18.0`
- pnpm: `11.8.0`
- Vite: `7.3.0`
- esbuild: `0.27.7`, Vite 단일 transitive path
- Vitest: `4.0.15`
- `@vitejs/plugin-react`: `5.1.2`
- Playwright: `1.57.0`

## 6. 목표 patched version

- Vite: `7.3.6`
- esbuild: `0.28.1`
- Vitest: `4.1.0`

Vite `7.3.5`는 Vite advisory를 해결하지만 dependency manifest가 esbuild `^0.27.0`만 허용한다. Vite `7.3.6`은 동일 major의 patch이며 공식 dependency range가 `^0.27.0 || ^0.28.0`으로 확장돼 esbuild `0.28.1`을 override 없이 선택할 수 있다. Vitest는 Critical advisory의 최소 patched `4.1.0`을 선택한다.

## 7. 포함 범위

- `frontend/package.json`의 Vite와 Vitest exact patch update
- `pnpm-lock.yaml`의 관련 dependency graph 갱신
- initial/final audit와 dependency graph 비교
- synthetic deny/path regression
- 별도 5184/5185 HTTP/HTTPS Vite 및 proxy smoke
- Frontend, backend, migration, mock UI와 isolated Full-Stack E2E 회귀
- persistent UAT read-only 보존 확인
- Task 종료 5종 산출물

## 8. 제외 범위

- Vite 8 major upgrade
- React, UI 또는 runtime 기능 변경
- backend package/runtime, migration과 DB 변경
- 현재 5081/5174 UAT restart 또는 patched runtime handover
- `.env.notify-local` loading과 실제 외부 notification 발송
- 실제 repository secret/certificate 내용 대상 exploit probe

## 9. Dependency update 전략

1. Vite `7.3.0 → 7.3.6` exact patch update
2. Vitest `4.0.15 → 4.1.0` exact patch update
3. Frontend dev dependency에 esbuild `0.28.1` security floor를 명시해 Vite의 공식 range 안에서 단일 transitive resolution 사용
4. Vite가 취약 0.27 line 지원을 제거해 resolver가 안전 version만 선택하면 direct security floor 제거
5. 다른 direct dependency는 변경하지 않음
6. frozen install과 lockfile diff로 무관한 churn을 차단

Peer/engine 확인 결과 Vite 7.3.6, Vitest 4.1.0, React plugin 5.1.2 모두 Node 24와 Vite 7을 허용한다. 일반 transitive update는 기존 lock의 esbuild 0.27.7을 유지했고 pnpm 11은 root `package.json`의 legacy override 설정을 무시한다. Allowlist 밖 workspace 설정 대신 Vite 7.3.6이 공식 허용하는 `^0.28.0` line의 최소 patched version을 frontend dev dependency로 명시한다.

## 10. Audit 기준

- Critical 0, High 0 필수
- 취약 Vite/esbuild/Vitest version lockfile 잔존 0
- 목표는 Moderate/Low 포함 전체 audit 0
- 남은 advisory가 있으면 package, path, exploit 조건, patch와 risk owner 결정을 기록

## 11. Synthetic security regression

`/tmp` 아래 별도 fixture와 canary file만 사용한다. Direct deny, query 변형, 허용 root 밖 path traversal을 patched Vite에서 검사하고 실제 repository `.env`·certificate·private key는 읽지 않는다. WebSocket과 Windows 전용 exploit code는 실행하지 않고 patched version threshold 및 HMR/dev server 회귀로 대체한다.

## 12. HTTP/HTTPS 회귀

- 현재 5081/5174는 사용하지 않는다.
- HTTP frontend는 5184, HTTPS frontend는 5185를 사용한다.
- Backend/API proxy가 필요하면 isolated E2E 또는 5091 별도 listener를 사용한다.
- External provider는 disabled/dry-run이며 `.env.notify-local`을 로드하지 않는다.

## 13. Persistent UAT 보호

작업 전후 원래 worktree branch/HEAD/WIP checksum, 5081/5174 listener PID, screen session, PostgreSQL container ID/health/restart count와 persistent volume을 비교한다. 현재 5174는 보존 확인용이며 patched dependency 검증 증빙이 아니다.

## 14. Rollback

Merge 전에는 Task commit을 revert하거나 branch를 폐기한다. Merge 후 dependency 회귀가 확인되면 해당 squash commit을 revert해 `frontend/package.json`과 `pnpm-lock.yaml`을 함께 복원한다. DB/migration 변경이 없어 DB rollback은 적용 대상이 아니다. 기존 취약 version으로 장기간 복귀하지 않고 forward-fix를 우선한다.

## 15. 남은 risk

- 현재 5174 runtime은 controlled handover 전까지 patched runtime이 아니다.
- Windows 전용 advisory는 macOS에서 exploit 재현하지 않는다.
- Dependency registry와 advisory 상태는 시간에 따라 변하므로 게시 직전 audit을 다시 실행한다.

## 16. 후속 Task

1. Patched frontend UAT runtime controlled handover
2. `TASK-UAT-002`
3. `UAT-VERIFY-001`
4. `TASK-NOTIFY-REL-001`
5. `TASK-NOTIFY-ESC-001`
6. `TASK-AUTH-HARDEN-001`
7. `TASK-GOV-002`

## 17. 검증 및 5종 산출물 상태

검증 결과:

- Final audit: Critical 0 / High 0 / Moderate 0 / Low 0
- Dependency graph: Vite 7.3.6 / esbuild 0.28.1 / Vitest 4.1.0 각 1개 version
- Frontend: lint 0 errors, typecheck, unit 57/57, build, mock UI 1/1 통과
- Backend: Release build warning/error 0, 전체 295/295, Migration 16/16 통과
- Synthetic fixture: deny/query/outside-root 7개 차단, canary 노출 0, HMR client 정상
- Alternate HTTP 5184와 HTTPS 5185: root/Teams/admin/API/health, strict port와 protocol mismatch 통과
- Browser: 역할별 3 routes, console error 0, non-aborted failure 0, 390px overflow 0
- Full-Stack E2E: isolated PostgreSQL에서 16/16 통과, cleanup residue 0
- Persistent UAT: E2E 전후 schema/table/delivery counts, container ID와 restart count 동일
- Actual external provider call: 0

- Implementation report: [TASK-FRONTEND-SEC-001 Implementation Report](frontend-sec-001-implementation-report.md), 작성 완료
- SOP: [TASK-FRONTEND-SEC-001 SOP](frontend-sec-001-sop.md), 작성 완료
- User manual: [TASK-FRONTEND-SEC-001 User Manual](frontend-sec-001-user-manual.md), 작성 완료
- Roadmap update: [Product Roadmap TASK-FRONTEND-SEC-001](../docs/00-product-roadmap.md#task-frontend-sec-001-frontend-dependency-security-remediation), 작성 완료
- User validation checklist: 이 문서 18장, Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 대기

## 18. 사용자 검수 체크리스트

상태: `Checklist 작성됨`, `자동 검증 완료`, `사용자 검수 대기`.

- [ ] 메인 화면이 기존과 동일하게 열림
- [ ] 프로젝트·업무·관리자 화면이 열림
- [ ] Teams Activity 화면이 열림
- [ ] HTTPS 접속이 정상
- [ ] API/User 카드 정상
- [ ] 화면 동작과 style의 눈에 띄는 회귀 없음
- [ ] SOP가 실행 가능함
- [ ] User manual이 이해 가능함
- [ ] `pnpm audit` High 0 확인
- [ ] 실제 UAT 반영은 별도 controlled handover가 필요함을 이해함

자동 검증 결과와 사용자 직접 검수는 별도 상태로 관리하고, 체크박스를 임의로 완료 처리하지 않는다.
