# TASK-012A — LQC·OQC·전진검수·FAT 후속 품질 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5_EXPERIMENT_FAST_TRACK`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 Fable 5가 `TASK-012A`를 기획하기 위한 interview source of truth다. 사용자는 이 `experiment/*` worktree에서 사용자-facing interview와 중간 승인 없이 Fable 권장안을 채택해 `Fable 1차 기획 → Codex 내용 review → review 기반 Fable 2차 기획 → Codex 구현·검증·페이지별 screenshot·local commit`까지 연속 진행하도록 명시했다. 아래에는 Roadmap, `TASK-009A` IQC 성적서, `TASK-011A` 제조 실행·panel LQC skeleton과 실제 Repository에서 확인된 계약만 기록한다. 미확정 LQC/OQC 실제 양식·사진 필수 위치·전진검수/FAT 세부 항목은 Fable의 비차단 권장안 대상으로 남긴다. 대표 repo, GitHub `main`, Persistent UAT, 실제 provider와 canonical runtime은 변경하지 않는다.

## Task Identity Gate

- proposedTaskId: `TASK-012A`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-012A`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 제조 완료 panel에 생성된 LQC 업무를 시작점으로 LQC·제조 완료 확인·OQC·전진검수·선택 FAT를 panel 단위로 수행하고, 부적합·PUNCH는 Pending 조치와 재검사로 연결해 다음 단계로 안전하게 인계한다.
- Root Finding 또는 정책 결정: 현재 IQC 상세 성적서와 제조 완료 panel의 LQC skeleton 업무는 존재하지만, LQC 이후 검사 record·화면·판정·PUNCH·재검사·다음 업무 handoff가 없다. generic 내 업무 완료로는 검사 증빙과 Pending 불변조건을 보장할 수 없다.
- 변경·검증 경계: 현재 experiment 계보의 additive migration·Backend·Frontend·isolated PostgreSQL·synthetic data·desktop/390px screenshot만 포함한다.
- 보존할 불변조건: 18단계 `LQC → 제조 완료 → OQC → 전진검수 → 선택 FAT → 포장` 순서, panel 단위 전진-only, FAT optional, Backend 권한·project scope, finalized 증빙 append-only, Pending 재검사 감사, 실제 provider 차단, 대표 repo·main·Persistent UAT 불변.
- 예상 산출물: Fable 1차 planning 원문, Codex 내용 review, Fable 2차 planning 원문, 구현·자동 검증·desktop/mobile screenshot·implementation report·local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

Roadmap의 canonical `TASK-012A` 한 건 외에 같은 목적의 Task 문서·local/remote branch·worktree·open/merged PR은 0건이다. `TASK-009A`는 구매품목 IQC, `TASK-011A`는 제조 실행과 LQC skeleton까지만 구현해 이번 목적과 중복되지 않는다.

## 사용자 실행 지시

- 사용자 요청일: 2026-07-17
- 실행 형태: 현재 실험 worktree에서 다음 미착수 기능을 즉시 진행
- workflow: Fable 1차 기획 → Codex 내용 review → Fable 2차 기획 → Codex 구현·검증·screenshot·local commit
- 승인 대체: 비차단 제품 선택은 Fable의 Repository 근거 권장안을 자동 채택한다.
- 모바일 원칙: PC 화면을 줄인 반응형이 아니라 검사 담당자의 현장 행동을 재구성한 적응형 화면, 작은 글씨·도형으로 핵심 정보 밀도 확보, 좌상단 숨김 메뉴, 다양한 도형을 사용한다.
- 안전 예외: Repository 충돌, secret·개인정보 노출, 18단계 순서·FAT optional·panel 전진-only·finalized 증빙·Pending 감사 무결성 위반은 fast-track으로 우회하지 않고 blocking decision으로 반환한다.
- 게시 경계: push·PR·merge 미승인, main merge 승인 `0/3`.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `COMPLETED_CONFIRMED` | 0 | 사용자 standing experiment 규칙과 Roadmap·009A·011A 계약 기록. 미확정 정책은 Fable 권장안 자동 채택 | Fable 1차 planning |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 제조 실행 완료 시 panel target `LQC` 내 업무는 생성되지만 링크는 임시 workflow fallback이고 검사 데이터·전용 화면이 없다. OQC·전진검수·FAT도 workflow stage와 담당자 구조만 있고 실제 입력·판정·재검사 기능이 없다.
- 해결할 문제: 품질 담당자가 자신에게 배정된 panel을 모바일에서 열어 현재 검사 단계를 확인하고 최소 체크리스트·값·선택 사진·판정 근거를 남긴 뒤 합격이면 다음 18단계 업무를 만들고, 부적합/PUNCH면 정확한 panel Pending으로 차단해야 한다.
- 현재 우회 방식: 검사 결과와 고객 지적을 종이·사진·메신저로 관리하고 workflow는 skeleton 또는 generic 업무 action으로만 해석한다.
- 성공했을 때 사용자가 할 수 있는 일: LQC/OQC/전진검수/FAT queue에서 panel을 선택해 성적서를 작성·확정하고 PDF snapshot을 확인한다. 실패/PUNCH는 Pending 조치·재검사 후 새 attempt로 이어지며, FAT 비대상은 자동으로 건너뛴다.
- 하지 않을 경우 영향: 제조에서 품질·물류로 이어지는 10~14단계가 시스템 밖에 남고, 부적합/PUNCH 이력·재검사와 프로젝트 진행률이 실제 검사 근거를 반영하지 못한다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| LQC 정·부 담당 | LQC queue·성적서 작성·판정·재검사 | 기존 project access와 LQC 업무 panel | 활성 LQC attempt만 | finalized snapshot·actor·시각·판정·사진·PDF 보존 |
| 제조 정·부 담당 | LQC 합격 후 제조 완료 확인, 다음 OQC handoff | 기존 project access와 제조 완료 업무 panel | 제조 완료 확인 업무만 | 18단계 11번을 건너뛰지 않고 exactly-once event·업무 전이 |
| OQC 정·부 담당 | 자체검수 성적서 작성·판정·재검사 | 기존 project access와 OQC 업무 panel | 활성 OQC attempt만 | finalized 증빙과 Pending 연계 |
| 전진검수/FAT 정·부 담당 | 전진검수·FAT 결과와 PUNCH 등록·재검사 | 기존 project access와 해당 업무 panel | 활성 CustomerInspection/FAT attempt만 | FAT optional·PUNCH 감사·고객 원문 과다 노출 금지 |
| 생산관리·영업·자재·물류·조회 역할 | 허용 project의 검사 상태·완료 증빙 조회 | 기존 project access | 검사 mutation 불가 | scope 밖 식별자 비노출 |
| Pending 조치 담당자 | 부적합/PUNCH 조치·상태·댓글 | 기존 Pending scope | 기존 Pending 계약 안에서만 | append-only history·expected version |
| System Administrator | 기준·이력 조회 | 기존 관리자 정책 | 검사·제조 업무 입력 무제한 우회 금지 | 서버 authorization과 감사 유지 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: panel LQC skeleton → LQC 성적서 합격 → 제조 완료 확인 업무 → 제조 확인 → OQC 성적서 합격 → 전진검수 합격 → FAT 필요 시 FAT 합격, 불필요 시 skip → 포장 skeleton handoff.
- validation 실패: 선행 stage/업무 없음, 이미 완료, 필수 항목·근거 누락, stale version, 잘못된 담당 단계, FAT 비대상, scope·권한 불일치를 서버가 안정적인 한글 오류로 차단한다.
- 동시 처리·중복: panel+stage별 active attempt 최대 1건, work item idempotency, optimistic version·row lock으로 중복 최종화·다음 업무·Pending·workflow event 생성을 막는다.
- 취소·재시도·복구: network 재시도는 완료 결과를 중복 생성하지 않는다. 부적합/PUNCH의 기존 attempt는 불변으로 보존하고 Pending 재검사 요청 뒤 새 attempt를 만든다. 완료된 성적서를 되돌리거나 덮어쓰지 않는다.
- 부분 실패와 rollback: 성적서 판정·attempt 완료·panel 상태·현재 업무 완료·Pending 또는 다음 업무·project stage event는 하나의 transaction이거나 전부 rollback한다. PDF rendering은 판정 transaction 뒤 derived artifact 상태로 분리한다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: panel quality inspection attempt(stage `LQC|OQC|CustomerInspection|FAT`), stage별 system template snapshot/version, report response·선택 사진·canonical snapshot·PDF artifact, Pending link, operation/version.
- 상태 전이: stage별 `Requested → Draft/InProgress → Passed|FailedBlocked`, 재검사는 새 attempt다. panel의 coarse workflow stage와 project workflow event는 전진-only이며 실패 시 단계 번호를 후퇴시키지 않는다.
- 보존·감사·삭제: finalized report·response·사진·snapshot·PDF, 판정·Pending·재검사 이력을 hard delete·덮어쓰기하지 않는다. project permanent purge는 기존 승인된 FK 역순 정합만 보강한다.
- attachment·Excel·PDF: `TASK-009A`의 bounded JPEG/PNG·canonical snapshot·저장 PDF 안전 계약을 재사용 후보로 둔다. 실제 고객 양식·전자서명·Excel은 제외한다. 미확정 사진 필수 위치를 임의로 required로 만들지 않는다.
- 외부 연동·notification: 기존 인앱 work item·notification/outbox만 재사용하며 실제 Teams/Mail/Activity provider는 실행하지 않는다.
- migration·기존 데이터: current latest `0034` 다음 additive migration을 사용한다. 기존 IQC/제조/Pending migration을 수정하지 않고 기존 panel에 가짜 품질 attempt/report를 backfill하지 않는다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 기존 전역 `IQC`를 분절된 검사 메뉴 여러 개로 늘리지 않고 `품질` 공통 진입으로 묶을지 Fable이 권장한다. 내 업무 deep link는 정확한 stage·panel을 연다. 모바일은 단계 queue → panel focus → `항목 → 사진/근거 → 판정` 한 화면 한 행동이고 desktop은 stage queue와 detail을 함께 본다.
- loading·empty·error·success feedback: queue loading/empty/error, 저장 중·완료·차단·PDF 상태와 다음 행동을 action 가까이 표시하고 중복 submit을 차단한다.
- 접근성·390px·Teams narrow: PC table 축소가 아닌 compact stage rail·card·sheet, 44px touch target, 작은 보조 글씨, 좌상단 숨김 메뉴·page-level overflow 0을 유지한다. 원형·타원형·각진/둥근 직사각형·정사각형을 상태·행동 의미에 맞게 사용한다.
- UAT와 rollout: isolated synthetic PostgreSQL·provider disabled만 사용한다. Persistent UAT migration·runtime handover는 미실행한다.
- rollback과 운영자 대응: 적용 전에는 local branch 폐기로 종료할 수 있다. migration 적용 후에는 additive forward-fix와 finalized 증빙·Pending/work item 보존으로 복구한다.

## 6. 포함·제외 범위

### 포함

- panel별 LQC·OQC·전진검수·FAT queue, attempt, 성적서·판정·이력
- LQC 합격 뒤 18단계 11번 제조 완료 확인과 OQC handoff의 최소 연결
- stage별 최소 system template와 선택 사진, canonical snapshot·저장 PDF
- 부적합은 `Nonconformance`, 전진검수/FAT PUNCH는 `Punch` Panel target Pending과 재검사
- panel별 다음 업무 exactly-once와 project stage completion의 모든 active panel 집계
- FAT 비대상 skip과 물류 포장 skeleton handoff
- 기존 IQC 성적서·Pending·workflow·project scope·authorization·PDF/font 계약 재사용
- 모바일 우선 adaptive 품질 화면과 desktop 관리/조회 composition
- additive migration, transaction·idempotency·authorization·Frontend·isolated E2E 검증

### 제외

- 실제 현업 LQC/OQC/FAT 상세 양식·사진 필수 위치의 임의 확정
- 관리자 template 편집·version activation (`TASK-ADMIN-002`)
- 실제 고객 PDF 양식·전자서명·승인 workflow·Excel export
- object storage·CDN·virus scanner·image transcoding, 신규 외부 알림 채널·실제 provider delivery
- 완료 성적서 수정·삭제·재발행·관리자 강제 합격·stage 되돌리기
- 물류 포장 상세(`TASK-013A`)와 영업 정산
- Persistent UAT migration·write·runtime handover
- 대표 repo·GitHub main·push·PR·merge

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 검사 데이터 구조 | IQC table을 억지 확장하면 구매품목 FK와 결합되고, 단계별 별도 table은 중복이 커진다. panel+stage 공통 모델은 4개 단계를 일관 처리할 수 있다 | IQC 증빙 계약을 재사용하되 `panel quality inspection` 공통 attempt/report 모델을 additive로 도입 | Fable 권장안 자동 채택 | No |
| 2 | 미확정 체크리스트·사진 | 빈 자유 입력은 증빙이 약하고, 실제 양식·사진 필수를 임의 확정하면 운영 오해가 크다 | stage별 소수의 일반 system template snapshot, 사진은 선택으로 두고 실제 양식은 새 version으로 후속 | Fable 권장안 자동 채택 | No |
| 3 | LQC와 OQC 사이 제조 완료 | 11단계를 자동 skip하면 18단계가 깨지고, 별도 대형 제조 기능은 과도하다 | LQC 합격 뒤 panel 제조 완료 확인 업무를 만들고 제조 화면에서 compact 확인 후 OQC를 생성 | Fable 권장안 자동 채택 | No |
| 4 | 부적합·PUNCH 재검사 | 같은 report 수정은 감사가 깨지고, 단계 후퇴는 Roadmap 위반이다 | finalized 실패 report + Panel Pending을 보존하고 `ReinspectionRequested`에서 새 attempt 생성, 합격 시 linked Pending 종결 | Fable 권장안 자동 채택 | No |
| 5 | 품질 진입 구조 | IQC/LQC/OQC/FAT 전역 메뉴를 각각 만들면 현장 메뉴가 비대해지고, 단일 IQC 명칭은 범위가 틀린다 | 전역 `품질` 한 개에서 IQC와 후속 검사 stage tab을 제공하고 내 업무는 정확한 stage/panel deep link | Fable 권장안 자동 채택 | No |
| 6 | project stage 완료 집계 | panel 하나의 완료로 project stage를 넘기면 미완료 panel이 숨고, 마지막 panel까지 다음 panel handoff를 막으면 병렬성이 사라진다 | panel별 다음 업무는 즉시 생성하고 project stage event는 모든 active panel이 해당 stage를 통과할 때 exactly-once 생성 | Fable 권장안 자동 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: 제조 뒤 LQC·제조 완료 확인·OQC·전진검수·FAT의 실제 검사·판정·Pending·재검사·handoff가 없음.
- 권장 범위: panel+stage 공통 inspection attempt/report, 최소 system template, 선택 사진·snapshot PDF, 품질 공통 화면, 18단계 순서·FAT optional·panel 병렬 handoff.
- 확정한 정책: Backend authoritative, project scope, panel 단위 전진-only, finalized append-only, 부적합/PUNCH Pending, 재검사는 새 attempt, FAT 선택 단계.
- 명시적 제외: 실제 현업 양식·사진 필수 위치, template 관리자, 고객 양식/전자서명, 물류 상세, provider·Persistent UAT·게시.
- Deferred 비차단 결정: 실제 checklist 문항·필수 사진 위치, 운영 storage·retention, template 관리와 PDF 양식.
- Fable 판정: `COMPLETED_CONFIRMED` — 사용자 명시적 experiment interview waiver에 따른 planning 입력 상태.

## 9. 성공 기준

- 업무 결과: 품질 담당자가 모바일에서 LQC/OQC/전진검수/FAT를 기록·판정하고 실패/PUNCH를 Pending 재검사로 연결하며 다음 stage를 정확히 인계한다.
- 권한·데이터 불변조건: mutation/read/download 서버 권한+scope, 18단계 순서·FAT optional, panel active attempt·next work·Pending·project event 중복 방지, finalized 증빙 불변과 transaction 감사.
- 자동 검증: migration fresh/existing, Backend build·전체/권한/transaction/concurrency tests, Frontend lint·typecheck·unit·build, isolated E2E, desktop·390px·Teams narrow overflow 0.
- 사용자 검수: synthetic 페이지별 screenshot을 보고하되 사용자 직접 검수 완료로 표시하지 않는다.

## 10. 사용자 확인

- [x] 사용자 standing rule로 interview 질문 왕복과 중간 승인을 생략한다.
- [x] Roadmap·009A·011A에서 확정된 업무 문제·역할·불변조건을 planning 입력으로 사용한다.
- [x] 비차단 선택은 Fable 권장안을 자동 채택한다.
- [x] Repository 충돌·18단계 순서·FAT optional·finalized 증빙·Pending 감사·secret/개인정보 위험은 fast-track으로 우회하지 않는다.
- [x] 대표 repo·main·Persistent UAT·provider·게시를 제외한다.
- [x] open blocking decision 0인 경우에만 1차 planning을 시작한다.

확인 source: 사용자는 이 실험 worktree의 신규 작업을 인터뷰 없이 Fable 권장안으로 바로 1차 기획·Codex review·Fable 2차 기획·Codex 구현하고 결과물을 보여주도록 반복 명시했다.
