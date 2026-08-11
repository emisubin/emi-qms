# TASK-NOTIFY-POLICY-001 — 알림 운영 정책 정합화 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/notify-policy-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 같은 성격의 이벤트가 서로 다른 수신자·채널로 발송되고, 프로젝트 완료 단계와 Pending 종류별 정책이 실제 업무와 다르다. 담당자 미지정 시 관리자에게 업무가 배정되고, 업무 예정일이 일정 원본과 자동으로 맞춰지지 않는다.
- 대상 사용자·역할: 모든 활성 사용자, 프로젝트 영업 담당자와 지정 담당자, 영업팀 전체, 업무 담당자, 활성 부서장 전원, Pending 원수신자, 시스템 관리자(운영 조사 전용, 현장 업무 fallback 금지).
- 정상 흐름: 업무 이벤트 저장 → 인앱 알림 원본 생성 → 확정된 수신자·채널별 delivery 비동기 처리.
- 예외·복구 흐름: 수신자·일정 원본이 없으면 임의 대체하지 않고(`due_date=null` 유지), 외부 채널 실패는 업무 저장을 취소하지 않으며 기존 재시도·append-only attempt lineage를 유지한다. 같은 이벤트 재시도는 중복 알림·delivery를 만들지 않는다.
- 확정한 정책과 명시적 제외: 인터뷰 7개 round의 14개 결정 전체(3장 매트릭스 참조). 제외 — Teams 공용 채널 Webhook 운영, L2·L3 확대 에스컬레이션, quiet hours, 임의 예정일 대체, 인앱 자동 삭제, 새 외부 채널·provider, 알림 세부 문구 후속 기획.
- planning으로 넘긴 비차단 미결정 사항: 실제 provider 운영 검증과 공개 배포는 구현·자동 검증 뒤 별도 승인 경계로 둔다(16장 1번). 미완료 업무 `due_date` backfill의 안전 범위는 이 기획에서 확정한다(16장 3번).

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

업무 성격에 맞는 대상자가 정해진 채널에서 알림을 한 번만 받고, 담당자가 없어도 부서장 전원이 업무를 확인하며, 일정 변경에 맞춘 D-1·초과 알림을 받을 수 있게 한다.

## 2. 배경과 해결할 업무 문제

- 현재 사용자는 PMS 인앱 알림을 원본으로 Teams Activity Feed·메일·Teams 공용 채널·일일 요약이 일부 이벤트에만 연결된 상태에서 업무를 확인한다.
- 시간 손실·누락·중복: 정상 Pending은 외부 채널이 빠지고, 제조 중단은 긴급 Pending과 참고 알림 두 건이 생기며, 프로젝트 완료는 17단계 납품 완료와 18단계 최종 완료가 구분되지 않는다. 담당자 미지정 업무가 시스템 관리자에게 배정되고, `work_items.due_date`가 일정 원본과 분리되어 기한 알림이 부정확하다.
- 현재 우회 방식: 사용자가 PMS 목록과 외부 채널을 각각 확인하고, 잘못 배정되거나 누락된 업무를 사람이 전달한다.
- 이 기능이 없을 때의 영향: 알림 누락·중복·과다 발송, 최종 완료 의미 혼선, 관리자 오배정, 기한 알림 부정확성이 계속된다.

### 실제 구현 기준선 (Repository 재확인 결과)

- Delivery 파이프라인: `NotificationDispatcher`가 즉시 delivery 생성 → daily digest 생성 → claim/lease·fencing·attempt lineage 기반 발송을 수행한다 (`backend/src/Emi.Qms.Api/Notifications/NotificationDispatcher.cs`, `NotificationDeliveryStore.cs`). TASK-NOTIFY-REL-001/ESC-001/004의 idempotency·retry 계약이 유지되고 있다.
- 채널 enum: `TeamsChannel`, `TeamsDirectMessage`, `TeamsActivity`, `Mail`. 긴급(Blocking) 알림은 현재 `TeamsChannel`+`Mail` delivery를 생성하고, `TeamsActivity` 전략일 때 긴급·프로젝트 lifecycle(생성·납기·상태·재검사·완료) TeamsActivity delivery를 추가 생성한다.
- 담당자 해석: `WorkflowStore.ResolveAssigneeAsync`가 정담당 → 부담당 → 영업 정담당 → 영업 부담당 → 활성 `system-administrator` 1인 순서로 fallback한다. 이번 정책은 영업·관리자 fallback을 부서장 전원 fallback으로 대체한다.
- 부서장 데이터: `qms_users.is_department_head`와 부서 연결이 TASK-ADMIN-003에서 구현되어 있어 재사용 가능하다.
- Daily Digest: 07:30 `Asia/Seoul` 창은 구현되어 있으나 평일 필터가 없고, 활성 프로젝트 담당자라는 이유만으로도 수신 대상이 되어 “보낼 내용 없으면 미발송” 결정과 다르다.
- 에스컬레이션: `NotificationEscalationService`가 L0~L3을 모두 평가하며 L2 확대용 단계별 부담당 매핑(`WorkItemEscalationStore`)이 존재한다. 이번 정책은 L0(D-1)·L1(초과)만 운영한다.
- 사용자 설정: `NotificationPreferenceCatalog`(taxonomy `2026-07-v1`)가 선택 3종(자동 업무 Teams, D-1 Teams, Daily Digest 메일)과 잠금 항목(긴급, L1~L3)을 정의한다. Teams 개인 설정 key는 `TeamsDirectMessage` 상수로 고정되어 있어 실제 발송 채널이 `TeamsActivity`여도 설정 억제가 동작한다.
- Pending: 정상 Pending은 `Info`(외부 필수 채널 없음), 긴급 Pending은 `Blocking`으로 TeamsChannel·Mail delivery가 생긴다. 제조 중단은 긴급 Pending과 별도 참고 알림을 함께 만든다(`PendingStore.cs`). 재검사(`ReinspectionRequested`)는 TeamsActivity만 연결되고 메일이 없다.
- 프로젝트 완료: 단일 `ProjectCompletion` 개념만 있고 TeamsActivity는 영업 담당자 1인으로 제한된다. 17단계 납품 완료와 18단계 최종 완료의 분리는 미구현이다.
- 문서 drift: Roadmap 6.5장의 채널 매트릭스·L2/L3·Teams 공용 채널 Webhook 기술은 이번 확정 정책과 다르므로, 구현 Task에서 Decision Log 추가와 함께 동기화해야 한다(기존 결정 이력은 수정하지 않는다).

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 모든 활성 사용자 | 프로젝트 생성 알림 확인 | 본인 수신 알림 | 읽음·보관 상태 |
| 프로젝트 영업 담당자·지정 담당자 | 납기·상태·납품 완료 확인 | 담당 프로젝트 알림 | 읽음 상태 |
| 영업팀 전체 | 18단계 최종 완료 메일 확인 | 완료 프로젝트 안내 | 없음 |
| 업무 담당자 | 자동 배정·D-1·초과 알림 확인 | 본인 업무 | 허용된 개인 알림 설정 3종 |
| 활성 부서장 전원 | 담당자 없는 부서 업무 처리 | 소속 부서 fallback 업무 | 본인 몫 처리(한 명 처리 시 묶음 전체 종료) |
| Pending 원수신자 | 등록·종결·재검사·재조치 확인 | 본인 수신 Pending | 읽음 상태 |
| 시스템 관리자 | 설정·delivery 이력·장애 조사 | 운영 상태 | 현장 업무 fallback 수신 금지 |

### 확정 이벤트 × 수신자 × 채널 매트릭스

| 이벤트 | 수신자 | 인앱 | Teams 개인(Activity) | 메일 | 사용자 해제 |
| --- | --- | :-: | :-: | :-: | :-: |
| 일반 자동 업무 배정 | 업무 담당자 | O | O | 일일 요약 포함 | Teams만 가능 |
| 일반·긴급 Pending 등록 | Pending 수신 대상자 | O | O | O | 불가 |
| Pending 종결 | 등록 알림 원수신자 | O | O | X | 불가 |
| 제조 중단(긴급 Pending 통합 1건) | 조치·참고 대상자 | O | O | O | 불가 |
| 재검사 요청·재조치 | 일반 Pending과 동일 | O | O | O | 불가 |
| 프로젝트 생성 | 모든 활성 사용자 | O | O | O | 불가 |
| 납기일·상태 변경 | 영업 담당자+지정 담당자 | O | O | X | 불가 |
| 17단계 납품 완료 | 영업 담당자+지정 담당자 | O | O | X | 불가 |
| 18단계 최종 완료 | 영업팀 전체 | O(기록 원본) | X | O | 불가 |
| 예정일 D-1 | 현재 담당자 | O | O | X | Teams만 가능 |
| 예정일 하루 초과 | 현재 담당자 | O | O | O | 불가 |
| 평일 Daily Digest | 보낼 내용이 있는 사용자 | — | X | O (07:30) | 가능 |
| Teams 공용 채널 | — | 사용하지 않음 (delivery 생성 중단) | | | |

PWA Web Push는 별도 `TASK-PWA-PUSH-001` 구현이 위 최종 인앱 원본·묶음을 그대로 파생하며, 이 Task에서는 통합 검증만 수행한다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 담당자 없는 업무의 부서장 fallback

1. 단계 완료 이벤트가 발생했으나 프로젝트에 해당 부서 담당자가 지정되어 있지 않다.
2. 시스템이 영업 담당자·시스템 관리자 대신 해당 업무 부서의 활성 부서장 전원에게 같은 fallback 묶음의 업무와 배정 알림(인앱+Teams)을 생성한다.
3. 부서장 한 명이 업무를 완료하면 같은 묶음의 나머지 부서장 업무가 자동 종료되고, 처리자·동기화 종료 이력이 감사 가능하게 남는다.

### 시나리오 B — 일정 원본 기반 예정일과 기한 알림

1. 생산계획일 또는 구매품 입고예정일이 입력·변경된다.
2. 시스템이 연결된 미완료 `work_items.due_date`를 자동 계산·동기화한다. 정확한 원본이 없으면 `null`을 유지하고 기한 알림을 만들지 않는다. 완료된 업무는 재계산하지 않는다.
3. 담당자는 D-1에 Teams 알림을, 하루 초과 시 Teams+메일 알림을 받는다. L2·L3 확대 발송은 발생하지 않는다.

### 시나리오 C — Pending lifecycle

1. 검사자가 제조 중단을 등록하면 긴급 Pending 알림 한 건에 조치·참고 내용이 통합되어 수신 대상자에게 인앱+Teams+메일로 발송된다.
2. 재검사 요청과 재검사 불합격 재조치도 같은 필수 채널 정책(인앱+Teams+메일)으로 발송된다.
3. Pending이 종결되면 등록 알림 원수신자에게 인앱+Teams로 종결이 통지되고 메일은 발송되지 않는다.

### 시나리오 D — 프로젝트 lifecycle과 일괄 처리

1. 프로젝트가 생성되면 모든 활성 사용자에게 인앱+Teams+메일이 발송된다. 납기·상태 변경은 영업 담당자와 지정 담당자에게 인앱+Teams로 발송된다.
2. 17단계에서 모든 패널 납품이 완료되면 `프로젝트 납품 완료` 이벤트가 영업 담당자+지정 담당자에게 인앱+Teams로 발송되고, 18단계 최종 완료 시 영업팀 전체에게 메일만 발송된다.
3. 패널 10건을 일괄 처리하면 `work_items`는 패널별로 생성되지만 같은 프로젝트·단계·수신자의 인앱 알림은 한 건으로 묶이고, Teams·PWA도 같은 묶음 원본 한 건을 사용한다.

## 5. 기능 요구사항

### 필수

- [ ] 3장 매트릭스대로 이벤트별 수신자·채널 delivery 생성 경로를 정합화 (정상 Pending 메일 추가, 재검사·재조치 메일 추가, Pending 종결 통지, 납기·상태 변경 수신자 보정)
- [ ] Teams 공용 채널(`TeamsChannel`) delivery 생성 중단과 비활성 정책 정리 (기존 이력은 보존)
- [ ] 제조 중단의 긴급 Pending·참고 알림 2건을 통합 1건으로 변경
- [ ] 17단계 `프로젝트 납품 완료`와 18단계 `프로젝트 최종 완료` 이벤트 분리, 각 수신자·채널 적용
- [ ] 담당자 미지정 시 활성 부서장 전원 fallback 업무 생성과 한 명 처리 시 묶음 동기화 종료 (+감사 이력)
- [ ] 일정 원본(생산계획일·구매품 입고예정일) 기반 미완료 `work_items.due_date` 자동 계산·동기화·재계산, 원본 없으면 `null`
- [ ] D-1 Teams·하루 초과 Teams+메일 단순 기한 알림, L2·L3 평가·발송 제거
- [ ] Daily Digest 평일 한정 발송과 “보낼 내용 없으면 미발송” 판정 보정
- [ ] 일괄 처리 시 프로젝트·단계·수신자 기준 인앱 알림 묶음 (업무는 패널별 유지)
- [ ] 사용자 설정 3종(일반 업무 Teams, D-1 Teams, Digest 메일)만 해제 가능, 나머지 필수 잠금 유지, L2·L3 항목 catalog 정리
- [ ] PWA push가 최종 인앱 수신자·묶음 원본을 그대로 따르는 통합 검증

### 선택

- [ ] 관리자 운영 화면의 delivery 이력에서 새 이벤트 유형 라벨 표시 보강

### 명시적 제외

- [ ] Teams 공용 채널 Webhook 운영, L2·L3 확대 에스컬레이션, quiet hours
- [ ] 정확한 일정 원본이 없는 업무의 임의 예정일 대체, 인앱 알림 자동 삭제
- [ ] 새 외부 알림 채널·provider, 알림 세부 문구 후속 기획, PWA push 자체 구현

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 알림 센터(기존) | 상단 알림 | 묶음 인앱 알림, 읽음 상태, 필터 | 읽음 처리·상세 이동 | 기존 규격 유지 |
| 내 업무 목록(기존) | 업무 메뉴 | fallback 묶음 업무, 예정일 | 처리 완료 | 한 명 처리 시 동기화 종료 안내 |
| 알림 설정(`NotificationPreferencesPage`) | 사용자 설정 | 선택 3종 토글, 필수 잠금 사유 | 3종 on/off | 기존 저장 피드백 유지, L2·L3 잠금 행 제거 |
| 관리자 delivery 이력(기존) | 관리자 메뉴 | 채널·상태·attempt | 조회 | 변경 없음 |

확인할 UX 항목:

- delivery 실패를 사용자 업무 실패로 표시하지 않는다.
- fallback 업무가 자동 종료된 부서장에게 종료 사유가 이해 가능해야 한다.
- 기존 흑백 와이어프레임·공통 Action Feedback 규격과 390px·Teams narrow 동작을 보존한다.

## 7. 업무 규칙과 불변조건

- 모든 알림의 원본은 PMS 인앱 알림이다. 18단계 최종 완료도 인앱 원본(영업팀 수신)을 만들고 외부 채널은 메일만 생성한다(16장 2번의 확인 대상).
- 외부 채널 실패는 업무 저장·다른 채널 성공을 되돌리지 않으며, 재시도·append-only attempt lineage·at-least-once 계약을 유지한다.
- 같은 이벤트 재시도는 같은 인앱 알림·delivery를 중복 생성하지 않는다(idempotency key·dedupe 유지).
- 필수 알림은 사용자가 끌 수 없고, 선택 3종 외 설정을 추가하지 않는다.
- 담당자 fallback은 부서장 전원이며 시스템 관리자·영업 담당자로 내려가지 않는다. 임의의 부서장 1인을 선택하지 않는다.
- fallback 묶음은 한 명 처리 시 나머지가 원자적으로 종료되고 처리자·종료 이력이 보존된다. 동시 처리 경쟁에서 이중 완료가 발생하지 않아야 한다.
- 신뢰할 일정 원본이 없으면 `due_date=null`이며 프로젝트 납기일·수동 입력으로 대체하지 않는다. 완료 업무의 예정일 이력은 불변이다.
- 인앱 알림은 기간 기준 자동 삭제·숨김하지 않는다.
- 개발·자동 테스트는 실제 Teams·메일·PWA를 발송하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| notification / recipient / delivery / attempt | 인앱 원본과 채널별 발송 이력 | 기존 재사용 | append-only attempt lineage 유지 |
| fallback 업무 묶음 식별자 | 같은 부서장 전원에게 표시되는 동일 업무의 묶음과 동기화 종료 상태 | 신규(additive migration) | 처리자·자동 종료 시각·사유 보존 |
| `work_items.due_date` 일정 원본 연결 | 생산계획일·입고예정일 → 미완료 업무 예정일 동기화 근거 | 기존 컬럼 + 신규 연결 규칙 | 완료 업무 재계산 금지 |
| 이벤트 유형 | `프로젝트 납품 완료`(17단계)와 `프로젝트 최종 완료`(18단계) 분리 | 기존 `ProjectCompletion` 확장 | 기존 이력 재해석 금지 |
| preference catalog | 선택 3종 + 필수 잠금, L2·L3 항목 정리 | 기존 코드 catalog 개정(taxonomy version 상향) | 기존 preference 원장·audit 보존 |

```text
원업무 생성 → 인앱 수신자 확정 → 채널별 delivery Pending → Sent/Failed/Suppressed (+재시도)
fallback 묶음: 활성(부서장 N명) → 한 명 완료 → 나머지 자동 종료(감사 이력)
due_date: 원본 일정 입력·변경 → 미완료 업무 재계산 / 원본 없음 → null 유지
```

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 수신자 확정, 채널 매트릭스, fallback 대상 계산, 묶음 동기화 종료, due_date 계산, 필수 알림 잠금.
- 필요한 조회와 mutation: 기존 workflow·pending·project·escalation store의 알림 생성 경로 수정이 중심이다. 신규 공개 API는 최소화하고 기존 endpoint 계약을 유지한다.
- 권한·validation: 부서장 fallback은 활성 사용자·부서 소속·`is_department_head`를 기준으로 하며, 관리자 fallback 경로를 제거한다.
- transaction·동시성·idempotency: fallback 묶음 종료는 같은 transaction에서 원자적으로 처리하고(경쟁 시 최초 완료 1건만 유효), delivery 생성은 기존 dedupe key 패턴을 따른다. 일괄 처리 묶음은 기존 `group_key`·batch window를 재사용하되 인앱 원본 자체를 묶음 1건으로 생성한다.
- audit trail: fallback 처리자·자동 종료, due_date 자동 변경, 정책 전환 시점의 delivery 상태를 기존 audit·attempt 구조로 남긴다.
- 외부 provider 영향: provider 종류는 추가하지 않는다. `TeamsChannel` handler는 delivery 생성 중단으로 자연 비활성화하고 설정·이력은 보존한다. 자동 테스트는 dry-run/fake provider만 사용한다.

Repository 조사 전 내부 클래스명, 컬럼명과 SQL 형태를 확정하지 않는다.

## 10. Frontend 고려사항

- route/component: 기존 알림 센터·업무 목록·`NotificationPreferencesPage` 유지. 신규 화면 없음.
- loading/empty/error/success: 기존 공통 규격 유지.
- 공통 Action Feedback: 설정 저장·업무 완료 기존 패턴 재사용.
- 접근성: 잠금 항목 사유 텍스트와 토글 label 유지.
- 390px/mobile/narrow pane: 기존 동작 보존을 회귀 확인.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: `WorkflowStore` 단계 핸드오프, `PendingStore`, `ProjectStore` lifecycle 이벤트, `NotificationDeliveryStore` delivery 생성이 직접 영향 범위다.
- 권한/관리자: TASK-ADMIN-003의 부서·부서장 데이터를 재사용한다. 관리자 fallback 제거로 시스템 관리자의 현장 업무 수신이 사라진다.
- Excel/PDF/첨부: 변경 없음.
- Teams/Mail: TASK-NOTIFY-003 Activity provider와 Gmail SMTP 경로 재사용. 공용 채널 Webhook은 운영 제외.
- 삭제·복구/감사: TASK-NOTIFY-005/AUDIT-001 preference 원장과 TASK-NOTIFY-REPROCESS-001 재처리 계약을 훼손하지 않는다.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) fallback: 묶음 식별자 + 부서장별 개별 업무 | 부서장 수만큼 `work_items`를 만들고 신규 묶음 식별자로 연결, 한 명 완료 시 같은 transaction에서 나머지 종료 | 기존 업무 목록·권한 모델 재사용, 처리자 감사 명확 | additive migration 필요, 동시 완료 경쟁 테스트 필요 |
| B fallback: 공유 업무 1건 다중 owner | 업무 1건을 부서장 전원이 보게 확장 | 묶음 종료 로직 불필요 | 기존 단일 `assigned_user_id` 모델·목록·권한 대폭 변경, 회귀 위험 큼 |
| A (권장) due_date: 원본 저장 시점 동기화 | 생산계획·입고예정일 저장 transaction에서 연결된 미완료 업무를 즉시 재계산 | 지연 없음, 원본과 항상 일치 | 저장 경로별 연결 규칙 구현 필요 |
| B due_date: 주기 worker 재계산 | 스케줄러가 주기적으로 전체 재계산 | 저장 경로 수정 최소 | 반영 지연, D-1 판정 부정확 위험, 불필요한 부하 |

권장안은 두 항목 모두 A이며, 사용자 확정 정책(부서장 전원·자동 갱신)을 기존 모델 위에서 최소 변경으로 구현한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 이 Task의 구현·자동 검증은 Persistent UAT에 write하지 않는다. 운영 적용은 별도 승인 경계.
- migration 필요 여부: 있음 — fallback 묶음 식별자 등 additive migration(다음 번호, forward-fix 원칙). 기존 완료 업무 데이터는 수정하지 않는다.
- 외부 발송/실제 데이터 영향: 자동 테스트는 실제 Teams·메일·PWA 무발송. 실제 provider 검증은 별도 승인된 안전 계정·대상에서만 수행(16장 1번).
- runtime 교체 여부: 이 Task 자체는 없음. 운영 반영은 기존 Azure release 승인 절차를 따른다.
- 추가 사용자 승인 필요 작업: due_date backfill 실행 범위(16장 3번), 운영 provider 활성 정책 전환, `main` merge·배포.

## 14. 검증 계획

- 최소 테스트: Backend 이벤트별 수신자·채널 매트릭스 단위 테스트(정상·긴급 Pending, 종결, 제조 중단 통합 1건, 재검사·재조치, 프로젝트 생성·납기·상태, 17/18단계 분리, TeamsChannel 미생성).
- 영향 영역 회귀: idempotency·dedupe, claim/lease·attempt lineage, preference 억제(선택 3종·필수 잠금·L2/L3 제거), 부서장 fallback 동시 완료 경쟁, due_date 동기화·재계산·null 유지·완료 불변, digest 평일·빈 내용 미발송, D-1·초과 알림, escalation L2/L3 미발송.
- PR/CI: 기존 Backend 격리 회귀와 Frontend lint·typecheck·unit·build, Validation Matrix 기준 적용. 실제 provider 호출 0을 delivery 상태로 확인.
- 사용자 검수: 역할별 checklist — 알림 설정 3종 토글, 주요 인앱 알림, 복수 부서장 업무 처리·자동 종료, 프로젝트 생성·변경·납품·최종 완료, Pending 등록·종결·재검사 흐름, PWA push가 인앱 원본과 일치하는지 통합 확인.

## 15. 완료 기준

- 기능/권한/데이터: 3장 매트릭스와 7장 불변조건이 자동 테스트로 검증되고, 관리자 fallback·공용 채널·중복 제조 중단 알림이 발생하지 않는다.
- UX: 기존 화면 규격·좁은 화면 동작 보존, 설정 화면이 개정 catalog를 정확히 표시.
- 자동 테스트: 신규·회귀 전체 통과, 실제 provider 무발송 확인.
- 5종 산출물: Implementation report·SOP·User manual·Roadmap update(6.5장 동기화, Decision Log 추가)·User validation checklist 상태·위치 추적.
- 사용자 검수 상태: `사용자 검수 대기`로 handoff 후 사용자 확인.
- PR 상태: 검수 완료·승인 전 Draft 유지, merge는 별도 승인 1회.

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 실제 provider 운영 검증과 공개 배포 시점·대상 (interview에서 명시적 deferred) | A. 구현·자동 검증 완료 후 안전 계정 smoke → 운영 활성 승인 (권장) / B. 다음 Azure release와 묶어 일괄 승인 | 대기 |
| 2 | 18단계 최종 완료의 인앱 기록 원본 범위 | A. 영업팀 수신 인앱 원본 생성 + 메일 delivery만 (권장 — “인앱이 모든 알림의 원본” 불변조건과 정합) / B. 인앱 원본 없이 메일 단독 발송 (원본 불변조건 예외 필요) | 대기 |
| 3 | 기존 미완료 업무 `due_date` backfill 범위 | A. 정확한 일정 원본이 연결되는 미완료 업무만 migration에서 1회 계산, 나머지 `null` 유지 (권장) / B. backfill 없이 신규·변경 일정부터만 적용 | 대기 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `Notifications/`(delivery 생성·catalog·escalation·digest), `Workflow/WorkflowStore.cs`(fallback·묶음·17/18단계), `Pending/PendingStore.cs`(채널·통합·종결), `Projects/ProjectStore.cs`(lifecycle 이벤트), 일정 저장 경로(`ProductionPlanning/`, `Procurement/`)의 due_date 동기화.
- Frontend: `NotificationPreferencesPage.tsx`와 관련 client 모듈의 catalog 표시.
- DB/Migration: fallback 묶음 식별자 additive migration, 승인 시 due_date backfill.
- Tests/Scripts: `NotificationDeliveryTests.cs` 확장과 workflow·pending 관련 테스트.
- Docs: Roadmap 6.5장 동기화, SOP·User manual, 이 Task 산출물.

## 18. Roadmap 연결

- 선행 Task: TASK-NOTIFY-001~005, REL/ESC/AUDIT/REPROCESS, TASK-ADMIN-003(부서장), TASK-AZURE-DEPLOY-001 Change 009(공개 Activity type 연결).
- 후속 Task: TASK-PWA-PUSH-001 통합 검증·승격, 운영 provider 활성 전환, 알림 문구 세부 기획.
- 현재 Go/No-Go: 사용자 승인 아래 TASK-AZURE-DEPLOY-001과 병렬 진행. 구현 승인 전 코드 변경 없음.
- 별도 Task로 분리할 항목: PWA push 구현, 확대 에스컬레이션 고도화, Teams 공용 채널 재도입(필요 시).

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-08-11 | 인터뷰 7개 round·14개 결정 확인, “기획 초안 작성 후 Codex 검토·보고” 지시 | 이 planning draft 작성 입력으로 사용 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

승인 전 실행하지 않는다. 승인 후 새 Codex 구현 세션 기준의 초안이다.

1. 승인된 이 planning과 `tasks/notify-policy-001-review.md` resolution만 구현 계약으로 사용한다. 인터뷰 원장(`tasks/notify-policy-001.md`)과 충돌하면 중단하고 보고한다.
2. 구현 순서 권장: (a) delivery 채널 매트릭스 정합화(TeamsChannel 중단, Pending·재검사 메일, 종결 통지, 제조 중단 통합) → (b) 17/18단계 이벤트 분리 → (c) 부서장 fallback 묶음 + migration → (d) due_date 동기화·기한 알림 단순화(L2/L3 제거) → (e) digest 평일·빈 내용 보정 → (f) preference catalog 개정과 Frontend 표시 → (g) 일괄 묶음 인앱 원본 → (h) PWA 원본 일치 통합 검증.
3. 각 단계에서 성공·차단·경쟁·실패 경로 테스트를 추가하고 실제 provider를 호출하지 않는다. Persistent UAT에 write하지 않는다.
4. 기존 idempotency·claim/lease·attempt lineage·preference audit 계약을 변경하지 않고, main 반영 migration을 수정하지 않는다.
5. Roadmap 6.5장 동기화와 Decision Log 추가, 5종 산출물, user validation checklist를 Task 종료 정책대로 기록한다.
6. 16장 미결정 3건은 사용자 결정이 기록되기 전 관련 부분을 구현하지 않는다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 3
