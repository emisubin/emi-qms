# TASK-AUDIT-001 — Deep Interview Round 4 (Fable 5)

## Round 4 안내

Round 3의 세 가지 답변을 interview 문서(`tasks/audit-001-interview.md`) 기준으로 확인했다 — ① 실패한 저장 시도 사건에는 시도 입력값을 저장하지 않고 metadata와 실패 사유만 기록, ② 사용자의 저장 의도가 거절된 경우 전체(validation 실패·동시 수정 충돌·중복 요청 거절)를 기록하고 서버 내부 오류는 제외, ③ 권한 거부는 기존 `authorization_audit_events`에만 기록하고 신규 관리자 화면에서 통합 조회. 이로써 결정 1~5와 실패 시도 관련 하위 결정까지 모두 확정됐고, 최신 Repository 기준선(운영 인증은 Microsoft Entra 위임, 기존 감사 자산 4종 재사용 가능, 최신 migration `0082`로 신규 원장은 additive `0083+`)과 충돌하는 항목도 없다. **남은 blocking 질문은 없으므로 이번 round는 새 질문 대신 확인용 요약이다.** 아래 요약이 정확한지 봐주시고, 맞으면 확인해주세요. 틀린 항목이 있으면 어느 부분인지 알려주시면 그 부분만 다시 정리한다.

## 확인용 요약

### 해결할 문제

지금은 Azure 요청 집계와 일부 테이블의 최종 수정자만으로 변경을 추정할 수 있을 뿐, "누가 언제 로그인했고, 어떤 데이터를 어떤 값에서 어떤 값으로 바꿨는지"를 확정할 수 없다. 이 기능이 완료되면 System Administrator가 사용자별 로그인 이력과 이후의 실제 변경(대상·항목·변경 전후 값·시각)을 연결해 조회하고 선택 Excel로 보존할 수 있다.

### 무엇을 기록하는가 (확정 결정)

1. **로그인 사건** — 앱이 확정할 수 있는 경계만: 성공 로그인(승인 대기 사용자의 성공 인증 포함), 앱 단계 거부(승인 대기·비활성), 명시적 로그아웃. 자동 세션 갱신은 로그인으로 세지 않는다. Microsoft Entra 단계 실패(비밀번호 오류·MFA 실패)는 v1에서 제외하고 Entra portal에서 별도 확인한다.
2. **IP·기기 정보** — 로그인 사건에만 IP와 브라우저·OS 계열 요약을 저장하고, 변경 사건은 안전한 correlation 값으로 해당 로그인에 연결한다.
3. **성공한 데이터 변경** — 인증된 사용자가 성공시킨 업무 mutation 전체를 서버 field allowlist로 field-level before/after까지 기록한다. worker·스케줄러의 시스템 자동 변경은 제외한다.
4. **실패한 저장 시도** — 사용자의 저장 의도가 거절된 경우 전체(validation 실패, 동시 수정 충돌, 중복 요청 거절)를 별도 사건으로 기록한다. 저장 내용은 행위자, 대상 영역·행동, 확인 가능한 대상 key, 실패 종류, 서버가 만든 사유 요약, 시각, 로그인 correlation까지만이며 **검증되지 않은 시도 입력값 자체는 저장하지 않는다.** 서버 내부 오류(5xx)는 신규 원장에서 제외하고 기존 오류 로그·운영 관찰이 담당한다.
5. **권한 거부** — 지금처럼 기존 `authorization_audit_events`에만 기록(중복 저장 없음)하고, 신규 관리자 화면이 읽기 전용으로 합쳐 실패 종류 필터의 하나로 보여준다.
6. **첨부** — 파일 내용 없이 파일명·크기·행위·행위자·시각 metadata 사건만 기록한다.

### 관리자 화면과 Excel

- System Administrator 전용 감사 조회 화면에서 기간(기본 최근 30일, KST 경계)·사용자·업무영역·행동·실패 종류로 요약·필터·목록·상세를 제공한다.
- Excel은 기존 선택 export 패턴을 재사용해 현재 필터·선택 범위만 내보내고, export 실행 자체도 기존 `data_export_events` 계약으로 자체 감사한다.
- desktop table과 의미 순서가 보존되는 mobile card(390px·Teams narrow), keyboard·label·focus·overflow 0을 검증한다.

### 보존과 안전 정책 (확정 불변조건)

- 감사 원장은 **기한 없이 append-only 보관**하며 수정·삭제·purge를 만들지 않는다. 정정·취소도 별도 사건으로 append한다.
- 비밀번호·access/id/refresh token·Authorization header·cookie·첨부 binary/body는 절대 기록하지 않는다.
- 성공 변경 audit은 업무 transaction과 같은 경계에서 기록해 rollback 시 함께 사라지고, audit 조회·Excel 장애는 원장 mutation을 만들지 않는다.
- 기존 업무 권한은 확대하지 않고, 관리자만 전체 조회하며, 일반 사용자는 감사기록을 우회·수정할 수 없다.

### 적용·배포 방식

- additive migration만 사용하고 과거 이력은 소급 생성하지 않는다 — 적용 이후 사건부터 확정 원장이다.
- isolated DB와 synthetic 사용자로 전체 자동 검증 후 PR·CI·exact main Azure 공개배포를 진행한다. 운영 검증은 privacy-safe aggregate와 관리자 실제 화면 수동 검수로 제한한다.
- application rollback 시에도 audit table과 기존 데이터는 보존하고 forward-fix한다.

### 명시적 제외

- Entra 단계 실패 로그인 수집(외부 연동), 신규 알림 채널, 기존 권한 확대
- 감사 원장 수정·삭제, 일반 사용자 전체 감사 열람, 과거 전체 이력의 추정 생성
- 비밀·인증정보·첨부 내용 수집, 서버 내부 오류의 신규 원장 기록

### Deferred 비차단 결정

없음. (장기 저장량 관리는 운영 관찰 후 별도 Task로 결정하기로 확정했으며 v1 미결정 사항이 아니다.)

## 사용자 확인 요청

위 요약이 맞으면 interview 문서 10장의 확인 항목대로 **"확인합니다"** 라고 답해주세요. 그러면 interview가 `COMPLETED_CONFIRMED`로 바뀌고 planning 작성을 시작할 수 있다. 이 확인은 interview 요약에 대한 동의이며, planning 승인이나 구현 승인은 아니다. 틀리거나 바꾸고 싶은 항목이 있으면 해당 부분만 알려주세요.

---

- interviewStatus: SUMMARY_CONFIRMATION_REQUIRED
- planningStatus: NOT_STARTED
- implementationApproved: false
