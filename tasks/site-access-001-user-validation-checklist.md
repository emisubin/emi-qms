# TASK-SITE-ACCESS-001 — 사용자 검수 체크리스트

> 상태: Latest-main 자동/Full-Stack 검증 완료 / 최종 독립 검증·사용자 검수 대기 / Git 게시·원격 main 병합 승인 / Persistent UAT·Azure 배포 미승인
> 환경: 격리된 Local Full-Stack, synthetic 사용자만 사용

현재 구현 후보·기준 SHA·검증 결과는 [Implementation report](site-access-001-implementation-report.md), 기능 계약은 [Change 001](site-access-001-change-001.md), 최신 main 통합·게시 계약은 [Change 002](site-access-001-change-002.md)을 기준으로 한다. 코드·migration 변경 뒤에는 체크 결과를 재사용하지 않는다. 사용자 화면 검수는 독립 검증 통과 후 별도 local synthetic runtime URL을 준비한 다음 시작한다. 일반 synthetic 사용자로 접속 신호를 만든 뒤 `Audit.Read.All`을 가진 synthetic 감사 관리자로 전환해 조회한다.

## 자동 검증

- [x] 같은 사용자·브라우저 client의 30분 미만 신호는 같은 접속 행을 사용한다.
- [x] 정확히 30분과 31분 간격의 신호는 새 접속 행을 만든다.
- [x] 동시 신호 20개가 active 접속 한 행으로 수렴한다.
- [x] 메뉴는 19개 고정 코드만 허용하고 중복 없이 최초 방문 순서로 누적한다.
- [x] URL·query·프로젝트·업무 식별자는 신호 body에 포함되지 않는다.
- [x] Web Locks를 끈 실제 두 탭에서 IndexedDB transaction을 사용한 최초 browser client ID가 한 값으로 수렴한다.
- [x] Web Locks가 가능해도 localStorage 읽기·쓰기가 차단되면 IndexedDB로 넘어가 실제 두 탭의 client ID가 한 값으로 수렴한다.
- [x] localStorage와 IndexedDB 쓰기가 모두 차단돼도 같은 문서 실행 중에는 안정적인 임시 client ID로 기록을 계속한다.
- [x] 명시적 로그아웃은 종료를 한 번만 기록하고 다른 사용자의 행 종료는 거절한다.
- [x] 멈춘 signal 또는 end 요청이 있어도 1.5초 안에 실제 로그아웃을 계속한다.
- [x] 접속 행의 직접 update·delete가 DB에서 차단된다.
- [x] Runtime 역할은 새 원장 테이블 조회와 승인된 함수 실행만 가능하다.
- [x] 익명 signal은 401, 감사 권한 없는 사용자 조회는 403, 감사 관리자는 목록·상세·Excel을 조회한다.
- [x] 기존 로그인·로그아웃·변경·권한 거절 감사 조회와 mutation audit 계약이 유지된다.
- [x] 별도 접속 coverage와 실제 근무시간이 아니라는 안내가 화면·Excel에 표시된다.
- [x] Desktop 1440px 표와 Mobile 390px 카드에 접속 메뉴 요약이 보이고 상세에는 전체 순서가 표시되며, 가로 overflow가 없다.
- [x] 최종 통합 tree의 Frontend `248/248`와 Backend `570/570` 전체 회귀가 통과한다.
- [x] 사이트 접속과 공개 G2 격리 Full-Stack이 각각 `1/1` 통과한다.
- [ ] 최종 종료 artifact commit을 대상으로 독립 Codex 검증이 Open P0/P1/P2 `0/0/0`으로 통과한다.

## 사용자 화면 검수

- [ ] 로그인된 상태에서 새로고침하면 `전체 감사 이력`의 `사이트 접속`에 내 접속이 보인다.
- [ ] 같은 브라우저에서 직접 로그아웃하지 않고 30분 미만에 다른 메뉴로 이동하면 새 행이 아니라 기존 행의 마지막 활동과 접속 메뉴가 갱신된다.
- [ ] 다른 브라우저 또는 기기에서 접속하면 별도 행이 보인다.
- [ ] 브라우저의 PMS 사이트 저장소를 지운 뒤 접속하면 새 browser client의 별도 행이 보인다.
- [ ] 앱에서 로그아웃하면 해당 접속 행이 `직접 로그아웃`으로 보인다.
- [ ] 창만 닫은 접속은 30분 이후 `30분 경과`로 보인다.
- [ ] 상세 화면에서 최초 접속·마지막 활동·종료·메뉴·접속 환경을 이해할 수 있다.
- [ ] 선택 Excel에서 화면과 같은 접속 정보와 시간 해석 안내를 확인할 수 있다.
- [ ] 감사 조회 권한이 없는 사용자는 관리자 감사 이력을 열 수 없다.
- [ ] Desktop과 휴대폰 화면에서 목록·상세를 읽고 닫을 수 있다.
- [ ] 이 기록이 실제 근무시간을 뜻하지 않는다는 안내를 이해할 수 있다.

## 게시·운영 검수

- [ ] Git commit·push·PR·merge가 별도 승인 후 완료됐다.
- [ ] Persistent UAT에 G2 migration `0084` 뒤 사이트 접속 migration `0085`와 Backend·Frontend가 승인 순서대로 적용됐다.
- [ ] Azure 공개배포가 별도 승인 후 완료됐다.
- [ ] 운영 synthetic 사용자로 신호·로그아웃·관리자 조회·Excel을 확인했다.

## 현재 판정

- 자동 검증: PASS — latest-main 통합 tree
- 독립 검증: 최종 재검증 진행 전
- 사용자 검수: 대기
- Git 게시·원격 main 병합: 승인 / 실행 전
- Persistent UAT·Azure 배포: 미승인
- Open P0/P1/P2: 자동 검증 기준 `0/0/0`, 독립 재검증 진행 전
