# TASK-NOTIFY-REPROCESS-001 구현 보고서

## 1. 상태

- Task: `TASK-NOTIFY-REPROCESS-001`
- 유형: `NEW_FEATURE` → 명시적 Roadmap 순서 override → Codex 2차 기획 대체 → 구현
- 구현 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- 사용자 검수: `사용자 검수 대기 — 마지막 일괄 검수`
- 구현 기준: [Codex 2차 기획](../docs/36-notification-delivery-reprocess-plan.md)
- Git 경계: 현재 experiment local commit만 허용. 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider 제외

## 2. 해결한 업무 문제

기존 terminal `Failed` 알림은 관리자가 확인·제외만 할 수 있어 일시적인 provider 장애가 해소돼도 같은 delivery를 안전하게 다시 처리할 방법이 없었다. 이제 실패 원인을 확인한 관리자가 중복 위험을 명시적으로 인정하고 사유를 남긴 뒤 새 generation으로 재처리할 수 있으며, 과거 시도 횟수와 append-only 재처리 이력을 함께 보존한다.

## 3. 포함·제외 범위

포함 범위는 terminal `Failed`만 최대 100건 원자 처리, 예상 generation CAS, generation 최대 5, 사유 10~500자, 중복 위험 확인, 현재 상태 `Pending` 복귀, generation별 retry budget 초기화, 전역 attempt lineage 보존, 관리자 목록·상세 UI와 390px 실행 UX, append-only event다.

Processing/Pending/Sent 재처리, 자동 무한 재처리, provider idempotency 보장, 실제 Teams/Mail 발송, Persistent UAT 적용은 제외했다.

## 4. 구현 구조

| 영역 | 구현 |
| --- | --- |
| DB | `0049_notification_delivery_reprocess_generations.sql`: current generation·generation attempt count, attempt generation, append-only reprocess event |
| Backend | worker retry 판단을 generation count로 분리하고 total attempt count는 계속 증가, row lock·CAS·all-or-nothing batch |
| API | `POST /api/admin/notification-deliveries/reprocess-failed` |
| Frontend list | 최종 실패 선택 시에만 재처리 panel, 사유·중복 확인, G/이번/전체 시도 표시 |
| Frontend detail | 현재 generation, generation별 attempt, 단건 재처리, `G1 → G2` 수동 재처리 이력 |
| 권한 | System Administrator 전용. 일반 부서 사용자는 403 |
| 외부 provider | 구현·E2E 모두 disabled/dry-run. 재처리는 outbox를 `Pending`으로만 전환 |

주요 파일은 `NotificationDeliveryStore.cs`, `NotificationDeliveryContracts.cs`, `NotificationDeliveryEndpointExtensions.cs`, migration `0049`, `App.tsx`, `projects.ts`, `api.ts`다.

## 5. 기술적 결정과 검토한 대안

- 기존 row를 삭제·복제하지 않고 generation을 올려 dedupe identity와 전체 lineage를 유지했다.
- total attempt count를 0으로 되돌리면 감사 추적이 깨지므로 유지하고, generation attempt count만 0으로 초기화했다.
- 배치 중 하나라도 상태·generation이 달라지면 전체 409로 거부해 부분 재처리를 막았다.
- provider 호출 후 결과 저장이 끊긴 경우 exactly-once를 보장할 수 없으므로 중복 위험 acknowledgement를 필수화했다.
- 관리자 note는 새 event에 이전 상태와 함께 snapshot하고 기존 attempt/event는 수정하지 않는다.

## 6. 시행착오 및 폐기한 접근

E2E synthetic insert에서 표시 recipient 컬럼명을 잘못 사용해 첫 실행이 실패했다. 실제 migration schema의 `display_recipient_name`으로 수정했다. 다음 실행에서는 `psql insert returning` 뒤 command tag를 delivery ID로 잘못 읽어 상세 route가 404였으며, 격리 테스트 고정 UUID로 데이터 identity를 명시했다. 모두 disposable DB에서만 발생했고 매 실행 후 resource가 삭제됐다.

## 7. 검증 결과

| 검증 | 결과 |
| --- | --- |
| Backend 기능 테스트 | admin-only, G1 Failed→G2 Pending, total attempt 3 유지, generation count 0, event 1, stale CAS 409, worker claim total 4/G2 PASS |
| Backend 원자성 | Failed+Pending batch 409, 상태/event 무변경 PASS |
| Frontend 회귀 | 기존 detail mock rolling compatibility와 generation display 포함 PASS |
| Full-Stack E2E | 실제 격리 DB에서 실패 선택→중복 확인→새 generation→상세 `G1 → G2` PASS |
| Frontend 전체 | `111/111` PASS, typecheck/build PASS, lint error 0·기존 warning 1 |
| Backend 전체 | `410/410` PASS, 실패·skip 0 |
| Migration | fresh isolated DB에서 `0001 → 0049` 적용 PASS |

화면 증거는 [실패 목록 desktop](notify-admin-controls-screenshots/03-failed-notification-reprocess-desktop-1440.png), [실패 목록 mobile](notify-admin-controls-screenshots/04-failed-notification-reprocess-mobile-390.png), [G2 상세](notify-admin-controls-screenshots/05-reprocessed-notification-detail-desktop-1440.png)이다.

## 8. SOP

1. `관리자 → 알림 발송 상태 → 미처리 실패`를 연다.
2. 오류 코드·메시지와 provider 장애가 해소됐는지 확인한다.
3. terminal `Failed` 행만 최대 100건 선택하고 `최종 실패 재처리`를 누른다.
4. 10자 이상의 구체적인 재처리 사유를 입력하고 provider 중복 가능성 확인에 체크한다.
5. `새 generation 시작` 후 결과 메시지와 상세의 generation·수동 재처리 이력을 확인한다.
6. 새 `Pending`은 worker 다음 주기에서 처리한다. 실제 provider 활성화·운영 적용은 별도 UAT 승인 절차를 따른다.

409가 나오면 목록을 새로고침해 상태와 generation을 다시 확인한다. generation 5 도달 건은 UI와 API가 추가 재처리를 차단하므로 원인 분석 후 별도 운영 결정을 한다.

## 9. 사용자 매뉴얼

- `G1/G2`는 같은 알림 delivery의 재처리 세대다.
- `이번 N회`는 현재 generation의 retry budget, `전체 N회`는 처음부터 누적한 감사 횟수다.
- 재처리 실행은 발송 완료가 아니라 새 대기열 등록이다.
- 이미 provider가 수신했지만 결과 저장만 실패한 경우 중복 알림이 갈 수 있으므로 확인 checkbox는 단순 형식이 아니라 운영 판단이다.

## 10. 사용자 검수 체크리스트

- [x] 자동: 일반 사용자 403, 관리자만 실행
- [x] 자동: terminal Failed만 허용·batch all-or-nothing·stale generation 409
- [x] 자동: 전체 attempt와 과거 attempt/event 불변, 새 generation retry budget 분리
- [x] 자동: desktop/mobile panel과 G2 상세 이력
- [x] 자동: 실제 provider 호출 0, 격리 DB cleanup
- [ ] 사용자: 운영자가 오류·사유·중복 확인 문구를 이해할 수 있는지 확인
- [ ] 사용자: 실제 운영 전 대표 runtime/UAT에서 승인된 synthetic delivery 1건 검수

상태: `사용자 검수 대기 — 마지막 일괄 검수`.

## 11. Finding·잔여 위험·rollback

- Open P0/P1/P2: 없음.
- 구조적 위험: 외부 provider exactly-once는 보장하지 않는다. acknowledgement·사유·generation cap·append-only event로 완화했다.
- P3: provider별 idempotency key가 공식 지원되면 별도 운영 고도화 Task에서 연결한다.
- Rollback은 새 endpoint/UI/worker generation 사용을 이전 application으로 되돌리되 additive `0049` column/table은 유지한다. 이미 생성된 generation/event는 삭제하지 않고 forward-fix한다.

## 12. 종료 산출물

| 산출물 | 상태·위치 |
| --- | --- |
| Implementation report | 완료 — 이 문서 |
| SOP | 완료 — 이 문서 8장 |
| User manual | 완료 — 이 문서 9장 |
| Roadmap update | 완료 — `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md` |
| User validation checklist | 작성됨·자동 검증 완료·사용자 검수 대기 — 이 문서 10장 |
