# TASK-012A 사용자 검수 체크리스트

- 환경: `experiment/task-012a-quality-inspections` local isolated/synthetic runtime
- 데이터: synthetic project·panel·role only
- 자동 검증 상태: `완료`
- 사용자 검수 상태: `대기`
- 대표 repo·GitHub main 반영: `없음`
- main merge 승인: `0/3`

## 자동 검증

- [x] `0035_panel_quality_inspections.sql` fresh migration, FK·check·partial unique·불변 trigger·purge 계약
- [x] project scope·permission·stage 책임자/current work assignee의 품질 mutation 권한
- [x] LQC 시작·항목 저장·사진 추가/삭제·합격 확정·불변 성적서·PDF
- [x] LQC 합격 → 별도 제조완료확인 → OQC panel 즉시 handoff
- [x] LQC/OQC 불합격 `Nonconformance`, 고객검수/FAT `Punch`, 조치 부서와 재검사
- [x] linked quality Pending의 generic 종결과 panel 품질/제조확인 업무 generic 전이 우회 차단
- [x] operation fingerprint replay·stale version·handoff 실패 rollback·last-panel event
- [x] project 취소의 open attempt/work 정리와 finalized report·confirmation 증빙 보존
- [x] Backend 전체 `376/376`, Frontend 전체 `80/80`, lint·typecheck·production build
- [x] disposable Full-Stack E2E `1/1`, isolated DB/container 자동 cleanup
- [x] desktop·390px 적응형 화면, mobile horizontal overflow 0, bottom navigation 0, 좌상단 숨김 메뉴

## 사용자 직접 검수

- [ ] [품질검사 Desktop](012a-screenshots/quality-inspections-desktop.png): 단계 tab·project queue·panel·검사항목·사진·판정·이력 확인
- [ ] [품질검사 Mobile 390](012a-screenshots/quality-inspections-mobile.png): compact queue → panel → 항목 → 사진 → 판정의 모바일 전용 배치 확인
- [ ] [제조완료확인 Desktop](012a-screenshots/manufacturing-confirmation-desktop.png): LQC 합격 뒤 독립 확인 card와 OQC 전달 action 확인
- [ ] [제조완료확인 Mobile 390](012a-screenshots/manufacturing-confirmation-mobile.png): 핵심 상태·확인 action이 한 화면에 밀도 있게 배치됐는지 확인
- [ ] [모바일 좌상단 메뉴](012a-screenshots/quality-mobile-menu.png): 하단 고정 메뉴 없이 숨김 메뉴가 열리고 품질 진입이 보이는지 확인
- [ ] 품질 담당자는 자기 stage의 시작·저장·사진·확정만 수행할 수 있고 다른 stage action은 없는지 확인
- [ ] 불합격/PUNCH 뒤 연결 Pending이 합격 재검사 전에는 일반 종결되지 않는지 확인
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
