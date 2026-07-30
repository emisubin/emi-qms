# TASK-ATTACHMENT-001 구현 보고

상태: `IMPLEMENTED / AUTOMATED_VALIDATION_COMPLETE / USER_VALIDATION_COMPLETE`

사용자 검수: 2026-07-30 사용자 명시로 완료.

## 해결한 업무 문제

1. Pending 조치 담당자가 조치 내용을 사진으로 남길 수 없었다.
2. 품질 재검사 담당자가 최초 부적합 근거·사진과 조치 내용·사진을 한 화면에서 비교할 수 없었다.
3. 확정 전 사진과 공식 조치 근거의 구분, 재시도 중복 방지와 확정 근거 보존 규칙이 없었다.

## 구현 결과

1. Pending이 `조치 중`일 때 현재 조치 담당자만 JPEG·PNG 사진을 선택적으로 추가·삭제할 수 있다.
2. 사진 설명은 필수이며 장당 5MB, 회차당 5장·15MB, Pending 누적 25장으로 제한한다.
3. 사진은 조치 완료 전 `Draft`이고 현재 담당자에게만 보인다.
4. `조치 완료 → 재검사 요청` 전환과 같은 transaction에서 Draft 사진과 조치 사유를 회차별 공식 근거로 확정한다.
5. 확정 사진과 사유 snapshot은 DB trigger로 수정·삭제를 차단한다.
6. 같은 요청 재시도는 operation receipt로 같은 결과를 반환하고, 같은 사진의 별도 중복 등록은 SHA-256으로 차단한다.
7. Pending 상세에는 확정 조치 회차를 시간순으로 표시한다.
8. IQC·LQC·OQC 재검사 화면은 다음 순서로 표시한다.
   - 최초 부적합 근거와 당시 사진
   - 최신 조치 내용과 확정 사진
   - 재검사 판정 항목
9. 사진 한 장을 불러오지 못해도 판정 전체를 막지 않고 해당 자리에 실패를 명시한다.
10. 사진 없는 기존 Pending과 사진 없는 조치 완료도 그대로 지원한다.

## 데이터·보안

- migration `0066_pending_action_photos.sql`
  - `pending_action_photos`: bytea, MIME, 크기, SHA-256, alt text, Draft/Confirmed, 조치 회차, 사유 snapshot, 등록·확정 사용자와 시각.
  - `pending_photo_operations`: 사진 mutation의 append-only 멱등 receipt.
  - 확정 근거와 receipt의 update/delete를 막는 DB trigger.
- content API는 인증, `Pending.Read`, 프로젝트 접근 scope를 모두 확인한다.
- Draft metadata와 binary는 현재 조치 담당자에게만 반환한다.
- 응답에는 binary나 내부 저장 경로를 넣지 않고 권한 검사를 거치는 content endpoint만 사용한다.
- 기존 IQC·패널 품질 확정 사진 table과 PDF는 수정하지 않고 읽기 전용으로 참조한다.

## Fable·Claude 5시간 사용량

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 측정 실패 — 값 추정 안 함 | 측정 실패 | 측정 실패 |
| 1차 planning 직후 | 0% 사용 / 100% 잔여 / 15:09 KST 초기화 | 12% 사용 / 88% 잔여 | 22% 사용 / 78% 잔여 |
| 2차 planning 직전 | 0% 사용 / 100% 잔여 / 15:09 KST 초기화 | 12% 사용 / 88% 잔여 | 22% 사용 / 78% 잔여 |
| 2차 planning 직후 | 17% 사용 / 83% 잔여 / 15:09 KST 초기화 | 12% 사용 / 88% 잔여 | 22% 사용 / 78% 잔여 |

세부 초기화 시각과 측정 실패 원문은 `tasks/attachment-001-change-001.md`에 보존했다.

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `ATT-001-F01` | P1 | Resolved | Pending 조치의 실물 근거가 시스템 밖에 남아 재검사 대조가 불가능했다. | Pending 전용 bounded Draft→Confirmed 사진 lifecycle을 추가했다. |
| `ATT-001-F02` | P1 | Resolved | 재검사 화면이 최초 부적합과 조치 근거를 한 흐름으로 제공하지 않았다. | IQC·패널 품질 재검사 DTO와 화면에 두 근거 묶음을 연결했다. |
| `ATT-001-F03` | P1 | Resolved | 확정 전 사진이 공식 근거로 노출되거나 확정 뒤 변경될 위험이 있었다. | Draft 담당자 전용 + transaction 확정 + append-only trigger를 적용했다. |
| `ATT-001-F04` | P2 | Resolved | 재시도·동시 요청이 중복 사진과 version 충돌을 만들 수 있었다. | operation receipt, Pending row lock, version CAS, SHA-256 중복 차단을 결합했다. |

Open P0/P1/P2: `0/0/0`.

## 실행한 검증

- Backend Release build: 오류 0, 경고 0.
- Backend 격리 집중 회귀: migration·Draft 비공개·조치 완료 확정·재검사 근거 `3/3` 통과.
- Backend 전체 회귀: 별도 격리 PostgreSQL에서 462/462 통과.
- Frontend 전체 unit: 22 files, `143/143` 통과.
- Frontend typecheck: 통과.
- Frontend lint: error 0, 기존 `src/main.tsx` Fast Refresh warning 1.
- Frontend production build: 통과.
- migration `0065 → 0066`: 운영 DB를 변경하지 않고 읽기 전용 복제본 기반 임시 DB에 적용해 table·trigger를 확인한 뒤 임시 DB를 제거했다.
- `git diff --check`: 통과.

## 미실행 검증과 이유

- 실제 사용자 사진 업로드 수동 검수와 390px 화면 증빙: 자동 검증 시점에는 runtime source 불일치로 체크리스트에 남겼고, 2026-07-30 사용자가 별도 검수 환경에서 완료를 명시했다.
- 실제 악성코드 scanner·외부 object storage: 이번 계약의 명시적 제외이며 운영 전환 Task 대상이다.
- Persistent UAT·실제 provider: 승인 범위 밖이다.

## 사용자 확인 방법

1. 조치 담당자로 Pending을 `조치 중`으로 바꾼다.
2. 조치 내용 영역에서 JPEG 또는 PNG와 사진 설명을 등록한다.
3. 다른 사용자로 같은 Pending을 열었을 때 확정 전 사진이 보이지 않는지 확인한다.
4. 조치 담당자로 `조치 완료`를 누르고 사진이 확정 회차로 바뀌는지 확인한다.
5. 품질 담당자의 재검사 업무로 이동해 최초 부적합 근거·사진, 조치 내용·사진, 재검사 판정이 위에서 아래 순서로 보이는지 확인한다.
6. 재검사 불합격 뒤 새 조치 회차를 진행했을 때 이전 확정 회차가 보존되는지 확인한다.

## Rollback·forward-fix

- 코드 rollback은 Pending 사진 API·projection·화면 allowlist만 역변경한다.
- migration `0066`은 additive schema이므로 적용 뒤 물리 삭제하지 않고 구형 코드가 읽지 않는 상태로 남긴 뒤 forward-fix한다.
- 확정 사진과 조치 사유 snapshot은 감사 근거이므로 자동 삭제하지 않는다.

## 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| 1차 Fable planning | 작성됨 | `tasks/attachment-001-planning.md` |
| Codex review | 작성됨 | `tasks/attachment-001-review.md` |
| 2차 Fable planning | 작성됨 | `docs/45-pending-action-attachment-plan.md` |
| Implementation report | 작성됨 | 본 문서 |
| SOP·user manual | 본 보고서에 포함 | `사용자 확인 방법`, `Rollback·forward-fix` |
| Roadmap·실험 원장 | 갱신됨 | `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md` |
| 사용자 검수 체크리스트 | 작성됨 | `tasks/attachment-001-user-validation-checklist.md` |

## 변경·게시 경계

- 현재 experiment worktree에서만 구현·검증했다.
- Commit·push·PR·merge는 수행하지 않았다.
- 대표 Repository, `main`, Persistent UAT와 실제 provider는 변경하지 않았다.
