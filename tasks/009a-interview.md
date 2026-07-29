# TASK-009A — IQC 디지털 성적서·필수 사진·PDF Snapshot Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5_EXPERIMENT_FAST_TRACK`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 Fable 5가 `TASK-009A`를 기획하기 위한 interview source of truth다. 사용자는 이 `experiment/*` worktree에서 사용자-facing interview와 중간 승인 없이 Fable 권장안을 채택해 `Fable 1차 기획 → Codex 내용 review → review 기반 Fable 2차 기획 → Codex 구현·검증·페이지별 screenshot·local commit`까지 연속 진행하도록 명시했다. 아래에는 Roadmap·USER-FLOW·완료된 실험 TASK-007A·008A·008B에서 확정된 계약만 기록하며, IQC 세부 양식·사진 저장·PDF 형식 같은 미확정 정책은 Fable의 비차단 권장안 대상으로 남긴다. 대표 repo, GitHub `main`, Persistent UAT, 실제 provider와 canonical runtime은 변경하지 않는다.

## Task Identity Gate

- proposedTaskId: `TASK-009A`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-009A`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 008A의 요청 건별 최소 적합·부적합 판정을 versioned IQC 체크리스트, 외함 필수 사진, 판정 근거와 불변 PDF snapshot을 갖춘 디지털 검사성적서로 확장한다.
- Root Finding 또는 정책 결정: 현재 IQC는 사유 1개와 결과만 저장해 어떤 항목을 확인했고 어떤 사진을 근거로 판정했는지 재현할 수 없으며, 승인 시점의 성적서를 다시 출력할 수 없다.
- 변경·검증 경계: 현재 experiment 계보의 additive migration·Backend·Frontend·isolated PostgreSQL·synthetic attachment/PDF·desktop/390px screenshot만 포함한다.
- 보존할 불변조건: 품질 권한 authoritative, 008A 도착 건·attempt·Pending transaction, stage 전진-only, append-only 감사, 인앱 원본, 실제 provider 차단, 대표 repo·main·Persistent UAT 불변.
- 예상 산출물: Fable 1차 planning 원문, Codex 내용 review, Fable 2차 planning 원문, 구현·자동 검증·desktop/mobile screenshot·implementation report·local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

Roadmap의 canonical `TASK-009A` 한 건 외에 같은 목적의 Task 문서·local/remote branch·PR은 0건이다. 기존 `/quality/iqc`와 migration `0030`은 TASK-008A가 명시적으로 만든 최소 판정 기반이며, 상세 성적서·사진·PDF는 TASK-009A에 위임되어 있어 ID 충돌이 아니다.

## 사용자 실행 지시

- 사용자 요청일: 2026-07-17
- 실행 형태: 현재 실험 worktree에서 다음 미착수 기능을 즉시 진행
- workflow: Fable 1차 기획 → Codex 내용 review → Fable 2차 기획 → Codex 구현·검증·screenshot·local commit
- 승인 대체: 비차단 제품 선택은 Fable의 Repository 근거 권장안을 자동 채택한다.
- 안전 예외: Repository 충돌, secret·개인정보 노출, 안전한 upload 경계 부재 또는 데이터 불변조건 위반은 fast-track으로 우회하지 않고 blocking decision으로 반환한다.
- 게시 경계: push·PR·merge 미승인, main merge 승인 `0/3`.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `COMPLETED_CONFIRMED` | 0 | 사용자 standing experiment 규칙, Roadmap 확정사항과 기존 008A 계약을 기록. 미확정 정책은 Fable 권장안 자동 채택 | Fable 1차 planning |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 품질 담당이 `/quality/iqc`에서 도착 건·검사 차수를 확인하고 합격/부적합과 3자 이상의 사유만 기록한다.
- 해결할 문제: 검사 항목별 확인 결과, 필수 외함 사진, 종합 판정과 판정 당시 데이터를 하나의 성적서로 보존하고 PDF로 다시 확인할 수 있어야 한다.
- 현재 우회 방식: 자유 형식 사유가 체크리스트·사진 증빙·성적서 출력 역할을 대신한다.
- 성공했을 때 사용자가 할 수 있는 일: 모바일에서 요청 건을 열어 항목을 체크·입력하고 사진을 등록한 뒤 판정하며, 완료된 성적서를 읽기 전용으로 보고 동일 snapshot PDF를 내려받는다.
- 하지 않을 경우 영향: IQC 판정 근거를 재현할 수 없고 부적합 조치·재검사·입고 확정의 품질 증빙이 분리된다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 품질 IQC 담당 | 검사 항목 입력, 사진 등록, 최종 판정, 완료 성적서·PDF 조회 | 기존 `QualityInspect` 접근 범위 | 현재 Requested attempt의 성적서 작성·최종화 | actor·시각·template version·snapshot 보존 |
| 자재 담당 | 요청·재검사, 판정 결과와 성적서 상태 조회, 합격 후 입고 확정 | 기존 자재 접근 범위 | 검사 내용 변경 불가 | 008A 상태·version gate 보존 |
| 생산관리·구매·Pending 담당 | 결과·부적합 근거와 연결 Pending 조회 | 기존 프로젝트 접근 범위 | 검사 mutation 불가 | 민감 첨부 접근권한 서버 강제 |
| Read-only·System Administrator | 승인된 조회·감사 | 기존 정책 범위 | 업무 mutation 우회 금지 | 다운로드까지 authorization 필요 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: IQC 요청 → 성적서 작성 → 필수 항목·외함 사진 검증 → 합격 또는 부적합 최종화 → 008A Passed/FailedBlocked와 Pending·work item 원자 처리 → 읽기 전용 성적서·PDF 제공.
- validation 실패: 필수 항목 미입력, 필수 사진 누락, 허용되지 않은 파일 형식·용량, 결과와 항목 불일치는 서버가 field-level 한글 오류로 차단한다.
- 동시 처리·중복: attempt·receipt version 또는 동등한 optimistic lock으로 경쟁 저장/최종화를 차단하고 동일 최종화·PDF snapshot을 중복 생성하지 않는다.
- 취소·재시도·복구: 완료 성적서는 수정·삭제하지 않는다. 재검사는 새 008A attempt와 새 성적서 instance를 사용하고 이전 판정·사진·PDF를 보존한다.
- 부분 실패와 rollback: 최종 성적서 snapshot, 008A 판정, Pending 생성/종결, work item 상태는 기존 Materials transaction owner 안에서 모순 없이 처리한다. binary 저장과 PDF 생성 시점의 원자성·재시도 방식은 Fable이 권장안을 정한다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: IQC template/version, template item, attempt별 report, item response, photo attachment metadata/content, immutable final snapshot, PDF artifact 또는 deterministic PDF projection.
- 상태 전이: 기존 attempt `Requested → Passed/Failed`와 receipt 상태를 authoritative로 유지하고, 성적서 작성 상태는 이를 우회하지 않는다.
- 보존·감사·삭제: 완료된 검사 항목·사진 hash·판정·template snapshot·PDF는 append-only다. hard delete와 완료 후 덮어쓰기를 제공하지 않는다.
- attachment·Excel·PDF: 외함 사진 필수는 확정이다. 상세 사진 위치·허용 MIME·크기·개수·storage·retention·backup, PDF 양식·font·snapshot 생성 방식은 Fable 권장안 대상으로 둔다. Excel은 제외한다.
- 외부 연동·notification: 기존 인앱 work item/Pending 연결만 재사용하며 Teams/Mail/Activity 실제 delivery는 생성하지 않는다.
- migration·기존 데이터: 현재 latest `0031` 다음 additive migration을 사용한다. 이미 판정 완료된 008A attempt를 거짓 상세 성적서로 backfill하지 않는 호환 projection 또는 legacy 표시 방식을 Fable이 정한다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 기존 `/quality/iqc` queue와 deep link를 유지하고 선택한 attempt에서 모바일 우선 단계형 검사 작성, 사진 등록, 검토·최종화, 완료 성적서·PDF 보기를 제공한다.
- loading·empty·error·success feedback: action 인접 feedback, 중복 submit 차단, upload 진행·실패·재시도, 첫 오류 focus와 `aria-live`를 제공한다.
- 접근성·390px·Teams narrow: PC table 축소가 아닌 한 열 단계형 composition, 44px hit area, camera/file input label, 사진 대체 설명, page-level overflow 0을 검증한다.
- UAT와 rollout: isolated synthetic PostgreSQL·합성 JPEG/PNG·provider disabled만 사용한다. Persistent UAT migration·runtime handover는 미실행한다.
- rollback과 운영자 대응: 적용 전에는 local branch 폐기로 종료할 수 있다. migration 적용 후에는 기존 migration을 수정하거나 table을 drop하지 않고 새 forward-fix와 artifact read-only 보존으로 복구한다.

## 6. 포함·제외 범위

### 포함

- IQC template/version과 초기 최소 system template
- attempt별 검사 항목 입력·필수값 검증·최종 합격/부적합 판정
- 외함 필수 사진 upload·조회와 안전한 experimental storage 경계
- 완료 성적서 immutable snapshot과 PDF 출력 기반
- 기존 008A Materials/Pending/work item transaction·재검사 cycle 연결
- desktop·390px·Teams narrow UI와 synthetic screenshot
- additive migration, authorization·concurrency·upload·PDF·full-stack tests

### 제외

- LQC·OQC·전진검수·FAT 전체 (`TASK-012A`)
- 관리자 template 편집·version 운영 UI (`TASK-ADMIN-002`)
- 실제 고객 양식 확정, 전자서명·승인 workflow, Excel import/export
- 외부 object storage·CDN·virus scanner 운영 연동과 실제 provider 발송
- Persistent UAT migration·write·runtime handover
- 대표 repo·GitHub main·push·PR·merge

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 초기 IQC 항목과 template 구조 | 고정 컬럼은 빠르지만 후속 품질 확장이 어렵고, 완전 관리자 template은 범위가 과도함 | Fable이 versioned 최소 system template을 권고하되 구체 항목을 명시 | Fable 권장안 자동 채택 | No |
| 2 | 작성·최종화 lifecycle | 즉시 최종화는 모바일 복구가 약하고, 장기 draft는 stale 관리가 필요함 | 기존 attempt 상태를 보존하는 최소 draft/finalize 계약을 Fable이 권고 | Fable 권장안 자동 채택 | No |
| 3 | 외함 사진 experimental storage | DB binary는 transaction·격리가 쉽지만 용량 부담, filesystem은 원자성·ownership·backup 계약 필요 | 보안·권한·magic-byte·크기 제한을 충족하는 최소안을 Fable이 선택 | Fable 권장안 자동 채택. 안전 경계 불가 시 Blocking | No |
| 4 | PDF snapshot 생성·보존 | 요청 시 재생성은 drift 위험, binary 저장은 용량·font 계약 필요 | 동일 snapshot 재현을 보장하는 최소 방식을 Fable이 선택 | Fable 권장안 자동 채택 | No |
| 5 | 008A 완료 attempt 호환 | 상세 backfill은 거짓 증빙, 숨김은 과거 이력 접근 저하 | 기존 결과는 legacy 최소 판정으로 명시하고 신규 요청부터 상세 성적서 적용하는 방향 검토 | Fable 권장안 자동 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: IQC 판정 근거·필수 사진·성적서 snapshot·PDF 재현성 부재.
- 권장 범위: 기존 008A attempt를 확장하는 versioned 검사성적서, 안전한 필수 외함 사진, immutable final snapshot·PDF, 모바일 우선 작성 UX.
- 확정한 정책: 품질 권한 authoritative, 외함 사진 필수, 완료 후 append-only, 재검사는 새 attempt, 008A/Pending transaction과 stage 전진-only 보존.
- 명시적 제외: 후속 품질·관리자 template UI·실제 고객 양식·외부 storage/provider·Persistent UAT·게시.
- Deferred 비차단 결정: 실제 운영 object storage·virus scan·장기 retention/backup, 고객 PDF 양식과 상세 IQC 현업 항목.
- Fable 판정: `COMPLETED_CONFIRMED` — 사용자 명시적 experiment interview waiver에 따른 planning 입력 상태.

## 9. 성공 기준

- 업무 결과: 품질 담당이 요청 건을 모바일에서 검사하고 필수 외함 사진과 항목 근거를 남겨 판정하며, 완료 성적서와 동일 snapshot PDF를 조회한다.
- 권한·데이터 불변조건: mutation/download 서버 권한, final immutable, 재검사 attempt 분리, 008A 상태·Pending·work item 원자성, upload signature·size guard.
- 자동 검증: migration fresh/existing, Backend build·전체/권한/transaction/concurrency/upload/PDF tests, Frontend lint·typecheck·unit·build, isolated E2E, desktop·390px·Teams narrow, PDF signature·snapshot consistency.
- 사용자 검수: synthetic 페이지별 screenshot과 PDF sample을 보고하되 사용자 직접 검수 완료로 표시하지 않는다.

## 10. 사용자 확인

- [x] 사용자 standing rule로 interview 질문 왕복과 중간 승인을 생략한다.
- [x] Roadmap·008A에서 확정된 업무 문제·역할·불변조건을 planning 입력으로 사용한다.
- [x] 비차단 선택은 Fable 권장안을 자동 채택한다.
- [x] Repository 충돌·unsafe upload·secret/개인정보 위험은 fast-track으로 우회하지 않는다.
- [x] 대표 repo·main·Persistent UAT·provider·게시를 제외한다.
- [x] open blocking decision 0인 경우에만 1차 planning을 시작한다.

확인 source: 사용자는 이 실험 worktree의 신규 작업을 인터뷰 없이 Fable 권장안으로 바로 1차 기획·Codex review·Fable 2차 기획·Codex 구현하고 결과물을 보여주도록 반복 명시했다.
