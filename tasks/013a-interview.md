# TASK-013A — 물류 포장·출발·납품 완료 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5_EXPERIMENT_FAST_TRACK`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 Fable 5가 `TASK-013A`를 기획하기 위한 interview source of truth다. 사용자는 이 `experiment/*` worktree에서 사용자-facing interview와 중간 승인 없이 Fable 권장안을 채택해 `Fable 1차 기획 → Codex 내용 review → review 기반 Fable 2차 기획 → Codex 구현·검증·페이지별 screenshot·local commit`까지 연속 진행하도록 명시했다. 아래에는 Roadmap, TASK-012A의 panel별 `PackingCompleted` skeleton handoff와 실제 Repository에서 확인된 계약만 기록한다. 미확정 Packing Unit 상세 필드·포장 구성·서명본 형식·사진 개수는 Fable의 비차단 권장안 대상으로 남긴다. 대표 repo, GitHub `main`, Persistent UAT, 실제 provider와 canonical runtime은 변경하지 않는다.

## Task Identity Gate

- proposedTaskId: `TASK-013A`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-013A`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 품질 완료 panel을 하나 이상의 Packing Unit에 정확히 매핑해 필수 포장 사진으로 포장을 확정하고, 필수 상차 사진·출발일을 거쳐 거래명세서 서명본과 함께 납품 완료한 뒤 영업 정산 skeleton으로 인계한다.
- Root Finding 또는 정책 결정: TASK-012A는 panel target `PackingCompleted` skeleton 업무까지만 생성하며 실제 포장 구성·증빙·출발·납품 record와 전용 화면이 없다. generic 내 업무 완료로는 필수 증빙·panel mapping·단계 순서·일괄 처리·감사를 보장할 수 없다.
- 변경·검증 경계: 현재 experiment 계보의 additive migration·Backend·Frontend·isolated PostgreSQL·synthetic data·desktop/390px screenshot만 포함한다.
- 보존할 불변조건: 18단계 `품질 완료 → 포장 완료 → 출발 처리 → 납품 완료 → 영업 정산` 순서, panel 단위 전진-only, Backend 권한·project scope, 필수 증빙, 확정 이력 append-only, open Pending 차단, 실제 provider 차단, 대표 repo·main·Persistent UAT 불변.
- 예상 산출물: Fable 1차 planning 원문, Codex 내용 review, Fable 2차 planning 원문, 구현·자동 검증·desktop/mobile screenshot·implementation report·local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

Roadmap의 canonical `TASK-013A` 한 건 외에 같은 목적의 Task 문서·local/remote branch·worktree·open/merged PR은 0건이다. TASK-012A는 품질 합격과 panel별 포장 skeleton까지만 구현해 이번 목적과 중복되지 않는다.

## 사용자 실행 지시

- 사용자 요청일: 2026-07-17
- 실행 형태: 현재 실험 worktree에서 다음 미착수 기능을 즉시 진행
- workflow: Fable 1차 기획 → Codex 내용 review → Fable 2차 기획 → Codex 구현·검증·screenshot·local commit
- 승인 대체: 비차단 제품 선택은 Fable의 Repository 근거 권장안을 자동 채택한다.
- 모바일 원칙: PC 화면을 줄인 반응형이 아니라 물류 담당자의 현장 행동을 재구성한 적응형 화면, 작은 글씨·도형으로 핵심 정보 밀도 확보, 좌상단 숨김 메뉴, 다양한 도형을 사용한다.
- 안전 예외: Repository 충돌, secret·개인정보 노출, 18단계 순서·panel 전진-only·필수 증빙·확정 이력·Pending 차단 무결성 위반은 fast-track으로 우회하지 않고 blocking decision으로 반환한다.
- 게시 경계: push·PR·merge 미승인, main merge 승인 `0/3`.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `COMPLETED_CONFIRMED` | 0 | 사용자 standing experiment 규칙과 Roadmap·012A 계약 기록. 미확정 정책은 Fable 권장안 자동 채택 | Fable 1차 planning |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 품질 최종 합격 시 panel target `PackingCompleted` 내 업무는 생성되지만 link는 임시 workflow fallback이고 포장 단위·패널 구성·포장 사진·상차·납품 증빙 record가 없다.
- 해결할 문제: 물류 담당자가 모바일에서 품질 완료 panel을 고르고 포장 단위를 구성해 필수 사진으로 확정한 뒤, 상차·출발과 고객 납품 증빙을 순서대로 남기고 다음 담당자에게 정확히 인계해야 한다.
- 현재 우회 방식: 포장번호·패널 구성·사진·출발일·거래명세서를 종이·사진첩·메신저로 따로 관리하고 workflow는 skeleton 또는 generic 업무 action으로만 해석한다.
- 성공했을 때 사용자가 할 수 있는 일: 하나 이상의 panel을 Packing Unit으로 묶어 포장 완료하고, 출발 대상 unit/panel을 일괄 선택해 상차 증빙과 출발일을 기록하며, 납품 대상 panel을 거래명세서 서명본과 함께 완료 처리한다. 모든 active panel 납품 완료 시 영업 정산 skeleton이 정확히 한 번 생성된다.
- 하지 않을 경우 영향: 품질 이후 15~17단계가 시스템 밖에 남아 어떤 panel이 어느 포장에 들어갔고 무엇이 출발·납품됐는지 알 수 없으며, TASK-014A 완료 조건의 근거가 없다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 물류 정·부 담당 | 포장 queue·Packing Unit 구성·사진·확정, 출발·납품 처리 | 기존 project access와 물류 업무 panel | 자신이 배정되거나 active work assignee인 포장·출발·납품 record | actor·시각·panel mapping·필수 증빙·operation 감사 |
| 영업 정·부 담당 | 납품 완료 상태와 거래명세서 증빙 조회, 후속 정산 업무 수신 | 기존 project access | 물류 mutation 불가 | 모든 active panel 납품 뒤 정산 skeleton exactly-once |
| 생산관리·품질·제조·조회 역할 | 허용 project의 포장·출발·납품 상태 조회 | 기존 project access | 물류 mutation 불가 | scope 밖 식별자 비노출 |
| Pending 조치 담당자 | 납품 차단 이슈 조치·상태·댓글 | 기존 Pending scope | 기존 Pending 계약 안에서만 | open blocking Pending이 있으면 다음 물류 action 차단 |
| System Administrator | 기준·이력 조회 | 기존 관리자 정책 | 물류 업무 입력 무제한 우회 금지 | 서버 authorization과 감사 유지 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: 품질 최종 합격 → panel `PackingCompleted` skeleton → Packing Unit 구성·포장사진 → 포장 확정 → 출발 업무 → 상차사진·출발일 → 출발 확정 → 납품 업무 → 거래명세서 서명본 → 납품 확정 → 모든 active panel 완료 시 영업 정산 skeleton.
- validation 실패: 품질 미완료 panel, 다른 project panel 혼합, 이미 다른 active/finalized packing에 포함, 빈 구성, 필수 사진·날짜·서명본 누락, 단계 순서 위반, open blocking Pending, stale version, scope·권한 불일치를 서버가 안정적인 한글 오류로 차단한다.
- 동시 처리·중복: panel은 같은 물류 단계에서 active record 최대 1개이며, group action·work item·project event는 operation fingerprint·row lock·unique key로 중복을 막는다.
- 취소·재시도·복구: network 재시도는 완료 결과를 중복 생성하지 않는다. 확정 전 draft 구성·증빙은 bounded하게 수정할 수 있고 확정 뒤 record·mapping·증빙은 덮어쓰지 않는다. 프로젝트/panel 취소는 진행 중 record/work만 terminal 정리하고 확정 증빙은 보존한다.
- 부분 실패와 rollback: Packing Unit 확정·panel stage·현재 업무 완료·다음 업무, 출발·납품 확정·다음 업무·project event는 각각 한 transaction에서 전부 성공하거나 전부 rollback한다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: Packing Unit, unit-panel mapping, 단계별 evidence, departure batch/record, delivery record, 거래명세서 signed evidence, operation/version. 실제 구조와 group boundary는 Fable 권장안으로 정한다.
- 상태 전이: panel별 `PackingRequested → Packed → DepartureRequested → Departed → DeliveryRequested → Delivered`. coarse panel stage와 project workflow event는 전진-only이며 실패나 Pending으로 단계 번호를 후퇴시키지 않는다.
- 보존·감사·삭제: 확정 Packing Unit 구성·사진, 출발 사진·일시, 납품 서명본·시각을 hard delete·덮어쓰기하지 않는다. approved permanent project purge는 기존 승인된 FK 역순 정합만 보강한다.
- attachment·Excel·PDF: 포장·상차 사진은 필수다. 거래명세서 서명본의 JPEG/PNG/PDF 허용 여부, 파일 크기·개수는 기존 bounded evidence 패턴을 근거로 Fable이 최소안을 권장한다. Excel·전자서명 생성은 제외한다.
- 외부 연동·notification: 기존 인앱 work item·notification 원본만 재사용하며 실제 Teams/Mail/Activity provider는 실행하지 않는다.
- migration·기존 데이터: current latest `0035` 다음 additive migration을 사용한다. 기존 품질/Pending/workflow migration을 수정하지 않고 기존 panel에 가짜 포장·출발·납품 record를 backfill하지 않는다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 전역 `물류` 공통 진입에서 단계 queue를 제공하고 내 업무 deep link가 정확한 project·panel·unit을 연다. 모바일은 `오늘 할 일/단계 → panel 선택 → 증빙 촬영 → 확인` 중심이고 desktop은 project queue·unit 구성·단계 현황·이력을 함께 본다.
- loading·empty·error·success feedback: queue loading/empty/error, 업로드 중·확정 완료·다음 단계·차단 이유를 action 가까이 표시하고 중복 submit을 차단한다.
- 접근성·390px·Teams narrow: PC table 축소가 아닌 compact stage switch·panel selection·one-column evidence·in-flow action, 44px touch target, 작은 보조 글씨, 좌상단 숨김 메뉴·page-level overflow 0을 유지한다. 원형·타원형·각진/둥근 직사각형·정사각형을 상태·행동 의미에 맞게 사용한다.
- UAT와 rollout: isolated synthetic PostgreSQL·provider disabled만 사용한다. Persistent UAT migration·runtime handover는 미실행한다.
- rollback과 운영자 대응: 적용 전에는 local branch 폐기로 종료할 수 있다. migration 적용 후에는 additive forward-fix와 확정 증빙·work item 보존으로 복구한다.

## 6. 포함·제외 범위

### 포함

- 품질 완료 panel queue와 Packing Unit 생성·panel mapping·draft/확정
- 필수 포장사진, 필수 상차사진·출발일, 필수 거래명세서 서명본·납품 완료
- panel 단위와 안전한 일괄 선택, 단계별 현재/완료 이력
- panel별 다음 업무 즉시 생성과 모든 active panel 기준 project stage event·영업 정산 skeleton exactly-once
- 물류 정·부 담당/current work assignee·`logistics.ship`·project scope 서버 권한
- generic 내 업무 완료 우회 차단, open blocking Pending·선행 단계·stale·중복 차단
- operation fingerprint/replay와 확정 증빙 append-only
- 모바일 우선 adaptive 물류 화면과 desktop 관리/조회 composition
- additive migration, transaction·idempotency·authorization·Frontend·isolated E2E 검증

### 제외

- 실제 운송사·기사 계정, 외부 고객 portal, GPS·차량·송장·택배 API
- 전자서명 생성, 거래명세서 문서 생성·PDF 양식, OCR·바코드·QR scan
- 포장방식 기준정보 관리자, 자동 규격·중량 계산, pallet/container 최적화
- 포장 해제·재포장·출발 취소·납품 정정·반품·분할 납품 고도화
- Excel export, 신규 외부 알림 채널·실제 provider delivery
- 영업 세금계산서·프로젝트 완료 상세(`TASK-014A`)
- Persistent UAT migration·write·runtime handover
- 대표 repo·GitHub main·push·PR·merge

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | Packing Unit 구성 | panel별 단독 포장은 단순하지만 일괄 포장을 표현하지 못하고, 무제한 계층은 과도하다 | 같은 project의 준비된 panel 1개 이상을 flat unit 한 건에 묶고 panel은 확정 unit 한 곳에만 속하게 하는 최소 구조 | Fable 권장안 자동 채택 | No |
| 2 | 물류 일괄 처리 단위 | 모든 단계를 unit만으로 처리하면 panel 납품 상태가 흐려지고, panel만 처리하면 포장 관계가 사라진다 | 포장은 unit 중심·출발은 unit 선택·납품은 선택 unit의 panel 일괄 결과를 panel별 record로 남기는 혼합안 | Fable 권장안 자동 채택 | No |
| 3 | 거래명세서 서명본 형식 | 사진만 허용하면 스캔 PDF를 못 받고, 임의 문서 생성은 범위가 크다 | 업로드된 JPEG/PNG/PDF 1개 이상을 signed evidence로 보존하고 전자서명 생성은 제외 | Fable 권장안 자동 채택 | No |
| 4 | 확정 뒤 정정 | 직접 수정은 감사가 깨지고, 완전한 재포장 lifecycle은 범위가 크다 | draft에서만 수정, 확정 뒤 append-only; 정정·재포장·반품은 후속 정책 | Fable 권장안 자동 채택 | No |
| 5 | 물류 진입 구조 | 단계별 전역 메뉴는 비대하고 단일 generic 업무는 현장 흐름을 숨긴다 | 전역 `물류` 한 개에서 포장·출발·납품 stage switch와 정확한 deep link 제공 | Fable 권장안 자동 채택 | No |
| 6 | project 완료 집계 | panel 한 건으로 project stage를 넘기면 미납품 panel이 숨고, 마지막 panel까지 다음 panel 처리를 막으면 병렬성이 사라진다 | panel별 다음 업무는 즉시 생성하고 project stage event·영업 정산 skeleton은 모든 active panel 완료 때 exactly-once | Fable 권장안 자동 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: 품질 뒤 포장·출발·납품의 실제 unit/panel mapping·필수 증빙·단계 기록·영업 인계가 없음.
- 권장 범위: flat Packing Unit, panel mapping, 필수 포장·상차·서명본 증빙, 포장→출발→납품의 panel별 전진 상태, 물류 공통 화면과 일괄 처리.
- 확정한 정책: Backend authoritative, project scope, panel 단위 전진-only, 필수 증빙, finalized append-only, open Pending 차단, 모든 active panel 기반 project event.
- 명시적 제외: 운송사/GPS/외부 portal·전자서명 생성·재포장/반품·정산 상세·provider·Persistent UAT·게시.
- Deferred 비차단 결정: 실제 Packing Unit 필드·사진 개수, 운영 storage·retention, 정정·재포장·부분 납품 정책.
- Fable 판정: `COMPLETED_CONFIRMED` — 사용자 명시적 experiment interview waiver에 따른 planning 입력 상태.

## 9. 성공 기준

- 업무 결과: 물류 담당자가 모바일에서 품질 완료 panel을 포장 unit으로 묶고 필수 증빙과 함께 포장·출발·납품을 순서대로 완료해 영업 정산으로 인계한다.
- 권한·데이터 불변조건: mutation/read/download 서버 권한+scope, 18단계 순서, panel active membership·next work·project event 중복 방지, 확정 증빙 불변과 transaction 감사.
- 자동 검증: migration fresh/existing, Backend build·전체/권한/transaction/concurrency tests, Frontend lint·typecheck·unit·build, isolated E2E, desktop·390px·Teams narrow overflow 0.
- 사용자 검수: synthetic 페이지별 screenshot을 보고하되 사용자 직접 검수 완료로 표시하지 않는다.

## 10. 사용자 확인

- [x] 사용자 standing rule로 interview 질문 왕복과 중간 승인을 생략한다.
- [x] Roadmap·012A에서 확정된 업무 문제·역할·불변조건을 planning 입력으로 사용한다.
- [x] 비차단 선택은 Fable 권장안을 자동 채택한다.
- [x] Repository 충돌·18단계 순서·panel 전진-only·필수 증빙·확정 이력·Pending 차단·secret/개인정보 위험은 fast-track으로 우회하지 않는다.
- [x] 대표 repo·main·Persistent UAT·provider·게시를 제외한다.
- [x] open blocking decision 0인 경우에만 1차 planning을 시작한다.

확인 source: 사용자는 이 실험 worktree의 신규 작업을 인터뷰 없이 Fable 권장안으로 바로 1차 기획·Codex review·Fable 2차 기획·Codex 구현하고 결과물을 보여주도록 반복 명시했다.
