# TASK-UAT-001 Change 005 — 공개 배포 P2 캐시·헤더·공급망 보정

## 1. Task Identity Gate

- proposedTaskId: `TASK-UAT-001 Change 005`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `운영 전환 Scope Review`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UAT-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

## 2. Purpose identity

- 업무 목표: Change 004 공개 배포 방어선을 다시 점검해 확인한 P2 캐시·보안 헤더 상속·공급망 재현성 문제를 모두 닫는다.
- Root Finding:
  - `SEC-PUBLIC-011` P2: 인증된 API 응답에 전역 `private, no-store`가 없어 브라우저 또는 공유 단말 캐시에 업무 응답이 남을 수 있다.
  - `SEC-PUBLIC-012` P2: Nginx `/assets/` location의 별도 `add_header`가 server-level 보안 헤더 상속을 끊어 정적 자산 응답의 방어 헤더가 누락된다.
  - `SEC-PUBLIC-013` P2: Production base/scanner image, CI service image와 GitHub Action이 가변 tag를 사용해 재빌드·재실행 시 검증하지 않은 외부 artifact로 바뀔 수 있다.
- 변경·검증 경계: Backend 응답 middleware와 회귀, Production Nginx cache/header 정책, Production·CI 외부 artifact digest/commit 고정, 운영 문서와 전체 자동 회귀를 포함한다.
- 보존할 불변조건: Entra 로그인·권한·업무 API response body, DB schema/data, upload·알림 흐름, 실제 provider, Persistent UAT와 운영 handover 상태를 보존한다.
- 예상 산출물: 전역 민감 응답 cache 차단, 정적 shell/assets별 cache 정책과 보안 헤더 보존, immutable 외부 artifact reference, 자동 정책 회귀, Implementation report·SOP·User manual·Roadmap·검수 checklist 갱신.

## 3. 검색 범위

- [x] `tasks/`의 TASK-UAT-001 change·implementation report·SOP·User manual
- [x] Product Roadmap 실행 큐·TASK-UAT-001 추적 항목·Decision Log
- [x] Local/remote TASK-UAT branch와 linked worktree
- [x] Open/merged UAT·security 관련 PR

## 4. 사용자 승인과 제외 범위

- 사용자는 2026-07-29에 남은 P2 전체 해결을 명시했다. 이 지시를 운영 전환 Scope Review 전 P2 재점검의 명시적 Roadmap override와 local experiment 구현 승인으로 기록한다.
- 실제 운영 domain·certificate·Entra registration·managed DB·SIEM·메일·Teams provider는 변경하거나 호출하지 않는다.
- DB migration·seed·reset, Persistent UAT runtime 재기동, commit, push, PR과 merge는 포함하지 않는다.
- Fable은 신규 기능이 아닌 기존 보안 정책 보정이므로 적용하지 않는다.

## 5. 보안 계약

- Backend의 모든 API·health 응답은 `Cache-Control: private, no-store, max-age=0`, `Pragma: no-cache`와 만료 지시를 가져야 한다.
- Frontend HTML shell은 재검증 없이 장기 보관하지 않고, fingerprint가 있는 `/assets/`만 장기 cache한다.
- `/assets/` 응답도 HSTS, CSP, nosniff, referrer, permissions와 cross-origin 보안 헤더를 잃지 않는다.
- Production Dockerfile과 Compose의 외부 image는 human-readable tag와 검증한 multi-platform digest를 함께 고정한다.
- CI GitHub Action은 full commit SHA, CI PostgreSQL service는 digest를 고정하고 checkout credential을 저장하지 않는다.
- 외부 artifact digest/commit 변경은 의존성 갱신으로 취급해 build·test·audit·image scan을 다시 통과해야 한다.

## 6. 완료 기준

- Backend 합성 응답에서 민감 cache 차단 헤더를 자동 검증한다.
- Production Nginx 합성 TLS runtime에서 HTML은 재검증 cache, asset은 장기 cache이며 두 응답 모두 필수 보안 헤더를 가진다.
- Repository 회귀가 Production/CI 외부 reference의 digest·full SHA 고정을 검사한다.
- Backend 전체, Frontend 전체, mock UI와 isolated Full-Stack E2E가 통과한다.
- Production Backend/Frontend image build, Compose validation, dependency audit와 container vulnerability scan이 통과한다.
- Open P0/P1/P2가 없다.

## 7. Rollback

- schema/data migration이 없으므로 cache middleware, Nginx 정책과 artifact reference만 이전 commit으로 되돌릴 수 있다.
- cache 차단이나 보안 헤더를 제거하는 방식은 운영 장애의 임시 우회로 사용하지 않는다.
- 고정 digest가 더 이상 배포 불가능하면 tag-only로 되돌리지 않고, 승인된 새 digest를 검증해 forward-fix한다.

## 8. 검수·게시 상태

- User validation checklist: `Checklist 작성됨`
- 자동 검증: `완료`
- 사용자 검수: `완료 — 2026-07-30`
- Commit·Push·PR: `완료 — PR #58`
- Merge: `사용자 승인 완료 — GitHub 실행 상태 기준`
- 실제 공개 배포: `NO_GO_EXTERNAL`
- Change 004까지의 P1 운영 hosting·security header·Host/forwarded proxy·rate limit·upload quarantine·Production Entra·backup/monitoring 방어선은 유지됐다.
- 이번 범위의 Open P0/P1/P2는 `0/0/0`이다. 고정 image의 upstream Low 2건은 수정 배포본이 없어 `SEC-PUBLIC-014` P3로 추적한다.
- 실제 domain·certificate·Entra registration·managed DB·restore·SIEM handover가 없으므로 공개 배포 전체는 계속 `NO_GO_EXTERNAL`이다.

## 9. 구현 결과

- Backend 전 응답에 `private, no-store, max-age=0`, `Pragma: no-cache`, 즉시 만료 지시를 적용했다.
- Nginx HTML shell은 `no-cache`, fingerprint asset은 1년 cache를 사용하면서 server-level 보안 header를 모두 상속한다.
- Production Backend·Frontend·ClamAV·TLS validator image와 CI PostgreSQL을 tag+digest로 고정했다.
- GitHub Action을 full commit SHA로 고정하고 workflow 기본 권한을 `contents: read`로 제한했으며 checkout credential 저장을 해제했다.
- Frontend image의 가변 package 설치를 제거하고 인증서 검증을 고정된 one-shot TLS validator로 분리했다.
- Production artifact immutable reference와 Nginx cache/header 구조를 자동 회귀로 고정했다.

## 10. 자동 검증 결과

| 검사 | 결과 |
| --- | --- |
| 공개 배포 보안 targeted | 27/27 통과 |
| Backend isolated PostgreSQL 전체 | 461/461 통과 |
| Frontend lint·typecheck·unit·build | error 0·기존 warning 1, 통과, 143/143, 통과 |
| Mock UI·Full-Stack E2E | 4/4, 55/55 통과 |
| Frontend audit·NuGet vulnerable package | 전 심각도 0, 0건 |
| Production Compose·Actionlint | 통과 |
| Backend·Frontend Production image build | 통과 |
| Backend final image scan | Critical/High/Medium/Low `0/0/0/0` |
| Frontend final image scan | Critical/High/Medium/Low/Unspecified `0/0/0/2/2`; Unspecified 2건은 영향 binary 부재 |
| ClamAV pinned image scan | Critical/High/Medium/Low/Unspecified `0/0/0/2/2`; Unspecified 2건은 영향 binary 부재 |
| TLS validator image scan·실행 | 전 심각도 0, 만료·hostname·key 일치 검증 통과 |
| 합성 Backend runtime | `/health/live` 200, cache·보안 header 통과 |
| 합성 Nginx TLS runtime | HTML·asset 200, cache 분리와 보안 header 통과 |

첫 Backend 전체 검사 시 기존 Persistent PostgreSQL이 중지돼 DB 의존 테스트가 연결 실패했다. Persistent 자원을 재시작하지 않고 Task 전용 격리 PostgreSQL로 다시 실행해 461/461을 확정했으며 전용 container·network는 제거했다.

## 11. Finding closure

| Finding | 심각도 | 상태 | 해소 |
| --- | --- | --- | --- |
| `SEC-PUBLIC-011` | P2 | `RESOLVED` | Backend 전역 private no-store cache 정책과 회귀 추가 |
| `SEC-PUBLIC-012` | P2 | `RESOLVED` | Nginx location-level header override 제거, HTML/asset cache 분리와 합성 TLS 검증 |
| `SEC-PUBLIC-013` | P2 | `RESOLVED` | Production/CI image digest·Action commit SHA 고정과 Repository 회귀 추가 |
| `SEC-PUBLIC-014` | P3 | `BACKLOG` | Frontend·ClamAV 기반 image의 libxml2 Low 2건은 2026-07-29 scanner 기준 수정 배포본이 없음. 운영 handover 및 digest 갱신 전에 재검사 |
| `SEC-PUBLIC-015` | P3 | `RESOLVED_NOT_AFFECTED` | `CVE-2026-11979`는 `xmlcatalog --shell`, `CVE-2026-58055`는 `nghttpx` 전용이며 두 실행 파일 모두 Frontend·ClamAV final image에 없음 |

## 12. 사용자 검수 체크리스트

- [ ] 로그인 후 주요 업무 화면 조회와 수정이 기존과 같은지 확인
- [ ] 로그아웃 뒤 브라우저 뒤로 가기에서 보호된 업무 내용이 다시 표시되지 않는지 확인
- [ ] 새 배포 뒤 HTML shell이 최신 화면을 불러오고 정적 asset은 정상 표시되는지 확인
- [ ] 파일 업로드·알림·권한 흐름에 회귀가 없는지 확인
- [ ] 실제 운영 domain·Microsoft 365 로그인·managed DB·SIEM은 별도 운영 전환 Task에서 확인
