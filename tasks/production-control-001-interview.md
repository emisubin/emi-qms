# TASK-PRODUCTION-CONTROL-001 — Item별 계획·자동 실적·가로 막대 일정 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- orchestrationOwner: `CODEX`
- interviewRound: 8
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 사용자가 확정한 최종 목표와 아직 직접 선택할 정책을 Fable interview 입력으로 고정한다. 사용자는 프로젝트 상세 생산관리 탭에서 Item별 고정 계획 항목, 계획 시작·종료, 부서 실데이터 기반 자동 실적 시작·종료와 계획/실적 가로 막대 캘린더를 구현하도록 명시했다. 다만 이번 Task의 세부 정책은 권장안을 자동 채택하지 않고 사용자가 직접 결정한다. Fable 5가 질문·선택지·권장안을 제시하고, 사용자 답변과 요약 확인이 완료된 뒤에만 1차 planning을 시작한다. 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge는 포함하지 않는다.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 1 | `QUESTIONS_REQUIRED` | 5 | “못알아듣겠어. 이해하기 쉬운 말로 질문해” — 정책 답변 아님 | 같은 결정 5개를 비기술적 업무 언어로 다시 질문 |
| 2 | `QUESTIONS_REQUIRED` | 5 | `1-A, 2-A, 3-A, 4-A, 5-A` | 남은 부서 실적 계산·계획 변경 이력·화면 구성 질문 |
| 3 | `QUESTIONS_REQUIRED` | 5 | `1-A, 2-A, 3-A, 4-A, 5-A` | 마지막 표·가로 막대 일정 화면 구성 질문 |
| 4 | `QUESTIONS_REQUIRED` | 5 | `1-A, 2-A, 3-A, 4-A, 5-A` — 일정표는 계획 항목을 기준으로 계획·실적을 가로 막대로 비교 | 질문 없이 전체 확인용 요약 생성 |
| 5 | `SUMMARY_CONFIRMATION_REQUIRED` | 0 | 사용자가 생산계획·제조 항목 전면 교체 예정임을 밝혀 고정 catalog 연결 전제를 재검토하도록 요청 | 동적 template 연결과 기존 프로젝트 전환 정책 확정 후 요약 재작성 |
| 6 | `QUESTIONS_REQUIRED` | 5 | `1-A, 2-A, 3-A, 4-A, 5-B` — 기존 프로젝트는 생성 당시 생산계획·제조 양식 snapshot을 영구 유지하고 새 version은 이후 생성 프로젝트부터 적용 | 선택지 5-B의 화면 의미와 부서 고정 실적 사건 선택 방식의 남은 모호함 확인 |
| 7 | `QUESTIONS_REQUIRED` | 2 | `1-B, 2-A` — 기존 프로젝트에는 새 연결이 없어 자동 실적이 없으므로 기존 화면·양식을 유지 | 질문 없이 변경 내용을 반영한 전체 확인용 요약 생성 |
| 8 | `COMPLETED_CONFIRMED` | 0 | `요약 확인` | [Fable Round 8 원문](production-control-001-interview-round-8-fable.md)을 확정 source로 1차 기획 시작 |

- 사용자 진행 변경: “이번에는 사용자가 직접 정할거야.”
- 사용자 확인 질문: 관리자 template version의 계획 항목을 실제 부서 데이터와 어떤 방식으로 연결하는지 설명을 요청했다.
- Round 1 사용자 feedback: 기술 용어와 schema 중심 설명을 이해하기 어려우므로 실제 화면·업무 결과 중심의 쉬운 질문으로 다시 제시해야 한다.

### Round 2 사용자 결정

1. 기존 생산계획 표를 확장해 계획 시작·종료와 연결 정보를 추가한다.
2. 기본 milestone과 연결 규칙은 migration seed와 Backend registry로 고정하고, 관리자 편집 화면은 이번 Task에서 만들지 않는다.
3. 자동 실적은 프로젝트 상세 생산관리 탭 조회 시 최신 부서 원본 데이터에서 결정적으로 계산한다.
4. 기존 `planned_date`는 계획 시작일·종료일 같은 날짜로 복사하고 원본 값도 보존한다.
5. 필수 항목의 계획 시작일·종료일이 모두 입력되면 생산계획 단계를 완료로 판정한다.

### Round 3 사용자 결정

1. 실적 날짜는 담당자가 입력한 실제 업무 날짜를 우선하고, 별도 업무 날짜가 없을 때만 시스템 확정 시각의 한국 날짜를 사용한다.
2. 구매·자재 milestone 완료는 기존 자재 화면의 담당자 확정 행위를 기준으로 판정하고 수량은 근거로 표시한다.
3. 과거 제조·검사 기록은 이름이 정규화된 exact match인 경우에만 stable code에 연결하고, 나머지는 기록을 보존한 채 `연결 안 됨`으로 표시한다.
4. 검사 재검사 실적 기간은 최초 검사 시작부터 최종 합격까지로 계산한다.
5. 가로 막대 일정은 최신 계획을 사용하고 과거 계획 수정은 기존 변경 이력으로 추적한다. 최초 기준 계획 비교 화면은 이번 범위에서 제외한다.

### Round 4 사용자 결정

1. PC 계획·실적 표는 `항목명 / 필수 / 계획 기간 / 실적 기간 / 진행률 / 일정 상태`의 핵심 6개 열을 기본 표시하고, 비고와 대상별 근거는 행을 펼쳐 확인한다.
2. 일정표는 하루 단위 고정 축과 가로 스크롤을 사용하며 열 때 오늘 위치로 자동 이동한다. 각 계획 항목을 기준으로 계획 막대와 실적 막대를 가로로 비교한다.
3. 390px 좁은 화면은 항목별 카드 안에 계획·실적 기간, 상태와 축약된 두 줄 막대를 표시하고 가로 스크롤은 사용하지 않는다.
4. 구매품목·패널별 근거는 펼침 표에서 확인하고, 일정표는 계획 항목 단위 막대만 유지한다.
5. 위 표를 접근 가능한 공식 대체 수단으로 유지하고, 막대에는 텍스트 설명을 제공한다. 상태는 색상에만 의존하지 않고 채움 패턴과 텍스트를 함께 사용한다.

### Round 5 요약 확인 중 사용자 수정

- 사용자는 생산계획 항목과 제조 항목을 전면 교체할 예정이므로, 정해진 항목명이나 고정 제조 catalog를 전제로 한 연결 방식은 사용할 수 없다고 밝혔다.
- 계획 항목과 제조 template 단계가 이후 교체되어도 관리자가 코드 수정 없이 다시 연결할 수 있는 동적 mapping이 필요하다.
- 생산계획 항목 하나는 여러 제조·품질·물류 source에 연결할 수 있다.
- 새 template은 새 프로젝트부터 적용하고, 기존 프로젝트는 생성 당시 template snapshot을 유지한다.
- 관리자와 각 부서 팀장이 제조 항목·생산계획 항목·실적 source 연결을 자유롭게 구성하는 양식 관리 페이지를 제공한다.
- 프로젝트 생성 시 선택된 Item 양식의 제조 항목·생산계획 항목·연결 관계를 자동 생성한다.
- 프로젝트 안에서도 생산계획 항목과 여러 실적 source 연결을 수정할 수 있게 한다.
- 이 결정은 Round 2의 “Backend 고정 catalog, 관리자 편집 화면 제외” 결정을 명시적으로 대체한다.

### Round 6 사용자 결정

1. 기존 `양식 관리` 화면에 생산계획 영역을 추가하고 기존 생산관리 설정의 단계 편집 기능을 통합한다.
2. 제조 항목도 Item별 versioned template으로 확장하고 프로젝트 생성 시 생산계획 항목·제조 항목·연결 관계를 함께 snapshot한다.
3. 항목에는 version이 바뀌어도 유지되는 immutable identity를 부여해 이름·순서 변경 시 연결을 유지하고, 삭제된 항목의 연결은 `연결 안 됨`으로 드러내 재연결한다.
4. 기존 사람 단위 양식 관리자 지정 방식을 확장한다. 제조 양식 관리자는 제조 항목을, 생산계획 양식 관리자와 System Administrator는 생산계획 항목·실적 연결을 관리하며, 프로젝트 안에서는 생산관리 정·부 담당자가 계획 기간·항목·연결을 수정한다.
5. 사용자는 선택지 `5-B`를 선택하고, 생산계획·제조 양식이 바뀌어도 이미 생성된 프로젝트는 생성 당시 양식을 계속 사용하며 새 version은 게시 이후 새롭게 생성되는 프로젝트부터 적용한다고 명시했다.
6. 구매·자재·품질·물류 source를 시스템의 고정 실적 사건 목록에서 선택한다는 Round 6 질문 3의 부가 확인에는 별도 문장으로 답하지 않았다.

### Round 7 사용자 결정

1. 기존 프로젝트는 새 source 연결 정보가 없어 자동 실적이 존재하지 않으므로 단일 예정일·체크 캘린더의 기존 화면과 생성 당시 양식을 그대로 유지한다.
2. 새 양식 version으로 생성되는 프로젝트부터 계획·실적 표, 가로 막대 일정과 자동 실적 연결을 사용한다.
3. 제조는 Item별 관리자가 구성한 제조 단계에서 연결하고, 구매·자재·품질·물류는 시스템이 이미 확정 처리하는 고정 실적 사건 목록에서 선택한다.

### Round 8 사용자 확인

- 사용자 답변: `요약 확인`
- 확인 대상: [Fable Round 8 전체 확인용 요약](production-control-001-interview-round-8-fable.md)
- 확인 결과: blocking 결정이 없으며 이 인터뷰를 Fable 1차 기획의 source of truth로 사용한다.

## 1. Task Identity Gate

- identityGateSource: `tasks/production-control-001-identity-gate.md`
- proposedTaskId: `TASK-PRODUCTION-CONTROL-001`
- canonicalTaskId: `TASK-PRODUCTION-CONTROL-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- roadmapExpectedTaskId: `ATTACHMENT-STORAGE-OR-OPERATIONS-TRANSITION`
- roadmapNextGate: `OPERATIONS_PROMOTION`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- policyInputResolution: `USER_DECISION_REQUIRED`
- gateStatus: `PASS_CREATE`

## 2. 업무 문제와 기대 결과

- 현재 업무 방식: 생산관리 담당자가 Item template에서 snapshot된 생산계획 행마다 단일 예정일과 비고를 직접 입력한다. 프로젝트 상세 생산관리 탭은 계획표와 날짜별 체크 표시 캘린더를 제공한다.
- 해결할 문제: 계획 항목이 실제 구매·자재·제조·품질·물류 데이터와 연결되지 않아, 각 부서가 업무를 완료해도 생산관리 담당자가 실제 진행일을 별도로 다시 확인하거나 수기로 옮겨야 한다. 단일 예정일과 체크 표시는 작업 기간, 실제 소요 기간, 지연과 병행 작업을 표현하지 못한다.
- 현재 우회 방식: 생산관리 담당자가 각 부서 탭과 전체 흐름을 직접 오가며 상태를 확인한다. 생산계획 자체에는 실제 시작·종료와 근거가 남지 않는다.
- 성공했을 때 사용자가 할 수 있는 일: 프로젝트 상세 생산관리 탭 한곳에서 Item별 고정 계획 항목의 계획 기간, 자동 실적 기간, 진행률, 지연·차단 상태와 구매품목·패널별 근거를 확인하고 같은 정보를 가로 막대 일정으로 비교한다.
- 하지 않을 경우 영향: 프로젝트 수와 패널 수가 늘수록 일정 재입력·대조 부담, 누락, 실제 병목 오판과 계획 대비 실적 분석 불가가 누적된다.

## 3. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 관리자·각 부서 팀장 | 제조·생산계획 양식과 실적 source 연결 구성 | 권한이 허용된 Item/template | 항목 생성·정렬·사용 종료·다중 source 연결과 새 version 게시 | code 수정 없는 양식 관리, version·audit 필요 |
| 생산관리 정·부 담당자 | 프로젝트별 계획 기간·항목·실적 연결 수정, 자동 실적·근거 확인, 전체 일정 판단 | 기존 project scope | 프로젝트 snapshot 안에서 승인된 계획·연결 변경 | 기존 권한과 optimistic version·audit 유지, 정확한 변경 경계는 추가 확정 필요 |
| 구매·자재·제조·품질·물류 담당자 | 자기 부서 입력이 어느 계획 항목 실적으로 반영됐는지 확인 | 기존 project scope | 기존 부서 업무 화면에서만 입력 | 생산관리 실적 직접 수정 불가 |
| 영업·설계·회계·다른 내부 부서 | 프로젝트 계획·실적 조회 | 기존 project scope | 없음 | 조회 전용 안내 |
| System Administrator | 전체 template·연결·audit 관리 | 기존 전사 관리자 범위 | 양식 관리만 가능, 자동 실적 원본 우회 입력 불가 | 기존 업무 입력 금지 불변 유지 |

## 4. 확정된 핵심 모델

1. 프로젝트 상세 생산관리 탭을 펼치면 프로젝트 생성 당시 Item template에서 snapshot된 계획 항목이 표시된다.
2. 제조 항목과 생산계획 항목은 관리자가 자유롭게 구성하되, 이름이나 화면 순서가 아닌 immutable identity와 template version으로 관리한다.
3. 프로젝트에는 적용 시점의 항목명·필수 여부·연결 정의 snapshot을 저장해 이후 template 수정이 과거 프로젝트 의미를 바꾸지 않게 한다.
4. 각 계획 항목은 `계획 시작일`, `계획 종료일`, `실적 시작일`, `실적 종료일` 네 날짜 의미를 가진다.
5. 계획 기간은 생산관리 담당자가 입력하고 같은 날 시작·종료를 허용한다. 시작일이 종료일보다 늦으면 저장하지 않는다.
6. 실적 기간은 연결된 부서 원본 데이터에서 자동 파생하며 사용자가 직접 수정할 수 없다.
7. 자동 실적 시작은 해당 milestone 대상에서 최초 유효 업무 사실이 발생한 시각이다.
8. 자동 실적 종료는 해당 milestone의 모든 활성 대상이 완료 기준을 충족한 마지막 시각이다. 일부만 완료되면 실적 시작과 `완료 수/전체 수`를 표시하되 종료일은 비워 둔다.
9. 프로젝트 표는 계획 항목 한 줄로 집계하고, 행을 펼치면 구매품목 또는 개별 패널 단위의 실제 근거·날짜·상태를 확인한다.
10. 원본 부서 데이터가 수정·취소되면 파생 실적은 현재 유효 사실로 재계산하되 원본 audit를 삭제하지 않는다.
11. 생산계획 항목 하나는 여러 제조 단계·품질검사·구매·자재·물류 source에 연결할 수 있으며, 연결된 대상의 최초 유효 사실과 전체 완료를 기준으로 실적 기간·진행률을 집계한다.
12. 프로젝트별 계획 항목·source 연결 변경은 template 원본을 수정하지 않고 해당 프로젝트 snapshot에만 새 revision으로 남긴다.

## 5. 부서 데이터 연결 원칙

- 구매: 구매품목 생성, 발주일, 입고예정일, 발주 수량과 공급 유형의 구조화 데이터를 재사용한다.
- 자재: 구매품목별 분할 도착, IQC, 입고 확정의 실제 날짜와 수량을 재사용한다.
- 제조: 관리자가 구성한 제조 template 단계의 immutable identity를 개별 패널 실행 snapshot과 연결하고 단계별 확인 시각을 재사용한다. 단계명·순서·template 전면 교체에 의존하지 않는다.
- 품질: IQC는 구매품목 도착분 단위, LQC·OQC·전진검수·FAT는 개별 패널 단위를 유지한다. OQC는 단계별 결과, 전진검수·FAT는 패널당 통합 판정 1회라는 확정 정책을 유지한다.
- 물류: 개별 패널의 포장·출발·납품과 각각의 확정 시각을 재사용한다. 부분 출하·부분 납품을 전체 완료로 오인하지 않는다.
- 사용자 추가 계획 항목은 삭제하지 않는다. stable source binding이 없는 기존 custom 항목은 `수동 항목 / 연결 안 됨`으로 보존하고 계획 기간만 입력할 수 있게 한다.

## 6. 상태·진행률·Pending 정책

1. 일정 상태와 기존 18단계 workflow 상태를 분리한다.
2. 일정 진행률은 milestone별 활성 대상 완료 수와 전체 수를 기반으로 표시하고, 프로젝트 전체 흐름 진행률 공식은 변경하지 않는다.
3. 일정 상태는 최소 `계획 미입력`, `착수 전`, `진행 중`, `계획 내 완료`, `지연`, `완료`, `차단`, `연결 안 됨`을 구분하되 Fable이 기존 디자인 상태 vocabulary와 실제 계산식을 대조해 최소 집합을 권장한다.
4. Open Pending이 연결 대상에 있으면 계획/실적 날짜나 과거 완료 사실을 삭제하거나 workflow 단계를 후퇴시키지 않고 현재 `차단` 상태와 Pending 근거를 우선 표시한다.
5. 재검사 합격과 Pending 해제 뒤에는 최신 유효 검사 결과를 기준으로 실적·진행률을 재계산한다.
6. FAT가 필요하지 않은 프로젝트에서는 FAT milestone을 비활성 또는 `해당 없음`으로 처리해 분모와 지연 계산에서 제외한다.
7. 취소된 패널·품목은 활성 대상 분모에서 제외하되 과거 이력과 취소 근거는 상세에서 보존한다.

## 7. 계획·실적 가로 막대 캘린더

1. 기존 날짜 셀 체크 표시 캘린더를 가로 막대 일정표로 교체한다.
2. 각 계획 항목은 같은 날짜 축에서 계획 막대와 실적 막대 두 줄 또는 명확히 구분되는 두 레이어를 가진다.
3. 계획 막대는 흑백 와이어프레임의 외곽선 또는 패턴, 실적 막대는 상태 의미색이 허용된 채움으로 구분한다.
4. 날짜 축에는 월·일, 주말·공휴일, 오늘 기준선을 표시하고 기존 `BusinessDayCalculator`/business-days API 기준을 재사용한다.
5. 실적 종료 전에는 최초 시작일부터 오늘 또는 최신 유효 처리일까지 진행 중 막대를 표시하되 완료일로 저장하지 않는다.
6. 계획 종료를 넘긴 진행 중 항목은 지연 구간과 지연 일수를 텍스트로도 표시해 색만으로 판단하지 않게 한다.
7. 날짜 범위가 길면 표 전체가 깨지지 않도록 고정된 항목 열과 일정 영역 가로 탐색을 허용한다. PC가 주 판단 화면이며 390px에서는 핵심 상태·기간과 축약 막대를 제공한다.

## 8. UX와 입력 방식

- 프로젝트 상세 생산관리 탭: KPI → Item 고정 계획·실적 표 → 계획/실적 가로 막대 일정 → 담당자 순서를 기본 권장안으로 사용한다.
- 조회 화면은 input을 직접 노출하지 않는다. 생산관리 담당자가 `계획 수정`을 누르면 기존 별도 수정 route로 이동한다.
- 양식 관리에는 제조 항목, 생산계획 항목과 각 생산계획 항목의 다중 실적 source 연결을 구성하는 전용 페이지를 제공한다.
- 프로젝트 생성 시 Item에 게시된 양식 version을 제조 실행·생산계획·연결 관계 snapshot으로 자동 생성한다.
- 프로젝트 수정 화면에서는 계획 시작·종료·비고뿐 아니라 프로젝트 전용 생산계획 항목과 다중 source 연결도 수정할 수 있다. 자동 실적 값 자체는 계속 읽기 전용이다.
- 표 행 펼침은 한 번에 한 항목을 기본으로 하고, 구매품목·패널별 대상명·현재 단계·계획/실적 근거·Pending을 표시한다.
- 저장 성공·validation·부분 refresh 실패는 기존 Action Feedback 계약을 재사용한다.
- 타 부서 조회와 선행조건 대기는 기존 `DsReadOnlyBanner` 의미를 유지한다.
- 흑백 와이어프레임과 사각형 기본 디자인을 유지하고 상태 의미색만 사용한다.

## 9. 정상·예외·복구 흐름

- 정상 흐름: 프로젝트 생성 → Item별 milestone snapshot 생성 → 생산관리 계획 시작·종료 저장 → 각 부서 기존 화면에서 업무 처리 → source fact 저장 transaction 이후 projection 재계산 또는 조회 시 deterministic 파생 → 생산관리 탭 표·일정 자동 갱신.
- validation 실패: 계획 시작/종료 누락·역전, inactive/완료 프로젝트, stale version, 권한 밖 변경을 Backend에서 차단하고 field 가까이에 한글 안내를 표시한다.
- 동시 처리·중복: 계획 수정은 optimistic concurrency를 유지하고, 파생 실적은 같은 원본 facts에 대해 결정적으로 같은 결과를 반환한다.
- 취소·재시도·복구: source fact가 이미 존재하지만 projection이 누락된 기존 데이터는 bounded reconciliation/backfill로 복구하고 반복 실행 시 중복하지 않는다.
- 부분 실패와 rollback: 부서 원본 mutation 성공 뒤 projection refresh가 실패해도 원본 성공을 롤백하지 않는다. 화면은 `저장 완료·일정 새로고침 실패`로 구분하고 다시 조회하면 복구한다.
- 기존 데이터: 알려진 template 항목은 semantic code로 backfill하고 모호한 이름은 임의 연결하지 않는다. custom 항목은 보존한다.

## 10. 포함 범위

- Item별 stable milestone template와 프로젝트 snapshot
- 관리자·각 부서 팀장용 제조·생산계획·실적 연결 양식 관리 페이지와 version·audit
- 프로젝트 생성 시 게시 양식 자동 snapshot, 프로젝트별 계획 항목·다중 source 연결 revision
- 계획 시작·종료 입력, validation, version·audit
- 구매·자재·제조·품질·물류 원본 데이터 binding
- 자동 실적 시작·종료, 대상 수·완료 수, 일정 상태·지연·차단 projection
- 프로젝트 상세 생산관리 탭의 통합 표와 근거 펼침
- 계획/실적 이중 가로 막대 일정과 휴일·오늘선
- 기존 단일 예정일·known 항목 backfill, custom 항목 보존
- 조회·수정 API/Frontend, desktop·390px, 자동 검증과 screenshot

## 11. 명시적 제외

- 기존 18단계 workflow 순서·전체 진행률 공식 변경
- 생산계획 또는 구매 예정일을 `work_items.due_date`에 자동 동기화하는 기능
- 일정 지연의 인앱·Teams·메일 자동 에스컬레이션
- ERP/MES/회계 외부 연동
- 부서 원본 업무 화면과 확정 상태 전이의 재구현
- 대표 repo·`main`, Persistent UAT, 실제 provider, push·PR·merge

## 12. 사용자가 직접 확정할 항목

Fable 5가 관련 항목을 한 round에 최대 5개씩 질문하고 선택지 비교와 권장안을 제시한다. 사용자가 직접 답변하며 Codex는 답을 대신 만들거나 자동 채택하지 않는다.

1. Item별 milestone definition·binding·snapshot·derived projection의 최소 additive schema.
2. 기본 milestone catalog와 Item별 제조/LQC template 단계를 함께 고정하는 방식.
3. 구매·자재의 수량 기반 완료 및 실적 시작·종료 계산식.
4. 제조·LQC의 단계 semantic identity를 ordinal/name 의존 없이 연결하는 migration과 legacy fallback.
5. OQC 단계별, 전진검수·FAT aggregate, 물류 패널별 실적 집계식.
6. source mutation마다 projection을 저장할지, 조회 시 계산할지, hybrid read model을 사용할지와 race-safe 재조정 방식.
7. legacy `planned_date`를 계획 시작·종료로 옮기는 보수적 규칙과 이름 매칭 실패 처리.
8. 표·상세 펼침·Gantt 축의 PC/390px 정보 밀도와 접근성.
9. 계획 baseline revision을 별도 보존할지, 기존 audit event로 현재 값 변경 이력만 남길지.
10. 계획 완료 stage의 기존 판정과 새 기간 입력 필수성의 호환 전략.

## 13. 성공 기준

- 프로젝트 Item에 맞는 고정 milestone이 이름·순서 변경에 흔들리지 않는 identity로 표시된다.
- 생산관리 담당자는 각 항목의 계획 시작·종료만 입력하고, 각 부서 담당자의 기존 업무 입력만으로 실적 시작·종료와 진행률이 자동 반영된다.
- 구매품목·분할 도착·IQC·개별 패널 제조/LQC/OQC/전진검수/FAT·패널별 포장/출발/납품이 확정 단위대로 집계된다.
- 일부 완료, FAT 해당 없음, Open Pending, 재검사, 취소 대상과 legacy custom 항목을 잘못 완료로 계산하지 않는다.
- 생산관리 탭의 표와 가로 막대 일정이 같은 projection을 사용해 날짜·상태·진행률이 일치한다.
- 기존 18단계 전체 흐름·진행률·부서 업무·권한·audit·부분 출하가 회귀하지 않는다.
- Backend 전체, Frontend lint/typecheck/unit/build, fresh/existing migration, isolated Full-Stack와 desktop/390px screenshot 검증을 통과한다.
- Open P0/P1/P2가 0이고 종료 산출물·사용자 검수 대기·게시 경계를 추적한다.

## 14. 승인·안전 경계

- planningApprovedForExperiment: `false`
- implementationApprovedForExperiment: `false`
- localCommitApproved: `true`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`
