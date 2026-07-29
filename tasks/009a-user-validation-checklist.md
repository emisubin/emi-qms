# TASK-009A 사용자 검수 체크리스트

- 환경: `experiment/task-009a-iqc-digital-report` local isolated runtime
- 데이터: synthetic project·item·photo only
- 자동 검증 상태: `완료`
- 사용자 검수 상태: `대기`
- 대표 repo·GitHub main 반영: `없음`
- main merge 승인: `0/3`

## 자동 검증

- [x] `0032_iqc_digital_reports.sql` fresh migration, template seed, Legacy/Detailed mode와 불변 trigger
- [x] preview GET 무변경, initialize idempotency, report/receipt optimistic version
- [x] Detailed `/result` 우회 차단과 기존 008A Legacy 판정 회귀
- [x] 필수 항목·외함 사진·판정 사유 gate, magic-byte·크기·개수 계약
- [x] canonical snapshot과 attempt·receipt·Pending·work item transaction 원자성
- [x] PDFsharp 6.2.4·동봉 OFL 한글 font·실제 사진 PDF·반복 동일 byte
- [x] photo/PDF 요청별 project scope, 합성 파일명, `private, no-store`
- [x] Finalized response·photo·snapshot·PDF artifact 불변
- [x] Backend 전체, Frontend unit·typecheck·build, isolated TASK-009A·008B Full-Stack E2E
- [x] desktop·390px 적응형 화면과 모바일 horizontal overflow 0

## 사용자 직접 검수

- [ ] [IQC 검사함 Desktop](009a-screenshots/01-iqc-queue-desktop.png): 신규 성적서·검사 대기 구분 확인
- [ ] [성적서 시작 Desktop](009a-screenshots/02-iqc-report-start-desktop.png): 품목·차수·필수 조건 안내 확인
- [ ] [체크리스트 Desktop](009a-screenshots/03-iqc-checklist-desktop.png): 항목 밀도·판정 button·다양한 shape 확인
- [ ] [사진 등록 Desktop](009a-screenshots/04-iqc-photo-desktop.png): 사진 선택·설명·preview 구조 확인
- [ ] [확정 성적서 Desktop](009a-screenshots/05-iqc-finalized-desktop.png): 읽기 전용 판정·항목·사진 요약 확인
- [ ] [PDF 준비 Desktop](009a-screenshots/05b-iqc-pdf-desktop.png): 출력본 상태와 저장 action 확인
- [ ] [IQC 검사함 Mobile 390](009a-screenshots/06-iqc-queue-mobile-390.png): 좌상단 메뉴 기반 한 건 카드 확인
- [ ] [체크리스트 Mobile 390](009a-screenshots/07-iqc-checklist-mobile-390.png): PC 축소가 아닌 세로 단계·compact 항목 확인
- [ ] [사진 등록 Mobile 390](009a-screenshots/08-iqc-photo-mobile-390.png): 카메라 중심 입력·preview·최종확인 action 확인
- [ ] 이번 실험 결과를 유지·수정·폐기 중 무엇으로 처리할지 결정

## 게시·운영 Gate

- [x] local experiment commit 완료
- [ ] push 승인 — 현재 없음
- [ ] PR 승인 — 현재 없음
- [ ] Persistent UAT migration·runtime handover 승인 — 현재 없음
- [ ] main merge 1차 승인
- [ ] main merge 2차 승인
- [ ] main merge 3차 승인

사용자 직접 검수와 세 번의 merge 승인은 서로 다른 Gate다. 자동 검증 완료는 사용자 검수 또는 게시·merge 승인을 대신하지 않는다.
