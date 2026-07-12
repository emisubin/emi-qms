# EMI 프로젝트 통합관리시스템 Product Roadmap

## 1. 문서 목적

이 문서는 EMI 프로젝트 통합관리시스템의 전체 개발 방향, 업무 프로세스, 확정사항, 미확정 추적 대상, 후속 TASK 우선순위를 한 곳에 정리하는 기준 문서다.

Codex와 개발자는 새 TASK를 시작하기 전에 이 문서를 먼저 읽고 다음을 확인해야 한다.

- 이번 TASK가 전체 18단계 업무 흐름 안에서 어떤 위치인지
- 이미 확정된 업무 원칙과 충돌하지 않는지
- 아직 미확정인 업무 규칙을 임의로 구현하고 있지는 않은지
- 기존 UAT DB, migration, 권한, 테스트 정책을 훼손하지 않는지
- 후속 TASK로 분리해야 할 범위를 현재 TASK에 포함하고 있지는 않은지

이 문서는 단순 요약본이 아니다. 실무진 결정사항, 사용자 검수 중 확정된 방향, 현재 구현된 기능, 앞으로 고쳐야 할 방향, 추적해야 할 의사결정 항목을 함께 관리한다.

업무 방향이 바뀌면 실제 코드 변경보다 이 문서를 먼저 업데이트해야 한다. 이 문서와 실제 구현이 충돌하면 Codex는 구현을 추측하지 말고 사용자에게 충돌 내용을 보고해야 한다.

확정사항과 미확정사항은 구분해서 관리한다.

- 확정사항: 후속 TASK에서 기본 전제로 사용한다.
- 미확정사항: 구현하지 않거나, 최소 구조만 준비하고 추적 대상에 남긴다.
- 임시 구현: 후속 TASK에서 정식 구현으로 대체할 수 있도록 문서에 명시한다.

본 시스템의 공식 명칭은 EMI 프로젝트 통합관리시스템이다. 이는 사용자 표시명과 문서상 명칭에 적용된다. 내부 코드명(Emi.Qms 솔루션/네임스페이스 등)은 별개이며 유지한다. 코드 네임스페이스나 솔루션명의 리네이밍은 수행하지 않는다.

### 1.1 목차

1. 문서 목적
2. 시스템 목적
3. 시스템 핵심 원칙
4. 18단계 표준 업무 프로세스
5. 부서별 역할과 담당자 구조
6. 내 업무 / 알림 / 긴급 알림 원칙 (알림 채널 기준 포함)
7. 프로젝트와 패널 관리 기준
8. QR 기준
9. 프로젝트 상태 집계 기준
10. 생산관리 기준
11. 구매 기준
12. 자재 기준
13. 품질 검사 기준
14. 제조 기준
15. Pending List 공통 모듈
16. 부적합 조치 흐름
17. 물류 기준
18. 영업 정산과 프로젝트 완료 기준
19. 첨부파일 / 사진 / PDF / Excel 기준
20. 로그인 / 권한 / 관리자 페이지 방향
21. 현재까지 개발된 기능
22. 현재 기능에서 수정해야 할 방향
23. 향후 개발 로드맵
24. 추적 대상 리스트
25. 결정 이력 (Decision Log)
26. 용어 사전
27. Codex 작업 시 유의사항

## 2. 시스템 목적

EMI 프로젝트 통합관리시스템은 단순 품질관리시스템이 아니라, 프로젝트 생성부터 납품 완료와 영업 정산까지 부서별 업무 흐름을 연결하는 시스템이다.

핵심 목적은 다음과 같다.

- 영업, 설계, 생산관리, 구매, 자재, 제조, 품질, 물류, 영업 정산 업무를 하나의 프로젝트 흐름으로 연결한다.
- 18단계 표준 업무 프로세스를 시스템 상태와 내 업무 흐름으로 관리한다.
- 각 단계 완료 시 다음 담당자의 내 업무를 자동 생성한다.
- 참조 대상자에게는 알림을 생성한다.
- 부적합, 고객사 PUNCH, 제조 중단, 필수 입력 누락 등 업무 차단 상황을 긴급/차단 알림과 Pending List로 관리한다.
- 제조현황을 종이 또는 구두 보고 중심에서 디지털 입력 중심으로 전환한다.
- 검사성적서를 웹 입력과 PDF 출력이 가능한 구조로 디지털화한다.
- 패널별 진행 상태를 추적한다.
- 구매품목별 입고, 검사, 입고 확정 상태를 추적한다.
- QR 기반으로 패널 단위 현장 추적을 지원한다.
- 포장, 출발, 납품 완료, 세금계산서 발행까지 프로젝트 완료 기준을 추적한다.
- 관리자 기준정보를 통해 Item, 생산계획 단계, 구매 필수 항목, 공휴일, 체크리스트, 역할을 관리할 수 있게 한다.
- Excel, PDF, 사진, 첨부파일을 업무 흐름 안에서 관리한다.

본 시스템은 각 부서 화면을 따로 만드는 것이 목표가 아니라, 부서 간 업무 인수인계와 책임 흐름을 데이터로 남기는 것이 목표다.

## 3. 시스템 핵심 원칙

### 3.1 업무 자동화 원칙

- 다음 단계 담당자에게 수동으로 요청하는 방식이 아니라 시스템이 자동으로 내 업무를 생성한다.
- 단계 완료 event가 발생하면 workflow 기준으로 다음 단계와 담당자를 계산한다.
- 참조 대상자에게는 처리 업무가 아닌 알림을 생성한다.
- 부적합, PUNCH, 제조 중단, 재검사 요청은 긴급/차단 알림으로 관리한다.
- 동일 이벤트가 재실행되어도 같은 내 업무나 알림이 중복 생성되지 않아야 한다.
- 중복 방지는 `idempotency_key` 또는 동등한 기준으로 처리한다.

### 3.2 데이터 단위 원칙

업무별 적정 입력 단위는 다르다. 화면을 만들 때 프로젝트 단위로만 단순화하지 않는다.

| 업무 영역 | 기본 입력 단위 | 설명 |
| --- | --- | --- |
| 프로젝트 생성 | 프로젝트 | 고객사, Item, PJT Code, PJT Title, 면수, 납기일, 영업담당자, 포장방식 |
| 패널 정보 | 패널 | 패널명, 사이즈, QR 가능 여부 |
| 생산계획 | 프로젝트 | Item 기준 생산단계, 예정일, 담당자 지정 |
| 구매정보 | 구매품목 | 발주품목, 업체, 기술 담당자, 발주일, 입고예정일, 이슈, 입고 완료 |
| 자재 도착 | 구매품목 | 구매품목별 도착 등록 |
| 입고 확정 | 구매품목 | 수입검사 적합 후 사용 가능 자재로 확정 |
| 키팅 완료 | 패널 | 제조 투입 가능 상태 |
| 제조 작업 | 패널 및 제조 단계 | 작업 시작, 작업 종료, 제조 중단 |
| 검사 | 검사 단위 및 패널 | IQC, LQC, OQC, 전진검수, FAT |
| Pending List | 이슈 단위 | 부적합, PUNCH, 제조 중단, 기타 |
| 포장 | 포장 단위 및 패널 | 포장번호, 포함 패널, 사진 |
| 납품 | 패널 및 프로젝트 | 출발, 납품 완료, 거래명세서 |
| 영업 정산 | 프로젝트 | 세금계산서 발행, 프로젝트 완료 |

### 3.3 이력 보존 원칙

- 모든 입력과 수정 이력은 저장한다.
- 일반 사용자는 업무 수행에 필요한 이력만 본다.
- 관리자 이력은 별도 관리자 기능으로 제공한다.
- 업무 생성, 시작, 완료 이력은 후속 관리자 페이지에서 추적 가능해야 한다.
- 승인 또는 완료된 기록을 직접 덮어쓰거나 삭제하지 않는다.
- 정정이 필요한 경우 변경 전/후 값, 사유, 변경자, 변경시각을 남긴다.
- Excel import도 원본 파일 자체를 무조건 저장하는 것이 아니라, import batch와 적용 결과, 오류 행, 변경 내용을 추적한다.

### 3.4 모바일 입력 원칙

- 현장 입력은 휴대폰에서 체크 클릭 중심으로 설계한다.
- 사진 촬영과 첨부가 가능해야 한다.
- PC는 관리, 조회, 일괄 편집, Excel/PDF 중심이다.
- 모바일은 현장 입력, 체크, 사진, 간단한 완료 처리 중심이다.
- 모바일에서 page-level horizontal overflow가 발생하면 안 된다.
- PC table이 필요한 화면도 모바일에서는 card 또는 단계별 입력 UI로 전환한다.

### 3.5 권한 원칙

- 입력 가능 부서만 수정할 수 있다.
- 나머지 부서는 조회만 가능하다.
- 권한 검사는 UI 숨김으로 끝내지 않고 서버 Policy에서 강제한다.
- System Administrator도 업무 입력을 무제한 우회하지 않는다.
- 관리자는 이력 조회, 기준정보 관리, 사용자 관리 역할이 중심이다.
- 개발 사용자 기능은 UAT/개발용이며 운영 로그인 전환 후 비활성화한다.

## 4. 18단계 표준 업무 프로세스

18단계 업무 프로세스는 현재 시스템의 표준 흐름이다. 화면 구현 순서와 workflow 표시 순서는 이 기준과 일치해야 한다.

특히 2번은 생산관리, 3번은 설계다.

| 번호 | 담당 부서 | 단계명 | 입력 단위 | 주요 입력 | 완료 기준 | 다음 내 업무 | 참조 알림 | 비고 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 영업 | 프로젝트 생성 | 프로젝트 | 고객사, Item, PJT Code, PJT Title, 면수, 납기일, 영업담당자, 포장방식, FAT 필요 여부 | 필수 프로젝트 정보 생성 | 생산관리: 생산계획·담당자 입력 | 관련 부서 프로젝트 생성 참조 | FAT 필요 여부는 프로젝트별 선택값 |
| 2 | 생산관리 | 생산계획·담당자 | 프로젝트 | 생산계획 단계, 예정일, 영업/설계/생산관리/구매/자재/제조/물류 정·부 담당자, 품질 단계별 담당자 | 필수 생산계획 예정일 및 필수 담당자 기준 충족 | 설계: 패널명·사이즈 입력, 구매: 구매정보 입력 가능 | 영업, 구매, 제조 참조 | 생산계획 skeleton은 새 프로젝트 생성 시 자동 생성 |
| 3 | 설계 | 패널명·사이즈 | 패널 | 패널명, 사이즈 | 활성 패널의 필수 패널 정보 입력 | 구매: 구매정보 입력 | 생산관리 참조 | 목포장 프로젝트는 사이즈 필수 |
| 4 | 구매 | 구매정보 | 구매품목 | 발주품목, 업체/기술 담당자, 발주일, 입고예정일, 이슈 | Item별 필수 구매 항목의 실제 입력 완료 | 자재: 자재 도착 등록 | 생산관리, 제조 참조 | 자동 생성 row만으로 완료 처리하지 않음 |
| 5 | 자재 | 자재 도착 | 구매품목 | 도착 여부, 도착일, 수량, 비고 | 구매품목 도착 등록 | 품질: 수입검사 입력 | 구매, 생산관리 참조 | 자재 도착 후 IQC 요청 |
| 6 | 품질 | 수입검사 | 구매품목 또는 패널 | IQC 체크, 값 입력, 외함 사진, 적합/부적합 | IQC 적합 또는 Pending 등록 | 자재: 입고 확정 | 구매, 생산관리 참조 | 부적합 시 Pending List |
| 7 | 자재 | 입고 확정 | 구매품목 | 입고 확정, 사용 가능 처리 | IQC 적합품 입고 확정 | 자재: 키팅 완료 | 생산관리 참조 | 구매품목 단위 |
| 8 | 자재 | 키팅 완료 | 패널 | 키팅 완료 여부, 부분/일괄 처리 | 제조 투입 가능 상태 | 제조: 제조 작업 입력 | 생산관리, 제조 참조 | 별도 생산 불출 단계 없음 |
| 9 | 제조 | 제조 작업 | 패널 및 제조 단계 | 작업 시작, 작업 체크, 작업 종료, 중단 사유 | 제조 단계 작업 완료 | 품질: LQC 입력 | 생산관리 참조 | 제조 중단은 Pending List |
| 10 | 품질 | LQC | 패널 또는 검사 단위 | LQC 체크리스트, 값, 사진, 적합/부적합 | LQC 적합 또는 Pending 등록 | 제조: 제조 완료 입력 | 제조, 생산관리 참조 | 상세 양식 회신 대기 |
| 11 | 제조 | 제조 완료 | 패널 | 제조 완료 체크, 완료일 | 제조 완료 처리 | 품질: 자체검수 입력 | 생산관리 참조 | 완료 후 OQC로 연결 |
| 12 | 품질 | 자체검수 | 패널 또는 검사 단위 | OQC 체크리스트, 값, 사진, 적합/부적합 | 자체검수 적합 또는 Pending 등록 | 품질: 전진검수 입력 | 제조, 생산관리 참조 | 상세 양식 회신 대기 |
| 13 | 품질 | 전진검수 | 프로젝트 및 패널 | 전진검수 결과, PUNCH LIST | 전진검수 완료 또는 Pending 등록 | 품질: FAT 또는 물류: 포장 완료 | 영업, 생산관리 참조 | PUNCH는 패널 단위 가능 |
| 14 | 품질 | FAT 선택 | 프로젝트 및 패널 | FAT 결과, 고객 확인, PUNCH LIST | FAT 필요 시 완료 또는 Pending 등록 | 물류: 포장 완료 | 영업, 생산관리 참조 | 선택 단계. FAT 불필요 프로젝트는 제외 |
| 15 | 물류 | 포장 완료 | 포장 단위 및 패널 | 포장번호, 포함 패널, 포장사진 | 포장 사진 포함 완료 | 물류: 출발 처리 | 영업, 생산관리 참조 | Packing Unit 필요 |
| 16 | 물류 | 출발 처리 | 패널 | 상차 여부, 상차 사진, 출발일 | 출발 처리 완료 | 물류: 납품 완료 | 영업 참조 | 출발 사진 필수 |
| 17 | 물류 | 납품 완료 | 패널 및 프로젝트 | 납품 완료, 거래명세서 서명본 | 납품 완료 증빙 등록 | 영업: 세금계산서·완료 처리 | 영업, 생산관리 참조 | “출하완료” 대신 “납품완료” |
| 18 | 영업 | 세금계산서·완료 | 프로젝트 | 세금계산서 발행, 완료 체크 | 모든 패널 납품 및 세금계산서 발행 | 없음 | 관련 부서 완료 알림 | 최종 프로젝트 완료 |

## 5. 부서별 역할과 담당자 구조

품질을 제외한 부서는 프로젝트별 정/부 담당자 2명을 가진다.

- 영업 정/부
- 설계 정/부
- 생산관리 정/부
- 구매 정/부
- 자재 정/부
- 제조 정/부
- 물류 정/부

품질은 검사 단계별 정/부 담당자를 가진다.

- IQC 수입검사 정/부
- LQC 정/부
- OQC 자체검수 정/부
- 전진검수/FAT 정/부

품질 담당자는 각 검사 단계별로 정담당자와 부담당자를 지정할 수 있다. 같은 사용자가 여러 품질 단계에 중복 지정될 수 있고, 실무 정책상 필요하면 같은 사용자가 정담당자와 부담당자로 중복 지정되는 것도 허용할 수 있다. 품질 부담당자는 정담당자가 부재하거나 비활성일 때 fallback 대상으로 사용된다.

DB 원칙은 다음과 같다.

- 부서별 담당자 테이블을 여러 개 만들지 않는다.
- `project_assignees` 또는 동등한 단일 테이블에서 `responsibility_type`으로 구분한다.
- 품질도 같은 테이블에서 `responsibility_type`으로 구분한다.
- 담당자 변경 이력은 field-level audit 또는 project workflow event로 추적한다.

responsibility_type 예시는 다음과 같다.

| responsibility_type | 사용자 표시명 | 설명 |
| --- | --- | --- |
| SalesPrimary | 영업 정담당자 | 프로젝트 주 영업 담당 |
| SalesSecondary | 영업 부담당자 | 영업 참조 및 fallback |
| DesignPrimary | 설계 정담당자 | 패널명·사이즈 입력 담당 |
| DesignSecondary | 설계 부담당자 | 설계 참조 및 fallback |
| ProductionPlanningPrimary | 생산관리 정담당자 | 생산계획·담당자 입력 담당 |
| ProductionPlanningSecondary | 생산관리 부담당자 | 생산관리 참조 및 fallback |
| ProcurementPrimary | 구매 정담당자 | 구매정보 입력 담당 |
| ProcurementSecondary | 구매 부담당자 | 구매 참조 및 fallback |
| MaterialsPrimary | 자재 정담당자 | 자재 도착, 입고 확정, 키팅 담당 |
| MaterialsSecondary | 자재 부담당자 | 자재 참조 및 fallback |
| ManufacturingPrimary | 제조 정담당자 | 제조 작업, 제조 완료 담당 |
| ManufacturingSecondary | 제조 부담당자 | 제조 참조 및 fallback |
| LogisticsPrimary | 물류 정담당자 | 포장, 출발, 납품 담당 |
| LogisticsSecondary | 물류 부담당자 | 물류 참조 및 fallback |
| QualityIQC | IQC 정담당자 | 수입검사 담당 |
| QualityIQCSecondary | IQC 부담당자 | 수입검사 참조 및 fallback |
| QualityLQC | LQC 정담당자 | LQC 담당 |
| QualityLQCSecondary | LQC 부담당자 | LQC 참조 및 fallback |
| QualityOQC | OQC 정담당자 | 자체검수 담당 |
| QualityOQCSecondary | OQC 부담당자 | 자체검수 참조 및 fallback |
| QualityCustomerInspection | 전진검수/FAT 정담당자 | 전진검수 및 FAT 담당 |
| QualityCustomerInspectionSecondary | 전진검수/FAT 부담당자 | 전진검수 및 FAT 참조 및 fallback |

담당자 fallback 규칙은 다음 확정 순서를 따른다.

1. 해당 단계 Primary 또는 품질 단계 정담당자
2. 해당 단계 Secondary 또는 품질 단계 부담당자
3. 영업 정(SalesPrimary)
4. 영업 부(SalesSecondary)
5. System Administrator

품질 단계 fallback 예시는 다음과 같다.

- IQC 단계: QualityIQC → QualityIQCSecondary → SalesPrimary → SalesSecondary → System Administrator
- LQC 단계: QualityLQC → QualityLQCSecondary → SalesPrimary → SalesSecondary → System Administrator

fallback으로 결정된 경우 알림 또는 업무 설명에 담당자 누락 정보가 포함될 수 있다.

## 6. 내 업무 / 알림 / 긴급 알림 원칙 (알림 채널 기준 포함)

### 6.1 내 업무

내 업무는 사용자가 실제로 처리해야 하는 업무다.

예시는 다음과 같다.

- 생산계획, 담당자 입력
- 패널명, 사이즈 입력
- 구매정보 입력
- 자재 도착 등록
- 수입검사 입력
- 입고 확정 입력
- 키팅 완료 입력
- 제조 작업 입력
- LQC 입력
- 자체검수 입력
- 전진검수 입력
- FAT 입력
- 포장 완료 입력
- 출발 처리 입력
- 납품 완료 입력
- 세금계산서, 완료 처리

내 업무는 workflow stage, project, assigned user, responsibility type, status를 가져야 한다.

상태 예시는 다음과 같다.

- 시작 전
- 진행 중
- 완료
- 취소

### 6.2 참조 알림

참조 알림은 사용자가 직접 처리할 필요는 없지만 알아야 하는 정보다.

예시는 다음과 같다.

- 프로젝트 생성 참조
- 담당자로 지정됨
- 생산계획 완료 참조
- 구매정보 입력 완료 참조
- 자재 도착 참조
- 제조 시작 또는 완료 참조
- 납품 완료 참조

참조 알림은 읽음/읽지 않음 상태를 관리한다. 알림 페이지는 프로젝트별로 묶고, 각 프로젝트 안에서는 최신 알림 순서로 표시한다.

### 6.3 긴급/차단 알림

긴급/차단 알림은 업무 진행이 막히는 상황을 알린다.

예시는 다음과 같다.

- 부적합 발생
- 고객사 PUNCH LIST 발생
- 제조 중단
- 필수 입력 누락
- 재검사 요청
- 납품 차단

긴급/차단 알림은 Pending List와 연결된다.

### 6.4 자동 생성 원칙

- 단계 완료 시 다음 담당자에게 자동 내 업무를 생성한다.
- 참조 대상자에게 참조 알림을 생성한다.
- 부적합, 중단, PUNCH는 Pending List 생성과 긴급/차단 알림을 함께 고려한다.
- 기한은 기본값 `null`로 둔다.
- 동일 이벤트 재실행으로 중복 업무가 생기면 안 된다.

### 6.5 알림 채널 기준

인앱 알림과 내 업무 기능은 이미 구현되어 있다. 인앱이 모든 알림의 원본이며, Teams와 메일은 인앱 위에 추가되는 채널이다.

#### 6.5.1 채널별 역할 정의

| 채널 | 역할 | 정의 |
| --- | --- | --- |
| 인앱 | 기록 (Record) | 모든 알림의 원본. 빠짐없이 전부 남는다 |
| Teams | 개입 (Interrupt) | 지금 하던 일을 멈추고 봐야 하는 것만 발송 |
| 메일 | 요약/증빙 (Digest & Evidence) | 일일 요약 + 긴급/에스컬레이션 실시간 발송 |

- Teams는 실시간 채널, 메일은 배치 채널이 기본이다.
- 같은 알림을 Teams와 메일로 동시에 실시간 발송하는 조합은 긴급/차단과 에스컬레이션에만 허용한다.

#### 6.5.2 알림 유형 × 채널 매트릭스

이 표의 Teams 열은 event coverage 상태다. 아래의 provider/capability 완료 여부와 개별 event의 자동 연결 여부를 혼동하지 않는다.

| 알림 유형 | 인앱 | Teams | 메일 |
| --- | --- | --- | --- |
| 내 업무 생성 (일반 단계 핸드오프) | 즉시 | 수동 업무 배정은 적용, 자동 단계 핸드오프는 후속 | 일일 요약에 포함 |
| 참조 알림 | 즉시 | 발송 안 함 | 일일 요약에 포함 |
| 긴급/차단 (부적합, PUNCH, 제조 중단) | 즉시 | 통합 채널 게시 기반은 구현, 개인 자동 Activity Feed는 후속 | 즉시 (조치 담당자 + 생산관리) |
| 재검사 요청 | 즉시 | Activity Feed event 연결 미확인 | 발송 안 함 |
| 예정일 임박 (D-1) | 즉시 | L0는 설정 선택 시 Activity Feed, 기본 설정은 dry-run DM | 발송 안 함 |
| 예정일 초과 | 즉시 | L1/L2는 설정 선택 시 Activity Feed, 기본 설정은 dry-run DM | 에스컬레이션 단계에서만 |
| 일일 요약 | — | 발송 안 함 | 매일 1통 |
| 프로젝트 완료 / 세금계산서 단계 | 즉시 | Activity type/renderer 기반만 존재하며 event 연결 미확인 | 발송 (증빙 성격) |

- 메일이 실시간으로 발송되는 경우는 긴급/차단과 에스컬레이션 두 가지뿐이다.
- 긴급/차단 알림의 Teams 채널 게시는 부서별 채널이 아니라 통합 채널 1개로 운영한다.

#### 6.5.2.1 Activity Feed provider/capability 상태

| capability | 상태 | 근거/주의 |
| --- | --- | --- |
| Graph Activity Feed provider 및 channel handler | 완료 | TASK-NOTIFY-003에서 actual 발송을 검수했다 |
| text topic + Teams deep link | 완료 | 사용자별 installedAppId 운영 의존을 제거했다 |
| recipient/access scope와 notification 연결 | 완료 | 개인 알림은 RecipientOnly, 채널 공지는 Authenticated 정책을 사용한다 |
| `/teams/activity` 및 상세 route | 완료 | Teams tab과 인앱 notification/detail을 연결한다 |
| 관리자 수동 개인/업무 배정 Activity Feed | 완료 | 선택한 EntraId 사용자별 Pending delivery를 생성한다 |
| 자동 event 전체 적용 | 부분 적용 | provider가 처리할 수 있는 것과 event가 실제 TeamsActivity delivery를 생성하는 것은 별도다 |

#### 6.5.2.2 Activity Feed event coverage 상태

| event | 상태 | 현재 기준 |
| --- | --- | --- |
| 관리자 수동 개인 알림 | 적용 | TeamsActivity 채널을 선택한 경우 |
| 관리자 수동 업무 배정 | 적용 | work_item, notification, recipient, TeamsActivity delivery를 연결한다 |
| L0/L1/L2 예정일 에스컬레이션 | 부분 적용 | `TeamsPersonalChannelStrategy=TeamsActivity`일 때 연결된다. repository 기본값은 `TeamsDirectMessageDryRun`이다 |
| 긴급/차단 자동 event | 후속 | renderer activity type만으로 자동 event 연결 완료를 의미하지 않는다 |
| 재검사 요청 | 미확인 | 현재 코드·문서에서 TeamsActivity delivery 생성 연결을 확인하지 못했다 |
| 자동 단계 핸드오프 업무 생성 | 후속 | 수동 업무 배정 적용과 구분한다 |
| 프로젝트 완료 | 미확인 | activity type/renderer는 존재하지만 실제 event 연결은 확인하지 못했다 |

후속 event coverage는 해당 event의 notification 원본, recipient, delivery 생성 경로와 테스트를 함께 확인한 뒤 상태를 올린다.

#### 6.5.3 일일 요약 메일

- 발송 시각: 매일 07:30
- 수신자별 개인화 1통
- 구성:
  1. 내 미완료 업무 (예정일 순 정렬, 예정일 초과 건은 상단 강조)
  2. 어제 새로 생성된 내 업무
  3. 내가 조치 담당인 오픈 Pending
  4. 참조 알림 요약 (제목 목록)
  5. 각 항목에 시스템 딥링크 포함
- 보낼 내용이 하나도 없으면 발송하지 않는다. 빈 메일은 금지한다.

#### 6.5.4 에스컬레이션 규칙

예정일 초과와 긴급 알림 미조치에 적용한다.

| 단계 | 조건 | 발송 대상과 채널 |
| --- | --- | --- |
| L0 | 예정일 D-1 | 정담당자 Teams 개인 delivery. 설정 선택 시 Activity Feed, 기본은 dry-run DM |
| L1 | 예정일 초과 즉시 | 정담당자 Teams 개인 delivery + 메일. Teams는 설정 선택 시 Activity Feed |
| L2 | 초과 +2영업일 미조치 | 부담당자 + 생산관리 담당자 Teams 개인 delivery. 설정 선택 시 Activity Feed |
| L3 | 초과 +3영업일 미조치 | 생산관리 담당자 + 영업 담당자 메일 |

- 긴급/차단 알림은 L1에서 시작한다. 발생 즉시가 이미 긴급 상황이기 때문이다.
- 미조치 판정 기준: 해당 내 업무 또는 Pending의 상태 변경 이벤트가 없는 경우.
- 영업일 계산에는 기존에 구현된 공휴일/국경일 데이터를 재사용한다.
- L3 수신자는 생산관리와 영업으로 한정한다. 부서장/경영진 수신은 포함하지 않는다.

#### 6.5.5 소음 방지 규칙

- 중복 억제: 동일 대상(같은 업무/Pending)에 대한 동일 유형 알림은 24시간 내 재발송하지 않는다. 에스컬레이션 단계 상승은 예외다.
- 일괄 처리 묶음: 일괄 처리(예: 패널 10건 일괄 키팅 완료)로 발생한 알림은 개별 발송하지 않고 1건으로 묶어 발송한다. 이벤트 발행 후 1~2분 버퍼로 그룹핑한다.
- 야간 억제는 적용하지 않는다. Teams 개인별 알림은 발생 시각과 무관하게 즉시 발송하는 방향을 기준으로 한다.

#### 6.5.6 구현 방향

- Teams 통합 채널 게시는 Webhook 기반으로 구현한다. Webhook payload는 Power Automate Teams 카드 액션과 호환되는 Adaptive Card root JSON을 기본으로 한다.
- Teams 개인별 알림은 DM보다 Activity Feed를 우선 사용한다. Activity Feed provider/capability는 Teams 앱 manifest, Graph 권한, 조직 앱 배포, Teams deep link를 포함해 TASK-NOTIFY-003에서 actual 발송까지 검증했다. 개별 자동 event coverage는 6.5.2.2 표를 따른다.
- 메일: 초기/UAT/시범운영 actual 발송은 Gmail 전용 계정 SMTP를 사용한다. Gmail 계정은 2단계 인증과 앱 비밀번호를 사용하며 실제 값은 env/secret으로만 관리한다.
- Hiworks SMTP와 Microsoft Graph Mail.Send는 사내 정책상 기본 발송 경로로 사용하지 않는다. Graph Mail provider는 Exchange Online 조직 또는 후속 선택지로 optional 유지한다.
- 아키텍처: 도메인 이벤트 발행 → NotificationDispatcher → 채널별 핸들러(InApp / Teams / Mail). 인앱은 이미 구현되어 있으므로 Dispatcher 뒤에 Teams/Mail 핸들러를 추가하는 형태로 확장한다. 18단계 각 단계에 알림 로직을 하드코딩하지 않는다.
- 발송 이력 테이블(`notification_deliveries` 또는 동등 명칭)을 둔다. 항목은 알림 ID, 채널, 수신자, 발송 시각, 성공/실패, 재시도 횟수다. 에스컬레이션의 미조치 판정과 중복 억제는 이 테이블에 의존한다.
- 실패 처리: Teams/메일 발송이 실패해도 업무 흐름은 진행한다. 인앱이 원본이기 때문이다. 실패 건은 재시도 3회, 최종 실패는 관리자 페이지에서 확인 가능해야 한다.
- `Sent`는 외부 provider 또는 Webhook endpoint가 요청을 수락했다는 의미다. 실제 Teams 화면 표시나 메일함 도착 여부는 provider 특성에 따라 사용자 수동 검수 또는 관리자 추적으로 확인한다.

#### 6.5.7 단계적 적용

- Phase 1: 외부 delivery 계층, Teams 통합 채널 Webhook, Gmail SMTP 메일, 일일 요약 구조, retry/dedupe/batch 기반
- Phase 2: Teams Activity Feed 개인별 알림 (TASK-NOTIFY-003)
- Phase 3: 에스컬레이션 자동화 (L0 ~ L3, TASK-NOTIFY-002)

## 7. 프로젝트와 패널 관리 기준

프로젝트는 영업이 생성한다.

프로젝트 생성 시 기본 입력은 다음과 같다.

- 고객사
- Item
- PJT Code
- PJT Title
- 면수
- 납기일
- 영업담당자
- 포장방식
- 판매금액
- 통화
- 납품장소
- FAT 필요 여부

Item 기준값은 다음과 같다.

- UL67
- UL891
- UL508A
- IEC
- LLP
- RPP

과거 오기 값인 RRP는 잘못된 명칭이며 RPP로 보정한다. 사용자 화면, DB 기준값, Excel template, Excel parser, 기존 데이터 보정 모두 RPP 기준으로 통일한다.

패널 기준은 다음과 같다.

- 패널명, 사이즈는 설계가 입력한다.
- 패널명 입력 시 시스템상 QR 생성 가능 상태가 된다.
- 목포장 프로젝트는 사이즈 입력이 필수다.
- 패널정보 완료 여부와 QR 가능 여부는 별도 표시한다.
- 실제 작업 진행은 패널별로 관리한다.
- 프로젝트 목록과 상세의 workflow 진행 상태는 9장 프로젝트 상태 집계 기준을 따른다.

현재 구현된 관리 상태와 workflow 상태 표시 우선순위는 다음과 같다. 장기적인 프로젝트 대표 상태 집계는 9장 기준으로 확장한다.

우선순위:

1. Cancelled: 취소
2. OnHold: 보류
3. Completed: 완료
4. Active: workflow 기준 현재 단계 표시

프로젝트 진행률은 완료된 필수 workflow 단계 수 / 전체 필수 workflow 단계 수 × 100으로 계산한다. FAT 필요 프로젝트는 FAT 단계를 분모에 포함하고, FAT 불필요 프로젝트는 FAT 단계를 분모에서 제외한다. 가중치 방식은 현재 기준이 아니며, 필요 시 후속 개선사항으로만 검토한다.

## 8. QR 기준

QR 기준은 시스템 생성 기준과 현장 부착 기준을 구분한다.

### 8.1 시스템상 QR 생성 가능 기준

- 프로젝트가 Active
- 프로젝트가 deleted 아님
- 패널이 Active
- 패널명 존재

생산계획, IQC 결과, 현장 부착 여부는 시스템상 QR 생성 가능 조건에 포함하지 않는다.

### 8.2 현장 QR 부착 기준

현장 운영 기준은 다음과 같다.

1. 자재팀이 외함 첫 입고 시 Product Tag를 부착한다.
2. 품질팀이 IQC 적합 판정 후 Product Tag 위에 QR을 부착한다.
3. IQC 불합격 시 QR을 부착하지 않는다.

### 8.3 QR 활성/비활성 기준

- QR은 한 패널당 하나만 발급한다.
- QR에는 민감정보를 직접 넣지 않는다.
- QR 활성은 생성 후 유지한다.
- 비활성화가 필요하다면 프로젝트 완료 후 별도 정책으로 처리한다.
- QR 기준 변경은 후속 TASK에서 명시 요청이 있을 때만 수행한다.

## 9. 프로젝트 상태 집계 기준

### 9.1 기본 원칙

- 프로젝트 상태는 사용자가 직접 입력하는 값이 아니라 서버가 계산하는 값이다.
- 어떤 사용자도 프로젝트 상태를 직접 변경할 수 없다.
- 원천 데이터(패널별 단계 상태, 구매품목별 상태, 검사 결과, Pending 상태)에서 서버가 도출한다.

### 9.2 상태의 3층 구조

| 층 | 상태 | 설명 |
| --- | --- | --- |
| 1층 | 원천 데이터 | 패널별 단계 상태, 구매품목별 상태, 검사 결과, Pending 상태. 모든 실제 입력은 이 층에서만 발생한다. |
| 2층 | 패널 상태 | 각 패널이 18단계 중 어디까지 진행됐는지. 패널의 현재 단계는 완료되지 않은 가장 이른 필수 단계다. |
| 3층 | 프로젝트 상태 | 패널 상태들의 집계값이다. |

### 9.3 프로젝트 대표 단계 규칙

- 프로젝트 목록 화면의 대표 상태는 병목 기준으로 표시한다. 즉 가장 뒤처진 패널의 단계가 프로젝트의 대표 단계다.
- 대표 단계와 함께 진행률(%)을 병기한다.
- 프로젝트 상세 화면에서는 단계별 패널 분포 매트릭스(어느 단계에 몇 개 패널이 있는지)를 표시한다.
- 진행률은 완료된 필수 workflow 단계 수 / 전체 필수 workflow 단계 수 × 100으로 계산한다.
- FAT 필요 프로젝트는 FAT 단계를 분모에 포함하고, FAT 불필요 프로젝트는 FAT 단계를 분모에서 제외한다.
- 가중치 방식은 현재 기준이 아니다.

### 9.4 단계 범위별 판정 기준

- 1~4단계(영업/생산관리/설계/구매정보)는 프로젝트 단위 단계다. 패널 집계와 무관하게 프로젝트 자체 속성으로 판정한다.
- 패널 단위 집계는 5단계(자재 도착) 이후부터 적용한다.
- 구매품목 단계(4~8)와 패널 단계(9~17)의 연결: 패널이 9단계(제조 작업)에 진입 가능한 조건은 해당 패널의 키팅 완료다. 자재/구매 흐름은 패널 상태에 "키팅 완료 여부"라는 게이트로만 반영된다.
- FAT 미대상 프로젝트는 14단계를 필수 단계 목록과 집계에서 제외한다. 필수 단계 목록은 프로젝트별로 다를 수 있다.

### 9.5 재검사와 차단 원칙

- 단계는 전진만 한다. 부적합 발생 시 패널의 단계 번호를 되돌리지 않는다.
- 대신 해당 패널에 차단(blocked) 플래그를 세운다. 차단 플래그는 Pending List와 연동된다.
- 재검사 적합 시 차단 플래그를 해제한다.
- 단계 번호가 전진/후퇴를 반복하면 이력 해석이 불가능해지므로 이 원칙은 변경하지 않는다.

### 9.6 Pending 오픈 상태의 표시

- 오픈 Pending이 있는 프로젝트는 상태 옆에 경고 배지와 오픈 Pending 건수를 표시한다.
- 프로젝트 상태값 자체를 "중단"으로 바꾸지 않는다. 일부 패널은 정상 진행 중일 수 있기 때문이다.

### 9.7 프로젝트 완료 조건

서버가 판정 가능한 조건식은 다음과 같다.

```text
프로젝트 완료 =
  모든 패널의 납품 완료 == true
  AND 세금계산서 발행 완료 체크 == true
  AND 오픈 상태 Pending == 0건
```

- 미종결 PUNCH나 부적합이 남아 있으면 프로젝트를 완료 처리할 수 없다.

## 10. 생산관리 기준

생산관리의 역할은 다음과 같다.

- 생산계획 입력
- 프로젝트 담당자 지정
- Item별 생산계획 단계 설정
- Pending List 관리
- 전체 진행상황 관리

생산계획 기준은 다음과 같다.

- 프로젝트 단위로 관리한다.
- Item 기준으로 생산계획 단계가 자동 생성된다.
- Item별 생산계획 단계 설정이 가능하다.
- Item별 설정은 최신 설정 1개만 유지한다.
- V1, V2처럼 version을 사용자 화면에 누적 표시하지 않는다.
- 설정 변경 이후 새 프로젝트에만 자동 반영한다.
- 기존 프로젝트에는 자동 반영하지 않는다.
- 프로젝트별 생산계획에서는 단계명과 필수 여부를 수정할 수 있다.
- 프로젝트별 수정은 해당 프로젝트 snapshot에만 적용된다.
- master template에는 영향이 없다.
- 생산계획 항목은 예정일 빠른 순으로 정렬한다.
- 프로젝트 상세 생산관리 section에 생산계획표와 캘린더를 표시한다.
- 생산계획 캘린더는 생산단계 열 sticky, 날짜 열 고정 폭을 유지한다.
- 생산관리 목록 펼침에는 캘린더를 표시하지 않는다.

생산계획 완료 기준은 다음과 같다.

- 필수 단계의 예정일이 모두 입력되어야 완료다.
- 일부만 입력되면 진행 중이다.
- 날짜가 전혀 없으면 미등록 또는 대기 상태로 본다.
- 담당자 지정도 workflow 완료 판정에 포함될 수 있으나, 구체 필수 담당자 기준은 TASK별로 명시한다.

## 11. 구매 기준

구매정보 입력 단위는 구매품목이다.

구매 화면 기준은 다음과 같다.

- 구매 페이지는 프로젝트 단위로 묶어 표시한다.
- 구매정보에는 업체 헤더가 필요하다.
- 구매 필수 항목은 Item별로 설정할 수 있다.
- 구매 필수 항목 설정은 최신 설정 1개만 유지한다.
- V1, V2처럼 version을 사용자 화면에 누적 표시하지 않는다.
- 설정 변경 이후 새 프로젝트에만 자동 반영한다.
- 기존 프로젝트에는 자동 반영하지 않는다.
- 새 프로젝트 생성 시 Item에 맞는 구매 필수 항목 skeleton row를 자동 생성한다.
- 자동 생성 row만으로 구매 단계 완료 처리하지 않는다.
- 구매 담당자가 실제 정보를 입력하거나 확인해야 완료 판정에 반영한다.

구매 완료 판정 기준은 다음과 같다.

- 해당 Item에 active required procurement item setting이 있으면 필수 구매 항목이 모두 실제 입력/확정되어야 완료다.
- 일부만 입력되면 진행 중이다.
- 설정이 없으면 기존 구매정보 완료 판정 정책을 따른다.
- 선택 항목 미입력은 완료에 영향이 없다.

구매 Excel 기준은 다음과 같다.

- 같은 파일명이어도 업로드 가능하다.
- 같은 hash라도 현재 웹 데이터와 비교해 변경분이 있으면 저장 가능하다.
- 웹에서 정보를 수정한 후 같은 Excel을 다시 업로드해도 현재 DB와 비교해 변경분을 판단한다.
- 변경분이 없으면 오류가 아니라 변경 없음으로 표시한다.
- filename/hash는 중복 차단 기준이 아니라 audit metadata로 사용한다.
- Preview와 Apply는 모두 현재 DB 기준으로 diff를 재계산해야 한다.

## 12. 자재 기준

자재 흐름은 다음 네 단계로 본다.

1. 자재 도착
2. IQC 요청
3. 입고 확정
4. 키팅 완료

자재 도착 기준:

- 구매품목 단위로 입력한다.
- 담당 부서는 자재다.
- 물류 또는 구매는 참조 알림 대상이 될 수 있다.
- 도착 등록 후 IQC 요청으로 연결한다.

입고 확정 기준:

- IQC 적합 후 자재가 입력한다.
- 입고 확정은 사용 가능한 자재가 되었음을 의미한다.
- 부적합품은 Pending List로 연결한다.

키팅 완료 기준:

- 패널 단위로 관리한다.
- 일괄 키팅과 부분 키팅을 모두 고려한다.
- 키팅 완료 시 제조팀 내 업무를 생성한다.
- 1차 시스템에서는 별도 생산 불출 단계를 두지 않는다.
- 키팅 완료는 제조 투입 가능 상태를 의미한다.

## 13. 품질 검사 기준

검사성적서는 디지털화하는 것으로 확정됐다.

공통 원칙:

- 검사성적서를 웹사이트에 적용한다.
- 휴대폰에서 체크 클릭과 값 입력이 가능해야 한다.
- 사진 등록이 가능해야 한다.
- 검사성적서 PDF 출력이 필요하다.
- PDF 출력 양식은 회신 대기 상태다.
- 사진 필수 항목은 사진 미첨부 시 저장 불가다.
- 오류는 해당 항목 아래에 한글로 표시한다.
- 장기적으로 체크리스트 항목에 `requires_photo` 속성을 둘 것을 권장한다.

검사별 기준:

| 검사 | 입력 단위 | 주요 입력 | 사진 | 상태 |
| --- | --- | --- | --- | --- |
| IQC | 구매품목 또는 패널 | 수입검사서 체크, 값 입력, 적합/부적합 | 외함 사진 필수 | 상세 항목 후속 |
| LQC | 패널 또는 검사 단위 | LQC 성적서 입력 | 필수 위치 회신 대기 | 입력 방식 회신 대기 |
| OQC | 패널 또는 검사 단위 | 자체검수 성적서 입력 | 필수 위치 회신 대기 | 입력 방식 회신 대기 |
| 전진검수 | 프로젝트 및 패널 | 검수 결과, PUNCH LIST | 필요 시 첨부 | 필수 단계 |
| FAT | 프로젝트 및 패널 | 고객 입회 검사, PUNCH LIST | 필요 시 첨부 | 선택 단계 |

부적합 또는 PUNCH 발생 시 Pending List로 등록하고, 조치 후 재검사 요청이 가능해야 한다.

## 14. 제조 기준

제조현황은 디지털화로 확정됐다.

원칙:

- 자주순차표 큰 틀을 웹사이트에 적용한다.
- 휴대폰에서 체크 클릭 중심으로 입력한다.
- 작업 시작과 종료 시 입력한다.
- 패널별 시작/종료 입력이 가능해야 한다.
- 프로젝트별 작업 단계 시작/종료 입력도 가능해야 한다.
- 제조 단계 상세 항목은 생산관리팀 회신 예정이다.

구분해야 할 항목:

- 화면에 항상 보여야 하는 항목
- 팝업으로 나와야 하는 항목
- 저장은 되지만 화면에 안 보여도 되는 항목

제조 중단 기준:

- 제조 중 자재 문제, 인원 문제, 작업 불가 상황 발생 시 중단 버튼을 제공한다.
- 중단 사유는 Pending List에 제조 중단 유형으로 등록한다.
- 조치 담당 부서를 지정할 수 있어야 한다.
- 제조 중단은 긴급/차단 알림 대상이다.

## 15. Pending List 공통 모듈

큰 틀 이름은 Pending List로 한다.

유형:

- 품질 부적합
- 고객사 PUNCH LIST
- 제조 중단
- 기타

용어 원칙:

- “귀책부서”라는 표현을 쓰지 않는다.
- “조치 담당 부서”, “조치 담당자”, “원인 구분”을 사용한다.

Pending List는 다음 항목을 관리한다.

- 프로젝트
- 패널
- 구매품목
- 제조 단계
- 유형
- 조치 담당 부서
- 조치 담당자
- 상태
- 코멘트
- 첨부파일
- 재검사 요청
- 종결

상태 예:

- 등록
- 조치 요청
- 조치 중
- 재검사 요청
- 종결

코멘트는 조치 완료까지 이어져야 한다.

생산관리 담당자는 Pending List를 관리할 수 있어야 한다. 생산관리 담당자는 Pending List 페이지에서 다른 부서에 업무를 생성할 수 있어야 한다.

## 16. 부적합 조치 흐름

구매품 부적합 조치 유형:

| 유형 | 처리 흐름 |
| --- | --- |
| 구매처 반송 | 구매가 부적합 조치 입력 → 물류가 발송 여부 체크 → 자재가 재입고 도착 여부 체크 → IQC 재검사 요청 |
| 구매처 현장 수리 | 구매가 부적합 조치 입력 → 자재가 자재 준비 여부 체크 → 구매가 조치 완료 입력 → IQC 재검사 요청 |

제조/LQC/OQC/전진검수/FAT 부적합은 발생 단계로 재검사 요청이 돌아가야 한다.

예:

- LQC 부적합 → LQC 재검사
- OQC 부적합 → OQC 재검사
- 전진검수 PUNCH → 전진검수 재검사
- FAT PUNCH → FAT 재검사

## 17. 물류 기준

물류 단계는 다음과 같다.

1. 포장 완료
2. 출발 처리
3. 납품 완료

포장 완료 기준:

- 패널 단위로 관리한다.
- 일괄 처리 가능하다.
- 포장 사진은 필수다.
- 어떤 패널이 어떤 포장에 들어갔는지 매핑해야 한다.

Packing Unit 후보 필드:

- 포장번호
- 포장방식
- 포함 패널
- 포장 사진
- 규격
- 중량
- 비고

출발 처리 기준:

- 패널 단위로 관리한다.
- 일괄 처리 가능하다.
- 상차 사진은 필수다.

납품 완료 기준:

- 패널 단위로 관리한다.
- 일괄 처리 가능하다.
- 거래명세서 서명본은 필수다.
- “출하완료” 표현은 “납품완료”로 변경한다.

## 18. 영업 정산과 프로젝트 완료 기준

납품 완료 후 영업 정산 단계가 있다.

흐름:

1. 물류 납품 완료
2. 영업 정산 대기
3. 세금계산서 발행 완료
4. 프로젝트 완료

다른 부서는 물류 납품 완료 시 사실상 완료로 볼 수 있다. 영업은 세금계산서 발행까지 추적해야 한다.

최종 프로젝트 완료 조건:

- 모든 패널 납품 완료
- 세금계산서 발행 완료 체크
- 오픈 상태 Pending 0건
- 프로젝트 완료 처리

미종결 PUNCH나 부적합이 남아 있으면 프로젝트를 완료 처리할 수 없다.

## 19. 첨부파일 / 사진 / PDF / Excel 기준

첨부파일 대상:

- 사진
- PDF
- 거래명세서
- 포장사진
- 상차사진
- 검사자료
- 고객 확인자료

사진 기준:

- 필수 사진 미첨부 시 저장 차단한다.
- 사진 필수 여부는 장기적으로 체크리스트 template에서 관리한다.
- 초기 구현에서는 코드 고정도 가능하나 후속 TASK에서 관리자 기준정보로 이동한다.

PDF 기준:

- 검사성적서 PDF 출력이 필요하다.
- PDF는 승인 또는 출력 시점 데이터의 snapshot으로 생성한다.
- 출력 양식은 회신 대기다.

Excel 기준:

- 모든 주요 페이지에 Excel 출력 기능이 필요하다.
- 현재 조회 조건을 반영해 Excel 출력한다.
- 페이지별 중복 구현보다 공통 export 구조를 추천한다.
- Excel import는 preview/apply 분리, 오류 행 표시, 저장 가능한 행만 적용 원칙을 유지한다.

Excel 출력 대상 후보:

- 프로젝트
- 패널정보
- 생산관리
- 구매
- 자재
- 내 업무
- 알림
- Pending List
- 검사
- 제조
- 물류
- 영업 정산

## 20. 로그인 / 권한 / 관리자 페이지 방향

로그인 방향:

- 운영 인증은 Frontend MSAL React + Backend JWT Bearer(Microsoft.Identity.Web) 구조를 사용한다.
- Microsoft Entra ID는 신원 확인만 담당한다.
- 부서와 역할은 앱 내부 DB에서 관리한다.
- 신규 Entra 사용자는 최초 로그인 시 자동 생성되지만 역할이 0개이면 승인 대기 상태다.
- 승인 대기 사용자는 `/api/me`, 본인 프로필, 승인 대기 안내, 로그아웃 외 업무 데이터를 조회할 수 없다.
- 승인 대기 해소 기준은 active role 1개 이상이다. department_id는 승인 대기 해소 조건이 아니라 표시/분류 정보다.
- dev user와 실계정은 이메일이 같아도 자동 병합하지 않는다.
- 운영에서는 dev user 인증을 비활성화한다.
- Dev 인증은 Development/Testing 환경에서만 허용한다.
- Entra 앱 등록 표시명은 EMI 프로젝트 통합관리시스템 기준을 따른다.
- 검수 사용자 전환은 Development/Testing/UAT 용도이며 Production/Staging에서는 비활성화한다.
- 검수 사용자 전환은 실제 Microsoft 로그인 사용자 중 System Administrator만 사용할 수 있다.
- 검수 사용자 전환은 기존 dev user persona를 대상으로 하며 실제 Entra 사용자를 impersonation하지 않는다.
- 로그인 상태 유지는 MSAL cache와 Microsoft Entra SSO 정책 범위 안에서만 제공한다.
- Microsoft Entra 조건부 액세스, MFA, sign-in frequency 정책을 우회하지 않는다.
- token을 앱 코드에서 직접 localStorage/sessionStorage에 저장하지 않는다.
- 로그인 상태 유지 preference와 auth token은 구분한다.

권한 방향:

- 권한은 서버 Policy에서 강제한다.
- UI는 권한 없는 버튼을 숨길 수 있지만, 숨김만으로 보안을 대체하지 않는다.
- 관리자는 업무 입력을 임의로 우회하지 않는다.
- 관리자 전용 이력 조회와 기준정보 관리를 분리한다.
- 마지막 active System Administrator는 비활성화하거나 system-administrator role을 제거할 수 없다.
- TASK-INFRA-001 최소 사용자 관리 화면에서는 EntraId 사용자 역할/부서/활성 상태만 수정한다.
- Dev 사용자는 최소 사용자 관리 화면에 읽기 전용으로 표시한다.

관리자 페이지 후보:

- 사용자 관리
- 역할 관리
- 부서 관리
- Item 관리
- 생산계획 단계 관리
- 구매 필수 항목 관리
- 공휴일 관리
- 검사 체크리스트 템플릿 관리
- 제조 체크리스트 템플릿 관리
- 포장방식 관리
- Pending 유형 관리
- 업무 시작/완료 이력 관리
- 전체 감사 이력 관리

## 21. 현재까지 개발된 기능

| 영역 | 현재 구현됨 | 후속 수정 필요 |
| --- | --- | --- |
| 프로젝트 | 생성, 목록, 상세, 수정, 상태 변경, 삭제/복구/보관함, FAT 필요 여부, workflow 기준 상태/진행률 | 패널 단위 병목 집계와 Pending 차단 flag 연동 |
| 패널정보 | placeholder, 패널정보 입력, Excel preview/apply, 목포장 사이즈 검증, 설계 단계 완료 판정 | 검사/제조/물류 단계와 패널 상태 연동 |
| 포장방식 | StretchWrap/WoodenCrate 등 기본 포장방식 | 포장방식 관리자 기준정보화 |
| 구매정보 | 직접 입력, Excel preview/apply, 업체, 입고 완료, 완료일시 표시, grouped history | 구매처 master 또는 업체 기준정보화 |
| 자재 | 자재 입고 입력 기반 | 자재 도착/IQC 요청/입고 확정/키팅 분리 |
| 생산관리 | 메뉴, 목록, 프로젝트 펼침, 생산계획 조회/수정, 담당자 지정, 확장 담당자 구조, Excel 업로드 | 관리자 기준정보화 |
| 생산계획 | Item 기반 단계, 프로젝트별 snapshot, 단계명/필수 여부 override, Excel, Business Calendar 기준 캘린더 휴일 표시 | 캘린더 UX 지속 보정, 관리자 기준정보화 |
| 구매 필수 항목 | Item별 필수 구매 항목 설정, 새 프로젝트 skeleton 자동 생성 | 업체/발주정보 입력 기준과 완료 판정 보강 |
| 내 업무 | 목록, KPI, 프로젝트별 그룹, 실제 입력 페이지 이동, 시작/완료 동기화 | 시작/완료 이력 관리자 화면 |
| 알림 | 전체/읽음/읽지 않음, 프로젝트별 그룹, 읽음 처리, 인앱 알림 원본 구조, 외부 delivery 이력, Teams 통합 채널 게시, Gmail SMTP 메일 발송, Teams Activity Feed provider actual, text topic + Teams deep link, `/teams/activity` 탭, `/teams/activity/notifications/{id}` 상세, 관리자 수동 개인/업무 배정 Activity Feed, 설정 선택형 L0~L2 Activity Feed, Daily Digest 구조, 담당 프로젝트 요약, dry-run/actual provider, retry/dedupe, 관리자 delivery 조회 API, `work_items.due_date` 기반 L0~L3 에스컬레이션, `work_item_escalations`, 관리자 에스컬레이션 조회 API, 관리자 수동 알림 발송 3모드, 수동 업무 배정 work_item 생성, 수동/자동 알림 양식 통일, display snapshot/detail, 실패/대기 확인·제외·대기 재시도 | Activity Feed 자동 event coverage 확대, 운영 Teams manifest URL 전환, `projectCreated` activityType 추가 여부, due_date 입력/동기화 정책, 알림/에스컬레이션 설정 UI, 사용자별 채널 preference, delivery 신뢰성/실패 재처리 고도화, 기존 업무 화면 action feedback UX 확대 |
| workflow | 18단계 stage, 프로젝트 workflow 요약, 기존 페이지 hook, 미구현 stage workflow fallback | 후속 실제 화면 단계 연결 |
| 로그인/권한 | Microsoft 365 로그인 기반, EntraId JIT 사용자 생성, 승인 대기, bootstrap admin, 최소 사용자 관리, Dev user read-only, System Administrator 검수 사용자 전환, 로그인 상태 유지, dev auth/E2E 보존 | 운영 배포 전 실제 Entra 설정, 운영 redirect URI, Production/Staging dev auth 및 AdminUserSwitch 비활성 검수 |
| 공휴일/영업일 | `system_holidays.holiday_type`, BusinessDayCalculator, `/api/calendar/business-days`, 생산계획 캘린더 연동, System Administrator 휴일 관리 API/UI, Excel 양식 다운로드/preview/apply, 회사휴일 Company type, UAT DB 보존 | 공식 공휴일 API service key 연동, 국가공휴일 자동 sync scheduler, 회사 자체 근무일 지정 필요성 검토, 운영 휴일 데이터 검수 |
| 관리자 | 시스템 관리 중심 관리자 홈, 사용자 관리 재사용/확장, 부서 관리, 휴일 관리 재사용, 권한 매트릭스 read-only, 기준정보 변경 이력, 업무 시작/완료 이력, 알림/에스컬레이션 조회, 발송 실패/대기 상세 추적, active escalation L0~L3 breakdown, 삭제 예정 + 7일 후 완전 삭제 시도, 복구, 일괄 삭제/복구, 삭제 보류, 부서 field-level validation | Item/포장방식/생산계획 단계/구매 필수 항목 관리자 통합 여부, role/permission 편집 UI, 삭제 예정 데이터 purge 운영 정책, 전체 field-level audit 확장 |
| UAT | 고정 UAT DB, UAT backend/frontend 포트 | 게시 전 persistence 자동 검증 강화 |
| E2E | 전용 backend/frontend 포트, 전용 DB, cleanup | 신규 업무 단계마다 시나리오 추가 |

## 22. 현재 기능에서 수정해야 할 방향

현재 개발된 기능에서 앞으로 수정해야 할 주요 방향은 다음과 같다.

- 프로젝트 상태 집계를 패널 단위 병목 기준, Pending 차단 flag, 완료 조건과 연결한다.
- Teams/메일 외부 delivery 계층은 구현되었으며 운영 전 Teams 운영 Webhook 재발급, Gmail SMTP 장기 운영 적합성, 회사 공식 발송 수단 전환 여부를 검토한다.
- 구매 업체를 장기적으로 master 또는 기준정보로 관리할지 결정한다.
- 자재 페이지를 자재 도착, 입고 확정, 키팅 완료 흐름으로 분리한다.
- Pending List 공통 모듈을 추가한다.
- 검사 체크리스트와 검사성적서 PDF 출력을 추가한다.
- 제조 체크리스트와 작업 시작/종료, 제조 중단을 추가한다.
- 물류 포장 구성, 출발, 납품 완료를 추가한다.
- 영업 정산과 세금계산서 발행 완료를 추가한다.
- 모든 페이지 Excel 출력 공통 기능을 추가한다.
- Microsoft 365 로그인 기반은 구현 완료되었으며, 운영 배포 전 실제 Entra 앱 등록값, 운영 redirect URI, secret/env 관리, Production/Staging dev auth 비활성, AdminUserSwitch 비활성 설정을 검수한다.
- Teams Activity Feed provider/capability와 관리자 수동 개인/업무 배정 경로는 TASK-NOTIFY-003에서 text topic + Teams deep link 방식으로 actual 발송까지 구현되었다. 자동 event coverage는 6.5.2.2의 적용/부분 적용/미확인/후속 상태를 따르며, 운영 전 manifest `contentUrl`/`websiteUrl`과 deep link webUrl을 운영 URL로 교체하고 조직 앱 배포 상태를 검수한다.
- 예정일 기반 에스컬레이션 엔진은 `work_items.due_date` 기준으로 구현되었으며, 실제 운영 대상 업무의 due_date 입력/동기화 정책은 후속으로 확정한다.
- 공휴일/영업일 기반은 구현 완료되었으며, 운영 전 연간 대한민국 공휴일/대체공휴일/임시공휴일/회사휴일 데이터를 관리자 휴일 관리 또는 공식 API sync로 검수한다.
- 공식 대한민국 공휴일 API service key 연동과 자동 sync scheduler는 후속으로 검토한다.
- NOTIFY-002는 BusinessDayCalculator를 재사용한다. 생산계획/구매 예정일을 `work_items.due_date`로 동기화할지 여부와 due_date 입력 UX는 후속으로 확정한다.
- ADMIN-001은 시스템 관리 중심으로 구현 완료되었으며, Item/포장방식/생산계획 단계/구매 필수 항목의 관리자 통합 여부는 후속 사용자 결정으로 남긴다.
- 관리자 삭제 예정 데이터의 7일 후 purge 운영 정책, 삭제 보류 처리 모니터링, 전체 field-level audit 확장은 운영 검수 후 고도화한다.
- role/permission 편집 UI, Pending 유형 관리, 검사/제조 체크리스트 템플릿, 발송 실패 수동 재처리 UI는 ADMIN-001 범위에서 제외되어 후속으로 검토한다.

## 23. 향후 개발 로드맵

### TASK-006A: 업무 요청 / 내 업무 / 알림 기반 구조

- 상태: 완료
- 목적: workflow event, 내 업무, 알림, 담당자 fallback 기반 구축
- 포함 범위: 18단계 stage, work_items, notifications, project workflow 요약, 메뉴 추가
- 제외 범위: 실제 Teams/Email 발송, 상세 제조/검사/물류 화면
- 선행조건: TASK-005A 생산관리/담당자 기반
- 주요 테스트: workflow 생성, 내 업무 조회, 알림 읽음, 권한, E2E

### TASK-006B: 기존 페이지 18단계 프로세스 연결 보강

- 상태: 완료
- 목적: 이미 구현된 프로젝트/패널/생산관리/구매 화면을 workflow 완료 판정과 더 정확히 연결
- 포함 범위: 단계별 완료/진행 중 계산, 담당자 구조 확장, 상태/진행률 통합
- 제외 범위: 신규 검사/제조/물류 상세 화면
- 선행조건: TASK-006A
- 주요 테스트: 프로젝트 상세 workflow, 목록 진행률, 단계별 필수값 partial/all 판정

### TASK-006C: 기존 페이지 잔여 정렬 / 자재·납품·Workflow 링크 보강

- 상태: 완료
- 목적: TASK-006B 이후 남은 사용자-facing 용어, 자재 페이지 표현, 미구현 stage fallback, Excel 양식 잔여 정합성을 정리
- 포함 범위: 자재 입고 처리 용어, 납품 완료 용어, Workflow tab, 미구현 stage 안전 fallback, Excel 양식 최종 점검
- 제외 범위: 실제 자재 도착/IQC/입고 확정/키팅 기능, 물류 기능, Pending List
- 선행조건: TASK-006B
- 주요 테스트: UAT 화면, Excel header, workflow fallback, E2E

### TASK-INFRA-001: Microsoft 365 로그인 / 사용자·역할 운영 전환

- 상태: 완료
- 목적: Microsoft 365 로그인 / 사용자·역할 운영 전환
- 포함 범위: MSAL React + JWT Bearer Microsoft.Identity.Web, EntraId JIT 사용자 생성, 승인 대기, Bootstrap admin, 최소 사용자 관리, Dev mode 보존, System Administrator 검수 사용자 전환, 로그인 상태 유지
- 제외 범위: Teams/메일 알림, Graph Mail.Send/Teams 권한, Entra 그룹/App Role 기반 권한, 권한 matrix 재설계, 정식 ADMIN-001 사용자 관리 고도화, 실제 Entra 사용자 impersonation, Azure 구독/결제
- 선행조건: 권한 matrix 정리
- 주요 테스트: backend 전체 test, frontend unit/build, Full-Stack E2E, seed A/B/C/D, 실제 Microsoft 로그인 수동 검수

### TASK-NOTIFY-001: Teams / 메일 알림 채널 확장

- 상태: 완료
- 목적: 기존 인앱 알림 위에 Teams/Mail 외부 delivery 계층을 추가한다.
- 포함 범위: `notification_deliveries`, NotificationDispatcher/Worker, Teams Webhook Channel, Adaptive Card payload, Gmail SMTP actual provider, Graph Mail optional provider, DryRun provider, 일일 요약 메일(07:30) 구조, retry/dedupe/batch, 관리자 delivery 조회 API, Teams Activity Feed 후속 기획 문서
- 제외 범위: Teams Activity Feed 실제 구현, Teams DM 실제 구현, 예정일 에스컬레이션, Pending List, 개인별 알림 설정 UI, 발송 실패 수동 재처리 UI, 카카오톡 등 기타 채널
- 선행조건: TASK-INFRA-001
- 주요 테스트: backend 전체 test, Notification targeted tests, Migration tests, Authorization tests, frontend lint/typecheck/unit/build, mock UI smoke, Full-Stack E2E, seed A/B/C/D, UAT DB persistence, Teams Webhook actual 사용자 검수, Gmail SMTP actual 사용자 검수

### TASK-CALENDAR-001: 공휴일 / 영업일 계산 / 휴일 관리

- 상태: 완료
- 목적: 생산계획 캘린더와 예정일 에스컬레이션에 공통으로 사용할 영업일 기준을 구축한다.
- 포함 범위: National/Substitute/Temporary/Company 휴일 유형, BusinessDayCalculator, business-days API, 생산계획 캘린더 연동, System Administrator 휴일 관리 API/UI, Excel 일괄 등록, 회사휴일 비활성화 정책
- 제외 범위: 공식 공휴일 API service key 운영 sync, 회사 자체 근무일 지정, NOTIFY-002 에스컬레이션 worker, Teams Activity Feed 실제 구현, Pending List
- 선행조건: TASK-NOTIFY-002 전제
- 주요 테스트: BusinessDayCalculator, Calendar API, Admin Holiday API/UI, Migration tests, frontend unit/build, mock UI smoke, Full-Stack E2E, UAT persistence

### TASK-NOTIFY-002: 예정일 기반 에스컬레이션

- 상태: 완료
- 목적: `work_items.due_date` 기반 L0~L3 예정일 에스컬레이션 엔진을 구축한다.
- 포함 범위: `work_item_escalations`, L0(예정일 직전 영업일), L1(초과 즉시), L2(+2영업일), L3(+3영업일, 생산관리·영업 한정), BusinessDayCalculator 재사용, recipient resolver, 인앱 notification/recipient 생성, `notification_deliveries` 연동, Gmail SMTP Mail delivery 연동, Teams 개인 알림 dry-run delivery, 관리자 에스컬레이션 조회 API, Daily Digest 담당 프로젝트 요약
- 제외 범위: Teams Activity Feed 실제 구현, Teams DM 실제 구현, 생산계획/구매 예정일 자동 due_date 동기화, due_date 입력 UI, Pending List, 알림 설정 UI, 수동 재처리 UI, 부서장/경영진 수신
- 선행조건: TASK-NOTIFY-001, TASK-CALENDAR-001
- 주요 테스트: backend 전체 test, Notification/Escalation targeted tests, Migration tests, Authorization tests, BusinessDay tests, frontend lint/typecheck/unit/build, mock UI smoke, Full-Stack E2E, seed A/B/C/D, UAT DB persistence, UAT L0 dry-run smoke

### TASK-ADMIN-001: 관리자 기준정보 페이지

- 상태: 완료
- 목적: 시스템 관리 중심의 관리자 홈과 사용자/부서/휴일/이력/모니터 화면을 제공한다.
- 포함 범위: 관리자 홈, 사용자 관리 재사용/확장, 부서 관리, 휴일 관리 재사용, 삭제 예정/복구/일괄 action, 권한 매트릭스 read-only, 기준정보 변경 이력, 업무 시작/완료 이력, 알림 발송 상태 조회와 실패/대기 상세 추적, 에스컬레이션 상태 조회와 L0~L3 breakdown, 부서 field-level validation
- 제외 범위: Item 관리, 포장방식 관리, 생산계획 단계 관리, 구매 필수 항목 관리, 권한 편집, role master 편집, Pending/검사/제조 템플릿, due_date 정책 관리, Teams Activity actual
- 선행조건: 권한/관리자 정책 확정
- 주요 테스트: backend 전체 test, Admin targeted tests, Migration tests, Authorization tests, Calendar/Holiday tests, User/Identity tests, frontend lint/typecheck/unit/build, mock UI smoke, Full-Stack E2E, UAT admin browser/deletion smoke, secret scan

### BASELINE-GOV-001: 개인정보 및 Task 거버넌스 기준선 정비

- 상태: 완료 — 사용자 승인 후 PR #21 squash merge(`3bc3ef8`)
- 목적: tracked 문서의 사용자 개인정보를 비식별화하고 모든 Task의 종료 산출물·품질 gate·검수 상태 기준을 단일 정책으로 확립한다.
- 포함 범위: 기존 동일 목적 branch read-only 비교, NOTIFY-003 문서 비식별화, [Task 종료 및 산출물 정책](12-task-completion-policy.md), Activity Feed provider/capability와 event coverage 상태 분리, 후속 Task 우선순위 등록
- 제외 범위: runtime code, dependency, migration, DB, UAT, worker, 외부 발송, 후속 기능 구현
- 선행조건: main Git 기준선 일치, 기존 WIP 보존, 동일 목적 branch의 고유 정책 비교·통합
- 산출물: [Task 정의와 검수 체크리스트](../tasks/baseline-gov-001.md), [Implementation report](../tasks/baseline-gov-001-implementation-report.md), [SOP](../tasks/baseline-gov-001-sop.md), [User manual](../tasks/baseline-gov-001-user-manual.md), 이 Roadmap update
- 완료 조건: 문서/링크/PII/secret/범위 검증 통과와 사용자 validation checklist 확인. 체크리스트 생성과 사용자 검수 완료를 구분한다.

### TASK-GOV-002: Git history 개인정보 risk decision

- 상태: 계획 / 별도 사용자·보안 결정 필요
- 목적: current checkout에서 제거된 개인정보가 Git history에 남은 위험과 repository 공개 범위를 평가하고, history rewrite 필요 여부와 협업 절차를 결정한다.
- 포함 범위: 영향 commit/file 수, repository visibility와 clone/fork/branch 영향, 보존·rewrite 대안, 공지·backup·re-clone 계획
- 제외 범위: 승인 전 history rewrite, force push, tag/branch 재작성 또는 삭제
- 선행조건: repository owner/보안 담당 risk owner 지정, 공개 범위와 downstream clone/fork 확인
- 예상 migration: 없음
- 핵심 검수 기준: 실제 값 원문을 재노출하지 않고 결정 근거·영향·완화책·실행 승인 여부를 문서화
- 주요 위험: rewrite 시 commit hash 변경과 열린 branch/PR 단절, 미조치 시 history 접근자가 과거 개인정보를 조회할 가능성

### TASK-E2E-ISOLATION-001: Full-Stack E2E PostgreSQL 물리 격리

- 상태/No-Go 기반: 완료 — PR #22 squash merge(`45fd61c`)
- 목적: Full-Stack E2E를 persistent UAT PostgreSQL과 container/network/storage 수준에서 분리하고 UAT/운영성 DB 이름을 data command 전에 차단한다.
- 포함 범위: 실행별 전용 PostgreSQL Compose project, 동적 loopback port, tmpfs storage, `emi_qms_e2e_*` DB-name guard, scoped cleanup, Testing external provider 차단, host `psql` 없는 Docker-only 경로
- 제외 범위: persistent UAT Compose/volume 변경, UAT DB reset, migration 변경, 실제 Teams/Graph/SMTP/Webhook 발송
- 선행조건: Docker Compose v2, canonical [Task 종료 및 산출물 정책](12-task-completion-policy.md)
- 예상 migration: 없음
- 핵심 검수 기준: UAT/E2E container·network ID 분리, E2E Docker volume mount 0, invalid DB name SQL-before-fail, Full-Stack E2E 16개 통과, cleanup 후 E2E 자원 0, UAT health/schema/업무 data 유지
- 산출물: [Task 정의와 검수 체크리스트](../tasks/e2e-isolation-001.md), [Implementation report](../tasks/e2e-isolation-001-implementation-report.md), [SOP](../tasks/e2e-isolation-001-sop.md), [User manual](../tasks/e2e-isolation-001-user-manual.md), 이 Roadmap update
- 주요 위험: 동적 application port 선택의 짧은 race window, CI의 사용하지 않는 bootstrap PostgreSQL 자원

### TASK-UAT-001: HTTPS Development UAT 안정화

- 상태/다음 순서: 구현·자동 검증·사용자 검수 완료 / PR #23 squash merge 승인
- 목적: Teams Activity 검수를 위한 HTTPS Development UAT의 frontend strict port, process ownership, HTTP/HTTPS readiness, notification env와 master-data transaction을 안정화한다.
- 포함 범위: 5174 strict port/ownership/PID, repo 경로 boundary, protocol mismatch 판정, literal notify dotenv loading, worker/provider Development 설정, master-data transaction, HTTPS UAT health와 화면 검수, E2E isolation 연계
- 제외 범위: read-only Review mode, dependency security, notification claim/lease, escalation starvation, 마지막 관리자 동시성
- 선행조건: TASK-E2E-ISOLATION-001 완료, persistent UAT와 HTTPS server 보존
- 예상 migration: 없음
- 핵심 검수 기준: HTTPS/Teams route 200, HTTP/HTTPS 전환, strict 5174, 다른 process 비종료, UAT DB 보존, isolated E2E, 사용자 저장·수정·알림 검수, 5종 산출물 확정
- 산출물: [Task 정의와 검수 체크리스트](../tasks/uat-001-https-dev-stability.md), [Implementation report](../tasks/uat-001-implementation-report.md), [SOP](../tasks/uat-001-sop.md), [User manual](../tasks/uat-001-user-manual.md), 이 Roadmap update
- 주요 위험: Development actual provider 오발송, UAT worker 자연 변경과 E2E 영향 혼동, 사용자 검수 전 완료 오판. 자동 검증에서는 신규 실제 발송과 저장·수정을 수행하지 않음

### TASK-FRONTEND-SEC-001: Frontend dependency security remediation

- 상태/다음 순서: 구현·자동 검증·사용자 검수 완료 / PR #24 squash merge 승인
- 목적: frontend dependency vulnerability baseline을 재현하고 최소 호환 upgrade로 알려진 보안 위험을 해소한다.
- 포함 범위: Vite 7.3.6, esbuild 0.28.1, Vitest 4.1.0, audit 전 Critical 1/High 3/Moderate 2/Low 1에서 전체 0, synthetic deny regression, HTTP/HTTPS alternate-port, frontend/backend/E2E 회귀
- 제외 범위: 기능 개발, 프레임워크 전면 교체, 근거 없는 일괄 major upgrade
- 선행조건: TASK-UAT-001과 E2E isolation 완료
- 예상 migration: 없음. `frontend/package.json`과 `pnpm-lock.yaml`만 dependency 변경
- 핵심 검수 기준: audit 전체 0, synthetic canary 노출 0, frontend unit 57/57, backend 295/295, migration 16/16, Full-Stack E2E 16/16, 5184/5185 proxy·strict port·console·overflow 회귀, persistent UAT snapshot 유지
- 산출물: [Task 정의와 검수 체크리스트](../tasks/frontend-sec-001.md), [Implementation report](../tasks/frontend-sec-001-implementation-report.md), [SOP](../tasks/frontend-sec-001-sop.md), [User manual](../tasks/frontend-sec-001-user-manual.md), 이 Roadmap update
- 주요 위험: 현재 running 5174는 Vite 7.3.0 process로 patch 전 runtime이다. Merge 후 `TASK-UAT-HANDOVER-001` controlled restart 전에는 patched UAT로 간주하지 않는다.

### TASK-UAT-HANDOVER-001: Patched frontend UAT runtime handover

- 상태/다음 순서: 구현·자동 검증·사용자 검수 완료 / PR #25 squash merge 승인
- 목적: 기존 HTTPS Development UAT를 통제된 절차로 재기동해 merged patched dependency를 실제 5174 runtime에 반영한다.
- 포함 범위: 최신 main `1dcefa1522a2f0c3db785756e043038b7eefb4ac` detached runtime, HTTPS 5186 candidate, frontend-only PID/session handover, Vite 7.3.6·esbuild 0.28.1·Vitest 4.1.0, HTTPS/Teams/API/UAT persistence smoke, rollback 절차
- 제외 범위: dependency 추가 변경, Review-safe UAT 구현, DB reset, actual external notification 신규 smoke
- 예상 migration: 없음
- 핵심 검수 기준: patched checkout 기반 5174, trusted HTTPS, route/API 정상, Backend 5081 PID 유지, UAT DB/schema/count와 PostgreSQL restart count 유지, rollback 가능한 session 기록
- 자동 검증 결과: 5186 검증 후 종료, 5174 Vite 7.3.6 cutover 완료, Backend/PostgreSQL 미재시작, DB/delivery snapshot 동일, 5185 Preview 유지, 신규 외부 알림 발송 없음
- 산출물: [Task 정의와 검수 체크리스트](../tasks/uat-handover-001.md), [Implementation report](../tasks/uat-handover-001-implementation-report.md), [SOP](../tasks/uat-handover-001-sop.md), [User manual](../tasks/uat-handover-001-user-manual.md), 이 Roadmap update
- 사용자 검수 결과: 5174 main/project/work/admin, Teams client와 기존 Activity 상세, 로그인·권한 안내, console·narrow pane, SOP/User manual 검수 완료. PR #25 병합 승인
- 주요 위험: 5185 Preview와 legacy worktree cleanup 미실행, 현재 dirty legacy worktree와 merged main 간 중복 WIP, rollback 전 process ownership 오판

### TASK-UAT-002: Review-safe UAT

- 상태/다음 순서: 구현·자동 검증·사용자 검수 완료 / PR #26 squash merge 승인 / 다음 UAT-VERIFY-001
- 목적: 데이터 변경과 외부 발송 없이 UAT의 schema, health, route와 persistence를 안전하게 검토할 수 있는 명시적 Review mode를 제공한다.
- 포함 범위: Development 5174/5081과 분리된 HTTPS 5190/Backend 5092, authoritative runtime mode, migration/seed/upsert 차단, mutation worker·actual provider 미등록, unsafe HTTP method 423, Entra JIT write 차단, DB session read-only, schema mismatch readiness 503, 전역 banner와 mutation action disabled
- 제외 범위: Development UAT 저장·수정 검수, 테스트 데이터 정리, 실제 알림 발송, 운영 배포
- 선행조건: TASK-UAT-001, TASK-E2E-ISOLATION-001, TASK-FRONTEND-SEC-001, TASK-UAT-HANDOVER-001 완료
- 예상 migration: 없음. 기존 DB role/schema도 변경하지 않음
- 자동 검증 결과: backend 303/303, frontend 59/59, Full-Stack E2E 16/16, 실제 5092/5190 startup, 주요 route 11개와 390px 3개, mutation 4 method/method override 423, DB read-only pool test, 5분 UAT snapshot·delivery status·container/PID 보존, actual provider call 0
- 핵심 검수 기준: 5190 banner와 조회 기능, mutation button disabled/이유, 직접 API 423, DB read-only, worker/provider/startup write 0, Development 5174와 Preview 5185 유지
- 산출물: [Task 정의와 검수 체크리스트](../tasks/uat-002-review-safe.md), [Implementation report](../tasks/uat-002-implementation-report.md), [SOP](../tasks/uat-002-sop.md), [User manual](../tasks/uat-002-user-manual.md), 이 Roadmap update
- 사용자 검수 결과: 5190 banner·주요 조회 화면·검색/필터/정렬/상세, mutation action disabled와 이유, console·narrow pane, SOP/User manual 검수 완료. PR #26 병합 승인
- 주요 위험: 신규 frontend action 문구가 공통 UX guard 분류에서 빠질 수 있으나 서버 middleware와 DB read-only가 최종 차단한다. 같은 DB를 사용하는 Development worker 자연 변화는 source를 구분해야 한다.

### UAT-VERIFY-001: UAT 통합 사용자 검수

- 상태/다음 순서: 최신 main 자동 검증·사용자 검수 완료 / UAT 기준선 Go / PR #29 squash merge 승인 / 다음 TASK-NOTIFY-REL-001
- 목적: 최신 main과 공식 Review-safe UAT의 migration·schema·data·authorization·notification·UI/UX·persistence 기준선을 read-only로 통합 검증한다.
- 포함 범위: runtime file 정합성, full migration ledger와 critical schema, 10개 핵심 table aggregate·참조 무결성, notification/dashboard/escalation/deletion lifecycle, 권한·access scope, Review-safe live 방어, 개인정보 안전 desktop/390px, isolated 자동 test
- 제외 범위: runtime/migration/dependency/script 수정, Persistent UAT data 정리, 실제 외부 발송, 기존 runtime 재시작, 신규 기능 구현·운영 배포
- 선행조건: TASK-UAT-001, TASK-FRONTEND-SEC-001, TASK-UAT-002, TASK-DB-MIGRATION-001 완료와 merged Review-safe runtime handover
- 예상 migration: 없음
- 기준선 결과: canonical/live/approved legacy 27/28/1, missing/unknown 0/0, critical schema mismatch 0, critical orphan/reference mismatch 0, dashboard open Failed/Pending과 active escalation 모두 detail과 0으로 일치
- 자동 검증 결과: backend targeted 141/141·전체 311/311, frontend 59/59, mock UI 1/1, Full-Stack E2E 16/16, API 16개, desktop/390px 각 13개, table/list geometry mismatch 0, output negative guard 5/5
- Persistent UAT: 10개 table count와 delivery/notification/work-item max timestamp 전후 동일, container/volume/restart/PID 유지, actual provider call 0
- 개인정보 안전 원칙: 실제 사용자·프로젝트·업무·알림 원문, ID, raw DB/API/DOM/console, screenshot을 출력하지 않고 boolean/integer/fixed enum/aggregate만 기록
- data cleanup 후보: notification 19, work item 3, delivery 41, department 1, holiday 3. Synthetic/historical 예외는 P3와 `TASK-UAT-DATA-001` 권장으로 분리하고 본 Task에서 변경하지 않음
- 산출물: [Task 정의와 검수 체크리스트](../tasks/uat-verify-001.md), [Implementation report](../tasks/uat-verify-001-implementation-report.md), [SOP](../tasks/uat-verify-001-sop.md), [User manual](../tasks/uat-verify-001-user-manual.md), 이 Roadmap update
- Findings: 신규 P0/P1/P2 0, 기존 migration checksum guard P3 유지
- 사용자 검수 결과: Current Review-safe 5190의 주요 조회·dashboard/detail·권한·알림 범위·표 정렬·desktop/390px·SOP/User manual·데이터 정리 권장안 검수 완료. UAT 기준선 Go, 신규 기능 No-Go 유지와 PR #29 병합 승인
- 개인정보 검증 절차 보정: 과도한 GitHub metadata 조회 Finding을 검증 절차 P2로 수용하고, 작성자 관련 field를 제외한 fixed-field projection·output guard·tracked/staged/PR leak 0 재확인 후 merge 절차 재개 승인
- 주요 위험: 자동 검증과 사용자 완료 상태 혼동, shared Development worker 자연 변화의 attribution, synthetic/historical data를 실제 업무 data와 혼동해 임의 정리하는 위험

### TASK-DB-MIGRATION-001: Migration ledger 전체 집합 검증

- 상태/다음 순서: 구현·자동 검증·사용자 검수 완료 / PR #27 squash merge 승인 / Review-safe controlled handover 대기
- 목적: repository migration canonical 전체 집합과 live ledger 전체 집합을 비교하고, 코드 리뷰로 승인된 historical marker만 schema probe 후 호환 처리한다.
- 포함 범위: validated migration catalog, full-set ledger inspector, exact approved legacy policy, TeamsActivity channel schema probe, Review-safe readiness/runtime diagnostic, frontend 요약 표시, isolated fixture, candidate 5093/5191
- 제외 범위: 기존/신규 SQL migration, live ledger row 삭제·rename·추가, Persistent UAT data 변경, Development startup 정책 확대, UAT-VERIFY 데이터 검증 재개
- 선행조건: TASK-UAT-002 완료, UAT-VERIFY-001 false-ready Finding, E2E isolation
- 예상 migration: 없음. repository `0001~0027` SQL과 live `schema_migrations`를 수정하지 않음
- 기준선: canonical 27개, historical live 28개, approved legacy `0020_teams_activity_delivery_channel`, canonical successor `0023_teams_activity_delivery_channel`
- 자동 검증 결과: exact/compatible ready 200, unknown/missing/successor/schema mismatch ready 503, catalog duplicate/missing prefix 차단, backend 311/311, frontend 59/59, mock UI 1/1, Full-Stack E2E 16/16, candidate 5191/5093 ready 200
- 핵심 검수 기준: Compatible 27/28/1 표시, legacy row 보존, DB read-only, mutation 423, worker/provider 미실행, Persistent UAT snapshot 보존
- 산출물: [Task 정의와 검수 체크리스트](../tasks/db-migration-001.md), [Implementation report](../tasks/db-migration-001-implementation-report.md), [SOP](../tasks/db-migration-001-sop.md), [User manual](../tasks/db-migration-001-user-manual.md), 이 Roadmap update
- 사용자 검수 결과: Candidate 5191의 banner·주요 조회 화면·Compatible 27/28/1 표시, legacy marker 보존 의미, SOP/User manual 검수 완료. PR #27 병합 승인
- 주요 위험: merge 후 current 5190/5092 controlled handover 전까지 latest-only runtime이 남음, 새 legacy 승인 시 exact policy/schema probe/code review 필요, checksum guard는 후속 P3

### TASK-UAT-HANDOVER-002: Privacy-safe Review-safe runtime handover

- 상태/다음 순서: 구현·자동 검증·사용자 검수 완료 / PR #28 squash merge 승인 / 다음 UAT-VERIFY-001 재실행
- 목적: PR #27 merged main의 full-ledger Review-safe runtime을 공식 5190/5092로 통제 전환하고 개인정보 안전 browser 검증과 rollback 증빙을 확립한다.
- 포함 범위: Candidate/main tree 비교, raw DOM 폐기와 boolean/count/enum output guard, desktop/390px fixed route matrix, Existing process ownership·rollback, 5190/5092 cutover, 27/28/1·DB read-only·mutation/worker/provider 차단, Persistent UAT aggregate 전후 비교
- 제외 범위: runtime code, migration SQL, dependency/lockfile/script, live ledger·업무 data 변경, actual external provider, Development/Preview/Candidate 재시작, UAT-VERIFY-001 재개
- 선행조건: TASK-UAT-002와 TASK-DB-MIGRATION-001 완료, PR #27 squash merge, Candidate 5191/5093 검수 완료
- 예상 migration: 없음. canonical 27개, live 28개와 approved legacy marker 1개를 보존
- 자동 검증 결과: Candidate/Main tree 동일, Candidate와 Main 각각 desktop 11/11·390px 11/11, output negative guard 5/5 차단, Main ready 200 Compatible 27/28/1, mutation 5/5 423, targeted 32/32, frontend 59/59, audit 0, Persistent UAT aggregate/container/volume/restart 동일
- 개인정보 안전 원칙: 실제 UAT에서 raw DOM/accessibility snapshot, text/HTML, screenshot, response body와 console message 원문을 출력하지 않고 fixed schema의 boolean/count/enum만 기록
- 산출물: [Task 정의와 검수 체크리스트](../tasks/uat-handover-002.md), [Implementation report](../tasks/uat-handover-002-implementation-report.md), [SOP](../tasks/uat-handover-002-sop.md), [User manual](../tasks/uat-handover-002-user-manual.md), 이 Roadmap update
- 사용자 검수 결과: Current 5190의 banner·주요 조회 화면·Compatible 27/28/1 표시·mutation action 비활성화, Candidate 5191과의 기능·구조 동등성, 개인정보 안전 browser 검증 정책, SOP/User manual 검수 완료. PR #28 병합 승인
- 주요 위험: Candidate와 legacy worktree 정리 미실행, UAT-VERIFY 장기 검증 중 Development worker 자연 변화 구분 필요, migration checksum guard P3

### TASK-NOTIFY-REL-001: Notification delivery claim/lease와 attempt audit

- 상태/다음 순서: 구현·자동 검증·사용자 검수 완료 / PR #30 squash merge 승인 / 다음 TASK-UAT-HANDOVER-003
- 목적: 다중 notification worker 정상 경쟁에서 같은 delivery의 provider 중복 호출과 늦은 completion overwrite를 차단하고 attempt별 계보를 감사 가능하게 만든다.
- 포함 범위: additive migration 0028, Pending→Processing claim, `FOR UPDATE SKIP LOCKED`, 300초 lease, opaque worker, fencing token, attempt audit, stale recovery, retry/permanent 분류, 관리자 Processing count/filter/detail/action 차단, isolated candidate 5094/5192
- 제외 범위: Persistent UAT 0028 적용, 기존 runtime handover, actual Teams/Mail/Channel 발송, provider exactly-once, escalation starvation, 사용자별 알림 설정, 기존 실패 data 정리
- 보장 수준: 정상 worker 경쟁 provider call 1회와 DB completion fencing을 보장한다. provider 성공 후 DB completion 전 crash는 재발송 가능하므로 at-least-once이며 exactly-once가 아니다.
- migration: `0028_notification_delivery_claim_lease`; delivery claim column/Processing constraint, attempt table, unique/FK/check, due·owner·attempt·stale index. 기존 0001~0027 diff 0
- 자동 검증: backend 전체 325/325, claim/migration 14/14, notification/migration/authorization 151/151, frontend unit 61/61, mock UI 1/1, Full-Stack E2E 16/16, candidate desktop/390px·output guard 통과
- Candidate: HTTPS 5192/backend 5094, 전용 `emi_qms_e2e_*` tmpfs PostgreSQL, canonical migration 28/latest 0028, synthetic Pending/Processing/Sent/Failed와 attempt history, actual provider 0
- Persistent UAT: 0028 미적용, aggregate 16/16 전후 동일, PostgreSQL restart 0, 기존 runtime PID 유지
- 산출물: [Task 정의와 검수 체크리스트](../tasks/notify-rel-001.md), [Implementation report](../tasks/notify-rel-001-implementation-report.md), [SOP](../tasks/notify-rel-001-sop.md), [User manual](../tasks/notify-rel-001-user-manual.md), 이 Roadmap update
- 사용자 검수: Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #30 병합 승인 / 미체크 항목 0
- 주요 위험: provider transaction과 DB transaction 사이 crash ambiguity, Persistent UAT controlled migration/handover 미수행, migration checksum guard P3

### TASK-UAT-HANDOVER-003: Notification delivery claim/lease UAT handover

- 상태/다음 순서: 구현·자동 검증·사용자 검수 완료 / PR #33 squash merge 승인 / 다음 TASK-NOTIFY-ESC-001
- 목적: Persistent UAT에 canonical 0028을 통제 적용하고 Development·Review-safe runtime을 최신 main으로 전환한다.
- 포함 범위: fresh backup·isolated restore·fault rollback, migration 0028, canonical 28 + approved legacy 1 = live 29 ledger 확인, Review-safe 5190/5092와 Development 5174/5081 controlled handover, Phase A/Phase B와 장시간 snapshot 검증
- 제외 범위: actual 외부 발송, 기존 업무 data 정리, escalation starvation 구현
- 선행조건: TASK-NOTIFY-REL-001 사용자 검수·merge와 candidate 증빙
- 핵심 결과: live 0028 schema와 ledger 28/29/1, missing/unknown 0, 최신 main Review-safe read-only·mutation 423, Development normal configuration 복구, 사용자 승인 ManualTest 1건의 단일 claim/attempt/Sent와 unrelated provider call 0, Persistent aggregate 보존
- worker 정책: normal configuration은 delivery·purge true, escalation false이며 TASK-NOTIFY-ESC-001 전 임의 활성화하지 않음
- backup/rollback: fresh backup mode 600과 checksum·isolated restore 확인, Persistent restore 미수행, 적용 후 forward-fix 원칙
- runtime: obsolete Review Candidate 5191/5093 종료, Notification Candidate 5192/5094와 Maintenance Candidate 5595 유지
- 개인정보 안전: desktop/390px 결과를 boolean/count/fixed alias로 검증하고 raw DOM/API body/screenshot 미생성
- 관찰 Finding: `UNEXPECTED_MANUAL_DELIVERY_DELTA` 자동 fail-stop 후 사용자 의도 활동임을 확인해 `AUTHORIZED_USER_ACTIVITY`로 재분류, 제품/runtime isolation 결함과 data cleanup 필요 없음, 기존 공식 runtime 유효 관찰 45분을 인정하고 다음 purge interval 1회 추가 확인
- 산출물: [Task 정의와 checklist](../tasks/uat-handover-003.md), [Implementation report](../tasks/uat-handover-003-implementation-report.md), [SOP](../tasks/uat-handover-003-sop.md), [User manual](../tasks/uat-handover-003-user-manual.md), 이 Roadmap update
- 사용자 검수: Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #33 squash merge 승인 / 미체크 항목 0

### TASK-NOTIFY-ESC-001: Escalation candidate starvation 보정

- 상태/다음 순서: 구현·자동 검증·사용자 검수 완료 / PR #34 squash merge 승인 / 다음 controlled UAT 적용은 별도 승인
- 목적: 고정된 첫 100건의 반복 점유와 후보 한 건의 오류가 poll 전체를 중단하는 P2를 제거한다.
- 포함 범위: 기존 escalation history evaluation timestamp를 재사용한 fair ordering, deterministic work item tie-breaker, 후보별 오류 격리와 cancellation 전파, 99/100/101/200/201·재시작·동시 evaluator 검증
- 제외 범위: migration/schema/API/UI/config, batch size, L0~L3·recipient 정책, escalation claim/lease, Persistent UAT 적용과 worker 활성화
- 핵심 결과: 99/100은 1 poll, 101/200은 2 poll, 201은 3 poll 이내 unique 후보 전체 평가, 후보 오류 뒤 같은 poll 진행, escalation·notification·delivery 중복 0
- ordering/watermark: 미평가·due 변경·inactive 후보 우선, active 후보는 가장 오래 평가되지 않은 순서, due date·created time·work item ID total order. 기존 `updated_at_utc`만 재사용하고 가짜 history를 만들지 않음
- query plan: isolated PostgreSQL synthetic 후보 20,000건에서 LIMIT 100·top-N sort, 약 48ms. 기존 schema/index로 수용 가능해 migration 없음
- 회귀: backend Release build·전체 suite, 신규 targeted 15/15, frontend 61/61·lint/typecheck/build, Full-Stack E2E 16/16, actionlint 통과
- Persistent UAT: read-only 전후 불변, ledger 28/29/1, Pending/Processing 0/0, active escalation 0, runtime PID와 PostgreSQL restart 유지, escalation worker disabled, actual provider call 0
- 산출물: [Task 정의와 검수 체크리스트](../tasks/notify-esc-001.md), [Implementation report](../tasks/notify-esc-001-implementation-report.md), [SOP](../tasks/notify-esc-001-sop.md), [User manual](../tasks/notify-esc-001-user-manual.md), 이 Roadmap update
- 사용자 검수: Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #34 병합 승인 / 미체크 항목 0
- 전달 계약: 기존 at-least-once 유지, exactly-once로 확대하지 않음
- 전체 신규 기능 개발: No-Go 유지

### TASK-UAT-MAINTENANCE-001: Mutation worker maintenance gate

- 상태/다음 순서: 구현·자동 검증·사용자 검수 완료 / PR #31 squash merge 승인 / 다음 TASK-UAT-HANDOVER-003 재개
- 배경: HANDOVER-003에서 Development purge worker가 무조건 등록·즉시 실행돼 all-workers-disabled Phase A를 만들 수 없는 P2 발견
- 포함 범위: `AdminDeletionPurge:Enabled` 기본 true와 strict validation, delivery·escalation·purge 조건부 DI, purge 내부 방어, worker별 runtime boolean, isolated Phase A/default 회귀
- 자동 검증: targeted 14/14, backend 331/331, frontend 61/61, Full-Stack E2E 16/16, isolated synthetic due 후보 두 관찰 구간 불변, enabled purge 회귀 성공
- Persistent UAT: migration 0028 미적용, DB write/restart 0, 기존 listener 9/9 유지, secure backup 보존
- backup 정책: 기존 pre-0028 backup은 rehearsal evidence로 보존하고 HANDOVER-003 migration 직전에 fresh backup과 isolated restore를 다시 수행
- 산출물: [Task 정의와 검수 체크리스트](../tasks/uat-maintenance-001.md), [Implementation report](../tasks/uat-maintenance-001-implementation-report.md), [SOP](../tasks/uat-maintenance-001-sop.md), [User manual](../tasks/uat-maintenance-001-user-manual.md), 이 Roadmap update
- 사용자 검수: Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #31 병합 승인 / 미체크 항목 0
- 전체 신규 기능 개발: No-Go 유지

### TASK-AUTH-HARDEN-001: Last System Administrator concurrency guard

기존 Roadmap의 `TASK-AUTH-001`을 실행 Task ID `TASK-AUTH-HARDEN-001`로 명확히 한다.

- 상태/No-Go 순서: 계획 / 4순위
- 목적: 동시에 실행되는 관리자 비활성화·역할 제거 요청에서도 마지막 active System Administrator 보호를 서버에서 보장한다.
- 포함 범위: transaction/locking 정책, 사용자 비활성화와 role 제거의 공통 guard, 동시성 integration test, 오류 응답·audit 확인
- 제외 범위: role/permission 편집 UI 전면 개편, Entra group/App Role 연동
- 선행조건: PostgreSQL 동시성 테스트 환경과 관리 action 경계 확정
- 예상 migration: 미정. 가능한 한 transaction/locking으로 해결하고 schema 변경이 필요하면 additive migration으로 분리
- 핵심 검수 기준: 경쟁 요청 중 하나만 성공하고 active System Administrator가 항상 1명 이상 유지됨
- 주요 위험: check-then-update race, 과도한 lock, 사용자/role API 간 guard 불일치

### TASK-NOTIFY-003: Teams Activity Feed 개인 알림 / 알림 운영 UX

- 상태: 완료(provider/capability 및 명시된 수동 발송 범위). 자동 event 전체 적용 완료를 의미하지 않는다.
- 목적: Teams Activity Feed actual 발송을 추가하고, 3채널 알림 운영/추적 UX를 고도화한다.
- 포함 범위: Teams Activity Feed actual provider, text topic + Teams deep link webUrl, installedAppId 운영 의존 제거, `/teams/activity` 탭, `/teams/activity/notifications/{id}` 상세, 인앱 notification 원본 구조, 개인 알림/채널 공지 접근권한, TeamsChannel/Mail/TeamsActivity 3채널 smoke, 관리자 수동 알림 발송 3모드, 업무 배정 수동 발송 시 work_item 생성, queue 방식 수동 발송, TeamsActivity/Mail 다중 수신자, display snapshot/detail, 자동/수동 알림 양식 통일, 실패/대기 확인·제외·대기 재시도, notification delivery admin handling, HTTPS local Teams test
- 제외 범위: Teams manifest/icon repo 포함, 운영 URL 확정, `projectCreated` activityType manifest 추가, 사용자별 알림 설정 UI, 실패 delivery 강제 성공 처리, delivery row hard delete, Teams DM/Bot 구현
- event coverage: 관리자 수동 개인/업무 배정은 적용, L0~L2는 설정 선택형 부분 적용, 그 밖의 자동 event는 6.5.2.2의 후속/미확인 상태를 따른다.
- 선행조건: TASK-NOTIFY-001, TASK-NOTIFY-002, TASK-ADMIN-001, Teams 앱 승인, Graph TeamsActivity 권한 승인
- 주요 테스트: backend 전체 test, Notification/Admin targeted tests, Migration tests, frontend lint/typecheck/unit/build, mock UI smoke, Full-Stack E2E, UAT health, UAT `/teams/activity` smoke, 3채널 actual smoke, secret scan

### TASK-NOTIFY-004: 외부 알림 delivery 신뢰성 및 실패 재처리

- 상태/권장 순서: 계획 / No-Go 3순위·후속 후보 B
- 목적: 동시 worker와 실패 재처리 상황에서도 외부 알림을 중복 발송하지 않고, 재시도 원인과 계보를 추적 가능하게 만든다.
- 포함 범위: delivery claim/lease, 동시 worker 중복 발송 방지, retryable/non-retryable 분류, Failed delivery 재처리와 retry lineage, escalation batch starvation 보정
- 제외 범위: 사용자별 채널 preference UI, 기존 업무 화면 feedback 전면 개편, 실제 운영 secret/채널 교체
- 선행조건: 외부 발송 없이 확인 가능한 safe UAT 검수 기반, 독립된 DB 동시성 테스트 환경, 실패 재처리 승인·감사 정책 확정
- 예상 migration: 필요 예상. claim/lease와 retry lineage를 additive migration으로 설계하고 기존 delivery 보존 및 forward-fix 정책을 명시한다.
- backend/frontend 영향: backend dispatcher/store/worker/escalation이 중심이며, frontend 관리자 delivery monitor와 재처리 action/status가 영향받는다.
- 핵심 검수 기준: 두 worker 경쟁 시 1회만 claim/발송, lease 만료 복구, 오류 분류별 retry 차등, 원본과 재시도 lineage 표시, 오래된 escalation 후보 starvation 방지
- 주요 위험: 실제 외부 중복 발송, lease 고착, 기존 Pending/Failed 이력 훼손, batch 정렬 변경에 따른 에스컬레이션 지연

### TASK-UX-001: 기존 업무 화면 Action Feedback UX 확대

- 상태/권장 순서: 계획 / 후속 기능 후보 A
- 목적: 저장·삭제·복구·발송 결과와 validation 오류를 사용자의 action 위치에서 즉시 이해하고 다음 행동으로 이어지게 한다.
- 포함 범위: A1 공통 feedback contract와 내 업무/알림, A2 생산계획/구매/자재/패널/Excel 화면의 inline feedback·field error·focus·`aria-live` 적용
- 제외 범위: 업무 규칙 변경, API 계약의 기능 확장, 알림 delivery 재처리 로직, 사용자 preference
- 선행조건: 공통 feedback, field error, focus, `aria-live`, target-not-found 계약 확정. A1 검수 후 A2를 진행한다.
- 예상 migration: 없음 예상. API 오류 계약 보정이 필요하면 runtime 범위를 Task 안에서 별도 명시한다.
- backend/frontend 영향: frontend 공통 component/hook와 각 업무 화면이 중심이며, backend는 일관된 field error 응답 확인 범위다.
- 핵심 검수 기준: action 인접 성공/실패 표시, 첫 오류 focus, screen reader 안내, 중복 submit 방지, loading/error/empty/target-not-found 다음 행동 안내, 모바일 overflow 회귀 없음
- 주요 위험: 화면별 임시 구현으로 contract가 분산되는 문제, 상단 banner와 inline feedback 중복, focus 이동 회귀, A1/A2 범위 팽창

### TASK-NOTIFY-005: 사용자별 알림 설정

- 상태/권장 순서: 계획 / 후속 기능 후보 C
- 목적: 사용자가 허용된 범위에서 event별 외부 채널 수신 방식을 조정하되 필수 업무 알림과 인앱 원본을 보존한다.
- 포함 범위: channel taxonomy, 사용자별 event/channel preference 저장·조회·수정, dispatcher 적용, 관리자/사용자 설정 UI, 기본값과 audit
- 제외 범위: 인앱 notification 원본 opt-out, 법적·업무상 필수 알림 해제, provider 신뢰성 재구현, 신규 외부 채널 추가
- 선행조건: TASK-NOTIFY-004 완료, 필수 알림 opt-out 금지 정책 확정, channel/event taxonomy 확정
- 예상 migration: 필요 예상. preference와 기본값/audit를 additive migration으로 설계한다.
- backend/frontend 영향: backend preference model/API/dispatcher와 frontend 사용자 설정 UI가 모두 영향받는다.
- 핵심 검수 기준: 필수 알림 해제 차단, 기본값 호환, event/channel별 저장과 재로그인 유지, 인앱 원본 보존, preference 변경 audit, 외부 delivery 생성 여부 검증
- 주요 위험: 필수 알림 누락, taxonomy 변경 시 기존 설정 drift, 기본값 migration 오류, 관리자 정책과 사용자 선택 충돌

현재 실행 순서는 `TASK-UAT-MAINTENANCE-001 사용자 검수/merge → TASK-UAT-HANDOVER-003 preflight 재개 → fresh backup/restore rehearsal → TASK-NOTIFY-ESC-001 → TASK-AUTH-HARDEN-001 → TASK-GOV-002`이다. `TASK-NOTIFY-REL-001`과 `TASK-NOTIFY-ESC-001`은 `TASK-NOTIFY-004` umbrella 중 claim/lease와 escalation starvation P2를 각각 분리한 실행 Task다. 기능 후보 순서는 `TASK-UX-001(A1 → A2) → TASK-NOTIFY-005`이며, UX-001은 NOTIFY-004와 묶지 않고 별도 검수한다. 전체 신규 기능 No-Go는 남은 P2 remediation이 닫힐 때까지 유지한다.

### TASK-007A: Pending List 공통 모듈

- 목적: 부적합, PUNCH, 제조 중단, 기타 이슈를 공통 모듈로 관리
- 포함 범위: Pending 생성, 상태, 조치 담당, 코멘트, 첨부, 긴급 알림
- 제외 범위: 검사별 상세 체크리스트 전체
- 선행조건: 내 업무/알림 기반
- 주요 테스트: 생성, 조치, 재검사 요청, 권한, 중복 방지

### TASK-008A: 자재 도착 / IQC 요청 / 입고 확정

- 목적: 구매품목 도착부터 입고 확정까지 자재 흐름 구현
- 포함 범위: 자재 도착, IQC 요청, 입고 확정, 구매품목 상태
- 제외 범위: IQC 상세 성적서 전체
- 선행조건: 구매정보, Pending List 기반
- 주요 테스트: 도착 등록, IQC 요청, 부적합 차단, 입고 확정

### TASK-009A: 검사 체크리스트 템플릿 / IQC 디지털 성적서 / PDF 출력 기반

- 목적: 검사성적서 디지털화 시작
- 포함 범위: IQC 체크리스트, 사진 필수, 결과, PDF snapshot 기반
- 제외 범위: LQC/OQC/FAT 전체
- 선행조건: 첨부파일 정책, Pending List
- 주요 테스트: 필수 사진, 값 입력, PDF 생성, 부적합 Pending

### TASK-010A: 키팅 완료 / 제조 내 업무 생성

- 목적: 자재 입고 확정 후 제조 투입 가능 상태 연결
- 포함 범위: 키팅 완료, 부분/일괄 처리, 제조 내 업무 생성
- 제외 범위: 제조 작업 체크리스트
- 선행조건: 자재 입고 확정
- 주요 테스트: 키팅 완료, 제조 업무 생성, 중복 방지

### TASK-011A: 제조 체크리스트 / 작업 시작·종료 / 제조 중단

- 목적: 제조현황 디지털화
- 포함 범위: 제조 단계, 작업 시작/종료, 제조 중단, Pending 연결
- 제외 범위: 품질 검사 상세
- 선행조건: 제조 단계 목록 확정
- 주요 테스트: 모바일 입력, 중단 등록, 권한, 이력

### TASK-012A: LQC / OQC / 전진검수 / FAT

- 목적: 후속 품질 검사 단계 구현
- 포함 범위: LQC, OQC, 전진검수, FAT 선택, PUNCH LIST
- 제외 범위: 물류 상세
- 선행조건: 검사성적서 양식, 사진 필수 위치 회신
- 주요 테스트: 검사 결과, PUNCH, 재검사, FAT optional 처리

### TASK-013A: 물류 포장 / 출발 / 납품 완료

- 목적: 포장부터 납품 완료까지 물류 흐름 구현
- 포함 범위: Packing Unit, 포장사진, 상차사진, 거래명세서 서명본
- 제외 범위: 영업 정산
- 선행조건: 품질 완료 기준
- 주요 테스트: 포장 구성, 사진 필수, 출발, 납품 완료

### TASK-014A: 영업 정산 / 세금계산서 / 프로젝트 완료

- 목적: 납품 후 영업 정산과 최종 프로젝트 완료 처리
- 포함 범위: 세금계산서 발행 체크, 완료 조건, 프로젝트 완료
- 제외 범위: 외부 회계 연동
- 선행조건: 납품 완료
- 주요 테스트: 완료 조건, 미납품 차단, 권한, 이력

### TASK-EXPORT-001: 모든 페이지 Excel 출력 공통 기능

- 목적: 조회 화면별 Excel export를 공통 구조로 제공
- 포함 범위: 현재 필터 반영, 컬럼 선택, 권한, audit
- 제외 범위: 복잡한 보고서 PDF
- 선행조건: 주요 화면 데이터 모델 안정화
- 주요 테스트: 권한, 필터, 파일 타입, 개인정보 노출 방지

## 24. 추적 대상 리스트

| 번호 | 항목 | 상태 | 담당/출처 | 후속 TASK | 비고 |
| --- | --- | --- | --- | --- | --- |
| 1 | 18단계 이미지 기준 확정 | 확정 | 사용자 제공 이미지/회의 결정 | TASK-006A/006B | 2번 생산관리, 3번 설계 |
| 2 | LQC 성적서 입력 방식 | 미확정 | 품질/현업 회신 | TASK-012A | 체크 항목, 값 입력 방식 필요 |
| 3 | LQC 사진 필수 위치 | 미확정 | 품질/현업 회신 | TASK-012A | requires_photo 후보 |
| 4 | OQC 성적서 입력 방식 | 미확정 | 품질/현업 회신 | TASK-012A | 자체검수 양식 필요 |
| 5 | OQC 사진 필수 위치 | 미확정 | 품질/현업 회신 | TASK-012A | 저장 차단 기준 필요 |
| 6 | 자주순차표 큰 틀 | 부분 확정 | 생산관리/제조 회신 | TASK-011A | 웹 적용 확정, 상세 항목 회신 필요 |
| 7 | 제조 화면 표시 항목 | 미확정 | 제조/생산관리 회신 | TASK-011A | 항상 표시 항목 |
| 8 | 제조 팝업 표시 항목 | 미확정 | 제조/생산관리 회신 | TASK-011A | 상세 입력 팝업 후보 |
| 9 | 제조 저장-only 항목 | 미확정 | 제조/생산관리 회신 | TASK-011A | 저장되지만 상시 표시 불필요 |
| 10 | 제조 LQC 요청 기준 | 미확정 | 제조/품질 회신 | TASK-011A/012A | 자동 생성 event 기준 필요 |
| 11 | 검사성적서 PDF 양식 | 미확정 | 품질/고객양식 회신 | TASK-009A/012A | PDF snapshot 필요 |
| 12 | IQC 체크리스트 상세 항목 | 미확정 | 품질 회신 | TASK-009A | 외함 사진 필수는 확정 |
| 13 | LQC 체크리스트 상세 항목 | 미확정 | 품질 회신 | TASK-012A | 성적서 양식 필요 |
| 14 | OQC 체크리스트 상세 항목 | 미확정 | 품질 회신 | TASK-012A | 성적서 양식 필요 |
| 15 | FAT 필요 여부 기본값 | 확정 | 사용자 검수/TASK-006B | TASK-006B/012A | 기본 false, 프로젝트별 선택 |
| 16 | 구매 업체 입력 방식 | 확정 | 사용자 검수/TASK-006B | TASK-006B 또는 후속 구매 TASK | 업체 header/field 포함, 업체 master는 후속 |
| 17 | Pending List 상태값 | 초안 | 사용자 논의 | TASK-007A | 등록/조치 요청/조치 중/재검사 요청/종결 |
| 18 | 조치 담당 부서 목록 | 초안 | 사용자 논의 | TASK-007A | 귀책부서 표현 금지 |
| 19 | 부적합 조치 유형 상세 | 부분 확정 | 사용자 논의 | TASK-007A/008A/012A | 반송/현장 수리 흐름 |
| 20 | 포장 구성 입력 필드 | 미확정 | 물류 회신 | TASK-013A | 포장번호, 규격, 중량 등 |
| 21 | 영업 정산 항목 | 부분 확정 | 사용자 논의 | TASK-014A | 세금계산서 완료는 확정 |
| 22 | 모든 페이지 Excel 출력 범위 | 초안 | 사용자 요청 | TASK-EXPORT-001 | 공통 export 권장 |
| 23 | Microsoft 365 로그인 적용 시점 | 완료 | 인프라/운영 결정 | TASK-INFRA-001 | 인증 기반 구현 완료. 운영 배포 전 실제 Entra 설정, 운영 redirect URI, Production/Staging dev auth 및 AdminUserSwitch 비활성 검수 필요 |
| 24 | 관리자 페이지 범위 | 완료 | 사용자 요청 | TASK-ADMIN-001 | 시스템 관리 중심으로 구현 완료. 업무 부서 입력 기준정보는 후속 결정 |
| 25 | 프로젝트 대표 상태 방식 | 확정 | 실무 협의 | 상태 집계 구현 TASK | 병목 기준 + 진행률 |
| 26 | 알림 채널 구성 | 부분 완료 | 실무 협의 | TASK-NOTIFY-001/002/003/004/005 | 인앱 원본, Teams 통합 채널, Gmail SMTP, Activity Feed provider actual, delivery 이력과 에스컬레이션 엔진은 구현. 자동 event coverage, delivery 신뢰성, 사용자 preference와 운영 URL/manifest 검수는 후속 |
| 27 | 진행률(%) 계산식 정의 | 확정 | 실무 협의 | 상태 집계 구현 TASK | 완료된 필수 workflow 단계 수 / 전체 필수 workflow 단계 수. FAT는 대상 프로젝트만 분모 포함. 프로젝트 상태 집계는 9장 기준. |
| 28 | Teams 통합 채널 생성 및 Webhook URL | 검수 완료 | 사용자 | TASK-NOTIFY-001 | 테스트 채널/Webhook actual 검수 완료. 운영 전 Webhook 재발급과 secret 주입 필요 |
| 29 | 알림 전용 메일 계정 생성 | 검수 완료 | 사용자 | TASK-NOTIFY-001 | Hiworks/M365 Graph Mail.Send 대신 Gmail SMTP 초기 경로 사용. 장기 운영 발송 수단 검토 필요 |
| 30 | Graph API 앱 등록 및 권한 승인 | 검수 완료 | 사용자 | TASK-INFRA-001 / TASK-NOTIFY-003 | 로그인 앱 등록은 INFRA-001에서 사용. Mail.Send는 기본 경로에서 제외. TeamsActivity.Send 권한 승인 및 Teams Activity actual smoke 완료 |
| 31 | 퇴사/부서이동 시 미완료 내 업무 이관 규칙 | 미확정 | 실무 협의 | TASK-INFRA-001 이후 | 담당자 부재 시 업무 귀속 처리 |
| 32 | 에스컬레이션 기한의 관리자 설정 가능 여부 | 미확정 | 실무 협의 | TASK-NOTIFY-002 이후 | L0/L1/L2/L3 기준은 코드 고정으로 구현. 관리자 설정 UI는 후속 검토 |
| 33 | dev user 담당 프로젝트/내 업무의 실계정 이관 수동 절차 | 미확정 | 실무 협의 | INFRA-001 이후 | 자동 병합 금지에 따른 후속 |
| 34 | Teams Activity Feed provider/capability | 완료 | 사용자/관리자 | TASK-NOTIFY-003 | Teams 앱 manifest/조직 앱/Graph 권한/text topic + Teams deep link actual 발송 검수 완료. 사용자별 installedAppId 운영 의존은 제거했다. 자동 event coverage는 6.5.2.2에서 별도 관리하고 운영 전 URL 전환 필요 |
| 35 | Gmail SMTP 운영 적합성 및 공식 발송 수단 전환 | 미확정 | 사용자/총무/보안 | 운영 전 검토 | Gmail SMTP는 초기/UAT/시범운영용. 발송량, 보안, 스팸 정책과 회사 공식 발송 수단 전환 검토 |
| 36 | 운영용 Teams Webhook 재발급 | 미확정 | 사용자/운영 | 운영 배포 전 | UAT/test Webhook과 운영 Webhook을 분리하고 secret/env로만 주입 |
| 37 | 대한민국 공휴일 데이터 동기화 service key | 미확정 | 사용자/운영 | CALENDAR sync 후속 | 공식 API sync 구조는 있으나 service key 준비 전까지 관리자 Excel/manual 등록 사용 |
| 38 | 회사 휴일 연간 등록/검수 | 부분 완료 | System Administrator | 운영 전 검수 | 관리자 휴일 관리 API/UI와 Excel 일괄 등록은 구현 완료. 운영 전 연간 Company holiday 입력 필요 |
| 39 | 회사 자체 근무일 지정 필요성 | 미확정 | 사용자/운영 | CALENDAR 후속 | 이번 TASK에서는 구현하지 않음. 필요 시 휴일 override 모델 별도 검토 |
| 40 | 생산계획/구매 예정일의 work_items.due_date 동기화 | 미확정 | 사용자/운영 | TASK-NOTIFY-002 이후 | NOTIFY-002는 엔진만 구현. 생산계획 planned_date, 구매 expected_receipt_date, 업무 입력 UX와 due_date 연결 정책 결정 필요 |
| 41 | due_date 없는 기존 업무 처리 정책 | 미확정 | 사용자/운영 | TASK-NOTIFY-002 이후 | due_date null 업무는 에스컬레이션 제외. 운영 적용 전 due_date 입력/보강 기준 필요 |
| 42 | Daily Digest HTML table 개선 여부 | 미확정 | 사용자/운영 | 알림 UX 후속 | 담당 프로젝트 요약은 plain text renderer 기준으로 구현. 필요 시 HTML table 개선 |
| 43 | Item 관리자 관리 여부 | 미확정 | 사용자/운영 | ADMIN 후속 | ADMIN-001에서는 제외. Item 신규 추가/정렬/비활성화 정책은 별도 결정 필요 |
| 44 | 포장방식 기준정보화 및 size_required | 미확정 | 사용자/운영 | ADMIN/패널 후속 | ADMIN-001에서는 제외. 패널 완료 판정, 프로젝트 입력, Excel 회귀 범위 검토 필요 |
| 45 | 생산계획 단계/구매 필수 항목 관리자 통합 | 미확정 | 사용자/운영 | ADMIN 후속 | 현재는 각 업무 영역 설정으로 유지 |
| 46 | role/permission 편집 UI | 미확정 | 사용자/운영 | ADMIN 후속 | ADMIN-001은 read-only 권한 매트릭스만 제공 |
| 47 | 삭제 예정 데이터 purge 운영 정책 | 미확정 | 사용자/운영 | 운영 고도화 | 7일 후 purge worker는 구현. 보류 데이터 처리/운영 알림은 후속 검토 |
| 48 | 전체 field-level audit 확장 | 미확정 | 사용자/운영 | Audit 후속 | ADMIN-001은 관리자 변경 이력 중심 |
| 49 | 관리자 모바일 UX 고도화 | 미확정 | 사용자/운영 | ADMIN 후속 | ADMIN-001은 page-level overflow 방지 기준으로 검수 |
| 50 | 외부 알림 delivery 동시성·실패 재처리 | 계획 | 개발/운영 | TASK-NOTIFY-004 | safe UAT와 DB 동시성 테스트 환경을 먼저 준비하고 claim/lease·retry lineage·starvation을 함께 검증 |
| 51 | 기존 업무 화면 Action Feedback UX | 계획 | 사용자/개발 | TASK-UX-001 | A1 공통 계약과 내 업무/알림을 먼저 검수한 뒤 A2 업무 화면으로 확대 |
| 52 | 사용자별 알림 설정 | 계획 | 사용자/운영 | TASK-NOTIFY-005 | NOTIFY-004 완료와 필수 알림 opt-out/channel taxonomy 결정이 선행 |
| 53 | Task 종료 5종 산출물과 개인정보 기준 | 완료 | BASELINE-GOV-001 | [Task 종료 및 산출물 정책](12-task-completion-policy.md) | 사용자 승인 후 PR #21 squash merge. canonical policy를 사용하고 Roadmap/AGENTS에는 세부 규칙을 중복 정의하지 않음 |
| 54 | Full-Stack E2E PostgreSQL 물리 격리 | 완료 | 개발/운영 | TASK-E2E-ISOLATION-001 | 전용 container/network/tmpfs, `emi_qms_e2e_*` guard, 외부 provider 차단, Full-Stack E2E 16개 통과. PR #22 squash merge `45fd61c` |
| 55 | HTTPS Development UAT 안정화 | 자동 검증·사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-UAT-001 | strict port/ownership, protocol readiness, notification env, master-data transaction, isolated E2E와 persistent UAT 보존. PR #23 |
| 56 | Frontend dependency security | 자동 검증·사용자 검수 완료 / merge 승인 | 개발/보안 | TASK-FRONTEND-SEC-001 | Vite 7.3.6, esbuild 0.28.1, Vitest 4.1.0. Audit 전체 0, frontend/backend/E2E와 5174/5185 비교 검수 통과. PR #24 |
| 57 | Review-safe UAT | 자동 검증·사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-UAT-002 | 5092/5190, startup·worker·provider·HTTP mutation 차단, DB session read-only, schema readiness, Development UAT 분리. PR #26 |
| 58 | UAT 통합 사용자 검수 | 자동 검증·사용자 검수 완료 / merge 승인 | 사용자/개발 | UAT-VERIFY-001 | 최신 main runtime·ledger/schema/data/권한/dashboard/Review-safe/UI 기준선과 개인정보 안전 merge projection 통과. UAT 기준선 Go, 신규 기능 No-Go 유지, PR #29 병합 승인 |
| 59 | Notification delivery claim/lease | 자동 검증·사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-NOTIFY-REL-001 | Processing·SKIP LOCKED·lease/fencing·attempt audit, 정상 경쟁 provider call 1회, isolated candidate 5094/5192. Persistent UAT 0028 미적용, actual provider 호출 0, at-least-once이며 exactly-once 미보장. PR #30 |
| 60 | Escalation starvation | 구현·자동 검증·사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-NOTIFY-ESC-001 | 기존 evaluation timestamp fair ordering, 후보 오류 격리, 101/200/201 유한 poll, 중복 0. Persistent UAT worker는 disabled 유지 |
| 61 | 마지막 System Administrator 동시성 보호 | 계획 | 개발/운영 | TASK-AUTH-HARDEN-001 | 경쟁 비활성화·role 제거 요청에서도 active System Administrator 1명 이상을 transaction/locking과 integration test로 보장 |
| 62 | Git history 개인정보 | risk decision 필요 | 사용자/보안 | TASK-GOV-002 | current checkout은 비식별화하되 history rewrite·force push는 본 Task에서 금지. 저장소 공개 범위에 따라 별도 결정 |
| 63 | Patched frontend UAT handover | 자동 검증·사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-UAT-HANDOVER-001 | 최신 main Vite 7.3.6 frontend를 5174에 인계. Teams client 검수, Backend/PostgreSQL 보존과 DB snapshot 확인 완료. PR #25 |
| 64 | Migration ledger 전체 집합 검증 | 자동 검증·사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-DB-MIGRATION-001 | canonical 27/live 28/approved legacy 1, full-set compare, schema probe, mismatch 503, candidate 5191/5093, live row 미변경. PR #27 |
| 65 | Privacy-safe Review-safe runtime handover | 자동 검증·사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-UAT-HANDOVER-002 | merged main 5190/5092, Compatible 27/28/1, redacted browser matrix, DB read-only·423, Candidate/Persistent UAT 보존. PR #28 |
| 66 | Notification claim/lease UAT handover | 사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-UAT-HANDOVER-003 | Persistent UAT 0028, canonical 28 + approved legacy 1 = live 29, Review-safe/Development controlled handover, 승인된 ManualTest 단일 Sent lineage와 unrelated provider call 0 |
| 67 | Repository 지침·Rules 이관 | 구현·자동 검증·사용자 검수 완료 / Draft PR 게시 대상 / merge 대기 | 개발 | TASK-GOV-CODEX-001 | 전역·영역별 지침, 종료 정책, 검증 matrix, privacy-safe evidence와 command rules의 역할을 분리하고 신규 기능 기획 템플릿에서 공통 장문 규칙을 제거. Shell wrapper는 prompt하되 내부 semantic 완전 차단은 미보장 |
| 68 | Mutation worker maintenance gate | 구현·자동 검증·사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-UAT-MAINTENANCE-001 | purge 기본 true·explicit disable, 세 mutation worker 조건부 DI와 runtime projection, Phase A isolated 검증. Persistent UAT/0028 무변경 |

## 25. 결정 이력 (Decision Log)

향후 방향이 바뀌는 결정이 있을 때마다 이 표에 한 줄씩 누적한다. 기존 행은 삭제하지 않는다.

| 날짜 | 결정 사항 | 이유 | 관련 섹션 |
| --- | --- | --- | --- |
| 2026-07-02 | 프로젝트 대표 상태는 병목 기준 + 진행률 병기 | 생산관리 관점은 병목 파악이 우선 | 9장 |
| 2026-07-02 | 단계는 전진만, 차단은 플래그로 관리 | 단계 번호 후퇴 시 이력 해석 불가 | 9장 |
| 2026-07-02 | 프로젝트 완료 조건에 오픈 Pending 0건 포함 | 미종결 PUNCH 상태의 완료 처리 방지 | 9장, 18장 |
| 2026-07-02 | 알림 채널 3종 확정 (인앱/Teams/메일) | 인앱 기 구현, Teams·메일 확장 계획 확정 | 6장 |
| 2026-07-02 | 일일 요약 메일 07:30 발송 | 출근 직후 확인 | 6장 |
| 2026-07-02 | 에스컬레이션 L2 +2영업일, L3 +3영업일 | 실무 리드타임 기준 | 6장 |
| 2026-07-02 | 야간 억제 미적용 | 운영 단순화 | 6장 |
| 2026-07-02 | 긴급 알림 Teams 게시는 통합 채널 1개 | 초기 관리 단순화 | 6장 |
| 2026-07-02 | L3 수신자는 생산관리 및 영업으로 한정, 경영진 미포함 | 조직 구조상 부서장 단계 없음, 영업은 프로젝트 총괄 관점에서 포함 | 6장 |
| 2026-07-02 | 시스템 공식 명칭을 “EMI 프로젝트 통합관리시스템”으로 확정 | 품질관리 범위를 넘는 프로젝트 통합관리 시스템으로 방향 확정 | 1장, 2장 |
| 2026-07-02 | 내부 코드명(Emi.Qms 솔루션/네임스페이스)은 유지, 리네이밍 불필요 | 명칭 확정은 사용자 표시명에 적용하며 코드 리네이밍은 파괴적 변경 | 27장 |
| 2026-07-02 | 백엔드 스택은 현행 ASP.NET Core (.NET) 유지, 전환 없음 | TASK-006까지 구현 완료된 현행 구조를 유지하는 것이 전환 비용 대비 합리적 | 27장 |
| 2026-07-02 | 진행률 = 완료된 필수 workflow 단계 수 / 전체 필수 workflow 단계 수로 확정 | 단순 개수 기준으로 초기 충분하며 가중치는 필요 시 후속 도입 | 7장, 9장 |
| 2026-07-02 | 담당자 fallback 순서 확정(Primary → Secondary → 영업 정 → 영업 부 → System Administrator) | 기존 구현 규칙을 문서화하여 담당자 부재 시 업무 누락을 방지 | 5장 |
| 2026-07-02 | 추적 단위 용어를 “패널” 단독 표기로 통일하고 “제품/패널” 병기 폐기 | 사용자 결정에 따라 실무 용어를 단순화 | 전체 |
| 2026-07-02 | 품질 담당자 구조를 검사 단계별 정/부 담당자 구조로 확정 | 실제 구현과 운영 기준을 일치시키고 정담당자 부재 시 fallback을 보장하기 위함 | 5장 |
| 2026-07-02 | 운영 인증은 MSAL(React) + JWT Bearer(Microsoft.Identity.Web)로 확정 | React SPA + ASP.NET Core 표준 패턴이며 NOTIFY-001 Graph 기반 확장과 공유 가능 | 20장 |
| 2026-07-02 | 부서/역할은 앱 내 관리, Entra는 인증만 담당 | 테넌트 관리자 의존을 줄이고 ADMIN-001 사용자 관리와 연결 | 20장 |
| 2026-07-02 | 신규 Entra 사용자는 승인 대기(역할 0개로 판정), 역할 지정 전 업무 데이터 조회 불가 | 권한 서버 강제 원칙과 정합하며 정보 노출을 방지 | 20장 |
| 2026-07-02 | dev user와 실계정 자동 병합 금지, 담당자 이관은 수동 절차 | 오연결 시 이력/담당자 데이터 훼손 위험 | 20장 |
| 2026-07-02 | Entra 앱 등록 표시명은 공식 명칭 기준 | 로그인/동의 화면 노출 시 명칭 기준 준수 | 20장 |
| 2026-07-02 | 승인 대기 해소 기준은 active role 1개 이상으로 확정 | 별도 상태 컬럼 없이 역할 부여만으로 승인 상태를 일관되게 관리 | 20장 |
| 2026-07-02 | Dev 사용자는 INFRA-001 최소 사용자 관리 화면에서 읽기 전용으로 표시 | Dev 인증은 InMemoryIdentityStore를 유지하므로 DB 수정 UI와 분리 필요 | 20장 |
| 2026-07-02 | 마지막 active System Administrator 보호 정책을 적용 | 관리자 권한 상실로 시스템 관리가 불가능해지는 상황 방지 | 20장 |
| 2026-07-02 | TASK-INFRA-001에서 Microsoft 365 로그인 기반 구현 완료 | EntraId 기반 운영 인증, 승인 대기, bootstrap admin, Dev mode 보존을 구현 | 20장, 23장 |
| 2026-07-02 | System Administrator에 한해 비운영 환경에서 검수 사용자 전환을 허용 | 실제 Microsoft 로그인 기반을 유지하면서도 기능 검수 효율을 확보하기 위함 | 20장, 27장 |
| 2026-07-02 | 로그인 상태 유지는 MSAL cache와 silent token acquisition 기준으로 제공 | Microsoft 보안 정책을 우회하지 않으면서 반복 인증 부담을 줄이기 위함 | 20장, 27장 |
| 2026-07-03 | TASK-NOTIFY-001에서 외부 알림 delivery 계층을 구현 | 인앱 알림을 원본으로 유지하면서 Teams/Mail 발송 이력을 분리 관리하기 위함 | 6장, 23장 |
| 2026-07-03 | 초기 메일 발송은 Gmail SMTP 전용 계정으로 처리 | 사내 정책상 Hiworks SMTP와 Microsoft Graph Mail.Send를 기본 발송 경로로 사용하지 않기로 결정 | 6장 |
| 2026-07-03 | 역사적 결정: Teams 개인별 알림을 Activity Feed 후속 TASK로 분리 | Teams 앱/manifest/Graph 권한/조직 배포가 필요한 별도 범위였으며 provider/capability는 이후 TASK-NOTIFY-003에서 완료 | 6장, 23장 |
| 2026-07-03 | 영업일 기준은 토/일, 대한민국 공휴일, 대체공휴일, 임시공휴일, 회사휴일을 비영업일로 계산 | 생산계획 캘린더와 예정일 에스컬레이션 기준을 통일하기 위함 | 9장, 10장 |
| 2026-07-03 | 회사휴일은 System Administrator가 수동 등록하고 Excel 일괄 등록을 지원 | 공식 공휴일 API service key 없이도 운영 휴일 데이터를 관리하기 위함 | 20장, 23장 |
| 2026-07-03 | NOTIFY-002 에스컬레이션은 BusinessDayCalculator를 재사용 | 알림 날짜와 생산계획 캘린더의 영업일 기준 불일치를 방지하기 위함 | 6장, 23장 |
| 2026-07-03 | 예정일 에스컬레이션은 `work_items.due_date` 기반 엔진만 구현하고, 세부 due_date 입력/동기화 정책은 후속 확정 | 생산계획/구매 예정일이 업무 기한인지 대상 일정인지 아직 확정되지 않았기 때문 | 6장, 23장 |
| 2026-07-03 | L0는 예정일의 직전 영업일 기준으로 확정 | 달력일 기준보다 회사 영업일 기준 알림이 실무에 적합 | 6장 |
| 2026-07-03 | Daily Digest에 담당 프로젝트 요약을 포함 | 담당자가 매일 자신의 담당 프로젝트와 납기/역할을 함께 확인할 수 있게 하기 위함 | 6장 |
| 2026-07-03 | ADMIN-001은 시스템 관리 중심으로 범위를 제한하고, Item/포장방식/생산계획 단계/구매 필수 항목 관리는 제외 | 각 부서가 업무 중 입력·관리하는 기준정보를 관리자 페이지에서 과도하게 통합하지 않기 위함 | 20장, 23장 |
| 2026-07-03 | 관리자 삭제는 삭제 예정 상태로 전환하고 7일 내 복구 가능하게 설계 | 실수 삭제를 방지하고 복구 기간을 제공하기 위함 | 20장 |
| 2026-07-03 | 삭제 예정 데이터는 재삭제 시 즉시 완전 삭제를 시도하되, 참조 데이터가 있으면 삭제 보류 처리 | 관리자 통제권과 데이터 무결성을 동시에 보장하기 위함 | 20장 |
| 2026-07-03 | 모든 TASK 완료 전 사용자 검수 체크리스트를 포함 | 자동 테스트 외 실제 화면 검수를 누락하지 않기 위함 | 27장 |
| 2026-07-08 | Teams Activity Feed actual 발송은 text topic + Teams deep link webUrl을 기본으로 사용 | 사용자별 installedAppId 운영 관리를 제거하고 Teams Activity 클릭 시 인앱 알림 상세로 이동시키기 위함 | 6장, 23장 |
| 2026-07-08 | 관리자 수동 알림 발송은 provider 동기 호출이 아니라 Pending delivery queue 저장 방식으로 처리 | 관리자가 발송 버튼 클릭 후 오래 기다리지 않고, worker/retry/이력 구조와 일관되게 운영하기 위함 | 6장, 23장 |
| 2026-07-08 | 수동/자동 알림의 Mail/TeamsChannel/TeamsActivity 표시 양식을 통일 | 채널별 표현 차이를 줄이고 알림발송상태에서 제목, 유형, 프로젝트, 수신자를 일관되게 추적하기 위함 | 6장, 23장 |
| 2026-07-08 | 관리자 수동 업무 배정 알림은 실제 work_item을 생성한다 | 업무 배정 알림이 수신자의 내 업무와 연결되지 않는 구조를 방지하기 위함 | 6장, 23장 |
| 2026-07-08 | Teams manifest/icon은 repo에 포함하지 않고 배포 패키지는 운영자가 별도 관리 | 앱 패키지와 아이콘은 조직 Teams 앱 배포 산출물이며 코드 repo에 민감/운영 파일을 섞지 않기 위함 | 23장, 27장 |
| 2026-07-10 | 모든 Task 종료 기준은 canonical 5종 산출물 정책을 사용하고 미적용 항목도 이유와 함께 N/A로 기록 | 문서 수가 아니라 산출물의 추적성, Finding gate, 검수 상태를 일관되게 관리하기 위함 | [Task 종료 및 산출물 정책](12-task-completion-policy.md), 27장 |
| 2026-07-10 | 사용자 검수 증빙은 역할명 또는 익명 사용자 A/B만 기록하고 실제 실명·회사 이메일·UPN은 기록하지 않는다 | tracked 문서의 개인정보 노출을 방지하면서 검수 흐름과 증빙 의미를 보존하기 위함 | [Task 종료 및 산출물 정책](12-task-completion-policy.md) |
| 2026-07-10 | Teams Activity Feed provider/capability 완료와 개별 자동 event coverage를 별도 상태로 관리 | provider가 activity type을 처리할 수 있다는 사실만으로 event delivery 연결까지 완료 처리하지 않기 위함 | 6장, 21장, 23장 |
| 2026-07-10 | 후속 기능 후보 B/A/C의 상대 순서는 TASK-NOTIFY-004 → TASK-UX-001 → TASK-NOTIFY-005 | delivery 신뢰성을 먼저 확립하고 공통 feedback을 분리 검수한 뒤 preference를 적용하기 위함. 전역 No-Go remediation 선행 순서는 별도 행을 따른다 | 23장, 24장 |
| 2026-07-10 | 기존 `docs/task-close-process-guidelines`의 유효 규칙은 BASELINE-GOV-001 canonical 정책에 수동 통합하고 기존 branch는 대체 상태로 보존 | 오래된 branch를 merge/cherry-pick하지 않고 5종 산출물·검수 상태를 포함한 최신 정책으로 drift를 해소하기 위함 | 23장, [Task 종료 및 산출물 정책](12-task-completion-policy.md) |
| 2026-07-10 | Git history 개인정보는 current checkout 비식별화와 분리해 risk decision으로 관리 | history rewrite는 commit hash와 협업 branch를 변경하는 별도 승인 작업이기 때문 | 24장 |
| 2026-07-10 | 전역 No-Go remediation은 TASK-UAT-001 → TASK-SEC-001 → TASK-NOTIFY-004 → TASK-AUTH-001 순서로 수행(당시 결정, 다음 행의 현재 순서로 대체됨) | 안전한 검수 기반, dependency 보안, 외부 delivery 동시성, 마지막 관리자 경쟁 조건을 신규 기능보다 먼저 해소하기 위함 | 23장, 24장 |
| 2026-07-10 | 현재 다음 실행 순서는 TASK-UAT-001 재개 → TASK-FRONTEND-SEC-001 → TASK-UAT-002 → UAT-VERIFY-001 | HTTPS Development UAT WIP를 먼저 완료하고 dependency 보안과 Review-safe mode를 분리한 뒤 통합 사용자 검수로 gate를 닫기 위함 | 23장, 24장 |
| 2026-07-10 | Full-Stack E2E는 실행별 전용 PostgreSQL container/network/tmpfs와 `emi_qms_e2e_*` guard를 사용 | host `psql` 부재 시 persistent UAT fallback과 DB 이름 오설정의 삭제 위험을 제거하기 위함 | 23장, 24장, TASK-E2E-ISOLATION-001 |
| 2026-07-10 | TASK-UAT-001 이후 remediation 순서를 TASK-FRONTEND-SEC-001 → TASK-UAT-002 → UAT-VERIFY-001 → TASK-NOTIFY-REL-001 → TASK-NOTIFY-ESC-001 → TASK-AUTH-HARDEN-001로 확정 | Development UAT 안정화 후 dependency 보안과 Review-safe mode를 닫고, notification reliability·starvation·마지막 관리자 동시성을 분리 검증하기 위함 | 23장, 24장, TASK-UAT-001 |
| 2026-07-10 | TASK-FRONTEND-SEC-001은 Vite 7.3.6, esbuild 0.28.1, Vitest 4.1.0으로 audit 전체 0을 달성하고 실제 5174 반영은 TASK-UAT-HANDOVER-001로 분리 | 현재 실행 중인 patch 전 UAT를 보존하면서 dependency 변경 검증과 runtime 교체 위험을 분리하기 위함 | 23장, 24장, TASK-FRONTEND-SEC-001 |
| 2026-07-10 | 현재 remediation 순서를 TASK-UAT-HANDOVER-001 → TASK-UAT-002 → UAT-VERIFY-001 → TASK-NOTIFY-REL-001 → TASK-NOTIFY-ESC-001 → TASK-AUTH-HARDEN-001 → TASK-GOV-002로 갱신 | Patched dependency를 실제 UAT runtime에 안전하게 반영한 뒤 Review-safe mode와 통합 검수를 진행하기 위함 | 23장, 24장 |
| 2026-07-10 | TASK-UAT-HANDOVER-001은 최신 main detached runtime을 5186에서 검증한 뒤 frontend 5174만 교체하고 Backend 5081·persistent PostgreSQL·5185 Preview를 유지 | 문서 branch와 runtime tree를 분리하고 전체 UAT 재시작 없이 보안 patch를 실제 Teams/UAT 주소에 적용하기 위함 | 24장, TASK-UAT-HANDOVER-001 |
| 2026-07-10 | TASK-UAT-HANDOVER-001의 5174·Teams client·기존 Activity 상세·SOP/User manual 사용자 검수를 완료하고 PR #25 병합을 승인 | Patched runtime handover의 자동 증빙과 사용자 직접 검수 gate를 모두 닫고 다음 remediation을 TASK-UAT-002로 전환하기 위함 | 24장, TASK-UAT-HANDOVER-001 |
| 2026-07-10 | TASK-UAT-002는 Development 5174/5081과 분리된 Review-safe 5190/5092에서 startup·worker·provider·HTTP·identity·DB의 다층 read-only를 강제 | 감사/기준선 조회에서 DB와 외부 시스템 변경을 차단하면서 Development UAT의 저장·worker 검수 능력을 유지하기 위함 | 23장, 24장, TASK-UAT-002 |
| 2026-07-10 | TASK-UAT-002의 5190 조회·mutation 차단·SOP/User manual 사용자 검수를 완료하고 PR #26 병합을 승인 | 자동 방어 증빙과 사용자 직접 검수 gate를 모두 닫고 다음 remediation을 UAT-VERIFY-001로 전환하기 위함 | 23장, 24장, TASK-UAT-002 |
| 2026-07-10 | UAT-VERIFY-001은 repository 27개와 live ledger 28개 차이를 latest-only readiness가 놓치는 P2로 중단하고 TASK-DB-MIGRATION-001을 선행 | 전체 migration set과 schema 호환성을 증명하지 않은 상태에서 Persistent UAT 통합 검증을 완료로 오판하지 않기 위함 | 23장, 24장, UAT-VERIFY-001 |
| 2026-07-10 | `0020_teams_activity_delivery_channel`은 동일 blob의 canonical `0023` successor와 schema probe가 모두 확인될 때만 승인 legacy로 보존 | live 감사 이력을 삭제하지 않으면서 unknown/missing/유사 marker를 fail-closed로 차단하기 위함 | 23장, 24장, TASK-DB-MIGRATION-001 |
| 2026-07-10 | TASK-DB-MIGRATION-001의 Candidate 5191·27/28/1 호환 상태·legacy marker 보존·SOP/User manual 사용자 검수를 완료하고 PR #27 병합을 승인 | full-set readiness의 자동 증빙과 사용자 직접 검수 gate를 닫고 다음 단계를 Review-safe controlled handover로 전환하기 위함 | 23장, 24장, TASK-DB-MIGRATION-001 |
| 2026-07-10 | TASK-UAT-HANDOVER-002는 raw DOM 검증을 폐기하고 boolean/count/enum output guard를 적용한 뒤 merged main full-ledger runtime을 공식 5190/5092로 통제 전환 | Persistent UAT와 기존 runtime을 보호하면서 UAT-VERIFY 재실행의 최신 main 전제와 개인정보 안전 증빙을 함께 충족하기 위함 | 23장, 24장, TASK-UAT-HANDOVER-002 |
| 2026-07-11 | TASK-UAT-HANDOVER-002의 Current 5190·Candidate 5191 구조 동등성·Compatible 27/28/1·개인정보 안전 검증 정책·SOP/User manual 사용자 검수를 완료하고 PR #28 병합을 승인 | 공식 Review-safe runtime handover의 자동 증빙과 사용자 직접 검수 gate를 모두 닫고 UAT-VERIFY-001을 최신 main에서 처음부터 재실행하기 위함 | 23장, 24장, TASK-UAT-HANDOVER-002 |
| 2026-07-11 | UAT-VERIFY-001을 최신 main에서 처음부터 재실행해 full ledger·schema·aggregate·권한·dashboard·Review-safe·desktop/390px·isolated CI 기준선을 통과하고 사용자 검수 대기로 전환 | 이전 false-ready 원인이 제거된 공식 runtime에서 Persistent UAT 통합 기준선을 데이터 변경 없이 확정 후보로 만들고 다음 TASK-NOTIFY-REL-001 gate를 준비하기 위함 | 23장, 24장, UAT-VERIFY-001 |
| 2026-07-11 | UAT-VERIFY-001 사용자 검수와 UAT 기준선 Go를 승인하고, GitHub metadata 과다 조회 Finding을 검증 절차 P2로 수용해 fixed-field projection과 output guard로 보정한 뒤 PR #29 병합을 승인 | 제품 runtime·Repository·Persistent UAT를 변경하지 않고 개인정보 안전 merge gate를 복구하며 다음 remediation을 TASK-NOTIFY-REL-001로 전환하기 위함 | 23장, 24장, UAT-VERIFY-001 |
| 2026-07-11 | TASK-NOTIFY-REL-001에서 delivery claim/lease·Processing·fencing·attempt audit을 구현하고 전용 tmpfs candidate 검증 후 사용자 검수 대기로 전환 | 정상 다중 worker 중복 provider 호출과 늦은 DB overwrite P2를 제거하되 provider/DB crash 경계의 at-least-once 제한을 명시하고 Persistent UAT 적용을 TASK-UAT-HANDOVER-003으로 분리하기 위함 | 23장, 24장, TASK-NOTIFY-REL-001 |
| 2026-07-11 | TASK-NOTIFY-REL-001 사용자 검수와 PR #30 squash merge를 승인 | claim/lease·fencing·attempt audit, 정상 경쟁 provider call 1회, at-least-once 제한과 exactly-once 미보장, Persistent UAT 0028 미적용, actual provider 호출 0을 확인하고 다음 단계를 TASK-UAT-HANDOVER-003으로 전환하기 위함 | 23장, 24장, TASK-NOTIFY-REL-001 |
| 2026-07-11 | 반복되는 공통 개발 원칙을 Root/영역별 `AGENTS.md`, 종료 정책, 개발 검증 문서와 project-local Codex Rules로 분리 | Task 프롬프트는 목표·범위·완료 기준에 집중하고 판단 규칙과 명령 통제를 각각 단일 source에서 유지하기 위함 | [Root 지침](../AGENTS.md), [종료 정책](12-task-completion-policy.md), [Validation Matrix](development/validation-matrix.md), [Privacy-safe Evidence](development/privacy-safe-evidence.md) |
| 2026-07-11 | TASK-GOV-CODEX-001 사용자 검수와 Draft PR 게시를 승인하고 shell wrapper는 prompt하되 내부 명령 완전 차단으로 과장하지 않음 | 실제 execpolicy 판정과 문서를 일치시키고 project-local Rules를 AGENTS·safe script의 보조 통제로 유지하기 위함 | 24장, 27장, TASK-GOV-CODEX-001 |
| 2026-07-11 | HANDOVER-003 preflight에서 purge worker disable gate 부재 P2를 발견해 Persistent migration 전에 중단하고 TASK-UAT-MAINTENANCE-001로 분리 | worker가 등록된 idle 상태를 maintenance-safe로 오판하지 않고 세 mutation worker 미등록과 candidate 불변을 먼저 보장하기 위함 | 23장, 24장, TASK-UAT-HANDOVER-003, TASK-UAT-MAINTENANCE-001 |
| 2026-07-11 | TASK-UAT-MAINTENANCE-001 사용자 검수와 PR #31 squash merge를 승인 | purge 기본 true, explicit disable·ReviewSafe·Phase A worker 미등록, synthetic 후보 불변, Persistent UAT·0028·runtime·backup 보존을 확인하고 HANDOVER-003 재개 조건을 충족하기 위함 | 23장, 24장, TASK-UAT-MAINTENANCE-001 |
| 2026-07-12 | TASK-UAT-HANDOVER-003에서 fresh backup·isolated rehearsal 후 Persistent UAT 0028과 latest main Review-safe/Development runtime을 통제 적용해 사용자 검수 대기로 전환 | Ledger 28/29/1, worker/provider gate, 사용자 승인 ManualTest 1건의 정상 Sent lineage와 unrelated provider call 0, Persistent aggregate 보존을 확인하고 다음 escalation starvation remediation을 준비하기 위함 | 23장, 24장, TASK-UAT-HANDOVER-003 |
| 2026-07-12 | TASK-UAT-HANDOVER-003 사용자 검수와 PR #33 squash merge를 승인 | Development·Review-safe 정상, ledger 28/29/1, `AUTHORIZED_USER_ACTIVITY` 단일 Sent lineage, Pending/Processing 0/0, backup restore 0과 at-least-once 제한을 확인하고 다음 P2를 TASK-NOTIFY-ESC-001로 유지하기 위함 | 23장, 24장, TASK-UAT-HANDOVER-003 |
| 2026-07-12 | TASK-NOTIFY-ESC-001에서 기존 evaluation timestamp 기반 fair ordering과 후보별 오류 격리를 구현해 사용자 검수 대기로 전환 | 100건 고정 window가 tail을 starvation시키고 후보 오류가 poll을 종료하던 P2를 schema/API/UI 변경 없이 제거하며 L0~L3·recipient·중복 방지·at-least-once 계약을 유지하기 위함 | 23장, 24장, TASK-NOTIFY-ESC-001 |
| 2026-07-13 | TASK-NOTIFY-ESC-001 사용자 검수와 PR #34 squash merge를 승인 | 101/200/201 유한 poll, 후보 오류 뒤 tail 진행, 동시 evaluator 중복 0, L0~L3·BusinessDay·recipient 정책 불변, Persistent UAT 미적용과 at-least-once 제한을 확인하기 위함 | 23장, 24장, TASK-NOTIFY-ESC-001 |

## 26. 용어 사전

| 용어 | 의미 | 사용자 표시/주의 |
| --- | --- | --- |
| 프로젝트 | 고객 주문 또는 생산 단위의 최상위 관리 객체 | 영업이 생성 |
| 패널 | 실제 진행, 검사, 포장, 납품 추적 단위 | 패널명 입력 시 QR 가능 |
| Item | Item 기준값 | UL67, UL891, UL508A, IEC, LLP, RPP |
| QR | 패널 추적용 식별 수단 | 시스템 생성 기준과 현장 부착 기준 구분 |
| Product Tag | 외함 첫 입고 시 부착하는 현장 태그 | IQC 적합 후 QR 부착 |
| 내 업무 | 내가 처리해야 하는 업무 | 시작 전/진행 중/완료/취소 |
| 알림 | 처리할 필요는 없지만 알아야 하는 정보 | 읽음/읽지 않음 |
| 긴급/차단 알림 | 업무 진행이 막히는 상황 알림 | Pending List 연결 |
| Pending List | 부적합, PUNCH, 제조 중단, 기타 이슈 공통 관리 | 조치 담당 부서 사용 |
| 품질 부적합 | 검사 결과 기준 미달 | 재검사 흐름 필요 |
| PUNCH LIST | 고객사 또는 검수 지적사항 | 전진검수/FAT에서 발생 가능 |
| 제조 중단 | 제조 진행 중 작업 불가 상태 | 긴급/차단 알림 |
| 조치 담당 부서 | 이슈 조치를 맡는 부서 | 귀책부서 표현 금지 |
| IQC | 수입검사 | 구매품/외함 중심 |
| LQC | 제조 중 또는 라인 품질 검사 | 상세 양식 회신 대기 |
| OQC | 자체검수 | 상세 양식 회신 대기 |
| 전진검수 | 고객/출하 전 검수 | 필수 단계 |
| FAT | 고객 입회 검사 | 선택 단계 |
| 자재 도착 | 구매품목 도착 등록 | 자재 담당 |
| 입고 확정 | IQC 적합 후 사용 가능 자재 확정 | 자재 담당 |
| 키팅 완료 | 제조 투입 준비 완료 | 제조 내 업무 생성 |
| 납품 완료 | 고객 납품 완료 | 출하완료 대신 사용 |
| 영업 정산 | 납품 후 세금계산서 및 완료 처리 | 최종 단계 |
| 세금계산서·완료 | 영업 정산 완료와 프로젝트 완료 | 18단계 마지막 |

## 27. Repository 작업 지침과 제품 불변조건

개발 작업 방식은 [Root AGENTS.md](../AGENTS.md)와 경로별 하위 지침을 따른다. 종료·Finding·사용자 검수는 [Task 종료 및 산출물 정책](12-task-completion-policy.md), 변경 유형별 테스트는 [Validation Matrix](development/validation-matrix.md), 비식별 증빙은 [Privacy-safe Evidence](development/privacy-safe-evidence.md)가 canonical source다. 이 Roadmap은 해당 절차를 중복하지 않고 제품 방향, Task 상태와 결정 이력을 관리한다.

제품 변경 시 다음 불변조건을 확인한다.

- 공식 사용자 표시명은 EMI 프로젝트 통합관리시스템이며 내부 `Emi.Qms` solution/namespace는 유지한다.
- 18단계 업무 순서, QR 기준, 패널 단독 용어와 필수 workflow 기반 진행률을 임의 변경하지 않는다.
- Backend stack을 전환하지 않고 권한과 업무 규칙은 서버에서 강제한다.
- 검수 사용자 전환은 Development/Testing/UAT의 System Administrator와 dev persona 범위이며 실제 Entra impersonation으로 확장하지 않는다.
- MSAL cache, MFA, 조건부 액세스와 sign-in frequency를 우회하거나 token을 앱 코드에서 직접 storage에 저장하지 않는다.
- Teams Activity, Mail/TeamsChannel 양식과 event coverage는 6장의 확정 상태를 따르며 correlation id를 사용자 메시지에 노출하지 않는다.
- 영업일 계산은 `BusinessDayCalculator`, 에스컬레이션은 `work_items.due_date` 정책을 사용하고 미확정 동기화 정책을 임의 구현하지 않는다.
- 관리자 삭제는 유예·복구·참조 무결성을 보존하고 업무 부서 기준정보를 사용자 결정 없이 관리자 페이지로 통합하지 않는다.
- 사용자-facing 문구는 한글로 작성하고 확정사항·미확정사항·후속 Task를 구분한다.

Roadmap 변경 후에는 문서 link, 공식 명칭, 18단계 순서, RPP 기준값, 패널 용어, 진행률 공식, 추적 대상과 Decision Log가 유지되는지 검증한다.
