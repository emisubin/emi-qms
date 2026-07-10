# TASK-UAT-HANDOVER-001 User Manual

## 1. Runtime handover란 무엇인가

Runtime handover는 사용자가 접속하는 개발 UAT 화면을 검증된 새 frontend로 교체하는 작업이다. 이번에는 서버 전체가 아니라 화면을 제공하는 frontend만 교체했다.

## 2. 왜 5174를 바꾸나요?

보안 업데이트가 코드에 반영돼도 이미 실행 중인 개발 서버는 자동으로 새 version이 되지 않는다. Teams 앱과 UAT 주소가 5174를 사용하므로 검증된 Vite 7.3.6 frontend를 실제 5174에 적용했다.

## 3. 5174/5185/5186 역할

- 5174: 사용자가 실제로 검수하는 patched HTTPS Development UAT
- 5185: 보안 patch 비교와 rollback 판단을 위한 Preview
- 5186: cutover 전에 최신 main을 확인한 임시 Candidate, 검증 후 종료됨

## 4. 사용자에게 보이는 변화

기능과 화면 디자인은 바뀌지 않아야 한다. 개발 도구 dependency와 실행 source만 안전한 version으로 교체됐다. Backend와 DB는 그대로이므로 기존 프로젝트, 업무와 알림 data도 유지된다.

## 5. 확인할 주소

- 메인: `https://localhost:5174`
- Teams Activity: `https://localhost:5174/teams/activity`
- 관리자: `https://localhost:5174/admin`
- 알림 발송 상태: `https://localhost:5174/admin/system/notification-deliveries`
- Preview 비교: `https://localhost:5185`

## 6. Teams 앱 확인 방법

1. Teams에서 EMI 앱 또는 기존 Activity 알림을 연다.
2. 오른쪽 앱 화면이 비어 있지 않은지 확인한다.
3. 프로젝트/업무/알림 정보가 정상적으로 보이는지 확인한다.
4. 로그인이나 권한 안내가 필요한 경우 안내가 이해 가능한지 확인한다.

## 7. 알림 상세 확인 방법

기존 Activity Feed 알림을 선택해 상세 화면으로 이동한다. 새 알림을 만들거나 발송할 필요는 없다. 상세 제목과 관련 업무 링크가 정상이고 빈 화면이나 target-not-found 오류가 없어야 한다.

## 8. 정상 기준

- 5174 HTTPS 접속 가능
- API, Database, User 카드 정상
- 프로젝트, 내 업무, 알림과 관리자 화면 정상
- Teams Activity와 기존 알림 상세 정상
- 5185와 기능·style 차이 없음
- Console fatal error와 page-level overflow 없음

## 9. 오류별 의미

- `ERR_SSL_PROTOCOL_ERROR`: HTTP/HTTPS 주소 또는 인증서 설정이 맞지 않을 수 있다.
- 연결할 수 없음: frontend가 실행 중이 아니거나 5174가 다른 process에 점유됐을 수 있다.
- API 오류: Backend 5081 또는 proxy 상태를 점검해야 한다.
- 권한 안내: 선택한 개발 사용자 역할에 해당 화면 권한이 없을 수 있다.
- 빈 Teams 화면: TeamsJS 초기화, 로그인 또는 route fallback을 점검해야 한다.

## 10. Rollback이란 무엇인가

새 frontend에 문제가 있을 때 검증된 이전 frontend로 되돌리는 절차다. Frontend만 교체하며 Backend와 DB는 재시작하지 않는다. 이번 handover에서는 문제가 없어 rollback을 실행하지 않았다.

## 11. 데이터가 유지되는 이유

화면 process만 교체했고 Backend, PostgreSQL, migration과 seed를 건드리지 않았다. 전환 전후 DB table 수와 알림 상태도 동일하게 확인했다.

## 12. 하면 안 되는 작업

- UAT DB 삭제·초기화 또는 volume 삭제
- Backend/PostgreSQL 임의 재시작
- 실제 알림을 검수 목적으로 새로 발송
- 5174/5185 process를 소유권 확인 없이 종료
- `.env`, token 또는 인증서 private key 내용을 공유

## 13. FAQ

### 5185는 왜 남아 있나요?

사용자가 5174와 비교하고 문제가 있을 때 rollback 판단 기준으로 사용할 수 있도록 유지한다.

### 5186은 왜 접속되지 않나요?

최신 main candidate 검증용 임시 주소라 5174 성공 후 정상 종료했다.

### Backend도 새 code로 바뀌었나요?

아니다. 최신 main과 running backend 사이 runtime 차이가 없어 기존 PID 49508을 유지했다.

### 새 Teams 알림을 받아야 하나요?

아니다. 이번 검수는 기존 알림과 앱 화면만 확인하며 신규 실제 발송은 범위 밖이다.

## 14. 사용자 검수 체크리스트

상태: `Checklist 작성됨`, `자동 검증 완료`, `사용자 검수 대기`.

- [ ] 5174 메인, 프로젝트와 내 업무 화면 정상
- [ ] 관리자와 알림 발송 상태 화면 정상
- [ ] Teams Activity 웹 화면 정상
- [ ] Teams 앱 내부 화면과 기존 알림 상세 정상
- [ ] API/User 카드 정상
- [ ] 5185와 눈에 띄는 기능·style 차이 없음
- [ ] Console error와 narrow pane overflow 없음
- [ ] Backend/PostgreSQL이 재시작되지 않았음을 이해함
- [ ] 신규 실제 알림을 발송하지 않았음을 확인함
- [ ] SOP와 이 문서가 이해 가능함
