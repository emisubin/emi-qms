# TASK-AZURE-PILOT-001 User Validation Checklist

상태: `Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 대기 / 실제 운영 서비스 미선정`.

## 1. 이번 Task 검수

- [ ] 이번 변경이 화면 기능이나 업무 입력 방법을 바꾸지 않는다는 설명을 확인했다.
- [ ] 운영 Microsoft 365 설정은 API app과 SPA app 두 개가 필요하다는 설명을 확인했다.
- [ ] database migration이 application보다 먼저 실행되고 실패하면 application을 시작하지 않는다는 설명을 확인했다.
- [ ] Azure hosting·managed DB·WAF·SIEM 선정은 이번 Task 완료 조건이 아니라는 범위 보류를 확인했다.
- [ ] Draft PR의 변경 파일에 실제 tenant/client ID, email, password, connection string과 certificate가 없는지 확인했다.

## 2. 자동 검증 완료

- [x] Backend 전체 469/469
- [x] 동시 migration 1/1
- [x] Production security 30/30
- [x] Frontend unit 144/144, lint/typecheck/build
- [x] Mock UI 4/4
- [x] Isolated Full-Stack E2E 55/55
- [x] Production preflight 4/4
- [x] Production migration image fresh/existing apply와 ledger Exact
- [x] ARM64·AMD64 Production Backend/Frontend image build
- [x] ARM64·AMD64 image Critical 0, High 0
- [x] 테스트 DB/container/network와 자동 생성 screenshot cleanup

## 3. 서비스 선정 후 실제 운영 검수

- [ ] 실제 HTTPS domain과 certificate
- [ ] 실제 Microsoft 365 로그인·재인증·로그아웃 cache
- [ ] 역할별 조회·수정 권한
- [ ] managed DB TLS, backup, PITR와 별도 restore
- [ ] clean/malware/unscannable/metadata upload
- [ ] WAF·rate-limit·보안 alert 수신
- [ ] 실제 Teams·메일 provider 승인 smoke
- [ ] 새 release 실패 시 traffic/application rollback
- [ ] 한 달 파일럿 data 용량·첨부·backup 시간 모니터링

실제 운영 검수가 끝나기 전 공개 배포 상태는 `NO_GO_EXTERNAL`이다.
