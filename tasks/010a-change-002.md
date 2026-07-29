# TASK-010A Change 002 — 키팅을 자재 메뉴에 통합

## 사용자 요청

- 요청일: 2026-07-17
- 요청: 독립 `키팅` 전역 메뉴를 제거하고 `자재`에 합친다. 수정 완료 뒤 다음 Task를 시작한다.
- 승인 경계: 현재 experiment branch의 source·test·synthetic screenshot·local commit만 포함한다. 대표 repo·`main`·push·PR·merge·Persistent UAT·provider는 제외하며 main merge 승인 수는 `0/3`이다.

## 조사 결과와 원인

- 기존 010A interview는 “기존 자재 영역에서 패널 키팅 진입”을 요구하지만 구현은 `App.tsx` 전역 navigation과 topbar에 `키팅`을 별도 등록했다.
- `/materials/kitting?project=&panel=` deep link와 전용 `PanelKittingPage`는 업무 분리·직접 진입을 위해 유지해야 하며, 메뉴 통합이 API·DB·키팅 transaction을 바꾸지는 않는다.

## 승인된 변경 계약

- 전역 navigation과 topbar에는 `자재`만 표시하고 별도 `키팅` 항목을 제거한다.
- `자재`는 입고 화면과 키팅 화면 모두에서 active로 표시한다.
- 자재 입고 화면 hero에 `패널 키팅` 내부 action을 제공한다.
- 입고 권한 없이 키팅 조회만 가능한 역할은 `자재` 메뉴에서 키팅 화면으로 진입한다.
- 키팅 내 업무·제조 업무의 직접 deep link와 `/materials/kitting` route는 유지한다.
- 모바일 좌상단 drawer에서도 `자재` 하나만 active로 표시하고 bottom navigation은 만들지 않는다.

## 검증·산출물

- Frontend 핵심 unit `60/60`, 전체 unit `77/77`, lint error 0(기존 warning 1), typecheck·production build `PASS`
- synthetic browser desktop 1440px에서 자재 내부 `패널 키팅` 진입점 1건, 독립 `키팅` 메뉴 0건, horizontal overflow 0 확인
- synthetic browser 390px에서 키팅 화면의 `자재` active, 독립 `키팅` 메뉴 0건, horizontal overflow 0 확인
- `04-panel-kitting-menu-mobile-390.jpg`를 통합 메뉴 상태로 교체하고 `05-materials-kitting-entry-desktop-1440.jpg`를 추가
- implementation report·user validation checklist 갱신 후 local follow-up commit
