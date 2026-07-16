# TASK-007A — Pending List 실험 기획 Codex 내용 Review

- reviewOwner: `CODEX`
- reviewSource: `tasks/007a-planning.md`
- reviewStatus: `RESOLVED_FOR_EXPERIMENT_IMPLEMENTATION`
- canonicalMainApproval: false

## 결론

Pending을 후속 자재·검사·제조보다 먼저 공통 vertical slice로 만드는 방향은 Product Roadmap과 사용자 흐름 기준선에 맞다. 전용 workspace와 프로젝트 deep link를 함께 두고, 상태·담당·코멘트·감사·내 업무·인앱 알림까지 한 transaction 경계로 연결해야 독립적인 사용자 가치가 생긴다.

## 유지

- 5단계 forward-only 상태 모델: 책임과 재검사 handoff를 단순 open/close보다 명확히 표현한다.
- 전용 `/pending` workspace: 생산관리의 전체 병목 관리에 필요하다.
- 프로젝트 deep link: 현장 사용자가 원래 업무 맥락으로 돌아갈 수 있다.
- 담당자 배정과 내 업무·인앱 알림 연결: 등록만 되는 고립된 CRUD를 피한다.
- append-only comment/history와 optimistic version: 감사와 경쟁 mutation 안전의 최소선이다.

## 추가

- 생성 시 담당자 유무로 `Registered`/`ActionRequested`를 결정해 빈 담당 상태의 의미를 명확히 한다.
- System Administrator의 업무 mutation 우회를 금지하고 감사 조회만 허용한다.
- 목록에서 긴급·기한 초과를 시각적으로 우선하고 390px에서는 한 열 카드로 전환한다.
- 첨부 보류 이유와 다음 gate를 UI와 report에 명시해 누락처럼 보이지 않게 한다.

## 보류

- 프로젝트 상세 안의 Pending tab: 전용 workspace와 deep link를 먼저 검증한 뒤 중복 navigation 필요성을 판단한다.
- 평균 체류시간·부서 병목 분석: 상태 이벤트가 쌓인 뒤 `TASK-007B`에서 구현해야 지표가 의미 있다.
- 유형 관리자 편집: 초기 고정 유형 사용성을 확인한 뒤 `TASK-ADMIN-002`와 함께 검토한다.

## 제거

- binary 첨부: storage, 파일형식·크기, 악성파일 검사, 권한, 보존·backup·restore가 미정이라 이번 실험에서 구현하면 거짓 안전 계약이 된다.
- 실제 Teams/Mail 발송: 인앱 원본 검증과 별개 운영 승인 대상이다.
- unrestricted role mutation: 생성자·담당자·생산관리 책임 경계를 훼손한다.
- 상태 되돌리기와 hard delete: append-only 감사·forward workflow 원칙과 충돌한다.

## 우선 구현 순서

1. additive schema와 permission seed
2. Backend domain validation·transaction·API·authorization
3. Frontend route·list/create/detail·responsive UX
4. Backend/Frontend tests와 isolated full-stack flow
5. synthetic screenshot과 implementation report

## Finding과 resolution

| ID | Severity | 상태 | 내용 | Resolution |
| --- | --- | --- | --- | --- |
| `007A-FABLE-OUTPUT` | P3 | `BACKLOG` | Fable planning이 contract-invalid로 artifact를 생성하지 못함 | 실험은 Codex fallback으로 진행, canonical 채택 전 Fable/사용자 gate 재수행 |
| `007A-ATTACHMENT-POLICY` | P2 | `RESOLVED` | 첨부 security/storage 계약 미정 | binary 첨부를 범위에서 제거하고 text-first MVP로 고정 |
| `007A-ADMIN-OVERRIDE` | P2 | `RESOLVED` | 관리자 업무 mutation은 책임·감사 경계를 우회할 수 있음 | 관리자에는 Pending.Read만 부여 |
| `007A-PROJECT-TAB` | P3 | `BACKLOG` | workspace와 프로젝트 tab 중복 가능성 | deep link 우선, 실제 사용 후 tab 필요성 재평가 |

## 구현 판정

실험 worktree 구현은 `GO`다. 이 판정은 대표 repo, commit, push, PR 또는 merge 승인이 아니다.
