# TASK-UAT-001 Change 004 — 공개 배포 P1 방어선과 운영 준비 게이트

## 1. Task Identity Gate

- proposedTaskId: `TASK-UAT-001 Change 004`
- taskType: `SECURITY_HARDENING`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `운영 전환 Scope Review`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UAT-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `true`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

## 2. Purpose identity

- 업무 목표: 공개 배포 사전감사에서 남은 P1을 애플리케이션·배포 구성·운영 준비 게이트로 모두 닫고, 필수 운영값이 없는 배포는 시작 전에 거절한다.
- Root Finding:
  - `SEC-PUBLIC-003`: 보안 응답 헤더와 운영용 정적 호스팅·reverse proxy 기준이 없다.
  - `SEC-PUBLIC-004`: Host와 forwarded header 신뢰 경계가 명시되지 않았다.
  - `SEC-PUBLIC-005`: 전역·변경·업로드 요청 rate limit이 없다.
  - `SEC-PUBLIC-006`: 업로드 파일 malware 검사, fail-closed quarantine와 이미지 metadata 차단이 없다.
  - `SEC-PUBLIC-007`: Production Entra domain·redirect·CORS 설정이 안전한지 startup에서 검증하지 않는다.
  - `SEC-PUBLIC-008`: secret 주입, DB TLS, 복구 검증, 보안 모니터링과 비상 관리자 준비를 강제하지 않는다.
- 변경 경계: Backend 보안 middleware와 Production startup policy, 운영 frontend/backend container·TLS reverse proxy 구성, 자동 보안 회귀, SOP와 구현 보고를 포함한다.
- 보존할 불변조건: 기존 Entra 로그인·권한·업무 API·DB data와 schema, 실제 provider 비활성 상태, Persistent UAT runtime과 개발용 upload 흐름을 보존한다.
- 예상 산출물: fail-closed Production policy, host/forwarded-header/security-header/rate-limit/upload 검사, production container 구성, 자동 회귀와 privacy-safe 운영 체크리스트.

## 3. 사용자 승인과 게시 경계

- 사용자는 2026-07-29에 공개 배포 사전감사의 남은 P1 전체 해결을 명시 승인했다.
- local experiment 구현·검증을 포함한다.
- 실제 운영 domain·certificate·Entra 등록·managed DB·secret·alert sink를 임의 생성하거나 변경하지 않는다.
- 실제 Teams/Mail 발송, Persistent UAT migration·data reset, commit, push, PR과 merge는 포함하지 않는다.

## 4. 보안 계약

- Production은 exact HTTPS frontend origin, exact public host, trusted reverse proxy IP, Entra 설정, malware scanner, TLS 검증 DB 연결, 최근 복구 검증 시각, 보안 alert sink와 비상 관리자 2명 이상이 없으면 시작하지 않는다.
- forwarded header는 알려진 proxy 1단계에서만 신뢰한다.
- 모든 응답에 브라우저 보안 헤더를 적용하고 운영 TLS에 HSTS를 적용한다.
- 전역 rate limit은 인증 사용자 또는 client IP 단위로 분리하며 mutation과 upload를 더 엄격하게 제한한다.
- multipart 파일은 endpoint 저장 전에 검사한다. malware 또는 허용하지 않는 image metadata는 저장하지 않으며 scanner 장애는 Production에서 fail-closed다.
- secret은 Git 추적 env 파일이 아니라 container secret file 또는 동등한 managed secret 주입을 사용한다.
- 운영 DB 연결은 server certificate와 host name을 모두 검증한다.

## 5. 완료 기준

- 잘못된 Production 설정 조합이 항목별로 startup 전 실패한다.
- 합성한 안전 Production 설정만 policy를 통과한다.
- Host allowlist, trusted forwarded header, security header와 rate-limit 회귀가 통과한다.
- clean/infected/scanner-unavailable/image-metadata upload 분기가 자동 검증된다.
- 운영 Compose는 공개 TLS proxy 하나만 publish하고 Backend·scanner는 내부 network에 남긴다.
- Frontend 정적 build가 Development server 없이 제공되고 `/api`·`/health`만 Backend로 proxy된다.
- Backend/Frontend 전체 회귀, dependency audit, build와 Compose validation이 통과한다.

## 6. Rollback

- Change 004는 schema/data migration을 포함하지 않는다.
- 회귀 시 보안 middleware와 운영 배포 파일만 이전 commit으로 되돌린다.
- unsafe 설정으로 Production gate를 끄거나 scanner를 우회하는 방식은 rollback으로 사용하지 않는다.
- 실제 운영 provider·DB·certificate handover는 별도 운영 승인 전까지 수행하지 않는다.

## 7. 구현 결과

- Production startup에 exact host/origin/redirect/proxy, Entra, rate limit, fail-closed upload scanner, DB `VerifyFull`, restore, SIEM과 비상 관리자 gate를 추가했다.
- Backend·ClamAV를 내부 network에 두고 unprivileged TLS static frontend만 공개하는 production Compose를 추가했다.
- 보안 header, Host filtering, known-proxy forwarded header, category별 rate limit, ClamAV INSTREAM과 image metadata 차단을 추가했다.
- 인증된 runtime-mode API가 Development에서도 정상 호출되도록 Dev 사용자 header 전달을 복구했다.
- schema, migration, 업무 data와 실제 provider 설정은 변경하지 않았다.

## 8. 검증 결과

- Production security targeted 25/25, Backend 전체 459/459
- Frontend unit 143/143, Mock UI 4/4, Full-Stack E2E 55/55
- lint error 0·기존 warning 1, typecheck·build 통과
- dependency audit 알려진 취약점 0
- Backend·Frontend·ClamAV image Scout 전 심각도 0
- Production Compose·TLS script·합성 TLS/Host/header runtime·secret projection 통과
- Open P0/P1/P2/P3 `0/0/0/0`

## 9. 검수·게시 상태

- User validation checklist: `Checklist 작성됨`, `자동 검증 완료`, `실제 운영 환경 검수 대기`
- 코드 게시 품질 gate: `GO`
- 실제 공개 배포: `NO_GO_EXTERNAL`
- commit, push, PR, merge와 actual 운영 handover는 미승인·미실행이다.

## 10. 남은 외부 게이트

- 실제 domain·공인 certificate와 Entra production registration
- managed PostgreSQL `VerifyFull` 연결과 최근 90일 restore 증빙
- SIEM receiver·보안 경보 수신과 비상 관리자 2명 검증
- 실제 운영 로그인·조회·수정·upload·rate-limit 복구 검수
