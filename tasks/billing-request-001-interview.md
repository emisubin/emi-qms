# TASK-BILLING-REQUEST-001 — 세금계산서 발행요청 자료 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5_EXPERIMENT_FAST_TRACK`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 현재 `experiment/*` branch에서 사용자가 요청한 세금계산서 발행요청 자료 기능의 interview source of truth다. 사용자는 사용자-facing interview와 중간 승인을 생략하고 `Fable 1차 기획 → Codex 내용 review → Fable 2차 기획 → Codex 구현·검증·screenshot`까지 이어가도록 명시했다. 비차단 제품 선택은 Fable의 Repository 근거 권장안을 자동 채택한다. 대표 repo, GitHub `main`, Persistent UAT, 실제 Teams·메일·회계 provider와 push·PR·merge는 제외한다.

## Task Identity Gate

- proposedTaskId: `TASK-BILLING-REQUEST-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-QR-001`
- roadmapNextGate: `TASK-QR-001`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-BILLING-REQUEST-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `USER_EXPLICIT_NEW_FEATURE`
- policyInputResolution: `FABLE_RECOMMENDATION_AUTO_ADOPT`
- gateStatus: `PASS_CREATE`

### Purpose identity

- 업무 목표: 영업 담당자가 매월 1일·16일 정기 업무에서 해당 기간에 납품 완료된 프로젝트를 선택하고, 회계팀에 전달할 세금계산서 발행요청 Excel 자료를 즉시 생성한다.
- Root Finding 또는 정책 결정: 기존 `TASK-014A`는 영업이 세금계산서 발행일·번호를 직접 입력해 프로젝트를 완료하는 계약이지만 실제 업무는 영업이 회계팀에 발행을 요청하는 것이다. 선택형 정기 요청 목록·회계 인계 Excel·요청 이력이 없다.
- 변경·검증 경계: 기존 납품 완료 근거와 영업 권한을 재사용한 batch candidate 조회, 반월 기간 기본값, 선택 Excel 생성, 요청 이력·중복 방지, final stage 문구와 adaptive UI, synthetic workbook 검증을 포함한다.
- 보존할 불변조건: 납품 완료 전 프로젝트 제외, open Pending과 project scope 검증, 영업 금액 권한, 서버 authoritative Excel, 같은 프로젝트·기간 중복 요청 경고/차단, 완료·감사 이력, 실제 외부 발송 없음, main·Persistent UAT 불변.
- 예상 산출물: Fable 1차 planning, Codex review, review 기반 Fable 2차 planning, additive DB/API/UI/Excel/tests, desktop/mobile 화면과 workbook screenshot, Implementation report.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

기존 `TASK-014A`는 단일 project의 발행정보 입력·최종 완료이며 Excel·회계 인계·batch 요청을 명시적으로 제외했다. 현재 Repository, branch와 PR에서 같은 목적의 Task는 확인되지 않았다. 사용자의 이번 명시 요청을 Roadmap 순서 override로 기록한다.

## 사용자 실행 지시

- 요청일: 2026-07-19
- 요청: 영업이 세금계산서를 직접 발행하는 것이 아니라 회계팀에 요청한다. 매월 1일과 16일, 해당 기간에 출하·납품된 프로젝트를 선택해 발행요청 자료를 바로 Excel로 뽑게 한다.
- 승인 대체: 비차단 선택은 Fable 권장안을 자동 채택한다.
- 게시 경계: local experiment 작업만 승인. `main` merge 승인 `0/3`.

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 납품 완료 프로젝트를 별도로 찾아 회계팀 요청용 목록을 수작업으로 정리하고, 시스템에서는 영업이 직접 발행일·번호를 입력하는 것처럼 보인다.
- 해결할 문제: 정기 마감 기간, 납품 근거, 요청 대상 중복과 회계팀 전달 열을 수기로 대조하지 않고 정확한 선택 Excel을 만들어야 한다.
- 성공했을 때 사용자가 할 수 있는 일: 오늘 기준 1일/16일 반월 기간의 납품 완료 후보를 보고 필요한 프로젝트만 선택하여 회계 발행요청 Excel을 내려받고, 이미 요청한 프로젝트를 구분한다.
- 하지 않을 경우 영향: 누락·중복 요청, 잘못된 기간과 금액 전달, 프로젝트 final stage 문구와 실제 책임의 불일치가 지속된다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 감사 요구 |
| --- | --- | --- | --- | --- |
| 영업 정·부 담당 | 요청 후보 조회, 프로젝트 선택, Excel 생성, 요청 이력 확인 | 기존 project access와 판매금액 권한 | 자신이 접근 가능한 납품 프로젝트 요청 batch | actor·시각·기간·선택 project·파일 checksum |
| 영업 부서장 | 팀 요청 현황 확인·누락 보정 | 기존 영업 범위 | 같은 기능 | 중복·재생성 사유 |
| System Administrator | 운영 지원 | 전체 | 필요 시 재다운로드 | 전체 audit |
| 회계팀 | 생성된 Excel을 시스템 밖에서 수신·발행 | 이번 MVP에는 시스템 계정 workflow 없음 | 없음 | 외부 전송은 범위 밖 |
| 다른 부서 | 프로젝트 탭에서 발행요청 상태 조회 | 기존 project read scope | 없음 | 판매금액 권한 없으면 금액 비노출 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: 영업 정산/발행요청 화면 → 1일 또는 16일 기준 추천 기간 확인 → 납품 완료 후보 조회 → 전체선택 또는 개별 선택 → 요청자료 생성 → `.xlsx` 다운로드 → 요청 batch와 project별 상태 기록.
- 기간 기본값: 1일 실행은 직전 월 16일~말일, 16일 실행은 당월 1일~15일을 우선 후보로 두되 경계·수동 기간 변경 허용 여부는 Fable이 권장한다.
- validation: 선택 0건, 미납품, 접근 불가, 금액 권한 없음, open Pending, 이미 같은 기간에 요청됨, stale candidate를 서버가 재검증한다.
- 동시성·중복: 같은 프로젝트·요청 기간은 정확히 한 번만 요청되도록 unique/idempotency를 적용한다. 동일 operation 재시도는 같은 결과를 재생하고, 사용자가 명시적으로 재발행할 필요는 별도 사유와 이력 정책을 Fable이 권장한다.
- 부분 실패·복구: DB commit과 workbook 생성의 원자 경계를 정하고 다운로드 실패 시 같은 batch를 재다운로드할 수 있게 한다. Excel 생성 실패는 요청 완료로 기록하지 않는다.

## 4. Data·integration·lifecycle

- 기존 근거: `logistics_delivery_results`, `projects`, `project_assignees`, `sales_settlements`, open `pending_issues`, project scope/permissions, 선택 내보내기 패턴.
- 신규 후보: billing request batch, 기준 기간, 상태, actor/time, operation id, selected project snapshot rows, workbook checksum/file metadata.
- 상태 전이 권장 대상: Candidate → Requested이며 회계 발행 완료 확인은 이번 범위에서 기존 settlement와 어떻게 연결할지 Fable이 최소안을 제시한다.
- Excel: 회계팀이 바로 사용할 수 있도록 프로젝트 코드·고객사·품목·납품일·공급가액/통화·영업담당·납품처·요청 메모 등 필요한 열을 서버가 생성한다. 실제 사업자번호·세율·공급가/부가세 필드가 Repository에 없으면 꾸며내지 않고 누락 표시 또는 입력 수단의 최소안을 권장한다.
- 외부 연동: 회계/ERP/국세청/메일 자동 발송은 제외한다. 실제 Teams·메일 provider도 호출하지 않는다.
- migration: 최신 migration 다음 additive migration을 사용하고 isolated existing/fresh DB만 검증한다.

## 5. UX와 운영 적용

- PC: 정기 요청 기간·요약 KPI → 필터/검색 → 체크박스 전체선택과 후보 table → 선택 발행요청 Excel action → 요청 이력.
- 모바일: 전체 PC 기능을 축소 복제하지 않고 현재 기간·미요청 건수·프로젝트 핵심 정보·선택 다운로드만 한 열로 제공한다. 복잡한 열 설정과 과거 batch 상세 관리는 PC 우선으로 둔다.
- 상태·문구: `세금계산서 발행` 대신 `회계팀 발행 요청`, `정산·완료` 대신 실제 next action이 드러나는 문구를 사용한다.
- loading·empty·error·success: 후보 없음, 이미 모두 요청함, 선택 없음, 생성 중, 재다운로드, stale candidate를 행동 근처에 표시한다.
- 디자인: 현재 WITHUS 기반 token과 공통 components, 얇은 divider·절제된 shadow·compact controls·blue active accent를 유지한다.

## 6. 포함·제외 범위

### 포함

- 반월 추천 기간과 납품 완료 candidate 목록
- checkbox 전체선택·개별선택과 선택 Excel 생성
- 서버 재검증, batch/row snapshot, 멱등·중복 방지, 재다운로드
- 프로젝트 final stage와 버튼·상태 문구를 실제 회계 요청 책임에 맞춤
- 프로젝트 상세 영업 탭에서 요청 상태 확인
- desktop/390px UI, synthetic workbook 내용·레이아웃 검증

### 제외

- 실제 회계·ERP·국세청 전자발행 API
- 실제 이메일·Teams 첨부 발송
- 회계팀 계정의 발행 완료 workflow와 수정세금계산서
- 사업자등록정보 마스터의 대규모 신규 구축, 수금·채권·원가
- 대표 repo·main·Persistent UAT·push·PR·merge

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 요청 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 기간 경계와 수동 변경 | 고정 반월은 단순, 제한적 변경은 누락 보정 가능 | 정기 기본값과 안전한 보정 범위 권장 | Fable 권장안 자동 채택 | No |
| 2 | Excel 생성과 요청 완료 원자성 | 다운로드만 하면 감사 약함, commit 먼저면 다운로드 실패 가능 | batch 생성 후 재다운로드 가능한 2단계 계약 권장 | Fable 권장안 자동 채택 | No |
| 3 | 중복·재요청 | 완전 금지는 정정 불가, 무제한은 중복 위험 | 중복 차단과 사유 있는 새 revision 최소안 권장 | Fable 권장안 자동 채택 | No |
| 4 | final 완료 의미 | 요청 시 완료, 회계 발행 확인 시 완료, 기존 발행입력 유지 | 실제 담당 경계와 18단계 정합 최소안 권장 | Fable 권장안 자동 채택 | No |
| 5 | 필수 Excel 열 | 현재 data만 사용하면 회계 정보 부족 가능 | 존재하는 근거만 출력하고 누락 필드 처리 최소안 권장 | Fable 권장안 자동 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: 영업의 실제 책임은 세금계산서 직접 발행이 아니라 회계팀 발행요청 자료 작성이며, 반월 납품분을 수기로 정리하면서 누락·중복 위험이 생긴다.
- 권장 범위: 납품 완료 candidate, 반월 추천 기간, 선택 batch, 서버 Excel, 요청 이력·멱등·재다운로드, 실제 업무 문구.
- 확정 정책: 1일/16일 정기 요청, 프로젝트 선택, Excel, 실제 provider 제외, main 불변.
- Deferred 비차단 결정: 기간 보정, revision, 요청과 프로젝트 완료 연결, 누락 회계 열 최소안.
- Fable 판정: `COMPLETED_CONFIRMED`.

## 9. 성공 기준

- 영업 담당자가 반월 납품 프로젝트를 선택해 회계팀 발행요청 `.xlsx`를 한 번의 행동으로 생성한다.
- 서버가 scope·납품·Pending·중복을 재검증하고 같은 operation 재시도에서 중복 batch를 만들지 않는다.
- workbook의 선택 project 수·금액·납품일·통화와 화면 요약이 일치한다.
- 390px에서 핵심 선택·생성 흐름에 가로 스크롤이 없고 PC에는 과거 요청·재다운로드가 보인다.
- Backend/Frontend tests, isolated migration/E2E, workbook render/screenshot 검증이 통과한다.

## 10. 사용자 확인

- [x] 사용자가 experiment branch의 인터뷰·중간 승인을 생략했다.
- [x] 사용자 요청과 기존 확정 계약만 기록했다.
- [x] 비차단 선택은 Fable 권장안 자동 채택으로 남겼다.
- [x] blocking decision은 0이다.

- `interviewStatus: COMPLETED_CONFIRMED`
- `userConfirmed: true`
- `openBlockingDecisionCount: 0`
- `planningApproved: false`
- `implementationApproved: false`
