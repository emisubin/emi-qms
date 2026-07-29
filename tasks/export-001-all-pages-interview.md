# TASK-EXPORT-001 Change 002 — 전 페이지 선택 Excel 내보내기 fast-track 기준선

- taskType: `NEW_FEATURE`
- canonicalTaskId: `TASK-EXPORT-001`
- canonicalChangeId: `change-002`
- fableSessionKey: `TASK-EXPORT-001-ALL-PAGES`
- interviewOwner: `FABLE_5_EXPERIMENT_FAST_TRACK`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 `TASK-EXPORT-001`의 Phase 2를 전 페이지 선택형 Excel 내보내기로 확장하는 Change 002의 Fable planning source of truth다. 사용자는 처음 품질 페이지 Excel screenshot을 요청한 뒤 범위를 다음과 같이 확대했다.

> 다른페이지들 전부 엑셀 내보내기 기능이 있는지 확인하고 모두 구현하라. (선택 내보내기 포함)
>
> 전체 내보내기 버튼은 삭제하고 선택 내보내기 버튼만 구현하고, 체크박스 전체선택을 구현해서 사용자가 같은 의미의 버튼을 두개를 보지않도록 한다.

사용자의 standing experiment 규칙에 따라 사용자-facing interview와 중간 승인을 생략하고, 비차단 선택은 Fable 권장안을 자동 채택해 `Fable 1차 기획 → Codex review → Fable 2차 기획 → 구현·검증·페이지 screenshot·실제 Microsoft Excel screenshot·workbook close·local commit`까지 이어간다. 대표 repo, `main`, origin, Persistent UAT, 실제 provider와 canonical runtime은 변경하지 않는다. 실제 사용자·고객·프로젝트 원문은 사용하지 않고 isolated synthetic data만 사용한다.

## Task Identity Gate

- proposedTaskId: `TASK-EXPORT-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-EXPORT-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 반복 가능한 업무 데이터가 있는 모든 사용자-facing page에서 사용자가 row/card를 선택하고, header 전체선택과 단일 `선택 Excel 내보내기` action으로 선택한 항목만 안전한 `.xlsx`로 생성한다.
- Root Finding 또는 정책 결정: 현재 공통 Excel 기반은 프로젝트·구매 dashboard·내 업무의 현재 조건 전체 export와 프로젝트 선택 export만 제공한다. 이 때문에 프로젝트 page에는 의미가 겹치는 전체/선택 action 두 개가 보이고, 다른 주요 page에는 Excel action이 없다.
- 변경·검증 경계: 전 페이지 inventory, 공통 선택 UX·request snapshot·stable ID validation, 화면별 explicit allowlist adapter, 기존 workbook·formula·resource fence·append-only audit 재사용, isolated synthetic tests와 대표 page/workbook screenshot을 포함한다.
- 보존할 불변조건: Backend read permission·scope authoritative, 선택 전부-or-전무, 선택하지 않은 row 0, 내부 GUID·secret·자유서술/첨부 원문 미출력, formula-safe text, bounded selection·row·resource, import·domain mutation 불변, 실제 data·main·Persistent UAT 불변.
- 예상 산출물: 전 페이지 inventory와 inclusion matrix, Fable 1차 planning 원문, Codex 내용 review, Fable 2차 planning 원문, 모든 대상 page의 선택 UX·Excel adapter·자동 검증·desktop/390px screenshot·실제 Excel screenshot·workbook close 확인·implementation report·local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

같은 목적은 Roadmap과 `TASK-EXPORT-001`의 “모든 주요 페이지 Excel 출력” 한 건뿐이다. 새 Task를 만들지 않고 `change-002`를 재사용한다. `TASK-EXPORT-002`는 프로젝트 subset 선택 UX의 선행 구현이며 이번 변경이 일반화해 흡수하되 판단 이력과 기존 commit은 보존한다. 동일 목적의 별도 branch·worktree·open/merged PR은 0건이다.

## 사용자 실행 지시

- 사용자 요청일: 2026-07-18
- action 정책: page마다 의미가 겹치는 전체 export와 선택 export를 함께 두지 않는다. 기존 GET 전체 export button은 UI에서 제거하고, checkbox header 전체선택을 포함한 단일 선택 export만 노출한다.
- 전체선택 의미: 현재 page가 사용자가 실제로 보고 있는 loaded/filtered/selectable row 전체를 선택하는 동작으로 기획하되, pagination·group/stage가 있는 page의 정확한 경계는 Fable 권장안으로 확정한다.
- selection 정책: 0건 disabled, 일부 선택 indeterminate, 실행 시 ID snapshot, 진행 중 selection lock, 실패·성공 뒤 선택 유지 또는 해제 정책은 공통 계약으로 정한다.
- page 정책: 로그인·인증·create/edit form·단일 detail처럼 반복 선택 대상이 없는 route를 “미구현”으로 가장하지 않는다. Fable은 실제 route/component/data model을 읽어 `선택 export 대상`, `상위 page에서 포함`, `선택 대상 없음`으로 전수 분류하고 누락 0을 검증한다.
- 증빙: 모든 대상 page의 자동 contract를 검증하고, 대표적인 desktop·390px 화면과 실제 생성 workbook을 Microsoft Excel에서 확인해 screenshot을 남긴 뒤 workbook을 닫는다.
- 게시 경계: local experiment commit까지만 승인. push·PR·merge 미승인, main merge 승인 `0/3`.

## 확인된 Repository 기준선

- `TASK-EXPORT-001` 공통 기반: explicit allowlist, formula-safe text, 10,000행 cap, 2-slot no-wait gate, append-only `data_export_events` audit, 프로젝트·구매 dashboard·내 업무 GET export.
- `TASK-EXPORT-002`: 프로젝트 visible row checkbox·header 전체선택·indeterminate·선택 snapshot·`POST /api/projects/export/selected`·전부-or-전무 scope 검증·`ProjectsSelected` audit kind.
- 현재 프로젝트 page는 기존 “현재 필터 Excel 내보내기”와 “선택 Excel 내보내기”가 함께 있어 사용자가 같은 목적의 action 두 개를 보게 된다.
- primary navigation/data workspace: 홈, 내 업무, 프로젝트, Pending, 생산관리, 구매, 자재 입고·키팅, 제조, 품질 IQC·panel 검사, 물류, 알림, 관리자.
- 반복 가능한 user-facing list/queue 후보: 프로젝트 목록, 내 업무, Pending 목록, 생산계획 프로젝트, 구매 프로젝트, 자재 입고 품목, 키팅 panel, 제조 panel, IQC 요청/attempt, LQC·OQC·전진검수·FAT panel, 물류 stage target, 알림 목록.
- 관리자 list 후보: 사용자, 부서, 휴일, 권한 matrix, 기준정보 변경 이력, 업무 이력, notification delivery, escalation. Fable은 개인정보·감사정보·권한과 실제 user value를 대조해 포함 또는 명시적 보류를 결정한다.
- detail/edit/settings/home/Teams projection은 상위 list의 동일 row를 다시 보여주거나 mutation form·summary일 수 있다. Fable은 중복 button을 만들지 않고 page identity 기준으로 포함 위치를 정한다.

## Fable이 권장안으로 확정할 비차단 항목

1. 전 route/component inventory와 `대상/상위 page 포함/선택 대상 없음/보류` 판정.
2. group·stage·tab·pagination이 있는 page에서 header 전체선택의 정확한 의미와 selection reset/유지 lifecycle.
3. 모든 대상 page가 재사용할 공통 checkbox·inline tray·selected request contract와 mobile 배치.
4. 화면별 stable selection key, 서버 재조회·exact count validation, explicit workbook column allowlist와 민감정보 제외.
5. 기존 전체 export endpoint를 API 호환용으로 보존할지 제거할지. 사용자 요구는 button 제거이므로 UI 중복 0이 필수다.
6. audit kind 확장 방식을 화면별 enum으로 둘지 generic selected kind+allowlisted screen key로 둘지.
7. 화면별 최대 선택 수와 10,000행 workbook cap의 관계, grouped parent 선택이 child row를 어떻게 포함하는지.
8. screenshot 대표 page 조합과 실제 Excel에서 workbook을 시각 확인할 sheet·상태 조합.

## 안전상 blocking으로 유지할 항목

- 권한·project/user scope 밖 row, 내부 GUID·실제 고객/사용자 원문·감사 원문·자유서술·첨부 bytes 노출
- 일부 ID만 포함하는 부분 성공, 선택하지 않은 row 포함, per-ID 실패 원인 노출, Frontend row data를 권한 검증 없이 authoritative source로 신뢰
- formula injection, unbounded body/row/workbook generation, 기존 migration 수정·번호 재사용
- 기존 Excel import·품질/제조/물류/Pending mutation 또는 lifecycle 변경
- Persistent UAT·대표 repo·main·origin·push·PR·merge

현재 Repository에서 위 blocking 충돌은 발견되지 않았고, openBlockingDecisionCount는 0이다.

## 성공 기준

- 모든 실제 route/component가 inventory에 포함되고 반복 선택 가능한 데이터 page의 누락 0이 확인된다.
- 대상 page마다 checkbox·header 전체선택·indeterminate·0건 disabled·단일 `선택 Excel 내보내기` action이 desktop·390px에서 동작한다.
- 기존 전체 export action은 UI에서 0개이며 프로젝트에서도 선택 action 하나만 보인다.
- 각 selected request는 서버가 같은 permission·scope·soft-delete/visibility 조건으로 전부-or-전무 재검증한다.
- workbook에는 선택 row만 있고 내부 ID·민감 원문·formula가 없으며 성공 audit만 남는다.
- 기존 import·조회·domain mutation 회귀가 없고 자동 검증·privacy/secret gate를 통과한다.
- 대표 page screenshot과 실제 Microsoft Excel screenshot을 저장하고 workbook을 닫은 상태를 확인한다.
- 사용자 검수는 `대기`, Git은 local experiment commit까지만 완료한다.
