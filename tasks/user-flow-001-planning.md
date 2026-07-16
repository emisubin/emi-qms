# TASK-USER-FLOW-001 — 웹사이트 전체 유저플로우 설계 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/user-flow-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: true
- implementationApproved: true

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 앞으로 어떤 기능을 추가 개발해야 하는지 명확하게 알 수 없다. 현재 우회 방식은 사용자 확정 문구 기준 “기준선은 Roadmap이었고, 개별 화면은 그때그때 기획해서 만들었다”이다.
- 대상 사용자·역할: 13개 여정 — 부서 7역할(영업·설계·생산관리·구매·자재·제조·물류), 품질 검사 단계별 4역할(IQC·LQC·OQC·전진검수/FAT), System Administrator, 승인 대기 사용자. 이 단위는 권한 role이 아니라 유저플로우 여정 단위이며, 기존 Backend 권한 계약을 새로 정의하지 않는다.
- 정상 흐름: 진입점 3유형(내 업무 카드, 알림 deep link, 메뉴 직접 진입)을 공통 패턴으로 하고, 역할별 여정을 18단계 표준 업무 프로세스의 담당 구간에 연결한다.
- 예외·복구 흐름: 공통 예외 4종(권한 없음·승인 대기 진입, 담당자 부재 fallback, Pending 차단, 알림·deep link 진입 실패)을 한 번 정의하고, 구현 구간은 실제 경로 기준 단계별 예외를 상세히, 미구현 구간은 Pending 연결과 다음 담당자 알림 수준까지만 기록한다.
- 확정한 정책과 명시적 제외: 목표 메뉴 골격(Home·내 업무·프로젝트·부서 업무·알림·관리자) 확정 + 구현·미구현 상태·도입 Task 병기, 미구현 메뉴는 해당 Task 승인·구현 전 Frontend 미노출. 승인 뒤 이 Task의 구현 단계에서 `docs/` canonical 유저플로우 문서를 생성하고 후속 Task가 화면·흐름을 바꾸면 함께 갱신한다. 제품 코드 구현, Figma 시각 디자인, API·DB·migration·runtime·provider 변경, Persistent UAT write·실제 외부 발송, 후속 Task 자동 승인, commit·push·PR·merge는 제외한다. 접근성·390px은 기존 동일 URL·overflow 0 원칙을 각 여정에 명시하고 narrow UX 상세·우선순위는 Roadmap 1.3 `TASK-MOBILE-001`에 위임한다(사용자 동의).
- planning으로 넘긴 비차단 미결정 사항: interview의 Deferred 비차단 결정은 없음. 이 planning에서 새로 제기하는 비차단 결정은 16절 3건이다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

이 Task가 완료되면 사용자는 13개 역할 여정과 목표 메뉴 골격이 정의된 canonical 유저플로우 기준선을 근거로, 다음에 어떤 기능을 어떤 순서로 추가 개발할지 명확하게 판단할 수 있다.

## 2. 배경과 해결할 업무 문제

- 현재 사용자는 Roadmap(18단계 프로세스, 실행 큐, 확정·미확정사항)을 기준선으로 삼고, 개별 화면은 각 Task 시작 시점에 그때그때 기획해 왔다.
- Repository에는 18단계 업무 프로세스(Roadmap 4절), 담당자 구조·fallback(5절), 알림 채널 matrix(6.5절), 구현 화면별 기준은 있으나, “한 역할이 로그인부터 자기 업무 완료·인수인계까지 웹사이트를 어떻게 통과하는가”를 한 곳에 고정한 canonical 문서가 없다. 초기 `docs/02-business-flow.md`는 부서 책임 수준의 개괄이며 현재 구현·18단계 세부와 상세도가 다르다.
- 이로 인해 추가 개발 대상 선정이 매번 Roadmap 항목·개별 화면 기준의 재대조에 의존하고, 신규 기능 planning마다 화면·내비게이션·진입 경로를 재논의하는 비용과 충돌 위험이 발생한다.
- 이 기준선이 없으면 추가 개발 대상을 명확히 정하지 못한 상태가 계속되고, 후속 NEW_FEATURE 큐(`TASK-007A` 이후)의 메뉴 위치·진입점 결정이 Task마다 흔들린다.
- 1차 용도는 후속 Task 기획 기준선과 사용자 온보딩·교육 겸용이며, 첫 사용자는 사용자 본인이다.

## 3. 대상 사용자와 권한

| 사용자/역할(여정 단위) | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 영업 | 프로젝트 생성(1)과 세금계산서·완료(18) 여정 | 기존 권한 계약 | 기존 권한 계약 |
| 설계 | 패널명·사이즈(3) 여정 | 기존 권한 계약 | 기존 권한 계약 |
| 생산관리 | 생산계획·담당자(2) 여정과 참조 흐름 | 기존 권한 계약 | 기존 권한 계약 |
| 구매 | 구매정보(4) 여정 | 기존 권한 계약 | 기존 권한 계약 |
| 자재 | 자재 도착(5)·입고 확정(7)·키팅(8) 여정 | 기존 권한 계약 | 기존 권한 계약 |
| 제조 | 제조 작업(9)·제조 완료(11) 여정 — 미구현 구간 | 기존 권한 계약 | 기존 권한 계약 |
| 물류 | 포장(15)·출발(16)·납품 완료(17) 여정 — 미구현 구간 | 기존 권한 계약 | 기존 권한 계약 |
| 품질 IQC | 수입검사(6) 여정 — 미구현 구간 | 기존 권한 계약 | 기존 권한 계약 |
| 품질 LQC | LQC(10) 여정 — 미구현 구간 | 기존 권한 계약 | 기존 권한 계약 |
| 품질 OQC | 자체검수(12) 여정 — 미구현 구간 | 기존 권한 계약 | 기존 권한 계약 |
| 품질 전진검수·FAT | 전진검수(13)·FAT(14) 여정 — 미구현 구간 | 기존 권한 계약 | 기존 권한 계약 |
| System Administrator | 관리자 여정(사용자·부서·휴일·이력·알림 운영) | 관리자 계약 | 관리자 계약. 업무 입력 무제한 우회 금지 |
| 승인 대기 사용자 | 로그인 → 승인 대기 안내 → 역할 부여 후 진입 | `/api/me`·본인 프로필·승인 대기 안내 수준(기존 계약) | 변경 불가 |

권한은 계속 서버 Policy가 authoritative하며, 이 문서와 canonical 유저플로우 문서는 권한을 새로 정의하거나 확대하지 않는다.

## 4. 핵심 사용자 시나리오

Canonical 문서는 13개 여정 전부를 다루며, planning에서는 상세도 계약을 보여주는 대표 시나리오 4종을 고정한다.

### 시나리오 A — 구현 구간 상세 flow 예시: 영업, 프로젝트 생성

1. 영업 담당자가 `프로젝트` 메뉴의 프로젝트 목록에서 생성 action으로 진입한다(고객사, Item, PJT Code, 면수, 납기일, FAT 필요 여부 등 실제 구현 필드 기준). `내 업무`는 새 프로젝트 생성 진입점이 아니다.
2. 시스템이 생산계획 skeleton을 자동 생성하고, 생산관리 담당자에게 다음 내 업무를, 참조 대상자에게 참조 알림을 생성한다.
3. 영업 담당자는 프로젝트 상세와 workflow 요약에서 진행 상태를 확인한다. Validation 실패·권한 없음 등 단계별 예외는 실제 구현 경로 기준으로 문서에 상세 기록한다.

### 시나리오 B — 미구현 구간 중간 상세도 예시: 품질 IQC, 수입검사

1. IQC 담당자가 도입 예정 검사 진입점 또는 내 업무 카드로 수입검사 업무에 진입한다.
2. 핵심 행동은 IQC 체크·값 입력·사진·적합/부적합 판정 수준까지만 기록하고, 검사 양식·필수 사진 위치 등 세부는 `TASK-009A` planning 승인 항목으로 위임 표기한다.
3. 적합 시 자재 담당자의 입고 확정 내 업무로 연결되고, 부적합 시 Pending 연결과 다음 담당자 알림 수준까지만 기록한다.

### 시나리오 C — 공통 진입 패턴: 알림 deep link

1. 사용자가 인앱 알림, Teams Activity(`/teams/activity`와 상세) 또는 메일 deep link로 특정 업무·알림 상세에 진입한다.
2. 시스템이 인증·권한을 확인하고 해당 화면으로 이동시키며, 실패 시 공통 예외 4종 중 “알림·deep link 진입 실패” 경로를 따른다.
3. 사용자는 처리 후 내 업무 상태 동기화를 확인한다. 구현된 deep link 진입 경로(`/my-work`, `/notifications`, `/teams/activity` 계열, `/admin` 계열)는 실제 경로 기준으로 문서화한다.

### 시나리오 D — 승인 대기 사용자

1. 신규 Entra 사용자가 최초 로그인하면 자동 생성되지만 active role 0개로 승인 대기 상태가 된다.
2. 승인 대기 화면 안내에 따라 대기하며, 업무 데이터 조회는 기존 계약 범위로 차단된다.
3. 관리자가 역할을 부여하면 해당 역할 여정의 진입점으로 합류한다. 승인·역할 부여 이력은 기존 정책대로 보존된다.

## 5. 기능 요구사항

### 필수

- [ ] 13개 역할 여정 각각에 진입점 3유형, 정상 흐름, 완료 시 다음 담당자 연결을 정의한다.
- [ ] 구현 구간(프로젝트~구매·자재 입고, 내 업무, 알림, 관리자, 로그인·승인 대기)은 실제 경로 기준 상세 flow로 기록한다.
- [ ] 미구현 구간(검사·제조·물류·정산·Pending List·Home)은 진입점·핵심 행동·다음 담당자 연결 수준의 중간 상세도로 기록하고 세부를 도입 Task에 위임 표기한다.
- [ ] 목표 메뉴 골격을 확정하고 각 메뉴에 구현·미구현 상태와 도입 Task를 병기한다. 미구현 메뉴 미노출 규칙을 명시한다.
- [ ] 공통 예외 4종을 한 번 정의하고 여정별 참조·상세를 이원화 coverage로 기록한다.
- [ ] 후속 화면·흐름 변경 Task가 canonical 문서를 동시 갱신하는 규칙을 정의한다.
- [ ] 역할별 여정 요약을 사용자 언어로 병기해 온보딩·교육 용도를 충족한다.
- [ ] Phase A preview 전문은 Fable 5가 직접 작성하고 GPT-5.6 SOL이 별도 review한다. Codex는 Fable 원문을 수정하지 않는다.

### 선택

- [ ] 여정별 Mermaid flow 다이어그램(문서 렌더링 범위 내).
- [ ] 도입 Task별 “이 문서에서 위임한 결정” 역색인.

### 명시적 제외

- [ ] Frontend·Backend 제품 코드 구현과 Figma 시각 디자인
- [ ] API·DB·migration·runtime configuration·provider 변경
- [ ] Persistent UAT write와 실제 외부 발송
- [ ] `TASK-007A`와 후속 기능의 planning·implementation 자동 승인
- [ ] commit·push·PR·merge와 worktree 정리

## 6. 화면·UX 기획

목표 메뉴 골격과 상태·도입 Task 병기(구현 상태는 현재 Repository 기준):

| 화면(메뉴) | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| Home — 미구현, `TASK-HOME-001` | 메뉴(도입 전 미노출) | widget-slot 요약 | 위치만 확정, 세부 위임 | 도입 Task 기준 |
| 내 업무 — 구현 | 메뉴, `/my-work` deep link | 목록·KPI·프로젝트별 그룹 | 카드에서 실제 입력 페이지 이동, 시작/완료 동기화 | 기존 공통 기준 보존 |
| 프로젝트 — 구현 | 메뉴 | 목록·상세·workflow 상태/진행률 | 생성·수정·상태 변경·복구 | 기존 공통 기준 보존 |
| 부서 업무: 생산관리·구매·자재 — 구현(자재는 권한 조건부) | 메뉴 | 각 dashboard·입력 화면 | 단계 업무 입력 | 기존 공통 기준 보존 |
| 부서 업무: 검사·제조·물류·정산 — 미구현, `TASK-009A/012A`·`011A`·`013A`·`014A` | 메뉴(도입 전 미노출) | 진입점·핵심 행동 수준 | 위치·연결만 확정, 세부 위임 | 도입 Task 기준 |
| Pending List — 미구현, `TASK-007A` | 공통 모듈 진입(위치 제안은 16절 결정 3) | 이슈 단위 목록 | 위치·연결만 확정, 세부 위임 | 도입 Task 기준 |
| 알림 — 구현 | 메뉴, `/notifications`·`/teams/activity` 계열 deep link | 전체/읽음/읽지 않음, 프로젝트별 그룹 | 읽음 처리, 상세·업무 이동 | 기존 공통 기준 보존 |
| 관리자 — 구현(관리자 권한 조건부) | 메뉴, `/admin` 계열 deep link | 시스템 관리 중심 dashboard | 사용자·부서·휴일·이력·알림 운영 | 기존 공통 기준 보존 |
| 승인 대기 — 구현 | 로그인 직후 자동 | 승인 대기 안내 | 대기·로그아웃 | 기존 계약 보존 |

확인할 UX 항목:

- 사용자가 현재 상태를 이해할 수 있는가 — 여정마다 “지금 어느 단계, 다음 담당자는 누구”를 명시한다.
- 다음 행동이 명확한가 — 진입점 3유형과 완료 시 인수인계 연결을 여정마다 고정한다.
- 저장·변경 결과가 action 근처에 보이는가 — 기존 공통 feedback 기준을 보존 참조한다.
- 권한 부족·검수 전용·오류 상태가 명확한가 — 공통 예외 4종으로 일원화한다.
- 좁은 화면에서도 핵심 행동이 가능한가 — 동일 URL·overflow 0 원칙을 각 여정에 명시하고 상세는 `TASK-MOBILE-001`에 위임한다.

## 7. 업무 규칙과 불변조건

- 기존 인증·권한·18단계 업무·내 업무·알림 계약과 Backend authoritative policy를 변경하지 않는다. 이 문서는 기술 기준선이 아니라 기획 기준선이다.
- 18단계 순서·완료 기준·다음 담당자 연결(Roadmap 4절)과 담당자 fallback 확정 순서(5절)를 임의 재해석 없이 재사용한다.
- 알림 채널 matrix와 provider/event coverage 상태(6.5절)를 그대로 인용하며 상태를 임의로 올리지 않는다.
- 미구현 메뉴·화면은 해당 기능 Task 승인·구현 전까지 Frontend에 노출하지 않는다.
- 미확정 정책(검사 양식, Pending 첨부, 포장 기준 등)은 이 문서에서 확정하지 않고 도입 Task planning 승인 항목으로 위임 표기한다.
- 후속 Task가 화면·흐름을 바꾸면 canonical 유저플로우 문서를 같은 Task에서 함께 갱신한다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 신규 데이터 개념 | 없음 — 이 Task 산출물은 문서다 | N/A | N/A |
| 18단계·내 업무·알림 상태 | 문서가 참조만 하는 기존 계약 | 기존 | 기존 정책 보존, 변경 없음 |
| canonical 유저플로우 문서 | 승인 뒤 구현 단계에서 `docs/` 아래 생성하는 산출물 | 신규 문서(데이터 아님) | Git history로 추적, 후속 Task 동시 갱신 |

```text
planning DRAFT → Codex review → 사용자 승인 → (구현 단계) docs/ canonical 문서 생성 → 후속 Task 동시 갱신
```

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 기존 그대로 보존. 이 Task는 API·mutation·권한·validation·transaction·audit·provider를 변경하지 않는다.
- 필요한 조회와 mutation: 없음. 문서 작성 시 실제 구현 대조는 read-only 조사로만 수행한다.
- 문서에 기록하는 endpoint·화면 경로는 실제 구현 확인분만 사용하고, 미구현 능력을 존재한다고 단정하지 않는다.

Repository 조사 전 내부 class명, column명과 SQL 형태를 확정하지 않는다.

## 10. Frontend 고려사항

- Route/component: 이번 Task에서 변경 없음. 현재 7종 메뉴와 deep link 진입 경로(`/my-work`, `/notifications`, `/teams/activity` 계열, `/admin` 계열)를 실제 기준으로 문서화한다.
- Loading/empty/error/success와 공통 Action Feedback: 기존 공통 기준을 보존 참조한다.
- 접근성·390px/mobile/narrow pane: 동일 URL·overflow 0 원칙을 여정별로 명시, 상세는 `TASK-MOBILE-001`에 위임한다.
- 미구현 메뉴 미노출 규칙은 후속 도입 Task가 구현 시점에 준수해야 할 계약으로 기록한다.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 18단계 완료 → 다음 내 업무 자동 생성 → 참조 알림 흐름을 여정 연결의 기본 축으로 사용한다.
- 권한/관리자: 서버 Policy 강제, System Administrator 업무 우회 금지, 승인 대기 계약을 여정 전제 조건으로 인용한다.
- Excel/PDF/첨부: 미확정 정책은 인용만 하고 확정하지 않는다.
- Teams/Mail: 채널별 역할(인앱 기록/Teams 개입/메일 요약·증빙)과 deep link 진입을 공통 진입 패턴으로 문서화한다.
- 삭제·복구/감사: 기존 정책 보존을 명시한다.
- 기존 문서: `docs/02-business-flow.md`·`docs/04-permission-matrix.md`와의 관계를 16절 결정 2로 확정한다.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A | 단일 canonical 문서 1개 — 공통 골격(메뉴·진입점·예외 4종) + 13개 여정 절 | 한 곳에서 전체 여정 파악, 온보딩 용이, 갱신 규칙 단순 | 문서가 커져 후속 diff가 한 파일에 집중 |
| B | 공통 골격 문서 1개 + 역할군별 부속 문서 분할 | 여정별 갱신 diff 분리 | 파일 수 증가, canonical 위치 분산으로 참조·갱신 규칙 복잡 |

권장안: A. 첫 사용자가 사용자 본인 1인이고 온보딩·기준선 겸용 목적상 단일 문서의 탐색성이 우선한다. 분량이 실제로 문제가 되면 후속 `DOCS_GOVERNANCE`에서 분할을 재검토한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음 — 문서 산출물만 생성한다.
- Migration 필요 여부: 없음.
- 외부 발송/실제 데이터 영향: 없음. Provider 호출·실제 발송 없음.
- Runtime 교체 여부: 없음. 5174·5176·5081 runtime을 건드리지 않는다.
- 추가 사용자 승인 필요 작업: Phase A preview 사용자 검수 뒤 Phase B 기존 문서 정렬 승인, commit·push·PR·merge 승인.
- 작성 책임: 승인된 Phase A preview는 Fable 5 원문 direct write, 사후 검토는 GPT-5.6 SOL 별도 review다. 수정은 Fable revise로만 수행한다.

## 14. 검증 계획

- 최소 테스트: 문서 Task이므로 코드 테스트 신규 실행 대상 없음(N/A — 제품 코드 무변경). 문서 내부 link·상대 경로 유효성과 privacy-safe 표기(실명·식별자·절대 경로 부재)를 검사한다.
- 영향 영역 회귀: 구현 구간 서술을 실제 code·화면 경로와 대조하는 Codex Repository 대조 review(`tasks/user-flow-001-review.md`).
- PR/CI: 게시 시 기존 CI 문서 검증 범위를 따르고, allowlist 경로만 stage한다(실행은 Codex·사용자 승인 범위).
- 사용자 검수: 13개 여정·메뉴 골격·예외 coverage·갱신 규칙에 대한 사용자 명시 승인. Planning 승인과 구현 결과 검수는 별도 gate다.

## 15. 완료 기준

- 기능/권한/데이터: 13개 여정, 메뉴 골격+상태·도입 Task 병기, 공통 예외 4종과 이원화 coverage, 후속 연결·갱신 규칙이 정의되고 기존 권한·데이터 계약 변경이 없다.
- UX: 진입점 3유형과 여정별 다음 담당자 연결이 빠짐없이 기록된다.
- 자동 테스트: 문서 link·privacy·allowlist 검증 통과.
- 5종 산출물: interview·planning·review·Roadmap update와 user validation checklist의 상태·위치를 추적한다.
- 사용자 검수 상태: planning 승인 → review resolution 승인 → 구현(문서 생성) 후 검수의 3단계를 각각 기록한다.
- PR 상태: 사용자 검수 대기 중에는 Draft로 유지한다.

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | canonical 유저플로우 문서의 파일명·번호 | A. `docs/13-user-flow-baseline.md`(기존 번호 체계 연장, 권장) / B. 번호 없는 `docs/user-flow.md` | A |
| 2 | 기존 `docs/02-business-flow.md`·`docs/04-permission-matrix.md`와의 관계 | A. 새 문서와 함께 즉시 정렬 / B. 별도 P2 Task / C. `docs/13` 미게시 preview를 먼저 검수하고, 승인된 경우 같은 Task에서 기존 두 문서를 정렬한 뒤에만 완료·게시 | C |
| 3 | Pending List·정산의 메뉴 위치 표기 | A. Pending List는 내 업무·프로젝트 하위 공통 진입, 정산은 영업(프로젝트) 하위로 골격에 위치만 표기(권장) / B. 위치 표기 없이 도입 Task에 전부 위임 | A |

세 건은 사용자 결정 완료다. 결정 2의 preview 단계에서는 기존 `docs/02`·`docs/04`를 변경하지 않는다. 사용자가 `docs/13`을 검수한 뒤 같은 Task에서 두 문서를 실제 구현과 정렬하며, P2가 해소되기 전에는 완료·commit·push·PR·merge하지 않는다.

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: 없음
- Frontend: 없음
- DB/Migration: 없음
- Tests/Scripts: 없음
- Docs: `tasks/user-flow-001-planning.md`, `tasks/user-flow-001-review.md`, `docs/00-product-roadmap.md` 추적 갱신, 승인 뒤 구현 단계의 `docs/` canonical 유저플로우 문서 1개

## 18. Roadmap 연결

- 선행 Task: 0.6 신규 기능 GO(완료)와 `TASK-007A`보다 먼저 수행하는 재정렬 승인(완료).
- 후속 Task: `TASK-007A`(1.1)부터의 실행 큐 전체가 이 문서의 메뉴 위치·진입점·위임 결정을 참조한다. 특히 `TASK-009A/011A/012A/013A/014A`, `TASK-HOME-001`, `TASK-MOBILE-001`.
- 현재 Go/No-Go: interview `COMPLETED_CONFIRMED`. planning·implementation 승인은 미완료.
- 별도 Task로 분리할 항목: narrow UX 상세(`TASK-MOBILE-001`), 검사 양식·Pending 첨부·포장 기준 등 미확정 정책(각 도입 Task), `docs/02` 대체 여부(결정 2에서 B 선택 시 별도 `DOCS_GOVERNANCE`).

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-15 | 결정 `1A`, `3A`; 기존 문서는 새 유저플로우 preview 검수 뒤 변경 | `docs/13-user-flow-baseline.md`, Pending·정산 위치를 확정하고 기존 `docs/02`·`docs/04`는 preview 승인 전 보존 |
| 2026-07-15 | Phase A preview 승인, Fable 5 전문 직접 작성 후 GPT-5.6 SOL review | Codex가 Fable 원문을 편집하지 않고 direct-write artifact와 별도 review를 분리. 질문도 Fable 원문 그대로 전달 |

## 20. 최종 승인 상태

- [x] 기능 목표와 업무 문제 승인
- [x] 포함·제외 범위 승인
- [x] 시나리오와 권한·업무 규칙 승인
- [x] UI/UX 방향 승인
- [x] Task 고유 안전 경계 승인
- [x] 검증·사용자 체크리스트 승인
- [x] Fable direct preview·GPT-5.6 SOL review 실행 승인

---

- planningStatus: `APPROVED_PHASE_A_PREVIEW`
- implementationApproved: true
- userDecisionRequiredCount: 0
