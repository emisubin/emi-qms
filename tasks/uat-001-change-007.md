# TASK-UAT-001 Change 007 — Pending 상세 Runtime 계약 복구

## 1. Task Identity Gate

- proposedTaskId: `TASK-UAT-001 Change 007`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `COST_GATE / PRE_TRAFFIC_GATE`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UAT-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

## 2. Purpose identity

- 업무 목표: 공식 HTTPS 5174에서 Pending 프로젝트·상세를 다시 열고, Frontend와 Backend가 잠시 다른 계약으로 실행돼도 상세 전체가 빈 화면이 되지 않게 한다.
- Root Finding:
  - `UAT-PENDING-007-A` P1: Backend 5081 프로세스가 현재 Frontend보다 오래돼 Pending 상세의 `actionEvidence`를 반환하지 않았다.
  - `UAT-PENDING-007-B` P1: Frontend가 누락된 `actionEvidence`를 즉시 읽어 상세 전체를 빈 화면으로 만들었다.
  - `UAT-PENDING-007-C` P2: `/pending/` 경로가 Pending이 아니라 Home으로 해석됐다.
- 변경 경계: Pending 경로 정규화, 혼합 버전 응답의 Frontend fail-safe, 집중 회귀, 승인된 현재 source로 HTTPS 5174·Backend 5081 runtime handover와 실제 로그인 browser 재검증을 포함한다.
- 보존할 불변조건: Pending 권한·상태 전이·API·DB schema/data, Persistent UAT container·volume, Entra 설정 원문, 실제 알림 provider와 다른 runtime·dirty worktree를 변경하지 않는다.
- 예상 산출물: `/pending/` dashboard, 증거 응답 누락 상세의 부분 격리, 현재 Backend 계약 상세, frontend 전체 검증과 5174 실제 smoke 증거.

## 3. 사용자 승인과 게시 경계

- 사용자는 2026-08-02에 로그인된 5174에서 Pending 상세가 열리지 않는 문제를 재확인한 뒤 해결을 명시했다.
- 이는 위 BUGFIX와 안전한 5174/5081 runtime handover를 승인한다.
- migration·seed·data reset, 실제 provider 발송, commit·push·PR·merge·배포는 포함하지 않는다.
- Azure 비용·traffic Gate보다 현재 사용자 검수 P1 복구를 먼저 수행하는 명시적 순서 변경으로 기록한다.

## 4. 완료 기준

- `/pending/`가 `/pending`으로 정규화되고 프로젝트 dashboard를 연다.
- `actionEvidence`가 없는 혼합 버전 응답도 핵심 상세·이력·뒤로가기를 유지하며 증거 영역만 명시적 복구 안내를 표시한다.
- 현재 Backend 5081은 기존 UAT DB를 재생성·변경하지 않고 `/health/live`, `/health/ready`를 계속 통과한다.
- 로그인된 5174에서 실제 Pending 프로젝트와 상세가 열리고 browser console에 render error가 없다.
- Frontend lint, typecheck, 전체 unit, build와 390px 상세 smoke가 통과한다.

## 5. Rollback

- schema/data 변경이 없으므로 Frontend 세 파일과 집중 test를 이전 상태로 되돌릴 수 있다.
- runtime rollback은 이전 5174/5081 source SHA·시작 명령을 재적용하되, 이전 stale Backend 계약 자체는 정상 rollback 후보로 간주하지 않는다.

## 6. 구현·검증 결과

- `/pending/`와 Pending 상세의 뒤쪽 slash를 canonical path로 정규화했다.
- `actionEvidence`가 없는 구형 응답은 조치 사진 section만 복구 안내로 대체하고, 발생 내용·담당·기한·코멘트·이력은 계속 렌더링한다.
- 집중 회귀 2/2, Frontend typecheck, lint error 0·기존 warning 1, 전체 unit 172/172, build와 mock UI E2E 4/4를 통과했다.
- 첫 전체 unit과 build 병렬 실행에서 알림 설정 test 1건이 로딩 중 assertion으로 실패했다. 해당 test 단독 2/2와 자원 경쟁 없는 전체 재실행 172/172가 통과해 제품 결함으로 재현되지 않았다.
- 로그인된 HTTPS 5174에서 `/pending/ → /pending` 정규화, 3개 프로젝트 dashboard, 실제 1건의 목록·상세, 제목·발생 내용·담당·기한·이력 렌더링을 확인했다. 이전 `draftPhotos` render exception은 재발하지 않았다.
- 390px 계약은 mobile layout mock에서 누락 응답 상세가 `mobile-pending-detail-page`를 유지하는 집중 회귀로 확인했다.

## 7. Runtime·DB 결정과 Finding

- Backend 5081과 Persistent PostgreSQL은 재시작·migration·seed·data 변경 없이 보존했다.
- privacy-safe aggregate 점검에서 live migration ledger는 64개이고 `pending_action_photos` table은 없었다. 현재 source는 67개 migration을 포함하므로 새 Backend를 그대로 넘겨받으면 조치 사진 query가 schema와 어긋난다.
- 사용자 승인 범위에 migration이 없으므로 current-source Backend handover를 실행하지 않았다. 상세 복구는 Frontend 호환 경계로 완료하고, 조치 사진 활성화는 별도 controlled migration·runtime handover 승인을 기다린다.

| Finding | 심각도 | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `UAT-PENDING-007-A` | P1 | `RESOLVED` | 오래된 5081 응답에 `actionEvidence`가 없어 상세 render가 중단됨 | 혼합 버전 응답 fail-safe와 실제 5174 상세 smoke |
| `UAT-PENDING-007-B` | P1 | `RESOLVED` | 단일 optional section 오류가 상세 전체를 빈 화면으로 만듦 | 증거 section만 격리하고 핵심 상세·이력 보존 |
| `UAT-PENDING-007-C` | P2 | `RESOLVED` | `/pending/`를 Home으로 해석 | Pending namespace trailing slash 정규화와 회귀 |
| `UAT-PENDING-007-D` | P3 | `BACKLOG` | local UAT DB 64개와 source 67개 사이 drift로 조치 사진 기능을 아직 활성화할 수 없음 | `TASK-UAT-001` 후속 controlled migration·5081 handover에서 재검토. 현재는 명시적 unavailable 안내로 완화 |

Open Finding은 P0/P1/P2/P3 `0/0/0/1`이다.

## 8. 사용자 검수 checklist

- 상태: `사용자 검수 완료` — 2026-08-02
- [x] `https://localhost:5174/pending`에서 Pending이 있는 프로젝트를 연다.
- [x] `상세 보기`가 빈 화면 없이 제목·발생 내용·담당·기한·처리 이력을 표시하는지 확인한다.
- [x] 조치 사진 영역의 unavailable 안내가 상세의 다른 내용을 가리지 않는지 확인한다.
- [x] Pending List로 돌아가 같은 상세에 다시 진입되는지 확인한다.

## 9. 5종 종료 산출물

- Implementation report: [TASK-UAT-001 Implementation report 28장](uat-001-implementation-report.md#28-change-007--pending-상세-runtime-계약-복구) — 갱신
- SOP: [기존 TASK-UAT-001 SOP](uat-001-sop.md) — 적용 절차 변경 없음. 이번에는 migration gate 때문에 runtime handover를 실행하지 않아 독립 SOP 추가는 `N/A`
- User manual: 독립 manual 변경 `N/A` — 새 사용자 기능이 아니라 빈 화면 복구이며, 검수 동선은 이 문서 8장에 기록
- Roadmap update: [Product Roadmap TASK-UAT-001](../docs/00-product-roadmap.md#task-uat-001-https-development-uat-안정화) — 갱신
- User validation checklist: 이 문서 8장 — 사용자 검수 완료

## 10. 게시 상태

- code quality gate: `GO`
- runtime migration/handover gate: `NO_GO_UNAPPROVED` — 조치 사진 활성화에 필요한 schema 변경 승인이 없음
- 사용자 검수: 완료 — 2026-08-02
- 구현 commit: `db9cb34` 완료
- local `main` fast-forward merge: 완료 — 2026-08-02
- Push·PR·remote merge·배포: 미실행. 사용자의 local `main` 병합 승인을 원격 게시 승인으로 확대하지 않는다.
