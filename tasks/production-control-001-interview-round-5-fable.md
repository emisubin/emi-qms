The baseline is complete: all ten §12 decision items were resolved across rounds 1–4 (each round answered 1-A through 5-A), Round 4's user decisions are recorded in the canonical interview, and no blocking question remains. Per the Round 4 commitment, Round 5 is the question-free confirmation summary. The task is not yet listed in the roadmap or experiment ledger, which matches the identity gate's recorded explicit override (`PASS_CREATE`), so this raises no new blocking issue. Below is the Round 5 artifact for Codex to validate.

---

# TASK-PRODUCTION-CONTROL-001 — Deep Interview Round 5 (Fable 5) — 전체 확인용 요약

- interviewRound: 5
- roundTheme: 질문 없음 — 전체 확인용 요약 (사용자 확인 대기)
- baselineSources: 누적 interview 문서(Round 2·3·4 사용자 결정 반영), Round 1~4 원문 artifact, `tasks/production-control-001-identity-gate.md`, `tasks/production-control-001-change-001.md`, Root/`frontend`/`backend` `AGENTS.md`, `CLAUDE.md`, `docs/00-product-roadmap.md`, `tasks/_templates/new-feature-interview-template.md`

지난 4개 round의 답변이 모두 기록되었고, 더 여쭐 blocking 질문이 없습니다. 아래는 지금까지 확정된 내용 전체를 한 번에 확인하는 요약입니다. 이 요약이 맞으면 확인해 주시고, 다르게 이해된 부분이 있으면 해당 번호를 지적해 주세요. 확인이 기록된 뒤에만 1차 기획을 시작합니다. 이 확인은 기획 승인이나 구현 승인이 아닙니다.

## 1. 해결할 문제와 기대 결과

- 지금은 생산계획 항목마다 단일 예정일과 비고만 입력하고, 각 부서가 일을 끝내도 생산관리 담당자가 부서 화면을 오가며 진행 상황을 직접 확인해 옮겨야 합니다.
- 이 기능이 완성되면 프로젝트 상세 생산관리 탭 한곳에서, Item별 고정 계획 항목의 **계획 기간(담당자 입력)**과 **실적 기간(부서 실데이터에서 자동 계산)**, 진행률, 지연·차단 상태와 구매품목·패널별 근거를 확인하고, 같은 정보를 계획/실적 가로 막대 일정으로 비교할 수 있습니다.

## 2. 사용할 사람과 권한

- 생산관리 정·부 담당자: 계획 시작·종료와 비고만 입력·수정. 기존 권한·수정 이력 방식 유지.
- 구매·자재·제조·품질·물류 담당자: 기존 부서 화면에서만 입력하며, 자기 입력이 어느 계획 항목 실적으로 반영됐는지 확인. 생산관리 실적을 직접 고칠 수 없음.
- 다른 내부 부서: 조회 전용. System Administrator: 업무 계획·실적 우회 입력 불가(기존 불변조건 유지).

## 3. 데이터 저장 방식 (Round 1~2 확정)

1. 기존 생산계획 표를 확장해 계획 시작·종료와 부서 연결 정보를 추가합니다.
2. 계획 항목은 이름·순서가 아닌 변하지 않는 고유 이름표(stable code)와 template version으로 관리하고, 프로젝트에는 적용 시점 내용을 snapshot으로 저장합니다.
3. 기본 milestone 목록과 연결 규칙은 seed와 Backend 등록으로 고정하고, 관리자 편집 화면은 이번에 만들지 않습니다.
4. 자동 실적은 생산관리 탭을 조회할 때 최신 부서 원본 데이터에서 항상 같은 결과가 나오도록 계산합니다.
5. 기존 단일 예정일은 계획 시작일·종료일 같은 날짜로 복사하고 원본 값도 보존합니다.
6. 필수 항목의 계획 시작일·종료일이 모두 입력되면 생산계획 단계를 완료로 판정합니다.

## 4. 실적 계산 규칙 (Round 3 확정)

1. 실적 날짜는 담당자가 적은 실제 업무 날짜(도착일·출발일 등)를 우선하고, 없는 업무만 시스템 확정 시각의 한국 날짜를 사용합니다.
2. 구매·자재 완료는 기존 자재 화면의 담당자 확정 행위(전체 입고 확정 + 마감)를 그대로 사용하고, 수량은 근거로만 표시합니다.
3. 과거 제조·검사 기록은 이름이 정확히 일치할 때만 연결하고, 나머지는 기록을 보존한 채 `연결 안 됨`으로 표시합니다. 임의 연결은 하지 않습니다.
4. 재검사가 있으면 실적 기간은 최초 검사 시작부터 최종 합격까지로 계산합니다.
5. 계획 수정은 현재 값 + 기존 변경 기록 방식을 유지하고, 최초 기준 계획 비교 화면은 이번 범위에서 제외합니다.

## 5. 화면 구성 (Round 4 확정)

1. PC 표는 `항목명 / 필수 / 계획 기간 / 실적 기간 / 진행률 / 일정 상태` 6칸을 기본 표시하고, 비고와 대상별 근거는 행 펼침(한 번에 한 항목)에서 확인합니다.
2. 가로 막대 일정은 하루 단위 고정 축 + 항목명 열 고정 + 가로 스크롤이며, 열면 오늘 위치로 자동 이동합니다. 계획 막대(외곽선/패턴)와 실적 막대(상태 의미색 채움)를 항목별로 비교하고, 오늘 기준선·주말·공휴일과 지연 일수 텍스트를 표시합니다.
3. 390px 좁은 화면은 항목별 카드(상태·계획/실적 기간·지연 텍스트 + 화면 폭에 맞춘 두 줄 요약 막대)로 표시하고 가로 스크롤을 쓰지 않습니다.
4. 구매품목·패널별 근거는 펼침 표에서만 보여주고 일정표는 계획 항목 단위 막대를 유지합니다.
5. 접근성은 표를 공식 대체 수단으로 유지하고, 각 막대에 텍스트 설명을 붙이며, 상태는 색 + 채움 패턴 + 텍스트를 함께 사용합니다.

## 6. 예외 상황 처리 (확정)

- 일부 대상만 완료되면 실적 시작과 `완료 수/전체 수`를 표시하고 종료일은 비워 둡니다.
- Open Pending이 있으면 과거 실적을 지우거나 단계를 되돌리지 않고 `차단` 상태와 근거를 우선 표시하며, 해제 후 최신 유효 결과로 재계산합니다.
- FAT 불필요 프로젝트는 `해당 없음`으로 분모·지연 계산에서 제외하고, 취소된 패널·품목은 분모에서 빼되 이력을 보존합니다.
- 부서 저장 성공 뒤 일정 새로고침이 실패해도 부서 저장을 되돌리지 않고 `저장 완료·일정 새로고침 실패`로 구분해 안내합니다.
- 사용자 추가 custom 항목은 삭제하지 않고 `수동 항목 / 연결 안 됨`으로 보존하며 계획 기간만 입력할 수 있습니다.
- 일정 상태·진행률은 기존 18단계 전체 흐름·진행률 공식과 분리하며 그 공식은 바꾸지 않습니다.

## 7. 이번에 하지 않는 것 (명시적 제외)

- 기존 18단계 workflow 순서·전체 진행률 공식 변경, 부서 업무 화면 재구현
- 예정일의 업무 목록 마감일 자동 동기화, 지연 자동 알림(인앱·Teams·메일), ERP/MES/회계 외부 연동
- 관리자 milestone 편집 화면, 실제 회사별 milestone 내용 확정·운영 template 일괄 입력
- 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge

## 8. 나중에 필요하면 후속 과제로 (비차단, 이번 범위 아님)

- 최초 기준 계획 대비 비교 화면, 일/주/월 확대·축소, 일정표의 대상별(패널별) 막대, 구매 수량 불일치 경고

## 9. 성공 기준 (요약)

- 담당자는 계획 시작·종료만 입력하고, 각 부서의 기존 입력만으로 실적·진행률이 자동 반영됩니다.
- 일부 완료·FAT 해당 없음·Pending·재검사·취소·custom 항목을 잘못 완료로 계산하지 않습니다.
- 표와 가로 막대 일정이 같은 계산 결과를 사용해 날짜·상태·진행률이 일치합니다.
- 기존 전체 흐름·권한·수정 이력·부분 출하가 회귀하지 않고, Backend/Frontend 자동 검증과 desktop/390px 화면 검증을 통과합니다.

## 10. 확인 방법

아래 항목이 모두 맞으면 "확인" 또는 "요약 확인"이라고 답해 주세요. 다르게 이해된 부분이 있으면 번호와 함께 알려 주시면 해당 부분만 다시 정리합니다.

- [ ] 업무 문제와 기대 결과(§1)가 정확하다.
- [ ] 대상 역할과 권한(§2)이 정확하다.
- [ ] 확정 정책(§3~§6)이 내가 답한 내용과 일치한다.
- [ ] 포함·제외 범위(§7)와 후속 과제 분리(§8)가 정확하다.
- [ ] 남은 blocking 결정이 없다.
- [ ] 이 요약을 1차 기획의 입력으로 사용하는 데 동의한다.

확인이 기록되면 interview는 `COMPLETED_CONFIRMED`, `userConfirmed: true`, `openBlockingDecisionCount: 0`이 되고 그 뒤에 1차 기획을 작성합니다. 기획 승인과 구현 승인은 별도 절차로 남습니다.

- interviewStatus: SUMMARY_CONFIRMATION_REQUIRED
- planningStatus: NOT_STARTED
- implementationApproved: false
