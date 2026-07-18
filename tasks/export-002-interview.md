# TASK-EXPORT-002 — 선택 프로젝트 Excel 내보내기 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5_EXPERIMENT_FAST_TRACK`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 `TASK-EXPORT-002`의 Fable 5 planning source of truth다. 사용자는 프로젝트 여러 건을 직접 선택해 선택한 프로젝트만 Excel로 내보내는 기능이 있는지 확인하고, 없다면 구현한 뒤 프로젝트 페이지와 생성된 Excel 파일의 screenshot을 보고하도록 명시했다. 현재 `TASK-EXPORT-001`은 현재 filter 결과 전체 export만 제공하고 행 선택 UI·선택 subset API는 제공하지 않는다. 이 `experiment/*` 계보에서는 사용자-facing interview와 중간 승인을 생략하고 Fable 권장안을 자동 채택한다. 대표 repo, `main`, push·PR·merge, Persistent UAT와 실제 provider는 제외한다.

## Task Identity Gate

- proposedTaskId: `TASK-EXPORT-002`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-EXPORT-002`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_CREATE`

### Purpose identity

- 업무 목표: 프로젝트 목록에서 사용자가 필요한 프로젝트 여러 건을 명시적으로 선택하고, 선택한 subset만 기존 권한·scope와 Excel 안전 계약을 적용해 내려받는다.
- Root Finding 또는 정책 결정: 기존 `TASK-EXPORT-001`은 filter 결과 전체 export만 제공하므로 사용자는 일부 프로젝트만 필요할 때 검색 조건을 인위적으로 바꾸거나 파일에서 불필요한 행을 다시 삭제해야 한다.
- 변경·검증 경계: 프로젝트 목록의 desktop·390px selection UX, 선택 subset Backend 계약, 기존 workbook·audit 기반 재사용, isolated synthetic E2E와 페이지·Excel screenshot만 포함한다.
- 보존할 불변조건: 기존 filter 전체 export 유지, Backend permission·project scope authoritative, 선택하지 않은 row 0, 내부 ID·원문 filter·파일 bytes audit 미저장, formula-safe text·매출 permission column omission·2-slot resource fence 유지, import/Persistent UAT/main 불변.
- 예상 산출물: Fable 1차 planning 원문, Codex review, Fable 2차 planning 원문, 구현·자동 검증·desktop/mobile 페이지 screenshot·선택 결과 Excel screenshot·implementation report·local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

`TASK-EXPORT-001`은 공통 export 기반과 filter 결과 전체 내보내기 목적이므로 선행 의존성이지만, 사용자가 행을 명시적으로 선택하고 subset만 내보내는 이번 사용자 능력과 purpose identity가 다르다. 같은 목적의 Task·branch·worktree·PR과 `TASK-EXPORT-002` ID 충돌은 0건이다.

## 사용자 실행 지시

- 사용자 요청일: 2026-07-18
- 요청: 여러 프로젝트 선택 내보내기 기능이 없다면 구현하고 페이지와 Excel 파일 screenshot을 보고한다.
- workflow: Fable 1차 기획 → Codex review → Fable 2차 기획 → Codex 구현·검증·screenshot·local commit.
- 승인 대체: 비차단 제품 선택은 Fable의 Repository 근거 권장안을 자동 채택한다.
- 게시 경계: push·PR·merge 미승인, main merge 승인 `0/3`.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `COMPLETED_CONFIRMED` | 0 | 사용자 standing experiment 규칙과 현재 기능 부재·신규 요청 기록 | Fable 1차 planning |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 현재 검색·filter 결과 전체를 Excel로 받은 뒤 필요하지 않은 프로젝트 행을 파일에서 수동 삭제한다.
- 해결할 문제: 현재 목록에서 필요한 프로젝트를 여러 건 체크하고 정확히 그 프로젝트만 한 파일로 내려받는다.
- 현재 우회 방식: 프로젝트를 한 건씩 검색해 여러 파일을 만들거나 전체 파일을 만든 뒤 Excel에서 행을 제거한다.
- 성공했을 때 사용자가 할 수 있는 일: 2건 이상의 프로젝트를 선택하고 선택 건수를 확인한 뒤 단일 `.xlsx`에서 선택한 행만 확인한다.
- 하지 않을 경우 영향: 부분 보고·인계 때 수작업과 잘못된 행 포함 위험이 남는다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 프로젝트 조회 역할 | 현재 보이는 프로젝트 중 여러 건 선택·내보내기 | 기존 `ProjectRead`와 `ProjectAccessScope` 이하 | 업무 데이터 변경 없음 | 선택 원문 ID를 저장하지 않는 최소 export audit |
| 매출 조회 권한 역할 | 선택 프로젝트의 허용된 매출 열 포함 | `Project.SalesAmount.Read` 보유 시에만 | 없음 | 기존 column omission 유지 |
| 조회 전용 역할 | 허용된 프로젝트 subset export | 기존 scope 이하 | 없음 | 권한 밖 선택은 fail-closed |

## 3. 정상·예외·복구 흐름

- 정상 흐름: 프로젝트 목록 조회 → 여러 row/card 선택 → 선택 건수 확인 → 선택 내보내기 → Backend가 선택 집합 전체의 scope를 다시 검증 → 기존 workbook 계약으로 단일 파일 생성 → 완료 feedback.
- validation 실패: 선택 0건, 중복/비정상 ID, 허용 상한 초과, stale·삭제·scope 밖 프로젝트가 섞인 요청을 안정적인 한글 오류로 반환한다.
- 동시 처리·중복: 기존 2-slot no-wait gate와 중복 클릭 차단을 재사용한다.
- 취소·재시도·복구: 실패 시 현재 선택을 유지해 수정·재시도할 수 있게 한다. filter나 page 이동 때 선택 유지 범위는 Fable 권장안으로 정한다.
- 부분 실패와 rollback: 선택 집합 일부만 조용히 파일에 넣지 않는다. 전체가 검증되지 않으면 파일·성공 audit을 만들지 않는다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: client selection state와 bounded selected-project request만 추가하며 업무 domain 상태는 추가하지 않는다.
- 상태 전이: 프로젝트 데이터 변경 없음. 선택은 화면 로컬의 임시 UI 상태다.
- 보존·감사·삭제: 선택한 project ID 목록과 파일은 영속 audit에 저장하지 않는다. 기존 export kind·row count·filter/selection 사용 여부의 최소 metadata 재사용 여부를 Fable이 권장한다.
- attachment·Excel·PDF: 기존 server `.xlsx` 생성기만 재사용한다. CSV·PDF·ZIP·외부 저장은 제외한다.
- 외부 연동·notification: 없음.
- migration·기존 데이터: migration 없는 최소안을 우선하되 기존 append-only audit 의미가 거짓이 되면 additive forward-fix 필요성을 Fable이 판단한다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 프로젝트 목록 row/card의 선택 control, 전체 선택 의미, `선택 N건`과 `선택 내보내기` action을 제공한다.
- loading·empty·error·success feedback: 0건 disabled/안내, 생성 중 duplicate block, 성공 row count와 오류 후 선택 유지.
- 접근성·390px·Teams narrow: desktop table checkbox와 모바일 card 선택 mode를 각각 설계하고 keyboard label·focus·`aria-live`·overflow 0을 유지한다. 모바일은 PC table 축소판으로 만들지 않는다.
- UAT와 rollout: isolated synthetic 프로젝트만 사용한다. Persistent UAT와 실제 프로젝트는 사용하지 않는다.
- rollback과 운영자 대응: selection UI·subset route를 제거해도 기존 filter 전체 export와 프로젝트 목록은 그대로 동작해야 한다.

## 6. 포함·제외 범위

### 포함

- 프로젝트 목록 desktop·390px 다중 선택 UX
- 선택 집합 전체의 permission·scope·존재/상태 재검증
- 선택한 프로젝트만 기존 safe `.xlsx` 형식으로 생성
- 기존 매출 permission column omission·formula safety·resource gate·audit 재사용
- 페이지와 실제 Excel 파일 screenshot

### 제외

- 프로젝트 이외 화면의 다중 선택 export
- 검색 결과 전체를 모든 page에 걸쳐 선택하는 대량 selection job
- 선택한 프로젝트의 상세·패널·구매 데이터를 여러 sheet로 묶는 복합 보고서
- 기존 column picker·CSV·PDF·email/Teams 발송
- Persistent UAT·대표 repo·main·push·PR·merge

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 요청 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 전체 선택 의미 | 현재 화면의 보이는 row만 / 모든 filter 결과 | 사용자가 선택 범위를 오해하지 않는 bounded v1 권장 | Fable 권장안 자동 채택 | No |
| 2 | 선택 유지 | filter/page 변경 시 유지 / 안전하게 초기화 | stale·숨은 선택 위험과 반복 작업을 비교해 권장 | Fable 권장안 자동 채택 | No |
| 3 | 선택 상한 | 화면 page cap / 별도 상한 / 기존 10,000 | UI·request·DB 안전성을 고려한 동기 상한 권장 | Fable 권장안 자동 채택 | No |
| 4 | scope 밖·stale 항목 | 허용 항목만 부분 export / 전체 fail | 정보 누출과 사용자 복구 가능성을 고려한 fail-closed 권장 | Fable 권장안 자동 채택 | No |
| 5 | API와 audit | GET query / POST body, 기존 audit 재사용 / additive field | URL 노출·길이·append-only 의미를 고려한 최소안 권장 | Fable 권장안 자동 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: 기존 전체 filter export만으로는 일부 프로젝트만 필요한 사용자가 Excel에서 행을 다시 삭제해야 한다.
- 권장 범위: 프로젝트 목록에 bounded multi-select와 선택 subset server export를 추가하고 기존 안전·권한 계약을 재사용한다.
- 확정한 정책: 선택하지 않은 row 0, Backend 전체 집합 재검증, 부분 성공 금지, 기존 전체 export 불변, synthetic isolated 검증.
- 명시적 제외: 다른 화면, 전 page 대량 선택, 복합 multi-sheet, column picker, 외부 발송, Persistent UAT와 게시.
- Deferred 비차단 결정: 전체 선택 의미, filter/page 전환 선택 유지, 상한, stale 처리, request/audit 세부 계약.
- Fable 판정: `COMPLETED_CONFIRMED`

## 9. 성공 기준

- 업무 결과: 사용자가 프로젝트 2건 이상을 선택해 한 파일로 받고 파일에는 선택한 프로젝트만 존재한다.
- 권한·데이터 불변조건: 선택 집합 전체 scope 검증, 권한 밖 부분 export 0, 매출 권한 열 omission, 내부 ID·formula object 0, domain write 0.
- 자동 검증: Backend selection contract·권한·stale·중복·상한·workbook tests, Frontend selection state/unit, 전체 build/test, isolated E2E와 desktop·390px overflow 0.
- 사용자 검수: 선택된 프로젝트가 보이는 desktop/mobile 페이지 screenshot과 생성된 Excel screenshot을 보고하되 사용자 직접 검수 완료로 가장하지 않는다.

## 10. 사용자 확인

- [x] 사용자 standing rule로 interview 질문 왕복과 중간 승인을 생략한다.
- [x] 현재 기능 부재와 기존 export 기반을 planning 입력으로 사용한다.
- [x] 비차단 선택은 Fable 권장안을 자동 채택한다.
- [x] 권한·scope·부분 성공·formula/resource·Repository 충돌은 fast-track으로 우회하지 않는다.
- [x] 대표 repo·main·Persistent UAT·provider·게시를 제외한다.
- [x] open blocking decision 0인 경우에만 1차 planning을 시작한다.

확인 source: 2026-07-18 사용자가 “여러 프로젝트 선택해서 내보내는 기능도 있어? 없다면 만들고난 후 페이지와 엑셀파일 스크린샷 보여줘”라고 명시했다.
