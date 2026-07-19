# TASK-ADMIN-002 사용자 검수 체크리스트

- 환경: `experiment/task-home-002-personalized-shell` isolated/synthetic runtime
- 자동 검증 상태: `완료`
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`
- 대표 repo·GitHub main 반영: `없음`
- main merge 승인: `0/3`

## 자동 검증

- [x] 고정 6종 catalog와 Draft→Active→Archived lifecycle
- [x] Active version·기존 실행 snapshot 불변
- [x] 시스템 관리자 전체 관리와 지정 부서장 자기 부서 scope
- [x] 부서 이동·binding 해제 뒤 mutation 차단
- [x] 제조 시작 시 활성 template lock·version FK·항목 snapshot
- [x] 전체선택 checkbox와 단일 선택 Excel export·audit
- [x] Backend 398/398, Frontend 104/104, 격리 Full-Stack 1/1
- [x] Desktop·390px 적응형 편집 화면과 horizontal overflow 0

## 사용자 직접 검수

- [ ] [양식 관리 Desktop](sales-admin-002-screenshots/05-form-templates-desktop-1440.png): 3열 구조·Draft 항목·활성화 action 확인
- [ ] [양식 관리 Mobile](sales-admin-002-screenshots/06-form-templates-mobile-390.png): catalog→version→editor 순서와 입력 크기 확인
- [ ] 시스템 관리자에서 부서장 지정·해제 흐름 확인
- [ ] 지정 부서장이 자기 부서 양식만 편집 가능한지 확인
- [ ] 실제 IQC/LQC/OQC/전진검수/FAT/제조 항목을 어느 change에서 입력할지 결정

## 게시·운영 Gate

- [x] local experiment commit 완료 — 이 checklist를 포함한 최종 experiment commit으로 고정
- [ ] push 승인 — 현재 없음
- [ ] PR 승인 — 현재 없음
- [ ] Persistent UAT migration·runtime handover 승인 — 현재 없음
- [ ] main merge 1차 승인
- [ ] main merge 2차 승인
- [ ] main merge 3차 승인
