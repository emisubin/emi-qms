# TASK-NOTIFY-004 SOP — Terminal Failed delivery 운영

## 1. 목적

운영자가 Pending, Processing과 terminal Failed를 구분하고, 안전하지 않은 수동 재발송이나 DB 변경 없이 실패 이력을 처리하도록 안내한다.

## 2. 상태별 의미

| 상태 | 의미 | 허용 action |
| --- | --- | --- |
| Pending | 첫 claim 또는 automatic retry 시각 대기 | due 확인, 필요 시 다음 시도 시각 앞당기기 |
| Processing | 한 worker가 lease로 소유 | 조회만 허용, 관리자 mutation 금지 |
| Failed | permanent failure 또는 retry limit 소진 | 원인·attempt 확인, acknowledge/dismiss |
| Sent·DryRunSent·Disabled·Suppressed | terminal 결과 | 결과와 audit 확인 |

## 3. Pending 재발송

관리자 재발송 action은 Pending의 `next_attempt_at_utc`만 앞당긴다. Attempt count는 worker가 실제 claim할 때 증가한다. 이 action은 Failed를 다시 Pending으로 바꾸지 않는다.

## 4. Failed 확인 절차

1. 관리자 알림 발송 상태에서 Failed filter를 연다.
2. Channel, delivery type과 안전한 error code를 확인한다.
3. Attempt history에서 마지막 outcome과 provider-call-start 여부를 확인한다.
4. 설정·권한·수신 대상 문제는 해당 운영 경계에서 수정한다.
5. Delivery row는 acknowledge 또는 dismiss로 처리 상태만 기록한다.
6. 같은 알림을 재발송해야 한다고 판단해도 현재 Failed row를 직접 바꾸지 않는다.

## 5. 수동 재처리가 필요할 때

현재 terminal Failed 수동 retry 기능은 없다. 다음 정보를 개인정보 없이 기록해 후속 기능 요청으로 전달한다.

- channel fixed enum
- failure category fixed enum
- retry exhausted boolean
- provider-call-started boolean
- ambiguous outcome boolean
- 업무상 재발송 필요 여부

새 기능 승인 없이 기존 Failed를 Pending으로 바꾸거나 같은 notification/delivery를 복제하지 않는다.

## 6. Duplicate 가능 경계

Provider가 성공했지만 DB completion 전 process가 종료되면 시스템은 외부 성공을 확정할 수 없다. 이 상태를 안전한 실패로 간주해 재발송하면 중복될 수 있다. Attempt outcome과 provider 운영 기록을 함께 확인하되 exactly-once로 안내하지 않는다.

## 7. Acknowledge와 dismiss

Acknowledge와 dismiss는 발송 결과를 변경하지 않는다. Dashboard의 open handling count와 운영 목록 상태만 바꾼다. Failed를 Sent로 강제하거나 새 attempt를 만들지 않는다.

## 8. Worker·runtime 장애 대응

- Pending 장기 체류: worker health, due 시각, admin handling과 retry limit 확인
- Processing 장기 체류: lease expiry와 stale recovery 확인
- Failed 증가: error category, attempt outcome과 provider 상태를 aggregate로 확인
- Ambiguous attempt: provider call 시작 후 crash 가능성을 분리하고 임의 재발송 금지
- Processing mutation 시도: action을 중단하고 claim owner 완료 또는 lease recovery를 기다림

## 9. 개인정보와 증빙

실제 알림 제목·본문, recipient, email, provider payload/response, claim token과 worker identity를 보고서에 복사하지 않는다. Boolean, count, fixed enum과 masked route alias만 사용한다.

## 10. 금지사항

- Failed row 직접 UPDATE
- Attempt count 초기화·감소
- Attempt row 삭제·번호 재사용
- 기존 notification/delivery 복제
- Processing row retry/acknowledge/dismiss
- 실제 provider를 이용한 승인 없는 smoke
- Retry를 exactly-once로 표현
- Persistent UAT의 임의 DML·backup restore

## 11. Rollback

이 Task는 문서 정책 정정만 수행한다. 잘못된 설명은 문서 revert 또는 forward correction으로 복구한다. Runtime·DB rollback은 대상이 아니다.

## 12. 후속 신규 기능 Gate

Failed 수동 재처리를 다시 요청하려면 NEW_FEATURE로 분류하고 다음을 먼저 결정한다.

- 허용 failure category와 ambiguity 정책
- manual retry generation과 attempt lineage
- append-only admin action audit
- duplicate-risk 확인 UX
- 반복 횟수와 권한
- additive migration 필요 여부
- fake/no-op provider isolated test와 controlled UAT

## 13. 사용자 검수 체크리스트

- [ ] Pending 재발송이 다음 시도 시각만 앞당김을 이해
- [ ] Failed가 terminal 상태임을 이해
- [ ] Failed는 acknowledge/dismiss만 지원함을 확인
- [ ] DB 직접 변경과 반복 재발송 금지를 확인
- [ ] At-least-once와 duplicate 가능 경계를 이해
- [ ] 후속 기능 요청 시 필요한 fixed-field 정보를 확인
