# TASK-008B 사급 자재 추적 구현 보고

## 상태

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- branch: `experiment/task-008b-customer-supplied-materials`
- implementation / automaticValidation: `완료`
- userValidation: `대기`
- commit: `완료 — local experiment commit`
- push / PR / merge: `미승인·미실행`
- main merge approval: `0/3`
- Persistent UAT / provider / 대표 repo 영향: `없음`

## Task 목적·기획 source

구매품목에 사급(고객 제공) 공급 유형과 제공 예정 기준을 추가하고, TASK-008A의 도착·IQC·입고 확정 원장을 재사용해 예정량, 누적 도착량, 입고 확정량, 미도착 잔량과 처리 대기량을 한 흐름으로 추적한다.

Authoritative implementation contract는 Fable 2차 기획 [docs/15-customer-supplied-materials-plan.md](../docs/15-customer-supplied-materials-plan.md)다. 1차 Fable 원문은 [008b-planning.md](008b-planning.md), Codex 내용 review는 [008b-review.md](008b-review.md)에 분리 보존했다.

## 포함·제외 범위

포함:

- `Purchased | CustomerSupplied` 공급 유형과 기존 주문 수량·단위 pair 재사용
- 구매 direct PATCH omitted-preserve, 신규 Purchased 기본값, 사급 pair·사유·audit 검증
- 공급 유형 변경 gate, 누적 도착 수량 floor, 도착 존재 시 단위 고정과 row-lock 경쟁 보호
- 사급 예정/도착/확정/미도착/처리대기 projection, 제공 지연, 공급 유형 filter
- 사급 잔량 0 전용 마감 gate, 기존 008A 상태 machine·권한·업무 재사용
- 구매 조회·수정, 자재 입고, IQC의 desktop·390px 적응형 UI

제외:

- 신규 권한·알림·외부 delivery, 고객 포털·ERP·SCM, 사급 전용 Excel 열
- 상세 IQC·사진·PDF, 키팅, 병목 자동 집계
- Persistent UAT migration·runtime handover, push·PR·merge, 대표 repo 변경

## 구현 결정과 영향

### DB·Backend

- additive `0031_customer_supplied_materials.sql`에 공급 유형 enum CHECK, 사급 수량·단위 조건부 CHECK와 active index를 추가했다. 기존 migration은 수정하지 않았다.
- 구매 저장은 필드 미전송 시 기존 supply/measurement를 보존하고 신규 품목만 Purchased를 기본값으로 사용한다. 기존 품목의 공급 유형·수량·단위 변경은 3~500자 사유와 old/new audit를 요구한다.
- Materials 조회는 `planned`, `arrived`, `confirmed`, `remaining`, `processing` 수량과 서버 기준 제공 지연을 derived한다. 잘못된 공급 유형 filter는 거부한다.
- 사급 도착 마감은 누적 도착량=예정량과 기존 008A 마감 조건을 모두 만족해야 한다. 일반 구매 마감 정책은 바꾸지 않았다.
- 공급 기준 update와 도착 등록은 같은 품목 row-lock 순서를 사용한다. 기존 Materials/IQC 권한과 work item·Pending 계약은 확대하지 않았다.

### Frontend·적응형 UX

- 구매 조회에는 `사급 · 고객 제공`과 제공 예정 수량·단위를 표시하고, 구매 편집에는 공급 방식·수량·단위와 수정 사유를 추가했다.
- 자재 화면에는 All/일반 구매/사급 filter, 고객 제공 책임, 제공 지연과 다섯 수량 projection을 표시했다. IQC 카드에는 사급 badge를 추가했다.
- 390px에서는 desktop 표를 축소하지 않고 구매 카드, 세로 입력, 자재 카드·요약 rail, IQC 카드와 하단 메뉴로 재배치했다.
- `supplier_name`은 참고 업체로 보존하고 고객 제공 책임 label과 분리했다.

### Excel·기존 회귀

- Excel preview/apply는 기존 품목의 supply/measurement를 보존하고 신규 Excel 품목만 Purchased로 생성한다. 사급 값을 묵시적으로 변경하는 열은 추가하지 않았다.
- 기존 Purchased 품목, 008A 분할 도착·IQC·Pending·완료 projection과 기존 프로젝트·구매 화면을 회귀검증했다.

## 실제 변경 파일과 역할

- DB: `database/migrations/0031_customer_supplied_materials.sql`
- Backend: Procurement contracts/domain/store, Materials contracts/endpoints/store
- Frontend: `App.tsx`, `MaterialsWorkspace.tsx`, `api.ts`, `materials.ts`, `projects.ts`, `styles.css`
- Tests: migration/API/unit, `customer-supplied-materials.full-stack.spec.ts`
- 기획·검토: interview, Fable 1차 planning, Codex review, Change 001, Fable 2차 planning
- 증빙: `tasks/008b-screenshots/*.png`, 이 보고서와 user validation checklist

## 실행한 자동 테스트와 결과

- Backend Release build: `PASS`, warning 0 / error 0
- Backend 신규 targeted API: `3/3 PASS`; 공급 기준/도착 동시성: `1/1 PASS`
- Backend 전체: `372/372 PASS`
- Frontend lint: `PASS`(error 0, 기존 `main.tsx` Fast Refresh warning 1)
- Frontend typecheck: `PASS`
- Frontend unit: `76/76 PASS`
- Frontend production build: `PASS`(기존 대형 chunk warning)
- TASK-008B targeted isolated Full-Stack E2E: `1/1 PASS`
- 전체 isolated Full-Stack E2E: `24/24 PASS`, 전용 PostgreSQL DB·container cleanup 완료
- Browser visual QA: 구매 조회·수정, 자재, IQC의 desktop·390px 8장 확인; 모바일 horizontal overflow 0

미실행:

- Persistent UAT migration·runtime·실사용자 검증: 승인 범위 밖
- 실제 Teams/Mail/Activity 발송: 신규 발송 기능 없음, provider disabled
- CI·GitHub PR: push·PR 미승인
- 사용자 직접 action 검수: screenshot handoff 후 대기

## 개인정보·secret 검토

- screenshot과 E2E는 합성 프로젝트·업체·역할 계정만 사용했다.
- Persistent UAT, 실제 고객·사용자·알림 원문은 읽거나 기록하지 않았다.
- tracked diff에 credential, token, private key, tenant/client/object ID를 추가하지 않았다.

## Finding gate

| ID | Severity | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `008B-SHORT-CLOSE` | P1 | `RESOLVED` | 예정량 미달 마감 시 사급 잔량과 완료 모순 | 사급 누적 도착량=예정량 gate 추가 |
| `008B-DELAY-SEMANTICS` | P1 | `RESOLVED` | 완료 boolean은 고객 미제공과 내부 처리 대기를 혼동 | 서버가 미도착 잔량 기준으로 derived |
| `008B-READ-AUTH-MISMATCH` | P1 | `RESOLVED` | 기획 조회 범위와 기존 endpoint 권한 불일치 | 기존 policy 유지, 구매 read projection만 additive 확장 |
| `008B-REQUEST-PRESERVATION` | P1 | `RESOLVED` | nullable bulk field가 008A 측정값을 지울 위험 | omitted-preserve와 Excel 보존 적용 |
| `008B-DB-CONDITIONAL-PAIR` | P2 | `RESOLVED` | Backend 우회 시 사급 null pair 가능 | 명시적 non-null 조건을 포함한 DB CHECK 추가 |
| `008B-QUANTITY-SEMANTICS` | P2 | `RESOLVED` | 도착·확정·잔량 의미가 모호 | 다섯 derived projection으로 분리 |
| `008B-AUDIT-REASON` | P2 | `RESOLVED` | 공급 책임·약속 변경 설명 부족 | 사유 필수와 same-transaction old/new audit |
| `008B-SUPPLIER-DISPLAY` | P3 | `RESOLVED` | 고객 제공 책임과 참고 업체 혼동 | 책임 badge와 업체 참고값 분리 |

Open P0/P1/P2/P3: `0/0/0/0`.

## Fable 사용량

Claude `/usage` 정수 반올림 기준이다.

| 시점 | 전체 사용/잔여 | Fable 사용/잔여 |
| --- | --- | --- |
| 1차 기획 직전 | 14% / 86% | 27% / 73% |
| 1차 기획 직후 | 14% / 86% | 28% / 72% |
| 2차 기획 직전 | 14% / 86% | 28% / 72% |
| 2차 기획 직후 | 15% / 85% | 29% / 71% |

1차 기획은 471초, 2차 기획은 234초가 걸렸다.

## 운영 SOP — 실험 검수용

1. 이 branch를 isolated DB와 external provider disabled 상태에서 실행한다.
2. 구매 담당은 프로젝트 구매 수정에서 공급 방식을 사급으로 선택하고 제공 예정 수량·단위와 수정 사유를 저장한다.
3. 자재 담당은 자재 입고에서 고객 제공 책임·잔량을 확인하고 기존 008A 도착→IQC→확정 흐름을 수행한다.
4. 잔량이 있으면 마감하지 않는다. 고객 합의로 총량이 줄었다면 구매 담당이 사유와 함께 예정량을 누적 도착량 이상으로 정정한다.
5. 전량 도착·모든 유효 건 처리 뒤 자재 담당이 입고 마감을 실행한다.
6. 충돌 시 최신 목록을 다시 불러온다. Persistent DB 적용은 별도 backup·restore rehearsal과 승인을 거친다.

## User manual — 역할별 사용법

- 구매 담당: 프로젝트 `구매` → `구매정보 수정` → 공급 방식 `사급 · 고객 제공` → 제공 예정 수량·단위 → 수정 사유 → 저장.
- 자재 담당: `자재` → `사급` filter → 품목 카드에서 미도착 잔량·처리 대기량 확인 → 기존 도착·IQC·확정 action 수행.
- 품질 담당: `IQC` → 사급 badge 카드 확인 → 기존 합격·부적합 판정 수행.
- 모바일: 하단 메뉴로 페이지를 이동하며 구매는 카드, 자재는 수량 우선 카드, IQC는 한 건 카드로 확인한다.

## Rollback·forward-fix

- local code는 이 experiment commit의 후속 commit으로 보정할 수 있으며 main에는 반영되지 않는다.
- Persistent DB에 `0031`을 적용한 뒤 destructive down rollback은 하지 않는다. 문제 시 write를 중단하고 backup 기반 isolated 복구를 검증한 뒤 additive forward-fix migration을 작성한다.
- supply/measurement와 receipt 원장의 불일치는 직접 값을 덮지 않고 audit·receipt 원장을 기준으로 보정한다.

## 5종 종료 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 작성 완료 |
| SOP | 이 문서 `운영 SOP — 실험 검수용` | 실험 검수용 완료, 운영 handover 미승인 |
| User manual | 이 문서 `User manual — 역할별 사용법` | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md` TASK-008B section | 실험 구현·검수 대기 기록, canonical queue 불변 |
| User validation checklist | [008b-user-validation-checklist.md](008b-user-validation-checklist.md) | 자동 검증 완료·사용자 검수 대기 |

## 남은 항목

- 사용자 screenshot·실제 action 검수
- push·PR·merge, Persistent UAT와 실제 provider는 미승인·미실행
- canonical Roadmap 다음 Gate는 계속 `TASK-007A` Fable deep-interview
