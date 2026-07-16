# TASK-USER-FLOW-001 — Fable Redraft 내용·제품 방향 Review

## 1. Review 메타데이터

| 항목 | 값 |
| --- | --- |
| taskId | `TASK-USER-FLOW-001` |
| reviewer | `CODEX` |
| reviewType | `CONTENT_PRODUCT_DIRECTION` |
| sourceArtifact | `docs/13-user-flow-baseline.md` |
| sourceAuthor | `FABLE_5` |
| sourceModificationByCodex | `false` |
| redraftContract | `tasks/user-flow-001-change-004.md` |
| previousContentReview | `tasks/user-flow-001-preview-review.md` |
| reviewStatus | `CONTENT_REVIEW_COMPLETE` |

## 2. 결론

Fable redraft는 사용자가 요청한 개인 개발 판단 자료의 목적과 Change 004를 충족한다. 기존 문서의 전체 업무 coverage를 보존하면서 canonical·온보딩·Phase B·전수 갱신 의무를 제거했고, 다음 개발 대상을 선택하는 데 필요한 병렬 업무 단위, 최소 vertical slice, 공통 복구 질문과 성공 신호를 보강했다.

현재 내용에서 게시를 차단하는 P0·P1·P2는 확인되지 않았다. 이 review는 제품 구현이나 Roadmap 순서 변경을 승인하지 않으며, `TASK-007A`부터 각 기능은 별도 Fable interview·planning·사용자 승인을 계속 거친다.

## 3. 사용자 문제와 목적 적합성

사용자의 문제는 “앞으로 어떤 기능을 추가 개발해야 하는지 명확하지 않다”는 것이었다. 재작성본은 다음 질문에 직접 답한다.

1. 현재 구현·부분 구현·미구현은 무엇인가.
2. 전체 18단계와 역할별 handoff는 어떻게 연결되는가.
3. 프로젝트·구매품목·패널·검사·포장 중 어느 단위가 병렬로 움직이는가.
4. 첫 end-to-end 가치 검증을 위해 무엇을 먼저 검토할 것인가.
5. 각 후속 Task 전에 어떤 정책·복구·운영 결정을 확인해야 하는가.
6. 어떤 지표로 다음 기능의 우선순위를 다시 판단할 것인가.

설명서 중심이던 기존 원문보다 개인 개발 의사결정에 필요한 우선순위·의존성·성공 신호의 비중이 커졌으므로 목적 적합성은 `충족`으로 판정한다.

## 4. 방향별 판정

| 대상 | 판정 | 내용 review |
| --- | --- | --- |
| 개인 개발 의사결정 지도 | 유지 | 문서 첫 절에서 목적과 우선 source를 명확히 분리했다. |
| 18단계 handoff·13개 여정 | 유지 | 현재/예정 상태와 다음 담당자 연결을 보존했다. |
| 공통 진입점·예외 E1~E4 | 유지 | 실제 구현과 후속 위임을 구분했고 재배정 누락을 별도 질문으로 드러냈다. |
| 업무 단위·병렬 진행 map | 추가 | 프로젝트 전체 stage와 구매품목·패널 상태를 잘못 결합할 위험을 줄인다. |
| 최소 vertical slice | 추가 | 자재·품질·Pending·handoff를 한 번에 검증할 가장 작은 흐름을 제시한다. |
| 재배정·정정·재개·부분 실패 | 추가 | 후속 기능에서 반복 확인할 공통 복구 질문으로 적절히 남겼다. |
| 첨부 storage·성공 신호 | 추가 | 기능별 중복 설계를 줄이고 다음 우선순위 판단 근거를 만든다. |
| Pending 메뉴 위치 | 보류 | contextual 진입과 전용 workspace를 `TASK-007A` 후보로 함께 열어 두었다. |
| Home·전체 모바일 묶음·알림 preference | 보류 | 핵심 업무 slice 뒤의 권고로 분리했고 Roadmap 자동 변경으로 쓰지 않았다. |
| canonical 제품 계약 | 제거 | Roadmap·최신 Task·Backend 정책 우선순위를 명시했다. |
| 온보딩·교육 역할 | 제거/후행 | 역할별 UAT 뒤 Later 후보로 이동했다. |
| Phase B·전수 갱신 의무 | 제거 | 개인 참고 자료 사용과 문서 정렬을 분리하고 사건 기반 재검토 권고로 바꿨다. |

## 5. 승인·권고·미확정 경계

- `확정/현재 계약`: 18단계, 담당자 fallback, 권한·알림·Backend authoritative 정책과 현재 구현 상태를 기존 source에 연결했다.
- `권고/후보`: `Pending → 병목 집계 → 자재 도착 → IQC → 키팅 → 제조 handoff`, Now/Next/Later, 업무 단위 map 활용과 성공 지표 선택을 별도 승인 전 제안으로 표시했다.
- `미확정/후속 위임`: Pending 위치, 첨부 storage, 재배정, 정정·재개, 검사·포장 세부, 모바일 현장 UX와 due date 동기화를 개별 Task로 남겼다.

이 세 범주가 본문에서 명시적으로 반복되므로 개인 참고 자료가 제품 승인 문서로 오해될 위험이 기존보다 낮다.

## 6. 제품 가치와 권장 다음 단계

가장 가치가 큰 권고는 `TASK-007A` Pending List를 먼저 기획하고, 안정화 뒤 `TASK-007B` 병목 집계를 연결한 다음 자재 도착·IQC·키팅·제조 handoff의 vertical slice를 검증하는 것이다. 이 순서는 다음 이유로 타당하다.

- Pending은 검사 부적합·제조 중단·PUNCH의 공통 차단 언어다.
- 병목 집계는 Pending과 하위 단위 상태가 있어야 실제 가치를 낸다.
- 자재→IQC→키팅은 구매품목과 패널 단위의 병렬성을 실제 데이터로 검증한다.
- 제조 내 업무 생성까지 이어져야 EMI의 핵심 가치인 부서 간 handoff가 증명된다.
- Home·전면 디자인·알림 preference는 이 데이터와 업무 흐름이 안정된 뒤 더 정확하게 설계할 수 있다.

다만 이 권고는 Roadmap 변경이나 구현 승인이 아니다. 현재 문서 merge 뒤 Roadmap의 다음 canonical Gate는 `TASK-007A`의 별도 Fable deep-interview다.

## 7. 남은 한계

- 성공 신호의 실제 기준값·수집 방식은 아직 정하지 않았다. `TASK-007A` planning에서 2~3개를 선택해야 한다.
- 첨부 storage 계약은 외부 blocker로 남아 있다. Text-first slice를 허용할지는 `TASK-007A`에서 결정해야 한다.
- 재배정·완료 정정·reopen은 공통 질문으로만 등록됐다. 신규 상태·권한이 필요하면 각각 별도 `NEW_FEATURE` 또는 `POLICY_DECISION` Gate가 필요하다.
- 병렬 dependency map은 개인 판단을 위한 개념도이며 실제 DB model이나 aggregate 계약이 아니다.

이 항목들은 현재 문서의 결함이 아니라 후속 planning으로 의도적으로 위임된 결정이므로 P2로 판정하지 않는다.

## 8. Finding과 판정

| Finding | Severity | 상태 | 근거 |
| --- | --- | --- | --- |
| `USER_FLOW_DOC_STATUS_CONFLICT` | P2 | `RESOLVED` | canonical·온보딩·Phase B·전수 갱신 선언을 제거하고 개인 참고 자료·source 우선순위를 명시했다. |
| `USER_FLOW_PENDING_LOCATION_PREMATURE` | P2 | `RESOLVED` | Pending 위치를 확정에서 `TASK-007A` 후보로 변경하고 전용 workspace 가능성을 보존했다. |
| `USER_FLOW_PARALLEL_UNIT_GAP` | P2 | `RESOLVED` | 프로젝트·구매품목·패널·검사·포장 단위 dependency map과 차단 범위를 추가했다. |

- Open P0/P1/P2/P3: `0/0/0/0`
- contentReviewComplete: true
- sourceModificationByCodex: false
- productImplementationApproved: false
- phaseBDocumentAlignmentApproved: false
- publicationGateRecommendation: `GO_AFTER_INDEPENDENT_VERIFICATION`
