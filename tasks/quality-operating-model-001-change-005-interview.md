# TASK-QUALITY-OPERATING-MODEL-001 Change 005 — 구매품 구분별 IQC 양식 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- orchestrationOwner: `CODEX`
- interviewRound: 4
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 구매품 구분마다 서로 다른 IQC 운영 방식과 검사 양식을 관리하는 신규 기능의 deep-interview source of truth다. Codex는 Fable 질문과 사용자 답변을 원문 의미 그대로 전달·기록하며 제품 정책을 대신 확정하지 않는다. Interview 완료는 planning 또는 구현 승인이 아니다.

## Task Identity Gate

- proposedTaskId: `TASK-QUALITY-OPERATING-MODEL-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: true
- instructionConflictCount: 0
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `Front Door domain Approved 대기 → managed TLS·route`
- roadmapSequenceMatch: false
- samePurposeMatchCount: 1
- canonicalTaskId: `TASK-QUALITY-OPERATING-MODEL-001`
- reuseExistingTask: true
- explicitRoadmapOverrideApproved: true
- experimentStandingInstructionApplies: false
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 구매품 구분이 많아져도 양식 관리에서 원하는 구분을 쉽게 찾고, 각 구매품 구분마다 서로 다른 IQC 검사 방식과 검사 항목을 설정해 실제 도착분 검사에 적용한다.
- Root Finding 또는 정책 결정: 현재 구매품 구분에는 `IQC 필요/없음`만 있고 상세 IQC는 전역 `MATERIAL_IQC` 현재 양식 하나를 사용한다. 따라서 IQC가 필요한 구매품 구분이 늘어나면 서로 다른 검사 항목을 적용할 수 없다.
- 변경·검증 경계: 기존 구매품 구분 catalog, IQC attempt·상세 성적서·외함 스캔형 성적서, 양식 관리와 신규 프로젝트/구매품 snapshot 계약을 재사용하는 기획과 이후 승인된 구현만 포함한다.
- 보존할 불변조건: 기존 프로젝트·구매품·도착분·확정 IQC·PDF·첨부·Pending·재검사 이력 불변, Backend 권한 authoritative, 구분/양식 변경의 비소급, IQC 비대상과 합격의 의미 분리, desktop·390px 기존 디자인 체계 유지.
- 예상 산출물: Change 005 interview·Fable planning·Codex review·승인된 구현·검증·종료 산출물.

### 검색 결과

- 같은 목적의 독립 Task·branch·PR은 확인되지 않았다.
- `TASK-009A`는 상세 IQC 성적서 엔진, `TASK-ADMIN-002`는 현재 양식 편집 기반, `TASK-QUALITY-OPERATING-MODEL-001`은 구매품 구분 routing을 소유한다.
- 이번 기능은 위 기반을 재구현하지 않고 구매품 구분과 IQC 양식의 연결을 추가하므로 canonical `TASK-QUALITY-OPERATING-MODEL-001`의 다음 Change를 재사용한다.
- 사용자는 Front Door 외부 검증 대기 중 5개 기능을 순서대로 기획·구현하도록 승인했고, Change 004에서 2번 작업을 `IQC 구매품 구분별 양식 기능`으로 명시했다.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `QUESTIONS_REQUIRED` | 0 | 대기 | Fable 질문 생성 |
| 1 | `QUESTIONS_REQUIRED` | 5 | 사용자가 "일단 먼저 우리 시스템의 검사 방식을 먼저 공부해보는 게 좋을 것 같아. 그리고나서 다시 질문하게 해. 내가 바라는게 뭔지, 우리 시스템이 어떤 프로세스로 굴러가고 있는지 전혀 모르는 질문인데?"라고 지적했다. Round 1 선택지는 답변·채택되지 않았다. | Codex가 실제 메인 UI·API·코드·기존 현장 조사와 테스트를 다시 확인한 뒤 그 기준선을 반영해 Fable 질문을 새로 작성한다. |
| 2 | `QUESTIONS_REQUIRED` | 5 | 사용자가 다섯 질문에 모두 답변했다. 답변 원문은 아래 `Round 2 사용자 답변`에 기록한다. | Fable이 답변을 실제 시스템 기준선과 대조해 추가 질문 또는 확인용 요약을 작성한다. |
| 3 | `QUESTIONS_REQUIRED` | 4 | 사용자가 `1a`, `2b`, `3a`, `4a`로 답변했다. 답변 해석은 아래 `Round 3 사용자 답변`에 기록한다. | Fable이 누적 답변을 확인용 요약으로 정리하고 blocking 결정 잔여 여부를 판정한다. |
| 4 | `SUMMARY_CONFIRMATION_REQUIRED` | 0 | 사용자가 Fable Round 4 확인용 요약에 `승인`이라고 답했다. | Interview를 `COMPLETED_CONFIRMED`로 고정하고 Fable planning을 시작한다. |

### Round 2 사용자 답변

Fable 원문 artifact: `tasks/quality-operating-model-001-change-005-interview-round-2-fable.md`

1. 질문 1 답변: "스캔형으로 할건지, 검사 항목을 만들어서 할건지는 양식에서 부서장이 입력가능"
2. 질문 2 답변: "양식에서 구매품별 검사 항목을 만들 수 있게하면 됨"
3. 질문 3 답변: "아니오."
4. 질문 4 답변: "이미 저장된 구매품에는 새 설정이 적용되지 않는다. 구매품 입력 시점에 해당 구매품의 검사 양식이 있고, 해당 검사 스위치가 켜져있는 경우에만 iqc를 띄운다."
5. 질문 5 답변: "품질부서 부서장이 모두 변경 가능. 관리자는 현장 업무에 관여하면 안됨. 관리자는 이 시스템만 관리하는 것임."

Codex는 위 답변에서 검사 방식 선택 UI의 정확한 표현, 구매품 저장 시 양식 snapshot 범위와 역할 식별 방식을 임의로 확정하지 않는다. Fable이 누적 interview source를 다시 읽고 추가 질문 또는 확인용 요약으로 판정한다.

### Round 3 사용자 답변

Fable 원문 artifact: `tasks/quality-operating-model-001-change-005-interview-round-3-fable.md`

1. 질문 1 답변: `1a` — 기존 품질 domain 부서 양식 관리자 지정을 사용한다.
2. 질문 2 답변: `2b` — 시스템 관리자의 쓰기 허용은 현행대로 유지한다.
3. 질문 3 답변: `3a` — 상세형은 검사 항목이 1개 이상 저장된 뒤에만 검사 스위치를 켤 수 있고, 활성 상태에서 항목 0개 저장을 차단한다. 스캔형은 항목 없이 스위치만으로 유효하다.
4. 질문 4 답변: `4a` — 검사 여부·방식은 구매품 저장 시점에 고정하고, 상세 검사 항목은 도착분 검사 회차 시작 시점의 해당 구분 현재 양식으로 고정한다.

### Round 1 무효 처리 경계

- `tasks/quality-operating-model-001-change-005-interview-round-1-fable.md`는 원문 이력으로 보존하되 사용자 확정 정책이나 planning 입력으로 사용하지 않는다.
- Round 1의 `1A, 2A, 3B, 4B, 5A` 권장 조합은 사용자가 승인하지 않았으며, 검사 방식을 범용 3-mode 선택 문제로 먼저 추상화한 전제가 실제 시스템·현장 요구를 충분히 반영하지 못했다.
- 다음 Fable round는 아래 `현재 시스템 실제 흐름 재조사`와 기존 사용자 현장 조사에서 출발해 질문 수를 필요한 최소치로 줄여야 한다.

## 0.1 현재 시스템 실제 흐름 재조사 (2026-08-05 Codex)

### 사용자 요구의 원래 업무 배경

- 기존 현장 조사(`tasks/quality-operating-model-001-interview.md`)에서 실제 IQC 대상은 `외함`, `판금류`, `부스바`, `명판` 네 종류라고 확인됐다.
- 네 종류 모두 육안 확인 대상이지만, 공식 수입검사서를 작성·보관하는 대상은 현재 외함뿐이다.
- 외함은 협력사가 종이 검사서를 동봉하고 품질팀이 추가 확인·서명한 스캔본으로 판정하는 정책이 이미 사용자 확정·구현됐다.
- 사용자는 당시부터 판금류·부스바·명판도 각기 맞는 기준으로 검사 기록 또는 성적서를 남기는 방안을 검토하고 싶다고 밝혔다.
- 이번 2번 작업의 직접 요구는 "양식관리에서 생산계획·실적 연결 또는 Item별 제조 양식처럼 구매품별 IQC 양식을 관리하고, 구매품 구분이 많아져도 찾기 쉬우며, 구분마다 다른 검사를 수행"하는 것이다.

### 실제 메인 5174 화면에서 확인한 구매·입고·IQC 흐름

1. 기능 업데이트 이후 생성되는 프로젝트는 `CategoryBased`이며 구매정보 저장 전에 각 품목의 `구매품 구분`을 필수 선택한다.
2. 실제 구매 화면의 선택지는 `외함 · IQC`, `판금류`, `부스바`, `명판`, `기타`다. 도급/사급은 같은 구분 규칙을 사용하며 실제 xAI 프로젝트에도 이 구분들이 저장돼 있다.
3. 구매품 저장 시 category id·code·표시명·`requires_iqc`가 구매품에 snapshot된다. catalog를 나중에 바꿔도 저장된 구매품의 검사 여부가 자동 변경되지 않으며, 도착 이력이 생긴 뒤에는 구매 구분 변경이 차단된다.
4. 자재 담당자가 도착분을 저장하면 같은 transaction에서 검사 분기가 결정된다. 분할 입고는 도착분마다 독립 회차다.
5. 현재 `requires_iqc=false`인 판금류·부스바·명판·기타는 IQC attempt를 만들지 않고 `InspectionNotRequired`를 거쳐 자재 담당자의 입고 확정으로 끝난다. 실제 API에서 기타 ACB 도착분은 IQC attempt 없이 `Confirmed`였다.
6. 현재 `requires_iqc=true`인 외함은 도착분마다 `ScanBased` attempt·품질 내 업무·알림이 생성된다. 실제 IQC 화면은 서명 스캔본 등록 후 적합/부적합을 확정하며 시스템 체크리스트와 시스템 생성 PDF를 사용하지 않는다.
7. 부적합은 기존 Pending 조치로 차단되고, 조치 완료 후 기존 확정본을 고치지 않는 새 재검사 회차가 생성된다. 스캔형 재검사는 새 스캔본이 필요하다.
8. 양식 관리의 전역 `자재 수입검사(MATERIAL_IQC)` 상세 체크리스트는 migration `0067` 이전 `AllReceipts` 프로젝트에만 사용된다. 현재 정상 생성되는 `CategoryBased` 프로젝트의 새 구매품에는 이 전역 양식이 사용되지 않는다.
9. 현재 코드에서 `CategoryBased + requires_iqc=true`이면 category code와 무관하게 모두 `ScanBased`로 생성된다. 따라서 단순히 판금류·부스바·명판의 `IQC 필요` checkbox만 켜면 구분별 상세검사가 아니라 외함 스캔 검사로 잘못 연결된다.

### 이번 기능이 확장해야 할 정확한 지점

- 이미 확정된 외함 스캔형은 보존 대상이며 범용 상세 체크리스트로 바꾸는 요청이 아니다.
- 현재 검사를 생략하는 판금류·부스바·명판 및 이후 추가될 구매품 구분 중 필요한 구분에 각각 전용 상세 체크리스트를 만들고 실제 도착분에 연결하는 것이 핵심이다.
- 기본 제품 방향은 `외함 = 기존 ScanBased`, `다른 구분 = IQC 미사용 또는 해당 구분 전용 Detailed`다. 모든 구분에 스캔형을 자유 선택하게 하는 범용 3-mode builder는 사용자 요구에서 확인되지 않은 과도한 확장 후보다.
- 양식 관리는 기존 `Item별 제조 양식`과 `생산계획·실적 연결`처럼 대상을 먼저 고르고 현재 양식 하나를 직접 관리하는 UX를 재사용해야 한다. 구분 수가 늘어날 수 있으므로 검색·필터 또는 빠른 대상 선택을 함께 제공해야 한다.
- 기존 전역 `MATERIAL_IQC`는 legacy `AllReceipts` 프로젝트의 상세검사를 위해 보존돼야 하며, 신규 구분별 양식과 적용 대상을 화면에서 혼동하지 않게 설명해야 한다.
- 기존 계약을 그대로 따르면 검사 여부/방식은 구매품 저장 snapshot으로 고정하고, 상세 체크리스트의 실제 항목은 도착분 검사 report를 시작하는 시점의 해당 구분 `현재 양식`으로 고정한 뒤 확정 후 불변으로 보존하는 것이 최소 변경안이다.
- 검사 항목·사진·판정·PDF·부적합 Pending·재검사 비교는 기존 Detailed IQC 엔진을 재사용하며 새 검사 엔진을 만들지 않는다.

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 양식 관리에는 구매품 구분별 `IQC 필요/없음` 설정과 전역 IQC 현재 양식 한 개가 따로 있다. 상세 IQC 도착분은 전역 양식 하나를 사용하고 외함은 별도 스캔형 IQC를 사용한다.
- 해결할 문제: 구매품 구분이 늘어나면 목록에서 원하는 구분을 찾기 어렵고, IQC가 필요한 구분마다 검사 내용이 달라도 서로 다른 양식을 적용할 수 없다.
- 현재 우회 방식: 전역 IQC 양식에 모든 검사항목을 합치거나, 외부 문서·자유 형식 사유로 구분별 차이를 보완해야 한다.
- 성공했을 때 사용자가 할 수 있는 일: 양식 관리에서 구매품 구분을 빠르게 찾고 구분별 IQC 운영 방식과 현재 검사 항목을 관리하며, 이후 적용 대상 구매품의 도착분은 저장된 연결에 맞는 검사를 수행한다.
- 하지 않을 경우 영향: 구분이 늘수록 잘못된 항목을 검사하거나 불필요한 항목을 반복 입력하고, 검사 기준과 실제 성적서가 분리된다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 품질 양식 관리자 | 구매품 구분별 IQC 운영 방식·현재 양식 조회와 항목 편집 | 품질 양식 관리 범위 | 허용된 구분의 IQC 설정·양식 | 변경자·시각·이전/이후 값, 동시성 |
| System Administrator | 전체 설정 조회와 관리 | 전체 | 전체 | 기존 관리자 감사 계약 |
| 구매 담당 | 구매품 구분 선택 | 담당 프로젝트 | 구매품 구분 snapshot | 기존 구매 변경 이력 |
| 품질 IQC 담당 | 도착분에 연결된 IQC 양식으로 검사 | 담당 프로젝트 IQC | 현재 검사 초안·확정 | 확정 후 불변, 회차 보존 |
| 자재·관련 조회 사용자 | IQC 대상 여부·진행·결과 조회 | 기존 project scope | 검사 양식 변경 없음 | 기존 입고·업무 계약 |

권한의 정확한 편집 범위는 Fable 질문과 사용자 확인으로 확정한다.

## 3. 정상·예외·복구 흐름

- 정상 흐름: 양식 관리에서 구매품 구분 검색/선택 → 해당 구분의 IQC 운영 방식과 현재 양식 편집·저장 → 적용 대상 구매품 저장 → 도착 등록 → 연결된 방식/양식으로 IQC 생성·검사.
- validation 실패: 양식 미연결, 비활성 구분, 필수 항목 누락, stale 저장은 서버가 안정적인 한글 오류로 차단해야 한다.
- 동시 처리·중복: 현재 양식 저장과 구매품 snapshot은 optimistic concurrency와 단일 transaction 경계를 유지한다.
- 취소·재시도·복구: 확정 검사는 수정하지 않고 재검사는 새 회차를 사용한다. 잘못된 양식 설정은 과거 기록을 바꾸지 않고 이후 적용 범위에서 새 현재 양식으로 보정한다.
- 부분 실패와 rollback: 양식 저장 실패 시 기존 현재 양식을 유지하고, IQC 생성 시 유효한 snapshot이 없으면 임의 전역 양식으로 fallback하지 않는다.

## 4. Data·integration·lifecycle

- 기존 data 개념: 구매품 구분 catalog와 `requires_iqc`, 프로젝트 IQC routing snapshot, 구매품 구분 snapshot, IQC `Legacy/Detailed/ScanBased`, IQC template/version/items, report·response·photo·PDF snapshot.
- 신규 후보 개념: 구매품 구분별 IQC 운영 mode, 구분별 현재 IQC template 연결, 구매품 또는 도착분의 적용 양식 snapshot.
- 상태 전이: `검사 대상 아님`, 외함 스캔형, 상세 체크리스트형을 구분하되 실제 mode와 적용 시점은 사용자 확인이 필요하다.
- 보존·감사·삭제: 사용된 구분·양식·연결은 hard delete하지 않고 과거 snapshot을 유지한다.
- attachment·Excel·PDF: 기존 상세 IQC의 항목 사진·시스템 PDF와 외함 스캔 첨부 계약을 보존한다. 구매 Excel의 구분 선택 계약도 유지한다.
- 외부 연동·notification: 신규 외부 provider는 추가하지 않고 기존 인앱 업무·알림 연결을 재사용한다.
- migration·기존 데이터: additive migration과 기존 데이터 비소급 원칙을 사용한다. 기존 프로젝트 처리 정책의 세부 기준은 사용자 확인이 필요하다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 기존 `양식 관리` 안에서 구매품 구분을 검색·필터·선택하고 한 화면에서 해당 구분의 IQC 운영 상태와 검사 항목을 확인·수정한다.
- 디자인 기준: 현재 Graphite 양식 관리의 catalog, editor, badge, feedback, 간격과 반응형 구조를 재사용한다. 생산계획·실적 연결과 Item별 제조/LQC 양식의 탐색 패턴을 참고하되 화면을 새 디자인으로 바꾸지 않는다.
- loading·empty·error·success feedback: action 가까이 표시하고 중복 저장을 막으며 첫 오류 focus와 `aria-live`를 유지한다.
- 접근성·390px·Teams narrow: 검색/구분 선택→설정→항목 편집 순서를 한 열로 유지하고 page-level overflow 0을 검증한다.
- UAT와 rollout: 격리 DB와 합성 데이터만 사용한다. Persistent DB·Azure runtime·실제 provider는 별도 승인 대상이다.
- rollback과 운영자 대응: additive migration 적용 뒤에는 기존 migration을 수정하지 않고 forward-fix한다.

## 6. 포함·제외 범위

### 포함

- 구매품 구분 검색·필터·선택 UX
- 구매품 구분별 IQC 운영 방식과 현재 검사 양식 연결
- 구분별 상세 IQC 항목 편집과 실제 도착분 적용
- 기존 프로젝트·구매품·도착분에 대한 비소급 snapshot
- 권한·감사·동시성·fresh/existing migration·desktop/390px 검증

### 제외

- LQC 운영 상태·Item별 LQC 양식 재작업(Change 004)
- OQC·전진검수·FAT 운영 모델 변경
- 협력사용 Excel 검사서 생성·배포, OCR, scanner 직접 연동
- 구매품 구분 catalog 자체의 전면 재설계
- 기존 확정 IQC·PDF·첨부·Pending·재검사 이력 수정/삭제
- LSE TASK NO, 부서 Pending, 설계 도번·필수값·패널 묶음
- commit·push·PR·main merge, Persistent DB·Azure runtime·실제 provider

## 7. 선택과 결정

1. 구매품 구분마다 스캔형 또는 상세형을 선택할 수 있고 스캔형을 외함 전용으로 고정하지 않는다.
2. 초기 상태는 외함=스캔형 검사 켜짐, 판금류·부스바·명판·기타=검사 없음으로 유지한다. 실제 전환과 항목 입력은 기능 제공 후 품질팀이 수행한다.
3. 검사 여부와 방식은 구매품 저장·수정 저장 시점에 고정하며 이미 저장된 구매품에는 이후 설정을 소급 적용하지 않는다.
4. 상세형은 검사 항목이 1개 이상 저장된 뒤에만 검사 스위치를 켤 수 있고, 활성 상태에서는 항목 0개 저장을 차단한다. 스캔형은 항목 없이 유효하다.
5. 상세 검사 항목 내용은 도착분 검사 회차 시작 시점의 해당 구분 현재 양식으로 고정하고 확정 후 불변으로 보존한다.
6. 구분별 검사 스위치·방식·항목은 품질 domain 부서 양식 관리자 지정을 받은 활성 부서장과 시스템 관리자가 변경할 수 있다.
7. 시스템 관리자의 쓰기 권한은 유지하며 관리자 비관여는 운영 관행으로 둔다.
8. 기존 구매품 구분 화면의 `IQC 필요` 토글은 새 검사 설정과 같은 권한으로 통일하거나 새 설정으로 대체해 우회 경로를 없앤다.
9. 기존 Detailed·ScanBased IQC 엔진과 legacy `MATERIAL_IQC` 계약을 재사용·보존한다.
10. 설정·양식 변경은 append-only 감사와 optimistic concurrency를 적용하며 사용된 기록을 hard delete하지 않는다.

## 8. Fable 확인용 요약

- 해결할 문제: 전역 IQC 양식 한 개로는 구매품 구분별로 다른 검사를 운영할 수 없고 구분 수가 늘면 관리·탐색이 어렵다.
- 권장 범위: 기존 양식 관리에서 구매품 구분을 검색·선택하고 구분별 검사 스위치·스캔형/상세형 방식·상세 검사 항목을 관리하며, 구매품 저장 snapshot과 도착분 검사 생성에 연결한다.
- 확정한 정책: 위 7절의 열 가지 결정, 현재 디자인 재사용, 기존 확정 기록과 기존 프로젝트 비소급, Backend authoritative, desktop·390px 검증.
- 명시적 제외: 다른 3~5번 작업, 운영 배포, 실제 provider, 기존 기록 삭제.
- Deferred 비차단 결정: 없음. 필요한 정책은 Fable 질문과 사용자 확인으로 확정한다.
- Fable 판정: `COMPLETED_CONFIRMED`

## 9. 성공 기준

- 업무 결과: 품질 관리자가 구분별 IQC 설정·양식을 쉽게 찾고 관리하며 검사 담당자는 올바른 양식으로 도착분을 검사한다.
- 권한·데이터 불변조건: 과거 snapshot·확정 증빙 보존, 허용 사용자만 변경, stale write 차단, 임의 fallback 금지.
- 자동 검증: migration fresh/existing, Backend 권한·snapshot·검사 mode·재검사 회귀, Frontend 전체 검증, 격리 Full-Stack, desktop·390px.
- 사용자 검수: 자동 검증과 별도로 양식 관리·구매·IQC 동선을 직접 확인한다.

## 10. 사용자 확인

- [x] 업무 문제와 기대 결과가 정확하다.
- [x] 대상 역할과 권한이 정확하다.
- [x] 정상·예외·복구 흐름이 정확하다.
- [x] 포함·제외 범위가 정확하다.
- [x] Blocking 결정이 남아 있지 않다.
- [x] Fable 5가 작성한 요약을 planning 입력으로 사용하는 데 동의한다.

- `interviewStatus: COMPLETED_CONFIRMED`
- `userConfirmed: true`
- `openBlockingDecisionCount: 0`
- `planningApproved: false`
- `implementationApproved: false`
