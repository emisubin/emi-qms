# Task 종료 및 산출물 정책

## 1. 목적과 적용 범위

이 문서는 EMI 프로젝트 통합관리시스템의 모든 Task에 적용하는 종료 기준의 canonical policy다. Product Roadmap은 제품 방향과 Task 상태의 source of truth이고, Task 종료 산출물·품질 gate·검수 상태 판정은 이 문서를 따른다.

작은 문서 수정, hotfix, 조사 Task를 포함한 모든 Task에 적용한다. 적용 대상이 없는 항목도 생략하지 않고 `N/A`와 이유를 기록한다.

## 2. 필수 종료 산출물

Task 종료 시 다음 5종 산출물의 상태와 위치를 추적할 수 있어야 한다.

1. Implementation report
2. SOP
3. User manual
4. Roadmap update
5. User validation checklist

다섯 개의 독립 파일을 반드시 생성한다는 의미는 아니다. 기존 Task 문서 또는 implementation report 안에 명확히 분리된 섹션으로 포함할 수 있다. 다만 implementation report 또는 Product Roadmap에서 각 산출물의 상태와 위치를 찾을 수 있어야 한다.

- 각 산출물의 경로와 상태를 기록한다.
- 독립 파일이 아니면 포함된 canonical 문서와 section을 기록한다.
- 적용 대상이 없으면 `N/A`로 기록하고 구체적인 비적용 근거를 함께 쓴다. 단순 누락은 `N/A`가 아니다.
- 산출물 링크는 현재 checkout에서 유효해야 한다.
- 5종 산출물 중 하나라도 누락·미추적 상태이면 Task 완료로 판정하지 않는다.

## 3. Implementation report 필수 내용

기본 파일명은 `tasks/<task-id>-implementation-report.md`다. repository naming convention 때문에 다른 이름을 사용하면 Product Roadmap 또는 Task 문서에서 실제 위치를 link한다.

Implementation report에는 다음을 실제 수행 결과 기준으로 기록한다.

- Task 목적, 배경, 포함·제외 범위
- 전체 아키텍처와 Backend/Frontend/DB/Migration/API/UI·UX/권한/Workflow 영향
- Excel/PDF/첨부파일 영향과 기존 기능 회귀 영향
- 실제 변경 파일과 주요 파일의 역할
- 실행한 자동 테스트, UAT와 사용자 검수 결과
- 미실행 검증과 구체적인 미실행 이유
- 개인정보·secret 검토 결과
- known issue, 잔여 위험, 후속 Task와 운영 적용 전 checklist
- rollback, 복구 또는 forward-fix 방법
- 5종 종료 산출물의 상태와 위치
- 코드 구현 사실과 live UAT 검증 사실의 구분
- user validation checklist의 생성 여부와 사용자 완료 여부

변경 유형별 최소·영향·전체 테스트 선택은 [Validation Matrix](development/validation-matrix.md)를 따르고, Implementation report에는 이 매트릭스를 복사하지 않고 실제 적용 결과와 미실행 이유를 기록한다.

범위상 해당하지 않는 report 항목은 삭제하지 않고 `N/A`와 이유를 기록할 수 있다. 코드 전체 diff를 대형 Markdown 문서에 복사하지 않는다. 전체 코드는 repository와 Git diff를 source of truth로 두고 report는 구조, 결정, 위치와 검증 결과를 설명한다.

Migration이 포함된 Task는 migration 번호, additive/destructive 여부, rollback 또는 forward-fix 정책을 기록한다. 외부 알림 발송이나 데이터 변경이 포함된 검수는 사용자 승인 여부를 기록한다.

개발 블로그 소재를 추적할 수 있도록 다음 고정 섹션을 포함한다.

1. 해결한 업무 문제
2. 기술적 결정과 검토한 대안
3. 시행착오 및 폐기한 접근
4. 사용자 검수 결과와 남은 항목

미실행 검증을 실행 완료처럼 표현하지 않는다. 체크리스트가 미완료이면 그 상태를 명시한다.

## 4. 사용자 검수 상태

user validation checklist는 자동 검증과 사용자 직접 확인 항목을 분리하고 다음 상태 중 하나로 관리한다.

- `Checklist 작성됨`
- `자동 검증 완료`
- `사용자 검수 대기`
- `사용자 검수 완료`
- `사용자 검수 실패`
- `적용 대상 아님`

체크리스트가 존재한다는 이유만으로 사용자 검수 완료로 기록하지 않는다. 미체크 항목이 남은 checklist는 완료가 아니며, 사용자 검수 실패 또는 대기 상태를 성공으로 바꾸지 않는다. 사용자 검수 대기 중 PR이 필요하면 상태를 명시한 draft PR만 허용하고 Task 완료·merge는 별도 gate를 따른다.

## 5. Finding gate

Finding은 P0/P1/P2/P3로 관리하고 다음 gate를 적용한다.

- P0: 하나라도 미해결이면 Task 완료, 게시와 merge를 금지한다.
- P1: 하나라도 미해결이면 Task 완료, 게시와 merge를 금지한다.
- P2: 원칙적으로 수정한 뒤 진행한다. 예외적으로 수용하려면 사용자 승인, risk owner, 수용 근거, 영향, 완화책, 재검토 시점과 후속 Task ID를 모두 기록한다.
- P3: 후속 Task ID 또는 명시적인 backlog 항목에 연결하면 현재 Task 완료를 허용한다.

Finding을 수용하거나 후속으로 넘긴 사실은 implementation report와 Roadmap 또는 backlog에서 추적할 수 있어야 한다.

미검증 항목, 미실행 테스트와 확인하지 못한 운영 상태를 성공으로 표시하지 않는다.

## 6. 개인정보 및 공개 문서

Task 산출물과 공개 가능한 tracked 문서에는 다음 원문을 기록하지 않는다.

- 실제 사용자 이름, 회사 이메일/UPN, 사번, 전화번호, 개인 식별 가능한 계정명
- 실제 tenant/client/object id
- secret, token, password, webhook URL, Authorization header
- 인증서 private key
- 고객, 프로젝트 또는 조직의 민감 정보

사용자 검수 증빙에는 다음 정보만 기록한다.

- 역할명 또는 `검수 사용자 A/B`와 같은 일관된 익명 식별자
- 날짜
- 환경
- 결과
- 증빙 유형

placeholder domain(`example.com`, `example.test`, `example.invalid`), 명백한 테스트 사용자명과 기능 역할명은 사용할 수 있다. 필요한 경우 마스킹 값을 사용한다. 실제 값이 필요한 운영 절차는 문서에 값을 적지 않고 승인된 secret/env 저장 위치만 안내한다.

Git/GitHub, browser, API와 DB 검증 증빙의 허용 projection, output guard와 임시 artifact 기준은 [Privacy-safe Evidence](development/privacy-safe-evidence.md)를 따른다.

## 7. Task 종료 표준 절차

### Task 시작 instruction chain gate

모든 새 Task와 분리된 Codex session은 첫 변경·runtime mutation·Git mutation 전에 현재 filesystem의 Root 및 적용 경로 `AGENTS.md`, Product Roadmap, 이 정책, Validation Matrix, Privacy-safe Evidence와 해당 Task 산출물을 다시 읽는다. 이전 Task나 대화 기억에서 읽은 상태를 재사용하지 않는다.

읽은 뒤 `instructionChainRead=true`, `taskType`, branch/worktree 기준선과 적용되는 하위 지침을 먼저 보고한다. 새 session, branch/base 변경, instruction file 변경 또는 source-of-truth drift가 있으면 이 gate를 다시 수행한다. 읽을 수 없거나 의미 있는 충돌이 있으면 구현 전에 중단한다.

### 표준 종료 절차

1. Task 시작 전 instruction chain gate, branch, HEAD, working tree, remote와 기존 동일 목적 작업을 확인한다.
2. 조사·기획에서 범위, 제외 범위, 선행조건, 위험과 검수 기준을 확정한다.
3. 승인된 범위만 구현하고 Task 범위와 실제 변경 범위를 대조한다.
4. 관련 자동 테스트를 실행하고 결과 및 미실행 항목을 기록한다.
5. user validation checklist를 작성하고 자동 검증과 사용자 검수 상태를 분리한다.
6. Finding을 P0/P1/P2/P3로 분류하고 gate를 적용한다.
7. 5종 산출물의 경로와 상태를 확인한다.
8. Product Roadmap을 실제 구현에 맞게 갱신한다.
9. 문서 link와 개인정보/secret 포함 여부를 검사한다.
10. 명시적 allowlist로만 staging하고 cached file list를 재검증한다.
11. 사용자 승인 범위에서 commit, push와 PR을 수행한다.
12. CI 결과와 사용자 검수 상태를 확인한 뒤 승인된 경우에만 merge한다.
13. merge 후 branch 정리는 별도 승인과 보존 필요성을 확인해 수행한다.

자동 테스트의 공통 명령과 변경 유형별 선택 기준은 [Validation Matrix](development/validation-matrix.md)의 canonical 절차를 사용한다. 환경별 기동·handover·rollback 명령은 해당 Task SOP에 두며 이 종료 정책이나 Roadmap에 복사하지 않는다.

Roadmap 갱신 후보는 현재 구현 기능, 수정 방향, 향후 Task 상태, 추적 대상, Decision Log, 관련 용어와 작업 유의사항이다. 구현하지 않은 기능을 완료로 쓰지 않고 방향 변경은 Decision Log에 누적하며 기존 행은 삭제하지 않는다.

### 고정 10개 항목 완료 보고

Task를 완료·중단하거나 사용자 검수 handoff로 종료할 때 최종 응답은 먼저 다음 고정 필드의 `작업 현황 요약` 표를 표시한다.

- 현재 Task와 현재 단계
- 현재 Task에 남은 일
- Git 게시 상태: Commit·Push·PR·Merge 각각 `완료`, `미완료`, `승인 대기` 또는 `적용 없음`
- 중단·보류 Task의 Task ID, 중단 단계, 사유와 재개 조건
- 재개 우선순위
- 모든 활성·중단·보류 작업이 끝난 뒤 Product Roadmap 기준 다음 canonical Task와 `Next Gate`

중단·보류 Task가 없으면 `없음`으로 쓰되 다음 Roadmap Task는 생략하지 않는다. 요약 표 뒤에는 다음 제목과 순서를 고정한다.

1. 수정 요약
2. 수정한 파일
3. 실행한 테스트
4. 테스트 결과
5. Frontend URL
6. Backend URL
7. 수동 검수 체크리스트
8. 미커밋 변경사항
9. 남은 문제
10. 게시 가능 여부

모든 항목은 필수다. 적용 대상이 없으면 `N/A`와 구체적인 이유를 쓰고 생략하지 않는다. 미실행 검증은 테스트 성공으로 기록하지 않으며, URL은 실제 확인한 환경만 쓴다. 미커밋 변경사항에는 changed/staged와 Commit·Push·PR·Merge 각각의 상태, 남은 Git 작업과 필요한 승인을 기록한다. 남은 문제에는 현재 Task의 잔여 단계, 중단·보류 Task·재개 조건, Finding, 외부 blocker, 별도 승인 항목과 Roadmap 다음 Gate를 포함한다. 게시 가능 여부의 `GO`는 품질 gate 판정일 뿐 Git 게시 승인이 아니다.

Finding은 count만으로 축약하지 않는다. 각 Finding의 ID 또는 stable label, severity, 상태(`OPEN`, `RESOLVED`, `RISK_ACCEPTED`, `BACKLOG`), 원인·영향과 해소 또는 후속 위치를 남긴다. 해소된 Finding도 무엇이 발생했는지 추적 가능해야 한다. 현재 Task와 Git 게시가 끝난 경우에도 중단·보류 Task가 없다는 사실과 Product Roadmap 기준 다음 canonical Task·`Next Gate`를 명시한다.

고정 10개 항목은 대화 완료 보고를 일관되게 만드는 형식이며 Implementation report, SOP, User manual, Roadmap update와 user validation checklist를 대체하지 않는다.

## 8. Staging, 게시와 branch 정리

- staging은 Task allowlist의 개별 경로만 사용한다. `git add .`와 `git add -A`는 사용하지 않는다.
- stage 후 cached file list에 runtime, dependency, migration, `.env`, 인증서, secret 또는 삭제 파일이 섞이지 않았는지 확인한다.
- Commit, push, PR, merge는 `AGENTS.md`와 사용자의 명시적 승인 범위를 따른다.
- 사용자 검수 대기 상태는 draft PR에 명시한다. 사용자 검수 완료로 가장하지 않는다.
- CI 실패, allowlist 위반, 개인정보/secret 잔존 또는 Finding gate 위반 시 게시·merge를 중단한다.
- 기존 또는 대체된 branch는 새 정책 PR이 merge됐다는 이유만으로 자동 삭제하지 않는다. 별도 승인 후 local/remote 보존 필요성을 확인한다.

## 9. 예외와 N/A

Task 규모가 작거나 문서 전용이어도 5종 산출물 상태는 기록한다. 독립 SOP/User manual이 필요하지 않으면 포함된 section을 canonical 위치로 지정하거나, 정말 적용 대상이 없을 때만 구체적 이유와 함께 `N/A`로 기록한다.

정책을 적용할 수 없는 충돌, 실제 secret/개인정보, 범위 밖 runtime 변경 또는 깨진 문서 구조가 발견되면 임의로 예외 처리하지 않고 작업을 중단해 보고한다.
