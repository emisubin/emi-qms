# TASK-007A — Pending List 공통 모듈 실험 기획안

> 상태: Experimental implementation baseline
> 작성자: Codex fallback — Fable 5 read-only planning이 328초 뒤 contract-invalid로 종료되어 산출물이 생성되지 않음
> 적용 범위: `experiment/task-007a-pending-list` worktree only

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/007a-interview.md`
- interviewWaiver: `EXPLICIT_USER_EXPERIMENT_DIRECTIVE`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApprovedForCanonicalMain: false
- experimentalImplementationApproved: true
- implementationApprovedForCanonicalMain: false

## 0. 확인된 기준선

- 문제: 프로젝트 후속 이슈가 부서별 메모로 흩어져 담당자·조치·재검사·종결 이력을 공통으로 추적할 수 없다.
- 사용자: 생산관리, 품질, 제조, 자재, 구매, 설계, 영업, 물류의 업무 사용자와 감사 조회 관리자.
- 정상 흐름: 등록 → 조치 요청 → 조치 중 → 재검사 요청 → 종결.
- 복구 흐름: 잘못된 상태 후퇴나 삭제 대신 코멘트와 상태 이력을 append-only로 보존한다.
- 확정 경계: 서버 권한 authoritative, stage 번호 불변, 인앱 알림 원본, 실제 provider·Persistent UAT 금지.
- 비차단 미결정: binary 첨부 저장·검역·보존·backup 정책. 이번 실험은 text-first로 보류한다.

## 1. 한 줄 목표

사용자가 프로젝트의 부적합·PUNCH·제조 중단·기타 이슈를 한곳에서 등록하고 담당자 조치부터 재검사·종결까지 추적할 수 있게 한다.

## 2. 해결할 업무 문제

현재 구현에는 프로젝트 workflow, 내 업무와 알림은 있지만 업무 흐름을 막는 예외 이슈의 공통 도메인이 없다. 이 때문에 같은 문제를 여러 화면이나 대화에서 중복 관리하고, 누가 언제 무엇을 해야 하는지와 종결 근거를 감사하기 어렵다. Pending을 후속 자재·검사·제조보다 먼저 공통 모듈로 도입해 이후 Task가 동일한 차단 계약을 사용하도록 한다.

## 3. 대상 사용자와 권한

| 역할 | 조회 | 변경 |
| --- | --- | --- |
| 일반 업무 역할 | 접근 가능한 프로젝트 Pending | 생성, 코멘트, 자신이 생성하거나 담당한 건의 허용 상태 전이 |
| 생산관리 | 전체 프로젝트 Pending | 생성, 담당 지정·재지정, 모든 정상 상태 전이 |
| 조치 담당자 | 자신에게 배정된 Pending | 조치 시작, 코멘트, 재검사 요청 |
| 생성자 | 자신이 등록한 Pending | 코멘트, 조치 요청, 재검사 확인·종결 |
| Read-only | 전체 조회 | 변경 불가 |
| System Administrator | 감사 조회 | 업무 mutation 우회 불가 |

권한은 `Pending.Read`와 `Pending.Manage`로 분리한다. Read-only와 System Administrator에는 조회만, 나머지 업무 역할에는 조회·변경을 부여한다. 개별 mutation은 permission 외에 생성자·담당자·생산관리 역할을 다시 검사한다.

## 4. 핵심 시나리오

### A. 긴급 제조 중단 등록과 담당 배정

1. 사용자가 프로젝트, 유형 `제조 중단`, 긴급도, 제목·설명, 담당자와 기한을 입력한다.
2. 서버가 Pending과 최초 이력을 한 transaction에 저장한다.
3. 담당자가 있으면 `내 업무`와 인앱 알림을 중복 없이 생성한다.
4. 목록은 긴급 건을 먼저 보여주고 담당자·기한·현재 상태를 표시한다.

### B. 조치와 재검사·종결

1. 담당자가 조치를 시작하고 코멘트에 처리 내용을 남긴다.
2. 담당자가 재검사를 요청하면 생성자 또는 생산관리가 결과를 확인한다.
3. 확인자는 종결 사유를 입력해 종료한다.
4. 상세 화면에서 상태·담당 변경과 코멘트를 시간순으로 확인한다.

### C. 오류와 동시성

1. stale version으로 상태 변경을 시도하면 서버가 409를 반환한다.
2. UI는 최신 상세를 다시 불러오도록 안내한다.
3. 잘못된 상태 전이와 권한 없는 mutation은 각각 409/403으로 차단한다.

## 5. 기능 요구사항

### 필수

- [x] Pending 전체 목록, 상태·유형·긴급도·담당 필터와 집계
- [x] 프로젝트 기반 생성, 담당자·기한 지정
- [x] 상태 전이와 optimistic version 검사
- [x] append-only 코멘트와 상태·담당 audit 이력
- [x] 담당자 배정 시 내 업무·인앱 알림 연결
- [x] desktop과 390px 반응형 UI, loading·empty·error·success 안내
- [x] read/manage 및 actor 범위의 서버 권한 검사

### 선택

- [ ] 프로젝트 상세의 contextual Pending 탭 — 이번 slice에서는 목록의 프로젝트 deep link로 대체
- [ ] overdue/평균 체류시간 분석 — 후속 병목 집계 Task에서 구현

### 명시적 제외

- [ ] binary 첨부 upload/download — storage·악성파일 검역·보존 정책 blocker
- [ ] 실제 Teams/Mail/Activity 발송
- [ ] 상세 검사 체크리스트와 판정 양식
- [ ] 유형 관리자 편집, 삭제·복구, bulk action
- [ ] Persistent UAT migration과 runtime handover

## 6. 화면·UX

| 화면 | 경로 | 핵심 정보 | 행동 |
| --- | --- | --- | --- |
| Pending workspace | `/pending` | 집계, 필터, 이슈 카드 | 생성, 필터, 상세 열기 |
| Pending 생성 dialog | `/pending` | 프로젝트·유형·긴급도·담당·기한·내용 | 등록 또는 취소 |
| Pending 상세 | `/pending/{id}` | 현재 상태, 설명, 담당, 코멘트, audit timeline | 조치 시작·재검사 요청·종결·코멘트 |

화면은 상태 다음 행동을 가장 가까운 위치에 표시한다. 좁은 화면에서는 목록과 상세를 한 열로 접고 모든 action은 keyboard focus와 label을 유지한다. 첨부 영역은 비활성 입력처럼 보이지 않게 “정책 확정 후 제공” 안내 카드로 표시한다.

## 7. 업무 규칙과 불변조건

- 상태는 `Registered → ActionRequested → InProgress → ReinspectionRequested → Closed`의 forward-only 전이만 허용한다.
- 생성 시 담당자가 있으면 `ActionRequested`, 없으면 `Registered`다.
- `ActionRequested` 이후에는 담당자가 반드시 존재한다.
- Closed는 재개하거나 삭제하지 않는다. 정정은 코멘트와 후속 Pending으로 남긴다.
- 담당 변경, 상태 변경, 생성은 audit history에 남긴다.
- 댓글은 수정·삭제하지 않는다.
- 프로젝트 workflow stage는 변경하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 감사 |
| --- | --- | --- |
| `pending_issues` | 프로젝트 연결, 유형·우선순위·상태·담당·기한·version | 현재 snapshot |
| `pending_comments` | 처리·재검사·종결 설명 | append-only |
| `pending_history` | 생성·상태·담당 변경 | append-only |
| 기존 `work_items` | 담당자의 실행 항목 | Pending별 idempotency key |
| 기존 `notifications` | 인앱 알림 원본 | Pending event별 idempotency key |

```text
Registered → ActionRequested → InProgress → ReinspectionRequested → Closed
```

Migration은 `0029_pending_list_foundation.sql` 하나의 additive forward migration으로 추가한다. 기존 migration은 수정하지 않는다.

## 9. API·Backend

- `GET /api/pending` — 집계와 필터 목록
- `POST /api/pending` — 생성
- `GET /api/pending/{id}` — 상세·코멘트·이력
- `POST /api/pending/{id}/transition` — forward-only 상태 전이
- `POST /api/pending/{id}/comments` — append-only 코멘트
- `GET /api/pending/assignees` — 활성 업무 사용자 선택 목록

생성·배정과 work item/notification은 transaction으로 묶는다. version을 비교해 경쟁 mutation을 차단하고 idempotency key로 동일 event의 업무·알림 중복을 막는다.

## 10. Frontend

- 기존 수동 router에 Pending list/detail view를 추가한다.
- API type과 호출은 기존 `projects.ts`/`api.ts` convention을 따른다.
- 전용 `PendingPage.tsx`로 화면 복잡도를 App shell에서 분리한다.
- 성공 action 뒤 목록/상세와 shell badge를 다시 불러온다.
- API 오류는 한국어 다음 행동과 함께 action 근처에 표시한다.

## 11. 기존 기능 연결

- 프로젝트: 프로젝트 ID/코드/제목을 snapshot이 아니라 join으로 표시한다.
- 내 업무: `target_type='Pending'`, link `/pending/{id}`로 생성한다.
- 알림: 인앱 notification과 recipient만 생성하며 delivery/provider queue는 만들지 않는다.
- 첨부: blocker 해소 전 schema와 UI mutation을 만들지 않는다.
- 관리자: 감사 조회만 허용하고 업무 mutation 권한은 주지 않는다.

## 12. 대안

| 후보 | 장점 | 단점 | 판정 |
| --- | --- | --- | --- |
| 전용 workspace + deep link | 전체 관리와 현장 맥락 모두 지원 | route 추가 | 채택 |
| 프로젝트 탭만 | 구현 작음 | 전체 병목 추적 불가 | 보류 |
| 첨부까지 즉시 구현 | 기능 completeness | 보안·보존 정책 추정 | 제거 |
| 모든 역할 unrestricted mutation | 단순 | 감사·책임 경계 파손 | 제거 |

## 13. 실험 안전 경계

- Persistent UAT, 실제 provider, 기존 runtime을 변경하지 않는다.
- 대표 repo와 GitHub main에는 commit·push·PR·merge하지 않는다.
- 개발·E2E는 isolated DB와 synthetic 데이터만 사용한다.
- 원본 반영 전에는 정식 interview·planning 승인 또는 이 실험 결과에 대한 별도 채택 승인이 필요하다.

## 14. 검증 계획

- Backend Release build, Pending endpoint validation/authorization/transition test
- migration catalog, fresh DB와 existing isolated DB apply
- Frontend lint, typecheck, unit, build
- isolated full-stack E2E: 생성→시작→코멘트→재검사→종결
- desktop와 390px browser smoke, page-level overflow 0

## 15. 완료 기준

- 기능: 생성·목록·상세·담당·상태·코멘트·audit·내 업무·인앱 알림이 연결된다.
- 품질: P0/P1/P2 open 0, 영향 테스트 통과.
- UX: desktop/390px 주요 화면 screenshot을 synthetic 환경에서 확보한다.
- 산출물: planning, review, implementation report 안의 SOP·manual·validation checklist 위치를 추적한다.
- 게시: 사용자 승인 전 commit/push/PR/merge 0.

## 16. Roadmap 연결

- 선행: `TASK-USER-FLOW-001` 완료.
- 현재: `TASK-007A` 실험 구현.
- 후속: `TASK-007B` 병목 현황, `TASK-MOBILE-001`, `TASK-HOME-001`.
- 별도 blocker: attachment security/storage baseline.

## 17. 실행 기록

| 일자 | 결정 | 근거 |
| --- | --- | --- |
| 2026-07-16 | interview 생략, 기획→코딩 연속 진행 | 사용자 실험 전용 명시 지시 |
| 2026-07-16 | Fable 산출물 없이 Codex fallback | Fable read-only planning 328초 후 contract-invalid, artifact 미생성 |
| 2026-07-16 | text-first attachment 보류 | Roadmap external blocker와 privacy/security 불변조건 |
