# TASK-QUALITY-OPERATING-MODEL-001 — Codex 1차 기획 Review

## Review 결론

- reviewStatus: `RESOLVED_FOR_SECOND_PLANNING`
- reviewedArtifact: `tasks/quality-operating-model-001-planning.md`
- taskType: `NEW_FEATURE`
- blockingDecisionCount: 0
- recommendation: `Fable 2차 기획에서 아래 resolution을 반영한 뒤 구현`

Fable 1차 기획의 제품 방향과 사용자 확정 정책은 타당하다. 기존 IQC attempt·Pending·업무·알림 축을 유지하면서 `ScanBased` 분기와 `검사 대상 아님` 상태를 추가하는 후보 A를 유지한다. 다만 구매 구분의 변경 시점과 스캔형 검사 저장 구조를 그대로 구현하면 과거 기록이 나중의 catalog 수정에 따라 바뀌거나, 상세 IQC용 `iqc_reports`에 가짜 양식·PDF가 생길 수 있으므로 아래 보정을 2차 기획의 구현 계약으로 고정해야 한다.

## 유지

1. **프로젝트 단위 전환**
   - migration 시점에 존재하는 모든 프로젝트는 `AllReceipts`로 backfill한다.
   - API 직접 생성·프로젝트 Excel 생성 등 기능 업데이트 이후의 모든 정상 생성 경로는 `CategoryBased`를 명시적으로 기록한다.
   - 기존 프로젝트에는 구분을 요구하지 않고 현재 Detailed IQC 흐름을 유지한다.

2. **기존 IQC attempt·Pending lineage 재사용**
   - `material_iqc_attempts.decision_mode`에 `ScanBased`를 추가한다.
   - 부적합·조치·재검사 work item과 Pending 연결은 기존 attempt 회차를 그대로 사용한다.
   - 재검사는 기존 확정본을 고치지 않고 새 attempt를 만든다.

3. **별도 비검사 상태**
   - IQC가 없는 도착분을 `Passed`로 저장하지 않는다.
   - `InspectionNotRequired` 같은 별도 상태를 사용하고 입고 확정 전 상태임을 화면과 집계에서 명확히 한다.
   - 입고 확정 API는 `Passed`와 `InspectionNotRequired`를 각각 허용하되 이벤트 문구를 구분한다.

4. **구분 catalog와 모든 구매 입력 경로의 일관성**
   - 양식 관리에 구매품 구분 관리를 추가한다.
   - 직접 입력뿐 아니라 현재 제공하는 구매 Excel preview/apply에도 구분 열과 필수 검증을 적용한다.
   - 초기값은 `외함`, `판금류`, `부스바`, `명판`, `기타`로 두고 현재 정책상 외함만 IQC 필요로 설정한다.

5. **스캔 증빙 계약**
   - 적합·부적합 모두 확정 전 서명 스캔본을 1개 이상 요구한다.
   - PDF·JPEG·PNG 다중 첨부, 파일당 10MB·회차당 10개 한도를 적용한다.
   - 확정 후 판정·첨부·핵심 정보는 API와 DB trigger 양쪽에서 수정·삭제를 거부한다.

## 추가·수정

### R1 — 구매품 저장 시 routing snapshot을 고정한다

**Finding:** 1차 기획은 구분의 `IQC 필요` 설정을 도착 등록 시 snapshot하도록 제안한다. 그러면 구매팀이 `외함`으로 발주한 뒤 관리자가 catalog 설정을 바꾸면 같은 구매품의 아직 도착하지 않은 수량이 갑자기 IQC 비대상으로 바뀔 수 있다. 이는 발주 당시 정책과 이력을 재현하기 어렵게 한다.

**Resolution:**

- 신규 정책 프로젝트의 구매품을 저장할 때 다음 값을 함께 snapshot한다.
  - category id
  - 안정적인 category code
  - 저장 당시 표시명
  - 저장 당시 `requires_iqc`
- 도착 등록은 현재 catalog를 다시 조회하지 않고 구매품 snapshot으로 routing한다.
- 구매 담당자가 구매품의 구분을 명시적으로 변경할 때만 새 snapshot으로 갱신하고 기존 변경 이력에 전·후 값을 남긴다.
- catalog 이름 또는 IQC 필요 여부 변경은 이후 새로 저장·변경되는 구매품에만 적용한다.
- 기존 도착분과 기존 구매품 snapshot에는 소급하지 않는다.

### R2 — 스캔형 보고서는 상세 IQC 테이블에 억지로 넣지 않는다

**Finding:** 현재 `iqc_reports`는 `template_version_id`가 필수이고, 상세 항목·snapshot JSON·시스템 생성 PDF lifecycle을 전제로 한다. 외함 스캔형 IQC는 시스템 검사서와 PDF를 만들지 않는다는 사용자 정책과 다르다.

**Resolution:**

- 회차·Pending·업무의 부모 축은 기존 `material_iqc_attempts`를 재사용한다.
- 스캔형 내용은 attempt와 1:1인 별도 `material_iqc_scan_reports`와 1:N `material_iqc_scan_attachments`에 저장한다.
- 스캔형 report에는 초안/확정 상태, 판정, 판정 사유 또는 비고, version, 확정자·확정일, snapshot hash를 둔다.
- 첨부에는 원본 파일명, 정규화 MIME, byte 수, SHA-256, content를 둔다.
- 스캔형에는 상세 `iqc_reports` row나 시스템 생성 IQC PDF를 만들지 않는다.
- 조회 응답은 decision mode에 따라 상세형 report id/status와 스캔형 report id/status/attachment count를 명시적으로 구분한다.

### R3 — 프로젝트 생성 경로를 빠짐없이 고정한다

**Finding:** 현재 프로젝트 생성은 직접 생성과 Excel 적용이 서로 다른 insert 경로를 사용한다. DB default만 바꾸면 seed·migration test·예외 insert까지 신규 정책으로 오인될 수 있다.

**Resolution:**

- migration은 기존 row를 `AllReceipts`로 backfill하고 안전한 legacy default를 유지한다.
- 정상 사용자 생성 경로인 `CreateProjectAsync`와 Excel `InsertProjectWithPanelsAsync`에서만 `CategoryBased`를 명시한다.
- project detail/procurement response가 routing mode를 반환해 Frontend와 Backend가 같은 기준을 사용한다.
- 개발 seed·fixture가 의도한 정책을 명시적으로 선택하도록 테스트 helper를 분리한다.

### R4 — 비검사 도착분도 자재 업무를 실제 생성한다

**Finding:** 상태만 `검사 대상 아님`으로 바꾸면 자재 담당자가 입고 확정 필요 사실을 놓친다.

**Resolution:**

- 도착 등록 transaction 안에서 IQC attempt 대신 기존 입고확정 담당자 해석을 사용해 인앱 알림과 내 업무를 생성한다.
- work item 상세에는 `IQC 검사 대상이 아닌 도착분입니다. 입고 확정을 진행해 주세요.`와 품목·수량을 표시한다.
- IQC 담당자가 없어도 비검사 도착 등록은 성공해야 한다.
- 입고 확정 후 해당 work item을 완료하고 기존 제조 투입·키팅 후속 흐름을 유지한다.

### R5 — 상태·집계·완료 조건의 의미를 구분한다

**Finding:** 현재 `ReadyToConfirmCount`는 `Passed`만 세고, 입고 확정 guard와 이벤트도 IQC 합격만 전제한다. 새 상태만 추가하면 KPI·프로젝트 흐름·생산계획 실적이 누락될 수 있다.

**Resolution:**

- `ReadyToConfirmCount`는 `Passed + InspectionNotRequired`를 포함한다.
- IQC 대기·합격 집계에는 `InspectionNotRequired`를 포함하지 않는다.
- 도착분 종료는 두 경로 모두 `Confirmed`로 통일한다.
- 자재 입고 완료·프로젝트 진행률·생산관리 실적은 최종 `Confirmed`를 기준으로 유지한다.
- IQC 단계 표시가 필요한 곳에서는 `검사 대상 아님`을 합격과 별도 표시한다.
- cancellation은 `Arrived`와 `InspectionNotRequired`에서 허용할지 기존 UX를 대조해 명시한다. 권장안은 입고 확정 전 비검사 도착분도 취소 허용이다.

### R6 — catalog 권한과 참조 보존을 분리한다

**Resolution:**

- catalog 변경은 현재 Quality 양식 관리 권한(`CanManage`)을 사용한다.
- 구매·자재 화면에서 사용하는 활성 목록 조회는 해당 화면 접근 사용자에게 허용한다.
- 사용된 category row는 hard delete하지 않고 `is_active=false`로 사용 중지한다.
- 이름 중복, code 불변, 낙관적 동시성(row_version), 변경 감사 기록을 적용한다.
- 구매품 snapshot이 있으므로 catalog 이름 변경·비활성화 후에도 과거 표시가 바뀌지 않는다.

### R7 — 파일 보안과 원본 파일명 정책을 명시한다

**Resolution:**

- 확장자와 client MIME을 신뢰하지 않고 magic byte를 검사한다.
- PDF는 `%PDF-`, JPEG는 SOI, PNG는 signature를 검사한다.
- 허용 형식 외 파일, 빈 파일, 파일당 10MB 초과, 회차당 10개 초과를 서버에서 차단한다.
- 다운로드 응답은 `nosniff`, 안전한 content disposition, DB에 저장한 정규화 MIME을 사용한다.
- 원본 파일명은 표시용으로만 정규화·길이 제한하고 경로로 사용하지 않는다.

## 보류

- 판금류·부스바·명판의 실제 검사·성적서 정책
- 협력사용 Excel 검사서 생성과 배포
- 스캐너 직접 연동, OCR, 외부 object storage
- LQC·OQC·전진검수·FAT 운영 모델 변경

이 항목들은 현재 기능의 확장 지점을 막지 않도록 category code와 routing snapshot을 일반화하되 이번 구현에는 포함하지 않는다.

## 제거

- 스캔형 IQC에 상세 검사 항목 template 또는 시스템 생성 IQC PDF를 강제로 연결하는 안
- 품목명 문자열로 외함을 자동 판별하는 안
- catalog 변경을 기존 구매품·도착분에 소급하는 안
- 비검사품을 `IQC 합격`으로 저장하는 안

## 권장 구현 순서

1. additive migration과 project/category/procurement snapshot 계약
2. catalog API·권한·감사
3. 구매 직접 입력·Excel 구분 validation
4. 자재 도착 routing과 비검사 입고확정 업무
5. 스캔형 IQC report·attachment·확정 불변
6. Pending 재검사 lineage
7. Frontend 양식 관리·구매·자재·IQC 분기
8. migration/API/UI/E2E·desktop/mobile 검증

## 2차 기획 요구

Fable 2차 기획은 R1~R7을 모두 반영하고, 다음을 명확한 구현 source of truth로 작성해야 한다.

- 정확한 상태명·decision mode·table 관계
- 기존 프로젝트와 신규 프로젝트 생성 경로별 전환 방식
- 구매품 routing snapshot 필드와 catalog 변경 적용 범위
- 비검사 도착 transaction의 알림·내 업무·입고 확정·취소 계약
- ScanBased 초안·첨부·확정·재검사 API와 불변 trigger
- PDF/JPEG/PNG 제한과 다운로드 보안
- 변경 allowlist와 회귀 테스트 matrix

openBlockingDecisionCount: 0
