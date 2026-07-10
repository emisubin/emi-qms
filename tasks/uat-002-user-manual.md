# TASK-UAT-002 User Manual — 검수 전용 읽기 모드

## 1. 검수 전용 읽기 모드란 무엇인가

Review-safe UAT는 현재 UAT 데이터를 살펴보되 저장, 삭제, 발송 또는 상태 변경을 할 수 없도록 잠근 검수 환경이다. 감사, 기준선 확인, 화면 조회와 데이터 상태 검토에 사용한다.

## 2. Development UAT와 차이

- Development UAT `https://localhost:5174`: 저장·수정·승인된 알림 검수용
- Review-safe UAT `https://localhost:5190`: 조회·검색·필터·상세 검수용

Review-safe에서 저장이 필요하면 잠금을 우회하지 말고 Development UAT로 이동한다.

## 3. 언제 사용하는가

- 현재 프로젝트/업무/알림 상태를 바꾸지 않고 확인할 때
- 사용자 권한별 조회 화면을 검수할 때
- 관리자 dashboard와 delivery/escalation 상태를 감사할 때
- migration/schema와 UAT persistence 기준선을 확인할 때

실제 저장·발송 흐름 검수에는 사용하지 않는다.

## 4. 접속 주소

- 메인: `https://localhost:5190`
- 프로젝트: `https://localhost:5190/projects`
- 내 업무: `https://localhost:5190/my-work`
- 알림: `https://localhost:5190/notifications`
- Teams Activity 웹 화면: `https://localhost:5190/teams/activity`
- 관리자: `https://localhost:5190/admin`

Teams manifest는 Development 5174를 사용한다. Review-safe 5190은 별도 웹 검수 주소다.

## 5. Banner 의미

화면 상단의 다음 안내가 보여야 한다.

> 검수 전용 읽기 모드 — 조회·검색·필터만 가능하며 저장, 삭제, 발송 및 상태 변경은 차단되어 있습니다.

Banner가 없거나 실행 모드 오류가 보이면 변경 버튼은 계속 잠겨야 한다. 정상으로 추정해 우회하지 않는다.

## 6. 가능한 작업

- 목록/상세 조회
- 검색, 필터, 정렬
- 상태 tab 이동
- 페이지 이동과 관리자 조회 화면 이동
- 읽기 전용 dashboard/이력 확인

## 7. 불가능한 작업

- 생성, 저장, 수정, 삭제, 복구, 일괄 처리
- 업무 시작·완료·취소와 프로젝트 상태 변경
- 알림 읽음 처리, 발송, retry, acknowledge, dismiss
- 사용자 승인/비활성화, 부서·휴일 변경
- Excel upload/import/apply
- 실제 Teams/Mail/Channel 발송

## 8. 버튼이 비활성화된 이유

비활성 버튼에 마우스를 올리면 Review-safe에서 변경할 수 없다는 이유가 표시된다. 일부 화면에서 버튼이 누락되거나 새 action이 비활성화되지 않아도 서버와 DB가 요청을 차단하지만, 발견 즉시 검수 결과에 기록한다.

## 9. 저장이 필요한 경우

1. 현재 조회 조건과 대상만 메모한다. 개인정보/secret은 복사하지 않는다.
2. `https://localhost:5174` Development UAT로 이동한다.
3. Development mode임을 banner/runtime 상태로 확인한다.
4. 데이터 변경/외부 발송 범위의 사용자 승인을 확인한다.
5. Review-safe 잠금을 해제하거나 5190에서 재시도하지 않는다.

## 10. 오류 메시지 의미

- `UatReviewReadOnly` / 423: 변경 요청이 정상적으로 차단됨
- 실행 모드 확인 실패: 안전을 위해 변경 버튼을 잠금. 조회만 제한적으로 사용
- schema mismatch / ready 실패: DB가 repository 기준과 다름. 자동 migration하지 않음
- 서버 연결 오류: 5092/5190 실행 상태를 운영자에게 확인 요청
- certificate 오류: trusted localhost certificate 상태를 확인하고 key 내용을 공유하지 않음

## 11. 데이터가 보호되는 방식

1. 시작할 때 migration/seed/upsert를 하지 않는다.
2. delivery/escalation/purge worker를 실행하지 않는다.
3. 외부 provider client를 등록하지 않는다.
4. 변경 API를 423으로 차단한다.
5. DB connection 자체가 read-only다.
6. readiness가 schema/read-only 상태를 검사한다.

Frontend 버튼 잠금은 추가 안내이며 서버/DB 방어가 최종 기준이다.

## 12. 정상 여부 확인

- 5190 HTTPS 접속
- 상단 Review-safe banner
- API/Database/User 카드 정상
- 프로젝트/업무/알림/관리자 조회
- 변경 버튼 disabled와 이유
- console error/빈 화면/target-not-found 없음
- 390px에서도 page-level 가로 overflow 없음
- Development 5174와 Preview 5185가 계속 접속됨

## 13. FAQ

### 검색 버튼도 비활성화되나요?

아니다. 검색·필터·정렬은 데이터를 바꾸지 않으므로 사용할 수 있다.

### 저장 버튼을 개발자 도구로 켜면 저장되나요?

아니다. API와 DB에서 다시 차단한다. 우회를 시도하지 않는다.

### 알림을 Dry-run으로 보내도 되나요?

안 된다. Dry-run도 delivery row를 만들 수 있어 Review-safe에서는 금지한다.

### Teams 앱에서 5190을 보나요?

아니다. 현재 Teams manifest는 Development 5174를 가리킨다. 5190은 브라우저 Review-safe 검수용이다.

### 데이터가 바뀌었다면 Review-safe 문제인가요?

Development 5081 worker가 같은 DB를 사용하므로 자연 변화 가능성이 있다. 운영자가 application/session/timestamp를 확인해 source를 구분한다. 원인이 불명확하면 검수를 중단한다.

## 14. 하면 안 되는 작업

- DB/volume/container reset
- Review-safe에서 저장/발송 우회
- Development server/worker 종료
- `.env`, token, 인증서 key 공유
- 실제 사용자/회사 이메일을 검수 문서에 기록
- Review-safe server를 production에 활성화

## 15. 사용자 검수 체크리스트

- [ ] 5190 메인과 주요 화면이 열린다.
- [ ] 모든 주요 화면에 Review-safe banner가 보인다.
- [ ] 조회·검색·필터·정렬·상세가 가능하다.
- [ ] 저장/수정/삭제/복구/상태 변경 버튼이 잠겨 있다.
- [ ] 읽음/발송/retry/확인/제외 버튼이 잠겨 있다.
- [ ] 잠긴 버튼에 이유가 표시된다.
- [ ] API/User 카드와 관리자 목록이 정상이다.
- [ ] console 오류와 모바일 가로 overflow가 없다.
- [ ] 5174 Development와 5185 Preview가 유지된다.
- [ ] 저장이 필요하면 Development UAT로 이동해야 함을 이해한다.
- [ ] SOP를 비개발 참여자와 함께 이해할 수 있다.

현재 상태: **Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 대기**
