# TASK-HOME-002 사용자 검수 체크리스트

## 상태

- 자동 검증: 완료
- 사용자 직접 검수: `사용자 검수 대기 — 마지막 일괄 검수`
- 대표 repo·`main`·Persistent UAT: 미반영

## 자동 검증 완료 항목

- [x] Backend 전체 test와 fresh migration `0042`
- [x] 사용자당 사진 1개, 업로드·교체·삭제·감사 3 event DB 통합 검증
- [x] 9개 부서 집계 SQL fresh schema 실행
- [x] Frontend lint·typecheck·unit·build
- [x] 전용 tmpfs PostgreSQL Full-Stack E2E `38/38`와 격리 자원 cleanup
- [x] 공통 shell 사용자 정보·프로필 팝업·Home 부서 지표 unit 검증
- [x] PC·375px 모바일 브라우저 구조·overflow·사용자 전환 검증
- [x] 격리 미리보기 종료와 대표 5081/5174 PID 보존 확인

## 마지막 일괄 사용자 검수

- [ ] PC 모든 페이지의 우측 상단에 사진·부서·이름이 일관되게 보인다.
- [ ] PC 프로필 팝업에서 사진·부서·이름·사진 변경·사진 제거·로그아웃을 이해할 수 있다.
- [ ] 왼쪽 고정 메뉴가 화면 위아래를 채우며 개발 사용자 변경이 맨 아래에 있다.
- [ ] 우측 상단의 중복 `자재` 바로가기가 사라졌다.
- [ ] 영업·품질 등 사용자 전환 시 Home 핵심 지표가 해당 부서 내용으로 바뀐다.
- [ ] 모바일 Home에서 핵심 지표 3개가 가로 넘침 없이 한 화면 폭에 보인다.
- [ ] 모바일 좌상단 drawer와 우상단 계정 시트가 역할별로 명확하다.
- [ ] JPEG/PNG 사진 업로드·교체·제거와 잘못된 파일 오류 안내가 기대와 같다.

## 스크린샷

- [영업 Home PC](home-002-screenshots/home-desktop-sales.png)
- [PC 프로필 팝업](home-002-screenshots/account-popover-desktop.png)
- [영업 Home 모바일](home-002-screenshots/home-mobile-sales.png)
- [품질 Home 모바일](home-002-screenshots/home-mobile-quality.png)
- [모바일 계정 시트](home-002-screenshots/account-sheet-mobile.png)
- [모바일 메뉴 drawer](home-002-screenshots/navigation-drawer-mobile.png)
