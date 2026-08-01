# TASK-AZURE-DEPLOY-001 사용자 검수 체크리스트

## Azure 생성 전

- [ ] 20일 예상 비용과 남은 credit을 확인했다.
- [ ] Budget 알림 3단계를 사용자가 직접 설정했다.
- [ ] 실제 hostname·identifier·email·secret이 Git diff에 없음을 확인했다.

## 배포와 복구

- [ ] Migration job이 Exact로 끝나기 전 public traffic이 열리지 않았다.
- [ ] PITR restore가 60분 이내에 끝났고 migration ledger와 aggregate가 맞았다.
- [ ] Backend와 ClamAV에 public ingress가 없다.
- [ ] Front Door URL은 열리고 Container App 원본 URL은 health 이외 403이다.
- [ ] TLS certificate가 최종 hostname과 일치한다.

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
