# TASK-QUALITY-OPERATING-MODEL-001 — 구매품 구분별 IQC 운영 방식·검사 양식 기획안 (Change 005)

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/quality-operating-model-001-change-005-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- authoringModel: `FABLE_5`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 전역 IQC 양식 한 개와 구매품 구분의 `IQC 필요/없음` 이분 설정만으로는 구분별로 다른 검사를 운영할 수 없고, 구분 수가 늘면 양식 관리에서 원하는 구분을 찾기 어렵다.
- 대상 사용자·역할: 품질 domain 부서 양식 관리자 지정을 받은 활성 부서장(설정·양식 변경), 시스템 관리자(쓰기 허용 유지, 비관여는 운영 관행), 구매 담당(구분 선택·snapshot), 품질 IQC 담당(연결된 양식으로 검사), 자재·조회 사용자(기존 계약 유지).
- 정상 흐름: 양식 관리에서 구분 검색·선택 → 구분별 검사 스위치·방식(스캔형/상세형)·상세 항목 관리 → 구매품 저장 시 검사 여부·방식 snapshot → 도착 등록 transaction에서 snapshot에 맞는 IQC 생성 → 기존 스캔형/상세형 엔진으로 검사·판정.
- 예외·복구 흐름: 상세형은 항목 1개 이상 저장 후에만 스위치를 켤 수 있고 활성 상태에서 항목 0개 저장을 차단한다. stale 저장은 409로 차단한다. 확정 검사는 불변이며 부적합은 기존 Pending·재검사 새 회차를 사용한다. 유효 snapshot이 없으면 임의 전역 양식 fallback을 하지 않는다.
- 확정한 정책과 명시적 제외: interview 7절의 10개 결정(방식 자유 선택, 초기 상태 현행 유지, 비소급 snapshot, 항목≥1 규칙, 회차 시작 시점 항목 고정, 지정 부서장+관리자 권한, `IQC 필요` 토글 우회 제거, 기존 엔진·legacy `MATERIAL_IQC` 보존, append-only 감사·optimistic concurrency, 기존 Graphite UX 재사용). 제외: LQC 재작업(Change 004), 3~5번 작업, OQC·전진검수·FAT 변경, 협력사 Excel·OCR·scanner 연동, 구분 catalog 전면 재설계, 기존 확정 기록 수정/삭제, commit·push·PR·merge, Persistent DB·Azure runtime·실제 provider.
- planning으로 넘긴 비차단 미결정 사항: interview는 deferred 항목이 없다고 확정했다. 다만 방식 선택 UI의 정확한 문구·배치와 우회 경로 제거의 구체 형태는 화면·구현 설계 사항으로 이 planning의 16절 사용자 결정 항목에 권장안과 함께 제시한다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

품질 양식 관리자가 양식 관리에서 구매품 구분을 빠르게 찾아 구분별 검사 스위치·검사 방식·상세 검사 항목을 관리하면, 이후 저장되는 구매품의 도착분이 그 설정에 맞는 스캔형 또는 상세형 IQC로 자동 연결된다.

## 2. 배경과 해결할 업무 문제

- 현재 실제 IQC 대상은 외함·판금류·부스바·명판이며, 공식 수입검사서는 외함 스캔형(협력사 종이 검사서+품질 확인·서명 스캔본)만 운영된다.
- 현재 코드 계약(재검증 완료): `CategoryBased` 프로젝트는 구매품 저장 시 구분 id·code·표시명·`requires_iqc`를 snapshot하고, 도착 등록 transaction에서 `requires_iqc=true`이면 구분과 무관하게 항상 `ScanBased` attempt를 만든다. `requires_iqc=false`는 `InspectionNotRequired`로 종료된다. 전역 `MATERIAL_IQC` 상세 양식은 legacy `AllReceipts` 프로젝트에서만 사용된다.
- 따라서 판금류·부스바·명판의 `IQC 필요`만 켜면 구분별 상세검사가 아니라 외함용 스캔 검사로 잘못 연결된다. 구분별로 다른 검사 항목을 정의할 방법이 없다.
- 우회 방식은 전역 양식에 모든 항목을 합치거나 외부 문서로 보완하는 것뿐이며, 구분이 늘수록 잘못된 항목 검사·불필요한 반복 입력·기준과 성적서의 분리가 커진다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 지정 품질 부서 양식 관리자(활성 부서장) | 구분 검색·선택, 검사 스위치·방식·상세 항목 편집 | 품질 양식 관리 | 구분별 IQC 설정·현재 양식 |
| System Administrator | 전체 조회·변경(쓰기 허용 유지) | 전체 | 전체. 비관여는 운영 관행 |
| 일반 품질부서 사용자 | 구분 catalog 이름·순서 등 기존 관리 | 기존 범위 | 검사 스위치·방식·항목은 변경 불가(16절 결정 1) |
| 구매 담당 | 구매품 구분 선택·저장 | 담당 프로젝트 | 구분·검사 방식 snapshot(자동) |
| 품질 IQC 담당 | 도착분에 연결된 방식으로 검사·판정 | 담당 프로젝트 IQC | 검사 초안·확정(확정 후 불변) |
| 자재·조회 사용자 | 검사 대상 여부·진행·결과 조회 | 기존 project scope | 없음 |

권한 근거: 기존 `form_template_manager_bindings`의 domain별 지정(활성 사용자·부서 일치 검증 포함)을 그대로 재사용한다. Change 004의 LQC 운영 스위치는 시스템 관리자 전용이지만, 이번 IQC 검사 스위치·방식·항목은 사용자 확정(`1a`+`2b`)에 따라 **지정 품질 부서장 + 시스템 관리자**다. 두 규칙을 혼동하지 않는다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 판금류를 상세형으로 전환

1. 지정 품질 부서장이 양식 관리에서 `구매품 구분별 IQC`에 진입해 검색으로 판금류를 선택한다.
2. 검사 방식에서 `상세형`을 선택하고 검사 항목을 1개 이상 작성·저장한다. 항목이 없으면 스위치를 켤 수 없다는 안내가 표시된다.
3. 항목 저장 후 검사 스위치를 켠다. 화면은 "이미 저장된 구매품에는 적용되지 않고 이후 저장되는 구매품부터 적용"됨을 안내한다.

### 시나리오 B — 상세형 구분의 구매·도착·검사

1. 구매 담당이 판금류 구분으로 구매품을 저장하면 검사 여부·방식(상세형)이 구매품에 snapshot된다.
2. 자재 담당이 도착분을 저장하면 같은 transaction에서 상세형 IQC attempt·품질 내 업무·알림이 생성된다.
3. 품질 IQC 담당이 검사 회차를 시작하면 그 시점의 판금류 현재 양식 version이 report에 고정되고, 기존 상세 IQC 엔진(항목·사진·판정·PDF)으로 검사·확정한다. 부적합은 기존 Pending·재검사 새 회차를 따른다.

### 시나리오 C — 비소급과 외함 보존 확인

1. 설정 변경 전에 저장된 구매품의 도착분은 저장 당시 snapshot대로 검사되거나 검사 없이 입고 확정된다.
2. 외함은 기존 스캔형 검사가 그대로 유지되고, legacy `AllReceipts` 프로젝트의 상세검사는 전역 `MATERIAL_IQC` 양식을 계속 사용한다.
3. 화면은 신규 구분별 양식과 legacy 전역 양식의 적용 대상을 혼동하지 않게 구분해 안내한다.

## 5. 기능 요구사항

### 필수

- [ ] 양식 관리에 구매품 구분 검색·필터·선택 UX 추가(구분 수 증가 대비)
- [ ] 구분별 검사 스위치와 방식(스캔형/상세형) 관리, 지정 품질 부서장+시스템 관리자 권한 강제
- [ ] 구분별 상세 검사 항목(현재 양식) 편집 — 기존 현재 양식 editor·항목 규칙 재사용
- [ ] 상세형 스위치 on 조건: 항목 ≥1 저장. 활성 상태 항목 0개 저장 차단. 스캔형은 항목 없이 유효
- [ ] 구매품 저장(수정 저장 포함) 시 검사 여부·방식 snapshot 고정, 비소급
- [ ] 도착 등록 transaction에서 snapshot 방식대로 `검사 없음`/`ScanBased`/`Detailed` 분기, 임의 fallback 금지
- [ ] 상세형 report 시작 시점에 해당 구분 현재 양식 version 고정, 확정 후 불변, 회차별 version 표시
- [ ] 기존 구분 관리 화면 `IQC 필요` 토글 우회 경로 제거(16절 결정 1)
- [ ] 설정·양식 변경 append-only 감사(변경자·시각·이전/이후 값)와 optimistic concurrency
- [ ] 초기 데이터: 외함=스캔형 켜짐, 판금류·부스바·명판·기타=검사 없음(현행 동작 보존)

### 선택

- [ ] 구분 목록에 검사 상태 badge(검사 없음/스캔형/상세형·항목 수) 표시로 탐색 보조

### 명시적 제외

- [ ] LQC 운영 상태·Item별 LQC 양식 재작업(Change 004 소유)
- [ ] OQC·전진검수·FAT 변경, 협력사 Excel 검사서·OCR·scanner 연동
- [ ] 구매품 구분 catalog 전면 재설계, 기존 확정 IQC·PDF·첨부·Pending·재검사 이력 수정/삭제
- [ ] 범용 3-mode builder 등 새 검사 엔진, commit·push·PR·merge, Persistent DB·Azure runtime·실제 provider

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 양식 관리 catalog | 관리 > 양식 관리 | 기존 카드에 `구매품 구분별 IQC` 카드 추가(품질 domain), legacy `자재 수입검사`는 적용 대상(기존 방식 프로젝트 전용) 안내 문구 보강 | 카드 선택 | 기존 catalog 규칙 |
| 구분별 IQC workspace | catalog 카드 | 구분 검색 입력+선택기(Item별 LQC의 selector 패턴), 선택 구분의 검사 스위치·방식·항목 수·현재 양식 | 검색·선택, 스위치·방식 변경, 항목 편집(기존 `수정→저장/취소`) | 저장 성공·실패를 action 근처에 표시, 비소급 안내 고정 노출, stale 409 시 새로고침 안내 |
| 구매품 구분 관리 | 기존 위치 | 이름·순서·사용 상태 등 기존 정보 + 검사 상태는 조회 표시(16절 결정 1) | 기존 catalog 관리 | 기존 규칙 |
| 구매·자재·IQC 화면 | 기존 동선 | 도착분의 검사 방식·회차별 양식 version 표시 | 기존 검사·판정 행동 | 기존 규칙 |

확인할 UX 항목: 현재 구분의 검사 상태를 한눈에 이해할 수 있는가, 스위치를 켤 수 없는 이유(항목 0개)가 즉시 보이는가, 권한 없는 사용자에게 조회 전용이 명확한가, 검색→선택→설정→항목 편집이 390px에서 한 열로 유지되는가, page-level overflow 0.

디자인은 기존 Graphite 양식 관리의 catalog·editor·badge·feedback·간격을 재사용하고 새 디자인을 만들지 않는다.

## 7. 업무 규칙과 불변조건

- Backend가 권한·검사 분기·snapshot의 authoritative layer다. Frontend 숨김은 권한이 아니다.
- 검사 여부·방식은 구매품 저장 시점 snapshot으로 고정하고 이후 설정 변경을 소급하지 않는다. 도착 이력이 생긴 구매품의 구분 변경 차단 계약을 유지한다.
- 상세 항목 내용은 검사 회차 시작 시점의 해당 구분 현재 양식 version으로 고정하고 확정 후 불변이다. 재검사는 기존 계약대로 새 회차를 만든다.
- 상세형 활성 구분은 항상 항목 ≥1을 보장한다(빈 양식 검사 구조적 불가). 유효 snapshot·양식이 없으면 오류로 차단하고 전역 양식 fallback을 하지 않는다.
- 사용된 구분·양식·연결·감사 기록은 hard delete하지 않는다. 기존 확정 IQC·스캔 증빙·PDF·Pending·재검사 이력을 수정하지 않는다.
- 외함 스캔형과 legacy `AllReceipts`+전역 `MATERIAL_IQC` 상세검사 계약을 보존한다.
- 검사 스위치·방식·항목 쓰기는 지정 품질 부서장+시스템 관리자만 가능하며, 이 값에 대한 다른 쓰기 경로를 남기지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 구매품 구분 catalog | code·표시명·순서·사용 상태·`requires_iqc` | 기존 | 기존 append-only 감사 유지 |
| 구분별 IQC 설정 | 구분별 검사 스위치·방식(ScanBased/Detailed)·version | 신규 | append-only 감사, optimistic concurrency, hard delete 금지 |
| 구분별 상세 현재 양식 | 구분과 1:1로 연결된 상세 검사 항목 집합 | 신규(기존 IQC template version·item 구조 재사용) | 사용된 version 불변 보존 |
| 구매품 검사 snapshot | 기존 구분 snapshot + 검사 방식 snapshot | 기존 확장 | 저장 시점 고정, 비소급 |
| IQC attempt/report | `Legacy/Detailed/ScanBased` 회차·report·PDF·스캔 증빙 | 기존 | 확정 후 불변, 회차별 template version 표시 |
| legacy 전역 양식 | `MATERIAL_IQC` 현재 양식 | 기존 | `AllReceipts` 전용으로 보존 |

```text
[구분 설정] 검사 없음 → (상세형: 항목≥1 저장 후) 검사 켜짐(스캔형|상세형) → 검사 끔  ※ 모두 비소급
[구매품]   저장 시 방식 snapshot 고정 → 도착 등록
[도착분]   snapshot 없음/검사 없음 → InspectionNotRequired → 입고 확정
           snapshot 스캔형   → ScanBased attempt → 스캔본 등록·판정
           snapshot 상세형   → Detailed attempt → 회차 시작 시 구분 현재 양식 고정 → 검사·판정
[부적합]   기존 Pending 차단 → 조치 완료 → 새 재검사 회차
```

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 지정 부서장+관리자 쓰기 권한, 상세형 항목≥1 규칙, snapshot 고정·비소급, 분기 결정, fallback 금지.
- 필요한 조회와 mutation: 구분별 IQC 설정 목록 조회(검색 대상 데이터 포함), 설정(스위치·방식) 변경, 구분별 현재 양식 조회·항목 저장. 구매품 저장·도착 등록·IQC 생성은 기존 mutation을 확장한다.
- 권한·validation: 기존 domain 지정 조회(`form_template_manager_bindings` 재사용)로 쓰기 검증. 항목 규칙은 기존 현재 양식 editor validation을 재사용. 안정적인 한글 오류 메시지.
- transaction·동시성·idempotency: 기존 패턴(Serializable transaction, `row_version` 기반 optimistic concurrency, 도착 등록과 IQC 생성 단일 transaction, attempt 중복 방지)을 재사용한다.
- audit trail: 설정 변경은 신규 append-only 감사, 양식 항목 변경은 기존 양식 감사 패턴을 재사용한다.
- 외부 provider 영향: 없음. 기존 인앱 내 업무·알림 연결만 재사용한다.

세부 테이블·컬럼·SQL 형태는 구현 조사에서 확정하되, 조사로 확인된 재사용 지점은 12절 권장안에 기록한다.

## 10. Frontend 고려사항

- route/component: 기존 양식 관리 page 안에 카드·workspace 추가. Item별 LQC selector·editor와 생산계획·실적 연결 workspace의 "대상 선택 → 현재 양식 관리" 패턴을 재사용한다.
- loading/empty/error/success: 기존 양식 관리 규칙을 따르고 저장 중 중복 제출을 차단한다.
- 공통 Action Feedback: 기존 계약(진행·성공·실패 메시지를 action 근처에) 재사용.
- 접근성: 첫 오류 focus, `aria-live`, 스위치·선택기 label, 키보드 탐색 유지.
- 390px/mobile/narrow pane: 검색→구분 선택→설정→항목 편집 한 열 배치, page-level overflow 0 검증.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 도착 등록 시 기존 품질 내 업무·알림 생성 계약을 방식과 무관하게 재사용한다. 상세형 도착분도 스캔형과 같은 인계 흐름을 따른다.
- 권한/관리자: 품질 domain 부서 양식 관리자 지정(기존 관리 화면)을 그대로 사용하며 새 권한 개념을 만들지 않는다.
- Excel/PDF/첨부: 구매 Excel의 구분 선택 계약 유지. 상세형은 기존 항목 사진·시스템 PDF, 스캔형은 기존 스캔 첨부 계약을 그대로 사용한다.
- Teams/Mail: 변경 없음. 실제 provider 발송은 범위 밖이다.
- 삭제·복구/감사: hard delete 금지, append-only 감사, 확정 증빙 불변을 유지한다.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | Change 004의 Item별 설정 패턴을 구분에 적용: 신규 구분별 IQC 설정(스위치·방식·version·감사) + 구분별 상세 양식은 기존 IQC template/version/item 구조에 구분 연결을 추가해 재사용. 구매품 snapshot에 방식 컬럼을 추가하고 도착 분기가 snapshot 방식을 읽음 | 검사 엔진·양식 editor·회차 고정·감사·동시성 전부 재사용, 기존 데이터 비소급이 구조적으로 보장, 권한 분리가 명확 | 신규 테이블·snapshot 컬럼 추가 migration 필요 |
| B | `material_categories`에 검사 방식 컬럼을 직접 추가하고 전역 `MATERIAL_IQC` 양식을 구분별로 분기 | 테이블 수 최소 | catalog 관리(품질부서 전원)와 검사 설정(지정 부서장) 권한이 한 테이블에 섞여 우회 위험, legacy 전역 양식과 의미 충돌 |
| C | 구분별 검사 정의를 위한 범용 3-mode builder 신규 엔진 | 장기 유연성 | 사용자 요구에서 확인되지 않은 과도 확장으로 interview에서 배제됨 |

권장안 A의 추가 근거(코드 재검증): 도착 등록 transaction의 분기 지점이 단일하고, 상세 report는 이미 생성 시점에 active template version을 고정하며 활성 양식이 없으면 fallback 없이 한글 오류로 차단하는 계약이 존재한다. A는 이 계약을 구분별 양식으로 확장만 하면 된다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. 격리 DB·합성 데이터만 사용한다.
- migration 필요 여부: 필요(additive). 현재 branch의 미커밋 Change 004가 `0070`을 사용 중이므로 이번 기능은 `0071` 이후 번호를 사용하되, Change 004의 commit·검수 상태를 구현 시작 시점에 재확인해 번호 충돌을 피한다.
- 외부 발송/실제 데이터 영향: 없음. 실제 provider 호출 금지.
- runtime 교체 여부: 없음. Azure runtime·Front Door·public traffic은 별도 Task다.
- 추가 사용자 승인 필요 작업: 이 planning 승인과 구현 승인, 이후 commit·push·PR·merge 각각. Change 004(사용자 검수 대기)와 같은 branch를 공유하므로 구현 착수 순서는 Codex가 사용자와 확인한다.

## 14. 검증 계획

- 최소 테스트: migration fresh/existing(초기 데이터: 외함 스캔형 켜짐·나머지 검사 없음 보존), 권한(지정 부서장 성공·일반 품질 사용자 403·관리자 성공), 상세형 항목≥1 규칙, stale 409, snapshot 비소급(설정 변경 전후 저장 구매품 분리), 도착 분기 3종(없음/스캔형/상세형), 회차 시작 시점 양식 고정과 재검사 회차.
- 영향 영역 회귀: Backend 전체 테스트(Release), Frontend unit·lint·typecheck·production build, 격리 Full-Stack에서 양식 관리→구매→도착→IQC 동선, legacy `AllReceipts` 상세검사와 외함 스캔형 회귀.
- PR/CI: 게시 승인 전 Draft 유지, allowlist 개별 stage.
- 사용자 검수: desktop·390px에서 구분 검색·설정·항목 편집, 상세형 구분의 실제 도착분 검사, 비소급 동작, legacy 안내 문구를 직접 확인한다.

## 15. 완료 기준

- 기능/권한/데이터: 5절 필수 항목 전부 충족, 7절 불변조건 위반 0.
- UX: 6절 화면이 기존 디자인 체계 안에서 동작하고 안내·피드백·접근성 항목 통과.
- 자동 테스트: Backend·Frontend 전체 통과, migration fresh/existing 통과, 열린 P0/P1/P2 0.
- 5종 산출물: canonical 종료 정책에 따라 위치·상태 추적.
- 사용자 검수 상태: 자동 검증과 분리해 별도 기록.
- PR 상태: 사용자 승인 전 commit·push·PR·merge 없음.

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 기존 구분 관리 화면의 `IQC 필요` 토글 우회 제거 방식 | (a) 구분 관리에서는 검사 상태를 조회 전용으로 표시하고 변경은 양식 관리에서만 수행 — 권장(변경 지점 단일화) / (b) 토글을 유지하되 지정 부서장+관리자 권한으로 통일 | 대기 |
| 2 | 신규 구분 생성 시 초기 검사 상태 | (a) 항상 `검사 없음`으로 생성하고 이후 양식 관리에서 설정 — 권장(정책 2·비소급과 일관) / (b) 생성 화면에서 검사 여부 선택을 유지하되 새 권한 규칙 적용 | 대기 |
| 3 | 비활성 구분의 양식 관리 표시 | (a) `비활성` badge와 함께 표시하고 설정 편집 허용(활성화 전 준비 가능) — 권장 / (b) 활성 구분만 표시 | 대기 |

세 항목 모두 비차단이며 구현 범위를 바꾸지 않는다. 결정 전 구현 착수 시 권장안을 기본값으로 두지 않고 사용자 확인을 먼저 받는다.

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `Admin`의 양식·구분 store/contracts/endpoints, `Procurement` 저장 snapshot, `Materials` 도착 분기·상세 report 양식 선택, 관련 조회 응답의 방식·version 표시.
- Frontend: 양식 관리 page·양식/구분 관련 type·API client·품질 검사 화면의 표시 보강.
- DB/Migration: 구분별 IQC 설정·감사·구분별 양식 연결·구매품 방식 snapshot의 additive migration 1건(`0071` 예정, 13절 참조).
- Tests/Scripts: Backend migration·권한·snapshot·분기·재검사 테스트, Frontend 양식 관리·품질 검사 테스트.
- Docs: 본 planning·review·change·implementation report·검수 checklist, Roadmap 추적 항목 동기화.

## 18. Roadmap 연결

- 선행 Task: TASK-009A(상세 IQC 엔진), TASK-ADMIN-002(양식 관리·부서 양식 관리자), 본 Task 기존 scope(migration `0067` 구분 routing·스캔형), Change 004(같은 branch의 Item별 LQC — 검수·commit 순서 확인 필요).
- 후속 Task: 5개 승인 작업 중 3번(LSE TASK NO), 4번(부서 Pending), 5번(설계 도번·필수값·패널 묶음). 고객용 요약 성적서는 별도 후속.
- 현재 Go/No-Go: Roadmap 기대 Task는 `TASK-AZURE-DEPLOY-001`(Front Door 외부 대기)이며, 사용자의 명시적 순서 변경 승인으로 본 Change를 진행한다(`explicitRoadmapOverrideApproved: true`).
- 별도 Task로 분리할 항목: 판금류·부스바·명판의 실제 검사 항목 내용 확정(품질팀 운영 작업), 협력사 검사서 연동류.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-08-05 | Round 2~4 답변과 확인용 요약 `승인` | interview `COMPLETED_CONFIRMED`, 본 planning 작성 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

planning·review resolution과 16절 결정이 승인된 뒤, 새 Codex 구현 세션은 다음 순서로 진행한다.

1. instruction chain·Roadmap·본 planning·review를 다시 읽고 Change 004의 commit·검수 상태와 migration 번호를 재확인한다.
2. additive migration: 구분별 IQC 설정(스위치·방식·version)·append-only 감사·구분별 상세 양식 연결·구매품 방식 snapshot 컬럼·초기 데이터(외함 스캔형 켜짐, 나머지 검사 없음)를 작성하고 fresh/existing을 검증한다.
3. Backend: 지정 부서장+관리자 권한의 설정·양식 API(항목≥1 규칙, optimistic concurrency, 감사), 구매품 저장 snapshot 확장, 도착 등록 분기(없음/스캔형/상세형, fallback 금지), 상세 report의 구분 양식 version 고정을 구현한다. 결정 1에 따라 기존 `IQC 필요` 변경 경로를 정리한다.
4. Frontend: 양식 관리에 구분 검색·선택 workspace와 스위치·방식·항목 editor를 기존 패턴으로 추가하고 legacy 전역 양식 안내를 보강한다.
5. 14절 검증 계약 전부와 desktop·390px 확인을 수행하고, implementation report·검수 checklist·Roadmap 동기화를 남긴다. commit·push·PR·merge와 Persistent DB·runtime·provider는 각각 별도 사용자 승인 없이는 수행하지 않는다.
6. 7절 불변조건을 깨야만 구현이 가능해지는 경우 즉시 중단하고 보고한다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 3
