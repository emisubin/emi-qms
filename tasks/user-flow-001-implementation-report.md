# TASK-USER-FLOW-001 — 개인 유저플로우 기획 산출물 Report

## 1. 범위와 작성 책임

- taskType: `NEW_FEATURE`
- phase: `COMPLETED_PR_55_MERGED`
- sourceArtifact: `docs/13-user-flow-baseline.md`
- sourceAuthor: `FABLE_5`
- contentReviewer: `GPT-5.6-SOL_INITIAL_AND_CODEX_REDRAFT_REVIEW`
- sourceModificationByCodex: `false`
- productSourceModification: `false`
- documentAuthoringApproved: `true`
- finalUserDirectionAndPublicationApproved: `true`
- productImplementationApproved: `false`
- phaseBDocumentAlignmentApproved: `false`

Fable 5가 최초 preview Markdown 전문을 작성하고 runner가 stdout과 byte-identical한 파일을 기록했다. 이후 사용자는 Governance merge 뒤 새 전문 redraft와 별도 merge를 승인했다. Change 004의 one-time approval로 Fable 5가 전문 전체를 다시 작성했으며 runner가 contract·privacy guard와 byte equality를 통과한 결과만 target에 교체했다. Codex는 최초 원문과 redraft 원문을 모두 편집하지 않았고, redraft 뒤 내용·제품 방향 review를 별도 파일에 작성했다.

## 2. 실제 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Interview | `tasks/user-flow-001-interview.md` | 사용자 요약 확인 완료 |
| Planning | `tasks/user-flow-001-planning.md` | 승인 완료 |
| Codex 기술 대조 review | `tasks/user-flow-001-review.md` | 내용 review의 보조 자료로 재분류 |
| Fable preview·redraft 원문 | `docs/13-user-flow-baseline.md` | Change 004 redraft 원문 / 개인 참고 자료 / Codex 수정 0 |
| GPT-5.6 SOL 내용 review | `tasks/user-flow-001-preview-review.md` | `CONTENT_REVIEW_COMPLETE` |
| Change 001 | `tasks/user-flow-001-change-001.md` | 과거 원문 작성·질문 무편집 계약과 실행 이력 |
| Change 002 | `tasks/user-flow-001-change-002.md` | 단일 초안·단일 내용 review·개인 참고 목적 확정 |
| Change 003 | `tasks/user-flow-001-change-003.md` | 문서 작성 승인과 제품 구현 미승인을 분리하고 대표 폴더 이식 경계 확정 |
| Change 004 | `tasks/user-flow-001-change-004.md` | Fable redraft exact target·내용 방향·별도 게시 승인 |
| Fable redraft 내용 review | `tasks/user-flow-001-preview-review-002.md` | `CONTENT_REVIEW_COMPLETE` / Open P0/P1/P2/P3 `0/0/0/0` |
| Roadmap | `docs/00-product-roadmap.md` | 완료 / PR #55 merge / 다음 Gate TASK-007A로 갱신 |
| User validation | Change 004와 사용자 실행 지시 | redraft 방향·별도 merge 승인 완료 / 제품 구현·Phase B 미승인 |

## 3. 과거 실행 이력과 최신 절차

1. Fable 5가 첫 preview 전문을 작성했다.
2. GPT-5.6 SOL review에서 기존 문서 정렬 Phase B gate와 현재 route·stage·회귀·복구 계약의 보정 항목을 분리했다.
3. Fable 5가 review를 읽고 전문 전체를 재작성했다.
4. 두 번째 review에서 출력 프롤로그, 구매 예정일 에스컬레이션의 현재/예정 구분, 삭제 보관함 조회와 관리자 복구 권한 구분을 확인했다.
5. Runner에 첫 비공백 행 H1 contract를 추가한 뒤 Fable 5가 전문 전체를 다시 작성했다.
6. 세 번째 GPT-5.6 SOL review에서 신규 P0/P1/P2가 없고 기존 P2-001·P2-003~008과 P3-001이 해소됐음을 확인했다.

각 반복에서 Codex는 `docs/13-user-flow-baseline.md`를 수정하지 않았다. 이 반복은 Change 002 이전에 이미 수행된 이력이며 향후 기본 절차가 아니다.
Review 저장 후 Codex는 privacy-safe 규칙에 맞지 않는 로컬 절대 링크 2개만 Repository 상대 링크로 정규화했다. Review의 판정·Finding·검증 결과와 Fable 원문은 변경하지 않았다.

최신 기본 절차는 `Fable primary draft 1회 → Codex 내용·제품 방향 review 1회 → 종료`다. 이번 redraft는 사용자의 명시 요청을 Change 004에 기록한 예외적 1회 실행이며 자동 revise가 아니다. Approval identity·digest별 private receipt가 소비되어 같은 승인을 재사용할 수 없다.

과거 interview·planning·technical review의 `implementationApproved: true`는 Phase A 문서 작성 실행 승인 이력이다. Change 003 당시에는 Fable redraft·Phase B·외부 게시가 미승인이었고, 이후 Change 004가 개인 참고 문서 redraft와 별도 Git 게시만 승인했다. 제품 기능 구현과 Phase B 정렬은 계속 `false`다.

## 4. 구현 결정

- Fable 질문은 round artifact 원문을 그대로 사용자에게 전달한다. Codex는 순서·표현·선택지·권장안을 바꾸지 않는다.
- Contract·privacy guard는 저장을 거부할 수 있지만 내용을 교정하지 않는다.
- Preview `draft`는 atomic exclusive create로 기존 target이나 symlink를 덮어쓰지 않는다.
- Preview `revise`는 사용자의 명시적 redraft 승인 marker와 기존 target·review가 있을 때만 Fable이 완전한 대체 전문을 출력한다. Approval change identity·digest는 one-time private receipt로 소비해 같은 승인을 재사용하지 않는다.
- `docs/02-business-flow.md`와 `docs/04-permission-matrix.md`는 개인 참고 목적에서는 변경하지 않는다. 향후 canonical 게시를 별도로 선택할 때만 재검토한다.
- Frontend·Backend·API·DB·migration·runtime·provider는 변경하지 않는다.
- USER-FLOW 산출물은 대표 폴더의 기존 Task branch로 이식하고 별도 임시 worktree는 일반 방식으로 제거한다. Fable Repository 정책과 runner의 최종 변경은 거버넌스 브랜치가 단일 소유한다.

## 5. 검증

- Fable 최종 원문의 첫 비공백 행과 단일 H1 확인
- 승인 결정 `1A/2C/3A`, 18개 stage, 13개 journey, 공통 예외 `E1~E4` 확인
- Mermaid 3개 블록의 정의·참조·중복·괄호 균형 정적 검사
- 상대 Markdown link 9개 대상 존재 확인
- Frontend route, Backend workflow·handoff·fallback·permission 계약 정적 대조
- privacy-safe email·UUID·절대 경로·credential 패턴 0 확인
- Frontend·Backend·database·infrastructure 제품 source diff 0 확인
- 실제 Markdown/Mermaid renderer: `NOT_RUN` — 현재 환경에서 renderer를 확인하지 못함
- Fable 원문 SHA-256: 보존 커밋과 대표 폴더 이식본 일치 여부를 기계적으로 비교하며 digest 원문은 privacy-safe 검증 로그에만 사용
- USER-FLOW 최종 diff의 제품 source·중복 Fable 정책·runner 포함 수 `0`
- Change 004 redraft runner: `READY`, stderr bytes `0`, artifact written `true`, stdout bytes `45618`, `REFRESHED_AFTER_DRIFT`
- Redraft 원문 H1·metadata·Mermaid 3개와 Fable stdout/target byte equality contract 통과
- Redraft 내용 review: 개인 참고 지위, source 우선순위, Phase B·전수 갱신 제거, Pending 후보화, 병렬 dependency map, vertical slice·성공 신호, 권고/미확정 경계 확인
- 독립 검증 1차: Roadmap 23절의 일반 서문이 USER-FLOW planning·review 존재와 문서 게시 승인까지 `false`로 읽히는 P2 `USER_FLOW_ROADMAP_PLANNING_PREAMBLE_STALE`를 확인
- P2 최소 보정: USER-FLOW의 개인 참고 문서 승인·제품 구현 미승인과 나머지 후속 신규 기능의 planning·implementation 미승인을 서문에서 분리
- 독립 재검증: `USER_FLOW_ROADMAP_PLANNING_PREAMBLE_STALE` `RESOLVED`, Open P0/P1/P2/P3 `0/0/0/0`, publication `GO`, mutation `0`
- Ready PR #55: Frontend·Backend·Full-Stack E2E CI `3/3` 성공, squash merge 완료
- Merge SHA: `d10efa13e061540a0e84c046054b55c81f04f449`
- Fable private Task session cleanup: session `6`, transcript `6` 제거, missing `0`

## 6. Finding

- Open P0/P1: `0/0`
- Open P2: `0`
- Open P3: `0`
- Content review status: `CONTENT_REVIEW_COMPLETE`
- Resolved P2 `USER_FLOW_DOC_STATUS_CONFLICT`: canonical·온보딩·Phase B·전수 갱신 선언을 제거하고 개인 참고 자료·source 우선순위를 명시했다.
- Resolved P2 `USER_FLOW_PENDING_LOCATION_PREMATURE`: Pending 위치를 확정에서 `TASK-007A` 후보로 전환했다.
- Resolved P2 `USER_FLOW_PARALLEL_UNIT_GAP`: 업무 단위 dependency map과 단위별 차단·완료 관계를 추가했다.
- Resolved P2 `USER_FLOW_ROADMAP_PLANNING_PREAMBLE_STALE`: Roadmap 23절 서문에서 USER-FLOW의 tracked planning·review·문서 승인과 나머지 후속 기능의 미승인 상태를 분리했다.
- Resolved P2 `USER_FLOW_PUBLICATION_STATE_STALE`: PR #55 merge 뒤 Roadmap·report의 실행 대기 상태를 완료와 TASK-007A Next Gate로 동기화했다.
- Resolved P3 `USER_FLOW_CLOSURE_GATE_REPORT_ORDER`: 최신 main closure branch를 먼저 만든 직후 파일·runtime 변경 전에 instruction chain과 identity gate를 재수행하고 범위를 2개 상태 문서로 고정했다.
- Task publish gate: `COMPLETE`
- Approval interpretation: `DOCUMENT_AUTHORING_ONLY_PRODUCT_IMPLEMENTATION_NOT_APPROVED`

## 7. 미실행·남은 승인

- Markdown/Mermaid 정적 구조 검증. 별도 시각 renderer 검수는 문서 내용·링크·fence 검증으로 대체하며 미실행 사실을 유지한다.
- 향후 canonical 게시를 선택할 경우에만 별도 문서 정렬 판단
- Branch 삭제는 미승인

## 8. Rollback

Phase A는 문서와 runner 계약만 변경한다. 게시 전에는 작업 branch의 변경 파일을 보존하고 승인된 경로만 후속 수정한다. 게시 뒤 rollback이 필요하면 제품 runtime이나 DB를 건드리지 않고 해당 문서 commit을 Git revert한다.

## 9. 종료 산출물 추적

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | Redraft·내용 review·독립 재검증·CI·PR #55 merge·private session cleanup 완료 |
| SOP | N/A | 제품·runtime 운영 절차를 만들지 않은 개인 기획 문서 Task라 적용 대상 아님 |
| User manual | N/A | 현재 산출물은 사용자 교육용 게시본이 아니라 개인 개발 판단용 미게시 참고 자료 |
| Roadmap update | `docs/00-product-roadmap.md` 23~25장 | 문서 작성 승인과 제품 구현 미승인을 분리해 추적 |
| User validation checklist | `tasks/user-flow-001-change-004.md`와 사용자 실행 지시 | Redraft 방향·게시 승인 완료 / 제품 구현·Phase B 미승인 |

## 10. 해결한 업무 문제

앞으로 무엇을 추가 개발할지 한눈에 판단하기 어려웠던 문제를 역할별 흐름, 구현 상태, 후속 Task와 Now/Next/Later 내용 review로 정리했다.

## 11. 기술적 결정과 검토한 대안

Fable 원문을 Codex가 고치지 않고 개인 참고 자료로 보존했다. Roadmap과 중복되는 canonical 제품 계약으로 즉시 승격하는 대안은 1인 개발의 유지 비용 때문에 보류했다.

## 12. 시행착오 및 폐기한 접근

Fable 초안과 Codex review를 여러 round 반복하는 방식, Codex가 Fable 원문을 보정하는 방식, 개인 참고 사용 전에 기존 문서 전체를 정렬하는 방식은 최신 Change 002·003에서 기본 절차가 아닌 것으로 확정했다.

## 13. 사용자 검수 결과와 남은 항목

- Interview 요약: 사용자 확인 완료
- Fable 전문 작성: 완료
- Codex 내용·제품 방향 review: 완료
- Fable redraft 방향·별도 게시·merge: 사용자 승인 완료
- Redraft 전문 작성·Codex 내용 review: 완료
- 독립 재검증: 완료 / Open P0/P1/P2/P3 `0/0/0/0` / publication `GO`
- CI·merge: Frontend·Backend·Full-Stack E2E `3/3` 성공 / PR #55 squash merge 완료
- 제품 구현·Phase B: 미승인
