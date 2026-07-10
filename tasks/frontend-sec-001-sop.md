# TASK-FRONTEND-SEC-001 SOP

## 1. Dependency audit 실행 방법

최신 main 기반 clean worktree에서 실행한다.

```bash
corepack pnpm install --frozen-lockfile
corepack pnpm audit --json
```

Audit는 시간이 지나면 달라질 수 있으므로 게시 직전 다시 실행한다. `audit --fix --force`는 사용하지 않는다.

## 2. 결과 읽는 법

Critical/High/Moderate/Low별 개수, package, affected/patched version과 dependency path를 확인한다. Critical 또는 High가 남으면 commit, push와 PR을 중단한다. Non-blocking advisory도 exploit 조건과 후속 조치를 기록한다.

## 3. Vite/esbuild version 확인

```bash
corepack pnpm --dir frontend list vite esbuild vitest --depth Infinity
corepack pnpm --dir frontend why vite
corepack pnpm --dir frontend why esbuild
```

TASK-FRONTEND-SEC-001 기준은 Vite 7.3.6, esbuild 0.28.1, Vitest 4.1.0 각 1개 version이다.

## 4. 안전한 update 방법

1. Official advisory의 patched version 확인
2. 현재 major와 peer/Node engine 확인
3. Direct package만 exact version으로 update
4. Transitive security floor가 필요하면 실제 consumer의 공식 range 안인지 확인
5. 불필요한 package 일괄 update 금지

```bash
corepack pnpm --dir frontend add --save-exact vite@7.3.6
corepack pnpm --dir frontend add --save-dev --save-exact vitest@4.1.0 esbuild@0.28.1
```

새 advisory가 있으면 숫자를 그대로 복사하지 말고 실행 시점의 공식 기준으로 다시 결정한다.

## 5. Lockfile 검토

```bash
git diff -- frontend/package.json pnpm-lock.yaml
```

변경 package가 Vite, esbuild, Vitest 및 그 내부 package로 한정되는지 확인한다. React, Playwright, UI dependency 또는 script가 예상 밖으로 바뀌면 중단한다.

## 6. Frozen install

```bash
corepack pnpm install --frozen-lockfile
```

실패하면 lockfile과 package manifest가 불일치한다. lockfile을 손으로 맞추거나 force install하지 않는다.

## 7. Lint/typecheck/unit/build

```bash
corepack pnpm --filter emi-qms-frontend run lint
corepack pnpm --filter emi-qms-frontend run typecheck
corepack pnpm --filter emi-qms-frontend test
corepack pnpm --filter emi-qms-frontend run build
corepack pnpm --filter emi-qms-frontend run e2e:mock
```

기존 warning과 신규 warning을 구분한다.

## 8. HTTP/HTTPS dev server smoke

현재 UAT port 5081/5174를 사용하지 않는다. Backend 5091, HTTP 5184, HTTPS 5185처럼 비어 있는 별도 port를 사용한다. HTTPS certificate는 승인된 local path만 전달하고 내용을 출력하지 않는다.

확인 항목:

- Root, Teams Activity와 admin 200
- API/health proxy 200
- HMR client 정상
- strict port collision이 non-zero로 실패
- 반대 protocol을 성공으로 판정하지 않음
- 역할별 browser console error와 narrow overflow 0

`.env.notify-local`은 로드하지 않고 external provider를 disabled/dry-run으로 설정한다.

## 9. Synthetic security test

`/tmp` 아래에 canary가 있는 가짜 env/certificate/source-map fixture를 만든다. 실제 repository secret은 요청하지 않는다.

- Direct deny와 query 변형이 secret body를 반환하지 않는지 확인
- 허용 root 밖 file canary가 반환되지 않는지 확인
- SPA fallback 200은 status만으로 실패 처리하지 않고 canary/content를 확인
- HMR client 회귀 확인
- 종료 후 fixture/log 삭제

WebSocket이나 Windows 전용 exploit 재현이 불필요하게 위험하면 patched threshold와 일반 회귀로 대체하고 미실행 이유를 기록한다.

## 10. E2E 실행

```bash
corepack pnpm --filter emi-qms-frontend run e2e:full-stack
```

E2E는 전용 Compose project/PostgreSQL/network/tmpfs와 `emi_qms_e2e_*` DB만 사용한다. 종료 후 E2E container/network/volume residue가 0인지 확인한다.

## 11. UAT 보호 확인

E2E 전후 persistent UAT를 read-only로 비교한다.

- PostgreSQL container ID, health와 restart count
- schema migration count/latest
- 핵심 table와 delivery status count
- persistent volume 존재
- 5081/5174 listener PID와 HTTPS endpoint

UAT worker의 자연 변경이 있으면 E2E 원인으로 단정하지 않고 별도 분석한다. UAT를 멈추거나 data를 삭제하지 않는다.

## 12. Rollback 방법

- Merge 전: Task branch commit을 revert하거나 폐기
- Merge 후: dependency manifest와 lockfile을 같은 commit으로 revert
- DB rollback: N/A
- 취약 version 장기 복귀 금지, forward-fix 우선

## 13. Advisory 추가 발생 시 절차

1. `pnpm audit --json`으로 재현
2. GitHub Advisory/upstream release의 affected/patched version 확인
3. exploit 조건과 OS/CI/UAT 영향을 구분
4. 최소 compatible patch 계획 작성
5. 전체 회귀와 UAT 보호 검증
6. Roadmap/report 갱신

## 14. 금지사항

- `audit --fix --force`
- 근거 없는 major upgrade와 package 일괄 update
- 5081/5174 session 재사용·재시작
- UAT DB drop/truncate/reset 또는 volume 삭제
- `.env.notify-local`, 실제 secret/certificate 내용 이용
- Actual Teams/Mail/Channel 발송
- Migration/backend runtime/UI 기능 결합
- `git add .`, `git add -A`, force push

## 15. 사용자 검수 체크리스트

상태: `Checklist 작성됨`, `자동 검증 완료`, `사용자 검수 완료`.

- [x] Main, project, work와 admin 화면 정상
- [x] Teams Activity와 HTTPS 정상
- [x] API/User 카드 정상
- [x] 눈에 띄는 기능/style 회귀 없음
- [x] Audit High 0 결과 확인
- [x] 현재 5174는 patch 전 runtime임을 이해
- [x] Merge 후 controlled UAT handover 필요성 확인
- [x] Rollback과 금지사항 이해

## 16. 변경 이력

- 2026-07-10: Vite 7.3.6, esbuild 0.28.1, Vitest 4.1.0 보안 기준과 검증 절차 최초 작성
- 2026-07-10: HTTPS 5174/5185 비교 검수와 문서 검수 완료, PR #24 병합 승인 반영
