# TASK-PRODUCTION-CONTROL-001 Change 008 — 생산계획 항목 담당자·필요 인원·코멘트

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- instructionChainRead: `true`
- canonicalTask: `TASK-PRODUCTION-CONTROL-001`
- taskIdentityGate: `PASS_REUSE`
- roadmapSequenceMatch: `true`
- approvalSource: `USER_EXPLICIT_CHANGE_REQUEST`
- branch: `experiment/task-home-002-personalized-shell`
- baseHead: `a7651b5c266d`
- 범위: 생산계획 항목의 담당자·필요 인원 metadata, 코멘트 명칭, 입력·조회 UI, migration·API·회귀 테스트와 종료 문서
- 제외: 업무 생성·알림 수신자·상태 전이·실적 계산 변경, 대표 repo·`main`, Persistent UAT, 실제 provider, push·PR·merge

## 사용자 요청

1. 조회 제목 `계획 대비 실적`을 `생산계획표`로 변경한다.
2. 생산계획 항목 입력에 담당자 선택과 필요 인원을 추가한다.
3. 기존 비고는 `생산관리 코멘트`로 명칭을 바꾼다.
4. 조회 표의 `실적 연결` 열은 숨기고 `담당자`, `필요 인원`, `코멘트`를 추가한다.

## 구현 계약

1. 계획 항목은 담당자 한 명을 선택할 수 있다. 선택지는 현재 생산계획 담당자 후보에 포함되는 활성 사용자 전체를 중복 없이 제공한다.
2. 담당자는 선택 입력이며 기존 프로젝트와 기존 행의 초기값은 미지정이다.
3. 필요 인원은 선택 입력이며 입력 시 `1~999`의 정수만 허용한다.
4. 담당자와 필요 인원은 프로젝트 생산계획 항목 snapshot에 저장한다. master 생산계획 양식에는 기본값을 추가하지 않는다.
5. 기존 `note` column과 API 호환은 유지하고 사용자 화면·오류·이력 용어만 `생산관리 코멘트`로 통일한다.
6. 실적 연결은 자동 실적 계산 계약으로 계속 저장·동작하지만 조회 표에서는 표시하지 않는다.
7. 담당자 지정은 일정 표시용 metadata다. 내 업무·알림·권한·부서 담당자 인계를 자동 변경하지 않는다.
8. 기존 프로젝트의 데이터와 row version은 보존하며 migration은 nullable column만 추가한다.

## 검증

- 신규·기존 생산계획 항목의 담당자·필요 인원 저장·조회·수정·해제를 검증한다.
- 미등록·비활성 사용자와 `0`, `1000`, 소수 필요 인원을 거부하는지 검증한다.
- 조회 표 이름과 헤더, 입력 화면의 명칭·선택·숫자 입력을 검증한다.
- 기존 실적 연결·진행률·일정 막대가 유지되는지 검증한다.
- migration fresh/upgrade, Backend·Frontend 전체 회귀와 desktop·390px 화면을 검증한다.
