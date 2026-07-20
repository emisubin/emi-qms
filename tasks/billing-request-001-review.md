# TASK-BILLING-REQUEST-001 — Codex 내용 Review

## 결론

Fable 1차 기획의 핵심인 `반월 추천 기간 → 후보 선택 → 서버 재검증 → 요청 batch와 workbook 저장 → 재다운로드`는 사용자 문제를 직접 해결하며 유지한다. 특히 기존 선택 내보내기와 구분해 회계 인계라는 실제 업무 event와 요청 이력을 남기는 방향, `operationId` 멱등, 동일 project 중복 요청 차단, 서버 생성 formula-safe workbook, 실제 provider 제외는 적절하다.

다만 후보의 업무 기준을 `납품 완료일`로 잡은 것은 사용자의 정확한 표현인 “해당 기간동안 출하된 프로젝트”와 다르다. Repository에도 `DepartureProcessed` Finalized batch와 `departure_date`가 별도 authoritative 근거로 존재하므로, 후보 기간 기준을 모든 활성 패널의 출발 처리 완료와 프로젝트 최종 출발일로 교정해야 한다. 납품 완료는 이후 영업 알림과 최종 완료 조건으로 유지한다.

## 사용자 문제와 기대 결과

- 사용자는 매월 1일과 16일에 출하분을 수기로 찾고 회계팀 발행요청 자료를 가공하는 일을 줄이려 한다.
- 기대 결과는 단순 프로젝트 export가 아니라, 정해진 기간의 출하 완료 후보를 선택하고 이미 요청한 건을 제외한 회계팀용 Excel을 재현 가능하게 생성하는 것이다.
- 영업의 책임은 `직접 발행`이 아니라 `발행 요청 자료 생성·전달`이며, 회계 발행 사실을 확인한 뒤 기존 final completion으로 이어진다.

## 제품 방향·Roadmap 정합성

- 유지: 18단계 `납품 완료 → 영업 발행 확인 → 프로젝트 완료` 구조와 기존 014A completion fence.
- 추가: final 단계 전에 영업이 수행하는 회계 발행요청 batch 능력과 요청 상태 projection.
- 수정: 화면 문구를 `세금계산서 직접 발행`에서 `회계팀 발행 요청 → 발행 확인 입력`으로 변경.
- 불변: 회계팀 계정 workflow, 국세청·ERP·메일/Teams 실제 발송은 별도 신규 기능으로 남긴다.

## 기능 분류

| 기능 | 판정 | 근거·Resolution |
| --- | --- | --- |
| Seoul 반월 추천 기간 | 유지 | 1일에는 직전월 16일~말일, 16일에는 당월 1~15일. 다른 날짜 접근도 가장 최근 마감 구간을 기본값으로 제시 |
| 수동 기간 최대 92일 | 보류 | 정기 업무에서 자유 기간은 중복·누락 이해를 어렵게 한다. desktop에서 최대 31일 제한의 시작/종료 보정만 허용하고 mobile은 추천 기간 고정 |
| 납품 완료 후보 | 제거·교체 | 사용자 exact wording과 불일치. 모든 active panel이 `DepartureProcessed` Finalized이고 project의 마지막 `departure_date`가 기간 안인 출하 완료 후보로 교체 |
| open Pending 차단 | 유지 | 중요 blocking 정보가 있는 project의 회계 요청을 막고 후보 카드에 사유 표시 |
| checkbox 전체/개별 선택 | 유지 | 기존 선택 내보내기 UX와 일치하고 같은 의미의 전체 내보내기 버튼을 만들지 않음 |
| batch+snapshot+workbook 단일 기록 | 유지 | 생성 실패 시 요청 미기록, 다운로드 실패 시 동일 batch 재다운로드라는 복구 계약을 만족 |
| workbook bytea+sha256 저장 | 유지 | 반월·선택 상한으로 크기가 bounded하고 과거 파일 byte 재현을 보장 |
| project별 활성 요청 1건 | 유지 | 동일 프로젝트 중복 회계 요청 방지 |
| 사유 있는 revision/Superseded | 보류 | 사용자가 요구하지 않은 별도 상태·권한·UI 부담. 잘못된 요청 정정은 후속 POLICY/NEW_FEATURE로 분리 |
| 요청 이력·재다운로드 | 유지 | 네트워크 실패 복구와 감사에 필요 |
| project 상세 영업 탭 요청 상태 | 유지 | 한 프로젝트에 모든 부서 data를 묶어 보는 사용자 방향과 일치 |
| 기존 settlement detail 요청 상태 | 유지 | `미요청 / 요청됨 / 회계 발행 확인 완료`를 구분해 다음 행동을 명확히 함 |
| 실제 알림·메일 첨부 발송 | 보류 | 실제 provider 금지 및 명시적 제외 |

## 누락 기능과 추가 권고

### 1. 출하 근거 snapshot

- batch item에 최종 출발일뿐 아니라 포함된 활성 panel 수와 출발 완료 panel 수를 snapshot으로 남긴다.
- 생성 직전 모든 active panel의 Finalized `DepartureProcessed` membership을 서버가 다시 확인한다.
- 한 project가 여러 departure batch로 나뉜 경우 마지막 `departure_date`를 회계 요청 기간 기준으로 사용하고 workbook에는 `최초 출발일`, `최종 출발일`을 모두 제공한다.

### 2. Excel 회계 기입란

- Repository에 존재하는 값만 채운다: 요청번호, 프로젝트 코드·명, 고객사, Item, 납품처, 최초/최종 출발일, panel 수, 공급가액, 통화, 영업담당, 요청 메모.
- `사업자번호`, `공급가/부가세 분리`, `세율`은 source가 없으므로 생성하지 않는다.
- 회계팀이 결과를 기록할 수 있도록 빈 `발행일`, `세금계산서 번호` 열은 추가하되 system fact처럼 보이지 않게 header note에 `회계팀 기입란`으로 표시한다.

### 3. 완료 의미와 문구

- 발행요청 Excel 생성은 project 완료가 아니다.
- 기존 014A의 `invoiceIssuedDate`는 “영업이 발행한 날짜”가 아니라 “회계팀 발행 사실을 확인한 날짜”로 label과 도움말만 교정한다.
- 완료 버튼은 `정산과 프로젝트 최종 완료`보다 `회계 발행 확인 후 프로젝트 완료`로 바꾼다.

### 4. 권한과 조회 범위

- candidate/history/create/download는 기존 `sales.settle`을 재사용한다.
- workbook은 판매금액이 업무상 필수이므로 `Project.SalesAmount.Read`가 없으면 생성하지 않는다.
- project 상세의 요청 여부·요청일은 기존 project read scope에서 조회 가능하되 금액·파일 다운로드는 별도 권한을 유지한다.

## 과도한 범위·운영 부담

- revision·Superseded 상태는 이번 MVP에서 제거한다. 이미 요청된 project는 다시 선택할 수 없고, 잘못된 요청의 취소·정정은 후속 정책 Task로 남긴다.
- 자유로운 92일 기간은 반월 업무 의미를 약화하므로 최대 31일 desktop 보정으로 축소한다.
- 이메일 첨부, 회계팀 승인·발행 workflow, 사업자정보 master, 세율·VAT 계산은 제외한다.
- 신규 범용 export registry 확장은 하지 않는다. 이 workbook은 audit를 남기는 도메인 command 결과다.

## 권장 구현 순서

1. additive migration: immutable batch/item/workbook bytes·checksum·operation receipt·download event, project active-request unique index.
2. Backend candidate query: 출발 완료 근거·Seoul 기간·scope·Pending·중복·금액 권한.
3. Backend create/download: project lock, 재검증, snapshot, formula-safe workbook, bytea/sha256, operation replay.
4. Frontend 발행요청 화면: 추천 기간·요약·선택 card/table·생성·history 재다운로드.
5. project 영업 탭·settlement 문구/요청 상태 projection.
6. migration/API/unit/UI/full-stack/workbook render 검증과 desktop/mobile screenshot.

## 검증 우선순위

- 출발 완료 vs 납품 완료가 섞이지 않는 후보 경계
- 월말·윤년·Seoul 날짜와 마지막 출발일 집계
- open Pending·scope·sales amount permission·soft-delete 제외
- 같은 project 동시 batch 생성과 같은 operation replay
- workbook row/합계/문자열 formula safety, 저장 bytes와 재다운로드 sha256 일치
- 기존 014A completion·Sales KPI·물류 lifecycle 회귀

## Review resolution

- 유지: Fable 권장안 A의 저장 batch와 재다운로드, 멱등·중복 방지, server workbook, project status projection.
- 추가: 첫/마지막 출발일 snapshot, 회계팀 기입란 명시, 실제 책임 문구.
- 보류: 재요청 revision, 자유 92일, actual provider, 회계 workflow.
- 제거·교체: `DeliveryCompleted` 기간 후보를 `DepartureProcessed` Finalized 출하 후보로 교체.
- blockingDecisionCount: 0
- secondPlanningRecommendation: 위 resolution을 반영한 2차 기획을 최종 구현 source of truth로 사용.
