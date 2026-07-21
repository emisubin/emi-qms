# TASK-WORKFLOW-CONTINUITY-001 구현 보고서

- taskType: `NEW_FEATURE` fast-track + 기존 Task change/검수 결함 보정
- branch: `experiment/task-home-002-personalized-shell`
- implementationStatus: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- userValidationCompleted: `false` — 마지막 일괄 검수 대기
- mainMergeApprovalCount: `0/3`
- persistentUatApplied: `false`
- actualProviderCallCount: `0`

## 1. 사용자 검수 실패와 원인

사용자는 실제 담당자 입력 중 IQC 부적합 Pending 이후 진행할 수 없었다. 이전 자동 검수는 과거 호환용 IQC 요청 endpoint와 API 중심 준비 절차를 섞어, 실제 사용자가 `내 업무`에서 정확한 검사로 들어가 Pending을 조치하는 UI 경계를 끝까지 통과하지 않았다.

확인한 root cause는 다음과 같다.

- 검사 Pending의 수동 종결과 재검사 생성이 분리돼 상태·업무·알림이 부분 저장될 수 있었다.
- 도착 저장과 IQC 요청이 별도 action이었다.
- IQC 업무 link가 특정 attempt 선택값을 Frontend까지 보존하지 않았다.
- workflow IQC 상태 계산이 완료 event와 최신 검사 facts를 일관되게 사용하지 않았다.
- 프로젝트 기본 탭, 자재 입고/키팅 정보 구조와 QR 선택 상태가 실제 사용 순서와 맞지 않았다.

## 2. 구현 결과

### 업무 흐름

- 프로젝트 상세 기본 진입을 `전체 흐름`으로 변경했다.
- 생산계획 저장과 동시에 설계·구매 정담당 업무를 멱등 생성한다.
- 구매 완료 조건을 handoff data 기준으로 재정의하고 도착·IQC와 분리했다. 이미 완료된 구매 stage는 회귀하지 않는다.
- 도착 등록 transaction에서 IQC attempt·품질 업무·정/부 알림·event를 함께 생성한다.
- IQC 업무·알림은 `/quality/iqc?request={attemptId}`로 해당 검사에 바로 진입한다.

### Pending·품질

- 검사 Pending은 `조치 시작`, 처리 내용, `조치 완료`만 노출한다.
- 조치 완료 transaction에서 다음 검사 회차·업무·정/부 알림을 생성한다.
- 재검사 부적합은 같은 Pending을 다시 열고, 합격만 Pending·업무·검사 stage를 함께 종결한다.
- comment와 상태 이력을 하나의 시간순 activity timeline으로 합쳤다.
- IQC·LQC·OQC·고객검수·FAT 부적합은 사진 1장 이상 또는 30자 이상 구체 사유를 요구한다. 양식의 사진 필수 조건은 별도 AND로 유지한다.

### 자재·QR

- 프로젝트 자재 탭을 `입고 관리`와 `키팅 관리`로 나눴다.
- 입고 화면에 공급구분, 업체, 예정일, 발주/도착/확정/처리중/잔량과 회차별 IQC를 연속 표시한다.
- 정상 흐름의 별도 IQC 요청·재검사 요청 버튼을 제거하고 legacy endpoint만 멱등 복구 경로로 유지했다.
- 발급 가능 패널 checkbox와 1~50개 all-or-nothing QR batch 발급을 구현했다.
- 이미 발급된 활성 QR은 재사용하고, 미리보기는 클릭한 행 바로 아래에 펼친다.

## 3. 구매 완료 권장안 적용

활성 품목이 1개 이상이고 모든 품목이 다음을 만족할 때 구매를 완료한다.

1. 공통: 품목명, 공급구분, 입고예정일
2. 일반 구매품: 업체명, 발주일
3. 사급품: 제공 예정 수량과 단위
4. required template가 있으면 모든 필수 row가 active 품목과 일치

일반 구매품 수량·단위는 첫 도착 때 자재가 확정하는 기존 책임을 유지한다. 도착·IQC·입고 확정은 후속 단계이므로 구매 완료 조건에 포함하지 않는다.

## 4. 검증

- Backend Release build: 통과, warning/error `0/0`
- Backend targeted integration: IQC 상세, 자재 정상·Pending 재검사, 설계·구매 병행, QR batch `5/5`
- Backend 전체 isolated PostgreSQL: `411/411`
- Frontend lint: error `0`, 기존 warning `1`
- Frontend unit: `113/113`
- Frontend production build: 통과
- Full-Stack workflow continuity E2E: `1/1`
- QR Full-Stack E2E: `1/1`
- 기존 IQC digital report E2E: `1/1`
- 기존 후속 품질 E2E: `1/1`
- desktop 8장·mobile 1장 screenshot, page-level mobile overflow `0`

## 5. 스크린샷

사용자의 이전 지침에 따라 workflow continuity 화면은 Repository 폴더에 복사하지 않고 `/tmp/workflow-continuity-001-screenshots/`에 생성했다.

- `01-project-default-workflow-desktop.png`
- `02-material-receiving-tab-desktop.png`
- `03-material-kitting-tab-desktop.png`
- `04-iqc-my-work-deep-link-desktop.png`
- `05-pending-action-timeline-desktop.png`
- `06-reinspection-notification-desktop.png`
- `07-reinspection-my-work-deep-link-desktop.png`
- `08-iqc-completed-workflow-desktop.png`
- `09-material-subtabs-mobile-390.png`

QR 증빙은 기존 `tasks/qr-001-screenshots/`의 desktop·mobile 파일을 새 batch 흐름으로 갱신했다.

## 6. Fable 사용량

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 기획 전 | 0% 사용 / 100% 잔여 | 30% 사용 / 70% 잔여 | 59% 사용 / 41% 잔여 |
| 1차 기획 후 | 16% 사용 / 84% 잔여 | 31% 사용 / 69% 잔여 | 61% 사용 / 39% 잔여 |
| 2차 기획 전 | 16% 사용 / 84% 잔여 | 31% 사용 / 69% 잔여 | 61% 사용 / 39% 잔여 |
| 2차 기획 후 | 16% 사용 / 84% 잔여 | 31% 사용 / 69% 잔여 | 61% 사용 / 39% 잔여 |
| 구현 종료 | 27% 사용 / 73% 잔여 | 32% 사용 / 68% 잔여 | 63% 사용 / 37% 잔여 |

초기화 시각과 runner provenance는 [Change 001](workflow-continuity-001-change-001.md)에 보존했다.

## 7. Finding과 잔여 경계

- Open P0/P1/P2: `0/0/0`
- P3: `App.tsx` bundle 분할과 기존 `main.tsx` Fast Refresh warning은 이번 업무 연속성 범위 밖이다.
- 실제 운영 데이터의 과거 잘못 종결된 Pending backfill은 수행하지 않았다.
- 실제 Teams/Mail provider, Persistent UAT, 대표 repo, push·PR·merge·main은 변경하지 않았다.

## 8. Rollback

이 실험 local commit을 revert하면 코드·테스트·문서 변경을 함께 되돌릴 수 있다. migration 추가나 Persistent DB mutation은 없다.
