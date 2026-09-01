# TASK-SITE-ACCESS-001 — 유지 세션 포함 사이트 접속 이력 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/site-access-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 새 Microsoft 대화형 로그인과 데이터 변경만 감사 화면에 남고, 로그인 상태가 유지된 채 PMS에 들어온 접속은 관리자가 확인할 수 없다.
- 대상 사용자·역할: System Administrator가 조회하고, 모든 인증 사용자(승인 대기·비활성 포함)의 실제 접속이 기록 대상이다.
- 정상 흐름: 새로고침·다른 페이지 진입을 접속 신호로 삼는다. 진행 중인 접속이 없으면 접속 행 하나를 만들고, 이후 신호는 같은 행의 마지막 활동 시각과 중복 없는 방문 메뉴 목록만 갱신한다.
- 예외·복구 흐름: 명시적 로그아웃은 종료로 확정한다. 종료 신호가 없으면 마지막 활동 뒤 30분을 만료로 계산하고 이후 사용은 새 접속이다. 기록 실패는 best-effort로 PMS 사용을 막지 않고 서버 오류 로그로 관찰해 forward-fix한다.
- 확정한 정책과 명시적 제외: 30분 활동 창 묶음, 큰 메뉴·화면의 고정 표시 이름 누적(`홈`, `알림` 포함), IP·브라우저·OS 계열 저장, 기존 `전체 감사 이력` 통합 조회·상세·선택 Excel, `Audit.Read.All` 재사용. 제외: Entra sign-in log 연동, 페이지별 클릭·키 입력·모든 HTTP 요청 수집, 전체 주소·업무 식별자, 과거 접속 소급 생성, 비밀·raw 원문, 기존 권한 확대, 신규 알림 채널.
- planning으로 넘긴 비차단 미결정 사항: interview 기준 없음. 단, planning 조사에서 종료 확정 기록 방식 1건을 사용자 결정 항목으로 새로 도출했다(16장).

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

이 기능이 완료되면 System Administrator는 로그인 방식과 무관하게 "누가 언제부터 언제까지 PMS에 접속해 어느 큰 메뉴를 봤는지"를 전체 감사 이력에서 조회하고 선택 Excel로 보존할 수 있다.

## 2. 배경과 해결할 업무 문제

- 현재 관리자는 `관리자 → 운영 → 전체 감사 이력`에서 대화형 `Login`·`Logout`·저장 성공·저장 실패·권한 거절만 확인한다. `TASK-AUDIT-001`은 silent token·restored account·자동 세션 갱신을 의도적으로 제외했다.
- 로그인 상태 유지가 일반적인 사용 패턴이므로, 유지 세션으로 들어온 실제 사용은 감사 화면 어디에도 남지 않아 "언제 누가 PMS를 썼는가"를 확정할 수 없다.
- 현재 우회 방식은 대화형 로그인 이력이나 Azure 요청 집계를 따로 보는 것인데, 사용자별 실제 접속으로 확정할 수 없다.
- 이 기능이 없으면 접속 기반 감사 질문(근태·보안·사용 현황 확인)에 감사 화면만으로 답할 수 없는 상태가 지속된다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| System Administrator | 사이트 접속 사건 조회·필터·상세·선택 Excel | 전체 접속 이력 (`Audit.Read.All` 재사용) | 접속 원장 수정·삭제 없음 |
| 일반 업무 사용자 | 기존처럼 PMS 사용. 접속은 자동 계측 | 기존 업무 권한 그대로 | 자기 인증 요청 기준으로만 접속이 기록됨 |
| 승인 대기·비활성 사용자 | 기존 앱 접근 결과 유지 | 기존 허용 범위 그대로 | 접속 행에 앱 접근 결과(`Allowed`/`ApprovalPending`/`Inactive`)로 구분 기록 |

서버가 인증 principal 기준으로 actor를 확정하며, 다른 사용자의 접속을 만들거나 조작하는 요청은 거절한다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 유지 세션 접속 조회

1. 사용자가 어제 로그인해 둔 브라우저로 오늘 아침 PMS를 다시 연다(새 로그인 없음).
2. Frontend가 접속 신호를 best-effort로 보내고, 서버는 진행 중 접속이 없으므로 새 접속 행(시작 시각, IP·브라우저·OS 계열, 앱 접근 결과)을 만든다.
3. 관리자는 전체 감사 이력에서 사건 종류 `사이트 접속`으로 이 행을 보고, 대화형 `Login` 행이 없어도 실제 접속을 확인한다.

### 시나리오 B — 30분 활동 창 묶음과 만료

1. 같은 사용자가 오전 중 여러 페이지를 이동하고 새로고침하며 다중 탭을 쓴다.
2. 서버는 같은 행의 마지막 활동 시각만 갱신하고 방문한 큰 메뉴·화면 이름을 중복 없이 누적한다. 자동 token 갱신도 새 행을 만들지 않는다.
3. 마지막 활동 뒤 30분간 조용하면 관리자 화면에는 만료로 표시되고, 오후에 다시 쓰면 새 접속 행이 생긴다. 명시적 로그아웃을 하면 그 접속은 종료로 확정된다.

### 시나리오 C — 방문 메뉴 확인과 선택 Excel 보존

1. 관리자가 기간·사용자·사건 종류로 필터해 접속 목록을 조회한다.
2. 목록·상세에서 접속별 방문 메뉴(`프로젝트`, `품질`, `홈`, `알림` 등)와 IP·기기 정보를 확인한다.
3. 필요한 행을 선택해 기존 선택 Excel 흐름으로 내보내고, 내보내기 자체는 기존 data export 감사에 남는다.

## 5. 기능 요구사항

### 필수

- [ ] 새로고침·페이지 진입을 신호로 하는 사이트 접속 세션 기록(유지 세션 포함, 신규 additive migration)
- [ ] 진행 중 접속 1건 유지: 동시 탭·새로고침·자동 token 갱신을 30분 활동 창 안에서 한 행으로 묶는 서버 측 원자적 중복 방지
- [ ] 마지막 활동 시각과 중복 없는 방문 메뉴 목록만 갱신하는 제한적 갱신 예외(그 외 필드·행 삭제는 DB 수준 차단)
- [ ] 명시적 로그아웃 종료 확정과 마지막 활동+30분 만료 계산(클라이언트 종료 신호에 의존하지 않음)
- [ ] 방문 메뉴는 서버 allowlist의 고정 표시 이름만 저장(`홈`, `알림` 등 큰 메뉴 밖 화면 포함, 전체 주소·업무 식별자 금지)
- [ ] IP·브라우저 계열·OS 계열·앱 접근 결과 저장(기존 로그인 계측 helper 재사용)
- [ ] 전체 감사 이력 목록·상세·필터에 사건 종류 `사이트 접속` 통합(`Audit.Read.All` 전용)
- [ ] 방문 메뉴를 목록·상세·선택 Excel 모두에 표시(mobile card는 좁은 화면에 맞게 축약 배치)
- [ ] best-effort 실패 처리: 기록 실패가 PMS 사용을 막지 않고 서버 오류 로그로 관찰

### 선택

- [ ] 감사 요약 aggregate에 사이트 접속 건수 추가(기존 summary 카드 확장)
- [ ] 접속 상태(진행 중/종료/만료) 필터

### 명시적 제외

- [ ] Entra 자체 sign-in log 연동과 로그인 실패 수집
- [ ] 페이지별 클릭·키 입력·모든 HTTP 요청·화면 내용 수집
- [ ] 전체 URL·query string·업무 식별자 저장
- [ ] 과거 접속 추정·소급 생성과 기존 `Login`/`Logout` 의미 변경
- [ ] 기존 업무 권한 확대, 신규 알림 채널, 접속 원장 수정·삭제 UI/API

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 전체 감사 이력 목록(확장) | 관리자 → 운영 → 전체 감사 이력 | 사건 종류 `사이트 접속`, 사용자·부서, 시작·마지막 활동 시각, 상태(진행 중/종료/만료), 방문 메뉴 요약, IP·브라우저·OS | 기간·사용자·사건 종류 필터, 페이지 이동, 행 선택 | 기존 loading/empty/error/권한 거절 UX 보존 |
| 감사 상세(확장) | 목록 행 클릭 | 접속 시작·마지막 활동·종료/만료 정보, 방문 메뉴 전체 목록, IP·기기·앱 접근 결과 | 닫기 | 기존 상세 패턴 보존 |
| 선택 Excel(확장) | 목록에서 행 선택 후 내보내기 | 접속 행 + 방문 메뉴 열 | 기존 선택 Excel 흐름 | 기존 export 피드백·export 감사 보존 |

확인할 UX 항목: 진행 중/종료/만료 상태가 한눈에 구분되는가, 방문 메뉴가 많은 행이 목록을 깨뜨리지 않는가(요약+상세 전체 표시), 390px·Teams narrow에서 mobile card에 핵심 정보(사용자·시각·상태)가 유지되는가, page-level horizontal overflow 0을 보존하는가.

## 7. 업무 규칙과 불변조건

- 기존 대화형 `Login`·명시적 `Logout` 사건의 의미·계약·화면 표시는 변경하지 않는다. 사이트 접속은 별개 사건이다.
- 서버가 인증 principal로 actor를 확정한다. 요청 본문으로 다른 사용자·시각·IP를 지정할 수 없다.
- 접속 행 생성은 append-only이며 갱신은 "마지막 활동 시각"과 "방문 메뉴 목록"(+ 16장 결정에 따른 1회성 종료 확정)으로만 한정한다. 그 외 갱신·삭제는 DB trigger로 차단하고 수정·삭제 API/UI를 만들지 않는다.
- 방문 메뉴는 서버가 검증한 고정 표시 이름 catalog 값만 저장한다. 자유 문자열·URL·업무 식별자는 거절한다.
- 비밀번호·token·Authorization header·cookie·raw request/response·화면 내용을 저장하지 않는다.
- 만료(30분)는 저장된 클라이언트 신호가 아니라 서버가 계산한다.
- 접속 기록 실패는 업무 요청을 실패시키지 않는다(best-effort). 실패는 서버 오류 로그로만 관찰한다.
- 과거 접속을 추정·소급 생성하지 않으며 application rollback 시에도 신규 원장을 삭제하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 사이트 접속 세션 | 사용자별 30분 활동 창 단위의 접속 1건. 시작·마지막 활동 시각, 종료 종류, IP·브라우저·OS 계열, 앱 접근 결과, 중복 없는 방문 메뉴 목록 | 신규 (별도 테이블) | 무기한 보존, 제한적 갱신 예외 외 append-only, 삭제 금지 |
| 방문 메뉴 catalog | 큰 메뉴·고정 화면 표시 이름 allowlist (`홈`, `내 업무`, `Pending`, `알림`, `프로젝트`, `생산관리`, `구매`, `자재`, `제조`, `품질`, `물류`, `영업`, `G2`, `양식 관리`, `관리자` 등 현재 navigation 기준) | 신규 (fixed enum 성격) | 코드+표시 이름 고정, 서버 검증 |
| 기존 `audit_events` `Login`/`Logout` | 대화형 로그인·명시적 로그아웃 원장 | 기존 | 의미·계약 불변 |
| 감사 조회 통합 | Global/Authorization에 사이트 접속 원본 추가 | 기존 확장 | `Audit.Read.All` 전용 |

```text
접속 신호 수신 → (진행 중 접속 없음) 새 접속 행 생성
             → (진행 중 접속 있음) 마지막 활동·방문 메뉴 갱신
진행 중 → 명시적 로그아웃 → 종료 확정
진행 중 → 마지막 활동 + 30분 무활동 → 만료(서버 계산) → 다음 신호는 새 접속 행
```

기존 `audit_events`의 check 제약은 `Login` 외 사건에 IP·기기 필드를 금지하므로, 사이트 접속은 기존 테이블 확장이 아니라 별도 테이블 + 조회 통합이 기존 구조와 정합적이다. 정확한 테이블·컬럼명과 migration 번호(현재 최신 `0083` 이후)는 구현 시 최신 main에서 확정한다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: actor 확정, 30분 창 묶음과 만료 계산, 방문 메뉴 allowlist 검증, 갱신 예외 한정, 관리자 전용 조회.
- 필요한 조회와 mutation:
  - 인증 사용자용 접속 신호(heartbeat) 수신 1개 — 현재 방문 메뉴 코드만 전달, 응답 실패는 앱 흐름에 영향 없음. 기존 `/api/audit/sessions/interactive-login`의 best-effort 패턴을 따른다.
  - 관리자 조회는 기존 `/api/admin/audit-events` 목록·상세에 사이트 접속 원본을 통합(신규 `eventType`·상세 `source` 추가)한다.
- 권한·validation: 신호는 `AuthenticatedIdentity` 수준(승인 대기·비활성 포함, 앱 접근 결과로 구분 기록), 조회는 기존 `Audit.Read.All` policy 재사용. 메뉴 코드는 fixed enum 검증, 위반은 거절하되 업무 화면에는 영향 없음.
- transaction·동시성·idempotency: 다중 탭·동시 신호 경쟁에서 진행 중 접속 행이 2개 생기지 않도록 원자적 upsert(단일 문 또는 DB 함수 + 적절한 unique/lock 전략)를 사용하고 동시성 테스트를 추가한다. 방문 메뉴 누적도 중복 없이 원자적으로 처리한다.
- audit trail: 접속 원장 자체가 감사 기록이다. append-only trigger 예외를 허용 필드로만 한정한 별도 guard를 둔다. 선택 Excel은 기존 `data_export_events` 흐름을 따른다.
- 외부 provider 영향: 없음. 알림·Teams·Mail 발송 없음.

Repository 조사 전 내부 클래스명, 컬럼명과 SQL 형태를 확정하지 않는다.

## 10. Frontend 고려사항

- route/component: 신규 화면 없음. `AuditPage` 확장(사건 종류 필터·목록 열·상세·mobile card), 접속 신호 전송은 앱 초기 로드/새로고침과 화면 전환 지점(`App.tsx`의 navigation 처리)에서 현재 화면의 고정 메뉴 코드와 함께 best-effort로 호출한다. `api.ts`의 기존 audit 호출 패턴(`recordInteractiveLoginAudit`류)을 따른다.
- loading/empty/error/success: 기존 감사 화면 상태 UX 보존. 신호 실패는 사용자에게 오류로 표시하지 않는다(best-effort).
- 공통 Action Feedback: 조회 전용 확장이므로 기존 패턴 유지. Excel 내보내기는 기존 선택 Excel 피드백 재사용.
- 접근성: 상태(진행 중/종료/만료)는 색상만이 아니라 텍스트로 구분하고 기존 keyboard·label 계약을 보존한다.
- 390px/mobile/narrow pane: mobile card에 사용자·시작/마지막 활동·상태를 우선 배치하고 방문 메뉴는 축약(개수+대표)·상세 전체 표시. horizontal overflow 0 유지.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 업무 데이터·알림 채널 영향 없음.
- 권한/관리자: `Audit.Read.All` policy와 관리자 운영 메뉴 구조 재사용. 권한 확대 없음.
- Excel/PDF/첨부: 기존 선택 Excel registry(`AuditLedgerSelected`)에 방문 메뉴 열 확장. PDF·첨부 영향 없음.
- Teams/Mail: 영향 없음. PWA·Teams 환경의 접속도 같은 신호로 같은 30분 창에 묶인다.
- 삭제·복구/감사: 접속 원장은 삭제·복구 대상이 아니며 기존 append-only 감사 계약과 나란히 보존된다.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | 별도 접속 세션 테이블 + 제한적 갱신(마지막 활동·방문 메뉴, 종료는 16장 결정에 따름) + 기존 감사 조회에 원본 통합 | 사용자 확정 결정(한 행 묶음·30분·목록 갱신)과 1:1 대응, 행 수 최소, 조회 단순, 기존 Global/Authorization 통합 패턴 재사용 | append-only 원칙에 명시적 갱신 예외가 생기므로 DB trigger로 허용 필드를 엄격히 한정해야 함 |
| B | 신호마다 append-only 이벤트 행을 쌓고 조회 시 30분 창으로 집계해 세션을 파생 | 순수 append-only, 갱신 예외 불필요 | 행 수가 사용량에 비례해 증가, 집계 쿼리·Excel·상세가 복잡, "한 접속 행으로 묶는다"는 사용자 결정과 저장 모델이 불일치 |
| C | 기존 `audit_events`에 사건 종류를 추가해 확장 | 테이블 추가 없음 | 기존 check 제약·append-only trigger·`Login` 전용 metadata 계약과 충돌, 기존 원장 계약 변경 위험 |

권장안은 A다. 사용자 확정 결정과 저장 모델이 일치하고, 기존 감사 화면·권한·Excel 통합 비용이 가장 낮으며, 갱신 예외는 interview에서 이미 확인된 범위다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 기본 검증은 isolated synthetic DB만 사용한다. Persistent UAT 적용·Azure 배포는 별도 승인 Gate다.
- migration 필요 여부: 필요. additive migration 1건(접속 세션 테이블·guard·export 제약 확장). 번호·schema는 구현 시 최신 main에서 확정하며 기존 migration을 수정하지 않는다.
- 외부 발송/실제 데이터 영향: 없음. provider 호출·알림 발송 없음. 과거 데이터 소급 없음.
- runtime 교체 여부: 이 Task 자체는 없음. 운영 반영은 별도 배포 승인에서 다룬다.
- 추가 사용자 승인 필요 작업: planning 승인, 구현 승인, Git 게시(commit/push/PR/merge) 각 단계, Persistent UAT·Azure 배포.

## 14. 검증 계획

- 최소 테스트: Backend — 새 접속 생성, 같은 창 내 신호의 단일 행 갱신, 30분 경계(29분/31분), 명시적 로그아웃 종료, 동시 신호 경쟁 단일 행 보장, 메뉴 중복 없는 누적, allowlist 위반 거절, 권한 거절, 갱신 guard(허용 외 필드 갱신·삭제 차단), fresh/forward migration. Frontend — `AuditPage` 확장 목록·필터·상세·mobile card, 신호 전송 실패 무해성.
- 영향 영역 회귀: 기존 감사 목록·상세·선택 Excel, 로그인·로그아웃 감사 회귀(`AuditInfrastructureTests`, `AuditPage.test.tsx`, `auth.test.tsx`), 전체 Backend·Frontend suite.
- PR/CI: 기존 필수 CI 전체. isolated Full-Stack에 접속→관리자 조회 시나리오 추가. desktop·390px 화면 검증.
- 사용자 검수: 운영 반영 뒤 본인 접속 1건과 privacy-safe aggregate(접속 행 count 증가, boolean/정수 projection)만으로 확인한다. raw row·개인 원문은 출력하지 않는다.

## 15. 완료 기준

- 기능/권한/데이터: 유지 세션 접속이 30분 창 1행으로 기록되고 관리자만 조회하며, 갱신 예외 외 append-only·비밀정보 미기록·기존 `Login`/`Logout` 불변이 검증됨.
- UX: 목록·상세·Excel·mobile card에서 방문 메뉴와 상태가 확인되고 overflow 0·기존 상태 UX 보존.
- 자동 테스트: 위 최소·회귀·Full-Stack이 모두 PASS이고 미실행 항목이 성공으로 기록되지 않음.
- 5종 산출물: `docs/12-task-completion-policy.md`에 따라 상태·위치 추적.
- 사용자 검수 상태: 자동 검증 완료와 사용자 검수 완료를 분리 기록.
- PR 상태: 사용자 게시 승인 전 Draft 유지.

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 명시적 로그아웃의 "종료 확정"을 어떻게 기록할까요? | A(권장): 접속 행에 1회성 종료 필드를 두고 비어 있을 때 한 번만 확정(그 외 변경은 DB 차단). 유지 세션 로그아웃도 종료로 남음 / B: 저장하지 않고 기존 `Logout` 사건·30분 계산으로 조회 시 파생. 저장 모델이 더 순수하지만 대화형 로그인 없이 쓰다가 로그아웃한 접속은 "만료"로만 표시됨 | 대기 (비차단 — 구현 전 확정 필요) |

Interview는 "명시적 로그아웃은 종료로 확정"과 "갱신 예외는 마지막 활동·방문 메뉴 한정"을 함께 확정했는데, 두 문장을 저장 방식 수준에서 잇는 선택이 남아 planning 사용자 결정으로 도출했다. 화면에 보이는 동작은 두 안이 거의 같고, 권장안 A는 로그아웃 종료를 모든 접속 유형에서 동일하게 보여준다.

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `backend/src/Emi.Qms.Api/Audit/`(신호 endpoint·store·조회 통합), `DataExports/` 선택 Excel 열 확장, 권한 policy 재사용
- Frontend: `frontend/src/AuditPage.tsx`, `frontend/src/audit.ts`, `frontend/src/api.ts`, `frontend/src/App.tsx`(신호 전송·메뉴 코드 매핑)
- DB/Migration: `database/migrations/` 신규 additive 1건(번호는 최신 main에서 확정)
- Tests/Scripts: `backend/tests/Emi.Qms.Api.Tests/`, `frontend/tests/`, `frontend/e2e/full-stack/` 신규·확장
- Docs: Roadmap 3.3M 상태 갱신, 필요 시 SOP/User manual — 게시·갱신은 Codex 승인 범위에서 수행

## 18. Roadmap 연결

- 선행 Task: `TASK-AUDIT-001`(원장·관리자 화면·`Audit.Read.All`·선택 Excel 기반, Change 004 Azure 반영 완료·공개 로그인 재검수는 사용자 승인으로 이 Task 뒤로 순연).
- 후속 Task: 운영 반영 후 접속 원장 보존량 관찰. Entra sign-in log 연동은 별도 신규 기능 후보로만 남긴다.
- 현재 Go/No-Go: Roadmap 3.3M `Deep Interview In Progress / Explicit Roadmap Override Approved` 상태에서 interview `COMPLETED_CONFIRMED`가 기록됐고, 다음 Gate는 Codex review → 사용자 구현 승인이다.
- 별도 Task로 분리할 항목: Persistent UAT·Azure 배포 Gate, `TASK-AUDIT-001`의 공개 대화형 로그인 재검수.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-08-31 | 신규 기능 착수(`추가구현 승인`)와 Roadmap 순서 변경(`네 승인`) | Task Identity Gate `PASS_CREATE`, Roadmap 3.3M·추적 97 등록 |
| 2026-08-31 | Interview Round 1~4 답변과 Round 5 요약 `확인` | `COMPLETED_CONFIRMED`, blocking 결정 0으로 이 planning의 0장·5~8장에 반영 |

## 20. Codex 구현 지시문 초안

1. 최신 main에서 instruction chain·Roadmap·이 planning과 review resolution을 다시 읽고 `APPROVED_FEATURE_IMPLEMENTATION`으로 시작한다(사용자 구현 승인 뒤).
2. additive migration으로 접속 세션 테이블, 허용 필드 한정 갱신 guard, 방문 메뉴 allowlist 검증, 선택 Excel export 제약 확장을 추가한다. 기존 migration과 `audit_events` 계약을 수정하지 않는다. 16장 결정 결과(A 또는 B)를 schema에 반영한다.
3. Backend에 인증 사용자 접속 신호 endpoint(best-effort, actor 서버 확정, 원자적 30분 창 upsert)와 기존 `/api/admin/audit-events` 목록·상세·summary의 사이트 접속 통합, 선택 Excel 열 확장을 구현한다. `Audit.Read.All`·`AuthenticatedIdentity` policy와 기존 browser/OS 계열 helper를 재사용한다.
4. Frontend는 앱 초기 로드·새로고침·화면 전환에서 고정 메뉴 코드와 함께 신호를 보내고 실패를 조용히 관찰 로그로 처리한다. `AuditPage`에 사건 종류·상태·방문 메뉴 표시를 확장하고 desktop·390px를 보존한다.
5. 14장 검증 계획의 최소·회귀·isolated Full-Stack을 실행하고, privacy-safe projection으로만 결과를 기록한다. Persistent UAT·Azure·실제 provider는 건드리지 않는다.
6. Implementation report와 5종 산출물 상태를 기록하고, 게시는 사용자 승인 범위에서만 진행한다.

## 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] 16장 미결정 1건(종료 확정 기록 방식) 사용자 결정
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 1
