# TASK-NOTIFY-POLICY-001 — 알림 운영 SOP

> 상태: local 구현 기준 / 실제 provider·Persistent UAT·Azure 운영 적용 전

## 운영 원칙

- 사용자에게 보이는 알림의 수신자는 PMS 인앱 알림 원본을 기준으로 한다.
- Teams Activity와 PWA push는 확정된 인앱 수신자에게 파생한다.
- Teams·메일·PWA 발송 실패는 원래 업무 저장을 되돌리지 않는다.
- 새 자동 Teams 공용 채널 delivery는 만들지 않는다. 기존 이력과 관리자 조회는 삭제하지 않는다.
- 개발·자동검사 환경에서는 Teams·메일·PWA 실제 provider를 비활성화하거나 dry-run으로 유지한다.

## 사건별 운영 기준

| 사건 | 수신자 | 채널 |
| --- | --- | --- |
| 일반 자동 업무 | 해당 업무 담당자 | 인앱·Teams Activity, Teams는 사용자 설정 가능 |
| 일반·긴급 Pending, 재검사·재조치 | 등록 당시 확정된 조치·참조 대상 | 인앱·Teams Activity·메일 필수 |
| Pending 종결 | 최초 Pending 등록 알림 수신자 snapshot | 인앱·Teams Activity |
| 프로젝트 생성 | 모든 활성 사용자 | 인앱·Teams Activity·메일 필수 |
| 납기·상태 변경 | 영업담당자와 프로젝트 지정 담당자 | 인앱·Teams Activity |
| 17단계 납품 완료 | 영업담당자와 프로젝트 지정 담당자 | 인앱·Teams Activity |
| 18단계 영업 최종 완료 | 활성 영업부서 전체 | 메일만 |
| L0 | 현재 담당자 | 예정일 직전 영업일 Teams Activity, 설정 가능 |
| L1 | 현재 담당자 | 예정일 다음 날 첫 평가 Teams Activity·메일 필수 |
| Daily Digest | 각 사용자 | 대한민국 영업일 07:30 메일, 설정 가능 |

PWA push는 위 표를 별도로 복제하지 않고 실제 인앱 알림 가시성을 따른다. 18단계 메일 전용 사건은 PWA push를 만들지 않는다.

## 담당자 미지정 처리

1. 프로젝트 정담당자, 부담당자 순서로 찾는다.
2. 둘 다 없으면 해당 업무 부서의 활성 부서장 전원에게 같은 업무를 표시한다.
3. 부서장 한 명이 완료하면 같은 fallback group의 나머지 업무도 같은 첫 처리자를 기록하고 자동 종료한다.
4. 해당 부서의 활성 부서장이 0명이면 업무 저장을 추측 배정하지 않고 409로 차단한다.
5. 사용자 관리에서 해당 부서장을 지정한 뒤 원래 업무를 다시 실행한다. System Administrator·영업·일반 역할로 우회하지 않는다.

## 일정과 에스컬레이션

- 생산 업무는 정확히 연결된 생산계획 항목의 계획 종료일을 사용한다.
- 구매 업무는 해당 구매품 입고예정일을 사용한다.
- 프로젝트 단위 구매 집계는 미완료 구매품 중 가장 이른 입고예정일을 사용한다.
- 일정 원본 변경은 미완료 업무만 갱신한다. 완료·취소 업무와 모호하거나 연결되지 않은 업무는 수정하지 않는다.
- L2·L3 신규 평가와 delivery는 만들지 않는다. 과거 schema·이력은 호환을 위해 유지한다.

## 일괄 처리와 중복 방지

- 실제 업무 행은 패널별로 유지한다.
- 같은 operation·프로젝트·단계·수신자에 대한 인앱 원본과 외부 delivery는 한 번만 만든다.
- 제조 중단은 별도 생산관리 참고 알림을 만들지 않고 긴급 Pending 한 건으로 처리한다.
- 기존 idempotency, claim/lease, attempt lineage와 재시도 제한을 유지한다.

## 운영 적용 전 점검

1. migration `0074`와 `0075`가 순서대로 적용되는지 확인한다.
2. 각 업무 부서에 최소 한 명의 활성 부서장이 있는지 확인한다.
3. 대한민국 공휴일·회사휴일 데이터가 현재 연도 기준으로 등록됐는지 확인한다.
4. Teams Activity manifest URL·Graph 권한·조직 앱 배포를 확인한다.
5. Gmail SMTP와 PWA VAPID secret은 Repository가 아닌 운영 secret으로 주입한다.
6. 실제 provider를 켜기 전 dry-run으로 사건별 delivery 수와 수신자를 확인한다.
7. 실제 발송은 별도 승인 후 synthetic 알림으로 채널별 1건씩 확인한다.

## 장애 대응과 rollback

- 외부 발송 장애: 해당 provider를 비활성화하거나 dry-run으로 전환한다. 인앱과 원업무는 유지된다.
- 부서장 미지정: 오류에 표시된 부서의 활성 부서장을 사용자 관리에서 지정한다.
- 잘못된 일정 원본: 원본 생산계획·구매 입고예정일을 수정한다. 미완료 업무 due date가 자동 동기화되는지 확인한다.
- migration `0074`·`0075`는 additive다. 적용 뒤 schema를 수동 삭제하지 않고 application rollback 또는 forward-fix migration을 사용한다.
