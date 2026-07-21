# TASK-WORKFLOW-CONTINUITY-001 Change 002 구현 보고서

- taskType: `P2_REMEDIATION`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `8631594b2dcb31de5ed5dd187df43393bba6a2fa`
- implementationStatus: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- instructionChainRead: `true`
- fableInvocationCount: `0`
- fableWaiverSource: 사용자가 기존 기능 보정이므로 Fable 없이 구현하라고 명시
- userValidationCompleted: `false` — 마지막 일괄 검수 대기
- mainMergeApprovalCount: `0/3`
- persistentUatApplied: `false`
- actualProviderCallCount: `0`

## 1. 해결한 업무 문제

구매, 자재 입고와 IQC data는 이미 연결돼 있었지만 실제 화면에서는 그 관계를 한눈에 확인하기 어려웠다.

- 구매의 도급 구매품과 사급 자재가 한 목록에 섞였다.
- 도급 구매품은 구매 시 발주 수량을 입력할 수 없었다.
- 자재 탭은 품목 요약과 도착 회차가 각각 별도 카드라 한 품목의 누적 상황을 읽기 어려웠다.
- 프로젝트 품질 탭에는 후속검사만 있고 IQC가 없었다.
- 도착분마다 IQC 업무가 생성되는 기존 Backend 계약을 실제 전체 화면 흐름으로 검증한 증빙이 부족했다.

이번 Change는 이 여섯 요청을 하나의 구매→자재→IQC 추적 보정 Task로 처리했다.

## 2. 포함·제외 범위

### 포함

- 구매 조회·수정의 `도급 구매품` / `사급 자재` 탭
- 도급 구매품의 선택 발주 수량·단위 입력과 서버 검증
- 구매 품목별 한 행과 행 아래 도착·IQC·Pending 시간순 이력
- 프로젝트 품질 탭의 `수입검사(IQC)` / `후속검사` 구분
- 도착 두 건이 서로 다른 receipt·IQC·내 업무·알림을 만드는 통합 검증
- desktop와 390px 화면 검증

### 제외

- 구매 완료 workflow 조건 변경
- IQC 성적서 양식·사진 저장 정책 변경
- DB schema·migration
- Persistent UAT, 실제 Teams·Mail provider, 대표 repo, `main`, push·PR·merge

## 3. 기술적 결정과 검토한 대안

### 같은 구매 품목 identity 유지

구매 data를 자재 화면용으로 복사하지 않았다. `project_procurement_items.id`를 구매, 자재 도착과 IQC가 함께 사용한다. 복제 table이나 동기화 worker를 추가하는 대안은 data drift와 복구 비용이 커서 사용하지 않았다.

### 도착분마다 IQC 자동 생성 유지

자재 도착 저장은 기존처럼 같은 transaction 안에서 receipt, IQC attempt, 품질 내 업무와 정·부 알림을 만든다. 별도 `IQC 요청` 버튼을 다시 추가하지 않았다. 한 품목이 두 번 도착하면 두 개의 독립 검사 회차가 생긴다.

### 도급 수량은 선택, 사급 수량은 필수

기존 도급 구매품 data의 수량 없음 호환성을 위해 도급 수량·단위는 둘 다 입력하거나 둘 다 비울 수 있다. 사급 자재는 제공 예정량 추적이 핵심이므로 기존 필수 계약을 유지했다. 어느 유형이든 도착 후 누적 도착량보다 작게 줄이거나 단위를 바꾸는 것은 차단한다.

### 한 행 + 펼침 이력

자재 탭은 구매 품목별 한 행을 기본으로 하고, 클릭 또는 keyboard로 열면 도착·IQC·Pending을 바로 아래에 시간순으로 표시한다. 모든 회차를 처음부터 카드로 펼치는 대안보다 목록 밀도와 품목 비교가 낫다.

## 4. 전체 영향

| 영역 | 영향 |
| --- | --- |
| Backend | 구매품 유형별 수량·단위 검증, 감사 field 기록, 기존 도착→IQC transaction 회귀 검증 |
| Frontend | 구매 유형 탭, 도급 수량 입력, 자재 품목 행·inline 이력, 품질 IQC/후속검사 탭과 품질 담당자 수정 진입 |
| DB / Migration | 변경 없음. migration `0049` 기준 유지 |
| API | 기존 구매 PATCH와 자재 도착 API shape를 유지하고 도급 수량 허용 범위만 확장 |
| 권한 | 변경 없음. 조회와 수정은 기존 부서·담당자 권한을 그대로 사용 |
| Workflow | 구매 완료 조건과 18단계 순서 변경 없음. 도착분별 IQC 자동 인계 계약 유지 |
| Excel / PDF / 첨부 | 변경 없음. 기존 구매 Excel·IQC PDF·증빙 계약 회귀 영향 없음 |
| 알림 | 새 채널 없음. 도착분별 기존 품질 정·부 인앱 알림을 통합 테스트로 검증 |

## 5. 주요 변경 파일

- `backend/src/Emi.Qms.Api/Procurement/ProcurementStore.cs`: 도급 구매품 수량·단위 검증과 감사 기록
- `backend/tests/Emi.Qms.Api.Tests/ProcurementApiTests.cs`: 도급 수량과 두 도착분의 IQC·업무·정/부 알림 통합 계약
- `frontend/src/App.tsx`: 구매 유형 탭, 자재 품목 이력, 품질 IQC 화면 구성과 진입
- `frontend/src/styles.css`: desktop/mobile 품목 행·이력·공급 유형 탭 layout
- `frontend/tests/App.test.tsx`: 구매 탭·도급 수량·품질 IQC 회귀
- `frontend/e2e/full-stack/procurement-material-trace.full-stack.spec.ts`: 실제 구매→분할 도착→자재→품질 전체 흐름
- 기존 구매·사급·lifecycle E2E: 최신 탭·문구·Pending UI 계약으로 보정

## 6. 검증 결과

### 자동 검증

- Backend 전체 isolated PostgreSQL: `412/412` 통과
- Frontend lint: error `0`, 기존 Fast Refresh warning `1`
- Frontend unit: `113/113` 통과
- Frontend production build: 통과, 기존 500 kB 초과 chunk warning 유지
- 신규 구매·자재·IQC Full-Stack: `1/1` 통과
- 연관 Full-Stack 묶음(신규 trace, workflow continuity, 사급, 구매 초기 준비): `4/4` 통과
- 실제 역할 stress lifecycle: `1/1`, panel `12`, 사급 도착 `6`, Pending `6`, 최종 workflow `18/18`
- 프로젝트 등록 구매 흐름: `1/1` 통과
- `git diff --check`: 통과

### 핵심 data 검증

- 도급 구매품 `10 EA`가 구매와 자재 조회에 동일하게 표시됨
- `4 EA`, `6 EA` 두 도착이 서로 다른 receipt `2건`, IQC attempt `2건` 생성
- 품질 내 업무 `2건`, 인앱 알림 `2건`, 정·부 recipient `4건` 생성
- 누적 도착 뒤 발주량을 `9 EA`로 줄이는 요청 차단
- desktop·390px page overflow `0`

### 시각 증빙

사용자의 지침대로 Repository 폴더에 복사하지 않고 `/tmp/procurement-material-trace-001-screenshots/`에 생성했다.

- `01-procurement-purchased-desktop.png`
- `02-procurement-customer-supplied-desktop.png`
- `03-material-item-inline-history-desktop.png`
- `04-quality-iqc-project-tab-desktop.png`
- `05-material-item-inline-history-mobile-390.png`
- `06-quality-iqc-project-tab-mobile-390.png`

### 미실행 검증

- 실제 Teams·Mail 발송: provider 호출은 사용자 승인 범위 밖이라 실행하지 않음
- Persistent UAT: migration·runtime handover 승인이 없어 적용하지 않음
- 사용자 직접 검수: 실험 branch 정책에 따라 마지막 일괄 검수 대기

## 7. 시행착오 및 폐기한 접근

- 도착 회차를 API 반환 순서대로 번호 붙이면 최신순 응답에서 1·2회차가 역전됐다. 도착일과 생성 시각 오름차순으로 정렬해 실제 시간 순서를 사용했다.
- IQC만 존재하는 프로젝트의 품질 담당자에게 프로젝트 품질 탭이 `조회 전용`으로 보이는 불일치를 시각 검수에서 발견했다. 품질 권한과 IQC 존재 여부를 함께 반영해 `품질 업무 수정`으로 바로잡고 실제 품질 담당자 E2E에 고정했다.
- 기존 stress E2E에는 프로젝트 기본 탭과 예전 Pending 입력 UI를 가정한 단계가 남아 있었다. 제품 흐름을 되돌리지 않고 최신 전체 흐름 기본 진입과 처리 내용 UI에 맞게 검수 절차를 보정했다.
- 구매 품목과 자재 이력을 별도 화면 model로 복제하지 않고 기존 동일 identity projection을 사용했다.

## 8. SOP — 담당자 업무 절차

1. 구매 담당자는 프로젝트 구매 탭에서 `도급 구매품` 또는 `사급 자재`를 선택한다.
2. 도급 구매품은 필요하면 발주 수량과 단위를 함께 입력한다. 사급 자재는 제공 예정 수량과 단위를 반드시 입력한다.
3. 자재 담당자는 실제 도착분마다 자재 입고에서 수량·단위·도착일을 등록한다.
4. 저장 즉시 해당 도착분의 IQC가 품질 정·부 담당자 내 업무와 알림에 생성된다. 별도 IQC 요청은 하지 않는다.
5. 자재 탭의 품목 행을 열어 각 도착 회차와 IQC 상태를 확인한다.
6. 품질 담당자는 프로젝트 품질 탭의 `수입검사(IQC)` 또는 내 업무의 해당 검사로 들어가 판정한다.

## 9. User manual — 화면 사용법

- 구매 탭의 상단 숫자는 공급 유형별 품목 수다.
- 자재 `입고 관리`에서 한 줄은 구매 품목 하나다. 줄을 누르면 바로 아래에 도착과 IQC 이력이 열린다.
- 품질 탭은 기본적으로 `수입검사(IQC)`를 보여주며 `후속검사`를 누르면 LQC·OQC·전진검수·FAT를 확인할 수 있다.
- 도급 수량을 입력하지 않은 기존 품목도 사용할 수 있다. 다만 수량을 입력할 때는 단위도 함께 입력해야 한다.

## 10. 사용자 검수 체크리스트

상태: `자동 검증 완료 / 사용자 검수 대기 — 마지막 일괄 검수`

- [ ] 구매 탭에서 도급 구매품과 사급 자재가 정확히 분리되는지 확인
- [ ] 도급 구매품에 발주 수량·단위를 입력하고 저장되는지 확인
- [ ] 한 품목을 두 번 나눠 도착 등록했을 때 품질 담당자의 내 업무·알림이 두 번 생성되는지 확인
- [ ] 자재 품목 행을 눌러 각 도착일·수량·IQC 판정이 시간순으로 보이는지 확인
- [ ] 프로젝트 품질 탭에서 IQC와 후속검사를 전환할 수 있는지 확인
- [ ] 모바일 390px에서 가로 잘림 없이 핵심 이력을 확인할 수 있는지 확인

## 11. 개인정보·secret 검토

- 테스트 data는 synthetic project와 dev role 계정만 사용했다.
- 실제 이름, 이메일, tenant/client id, token, secret, provider payload를 문서·screenshot에 기록하지 않았다.
- 신규 secret·environment variable·외부 연결을 추가하지 않았다.

## 12. Finding, 잔여 위험과 후속

- Open P0/P1/P2: `0/0/0`
- P3: 대형 `App.tsx` route 분할, 기존 `main.tsx` Fast Refresh warning과 production chunk 분할은 실험 완료 원장의 기존 backlog를 유지한다.
- 실제 운영 양식 content와 binary 사진 storage는 별도 후속 범위다.
- 이 Change를 대표 repo에 반영하려면 재구현이 아니라 별도 승격·UAT Task가 필요하다.

## 13. Rollback과 복구

DB migration이 없으므로 이 local experiment commit을 revert하면 코드·테스트·문서 변경이 함께 되돌아간다. 이미 저장된 도급 수량은 nullable 기존 column을 사용하므로 schema rollback이 필요 없다. 운영 전환 뒤 문제가 생기면 UI projection을 먼저 forward-fix하고 구매·자재 동일 identity는 유지한다.

## 14. 종료 산출물 추적

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 전체 |
| SOP | 완료 | 이 문서 `8. SOP` |
| User manual | 완료 | 이 문서 `9. User manual` |
| Roadmap update | 완료 | `docs/00-product-roadmap.md` 11~13장·Task row·Decision Log |
| User validation checklist | 작성·자동 검증 완료, 사용자 검수 대기 | 이 문서 `10. 사용자 검수 체크리스트` |

## 15. 게시 경계

- experiment local commit: 승인됨
- push / PR / merge: 미승인
- 대표 repo / GitHub `main`: 변경하지 않음
- `main` merge 승인: `0/3`
- Persistent UAT / 실제 provider: 미승인·미적용
