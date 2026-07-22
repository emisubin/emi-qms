# TASK-UL891-SET-001 사용자 검수 체크리스트

- 상태: `사용자 검수 대기 — 실험 구현 완료로 간주, 마지막 일괄 검수 가능`
- Frontend: `http://127.0.0.1:42983`
- Backend: `http://127.0.0.1:41166`
- 검수 프로젝트: `UL891-UAT-0722 / UL891 세트 부분출하 검수`
- 사용자: `dev-sales`, `dev-design`

## 영업

- [ ] 프로젝트 상세 `영업` 탭에서 세트 사양 3개, 활성 세트 6개, 개별 패널 38개가 보인다.
- [ ] Booth A는 3세트의 A~G 이름·규격이 같고 P01~P21 ID는 각각 다르다.
- [ ] `새 세트 사양 추가`에서 사양명·수량·구성 code·사유를 입력할 수 있다.
- [ ] 기존 사양의 `수량 추가`, instance checkbox 선택 취소와 발주품 회수 안내가 이해된다.
- [ ] `월별 부분출하 발행요청`에 판매액·요청 합계·잔액과 1일~말일 월 입력이 보인다.

## 설계

- [ ] `설계` 탭에서 Booth A·Booth B v1이 `확정`으로 보이고 Control 세트는 Draft로 보인다.
- [ ] Published 버전은 직접 수정되지 않고 `새 Draft 만들기`로 다음 version을 만든다.
- [ ] 구성 패널명·규격을 한 번 입력하면 같은 사양의 모든 세트 panel card에 같은 값이 보인다.

## 개별 패널·모바일

- [ ] 패널 card를 누르면 P01 상세에 `SET 1 · Booth A`, `1번 · 사양 v1`, code A, 공통 규격이 보인다.
- [ ] 390px에서 좌상단 drawer, 상단 project context, 세트 summary, 한 열 spec card가 가로 overflow 없이 보인다.
- [ ] 모바일 영업에서도 세트별 panel card와 월별 발행요청으로 이동할 수 있다.

## 안전 경계

- [ ] 기존 평면 UL891 프로젝트 `ps26-001`은 기존 10면 구조로 유지된다.
- [ ] 대표 repo·GitHub `main`에는 반영되지 않았음을 확인한다.
- [ ] push·PR·merge·Persistent UAT·실제 Teams/Mail/회계 provider는 실행하지 않는다.
