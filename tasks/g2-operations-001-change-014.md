# TASK-G2-OPERATIONS-001 Change 014 — 제조 인원 출근 관리 월간표 disclosure

- taskType: `BUGFIX`
- changeStatus: `IMPLEMENTED_VALIDATED_USER_REVIEW_PENDING`
- userInstructionDate: 2026-08-19
- canonicalTaskId: `TASK-G2-OPERATIONS-001`
- implementationApproved: true
- commitApproved: false
- pushApproved: false
- pullRequestApproved: false
- mergeApproved: false
- persistentUatApproved: false
- azureDeploymentApproved: false

## 1. 사용자 지정 화면 계약

- `제조 인원 출근 관리`의 월간 출근표는 오전 합계·오후 합계·하루 총원을 기본으로 표시한다.
- 오전 합계의 왼쪽 header 또는 날짜별 합계 숫자를 누르면 오전 EMI·도급 행을 펼치고 다시 누르면 접는다.
- 오후 합계도 같은 방식으로 오후 EMI·도급 행을 독립적으로 펼치고 접는다.
- 기존 미래 `예상`, 날짜 filter, 휴일 열 red text, 합계·세부 행 디자인과 keyboard/button semantics를 유지한다.
- Change 013의 `//`는 `0~60` 확대와 `60~180` 압축 사이의 축 전환 marker이며 이번 변경에서는 graph scale·marker를 변경하지 않는다.

## 2. 보존 범위

- 출근 입력 form·즉시 합계·API·DB·권한·data는 변경하지 않는다.
- G2 홈 출근표의 기존 disclosure 동작은 변경하지 않는다.
- 원격 `main`, Persistent UAT와 Azure 공개배포는 변경하지 않는다.

## 3. 검증 계획

- 관리 월간표의 기본 세부행 숨김, 합계 숫자·header 양쪽 펼침과 오전·오후 독립 상태 test
- Frontend G2 집중·전체 unit, lint, typecheck와 production build
- local desktop 1440·mobile 390에서 disclosure·휴일 열·page/table overflow 확인

## 4. 다음 Gate

구현·자동·browser 검증 뒤 local 제조 인원 출근 관리 화면을 사용자 검수로 유지한다. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.

## 5. 구현·검증 결과

- `G2AttendancePage` 월간표를 구분·오전 합계·오후 합계·하루 총원 기본 구성으로 바꾸고 오전·오후 EMI/도급 상세 행을 독립 disclosure로 제공했다.
- 왼쪽 합계 header와 모든 날짜의 합계 숫자 cell이 같은 펼침 상태를 제어하며, button semantics·keyboard focus와 기존 full-cell hit area를 유지했다.
- G2 집중 unit `6/6`, Frontend 전체 `225/225`, typecheck, lint error `0`, production build를 통과했다. 기존 Fast Refresh warning 1건과 chunk-size warning은 유지된다.
- local desktop·mobile live 검수에서 기본 세부 행 `0`, 선택한 조의 EMI·도급 행만 `1/1` 표시, 다른 조 `0`, page overflow `0`, 표 내부 가로 scroll과 휴일 red text 유지, browser console error `0`을 확인했다.
- graph의 `//` marker와 공통 2단계 scale은 변경하지 않았다.
