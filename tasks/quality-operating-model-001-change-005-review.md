# TASK-QUALITY-OPERATING-MODEL-001 Change 005 — Codex 내용 Review

- reviewTarget: `tasks/quality-operating-model-001-change-005-planning.md`
- planningAuthor: `FABLE_5`
- reviewOwner: `CODEX`
- reviewRound: 1
- reviewStatus: `USER_RESOLUTION_REQUIRED`
- planningApproved: false
- implementationApproved: false

## 1. 결론

기획의 제품 방향은 유지한다. 실제 업무 문제인 "구매품 구분마다 다른 IQC 방식·항목을 적용"하는 데 직접 집중하고, 기존 Detailed·ScanBased 검사 엔진과 비소급 snapshot을 재사용해 불필요한 새 검사 엔진을 만들지 않는다.

다만 구현 전에 아래 두 구조적 보완과 planning 16절의 사용자 결정 3건을 확정해야 한다. Fable 원문은 수정하지 않으며, 이 review resolution을 함께 구현 계약으로 사용한다.

## 2. 기능별 판단

### 유지

1. 양식 관리의 `대상 선택 → 현재 양식 관리` 패턴과 검색·필터를 구매품 구분별 IQC에도 적용한다.
2. 구분별로 검사 없음·스캔형·상세형을 운영하되, 실제 입력은 검사 스위치와 스캔형/상세형 방식으로 표현한다.
3. 검사 여부·방식은 구매품 저장 시점에 고정하고 이미 저장된 구매품에는 소급하지 않는다.
4. 상세 항목은 도착분의 Detailed report를 최초 생성하는 시점에 해당 구분의 현재 양식 version으로 고정한다. 이후 양식 변경과 무관하게 확정·재검사 이력을 보존한다.
5. 상세형은 항목 1개 이상일 때만 활성화하고 활성 상태에서 빈 양식 저장을 차단한다.
6. 설정·양식 변경 권한은 지정된 활성 품질 domain 양식 관리자와 시스템 관리자에게만 허용한다.
7. 외함 초기 스캔형, 나머지 초기 검사 없음, legacy `AllReceipts`의 전역 `MATERIAL_IQC`를 그대로 보존한다.

### 추가

1. **검사 설정의 단일 source of truth**: 신규 구분별 IQC 설정을 authoritative data로 두고, 기존 `material_categories.requires_iqc`를 독립적으로 수정 가능한 두 번째 설정으로 남기지 않는다. Migration에서는 기존 값을 신규 설정으로 backfill하고, 호환 응답이 필요하면 신규 설정에서 계산한 조회 projection으로만 제공한다. 두 값을 별도 저장·수정하는 dual-write는 금지한다.
2. **구분 metadata와 검사 설정 API 분리**: 일반 품질부서 사용자가 기존 권한으로 구분 이름·순서·활성 상태를 관리하더라도 검사 스위치·방식은 변경할 수 없어야 한다. 현재 `MaterialCategoryStore.UpdateAsync`처럼 한 요청에서 `requires_iqc`까지 필수로 받는 계약은 분리하거나 서버가 기존 값을 보존하도록 바꿔, 지정 부서장 권한을 우회하지 못하게 한다.
3. **양식 선택 키 고정**: Detailed report 최초 생성 시 표시명이나 code가 아니라 구매품에 snapshot된 material category id로 해당 구분의 현재 양식 version을 찾는다. 양식이 없으면 기존 전역 양식으로 fallback하지 않고 명확한 오류로 차단한다.
4. **방식 전환의 비소급 표시**: 부서장이 스캔형↔상세형 또는 검사 on↔off를 저장할 때 기존 구매품은 변하지 않는다는 안내를 저장 action 가까이에 고정한다. 한 번의 optimistic-concurrency mutation으로 방식과 스위치를 저장해 중간 불일치 상태를 만들지 않는다.
5. **재검사 불변조건 명시**: Detailed 재검사는 기존 실패 항목과 원회차 snapshot을 기준으로 새 회차를 만들며, 그 사이의 구분 양식 변경으로 실패 항목 구성이 바뀌지 않는다. ScanBased 재검사는 기존 계약대로 새 스캔본을 요구한다.

### 보류

1. 구분 목록의 검사 상태 badge는 탐색에 도움이 되므로 구현 비용이 작으면 포함하되, 핵심 흐름·390px 검증을 지연시키면 후속 개선으로 남긴다.
2. 판금류·부스바·명판의 실제 검사 항목 입력과 검사 활성화는 이번 기능 배포 후 품질팀 운영 작업으로 둔다.

### 제거

1. 범용 검사 엔진, OCR·scanner·협력사 Excel 연동은 이번 문제 해결에 필요하지 않다.
2. 기존 확정 IQC, PDF, 첨부, Pending, 재검사 이력을 변환하거나 재작성하지 않는다.
3. 새 구분 생성 화면에서 검사 정책까지 동시에 정하게 해 catalog 관리와 검사 정책 권한을 다시 섞는 방식은 권장하지 않는다.

## 3. Review Finding

### R1 — P1: 기존 `requires_iqc`와 신규 설정의 dual source 위험

Planning 후보 A는 신규 설정 table을 제안하지만 기존 `material_categories.requires_iqc`의 최종 역할을 명시하지 않았다. 두 값을 각각 수정할 수 있으면 구매 저장 안내, snapshot, 도착 분기가 서로 다른 값을 읽을 수 있다.

**Resolution 권고**: 신규 설정을 유일한 쓰기 source로 만들고 기존 컬럼은 migration backfill 입력 및 필요한 호환 조회 projection으로만 취급한다. Backend 저장·도착 분기 tests에서 두 번째 쓰기 경로가 없음을 검증한다.

### R2 — P1: 현행 구분 수정 API를 통한 권한 우회 위험

현재 구분 수정 API는 표시명·순서·활성 상태와 `requires_iqc`를 한 요청에서 함께 저장하고 품질부서 활성 사용자 전원에게 열려 있다. 새 양식 관리 화면만 부서장 권한으로 막아도 기존 API가 남으면 일반 품질 사용자가 검사 상태를 바꿀 수 있다.

**Resolution 권고**: 구분 metadata mutation과 IQC 설정 mutation을 분리한다. 일반 품질 사용자의 metadata 수정은 기존 검사 설정을 서버에서 그대로 보존하고, IQC 설정 mutation은 지정 품질 부서장+관리자만 허용한다. 403과 값 불변 회귀 test를 추가한다.

### R3 — P2: "검사 회차 시작"의 정확한 시스템 동작

현재 Detailed attempt는 도착 등록 시 생성되지만 template version은 품질 사용자가 성적서를 최초 초기화할 때 `IqcReportStore.InitializeAsync`에서 고정된다. Planning의 "회차 시작 시점"은 이 최초 성적서 생성 동작으로 해석해야 한다.

**Resolution 권고**: 구현·화면 문구·tests에서 `도착 등록 시 attempt 생성`, `성적서 최초 열기/생성 시 category template version 고정`을 구분한다. 단, 양식 부재 오류가 현장에서 늦게 발견되지 않도록 활성 상세형 설정은 항상 현재 양식 1개 이상을 보장한다.

## 4. 사용자 결정 필요 3건

Planning 16절의 선택지는 다음 조합을 권장한다.

1. **1A 권장** — 기존 구분 관리에서는 검사 상태를 조회 전용으로 표시하고 실제 변경은 양식 관리에서만 수행한다. 변경 위치와 권한을 하나로 만든다.
2. **2A 권장** — 신규 구분은 항상 `검사 없음`으로 생성한다. 지정 부서장이 양식 관리에서 방식·항목을 준비한 뒤 활성화한다.
3. **3A 권장** — 비활성 구분도 `비활성` badge와 함께 양식 관리에 표시하고 편집을 허용한다. 활성화 전에 검사 기준을 준비할 수 있다.

## 5. 권장 개발 순서

1. 신규 설정의 단일 source of truth와 권한 분리 계약을 migration·Backend tests로 먼저 고정한다.
2. 구분별 설정·양식 API와 감사·동시성·항목≥1 validation을 구현한다.
3. 구매품 방식 snapshot과 도착분 3-way 분기를 구현한다.
4. category id 기반 Detailed report 초기화와 기존 재검사 불변조건을 연결한다.
5. 기존 디자인을 재사용해 양식 관리 workspace와 기존 구분 관리의 조회 전용 표시를 구현한다.
6. fresh/existing migration, 권한, 비소급, 없음/스캔형/상세형, legacy, desktop·390px을 순서대로 검증한다.

## 6. 승인 요청 경계

구현을 시작하려면 사용자가 다음을 함께 승인해야 한다.

- Fable planning의 목표·포함·제외 범위
- 본 review의 R1~R3 resolution
- 4절의 선택 3건
- 구현 착수

Fable 재작성은 자동으로 수행하지 않는다. 사용자가 전문 재작성을 명시적으로 요청할 때만 별도 change와 승인 기록 후 진행한다.

---

- reviewStatus: `USER_RESOLUTION_REQUIRED`
- openP0: 0
- openP1: 2
- openP2: 1
- implementationApproved: false
