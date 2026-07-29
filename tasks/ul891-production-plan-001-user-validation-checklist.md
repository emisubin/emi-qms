# TASK-UL891-PRODUCTION-PLAN-001 사용자 검수 체크리스트

- 상태: `자동 검증 완료 / 사용자 검수 대기 — 마지막 일괄 검수`
- 환경: experiment 고정 검수 runtime
- Frontend: `http://127.0.0.1:42983`
- Backend: `http://127.0.0.1:41166`
- 검수 역할: 생산관리 검수 사용자
- 대표 repo·`main`·Persistent UAT·실제 provider: 미반영

## 자동 검증 완료

- [x] UL891 세트형 LinkedV1 프로젝트에서 `전체`와 실제 세트 인스턴스 범위를 조회한다.
- [x] 범위를 바꾸면 생산계획표와 계획·실적 일정표가 같은 범위로 함께 바뀐다.
- [x] 전체에서는 14면, 선택 세트에서는 해당 세트의 7면을 제조·품질·물류 실적 분모로 사용한다.
- [x] 1번 세트의 기간을 변경해도 2번 세트 기간과 row version은 유지된다.
- [x] 전체 범위는 활성 세트의 가장 이른 시작일과 가장 늦은 종료일을 집계한다.
- [x] 새 세트 추가 시 계획 scope가 자동 생성된다.
- [x] 프로젝트 공통 저장과 기존 Excel 입력으로 세트 일정을 우회 변경하지 못한다.
- [x] Backend 전체 `430/430`, Frontend 전체 `140/140`, lint·production build, migration `0064` 검증을 통과했다.
- [x] 고정 검수 runtime ready, desktop·390px 화면과 browser warning/error 0을 확인했다.

## 사용자 최종 검수 대기

- [ ] 생산관리 탭에서 `전체`와 각 세트를 전환해 두 표가 함께 바뀌는지 확인한다.
- [ ] `생산계획 수정 → 세트 일정`에서 서로 다른 두 세트에 다른 기간·담당자·필요 인원·코멘트를 입력한다.
- [ ] 저장 후 각 세트 값이 섞이지 않고 `전체`에는 집계값만 표시되는지 확인한다.
- [ ] 세트가 13개 이상인 프로젝트에서 선택 목록 방식이 사용하기 편한지 확인한다.
- [ ] 취소 세트는 조회만 가능하고 전체 집계에서 빠지는지 확인한다.
- [ ] 모바일에서 세트 선택, 생산계획 카드와 일정표를 한 열로 확인할 수 있는지 확인한다.

## 화면 증빙

- [전체 세트 PC](ul891-production-plan-001-screenshots/desktop-overall.png)
- [1번 세트 PC](ul891-production-plan-001-screenshots/desktop-set-1.png)
- [1번 세트 모바일 범위](ul891-production-plan-001-screenshots/mobile-set-1.png)
- [1번 세트 모바일 계획표](ul891-production-plan-001-screenshots/mobile-set-1-table.png)

## 사용자 검수 실패 시 재개 규칙

기존 `TASK-UL891-PRODUCTION-PLAN-001`의 다음 `change-###` 또는 확인된 결함의 `BUGFIX`로 재개한다. 같은 기능을 새 Task로 다시 기획하거나 구현하지 않는다.
