# TASK-AZURE-DEPLOY-001 사용자 검수 체크리스트

## Change 005 — Active workload readiness

- [x] Backend probe Host 보정과 Nginx 64자 token configuration의 로컬 자동 검증이 통과했다.
- [ ] 새 Frontend image와 workload revision이 실제 Azure에 반영됐다.
- [ ] Backend·Frontend·ClamAV replica가 모두 ready이고 재시작이 증가하지 않는다.
- [ ] Frontend direct health는 `200`, direct 업무 route는 `403`이다.
- [ ] public traffic과 external notification은 Edge·인증 Gate 전까지 비활성이다.

## Change 003 로컬 P1 보안 변경

- [x] Backend·Frontend·migration·DB bootstrap identity 분리 구조와 secret 접근표를 확인했다.
- [x] 관리자·migration·runtime PostgreSQL 연결 분리와 `pms_app` 최소 권한 검증 결과를 확인했다.
- [x] 실제 Azure 자원 생성·비용·traffic·외부 발송은 이번 검수에 포함되지 않음을 확인했다.
- [x] Change 003 사용자 검수를 완료하고 commit·push·PR·CI·`main` merge를 승인했다. (2026-08-02)

## Azure 생성 전

- [x] 20일 예상 비용과 남은 credit을 확인했다. (사용자 확인, 2026-08-02)
- [x] Budget 알림 3단계를 사용자가 직접 설정했다. (사용자 확인, 2026-08-02)
- [ ] 실제 hostname·identifier·email·secret이 Git diff에 없음을 확인했다.
- [ ] DB 관리자·`pms_migrator`·`pms_app` password가 서로 다르고 각각 32자 이상이다.

## Change 004 — 터미널 없는 Portal·GitHub 준비

- [x] ARM JSON 4개가 JSON parse와 Bicep 재compile 구조 동등성 검사를 통과했다.
- [x] GitHub 수동 image workflow가 actionlint, ShellCheck와 정상·negative 입력 검사를 통과했다.
- [x] Workflow가 `workflow_dispatch`, Environment 승인, 비용 확인 checkbox와 `main` 포함 full SHA만 허용한다.
- [x] Azure client secret, mutable `latest` tag와 자동 Container Apps 배포가 없음을 자동 확인했다.
- [x] Azure Portal `로드 파일`용 JSON 4개와 GitHub 웹 실행 절차를 사용자가 확인하고 main 병합을 승인했다. (2026-08-02)
- [ ] 실제 GitHub Environment·OIDC·ACR 권한은 Foundation 생성 후 웹 화면에서 설정한다.
- [ ] 실제 GitHub Actions image push는 별도 비용 확인 후 사용자가 실행한다.

## 배포와 복구

- [ ] Migration job이 Exact로 끝나기 전 public traffic이 열리지 않았다.
- [ ] PITR restore가 60분 이내에 끝났고 migration ledger와 aggregate가 맞았다.
- [ ] Backend와 ClamAV에 public ingress가 없다.
- [ ] Front Door URL은 열리고 Container App 원본 URL은 health 이외 403이다.
- [ ] TLS certificate가 최종 hostname과 일치한다.
- [ ] Key Vault 전체 scope의 workload secret-read role이 0이다.
- [ ] 이전 단일 runtime identity를 배포한 이력이 있다면 남은 vault-scope role assignment와 미사용 identity를 정리했다.
- [ ] Backend·Frontend·migration·DB bootstrap identity가 서로 다르고 접근표 밖 secret read가 거부된다.
- [ ] DB role bootstrap job이 성공한 뒤에만 migration job을 실행했다.
- [ ] Backend DB session은 `pms_app`, migration DB session은 `pms_migrator`로 확인된다.
- [ ] `pms_app`의 업무 CRUD는 성공하고 schema·role·temporary table 생성과 migration ledger 변경은 거부된다.

## 로그인과 권한

- [ ] 비상 관리자 두 명이 System Administrator로 로그인할 수 있다.
- [ ] 처음 로그인한 일반 사용자는 역할 승인 전 조회·입력이 불가능하다.
- [ ] 관리자가 역할을 부여한 뒤 해당 부서 권한만 사용할 수 있다.
- [ ] 로그아웃 뒤 protected API와 화면이 다시 열리지 않는다.

## 실제 업무

- [ ] 세 프로젝트를 생성하고 부서별 조회·입력·첨부가 정상이다.
- [ ] ClamAV 정상 파일 허용, 위험·검사 불가 파일 차단을 확인했다.
- [ ] Excel·PDF·QR 다운로드가 정상이다.
- [ ] DB·첨부 증가량과 API·ClamAV memory를 매일 확인했다.

## 알림

- [ ] In-app 알림과 내 업무가 정상이다.
- [ ] 새 Teams manifest가 조직 catalog에서 PMS 이름과 최종 주소로 열린다.
- [ ] Teams activity smoke 한 건이 올바른 사용자에게 도착한다.
- [ ] Gmail smoke 한 건이 올바른 사용자에게 도착한다.
- [ ] 중복 발송과 실제 업무 원문 과다 노출이 없다.

## 운영 종료

- [ ] 20일간 비용, 장애, 응답시간, DB·첨부 증가량을 기록했다.
- [ ] 정식 운영 사양과 HA/WAF/Blob 여부를 실측값으로 다시 결정했다.
- [ ] 시범 데이터를 정식 운영에 유지할지 최종 확인했다.
