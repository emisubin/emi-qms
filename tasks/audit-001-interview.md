# TASK-AUDIT-001 — 로그인·데이터 변경 감사 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- orchestrationOwner: `CODEX`
- interviewRound: 4
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: true
- implementationApproved: true

이 문서는 Fable 5가 사용자와 진행하는 deep-interview를 round별로 고정한다. Codex는 Fable 질문과 사용자 답변을 전달·기록하지만 업무 질문을 대신 만들거나 답하지 않는다. Interview 완료는 planning 또는 구현 승인이 아니다.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `QUESTIONS_REQUIRED` | 0 | 대화에서 확정한 기준선 기록 | Fable 질문 생성 |
| 1 | `QUESTIONS_REQUIRED` | 5 | 사용자가 질문을 실제 사람이 묻듯이 물어보도록 요청 | 같은 결정을 자연스러운 대화형 질문으로 다시 제시 |
| 2 | `QUESTIONS_REQUIRED` | 5 | 질문 4-a만 실패 시도 기록을 선택하고 나머지는 모두 Fable 권장안 채택 | Fable 확인용 요약 생성 |
| 3 | `QUESTIONS_REQUIRED` | 2 | 실패 입력값 미저장, 사용자 저장 의도 거절 전체 기록, 기존 권한 거부 원장과 화면 통합 확정 | Fable 확인용 요약 생성 |
| 4 | `SUMMARY_CONFIRMATION_REQUIRED` | 0 | 사용자가 Fable 확인용 요약 전체를 확인 | Fable planning 작성 |

### 사용자 전달 방식 요청

- 사용자 원문: `실제 사람이 질문하듯이 물어봐`
- 의미 변경 없이 반영할 방식: 질문 목적·선택지·권장안과 blocking decision은 유지하되, 한 번에 답하기 쉬운 자연스러운 대화형 질문으로 제시한다.
- 후속 사용자 원문: `앞으로 모든 질문은 사람이 질문하듯이 물어봐.`
- 지속 적용: 이후 모든 질문은 문서형 질문 묶음 대신 실제 대화처럼 짧고 자연스럽게 한 번에 필요한 결정만 제시한다.

### Round 2 사용자 답변

- 사용자 원문: `4번 실패한 저장 시도는 실패 시도도 기록한다. 나머지는 모두 권장안으로 진행`
- 결정 1: 앱이 확정 가능한 성공 로그인·앱 단계 거부·명시적 로그아웃만 기록한다. Entra 단계 실패는 v1에서 제외한다.
- 결정 2: 로그인 사건에만 IP와 브라우저·OS 계열 요약을 저장하고 변경 사건은 안전한 correlation으로 연결한다.
- 결정 3: 인증된 사용자가 성공시킨 업무 mutation 전체를 서버 field allowlist로 기록하고 worker·scheduler 자동 변경은 제외한다.
- 결정 4-a: 권한 거부 외 validation 등 실패한 저장 시도도 신규 감사 원장에 별도 사건으로 기록한다.
- 결정 4-b: 첨부는 파일 내용 없이 파일명·크기·행위·시각 metadata만 기록한다.
- 결정 5-a: 관리자 화면 기본 조회 기간은 최근 30일이다.
- 결정 5-b: 감사 원장은 기한 없이 append-only로 보관하고 v1에서 만료·purge하지 않는다.

### Round 3 사용자 답변

- 질문: 실패한 저장 시도 사건에 입력값 자체를 저장할지 여부.
- 사용자 답변: `네`
- 확정 해석: Fable 권장안 A를 채택해 실패 사건에는 행위자, 대상 영역·행동, 확인 가능한 대상 key, 실패 종류, 서버가 만든 사유 요약, 시각과 로그인 correlation만 저장한다. 검증되지 않은 시도 입력값은 저장하지 않는다.
- 질문: 신규 원장에 기록할 실패 종류.
- 사용자 답변: `네`
- 확정 해석: Fable 권장안 2-a A를 채택해 validation 실패, 동시 수정 충돌과 중복 요청 거절을 기록한다. 서버 내부 오류는 신규 원장에서 제외하고 기존 오류 로그·운영 관찰이 담당한다.
- 질문: 기존 권한 거부 감사와 신규 관리자 화면의 분담.
- 사용자 답변: `네`
- 확정 해석: Fable 권장안 2-b A를 채택해 권한 거부는 기존 `authorization_audit_events`에만 기록하고 신규 관리자 감사 화면에서 다른 실패 사건과 함께 통합 조회한다.

### Round 4 사용자 확인

- Fable 원문: `tasks/audit-001-interview-round-4-fable.md`
- 사용자 원문: `네 확인했어요`
- 확정 해석: Fable Round 4 확인용 요약의 업무 문제, 기록 범위, 관리자 화면·Excel, 보존·안전 정책, 적용·배포 방식, 명시적 제외와 비차단 결정 없음 전체를 확인했다.

### Planning·review resolution·구현 승인

- 승인일: 2026-08-28
- 사용자 원문: `승인.`
- 승인 해석: `tasks/audit-001-planning.md`의 기능 목표·포함/제외 범위와 `tasks/audit-001-review.md`의 세 권장 결정(G2 포함, 기존 전용 원장과 신규 통합 원장 병행, 자유문·대용량 값 metadata-only) 및 전체 resolution을 승인하고 구현 착수를 승인했다.
- 게시 경계: 구현·자동 검증·독립 검증 뒤 PR·main merge·Azure release 실행은 별도 승인 Gate로 유지한다.

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: Azure Front Door 요청 집계와 일부 업무 테이블의 최종 수정자·최종값을 대조해 제한적으로 변경을 추정한다.
- 해결할 문제: 현재 보관 자료만으로는 누가 언제 로그인했는지, 한 데이터가 어떤 값에서 어떤 값으로 바뀌었는지 전체 이력을 확정할 수 없다.
- 현재 우회 방식: 운영 DB의 최종 수정자와 Azure 변경성 HTTP 요청을 별도 근거군으로 조회하고 미귀속 요청은 특정 사용자에게 귀속하지 않는다.
- 성공했을 때 사용자가 할 수 있는 일: 관리자가 사용자별 로그인 시각과 결과, 이후 변경한 대상·항목·변경 전후 값·시각을 연결해 조회하고 Excel로 내보낸다.
- 하지 않을 경우 영향: 장애·오입력·권한 문의와 운영 책임 확인 때 네트워크 요청과 최종값만으로 추정해야 하며 과거 변경 경로를 복원할 수 없다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| System Administrator | 로그인·데이터 변경 감사 조회, 검색·필터, Excel 내보내기 | 승인된 감사 원장 전체 | 감사 원장 수정·삭제 없음 | 서버 관리자 권한, export 자체 감사 |
| 일반 업무 사용자 | 정상 로그인과 업무 데이터 입력·수정 | 본래 업무 권한 범위 유지 | 본래 업무 mutation만 | 감사기록을 우회하거나 수정할 수 없음 |
| 승인 대기 사용자 | 승인 대기 안내와 허용된 인증 경로 | 기존 승인 대기 범위 유지 | 업무 mutation 없음 | 성공 인증과 앱 단계 거부를 로그인 사건으로 기록 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: 로그인 결과를 감사 원장에 기록하고, 인증된 mutation이 성공하면 같은 transaction 경계에서 actor·대상·변경 전후·시각을 append-only로 기록한다. 관리자는 기간·사용자·업무영역·행동으로 조회하고 선택 Excel을 만든다.
- validation 실패: 저장되지 않은 업무 변경은 데이터 변경 성공으로 기록하지 않는다. 사용자의 저장 의도가 거절된 사건은 입력값 없이 metadata와 서버 실패 사유만 별도 기록한다.
- 동시 처리·중복: 재시도·idempotency·CAS가 같은 업무 mutation을 중복 적용하지 않으며 감사 원장은 실제 확정 transaction과 일치해야 한다.
- 취소·재시도·복구: 감사 원장은 수정·삭제하지 않고 정정·취소도 별도 사건으로 append한다. provider 인증 실패와 앱 로그인 실패의 경계를 구분해야 한다.
- 부분 실패와 rollback: 업무 transaction이 rollback되면 성공 변경 audit도 남지 않아야 한다. audit 조회·Excel 장애는 원장 mutation을 만들지 않는다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: 로그인 사건과 범용 데이터 변경 사건의 append-only 감사 원장, actor snapshot, 대상 type·stable key, action, 허용된 field별 before/after, occurred time, request/session correlation의 비밀이 아닌 안전 식별값.
- 상태 전이: audit event는 append-only이며 수정·삭제 상태 전이가 없다. 기한 없이 보관하고 v1에서 만료·purge하지 않는다.
- 보존·감사·삭제: 비밀번호·access/id/refresh token·Authorization header·cookie·첨부 binary/body는 절대 기록하지 않는다. IP는 로그인 사건에만 저장하고 기기 정보는 브라우저·OS 계열로 요약한다.
- attachment·Excel·PDF: attachment 내용은 audit에서 제외하고 파일명·크기·행위·행위자·시각 metadata만 기록한다. 관리자 선택 Excel은 포함한다. PDF는 현재 요청에 없다.
- 외부 연동·notification: Microsoft Entra 실패 로그인까지 포함하려면 Entra sign-in log/Graph 또는 Azure 진단 경계가 필요할 수 있다. 신규 알림 채널은 포함하지 않는다.
- migration·기존 데이터: additive migration만 허용한다. 과거 전체 이력은 소급 생성하지 않고 적용 이후 사건부터 확정 원장으로 관리한다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 관리자 조회 메뉴의 전체 감사 이력 화면에서 요약·필터·목록·상세·선택 Excel을 제공한다.
- loading·empty·error·success feedback: 조회와 export 상태를 구분하고 filter 상태를 보존한다. 원장 조회는 self-noise audit를 만들지 않는다.
- 접근성·390px·Teams narrow: desktop table과 의미 순서가 보존되는 mobile card, keyboard·label·focus·overflow 0을 검증한다.
- UAT와 rollout: isolated DB와 synthetic 사용자로 전체 검증 후 PR·CI·exact main Azure release를 수행한다. 운영 검증은 개인정보 안전 aggregate와 관리자 실제 화면 수동 검수로 제한한다.
- rollback과 운영자 대응: application rollback 시 additive audit table을 보존하고 forward-fix한다. 기존 업무 데이터와 audit 원장을 삭제하지 않는다.

## 6. 포함·제외 범위

### 포함

- 로그인 성공·실패·로그아웃 감사의 확정 가능한 경계
- 인증된 사용자의 성공한 데이터 변경과 field-level before/after
- 관리자 전용 조회·상세·선택 Excel
- additive migration, 격리 자동 검증, PR·CI·Azure 공개배포
- 개인정보 최소화·권한·보존·운영 SOP

### 제외

- 비밀번호·token·인증 헤더·cookie·attachment 내용 수집
- 감사 원장 수정·삭제와 일반 사용자 전체 감사 열람
- 과거 전체 변경 이력의 추정 생성
- 기존 업무 권한 확대와 신규 알림 채널

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | Microsoft Entra 실패 로그인과 앱 진입을 어디까지 로그인 사건으로 볼지 | 앱 확정 경계 / Entra 연동 / 성공만 | 앱 확정 경계 | 권장안 채택 | No |
| 2 | IP·기기 정보의 저장 형태와 보존기간 | 로그인 사건만 / 전 사건 / 미저장 | 로그인 사건만, 브라우저·OS 요약 | 권장안 채택 | No |
| 3 | 전체 업무 mutation 중 v1에서 기록할 field·대상 범위 | 인증 사용자 전체 / 핵심 우선 / 관리자 중심 | 인증 사용자 전체 | 권장안 채택 | No |
| 4 | 실패한 업무 변경 시도와 첨부 metadata 기록 범위 | 실패 제외/포함, 첨부 metadata 포함/제외 | 실패 제외, 첨부 metadata 포함 | 실패 시도 포함, 첨부 metadata 포함 | No |
| 5 | 관리자 조회·Excel의 기본 기간과 장기 보존 운영 | 7/30/90일, 무기한/아카이브 삭제 | 30일, 무기한 | 권장안 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: 로그인과 데이터 변경을 사용자·시각·변경 전후 값까지 확정 가능한 감사 원장으로 남긴다.
- 권장 범위: 관리자 전용 append-only 원장과 조회·Excel, 적용 이후 사건, additive migration, Azure 공개배포.
- 확정한 정책: 기존 업무 권한 보존, audit 수정·삭제 금지, 비밀번호·token·인증 헤더·cookie·첨부 내용 제외, 과거 이력 소급 생성 금지.
- 명시적 제외: 기존 권한 확대, 신규 알림 채널, 과거 데이터 추정 생성.
- Deferred 비차단 결정: 없음.
- Fable 판정: `COMPLETED_CONFIRMED`

## 9. 성공 기준

- 업무 결과: 관리자가 익명 요청 집계가 아니라 사용자와 실제 저장 변경을 연결해 조회·Excel 보존할 수 있다.
- 권한·데이터 불변조건: 관리자만 전체 조회, audit append-only, 업무 transaction과 audit consistency, secret·attachment body 미기록.
- 자동 검증: Backend·Frontend·migration·authorization·concurrency·Excel·desktop/390px·isolated Full-Stack·전체 pipeline 통과.
- 사용자 검수: 공개 운영에서 로그인 사건과 synthetic 또는 승인된 bounded 변경 사건을 privacy-safe 방식으로 확인한다.

## 10. 사용자 확인

- [x] 업무 문제와 기대 결과가 정확하다.
- [x] 대상 역할과 권한이 정확하다.
- [x] 정상·예외·복구 흐름이 정확하다.
- [x] 포함·제외 범위가 정확하다.
- [x] Blocking 결정이 남아 있지 않다.
- [x] Fable 5가 작성한 이 요약을 planning 입력으로 사용하는 데 동의한다.

현재 상태는 다음과 같다.

- `interviewStatus: COMPLETED_CONFIRMED`
- `userConfirmed: true`
- `openBlockingDecisionCount: 0`
- `planningApproved: true`
- `implementationApproved: true`
