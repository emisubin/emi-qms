# TASK-EXPORT-001 Change 002 — 전 페이지 선택 Excel 내보내기 통합

## 요청과 Task Identity

- canonicalTaskId: `TASK-EXPORT-001`
- changeId: `002`
- taskType: `NEW_FEATURE`
- gateStatus: `PASS_REUSE`
- branch: `experiment/task-export-001-all-pages-selected-export`
- baseExperimentCommit: `917693bf1dffba1754765a4170247504bb6352b4`
- mainMergeApprovalCount: `0/3`

사용자는 품질 페이지 Excel screenshot 요청을 모든 page의 Excel 내보내기 구현으로 확대하고, 전체 내보내기 button을 제거한 뒤 checkbox 전체선택을 포함한 선택 내보내기 button 하나만 남기도록 지시했다. 기존 `TASK-EXPORT-001` Phase 2와 `TASK-EXPORT-002`의 선택 UX를 하나의 공통 계약으로 통합한다. canonical Roadmap 다음 Gate는 `TASK-007A`이지만 사용자의 experiment fast-track standing rule과 이번 직접 지시를 현재 변경의 명시적 순서 override로 기록한다.

## 실행·안전 경계

- Fable 1차 planning → Codex 내용 review → Fable 2차 planning → Codex 구현·검증
- 전 page inventory와 선택 export 대상 누락 0 검증
- 기존 전체 export UI 제거, 공통 checkbox 전체선택·단일 선택 Excel action 구현
- 대표 desktop·390px page screenshot과 실제 Microsoft Excel screenshot 수집
- synthetic isolated data만 사용하고 확인 뒤 workbook을 닫는다.
- local experiment commit까지 승인됨
- 대표 repo·`main`·origin·Persistent UAT·실제 provider·push·PR·merge는 미승인·제외

## 산출물 상태

- interview source: `tasks/export-001-all-pages-interview.md`
- first planning: `tasks/export-001-all-pages-planning.md` 완료
- Codex review: `tasks/export-001-all-pages-review.md` 완료
- second planning: `docs/24-all-pages-selected-excel-export-plan.md` 완료
- implementation / validation / screenshot: 완료, 사용자 검수 대기

## Fable 1차 사용량

- 직전: 5시간 28% 사용·72% 잔여, 주간 전체 7%·93%, 주간 Fable 13%·87%
- 직후: 5시간 28% 사용·72% 잔여, 주간 전체 7%·93%, 주간 Fable 13%·87%
- Fable reset 시각은 reporter가 parse하지 못해 추정하지 않는다.

## Fable 2차·구현 종료 사용량

- 2차 직전·직후: 5시간 48% 사용·52% 잔여, 주간 전체 8%·92%, 주간 Fable 15%·85%
- 구현·자동 검증 종료: 5시간 64% 사용·36% 잔여·17:40 KST 초기화, 주간 전체 9%·91%·07-25 08:00 KST 초기화, 주간 Fable 18%·82%
- Fable Task session/transcript: runner cleanup 완료, session 2개·transcript 2개 제거

## 실제 구현 결과

- 업무 12개·관리자 8개, 총 20개 조회 route에 row checkbox와 현재 목록 `전체선택` checkbox를 적용했다.
- 각 화면의 export action은 `선택 Excel 내보내기` 하나만 표시한다. 기존 프로젝트·내 업무·구매의 전체 export UI와 키팅의 중복 전체 선택 button은 제거했다.
- 공통 `POST /api/data-exports/selected`가 화면 종류·선택 UUID·현재 filter를 받고 권한·scope·현재 존재 여부를 재검증한다. 요청 중 일부라도 유효하지 않으면 generic 422로 전체 차단한다.
- 최대 선택 1,000건, formula-safe workbook, 2-slot concurrency fence, allowlist column, 최소 audit를 기존 export 기반과 공유한다.
- additive migration `0040_all_pages_selected_export_audit.sql`은 20개 선택 export kind를 allowlist에 추가한다. Persistent UAT에는 적용하지 않았다.
- desktop 20개·390px 20개 screenshot과 data-bearing route 11개 workbook을 disposable Full-Stack E2E에서 생성했다. Microsoft Excel에서 품질 검사·관리자 사용자 workbook을 직접 열어 확인한 뒤 모두 닫았다.
- 자동 검증: Backend `388/388`, Frontend `92/92`, Full-Stack E2E `1/1`, lint error 0, build/typecheck PASS, workbook formula node 0.
