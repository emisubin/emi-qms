# TASK-EXPORT-001 — 모든 페이지 Excel 출력 공통 기능 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5_EXPERIMENT_FAST_TRACK`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 `TASK-EXPORT-001`의 Fable 5 planning source of truth다. 사용자는 이 `experiment/*` 계보에서 사용자-facing interview와 중간 승인을 생략하고 권장안을 자동 채택해 `Fable 1차 기획 → Codex 내용 review → review 기반 Fable 2차 기획 → Codex 구현·검증·페이지별 screenshot·local commit`까지 연속 수행하도록 명시했다. 아래에는 Product Roadmap, 기존 Excel import/template 구현과 현재 experiment source에서 확인된 계약만 기록한다. export 대상 화면 우선순위, column 선택 방식, row 제한, audit 수준과 민감 필드 제외 정책은 Fable의 비차단 권장안 대상으로 남긴다. 대표 repo, GitHub `main`, Persistent UAT, 실제 provider와 canonical runtime은 변경하지 않는다.

## Task Identity Gate

- proposedTaskId: `TASK-EXPORT-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-EXPORT-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 주요 조회 화면의 현재 검색·필터·정렬과 사용자의 조회 권한을 그대로 적용해 안전한 `.xlsx` 파일을 내려받고, 각 화면이 같은 export 보안·파일·감사 계약을 재사용하게 한다.
- Root Finding 또는 정책 결정: Roadmap은 모든 주요 페이지의 Excel 출력을 요구하지만 현재 구현은 입력용 template·preview/apply 중심이며, 조회 결과 export는 화면마다 없거나 공통 계약이 없다. 화면별 임시 구현은 필터 누락·민감 필드 과다 노출·파일 수식 주입·권한 우회 위험을 만든다.
- 변경·검증 경계: 현재 experiment 계보의 공통 export 구조, 제한된 우선 화면 vertical slice, Backend·Frontend·isolated synthetic test와 desktop/390px screenshot만 포함한다.
- 보존할 불변조건: Backend 권한·scope authoritative, 현재 필터와 데이터 의미 보존, 원문 secret/개인정보·내부 ID 미출력, formula injection 방어, bounded resource 사용, import 계약 불변, Persistent UAT·provider·main 불변.
- 예상 산출물: Fable 1차 planning 원문, Codex review, Fable 2차 planning 원문, 공통 export 구현·자동 검증·desktop/mobile screenshot·implementation report·local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

Roadmap의 canonical `TASK-EXPORT-001` 한 건 외에 같은 목적의 planning·review·change·implementation report, local/remote branch, worktree와 open PR은 0건이다. `TASK-003B`와 기존 project·production planning·procurement·calendar Excel 기능은 import/template 목적이므로 동일 Task가 아니라 재사용 가능한 ClosedXML·파일 응답 기반이다. 과거 merged PR의 Excel 언급은 해당 import/template 또는 후속 Task 분리 기록이다.

## 사용자 실행 지시

- 사용자 요청일: 2026-07-18
- 실행 형태: 현재 실험 계보에서 `TASK-014A` 다음 기능을 인터뷰 없이 즉시 진행
- workflow: Fable 1차 기획 → Codex review → Fable 2차 기획 → Codex 구현·검증·screenshot·local commit
- 승인 대체: 비차단 제품 선택은 Fable의 Repository 근거 권장안을 자동 채택한다.
- 모바일 원칙: PC 표를 축소하지 않고 모바일에서는 export 범위 요약과 실행·완료 feedback을 작은 공간에 맞게 재배치한다. 좌상단 숨김 메뉴와 기존 적응형 화면 원칙을 유지한다.
- 안전 예외: 권한·scope 우회, 개인정보/secret·내부 식별자 노출, formula injection, 무제한 메모리·row 처리, Repository 계약 충돌은 fast-track으로 우회하지 않는다.
- 게시 경계: push·PR·merge 미승인, main merge 승인 `0/3`.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `COMPLETED_CONFIRMED` | 0 | 사용자 standing experiment 규칙과 Roadmap·기존 Excel 계약 기록. 미확정 정책은 Fable 권장안 자동 채택 | Fable 1차 planning |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 조회 데이터를 Excel로 전달하려면 화면을 수작업으로 옮기거나 입력용 양식을 잘못 재사용해야 한다. 현재 조회 조건과 권한이 파일에 동일하게 적용된다는 공통 보장이 없다.
- 해결할 문제: 사용자가 현재 보고 있는 업무 범위를 안전한 Excel 파일로 내려받고, Backend가 화면과 같은 필터·scope·민감도 규칙을 다시 검증해야 한다.
- 현재 우회 방식: 브라우저 복사·붙여넣기 또는 화면별 별도 export 구현은 컬럼·날짜·상태 의미가 달라지고 감사·formula 방어·파일명 규칙이 흩어진다.
- 성공했을 때 사용자가 할 수 있는 일: 허용된 주요 조회 화면에서 현재 조건과 export 컬럼을 확인하고 `.xlsx`를 생성해 후속 정리·보고에 사용하며, 권한 밖 행·민감 필드·내부 ID는 파일에 포함되지 않는다.
- 하지 않을 경우 영향: 업무 보고의 수작업과 오류가 계속되고, 화면별 export가 추가될수록 개인정보·권한·운영 비용 위험이 커진다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 업무 역할 | 현재 조회 조건의 Excel 생성·다운로드 | 기존 화면과 동일한 project/data scope | 업무 데이터 변경 없음 | export actor·화면 유형·필터 projection·row count 기록 수준을 Fable이 권장 |
| 조회 전용 역할 | 허용된 조회 결과 export | 기존 read scope 이하 | 없음 | 민감 컬럼 자동 제외 |
| System Administrator | 승인된 관리·감사 화면 export | 기존 admin read 정책 | 없음 | 업무 mutation 우회 없음, 고위험 데이터 최소화 |

신규 `data.export` permission을 둘지 각 화면 read permission을 재사용할지, 민감 화면을 어떤 단계로 포함할지는 Fable이 현재 permission matrix와 least-privilege 원칙을 근거로 권장한다.

## 3. 정상·예외·복구 흐름

- 정상 흐름: 화면 진입 → 검색·filter·sort 적용 → export action → Backend scope·필터·컬럼 재검증 → bounded workbook 생성 → 안전한 파일명으로 다운로드 → 완료 feedback.
- validation 실패: 지원하지 않는 화면·컬럼·필터, 권한 없음, row 제한 초과, 잘못된 날짜 범위와 취소된 요청을 안정적인 한글 오류로 반환한다.
- 동시 처리·중복: export는 read-only이며 같은 요청의 중복 실행이 업무 data를 바꾸지 않는다. resource concurrency 제한 필요성은 Fable이 권장한다.
- 취소·재시도·복구: 생성 중 중복 클릭을 막고 실패 시 조건을 유지한 채 재시도한다. 브라우저 다운로드 실패를 업무 성공으로 기록하지 않는다.
- 부분 실패와 rollback: workbook 생성이 실패하면 부분 파일을 제공하거나 audit를 성공으로 남기지 않는다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: 공통 export request/definition, 화면별 allowlisted column·filter adapter, workbook response와 필요 시 export audit metadata.
- 상태 전이: 업무 domain data 상태는 변경하지 않는다. 생성 요청의 별도 영속 상태가 필요한지는 Fable 권장안으로 정한다.
- 보존·감사·삭제: 원본 workbook 영구 저장은 기본 제외한다. audit가 필요하면 payload 원문 대신 화면 유형·row count·bounded filter projection만 기록한다.
- attachment·Excel·PDF: `.xlsx` 출력만 포함한다. import, CSV, PDF, ZIP 대량 묶음과 사용자 업로드는 제외한다.
- 외부 연동·notification: 외부 storage·메일·Teams 전송 없이 현재 브라우저 다운로드만 사용한다.
- migration·기존 데이터: audit persistence가 필요하지 않으면 migration 없이 기존 data를 read-only로 사용한다. 필요하면 다음 additive migration으로 제한하며 기존 migration을 수정하지 않는다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 각 포함 화면의 filter/action 영역에 공통 export action을 배치하고, 현재 범위·예상 row 제한·포함 컬럼을 사용자에게 간단히 알린다.
- loading·empty·error·success feedback: 생성 중 중복 submit 차단, 0건 처리, 권한·row 제한·서버 오류와 다운로드 완료를 구분한다.
- 접근성·390px·Teams narrow: 모바일은 PC action bar 축소가 아니라 compact action/summary sheet 또는 동등한 적응형 배치를 사용하며 overflow 0과 keyboard/focus/aria-live를 유지한다.
- UAT와 rollout: isolated synthetic data와 disposable runtime만 사용한다. Persistent UAT·실제 업무 파일 생성은 미실행한다.
- rollback과 운영자 대응: 공통 export route·button을 제거하면 기존 조회·import 계약은 그대로 동작해야 한다.

## 6. 포함·제외 범위

### 포함

- 공통 `.xlsx` 생성·파일명·header/date/number/status 표현·formula injection 방어
- 화면과 동일한 Backend read permission·project/data scope·filter allowlist
- Roadmap 후보 중 데이터 모델이 현재 실험 계보에서 안정된 우선 화면의 vertical slice
- desktop·390px export action과 loading/empty/error/success feedback
- 권한·필터·파일 타입·민감 필드·row/resource 제한 자동 검증

### 제외

- Excel import 계약 변경, CSV·PDF·ZIP·정기 batch·이메일/Teams 발송
- 사용자 정의 수식·pivot·chart·macro·template designer
- 실제 사용자·고객·프로젝트 원문을 이용한 UAT export
- 외부 storage·회계·BI·Microsoft Graph 파일 연동
- Persistent UAT migration·runtime handover, 대표 repo·GitHub main·push·PR·merge

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 요청 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 첫 구현 화면 범위 | 모든 화면 동시 구현은 넓고, 한 화면만 구현하면 공통성 검증이 약하다 | 서로 다른 데이터 형태 2~4개 화면으로 공통 구조를 증명하고 후속 adapter를 쉽게 추가하는 최소 범위 권장 | Fable 권장안 자동 채택 | No |
| 2 | 권한 모델 | 신규 global permission은 단순하지만 기존 read scope와 이중 정책이 되고, read permission 재사용은 최소 권한에 맞다 | 화면별 기존 read permission+scope를 기본으로 하고 추가 민감 gate 필요성만 분리 | Fable 권장안 자동 채택 | No |
| 3 | 컬럼 선택 | 고정 컬럼은 안전하지만 유연성이 낮고 임의 컬럼은 노출 위험이 있다 | 서버 allowlist 안에서 화면별 기본 컬럼과 제한된 선택을 제공할지 권장 | Fable 권장안 자동 채택 | No |
| 4 | 대용량 처리 | 무제한 동기 생성은 자원 위험, 비동기 job/storage는 범위가 크다 | bounded 동기 export의 row·시간·concurrency 제한과 초과 안내 권장 | Fable 권장안 자동 채택 | No |
| 5 | 감사 수준 | 저장 없음은 단순하지만 민감 export 추적이 없고, 파일 영구 저장은 과도하다 | 원문·파일 없이 최소 audit metadata 또는 기존 audit 재사용 여부 권장 | Fable 권장안 자동 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: 모든 주요 조회 화면에 공통 Excel 출력 요구가 있으나 현재는 입력용 Excel 기반만 있고 조회 필터·권한·민감 필드·안전성 계약이 없다.
- 권장 범위: 공통 server-side export 기반과 서로 다른 2~4개 우선 화면 vertical slice, 기존 read scope, bounded `.xlsx`, adaptive action UX.
- 확정한 정책: Backend authoritative, 현재 조회 조건 반영, allowlisted 컬럼, 민감정보·내부 ID·secret 배제, formula injection 방어, read-only, import 불변.
- 명시적 제외: 모든 화면 일괄 완료를 가장하지 않음, import/CSV/PDF/batch/external delivery/Persistent UAT/게시.
- Deferred 비차단 결정: 첫 화면 조합, 신규 permission 필요성, 제한된 컬럼 선택, row/concurrency 제한, audit persistence.
- Fable 판정 요청: `COMPLETED_CONFIRMED` — 사용자 명시적 experiment interview waiver에 따른 planning 입력 상태.

## 9. 성공 기준

- 업무 결과: 포함 화면에서 현재 필터·scope에 맞는 읽기 쉬운 `.xlsx`를 내려받고 0건·오류·대용량 상황을 이해할 수 있다.
- 권한·데이터 불변조건: 서버 read permission·scope, allowlisted columns, formula-safe text, bounded resource, domain write 0, raw PII/secret/internal ID 0.
- 자동 검증: Backend build·권한/filter/workbook tests, Frontend lint·typecheck·unit·build, isolated E2E download inspection, desktop·390px overflow 0.
- 사용자 검수: synthetic 화면별 screenshot을 보고하되 사용자 직접 검수 완료로 표시하지 않는다.

## 10. 사용자 확인

- [x] 사용자 standing rule로 interview 질문 왕복과 중간 승인을 생략한다.
- [x] Roadmap과 기존 Excel·권한·조회 계약을 planning 입력으로 사용한다.
- [x] 비차단 선택은 Fable 권장안을 자동 채택한다.
- [x] 권한·scope·민감정보·formula injection·resource·Repository 충돌은 fast-track으로 우회하지 않는다.
- [x] 대표 repo·main·Persistent UAT·provider·게시를 제외한다.
- [x] open blocking decision 0인 경우에만 1차 planning을 시작한다.

확인 source: 사용자는 이 실험 worktree에서 신규 작업을 인터뷰 없이 Fable 권장안으로 바로 기획·review·2차 기획·Codex 구현하고 결과물을 보여주도록 반복 명시했고, 2026-07-18 `TASK-014A` 완료 뒤 “다음작업 시작하라”고 요청했다.
