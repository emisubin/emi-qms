# TASK-AUDIT-001 — 로그인·데이터 변경 감사 원장 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/audit-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- authoringModel: `FABLE_5`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 현재는 Azure 요청 집계와 일부 업무 테이블의 최종 수정자·최종값만으로 변경을 추정할 수 있을 뿐, 누가 언제 로그인했고 어떤 데이터가 어떤 값에서 어떤 값으로 바뀌었는지 확정할 수 없다.
- 대상 사용자·역할: System Administrator가 감사 원장을 조회·필터·선택 Excel로 보존한다. 일반 업무 사용자와 승인 대기 사용자는 기존 권한 범위를 그대로 유지하며 감사기록을 우회·수정할 수 없다.
- 정상 흐름: 로그인 결과를 감사 원장에 기록하고, 인증된 사용자의 성공한 업무 mutation을 같은 transaction 경계에서 actor·대상·field-level before/after·시각과 함께 append-only로 기록한다. 관리자는 기간(기본 최근 30일, KST 경계)·사용자·업무영역·행동·실패 종류로 조회하고 선택 Excel을 만든다.
- 예외·복구 흐름: 사용자의 저장 의도가 거절된 사건 전체(validation 실패·동시 수정 충돌·중복 요청 거절)를 입력값 없이 metadata와 서버 사유 요약으로 별도 기록한다. 서버 내부 오류(5xx)는 신규 원장에서 제외한다. 업무 transaction rollback 시 성공 변경 audit도 함께 사라져야 하고, audit 조회·Excel 장애는 원장 mutation을 만들지 않는다. 정정·취소도 별도 사건으로 append한다.
- 확정한 정책과 명시적 제외: 앱이 확정 가능한 성공 로그인·앱 단계 거부·명시적 로그아웃만 기록(자동 세션 갱신 제외, Entra 단계 실패는 v1 제외). IP·브라우저·OS 계열 요약은 로그인 사건에만 저장하고 변경 사건은 안전한 correlation으로 연결. worker·scheduler 자동 변경 제외. 권한 거부는 기존 `authorization_audit_events`에만 기록하고 신규 화면에서 읽기 전용 통합 조회. 첨부는 파일명·크기·행위·행위자·시각 metadata만. 기한 없는 append-only 보관, v1 만료·purge 없음. 비밀번호·token·Authorization header·cookie·첨부 binary/body 절대 미기록. 과거 이력 소급 생성 금지, 기존 권한 확대·신규 알림 채널 없음.
- planning으로 넘긴 비차단 미결정 사항: interview 기준 없음. 단, Repository 대조에서 발견한 범위 해석 2건을 16장 사용자 결정 항목으로 새로 제시한다(기존 확정 정책과 충돌하지 않는 경계 해석이다).

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

System Administrator가 익명 요청 집계 대신, 사용자별 로그인 이력과 그 사용자가 실제로 저장한 변경(대상·항목·변경 전후 값·시각)과 거절된 저장 시도를 하나의 감사 화면에서 연결 조회하고 선택 Excel로 보존할 수 있다.

## 2. 배경과 해결할 업무 문제

- 현재 관리자는 Azure Front Door 요청 집계와 일부 테이블의 최종 수정자·최종값을 별도 근거군으로 대조해 변경을 제한적으로 추정한다. 미귀속 요청은 특정 사용자에게 귀속하지 않는다.
- 장애·오입력·권한 문의와 운영 책임 확인 때 과거 변경 경로를 복원할 수 없고, 로그인 사실 자체도 확정 근거가 없다.
- 기존 감사 자산은 부분적이다: 권한 거부(`authorization_audit_events`), Excel export(`data_export_events`), 관리자 기준정보 변경(`admin_master_change_logs`), 알림 설정(`user_notification_preference_audit_events`)은 각자 원장이 있지만, 로그인 사건과 일반 업무 mutation의 field-level 이력은 없다.
- 이 기능이 없으면 감사 요구가 생길 때마다 운영 DB와 네트워크 로그를 수동 대조해야 하며 그 결과도 추정에 그친다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| System Administrator | 감사 조회·검색·필터·상세·선택 Excel | 감사 원장 전체(로그인·성공 변경·실패 시도·권한 거부 통합) | 감사 원장 수정·삭제 없음. Excel export는 기존 `data_export_events`로 자체 감사 |
| 일반 업무 사용자 | 정상 로그인과 본래 업무 mutation | 기존 업무 권한 범위 유지(감사 화면 접근 불가) | 본래 업무 mutation만. 감사기록 우회·수정 불가 |
| 승인 대기 사용자 | 허용된 인증 경로와 승인 대기 안내 | 기존 승인 대기 범위 유지 | 업무 mutation 없음. 성공 인증·앱 단계 거부가 로그인 사건으로 기록됨 |

권한은 기존 `Audit.Read.All` permission(마이그레이션 `0006`에서 system-administrator 전용으로 고정, `QmsPolicies.AuditReadAll` 정책 기존재)을 재사용한다. 신규 권한 능력을 만들지 않으므로 interview의 "기존 권한 확대 없음" 불변조건을 보존한다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 로그인·변경 연결 조회

1. 관리자가 관리자 메뉴의 전체 감사 이력 화면에 진입한다(기본 최근 30일).
2. 시스템이 기간 내 사건 요약(로그인·성공 변경·실패 시도·권한 거부 건수)과 목록을 표시한다.
3. 관리자가 특정 사용자·업무영역으로 필터하고, 변경 사건 상세에서 field별 before/after와 연결된 로그인 사건(시각·IP·브라우저/OS 계열)을 확인한다.

### 시나리오 B — 거절된 저장 시도 확인

1. 사용자가 업무 화면에서 저장을 시도했으나 validation 실패로 거절된다.
2. 시스템이 입력값 없이 행위자·대상 영역/행동·확인 가능한 대상 key·실패 종류·서버 사유 요약·시각·로그인 correlation을 실패 사건으로 기록한다.
3. 관리자가 실패 종류 필터(권한 거부 포함)로 해당 사건을 조회한다. 권한 거부는 기존 원장에서 읽기 전용으로 합쳐 보인다.

### 시나리오 C — 선택 Excel 보존

1. 관리자가 현재 필터 결과에서 행을 선택하고 Excel 내보내기를 실행한다.
2. 시스템이 기존 선택 export 패턴(server allowlist column picker)으로 파일을 만들고 export 실행 자체를 `data_export_events`에 기록한다.
3. 관리자가 파일을 보존한다. export 장애는 감사 원장에 mutation을 만들지 않는다.

## 5. 기능 요구사항

### 필수

- [ ] 로그인 사건 기록: 성공 로그인(승인 대기 사용자의 성공 인증 포함), 앱 단계 거부(승인 대기·비활성), 명시적 로그아웃. 자동 세션 갱신은 제외.
- [ ] 로그인 사건에만 IP와 브라우저·OS 계열 요약 저장. 변경 사건은 서버 발급 안전 correlation으로 로그인에 연결.
- [ ] 인증된 사용자가 성공시킨 업무 mutation 전체를 서버 field allowlist 기준 field-level before/after와 함께 같은 transaction 경계에서 append-only 기록. worker·scheduler 자동 변경 제외.
- [ ] 실패한 저장 시도(validation 실패·동시 수정 충돌·중복 요청 거절)를 입력값 없이 별도 사건으로 기록. 서버 내부 오류 제외.
- [ ] 첨부 행위는 파일 내용 없이 파일명·크기·행위·행위자·시각 metadata 사건으로 기록.
- [ ] System Administrator 전용 통합 감사 화면: 요약·필터(기간 KST 경계·사용자·업무영역·행동·실패 종류)·목록·상세. 권한 거부는 기존 `authorization_audit_events` 읽기 전용 통합.
- [ ] 선택 Excel export(기존 selected export 계약 재사용, 신규 export kind 추가, `data_export_events` 자체 감사).
- [ ] additive migration(`0083`+, 구현 시점 최신 번호 확정)과 적용 이후 사건부터의 확정 원장.

### 선택

- [ ] 변경 사건 상세에서 같은 correlation의 다른 사건으로 이동하는 링크.
- [ ] 요약 영역의 사건 종류별 기간 집계 카드.

### 명시적 제외

- [ ] Entra 단계 실패 로그인 수집(Entra sign-in log/Graph 연동), 신규 알림 채널, 기존 권한 확대.
- [ ] 감사 원장 수정·삭제·purge, 일반 사용자 전체 감사 열람.
- [ ] 과거 전체 변경 이력의 추정·소급 생성.
- [ ] 비밀번호·access/id/refresh token·Authorization header·cookie·첨부 binary/body 수집.
- [ ] 서버 내부 오류(5xx)의 신규 원장 기록(기존 오류 로그·운영 관찰 담당).
- [ ] 검증되지 않은 시도 입력값 저장.

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 전체 감사 이력 | 관리자 메뉴(Roadmap 20장의 "전체 감사 이력 관리" 슬롯) | 기간 요약 집계, 사건 목록(시각·행위자·종류·업무영역·행동·대상 key), 페이지네이션 | 기간·사용자·업무영역·행동·사건 종류(성공 변경/실패 시도/권한 거부/로그인) 필터, 행 선택, 상세 열기, 선택 Excel | 조회 loading/empty/error 구분, filter 상태 보존, export 진행·완료·실패 피드백 |
| 사건 상세 | 목록 행 클릭(패널 또는 시트) | 변경 사건: field별 before/after, 첨부 metadata, 연결 로그인 요약. 로그인 사건: 결과·IP·브라우저/OS 계열. 실패 사건: 실패 종류·서버 사유 요약 | 닫기, correlation 연결 사건 조회(선택) | 권한 부족·대상 없음 구분 표시 |

확인할 UX 항목:

- 기존 `NotificationPreferenceAuditPage`의 요약·필터·목록·Excel 구성 관례를 재사용해 관리자 화면 간 일관성을 유지한다.
- desktop table과 의미 순서가 보존되는 mobile card(390px·Teams narrow), keyboard·label·focus, page-level horizontal overflow 0.
- 원장 조회·상세 열람은 self-noise audit(조회 행위의 mutation 기록)를 만들지 않는다. Excel export만 기존 계약대로 자체 감사한다.
- correlation이 없는 변경 사건(예: 배포 전 시작된 세션)은 오류가 아니라 "로그인 연결 없음"으로 구분 표시한다.

## 7. 업무 규칙과 불변조건

- 감사 원장은 append-only다. 수정·삭제·purge API와 상태 전이를 만들지 않고 정정·취소도 별도 사건으로 append한다.
- 성공 변경 audit은 해당 업무 mutation과 같은 DB transaction 경계에서 기록한다. rollback되면 audit도 남지 않는다.
- 실패 시도 사건은 rollback된 업무 transaction 밖(별도 연결)에서 기록하되, 기록 실패가 원래 오류 응답을 바꾸지 않는다.
- Backend가 권한·기록 여부의 authoritative layer다. Frontend 숨김은 보조 수단이다.
- 비밀 값 필드(예: token·연결 문자열)는 field allowlist에 절대 포함하지 않으며 allowlist에 없는 field는 기록하지 않는다.
- AdminUserSwitch(개발 전용 사용자 전환)로 수행된 mutation은 effective actor와 함께 기존 claim의 actual actor도 기록해 행위자 위장을 남기지 않는다.
- 재시도·idempotency·CAS 경계에서 같은 업무 mutation이 중복 적용되지 않는 기존 계약을 유지하고, 감사 원장은 실제 확정 transaction과 1:1로 일치해야 한다.
- 기존 원장(`authorization_audit_events` 등 4종)의 schema·쓰기 경로를 수정하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 로그인 사건 | 성공/앱 단계 거부/명시적 로그아웃, IP·브라우저/OS 계열 요약, 서버 발급 correlation | 신규 | append-only, 무기한 |
| 데이터 변경 사건 | actor(및 actual actor)·업무영역·행동·대상 type·stable key·시각·correlation | 신규 | append-only, 무기한 |
| 변경 field 상세 | 사건별 allowlist field의 before/after 값 | 신규 | 사건과 함께 보존 |
| 실패 시도 사건 | 실패 종류(fixed enum)·서버 사유 요약·대상 key(확인 가능한 경우) | 신규 | append-only, 입력값 미저장 |
| 첨부 metadata 사건 | 파일명·크기·행위(업로드/삭제 등)·행위자·시각 | 신규(변경 사건의 하위 유형으로 표현 가능) | 내용물 미저장 |
| 권한 거부 사건 | 기존 `authorization_audit_events` | 기존 | 신규 화면에서 읽기 전용 통합, 중복 저장 없음 |
| Export 감사 | 기존 `data_export_events` + 신규 export kind | 기존 확장 | 기존 계약 유지 |

```text
(로그인/변경/실패 사건 발생) → append 기록 → [종결: 상태 전이 없음, 수정·삭제 없음]
```

테이블·컬럼의 실제 이름과 index 구성은 구현 조사에서 확정한다. 조회 축(occurred_at desc, actor, 업무영역)의 index와 field 상세의 사건 단위 저장은 필수 성능 요건으로 남긴다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 기록 대상 판정(field allowlist·worker 제외·실패 종류 분류), 관리자 전용 조회 권한, append-only 불변.
- 필요한 조회와 mutation(이름은 제안이며 구현 조사에서 확정):
  - 로그인 사건 기록 endpoint(프론트가 MSAL 인증 완료 직후 호출, 서버가 claims로 성공/승인 대기/거부를 확정하고 opaque correlation을 발급)와 명시적 로그아웃 기록 endpoint(best-effort beacon).
  - 관리자 감사 조회 endpoint(paged, 필터, 요약 집계, 기존 권한 거부 원장 통합 read)와 선택 Excel endpoint.
- 인증 구조 제약: 현재 운영 인증은 요청별 Entra bearer로 stateless이며 서버 로그인 endpoint가 없다(`GET /api/identity/me`가 부트스트랩). 따라서 "성공 로그인"은 프론트의 실제 대화형 인증 완료 시점에 기록하는 사건으로 정의하고, silent token 갱신·새로고침은 기록하지 않는다(중복 방지 가드는 클라이언트+서버 양쪽에 둔다). 이는 interview가 확정한 "앱이 확정 가능한 경계"의 구현 해석이다.
- 성공 변경 기록 메커니즘: 공용 audit recorder를 각 feature store의 기존 transaction에 주입해 mutation과 같은 경계에서 append한다. 현재 약 40개 store가 자체 Npgsql transaction을 관리하므로, recorder는 ambient connection/transaction을 받는 형태여야 한다. 업무영역별 field allowlist는 서버 코드에 선언적으로 고정한다.
- 실패 시도 기록 메커니즘: endpoint 오류 경계에서 fixed enum(validation 실패·동시 수정 충돌·중복 거절)으로 분류해 별도 연결로 기록한다. 5xx는 제외한다.
- 권한·validation: 조회·Excel은 `QmsPolicies.AuditReadAll` 재사용. 기록 endpoint는 인증 사용자 전용이며 다른 사용자의 사건을 만들 수 없다. correlation은 서버 발급 opaque 값으로, 제출 시 소유 사용자 일치까지 검증하고 불일치·부재 시 null 처리한다.
- transaction·동시성·idempotency: audit append는 기존 mutation의 lock·CAS 계약을 바꾸지 않는다. 로그인 사건 기록은 저부하 append라 별도 경합 제어가 필요 없고, 중복 호출 방어만 둔다.
- audit trail: 이 Task 자체가 audit trail이다. Excel export는 기존 `data_export_events` check constraint에 신규 kind를 additive migration으로 추가한다.
- 외부 provider 영향: 없음. 실제 발송·신규 연동 없음. IP는 Front Door 뒤이므로 신뢰 가능한 forwarded header에서만 추출한다는 구현 규칙을 남긴다.

Repository 조사 전 내부 클래스명, 컬럼명과 SQL 형태를 확정하지 않는다(위 이름은 재사용 대상 실제 자산 외에는 제안이다).

## 10. Frontend 고려사항

- route/component: `App.tsx` 관리자 메뉴에 신규 route 1개. 페이지는 `NotificationPreferenceAuditPage.tsx`·`notificationPreferenceAudit.ts` 구성 관례를 따라 신규 page + api module로 작성. 인증 완료 훅에서 로그인 사건 기록 호출, 명시적 로그아웃 경로(`webPushLogout.ts`가 사용하는 로그아웃 흐름과 같은 지점)에 로그아웃 beacon 추가.
- loading/empty/error/success: 조회와 export 상태 분리, filter 상태 보존, 권한 거부·대상 없음 구분.
- 공통 Action Feedback: `useActionFeedback.ts`와 `SelectedExcelExport.tsx`·`useSelectedRows.ts` 재사용.
- 접근성: keyboard·label·focus order·`aria-live` 기존 규칙 적용.
- 390px/mobile/narrow pane: desktop table ↔ 의미 순서 보존 mobile card, page-level overflow 0 검증.
- 로그인 사건 기록 실패는 사용자 로그인 흐름을 막지 않는다(best-effort, 서버 로그로 관찰).

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 모든 업무영역 store의 mutation 경로에 recorder 연결이 필요하다(변경 범위가 넓은 대신 각 연결은 기계적이다). 알림 발송 worker의 자동 변경은 제외한다.
- 권한/관리자: `Audit.Read.All`·system-administrator 전용 유지. 관리자 홈 메뉴에 화면 1개 추가.
- Excel/PDF/첨부: 선택 Excel은 `SelectedExportColumnRegistry`·`SelectedExcelExportService`·`ExcelExportConcurrencyGate` 재사용. 첨부는 metadata 사건만. PDF 없음.
- Teams/Mail: 영향 없음.
- 삭제·복구/감사: 기존 원장 4종(권한 거부·export·기준정보 변경·알림 설정)은 그대로 두고, 권한 거부만 신규 화면에서 통합 표시한다(interview 확정). 관리자 삭제 lifecycle의 purge worker 실행은 시스템 자동 변경으로 제외되고, 관리자가 직접 실행한 삭제·복구 action은 성공 변경 사건으로 기록된다.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | store transaction 내 공용 recorder + 업무영역별 field allowlist 선언 + endpoint 오류 경계의 실패 사건 기록 | interview의 "같은 transaction 경계·field-level before/after"를 정확히 충족. rollback 일관성 보장. allowlist가 코드 리뷰 가능한 단일 위치에 고정 | 전 업무영역 store를 수정하는 넓은 변경 범위. 구현·회귀 검증 비용이 큼 |
| B | HTTP middleware가 mutation 성공 응답을 일괄 기록 | 변경 범위 최소 | field-level before/after 불가, transaction 경계 밖이라 rollback 불일치. interview 확정 요건 미달 — 채택 불가 |
| C | DB trigger 기반 table 단위 이력 | 누락 없는 포착 | actor·업무 맥락·allowlist 표현이 어렵고 기존 raw-SQL store 계약과 이질적. 운영 diagnosability 저하 |

권장안은 A다. interview 결정 3(성공 mutation 전체·field allowlist·transaction 일치)이 A만 충족한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 검증은 isolated DB·synthetic 사용자만 사용한다. Persistent UAT write 없음.
- migration 필요 여부: 있음. additive 신규 테이블 + `data_export_events` kind constraint 확장(`0083`+, 구현 시점 최신 번호 확정). destructive 변경 없음, rollback은 application forward-fix·audit table 보존.
- 외부 발송/실제 데이터 영향: 실제 provider 발송 없음. 기존 업무 데이터·기존 원장 원문을 수정·삭제하지 않는다.
- runtime 교체 여부: Azure 공개배포 포함(사용자가 재정렬과 함께 명시 승인). 기존 release gate(PR 필수 CI → exact main SHA → migration 성공 후 application 교체 → public security smoke)를 따르되, 실제 배포 실행은 구현·검증 완료 후 별도 게시·release 승인 경계를 유지한다.
- 추가 사용자 승인 필요 작업: planning 승인, 구현 착수 승인, PR·main merge 승인 1회, Azure release 승인. 운영 검증은 privacy-safe aggregate와 관리자 실제 화면 수동 검수로 제한한다.

## 14. 검증 계획

- 최소 테스트(Backend): recorder의 성공 기록·rollback 시 미기록·allowlist 준수(비밀 field 미기록), 실패 사건 분류(validation·CAS 충돌·중복 거절)와 5xx 제외, 로그인 사건 성공/승인 대기/거부/로그아웃과 중복 방지, correlation 소유 검증, 관리자 조회·권한 차단(403 + 기존 권한 거부 원장 기록), Excel export kind.
- 영향 영역 회귀: recorder가 연결된 대표 업무영역 store의 기존 mutation·동시성 테스트 전체, migration 기존 DB·fresh DB 이중 검증, Frontend lint·typecheck·unit·build와 신규 페이지 desktop/390px smoke.
- PR/CI: Validation Matrix 기준 전체 Backend·Frontend·isolated Full-Stack pipeline. desktop/mobile privacy-safe 시각 증빙.
- 사용자 검수: 공개 운영에서 관리자 화면으로 본인 로그인 사건과 synthetic 또는 승인된 bounded 변경 사건을 확인한다. 증빙은 boolean·count·fixed enum 등 privacy-safe projection만 사용한다.

## 15. 완료 기준

- 기능/권한/데이터: 로그인·성공 변경·실패 시도·첨부 metadata 사건이 확정 정책대로 기록되고, System Administrator만 통합 조회·Excel이 가능하며, append-only·secret 미기록·transaction 일치 불변조건이 테스트로 고정된다.
- UX: 요약·필터·목록·상세·선택 Excel이 desktop과 390px에서 overflow 0으로 동작하고 loading/empty/error/권한 상태가 구분된다.
- 자동 테스트: 14장 계획의 전체 통과. 미실행 항목은 이유와 함께 기록.
- 5종 산출물: Implementation report(내부 SOP·user manual·checklist 포함 가능)·Roadmap update(실행 큐·추적 항목 48·Decision Log의 2026-08-27 재정렬 반영)·user validation checklist의 상태·위치 추적.
- 사용자 검수 상태: 자동 검증 완료와 사용자 검수 완료를 분리 관리하며 검수 전 완료로 표기하지 않는다.
- PR 상태: 사용자 승인 아래 PR·CI·main merge·exact main Azure release까지. 각 게시 단계는 별도 승인 경계를 유지한다.

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | G2 운영 모듈(생산·납품·출근)의 mutation도 신규 감사 원장에 포함하는가 | A. 포함(권장 — "인증 사용자의 업무 mutation 전체"라는 확정 결정에 부합하고 감사 사각을 없앰) / B. 제외(G2는 purpose·data 분리 워크스페이스라는 기존 결정을 우선) | 대기 |
| 2 | 이미 전용 원장이 있는 영역(알림 설정·기준정보 변경·업무 시작/완료·export)과 개인 설정성 mutation(프로필 사진·web push 기기 등록)의 처리 | A. 중복 저장 없이 기존 원장·기존 화면 유지, 개인 설정성 mutation은 신규 원장에서 제외, 신규 화면 통합은 interview 확정대로 권한 거부만(권장 — 중복 저장 금지 원칙과 interview 확정 범위에 일치) / B. 신규 원장에도 병행 기록해 한 화면에서 전부 조회 | 대기 |

두 항목 모두 비차단이며, 결정 전까지 구현 착수를 막지 않도록 권장안 기준으로 범위를 산정했다. 사용자 결정이 다르면 해당 경계만 조정한다.

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: 신규 audit feature 폴더(recorder·contracts·store·endpoints), 각 업무영역 store의 mutation 경로 recorder 연결, endpoint 오류 경계의 실패 사건 기록, Excel export kind 등록.
- Frontend: 신규 관리자 감사 페이지와 api module, `App.tsx` route·메뉴, 인증 완료·로그아웃 훅의 사건 기록 호출.
- DB/Migration: 신규 append-only 감사 테이블들과 index, `data_export_events` kind constraint 확장(additive, `0083`+).
- Tests/Scripts: Backend 신규·회귀 테스트, Frontend unit·smoke, isolated Full-Stack 시나리오 추가.
- Docs: Product Roadmap(20·21·23·24장 추적 항목 48·Decision Log), Implementation report와 5종 산출물.

## 18. Roadmap 연결

- 선행 Task: TASK-002 identity 기반, TASK-ADMIN-001 관리자 화면 관례, TASK-EXPORT-001 선택 export 계약, TASK-AZURE-DEPLOY-001 운영 release gate.
- 후속 Task: 장기 저장량·아카이브 운영 정책(운영 관찰 후 별도 Task), Entra 단계 실패 로그인 연동(별도 NEW_FEATURE 후보).
- 현재 Go/No-Go: 사용자가 2026-08-27 운영 관찰보다 본 Task를 우선하는 Roadmap 재정렬을 명시 승인했다(Identity Gate `PASS_CREATE`, `explicitRoadmapOverrideApproved: true`). Roadmap 실행 큐·Decision Log 반영은 본 Task 산출물이다.
- 별도 Task로 분리할 항목: 감사 데이터 보존량 운영 고도화, Entra sign-in 연동, 감사 기반 alerting.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-08-27 | Interview Round 1~4 답변과 확인용 요약 확인(`tasks/audit-001-interview.md`) | 0장 기준선과 5·7·13장 확정 정책으로 반영 |

## 20. Codex 구현 지시문 초안

승인 후 새 Codex 구현 세션에 전달할 계약 요약이다(승인 전 실행 금지).

1. 이 planning과 Codex review resolution, 최신 Repository를 다시 읽고 `APPROVED_FEATURE_IMPLEMENTATION`으로 시작한다. 16장 사용자 결정 2건의 확정값을 먼저 반영한다.
2. additive migration(`0083`+ 최신 번호)으로 로그인·변경·field·실패 사건 테이블과 export kind 확장을 추가하고 기존 DB·fresh DB에서 검증한다. 기존 migration·원장 schema를 수정하지 않는다.
3. transaction 주입형 공용 recorder와 업무영역별 field allowlist를 구현하고, 전 업무영역 store의 성공 mutation·첨부 metadata·실패 분류를 연결한다. worker·scheduler 경로와 5xx는 제외한다. 비밀 field는 allowlist에 넣지 않는다.
4. 로그인/로그아웃 사건 endpoint와 서버 발급 correlation(소유 검증·null fallback)을 구현하고, 자동 세션 갱신 중복 기록을 차단한다.
5. `AuditReadAll` 정책으로 관리자 통합 조회(권한 거부 원장 읽기 전용 포함)·선택 Excel을 구현하고, 기존 selected export·Action Feedback·관리자 audit 페이지 관례를 재사용해 desktop/390px 화면을 만든다.
6. 14장 검증 계획 전체를 실행하고, Roadmap update·Implementation report·5종 산출물을 기록한 뒤 사용자 검수·게시 승인 경계(PR·merge·Azure release 각 1회)를 지킨다.

## 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 2
