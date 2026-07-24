# TASK-UL891-SET-001 Change 004 — 프로젝트 상세 패널 진척률·부서 KPI

## Task Identity Gate

- proposedTaskId: `TASK-UL891-SET-001`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-UL891-SET-001`
- roadmapNextGate: `USER_VALIDATION_BATCHED_FINAL`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UL891-SET-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 프로젝트 상세 제조·품질·물류 탭을 패널별 실제 완료 단위와 프로젝트 KPI를 함께 보는 운영 현황판으로 보완한다.
- Root Finding 또는 정책 결정: 현재 표가 단계·상태 중심이라 패널별/프로젝트 전체의 실제 완료율을 비교하기 어렵다.
- 변경·검증 경계: 프로젝트 상세 read model에 활성 제조·OQC 양식 단계 수를 노출하고, 기존 부서 API 결과를 합성해 패널 진척률과 KPI를 계산한다.
- 보존할 불변조건: 패널 상세 deep link, 담당자 mutation workspace, 동적 양식 버전, 상태 색 외 흑백 사각형 디자인과 서버 권한을 유지한다.
- 예상 산출물: 네 열 패널 목록, 숫자+막대 진척률, 부서별 KPI, desktop/mobile 검증.

## 1. 제조

- 표 헤더는 `No · 패널명 · 핵심정보 · 진행률`로 고정한다.
- 시작한 패널은 실행에 고정된 제조 단계 수와 체크 완료 수를 사용한다.
- 미착수 패널은 현재 활성 제조 양식의 단계 수를 분모로 사용한다.
- KPI는 `착수 대기 · 제조 중 · 중단 · 완료 · 진행률`이며 앞 네 값은 `완료 면수/전체 면수` 형식이다.
- 착수 대기는 활성 패널 전체에서 제조 중·중단·완료를 제외한 모든 패널이다.

## 2. 품질

- 표 헤더는 `No · 패널명 · 핵심정보 · 진행률`로 고정한다.
- 패널 진척 단위는 `OQC Check 항목 수 + 전진검수 1 + 선택 FAT 1`이다.
- LQC는 진척률 분모에서 제외하고 별도 완료 KPI로만 표시한다.
- KPI는 `LQC 완료 · OQC 완료 · 전진검수 완료 · FAT 완료 · 진행률`이다.
- FAT 비필수 프로젝트의 FAT KPI 값은 `없음`으로 표시한다.

## 3. 물류

- 표 헤더는 `No · 패널명 · 핵심정보 · 진행률`로 고정한다.
- 패널당 `포장 · 출발 · 납품` 세 단계를 각각 1로 계산한다.
- KPI는 `포장 완료 · 출발 완료 · 납품 완료 · 진행률`이며 완료 수는 패널 단위 중복 제거 집계다.

## 4. 공통 UI·검증

- 진행률은 반올림한 숫자 `%`와 접근 가능한 가로 채움 막대를 함께 표시한다.
- 상태 의미는 기존 tone 색을 사용하고 나머지 표·카드·막대 배경은 흑백 사각형 토큰을 유지한다.
- desktop은 네 열 표, 390px는 패널명·핵심정보·진척률에 집중한 전용 카드로 표시한다.
- 패널 행/카드를 누르면 기존과 동일하게 패널 상세의 해당 부서 탭으로 이동한다.
- Backend project detail contract test, Frontend unit 전체, typecheck, lint, build, 영향 E2E와 고정 runtime desktop/mobile을 검증한다.

## 5. 제외 범위

- 제조·품질·물류 상태 전이와 mutation 권한 변경
- 전진검수·FAT 통합 판정 입력 모델 구현 — `TASK-012A Change 003`의 OPEN P2로 추적
- 대표 repo·`main`·Persistent UAT·push·PR·merge
