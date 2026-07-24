# TASK-UL891-SET-001 Change 003 — 프로젝트 상세 부서 탭 재구성

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
- experimentLedgerSelectedTask: `TASK-UL891-SET-001`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 프로젝트 상세를 프로젝트 공통 정보와 패널 단위 실행 현황의 읽기 허브로 재구성한다.
- Root Finding 또는 정책 결정: 영업 기본정보와 자재 상세가 중복되고, 제조·품질·물류는 패널별 현재 상태와 패널 상세 진입이 충분히 드러나지 않는다.
- 변경·검증 경계: 기존 프로젝트·구매·자재·제조·품질·물류 API의 조회 projection과 route만 재구성한다.
- 보존할 불변조건: 프로젝트 기본정보, 구매 입력, 자재 입고·키팅 업무, 패널별 제조·검사·물류 원자성, 기존 담당자 mutation 화면과 서버 권한을 유지한다.
- 예상 산출물: 프로젝트 상세 탭 재배치, 구매 입고확정 요약, 패널별 제조·품질·물류 목록과 정확한 패널 상세 탭 deep link, 자동 검증과 사용자 검수 항목.

### 검색 범위

- [x] `tasks/`의 UL891 planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log와 실험 완료 원장
- [x] Local/remote branch와 worktree
- [x] Open/merged PR — 같은 목적 PR 0건

## 1. 사용자 확정사항

1. 영업 탭의 중복 프로젝트 기본정보를 제거한다.
2. 영업 탭은 가장 마지막에 배치하고 현재 로그인한 사용자의 유효 부서가 영업팀일 때만 표시한다.
3. 자재 탭을 프로젝트 상세에서 제거한다. 자재 입고·키팅 업무 화면과 패널 상세의 자재·키팅 projection은 유지한다.
4. 구매 탭에는 구매품목별 입고 확정 여부만 간단히 표시한다. IQC·Pending 상세는 노출하지 않는다.
5. 제조·품질·물류 탭은 활성 패널 전체를 한 행/카드씩 표시하고 현재 상태를 한눈에 보여 준다.
6. 제조·품질·물류의 패널 행을 누르면 같은 패널 상세의 해당 탭이 선택된 상태로 열린다.

## 2. 구현 계약

### 2.1 탭 순서와 노출

- 공통 탭 순서: `전체 흐름 → 생산관리 → 설계 → 구매 → 제조 → 품질 → 물류`
- 영업 사용자만 마지막에 `영업` 탭을 본다.
- 이전 `?section=materials` 링크는 깨뜨리지 않고 구매 탭으로 안전하게 정규화한다.
- 영업 권한이 없는 사용자가 `?section=sales`로 직접 접근하면 전체 흐름으로 정규화한다.

### 2.2 구매

- 도급·사급 구분과 구매품목 기본 정보는 유지한다.
- 프로젝트 상세에서는 입고 상태를 `입고 확정` 또는 `미확정`으로만 표시한다.
- 자재 도착 회차, IQC 판정, Pending과 키팅 상세는 구매 탭에 복제하지 않는다.

### 2.3 제조·품질·물류

- 설계 패널 목록과 같은 정보 밀도로 `No · 패널명 · 현재 단계 · 상태 · 핵심 세부정보`를 표시한다.
- 제조는 착수 대기·제조 중·중단·완료와 체크 진행률을 표시한다.
- 품질은 IQC를 제외한 패널 검사 중 가장 최근/차단 우선 단계를 표시한다.
- 물류는 포장·출발·납품 중 가장 최근/차단 우선 단계를 표시한다.
- 아직 해당 부서 기록이 없는 활성 패널도 누락하지 않고 `미시작`으로 표시한다.
- 상태 의미만 기존 StatusBadge 색을 사용하고 나머지는 흑백 사각형 구조를 유지한다.

## 3. 제외 범위

- Backend API, DB, migration, workflow 상태 전이, 알림, 권한 확대
- 자재 독립 업무 메뉴·입고 화면·키팅 화면 삭제
- 패널 상세의 `자재·키팅` 탭 삭제 또는 mutation form 복제
- 대표 repo·`main`·Persistent UAT·push·PR·merge

## 4. 검증

- Frontend typecheck, lint, unit 전체, build
- 영업 사용자: 탭 순서, 영업 기본정보 제거, 정산 데이터 유지
- 비영업 사용자: 영업·자재 탭 미노출, 직접 query 정규화
- 구매: 품목별 입고 확정/미확정 표시
- 제조·품질·물류: 활성 패널 전체 표시, 상태 집계, 행 클릭 시 `?tab=manufacturing|quality|logistics`
- desktop 및 390px에서 page-level horizontal overflow 없음
