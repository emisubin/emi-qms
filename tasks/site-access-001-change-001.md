# TASK-SITE-ACCESS-001 Change 001 — 승인된 구현 계약

- instructionChainRead: `true`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- canonicalTaskId: `TASK-SITE-ACCESS-001`
- roadmapSequenceMatch: `false`
- explicitRoadmapOverrideApproved: `true`
- planningApproved: `true`
- implementationApproved: `true`
- approvalSource: `USER_EXPLICIT`
- approvedDecision: `명시적 로그아웃 종료는 planning 16장 권장안 A`
- publicationApproved: `false`
- azureDeploymentApproved: `false`

## 승인 내용

사용자는 Fable interview 요약을 확인한 뒤 planning과 Codex review의 권장안으로 구현 시작을 승인했다. 명시적 로그아웃은 접속 행에 종료 시각·고정 종료 사유를 한 번만 기록한다.

## 포함 범위

- additive `0084` migration
- actor+browser client+30분 창 세션, 서버/DB 시각, 동시성·idempotency
- 고정 메뉴 코드 19개와 최초 방문 순서의 중복 없는 누적
- IP·browser/OS family·app access outcome snapshot
- best-effort signal과 bounded explicit end
- 전체 감사 이력 목록·상세·필터·summary·mobile·선택 Excel
- 별도 site coverage와 시간 해석 안내
- Backend/Frontend/unit/PostgreSQL/full-stack/desktop/390 검증
- planning/review/change/implementation report/SOP/user manual/checklist/Roadmap

## 제외 범위

- Commit, Push, PR, Merge
- 대표 repo `main` 반영
- Persistent UAT·운영 DB migration
- Azure 공개배포
- 실제 Entra/외부 provider mutation
- 과거 접속 소급, URL·query·업무 식별자, heartbeat·클릭·키 입력

## 보존 불변조건

- 기존 Login/Logout/global mutation/authorization audit 의미와 데이터
- `AuthenticatedIdentity` signal, `Audit.Read.All` 조회
- 감사 실패로 업무·화면 이동·로그아웃 차단 금지
- 제한 필드 외 update와 모든 delete 금지
- 개인정보·secret·raw request/response 미기록
