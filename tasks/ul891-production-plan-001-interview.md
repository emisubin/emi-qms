# TASK-UL891-PRODUCTION-PLAN-001 — UL891 세트별 생산계획 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5_EXPERIMENT_FAST_TRACK`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 현재 `experiment/*` branch에서 사용자가 요청한 UL891 세트별 생산계획의 interview source of truth다. 사용자는 이 branch의 신규 기능을 사용자-facing interview·중간 승인 없이 `Fable 1차 기획 → Codex 내용 review → Fable 2차 기획 → Codex 구현·검증·screenshot → local commit`까지 이어가도록 명시했다. 아래 비차단 선택은 Fable의 Repository 근거 권장안을 자동 채택한다. 대표 repo, GitHub `main`, push·PR·merge, Persistent UAT와 실제 provider는 제외한다.

## Task Identity Gate

- gateSource: `tasks/ul891-production-plan-001-identity-gate.md`
- gateStatus: `PASS_CREATE`
- canonicalTaskId: `TASK-UL891-PRODUCTION-PLAN-001`
- explicitRoadmapOverrideApproved: `true`
- policyInputResolution: `FABLE_RECOMMENDATION_AUTO_ADOPT`

## 사용자 실행 지시

- 요청일: 2026-07-28
- 요청: Item별 생산계획 차이를 인정하고, UL891은 세트 단위 생산계획을 관리한다. 생산관리 탭의 생산계획표와 일정표 안에 scope tab을 두어 세트별 계획표와 일정표를 설정한다.
- standing rule: 이 experiment branch에서는 신규 기능도 인터뷰·중간 승인 없이 Fable 2-pass 권장안과 Codex 구현을 결과까지 진행한다.
- 게시 경계: local experiment만 승인. `main` merge 승인 `0/3`.

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: UL891 프로젝트는 `세트 사양 → 실물 세트 인스턴스 → 개별 물리 패널` 구조로 생성되지만 생산계획은 프로젝트에 한 벌만 존재한다.
- 해결할 문제: 여러 세트의 착수·완료·출하 시점이 달라도 프로젝트 전체 계획 한 벌로만 보여 세트별 계획과 실제 진행을 비교할 수 없다.
- 성공했을 때 사용자가 할 수 있는 일: 생산관리 탭에서 전체와 각 세트를 전환하고, 동일한 생산계획 항목을 세트별로 계획·배치하며 해당 세트 패널의 실제 제조·품질·물류 실적을 바로 비교한다.
- 하지 않을 경우 영향: 프로젝트 패널 수가 커질수록 어떤 세트가 예정 대비 빠르거나 늦는지 알 수 없고, 생산관리 담당자가 별도 문서로 세트 일정을 다시 관리해야 한다.

## 2. 확정된 Repository 계약

- 신규 UL891 세트형 프로젝트만 `structure_mode='Ul891Set'`을 사용한다. 비-UL891과 기존 평면 UL891은 기존 프로젝트 단위 생산계획을 유지한다.
- 같은 세트 사양을 여러 개 주문하면 구성 정의는 공유하지만 실물 세트 인스턴스와 개별 패널은 각각 독립 identity·상태를 가진다.
- 제조·LQC·OQC·전진검수·FAT·포장·출발·납품은 개별 패널 원자다. 부분출하도 허용한다.
- Item별 생산계획 양식·제조 양식·실적 연결은 프로젝트 생성 시 snapshot되며 이후 master 변경으로 기존 프로젝트를 바꾸지 않는다.
- 자동 실적은 부서 원본 데이터에서 조회 시 파생하고 사용자가 직접 수정하지 않는다.
- 생산계획 항목은 계획 기간·담당자·필요 인원·생산관리 코멘트를 갖고 내부 실적 연결 설정을 보존한다.
- 생산관리 정·부 담당자만 수정하고 다른 부서는 조회만 한다. 서버 권한이 최종 기준이다.

## 3. Fable이 권장해야 할 비차단 선택

| 번호 | 결정 대상 | 비교할 경계 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- |
| 1 | 세트 계획 원자 | 세트 사양별 한 계획 vs 실물 세트 인스턴스별 독립 계획 | Fable 권장안 자동 채택 | No |
| 2 | 전체 scope 의미 | 기존 독립 프로젝트 계획 유지 vs 활성 세트 계획 aggregate | Fable 권장안 자동 채택 | No |
| 3 | 초기값·추가 세트 | 프로젝트 생성/추가 시 어느 계획 snapshot을 복제할지 | Fable 권장안 자동 채택 | No |
| 4 | 실적 source scope | 패널 귀속 source의 세트 필터와 구매·자재·IQC 같은 프로젝트 공통 source 표시 | Fable 권장안 자동 채택 | No |
| 5 | 취소 세트 | 숨김·read-only 이력·집계 제외 방식 | Fable 권장안 자동 채택 | No |
| 6 | 기존 세트형 프로젝트 | migration backfill, lazy 생성 또는 신규 프로젝트부터 적용 | Fable 권장안 자동 채택 | No |
| 7 | 많은 세트의 tab UX | 가로 tab, 검색/선택, 사양 group과 390px 표현 | Fable 권장안 자동 채택 | No |
| 8 | workflow 완료 판정 | 프로젝트 계획 완료와 모든 활성 세트 계획 완료의 관계 | Fable 권장안 자동 채택 | No |

## 4. 정상·예외·복구 흐름

- 정상: UL891 프로젝트 생성과 같은 transaction에서 활성 실물 세트별 계획 snapshot 생성 → 생산관리 탭에서 전체/세트 scope 선택 → 세트별 계획 기간·담당자·필요 인원·코멘트 저장 → 부서가 기존 화면에서 패널 업무 처리 → 선택 세트의 자동 실적과 일정 막대 갱신.
- 세트 추가: 프로젝트 생성 뒤 새 사양·세트가 추가되어도 기존 세트 계획은 바꾸지 않고 새 세트에만 일관된 계획 snapshot을 생성한다.
- 세트 취소: 실행·발주·회수 정책은 기존 UL891 계약을 유지하며 취소 세트가 활성 계획 완료율과 workflow를 왜곡하지 않는다. 이력은 잃지 않는다.
- validation: 다른 프로젝트/세트 identity, 비활성·취소 scope, stale row version, 역전 날짜, 잘못된 담당자·필요 인원을 서버가 field-level 오류로 거부한다.
- 동시성: 세트별 revision/CAS로 다른 세트의 정상 저장을 막지 않고 같은 세트 stale 수정만 409로 차단한다.
- 부분 실패: 계획 row·revision·audit은 한 transaction으로 저장하고 실패 시 해당 세트 변경 전체를 rollback한다.

## 5. Data·integration·lifecycle

- 기존 data 재사용: UL891 spec/instance/panel 관계, LinkedV1 project plan item/connection snapshot, 자동 실적 projection, 계획 담당자·필요 인원·코멘트, field audit.
- 신규 후보: 세트 인스턴스별 계획 scope와 revision 또는 기존 project plan item을 세트 scope로 연결하는 additive 구조.
- source scope: 패널에 귀속되는 실적은 선택 세트의 active 패널만 집계한다. 세트에 귀속되지 않는 프로젝트 공통 source를 특정 세트의 독립 실적으로 가장하지 않는다.
- 보존: master 양식과 기존 프로젝트 snapshot, 완료된 부서 원본·audit를 수정하지 않는다. 취소·변경도 hard delete보다 상태·이력 보존을 우선한다.
- attachment·Excel·PDF: 이번 기능의 핵심은 web 계획표·일정표다. 기존 생산계획 Excel 호환은 보존하되 신규 세트별 export는 Fable이 최소 필요성을 판단한다.
- 외부 연동·notification: 신규 Teams·메일·내 업무 종류를 만들지 않는다.
- migration: additive 다음 번호를 사용하며 fresh DB와 기존 `0063` DB upgrade, 기존 평면·LinkedV1·UL891 세트 프로젝트를 모두 검증한다.

## 6. UX와 운영 적용

- 진입: 프로젝트 상세 `생산관리` 탭의 기존 생산계획표와 계획·실적 일정표 영역 안에 같은 scope selector를 둔다.
- 핵심 행동: 전체 또는 세트를 선택하면 계획표와 일정표가 함께 같은 scope로 바뀐다. 선택한 세트 label·사양·패널 수를 명확히 표시한다.
- PC: 많은 세트가 있어도 현재 scope와 전환 방법이 분명하고 8열 표 정렬·일정 날짜 축을 유지한다.
- 모바일: PC table 축소본이 아니라 scope selector와 세트 요약·계획 카드·실제 가로 막대를 한 열로 제공하고 page-level overflow를 만들지 않는다.
- feedback: loading·empty·error·success, 중복 submit 잠금, stale 안내, 취소 세트 read-only를 구분한다.
- rollout: isolated DB·고정 experiment runtime에서만 검증한다. Persistent UAT와 main은 변경하지 않는다.

## 7. 포함·제외 범위

### 포함

- UL891 세트형 프로젝트의 세트별 생산계획 persistence·조회·수정·audit
- 프로젝트 전체와 세트별 생산계획표·일정표 scope tab
- 선택 세트 패널에 한정한 제조·품질·물류 자동 실적
- 프로젝트 공통 source의 오해 없는 표시 원칙
- 신규/추가/취소 세트와 기존 세트형 프로젝트 lifecycle
- 권한·CAS·validation·fresh/existing migration·Backend/Frontend/isolated test
- desktop·390px privacy-safe 시각 증빙

### 제외

- 비-UL891과 기존 평면 UL891 생산계획 UX 변경
- master 생산계획·제조 양식 version 정책 변경
- 패널 제조·품질·물류 처리 단위 변경
- 세트별 판매단가·납기·원가·BOM 신규 입력
- 신규 알림·내 업무·Teams·메일·ERP/MES 연동
- 대표 repo·`main`·Persistent UAT·push·PR·merge

## 8. 성공 기준

- UL891 세트형 프로젝트에서 전체와 활성 실물 세트별 scope를 전환하면 생산계획표와 일정표가 함께 전환된다.
- 한 세트의 계획을 수정해도 다른 세트와 master 양식은 변하지 않는다.
- 패널 귀속 실적은 선택한 세트 패널만 집계하고 프로젝트 공통 source는 공통임을 명확히 표시한다.
- 새 세트 추가·세트 취소·stale 저장·권한 없음·기존 프로젝트 upgrade가 데이터 손실 없이 처리된다.
- 비-UL891과 평면 UL891의 기존 프로젝트 단위 생산계획이 회귀하지 않는다.
- Backend/Frontend 전체 회귀, fresh/existing migration, isolated Full-Stack, desktop/390px browser 검증이 통과한다.

## 9. Fable 확인용 요약

- 해결할 문제: 세트 계층이 있는 UL891도 현재 생산계획은 프로젝트 한 벌이라 세트별 일정·실적을 비교할 수 없다.
- 권장 범위: 실물 세트별 계획 원자, 전체 aggregate, 패널 원본 filtered actual, 프로젝트 공통 source 표시, 기존 프로젝트·비-UL891 불변.
- 확정한 정책: 사용자-facing interview·중간 승인 생략, Fable 권장안 자동 채택, local experiment only.
- 명시적 제외: master 양식 재설계, 실행 원자 변경, 신규 알림·외부 연동, main/UAT.
- Deferred 비차단 결정: 세트 scope 원자·aggregate·backfill·tab UX·workflow 판정은 Fable이 Repository 근거 권장안을 확정한다.
- Fable 판정: `COMPLETED_CONFIRMED`

## 10. 사용자 확인

- [x] 업무 문제와 기대 결과가 정확하다.
- [x] 대상 역할과 권한이 정확하다.
- [x] 정상·예외·복구 흐름이 정확하다.
- [x] 포함·제외 범위가 정확하다.
- [x] Blocking 결정이 남아 있지 않다.
- [x] experiment standing instruction에 따라 Fable 권장안을 planning 입력으로 자동 채택한다.
