# TASK-SITE-ACCESS-001 — 유지 세션 포함 사이트 접속 이력 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- orchestrationOwner: `CODEX`
- interviewRound: 5
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 Fable 5가 사용자와 진행하는 deep-interview를 round별로 고정한다. Codex는 Fable 질문과 사용자 답변을 전달·기록하지만 업무 질문을 대신 만들거나 답하지 않는다. Interview 완료는 planning 또는 구현 승인이 아니다.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `QUESTIONS_REQUIRED` | 0 | 대화에서 확인된 문제·승인 기준선 기록 | Fable 질문 생성 |
| 1 | `QUESTIONS_REQUIRED` | 5 | 사용자가 질문을 실제 사람이 묻듯이 물어보도록 요청했고 정책 결정 답변은 아직 하지 않음 | 같은 결정을 짧고 자연스러운 대화형 질문으로 다시 제시 |
| 2 | `QUESTIONS_REQUIRED` | 5 | 사용자는 새로고침이나 다른 페이지에 들어갈 때 기록하면 된다고 답했고, 이후 질문은 한 번에 5개씩 그대로 전달하도록 요청 | Fable이 접속 세션과 페이지 방문 기록 경계를 확인하고 남은 결정을 질문 |
| 3 | `QUESTIONS_REQUIRED` | 5 | 사용자가 `1 B / 2 A / 3 B / 4 A / 5 A`를 선택 | Fable 확인용 요약 생성 |
| 4 | `QUESTIONS_REQUIRED` | 3 | 사용자가 `1 A / 2 A / 3 A`를 선택 | Fable 확인용 요약 생성 |
| 5 | `SUMMARY_CONFIRMATION_REQUIRED` | 0 | 사용자가 확인용 요약 전체를 확인 | Fable planning 작성 |

### Round 1 Fable 원문

- [site-access-001-interview-round-1-fable.md](site-access-001-interview-round-1-fable.md)

### Round 1 사용자 응답

- 사용자 원문: `사람이 물어보듯이 질문해`
- 의미 변경 없이 반영할 방식: 질문의 순서·정책 결정·선택지·권장안은 유지하되, 한 번에 답하기 쉬운 짧고 자연스러운 대화형 질문으로 다시 제시한다.
- 정책 결정 답변 상태: 미수집

### Round 2 Fable 원문

- [site-access-001-interview-round-2-fable.md](site-access-001-interview-round-2-fable.md)

### Round 2 사용자 응답

- 사용자 원문: `새로고침이나 다른 페이지를 들어가면 그때 기록하면 됨.`
- 사용자 원문: `한번에 5개 질문 하는거 그대로 해.`
- 확정 가능한 의미: 기존 인증 세션이어도 새로고침하거나 다른 페이지에 들어간 시점을 기록 대상으로 삼는다.
- 추가 확인 필요: 각 새로고침·페이지 이동을 독립 감사 행으로 남기는지, 해당 신호로 하나의 접속 세션 시작·최근 활동만 갱신하는지 Fable이 구분해 확인한다.
- 질문 전달 방식: 실제 사람이 묻듯이 표현하되 한 round의 질문을 최대 5개까지 한 번에 전달한다.

### Round 3 Fable 원문

- [site-access-001-interview-round-3-fable.md](site-access-001-interview-round-3-fable.md)

### Round 3 사용자 응답

- 사용자 원문:
  - `1 b`
  - `2 a`
  - `3 b`
  - `4a`
  - `5 a`
- 결정 1: 새로고침·페이지 이동은 진행 중인 접속 한 건으로 묶고 같은 행의 마지막 활동 시각만 갱신한다.
- 결정 2: 마지막 활동 뒤 30분이 지나면 접속이 끝난 것으로 보고 이후 사용은 새 접속으로 기록한다.
- 결정 3: 전체 주소나 업무 식별자는 저장하지 않고 `프로젝트`, `품질` 같은 큰 메뉴 이름 수준만 기록한다.
- 결정 4: 기존 `관리자 → 운영 → 전체 감사 이력`에 `사이트 접속`을 추가하고 IP·브라우저·OS 계열과 선택 Excel을 지원한다.
- 결정 5: 접속 기록 실패는 best-effort로 처리해 PMS 사용을 막지 않고 서버 운영 오류로 관찰한다.

### Round 4 Fable 원문

- [site-access-001-interview-round-4-fable.md](site-access-001-interview-round-4-fable.md)

### Round 4 사용자 응답

- 사용자 원문:
  - `1a`
  - `2a`
  - `3a`
- 결정 1: 한 접속 행에 방문한 큰 메뉴·화면 이름을 중복 없이 모아서 저장한다.
- 결정 2: 큰 메뉴 밖 화면도 `홈`, `알림`처럼 화면에 표시되는 고정 이름 수준으로 저장한다.
- 결정 3: 방문 메뉴는 감사 이력 목록·상세·선택 Excel에 모두 표시하고 mobile card에서는 좁은 화면에 맞게 배치한다.

### Round 5 Fable 확인용 요약 원문

- [site-access-001-interview-round-5-fable.md](site-access-001-interview-round-5-fable.md)

### Round 5 사용자 확인

- 사용자 원문: `확인`
- 확정 의미: Fable Round 5 확인용 요약의 업무 문제, 접속 신호·묶음·30분 만료, 방문 메뉴 누적, 관리자 화면·Excel·IP·기기 정보, best-effort 실패 처리, 권한·개인정보·migration·rollout과 명시적 제외 전체를 planning 입력으로 확인했다.

### 사용자 요청 기준선

- 사용자 원문: `맞아 사이트 접속 기록을 보고싶은거야`
- 사용자 원문: `추가구현 승인`
- 확정 의미: System Administrator가 새 Microsoft 인증뿐 아니라 기존 인증 세션으로 PMS에 실제 들어온 사용자의 사이트 접속을 확인할 수 있는 신규 기능을 기획·구현한다.
- Roadmap 순서 변경 확인 질문: `기존 로그인 검수는 뒤로 미루고, 사이트 접속 이력 기획과 구현을 지금 먼저 진행해도 될까요?`
- 사용자 원문: `네 승인`
- 확정 의미: 기존 `TASK-AUDIT-001`의 공개 대화형 로그인 재검수보다 이 신규 기능의 기획·구현을 먼저 진행한다.
- 질문 전달 방식: 이전 사용자 standing instruction에 따라 모든 질문은 실제 사람이 묻듯이 짧고 자연스럽게 전달한다. Fable 원문의 질문·선택지·권장안 의미는 바꾸지 않는다.

### Repository 기준선

- 선행 기능 `TASK-AUDIT-001`은 MSAL Redirect/Popup의 대화형 `LOGIN_SUCCESS`와 명시적 `Logout`만 기록하고 silent token·restored account·자동 세션 갱신은 의도적으로 제외한다.
- 관리자 `전체 감사 이력`은 `Audit.Read.All` 권한으로 Login·Logout·저장 성공·저장 실패·권한 거절을 조회하고 선택 Excel로 내보낸다.
- 신규 기능은 기존 `Login`의 의미를 바꾸지 않고 별도 사이트 접속 사건·세션 lifecycle을 추가하는 방향을 우선 검토한다.
- 기존 audit는 append-only·무기한 보관·과거 소급 금지이며 비밀번호·token·Authorization header·cookie·raw request/response를 기록하지 않는다.
- 최신 main migration은 `0083_global_access_change_audit.sql`이다. 신규 migration 번호·schema는 planning 승인 뒤 최신 main에서 다시 확정한다.

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 관리자는 대화형 Microsoft 로그인과 데이터 변경은 감사 화면에서 확인할 수 있지만, 기존 인증 세션으로 PMS에 들어온 접속은 확인할 수 없다.
- 해결할 문제: 사용자가 사이트에 실제로 접속한 시각과 접속 세션을 로그인 방식과 무관하게 관리자에게 제공한다.
- 현재 우회 방식: 대화형 로그인 이력 또는 Azure 요청 집계를 별도로 보지만 유지 세션의 사용자별 실제 접속으로 확정할 수 없다.
- 성공했을 때 사용자가 할 수 있는 일: 관리자가 사용자별 사이트 접속 시작·최근 활동과 확정된 종료/만료 정보를 전체 감사 이력에서 조회하고 필요한 범위를 Excel로 보존한다.
- 하지 않을 경우 영향: 로그인 상태가 유지되는 일반 사용 패턴에서는 누가 언제 PMS를 사용했는지 감사 화면만으로 파악할 수 없다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| System Administrator | 사이트 접속 이력 조회·필터·상세·Excel | 승인된 접속 이력 전체 | 접속 원장 수정·삭제 없음 | 기존 `Audit.Read.All` 재사용 우선 |
| 일반 업무 사용자 | 기존 인증 상태로 PMS 접속·사용 | 기존 업무 권한 유지 | 본래 업무만 | 접속 계측을 임의 사용자로 만들거나 수정할 수 없음 |
| 승인 대기·비활성 사용자 | 기존 앱 접근 결과 유지 | 기존 허용 범위 유지 | 업무 mutation 없음 | 사이트 접속과 앱 접근 결과의 구분 필요 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: 새로고침이나 다른 페이지 진입을 접속 신호로 사용한다. 진행 중인 접속이 없으면 접속 행 하나를 만들고 이후 신호는 같은 행의 마지막 활동 시각과 방문 메뉴 목록만 갱신한다. 30분 동안 활동이 없는 뒤의 다음 사용은 새 접속으로 기록한다.
- validation 실패: 다른 사용자의 접속을 만들거나 조작한 요청은 서버가 거절해야 한다.
- 동시 처리·중복: 새로고침, 다중 탭, PWA/Teams와 자동 token 갱신은 같은 30분 활동 창 안에서 접속 한 건으로 묶고 방문 메뉴는 중복 없이 누적한다.
- 취소·재시도·복구: 명시적 로그아웃은 종료로 확정한다. 네트워크 단절·브라우저 강제 종료처럼 종료 신호가 없으면 마지막 활동 뒤 30분을 만료로 계산하고 이후 접속 때 새 행을 만든다.
- 부분 실패와 rollback: 접속 기록은 best-effort이며 기록 실패가 PMS 사용을 막지 않는다. 실패는 서버 운영 오류로 관찰하고 forward-fix한다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: 기존 `Login`과 구분되는 사이트 접속 세션. 시작 시각, 마지막 활동 시각, 종료 종류, IP·브라우저·OS 계열, 앱 접근 결과와 중복 없는 고정 방문 메뉴 표시 이름을 가진다.
- 상태 전이: 접속 시작→마지막 활동·방문 메뉴 갱신→명시적 로그아웃 종료 또는 마지막 활동+30분 만료. 만료 뒤 새 신호는 새 접속을 만든다.
- 보존·감사·삭제: 접속 시작 원장은 기존과 같이 보존하고 수정·삭제 UI/API를 만들지 않는다. 갱신 예외는 비밀정보가 아닌 마지막 활동 시각과 방문 메뉴 목록으로만 한정한다.
- attachment·Excel·PDF: 첨부·PDF 영향 없음. 방문 메뉴를 관리자 목록·상세·선택 Excel에 포함한다.
- 외부 연동·notification: Entra sign-in log·신규 알림 채널은 현재 요청에 없으며 기본 제외 후보다.
- migration·기존 데이터: additive migration만 허용하고 과거 접속을 추정·소급 생성하지 않는다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 기존 `관리자 → 운영 → 전체 감사 이력`에서 `사이트 접속` 사건과 접속 세션 상세를 조회하는 방향을 우선 검토한다.
- loading·empty·error·success feedback: 기존 감사 화면 상태 UX를 보존한다.
- 접근성·390px·Teams narrow: 기존 desktop table·mobile card·keyboard·overflow 0 계약을 보존한다.
- UAT와 rollout: isolated synthetic 검증 후 사용자 직접 접속 1건과 aggregate를 분리 검수한다. Persistent UAT·Azure는 별도 승인 Gate다.
- rollback과 운영자 대응: application rollback 시 신규 원장을 삭제하지 않고 forward-fix하며 기존 로그인·업무 감사는 계속 보존한다.

## 6. 포함·제외 범위

### 포함

- 기존 인증 세션을 포함한 사이트 접속 시작과 세션 lifecycle
- 새로고침·페이지 이동을 신호로 한 30분 접속 창, 다중 탭·자동 token 갱신 중복 방지
- 큰 메뉴·화면의 고정 표시 이름을 접속 행에 중복 없이 누적
- 관리자 전체 감사 이력 조회·상세·필터·선택 Excel과 IP·브라우저·OS 계열
- additive migration, Backend/Frontend 계측, isolated 자동 검증과 privacy-safe 운영 검수

### 제외

- Entra 자체 로그인 실패·sign-in log 연동
- 페이지별 클릭·키 입력·모든 HTTP 요청 수집
- 비밀번호·token·Authorization header·cookie·raw request/response·화면 내용 수집
- 과거 접속 추정·소급 생성
- 기존 업무 권한 확대와 신규 알림 채널

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 접속 신호와 묶음 | 새로고침·페이지 이동마다 낱개 / 30분 활동 창으로 묶음 | 30분 활동 창 | 한 접속 행으로 묶음 | No |
| 2 | 만료 기준 | 30분 / 2시간 / 사용자 지정 | 30분 | 30분 | No |
| 3 | 방문 영역 | 미저장 / 큰 메뉴·화면 표시 이름 / 전체 주소 | 미저장 | 큰 메뉴·화면 표시 이름 | No |
| 4 | 표시·환경 정보 | 기존 감사 통합+IP·기기 / 별도 화면 / 통합+IP 제외 | 기존 감사 통합+IP·기기 | 기존 감사 통합+IP·기기·Excel | No |
| 5 | 기록 실패 | best-effort / 접속 차단 | best-effort | best-effort | No |
| 6 | 한 접속의 여러 메뉴 | 한 행에 누적 / 메뉴별 행 / 하나만 | 한 행에 누적 | 한 행에 중복 없이 누적 | No |
| 7 | 큰 메뉴 밖 화면 | 고정 표시 이름 / 미기록 | 고정 표시 이름 | `홈`, `알림` 등 고정 표시 이름 | No |
| 8 | 방문 메뉴 표시 위치 | 목록·상세·Excel / 상세만 | 목록·상세·Excel | 목록·상세·Excel | No |

## 8. Fable 확인용 요약

- 해결할 문제: 유지 세션을 포함해 실제 PMS 사이트 접속을 사용자별로 확인한다.
- 권장 범위: 새로고침·페이지 이동을 접속 신호로 사용하되 30분 활동 창을 한 행으로 묶고 마지막 활동·중복 없는 방문 메뉴를 갱신한다. 기존 전체 감사 이력·선택 Excel과 `Audit.Read.All`을 재사용한다.
- 확정한 정책: 30분 만료, 큰 메뉴·화면 고정 표시 이름 누적, IP·브라우저·OS 계열 저장, 목록·상세·Excel 표시, best-effort. 기존 대화형 Login 의미·권한·과거 소급 금지와 비밀정보 미기록을 보존한다.
- 명시적 제외: Entra sign-in log, 페이지별 클릭·키 입력·모든 HTTP 요청, 전체 주소·업무 식별자, 과거 접속 추정, 비밀·raw 원문, 기존 권한 확대, 신규 알림 채널.
- Deferred 비차단 결정: 없음.
- Fable 판정: `COMPLETED_CONFIRMED`

## 9. 성공 기준

- 업무 결과: 관리자가 유지 세션 사용자의 실제 사이트 접속을 대화형 로그인과 구분해 확인한다.
- 권한·데이터 불변조건: 관리자 전용 조회, 서버 actor 확정, 중복 방지, append-only 또는 승인된 session lifecycle, 비밀정보 미기록.
- 자동 검증: Backend·Frontend·migration·authorization·동시 탭·새로고침·timeout·Excel·desktop/390px·isolated Full-Stack과 전체 pipeline.
- 사용자 검수: 운영에서 사용자 접속 성공과 감사 row/session aggregate 증가를 별도 privacy-safe 근거로 확인한다.

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
- `planningApproved: false`
- `implementationApproved: false`
