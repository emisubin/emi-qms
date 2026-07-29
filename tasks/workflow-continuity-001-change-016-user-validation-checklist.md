# TASK-WORKFLOW-CONTINUITY-001 Change 016 사용자 검수 체크리스트

상태: `사용자 검수 대기 — 마지막 일괄 검수`

고정 검수 환경:

- Frontend: `http://127.0.0.1:42983`
- Backend: `http://127.0.0.1:41166`

## 자동 검증 완료

- [x] 같은 Packing Unit에 들어 있는 두 패널이 출발 대기 목록에 각각 한 행으로 표시된다.
- [x] 패널 하나만 선택해 출발하면 선택 패널의 출발 업무만 완료되고 납품 업무가 생성된다.
- [x] 선택하지 않은 패널은 출발 대기 상태와 기존 증빙·업무를 유지한다.
- [x] 출발한 패널만 납품 대기 목록에 표시되고 개별 선택할 수 있다.
- [x] 패널 하나만 납품하면 선택 패널만 `ShipmentCompleted`가 된다.
- [x] 모든 활성 패널의 마지막 납품 전에는 영업 정산 업무가 생성되지 않고 마지막 납품에서 한 번 생성된다.
- [x] 같은 프로젝트·단계 선행조건·Pending·권한·필수 증빙·version·멱등성 계약이 유지된다.
- [x] 기존 unit 기반 기록은 panel membership으로 backfill되고 기존 `unitIds` 요청도 호환된다.
- [x] Backend 전체 `424/424`, Frontend 전체 `135/135`, isolated Full-Stack `1/1`이 통과한다.
- [x] 고정 검수 runtime migration `0057`, Frontend/Backend health와 browser console error 0을 확인했다.

## 사용자 수동 검수

- [ ] 물류 담당자로 같은 Packing Unit에 패널 두 개 이상을 포장한다.
- [ ] `출발`에서 패널별 행과 소속 Packing Unit 표시를 확인한다.
- [ ] 일부 패널만 선택해 상차 사진·출발일을 저장하고 나머지 패널이 출발 대기에 남는지 확인한다.
- [ ] `납품`에서 출발한 패널만 선택해 서명본을 저장할 수 있는지 확인한다.
- [ ] 부분 납품 뒤 선택하지 않은 패널의 상태·업무·증빙이 변경되지 않았는지 확인한다.
- [ ] 모든 패널을 각각 납품한 마지막 순간에만 영업 정산 업무가 생성되는지 확인한다.

## 게시 경계

- local experiment 검수만 수행한다.
- 대표 repo·`main`·Persistent UAT·실제 알림 provider는 이 체크리스트 범위가 아니다.
- Commit·push·PR·merge는 별도 사용자 요청 전까지 수행하지 않는다.
- `main` merge 승인 상태: `0/3`.
