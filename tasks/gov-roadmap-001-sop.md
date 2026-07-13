# TASK-GOV-ROADMAP-001 SOP — Roadmap 상태 동기화

## 1. 목적

Roadmap을 실제 main, merged PR과 canonical Task 산출물에 맞춰 갱신하고, 구현 완료·controlled UAT·정책 결정·미승인 미래 계획을 혼동하지 않도록 한다.

## 2. 동기화 절차

1. `main`, `origin/main`, worktree clean 상태와 동일 목적 branch/PR 존재 여부를 확인한다.
2. Root·하위 `AGENTS.md`, 종료 정책, validation matrix와 privacy-safe evidence를 읽는다.
3. merged PR diff와 Task·Implementation report를 대조해 완료 상태를 확인한다.
4. planning 파일, review와 사용자 승인 문구를 각각 확인한다. 파일이 없거나 승인이 없으면 승인 상태를 추정하지 않는다.
5. 21~23장에는 현재 상태, 남은 Gate와 dependency 중심 실행 큐를 반영한다.
6. 24장의 기존 번호와 의미를 유지한다. 신규 추적 항목은 마지막 번호 다음에 추가한다.
7. 25장의 기존 Decision Log 행은 수정·삭제하지 않고 실제 결정만 새 행으로 추가한다.
8. 알림 채널, 인증, 삭제·복구와 운영 정책은 별도 승인 없이 변경하지 않는다.
9. 명시적 allowlist로 문서만 stage하고 자동 검증 뒤 Draft PR로 게시한다.
10. 사용자 검수 전 Ready 전환과 merge를 수행하지 않는다.

## 3. 상태 판정 규칙

- `Completed`: 구현·필수 UAT와 merge가 완료됨
- `Controlled UAT Pending`: 코드는 merge됐지만 운영성 runtime 적용 Gate가 남음
- `P2 Blocked`: 선행 P2 Finding 또는 사용자 Go 승인 전 신규 기능
- `Scope Review Required`: 실제 범위·정책·P2 여부를 조사해야 함
- `Planning Draft` / `Planning Approved`: canonical planning 파일과 승인 증빙이 있을 때만 사용
- `Implementation Ready`: planning·review·사용자 구현 승인이 모두 확인됐을 때만 사용
- `External Decision`: repository owner, 보안, 운영이나 provider 등 외부 결정 필요
- `Deferred`: 선행 data model 또는 운영 조건 뒤로 연기

## 4. 변경 금지

- 기존 Decision Log 삭제·수정
- 기존 추적 번호 의미 변경·재사용
- planning 파일 존재만으로 planning 승인 표시
- 고정 날짜를 확정 약속으로 표현
- 공용 태블릿·공용 기기 session 정책을 승인 없이 추가
- 알림 채널 matrix를 문서 동기화만으로 변경
- docs Task에서 제품 코드·runtime·Persistent UAT 변경

## 5. 검증

- changed-file allowlist와 삭제 파일 0
- 실제 Task·PR 상태와 Roadmap 대조
- planning·implementation 승인 상태 확인
- Decision Log 기존 행 보존과 추적 번호 유일성
- Markdown local link·anchor·heading
- `git diff --check`
- secret/PII scan
- Backend·Frontend·migration·dependency·script diff 0

## 6. 이상 발생 시

실제 merge 상태, 사용자 결정 또는 canonical 정책과 충돌하면 Roadmap을 추정으로 갱신하지 않는다. 충돌 위치와 영향을 보고하고 사용자 결정을 기다린다. Runtime·DB rollback이나 history rewrite를 수행하지 않는다.
