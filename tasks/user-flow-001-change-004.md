# TASK-USER-FLOW-001 Change 004 — 개인 유저플로우 전문 Fable 재작성

## 1. 사용자 승인과 목적

사용자는 Governance 정책의 독립 검증·merge가 끝난 뒤 기존 `TASK-USER-FLOW-001` 문서를 최신 `main`에서 Fable 5가 전문 전체로 다시 작성하고, Codex 내용 review·독립 검증·별도 게시·merge까지 진행하도록 명시 승인했다.

- fableRedraftApproved: true
- fableRedraftSource: `USER_EXPLICIT_REQUEST`
- fableRedraftTarget: `docs/13-user-flow-baseline.md`
- publishingApproved: true
- pushApproved: true
- prApproved: true
- mergeApproved: true
- productImplementationApproved: false
- phaseBDocumentAlignmentApproved: false

이 승인은 이 Change와 exact target에 대한 Fable redraft 1회 및 검증된 문서 게시에만 사용한다. 제품 기능 구현, Roadmap의 후속 기능 순서 자동 변경, `docs/02-business-flow.md`·`docs/04-permission-matrix.md` 정렬, runtime·DB·provider 변경으로 확대하지 않는다.

## 2. Fable 재작성 방향

Fable은 기존 원문을 부분 수정하지 않고 완전한 대체 Markdown 전문을 한 번 출력한다. 다음 사용자 결정과 Codex 내용 review를 함께 반영하되, 새 사용자 정책을 만들지 않는다.

### 유지

- 사용자가 다음 개발 대상을 판단할 수 있는 개인 개발 의사결정 지도
- 현재 구현·부분 구현·미구현의 구분
- EMI의 18단계 업무 흐름과 13개 역할·검사 여정 coverage
- 내 업무·알림 deep link·메뉴 직접 진입의 공통 진입 패턴
- 공통 예외·복구 흐름과 후속 Task 매핑
- `Pending → 병목 집계 → 자재 도착 → IQC → 키팅 → 제조 handoff`를 우선 검토할 end-to-end 제품 slice라는 권고
- 기존 권한·알림·Backend authoritative 계약과 미구현 기능의 별도 planning·승인 Gate

### 변경

- 이 문서의 지위를 canonical 제품 계약이나 사용자 온보딩·교육 문서가 아닌 사용자 개인 개발 판단용 참고 자료로 명확히 한다.
- 충돌 시 Product Roadmap, 승인된 최신 Task 계약과 실제 Backend 정책이 우선임을 명시한다.
- 모든 후속 Task의 의무 갱신 규칙을 제거한다. 18단계·담당 역할·완료 조건, 최상위 내비게이션, 공통 진입·handoff, 핵심 업무 단위가 바뀌는 중요한 사건에서만 이 자료의 재검토를 권고한다.
- Pending List의 내 업무·프로젝트 하위 위치는 확정사항이 아니라 contextual 진입과 전용 관리 workspace를 함께 검토할 `TASK-007A` 후보로 표시한다.
- 프로젝트·구매품목·패널·검사·포장 단위가 병렬로 진행되고 일부 단위의 차단이 전체 완료에 어떤 영향을 주는지 판단할 dependency map을 포함한다.
- 미완료 업무 재배정, 완료 기록 정정·재개, 초안 저장·부분 실패·재시도, 첨부 storage 계약과 제품 성공 신호를 후속 planning에서 확인할 공통 질문 또는 권고로 구분해 추가한다.
- 다음 기능 선택에 바로 쓸 수 있도록 Now/Next/Later와 최소 vertical slice, 선행조건, 성공 신호를 중심으로 정리한다.

### 제거·보류

- `docs/13`의 canonical 선언
- 현재 단계에서의 사용자 온보딩·교육 문서 역할
- `docs/02-business-flow.md`·`docs/04-permission-matrix.md` Phase B 자동 정렬
- 모든 후속 Task가 이 문서를 반드시 함께 갱신해야 한다는 완료 Gate
- 아직 검증되지 않은 “부서 업무” 묶음과 Pending List 메뉴 위치의 조기 확정
- Home·전체 모바일 묶음·알림 preference·전면 디자인 시스템을 핵심 업무 slice보다 앞선 확정 순서로 표현하는 것

## 3. 승인·권고·미승인 표기

문서 안의 각 주요 판단은 다음 세 범주가 혼동되지 않게 표현한다.

- `확정/현재 계약`: Roadmap·Decision Log·실제 구현 또는 사용자 확인으로 이미 정해진 사실
- `권고/후보`: Codex 내용 review가 제품 가치와 의존성을 근거로 제안한 방향. 후속 Task 순서·정책 승인이 아님
- `미확정/후속 위임`: 개별 NEW_FEATURE interview·planning과 사용자 승인이 필요한 항목

특히 `Pending → 병목 집계 → 자재 도착 → IQC → 키팅 → 제조 handoff`는 이번 문서의 권고안이며 실제 Roadmap 순서 변경이나 기능 구현 승인으로 쓰지 않는다.

## 4. 작성·검증 불변조건

- 첫 H1과 기존 USER-FLOW metadata 계약을 유지한다.
- Fable stdout과 target 파일은 byte-for-byte 동일해야 하며 Codex는 `docs/13-user-flow-baseline.md`를 patch하지 않는다.
- 개인정보·secret·절대 경로·raw 증빙을 포함하지 않는다.
- Frontend·Backend·API·DB·migration·dependency·runtime·provider를 변경하지 않는다.
- 5174·5176·5081·5092·5190·5432와 Persistent UAT 자원을 재시작하거나 변경하지 않는다.
- Fable redraft 뒤 Codex는 원문과 분리된 새 내용 review를 작성하고, 분리된 Codex 검증 session이 diff·내용·Finding·게시 Gate를 read-only로 확인한다.

## 5. 완료 Gate

1. Fable 전문 redraft 1회 성공과 direct-write byte equality
2. Codex 내용·제품 방향 review 완료
3. 문서 link·heading·Mermaid·privacy·allowlist와 제품 source diff 0 검증
4. 독립 Codex 검증에서 Open P0/P1/P2 0
5. 사용자 승인 범위의 별도 commit·push·Ready PR·CI·merge
6. 제품 구현·Phase B 미승인 상태와 다음 `TASK-007A` Gate를 Roadmap·implementation report에 명시
