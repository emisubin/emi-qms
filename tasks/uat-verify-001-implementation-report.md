# UAT-VERIFY-001 Implementation Report

## 1. 목적

Persistent UAT의 최신 main 통합 기준선을 read-only로 재검증하고, migration·schema·data·authorization·notification·UI/UX·runtime protection의 사실을 재현 가능한 증빙으로 남긴다.

## 2. 배경

기존 검증은 live ledger의 approved historical marker를 latest-only readiness가 놓쳐 중단됐다. Full-ledger 검증과 official Review-safe handover가 완료된 뒤 `b1b2e2d9eecaa0e0cd181cd21d0adf45df48fcd3`에서 새 clean branch로 처음부터 재실행했다.

## 3. 검증 대상 환경

| 환경 | 주소 | 역할 | 변경 여부 |
| --- | --- | --- | --- |
| Development | 5174 / 5081 | 저장·worker 가능 비교 환경 | 유지 |
| Security Preview | 5185 | frontend 비교 보조 | 유지 |
| Current Review-safe | 5190 / 5092 | 공식 read-only 검증 | 유지 |
| Candidate | 5191 / 5093 | rollback 비교 | 유지 |
| PostgreSQL | persistent UAT | 검증 데이터 | SELECT only |

## 4. Runtime tree 정합성

공식 runtime worktree HEAD와 origin/main commit SHA는 문서 merge 때문에 다르지만, diff는 TASK-UAT-HANDOVER-002 문서 5개뿐이다. backend, frontend, scripts, configuration, migration runtime file diff는 0이므로 current 5190/5092를 최신 main과 기술적으로 동일한 runtime으로 판정했다.

## 5. Migration 결과

- repository `.sql` file source of truth: 27개
- ordinal prefix: 0001~0027, 중복·누락 0
- live canonical: 27개 전부 적용
- live extra: approved legacy 1개
- unknown extra/missing canonical: 0/0
- expected/actual latest: 모두 0027
- ledger status: `CompatibleWithApprovedLegacy`
- ready: 200

## 6. Schema 결과

Lifecycle 15개 column, delivery 0024~0026 17개 column, notification 0027 4개 column, critical constraint/index 각각 9개가 모두 존재했다. TeamsActivity constraint는 canonical channel 집합과 호환됐다. System catalog와 SELECT만 사용했고 DDL/migration apply는 수행하지 않았다.

## 7. 데이터 기준선

| 항목 | Count |
| --- | ---: |
| projects / work_items | 22 / 37 |
| notifications / recipients / deliveries | 90 / 164 / 93 |
| escalations | 2 |
| users / departments / holidays | 14 / 12 / 6 |

Project status는 Active 20, Cancelled 1, OnHold 1이다. Work item은 Completed 18, InProgress 13, Requested 6이다. 개인·업무 원문과 row identifier는 기록하지 않았다.

## 8. 참조 무결성

Project/work item/user orphan, completed/cancelled timestamp contradiction, duplicate idempotency, notification recipient/delivery orphan, delivery-recipient mismatch, escalation orphan/duplicate/active-closed mismatch는 모두 0이었다.

Open work item due date null 19건은 schema nullable과 현행 workflow가 허용한다. 검증 Task에서는 정책을 확대하거나 데이터를 보정하지 않았다.

## 9. Notification / delivery 상태

- Sent 60, Failed 20, DryRunSent 6, Suppressed 6, Disabled 1, Pending 0
- channel: TeamsChannel 16, TeamsDirectMessage 2, TeamsActivity 48, Mail 27
- Failed channel: TeamsActivity 19, Mail 1
- Failed admin handling: Acknowledged 6, Dismissed 14, Open 0
- retry overdue: 0
- attempt count 1~3: 93, 3 초과: 0

Failed 20건은 운영 상태를 보존하며 dashboard에서 모두 처리 완료 상태다. 실제 수신자·메시지·오류 원문은 출력하지 않았다.

## 10. Escalation 상태

Escalation 2건은 모두 Resolved이며 active 0이다. Work item orphan, closed item active escalation, duplicate active row, resolution timestamp mismatch, due date 없는 escalation은 모두 0이다. Dashboard L0~L3 합계와 detail active row가 일치한다.

## 11. Deletion lifecycle 상태

Request/schedule 편측 값과 purge block reason mismatch는 0이다. Active System Administrator는 aggregate 2명이다. Synthetic holiday 후보 1건에 historical `pre_delete_is_active` 미채움이 있으나 restore path의 `coalesce(..., true)` fallback이 있고 실제 업무 data가 아니어서 P3 정리 후보로 분류했다.

## 12. 권한·접근 범위

- admin route: administrator 200, 일반/업무 역할 403
- RecipientOnly: 수신자 list/detail 허용, 비수신자 list 미노출/detail 403
- Authenticated channel notice: active 일반 사용자 list/detail 200
- AdminOnly live row: 없음; code branch와 authorization suite로 확인
- 다른 사용자 work item detail: 404 비노출
- project list: 일반/업무 역할 200, project policy 자동 테스트 통과

## 13. Dashboard 정합성

Dashboard와 detail aggregate는 Failed 0/0, Pending 0/0, active escalation 0/0이다. L0~L3 합계도 0으로 일치한다. Daily Digest sent row는 현재 기준선에 없다.

## 14. Review-safe 방어 검증

Runtime projection은 ReviewSafe, DB read-only true, mutation/workers/providers/migration false, ledger compatible 27/28/1, missing/unknown 0을 반환했다. Live/ready는 200이다. POST/PUT/PATCH/DELETE와 unsafe method override는 모두 423 `UatReviewReadOnly`였다.

Targeted integration test는 pooled connection, explicit transaction, insert/update/delete fail-closed와 startup/seed/worker/provider exclusion을 검증했다. Persistent UAT에는 write probe를 보내지 않았다.

## 15. API / route 결과

Fixed alias GET API 16개는 모두 expected 200과 JSON shape를 충족했다. Admin denied 역할 2개는 expected 403이었다. Dynamic identifier는 내부 변수로만 사용하고 stdout/문서에 남기지 않았다.

## 16. UI/UX 결과

Desktop 13개와 390px 13개 fixed route에서 HTTP 200, shell/banner/migration diagnostic, blank false, target-not-found false, enabled mutation control 0, console/request error 0, page overflow 0을 확인했다.

8개 table/list alias에서 header/data column geometry mismatch는 0이었다. Wide admin table은 자체 scroll container를 사용하며 page-level overflow는 0이다. Project mobile cards가 표시되고 loading/empty/error/next-action contract가 존재한다.

## 17. 테스트 데이터 후보와 권장안

| 분류 | Count | 권장 |
| --- | ---: | --- |
| notification 후보 | 19 | 원본 보존, 별도 정리 승인 |
| work item 후보 | 3 | 완료 검수는 Completed, 중단 검수는 Cancelled |
| delivery 후보 | 41 | 실패 근거는 Acknowledged, noise는 Dismissed |
| department / holiday 후보 | 1 / 3 | hard delete 금지, lifecycle 처리 |
| recipient 없는 synthetic notification | 1 | 추적 보존 후 TASK-UAT-DATA-001 검토 |
| historical snapshot 미채움 delivery | 30 | runtime fallback 유지, 필요 시 별도 backfill 승인 |

검증 Task에서는 어떤 row도 변경하지 않았다.

## 18. 자동 테스트 결과

| 검증 | 결과 |
| --- | --- |
| git diff --check / actionlint | pass / pass |
| frozen install / audit | pass / 0 advisories |
| backend Release build | warning 0, error 0 |
| targeted backend | 141/141 |
| backend 전체 | 311/311 |
| frontend lint | error 0, 기존 warning 1 |
| frontend typecheck / unit / build | pass / 59/59 / pass |
| mock UI | 1/1 |
| Full-Stack E2E | 16/16 |
| redacted browser guard | negative 5/5 차단 |
| desktop / 390px | 13/13 / 13/13 |

기존 warning은 Fast Refresh 1건, Vite chunk size, Playwright 색상 환경 안내다. 신규 warning/error로 분류하지 않았다.

## 19. Secret / PII

Live 결과는 boolean, integer, fixed enum, aggregate count, status code, table/schema/migration 이름만 출력했다. Raw SQL row, user/project/notification text, GUID/ID, response body, DOM/text/accessibility snapshot, screenshot, console message, cookie/storage는 출력하지 않았다. Synthetic output guard는 email, GUID, HTML, long token, free-form string 5종을 모두 차단했다.

## 20. Findings

- P0: 0
- P1: 0
- 신규 미해결 P2: 0
- P3: migration checksum guard, synthetic/historical cleanup 후보

첫 browser fallback 시도는 없는 상세 identifier를 사용해 expected 404가 browser console error count에 포함됐다. Raw message는 읽거나 출력하지 않았고, 동적 identifier가 없는 fixed list fallback으로 교체해 desktop/mobile console error 0을 재확인했다. 제품 code defect가 아닌 검증 fixture 시행착오다.

## 21. 제한사항

- AdminOnly live fixture가 없어 live status matrix는 N/A이며 code/automated suite로 보완했다.
- Development worker가 Persistent DB를 공유하지만 관찰 구간에는 aggregate와 max timestamp 변화가 없었다.
- Historical snapshot 미채움은 UI fallback으로 표시 가능하지만 source snapshot 자체는 복구하지 않았다.
- Candidate 5191/5093와 legacy worktree 정리는 본 Task 범위가 아니다.

## 22. 후속 Task

1. 사용자 검수와 Draft PR merge gate
2. TASK-NOTIFY-REL-001
3. TASK-NOTIFY-ESC-001
4. TASK-AUTH-HARDEN-001
5. TASK-GOV-002
6. 별도 승인 시 TASK-UAT-DATA-001

## 23. 해결한 업무 문제

검수 화면이 안전하다는 사실뿐 아니라, 표시되는 숫자·권한·알림 범위·schema·ledger·data reference가 서로 맞는지를 한 번에 확인할 수 있는 기준선을 만들었다. 운영자는 저장이나 발송 없이 현재 UAT 상태를 감사할 수 있다.

## 24. 기술적 결정과 검토한 대안

- Live write test 대신 Review-safe status와 isolated DB write rejection test를 사용했다. Persistent UAT 무변경 요구를 우선했다.
- Commit SHA 단순 비교 대신 runtime path file diff를 사용했다. 문서 전용 squash merge 차이를 runtime mismatch로 오판하지 않는다.
- Full body/DOM 캡처 대신 allowlist projection과 selector geometry를 사용했다.
- Data candidate 자동 삭제 대신 aggregate 분류와 후속 승인 정책을 선택했다.

## 25. 시행착오 및 폐기한 접근

- Invalid detail fallback은 expected 404도 console error로 집계하므로 폐기했다. Fixed 목록 fallback으로 대체했다.
- Live identifier와 원문을 보고서에 예시로 넣는 접근은 사용하지 않았다.
- Persistent DB write rejection을 직접 DML로 시도하는 접근은 금지사항 때문에 사용하지 않았다.

## 26. 사용자 검수 결과와 남은 항목

현재 상태는 **Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 대기**다. 사용자는 5190 화면, dashboard/detail 수치, notification scope, table alignment, desktop/390px, SOP와 User manual을 직접 확인해야 한다. 자동 검증 항목을 사용자 검수 완료로 표시하지 않았다.
