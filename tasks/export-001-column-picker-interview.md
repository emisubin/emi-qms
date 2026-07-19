# TASK-EXPORT-001 Change 003 — 선택 Excel 컬럼 선택 Fast-track Interview

- taskType: `NEW_FEATURE`
- canonicalTaskId: `TASK-EXPORT-001`
- canonicalChangeId: `change-003`
- interviewOwner: `FABLE_5_EXPERIMENT_FAST_TRACK`
- orchestrationOwner: `CODEX`
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 완료된 `TASK-EXPORT-001 Change 002`의 20개 화면 선택 Excel 내보내기를 보존하면서 사용자가 내보낼 컬럼을 선택하는 optional 후속 기능의 Fable planning source of truth다. 사용자는 현재 experiment 대화에서 “다음 작업”을 바로 이어가도록 명시했고, 실험 브랜치의 신규 기능은 사용자-facing interview와 중간 승인을 생략해 Fable 권장안을 자동 채택하도록 지시했다. 대표 repo·`main`·origin·Persistent UAT·실제 provider·push·PR·merge는 변경하지 않는다.

## 1. Task Identity Gate

- proposedTaskId: `TASK-EXPORT-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-EXPORT-001`
- roadmapNextGate: `OPTIONAL_COLUMN_PICKER_USER_REQUEST`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-EXPORT-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 사용자가 20개 선택 export 화면에서 서버가 허용한 컬럼 중 필요한 컬럼만 고른 뒤 선택한 행을 단일 `.xlsx`로 내보낸다.
- Root Finding 또는 정책 결정: 현재 선택 export는 화면별 고정 컬럼만 제공해 불필요한 컬럼을 파일에서 수동 삭제해야 한다. 자유 입력이나 client-only column 제어는 민감 필드·권한 우회와 빈/불완전 workbook 위험을 만든다.
- 변경·검증 경계: 기존 선택 tray, 20개 화면 registry, selected export POST contract, explicit server column allowlist, synthetic desktop screenshot과 workbook inspection을 포함한다.
- 보존할 불변조건: 기존 checkbox 전체선택·단일 export action, 선택 ID 전부-or-전무 scope 재검증, 민감정보·내부 ID 제외, formula-safe text, bounded resource, 기존 audit·import·domain workflow 불변, mobile simple-mode에서 bulk export 기본 제외.
- 예상 산출물: Fable 1차 planning 원문, Codex review, review 기반 Fable 2차 planning 원문, 구현·자동 검증·desktop/mobile evidence·실제 workbook 검사·implementation report·local experiment commit.

### 검색 범위

- [x] `tasks/`의 TASK-EXPORT-001 본체·Change 002 planning/review/change/report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] 실험 완료 원장과 optional next priority
- [x] local/remote export branch와 worktree branch projection
- [x] 기존 PR 기록 — 현재 local source에 column picker 구현·planning 없음

같은 목적은 canonical `TASK-EXPORT-001`의 명시된 optional column picker 한 건이다. 새 Task ID를 만들지 않고 Change 003으로 재사용한다. Fable runner artifact stem만 기존 Change 002의 `export-001-all-pages` 관례에 따라 `export-001-column-picker`를 사용한다.

## 2. 확인된 사용자·제품 계약

- 사용자는 화면에서 선택한 row/card만 내보내며 전체 export 버튼은 다시 만들지 않는다.
- header checkbox의 현재 목록 전체선택과 단일 `선택 Excel 내보내기` action을 보존한다.
- 모바일은 PC 기능을 전부 복제하지 않는다. Change 004의 mobile simple-mode대로 Excel·대량 선택은 기본 화면에서 제외하고 desktop에서 관리한다.
- 컬럼 선택은 export의 편의 기능이며 업무 데이터·권한·상태를 변경하지 않는다.
- 비차단 제품 선택은 Fable이 Repository 근거와 trade-off를 적고 권장안을 확정한다.
- 권한·민감정보·내부 ID·공식 workbook 안전성 충돌은 fast-track으로 우회하지 않는다.

## 3. 현재 Repository 기준선

- 업무 12개·관리자 8개, 총 20개 화면이 `SelectedExportScreenRegistry`의 stable screen key·selection key·explicit workbook columns를 사용한다.
- Frontend `SelectedExportTray`가 선택 수·전체선택·단일 export action·feedback을 공통 제공한다.
- `POST /api/data-exports/selected`는 screen·selectedIds·filter를 받고, Backend가 permission·scope·현재 존재 여부를 다시 확인한다.
- 최대 선택 1,000건, workbook 10,000행 cap, 2-slot concurrency fence, formula-safe text와 append-only audit가 적용돼 있다.
- 기존 API와 workbook은 서버가 컬럼을 고정하며 client가 column key를 보내지 않는다.
- 모바일에서는 선택 tray·checkbox·Excel action을 숨기고 desktop 관리 기능을 유지한다.

## 4. Fable이 권장안으로 확정할 비차단 항목

1. 진입 UX: tray 안 `컬럼 선택` popover/dialog, 기본 컬럼 요약과 export action의 관계.
2. 기본값·lifecycle: 화면별 기본 컬럼, 열기/닫기·route/filter 변경·export 완료 뒤 선택 유지, 브라우저 재접속 persistence 필요성.
3. 필수 컬럼: 행 식별·업무 이해에 반드시 필요한 1개 이상 컬럼을 사용자가 끌 수 있는지.
4. 권한·민감 컬럼: 서버가 현재 사용자에게 허용한 컬럼 metadata만 제공하고 요청 시 다시 검증하는 contract.
5. 요청·오류: 빈 선택, 미지원/중복/권한 상실 column key, stale client와 서버 버전 차이의 fail-closed 처리.
6. 접근성·밀도: keyboard focus, 전체 선택/기본값 복원, selected count, 좁은 desktop pane과 390px mobile evidence.
7. audit: 실제 선택 컬럼 원문 대신 bounded count 또는 allowlisted key projection을 기록할지.
8. rollout: 20개 screen 동시 적용과 대표 workbook/screenshot 조합, 기존 client가 columns 없이 호출할 때의 호환성.

## 5. 안전상 blocking 경계

- Client가 임의 header·accessor·cell value를 전달하거나 서버 allowlist 밖 column을 요청하는 방식
- 권한 없는 민감 컬럼, 내부 GUID, 자유서술·첨부 bytes, secret·개인 식별 원문 노출
- 필수 식별 컬럼 0개 또는 선택 컬럼 0개인 불완전 workbook 성공 처리
- 일부 column만 조용히 무시하고 성공하는 partial contract
- 기존 선택 ID scope 검증·formula 방어·resource fence·audit·mobile simple-mode 약화
- 기존 migration 수정, Persistent UAT·대표 repo·`main`·push·PR·merge

현재 Repository에서 blocking 충돌은 발견되지 않았으며 open blocking decision은 0이다.

## 6. 성공 기준

- Desktop 20개 대상 화면이 공통 컬럼 선택 UX를 사용하고 현재 screen에서 허용된 컬럼만 표시한다.
- 기존 client의 columns 미전달은 화면별 안전한 기본 컬럼으로 호환된다.
- 서버는 요청 column key의 allowlist·권한·중복·최소 개수를 검증하고 위반 시 file·success audit 0으로 차단한다.
- workbook header와 셀은 선택한 허용 컬럼만 포함하고 선택 row·formula-safe·scope 계약은 유지된다.
- mobile 390px에서는 bulk export가 계속 기본 제외되며 page-level overflow 0이다.
- Backend·Frontend 최소/영향 검증, isolated Full-Stack E2E, 대표 desktop UI·실제 workbook screenshot을 완료한다.
- 사용자 검수는 마지막 일괄 대기로 남기고 local experiment commit까지만 수행한다.

## 7. 사용자 확인

- [x] 사용자가 실험 브랜치에서 “다음 작업”을 바로 진행하도록 요청했다.
- [x] 실험 신규 기능의 interview·중간 승인을 생략하고 Fable 권장안을 자동 채택한다.
- [x] 완료된 선택 export는 재구현하지 않고 optional 컬럼 선택 Change 003만 시작한다.
- [x] 대표 repo·main·Persistent UAT·provider·게시를 제외한다.
- [x] blocking decision 0인 경우 Fable 1차 planning을 시작한다.

확인 source: 현재 대화의 experiment fast-track standing instruction과 2026-07-19 “그리고 다음 작업 이어서 진행해” 요청, Product Roadmap·실험 완료 원장의 다음 optional Gate.
