# TASK-QUALITY-OPERATING-MODEL-001 조사 입력

- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: true

## Task Identity Gate

- proposedTaskId: `TASK-QUALITY-OPERATING-MODEL-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: true
- instructionConflictCount: 0
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `검사·제조 양식 content 현업 입력 시 scope 확정`
- roadmapSequenceMatch: true
- samePurposeMatchCount: 0
- canonicalTaskId: `TASK-QUALITY-OPERATING-MODEL-001`
- reuseExistingTask: false
- explicitRoadmapOverrideApproved: false
- experimentStandingInstructionApplies: true
- experimentLedgerSelectedTask: `검사·제조 양식 content`
- policyInputResolution: `N/A`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: 실제 품질팀의 IQC·LQC·OQC·전진검수·FAT·도면 대조 업무를 현재 EMI 프로젝트 통합관리시스템에 어떻게 반영할지 독립적인 Codex 의견과 Fable 의견을 각각 제시한다.
- Root Finding 또는 정책 결정: 현재 시스템은 모든 구매품 도착분에 IQC를 자동 생성하고 고정 양식 중심으로 검사하지만, 실제 현장은 선택 품목 IQC·외함 협력사 원본 검사서·제조 중 LQC·프로젝트별 가변 OQC·고객 입회 검수 패키지·종이 도면 대조로 운영된다.
- 변경·검증 경계: Repository·실제 구현 read-only 조사와 Markdown 의견서 2개 작성만 포함한다. 제품 코드·DB·runtime·provider·Git 게시를 변경하지 않는다.
- 보존할 불변조건: 검사 결과와 완료 증빙의 불변 snapshot, Pending 재검사 이력, 패널·구매품목 단위, 담당자 권한과 project scope, 기존 프로젝트 양식 snapshot, 전진검수/FAT 패널 단위 판정을 보존한다.
- 예상 산출물: `docs/46-quality-system-fable-opinion.md`, `docs/47-quality-system-codex-opinion.md`.

## 사용자 현장 조사 원문 요약

### IQC

- 모든 구매품을 검사하지 않는다.
- 실제 IQC 대상은 `외함`, `판금류`, `부스바`, `명판` 네 종류다.
- 네 종류 모두 눈으로 확인하지만 공식 수입검사서를 작성·보관하는 대상은 현재 `외함`뿐이다.
- 구매팀이 품목명을 자유롭게 입력하면 시스템이 외함 등 검사 대상을 안정적으로 구분하기 어렵다.
- 외함 업체에는 EMI가 Excel 검사 양식을 미리 전달한다.
- 외함 입고 시 업체가 종이에 체크한 검사서가 제품과 함께 도착한다.
- 그 검사서에는 EMI가 직접 확인할 항목도 포함되며, 품질팀이 추가 검사·서명 후 보관한다.
- 사용자는 판금류·부스바·명판의 육안 검사도 기준을 만들고 기록 또는 성적서를 남기는 방안을 검토하고 싶다.

### LQC

- 현재 품질 담당자가 제조 중 검사를 수행한다.
- 공식 기준서와 성적서는 없다.
- 사용자는 LQC 기준과 성적서를 새로 만드는 것이 필요한지 의견을 원한다.

### OQC

- 현재 기준과 성적서가 비교적 명확하게 운영된다.
- 고객 요청·과거 문제·특이사항을 다음 프로젝트의 OQC 기준과 성적서에 계속 추가한다.
- 예: 이전 프로젝트의 문 경첩 파손 문제를 다음 OQC 항목으로 추가한다.
- 프로젝트에 문 또는 경첩이 없는 경우 해당 항목을 아예 제외할지, 전체 항목을 유지하고 `N/A`로 판정할지 품질팀도 고민 중이다.
- 프로젝트마다 적용할 OQC 기준과 성적서가 달라질 수 있다.

### 전진검수와 FAT

- 고객이 방문해 실제 제품·도면·기존 검사성적서를 종합 확인한다.
- 세부 체크리스트를 다시 작성하는 검사라기보다, 기존 모든 품질 증빙을 제시하고 고객 확인을 받는 절차에 가깝다.
- OQC 성적서를 요약한 별도의 고객용 성적서에 고객 서명을 받아 보관한다.

### 도면 대조

- 품질팀은 검사 중 도면과 실제 제품이 일치하는지 계속 확인한다.
- 현재는 검사성적서와 도면을 출력해 현장에 들고 다니며 검사한다.
- 시스템에서 종이 출력 의존을 줄이면서 정확한 도면 revision과 검사 기록을 연결할 방안이 필요하다.

## 현재 Repository 기준선

- 자재 도착 등록은 현재 구매품 종류와 무관하게 같은 transaction에서 IQC attempt·품질 내 업무를 생성한다.
- 현재 IQC는 구매품 도착분당 하나의 상세 성적서와 PDF를 만들며 단일 `MATERIAL_IQC` 양식을 사용한다.
- 현재 패널 품질은 LQC·OQC checklist와 전진검수·FAT 통합 판정을 제공한다.
- 현재 양식 관리 catalog는 실제 운영 화면에서 IQC·LQC·OQC 항목을 관리할 수 있으나 품목별 IQC routing, 프로젝트별 OQC 적용 계획, 협력사 원본 검사서, 도면 revision과 검사 보고서 연결은 제공하지 않는다.
- 현재 검사 보고서는 확정 후 불변 snapshot·PDF·사진, Pending 조치·재검사 이력을 보존한다.
- 기존 프로젝트는 생성·검사 시작 당시 양식 snapshot을 유지하는 원칙이 확정돼 있다.

## 두 의견서의 독립성 계약

- Codex 의견은 Fable 호출 전에 먼저 작성·고정하고 Fable이 읽을 수 없는 Repository 외부 임시 위치에 보관한다.
- Fable은 Codex 의견서를 읽지 않고 이 interview와 Repository 기준선만 사용한다.
- Codex는 Fable 결과를 수정·review·재작성하지 않는다.
- Fable 결과 생성 뒤 이미 고정한 Codex 의견을 그대로 Repository target에 옮긴다.
- 두 문서의 결론이 달라도 합치지 않는다.

## Fable 의견서 요청

Fable은 구현 계약이나 Codex review가 아니라 독립적인 제품·업무 설계 의견서를 작성한다.

- 현장 조사에서 확인된 사실, 현재 시스템의 문제, Fable의 제안을 구분한다.
- IQC 대상 분류와 구매 자유 입력 연결, 외함 협력사 검사서, 나머지 세 품목의 기록 수준을 다룬다.
- LQC 기준·성적서 필요성과 제조 단계 연결을 다룬다.
- OQC 프로젝트별 가변 항목, 과거 문제의 재발 방지 항목 관리와 `제외` 대 `N/A` 정책을 다룬다.
- 전진검수·FAT 고객용 성적서와 기존 증빙 묶음을 다룬다.
- 도면 revision·현장 조회·검사 결과 연결을 다룬다.
- 권장 정보구조, 데이터 개념, 단계별 도입 순서, 위험과 사용자 결정 항목을 포함한다.
- 아직 구현 승인을 받은 것이 아니므로 implementation approval을 주장하지 않는다.

## 2026-07-30 구현 확정 정책

### 적용 기준

- 전환 기준은 구매품 하나가 아니라 프로젝트다.
- 기능 업데이트 전에 등록된 프로젝트는 이후 추가되는 구매품과 도착분까지 모두 기존 `모든 구매품 IQC` 흐름을 유지한다.
- 기능 업데이트 이후 새로 등록되는 프로젝트부터 구매품 구분 기반 IQC routing을 적용한다.
- 기존 프로젝트 데이터는 자동 분류하거나 소급 변경하지 않는다.

### 구매품 구분

- 구매 입력에 `구분` 선택값을 추가한다.
- 신규 정책 프로젝트에서는 구매품 구분을 필수로 선택한다.
- 구분 목록은 양식 관리에서 추가·이름 변경·사용 중지할 수 있다.
- 이미 사용된 구분은 기록 보존을 위해 실제 삭제하지 않고 사용 중지한다.
- 각 구분에는 `IQC 필요` 또는 `IQC 없음`을 1:1로 설정한다.
- 초기 운영값은 `외함 = IQC 필요`, 그 외 구분 = `IQC 없음`이다.
- 도급·사급 여부와 무관하게 구분이 외함이면 IQC를 진행한다.

### 외함 IQC

- 시스템 안에서 별도 IQC 검사 항목이나 검사서를 만들지 않는다.
- 협력사가 보내온 종이 수입검사서에 품질팀이 직접 확인·서명한 뒤 스캔본을 근거로 등록한다.
- 품질팀은 도착분별로 `적합` 또는 `부적합`을 판정한다.
- 적합과 부적합 모두 서명된 스캔본 첨부가 필수다.
- PDF와 사진 파일을 여러 개 첨부할 수 있다.
- 확정 전에는 품질팀 사용자가 판정과 첨부를 수정할 수 있다.
- 다른 IQC·LQC·OQC·전진검수·FAT와 동일하게 확정 후에는 정정하지 않는다.
- 부적합 확정 시 기존 Pending 흐름으로 연결한다.
- 조치 후 재검사는 기존 판정을 고치지 않고 새 검사 회차를 만든다.
- 재검사 회차에도 새 서명 스캔본이 필수이며 최초 검사와 모든 재검사 회차를 보존한다.

### IQC가 없는 구매품

- 도착 등록 후 IQC를 만들지 않는다.
- 자재 담당자가 `입고 확정`을 눌러야 최종 입고가 완료된다.
- IQC만 생략하고 도착·부분 입고·입고 확정·이력·알림·내 업무 계약은 유지한다.
- 시스템은 IQC 미실시를 IQC 합격으로 표시하지 않고 `검사 대상 아님`으로 구분한다.

### 확정 후 불변성 대조 결과

- 현재 IQC·LQC·OQC·전진검수·FAT는 확정된 검사 응답·사진·핵심 정보·PDF를 DB trigger와 API에서 수정하지 못하게 막는다.
- 재검사는 기존 확정본을 수정하지 않고 새 attempt/report를 생성한다.
- 새 외함 스캔형 IQC도 이 계약을 그대로 따른다.

### 구현 승인

- 사용자는 위 확정 정책을 기준으로 기획과 구현을 바로 시작하도록 명시했다.
- `experiment/*` standing instruction에 따라 Fable 1차 기획, Codex review, Fable 2차 기획의 권장안을 자동 채택하고 구현·검증까지 이어간다.
- 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 외부 provider 변경은 승인 범위에 포함하지 않는다.
