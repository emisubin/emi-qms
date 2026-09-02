# EMI PMS Product Roadmap

## 1. 문서 목적

이 문서는 EMI PMS의 전체 개발 방향, 업무 프로세스, 확정사항, 미확정 추적 대상, 후속 TASK 우선순위를 한 곳에 정리하는 기준 문서다.

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

본 시스템의 공식 사용자 표시명은 `EMI PMS`다. 한국어 전체 이름 `EMI 프로젝트 통합관리시스템`과 영문 의미 `EMI Project Management System`은 설명 문구에서 사용한다. 내부 코드명(Emi.Qms 솔루션/네임스페이스 등)은 별개이며 유지한다. 코드 네임스페이스나 솔루션명의 리네이밍은 수행하지 않는다.

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
| 키팅 완료 알림 | 패널 | 선택형 자재 준비 참고 정보. 제조 투입 조건이 아님 |
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

18단계 업무 프로세스 catalog는 현재 시스템의 표준 흐름이다. 화면 구현 순서와 workflow 표시 순서는 이 기준과 일치해야 한다. 단, 2026-08-05 현장 운영 결정에 따라 LQC 적용 여부는 Item별로 관리하고 프로젝트 생성 시점에 고정한다. LQC 운영 중지로 생성된 프로젝트에서는 활성 workflow·진행률·필수 담당자에서 LQC를 제외하고 제조 완료 뒤 OQC로 직접 인계한다.

특히 2번은 생산관리, 3번은 설계다.

| 번호 | 담당 부서 | 단계명 | 입력 단위 | 주요 입력 | 완료 기준 | 다음 내 업무 | 참조 알림 | 비고 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 영업 | 프로젝트 생성 | 프로젝트 | 고객사, Item, PJT Code, PJT Title, 면수, 납기일, 영업담당자, 포장방식, FAT 필요 여부 | 필수 프로젝트 정보 생성 | 생산관리: 생산계획·담당자 입력 | 관련 부서 프로젝트 생성 참조 | FAT 필요 여부는 프로젝트별 선택값 |
| 2 | 생산관리 | 생산계획·담당자 | 프로젝트 | 생산계획 단계, 예정일, 영업/설계/생산관리/구매/자재/제조/물류 정·부 담당자, 품질 단계별 담당자 | 필수 생산계획 예정일 및 필수 담당자 기준 충족 | 설계: 패널명·사이즈 입력, 구매: 구매정보 입력 가능 | 영업, 구매, 제조 참조 | 생산계획 skeleton은 새 프로젝트 생성 시 자동 생성 |
| 3 | 설계 | 패널명·사이즈 | 패널 | 패널명, 사이즈 | 활성 패널의 필수 패널 정보 입력 | 구매: 구매정보 입력 | 생산관리 참조 | 목포장 프로젝트는 사이즈 필수 |
| 4 | 구매 | 구매정보 | 구매품목 | 발주품목, 업체/기술 담당자, 발주일, 입고예정일, 이슈 | Item별 필수 구매 항목의 실제 입력 완료 | 자재: 자재 도착 등록 | 생산관리, 제조 참조 | 자동 생성 row만으로 완료 처리하지 않음 |
| 5 | 자재 | 자재 도착 | 구매품목 | 도착 여부, 도착일, 수량, 비고 | 구매품목 도착 등록 | 품질: 수입검사 입력 | 구매, 생산관리 참조 | 자재 도착 후 IQC 요청 |
| 6 | 품질 | 수입검사 | 구매품목의 개별 도착분 | IQC 체크, 값 입력, 외함 사진, 적합/부적합 | 도착분 IQC 적합 또는 Pending 등록 | 자재: 입고 확정 | 구매, 생산관리 참조 | 패널 검사가 아니며 부적합 시 Pending List |
| 7 | 자재 | 입고 확정 | 구매품목 | 입고 확정, 사용 가능 처리 | IQC 적합품 입고 확정 | 자재: 키팅 완료 | 생산관리 참조 | 구매품목 단위 |
| 8 | 자재 | 키팅 완료 알림 | 패널 | 키팅 완료 여부, 부분/일괄 처리 | 선택형 준비 정보 등록 | 없음 | 생산관리, 제조 참조 | 선택 단계. 제조 투입 요청·시작을 차단하지 않음 |
| 9 | 제조 | 제조 작업 | 패널 및 제조 단계 | 작업 시작, 작업 체크, 작업 종료, 중단 사유 | 제조 단계 작업 완료 | 품질: LQC 또는 자체검수 입력 | 생산관리 참조 | 프로젝트 생성 시 LQC 운영 중지로 고정된 경우 OQC 직접 인계. 제조 중단은 Pending List |
| 10 | 품질 | LQC | 패널 또는 검사 단위 | Item별 LQC 체크리스트, 값, 사진, 적합/부적합 | LQC 적용 프로젝트는 적합 또는 Pending 등록, 미적용 프로젝트는 제외 | 제조: 제조 완료 입력 | 제조, 생산관리 참조 | Item별 운영 상태·양식을 프로젝트 생성 시 snapshot. 기존 프로젝트·과거 이력 보존 |
| 11 | 제조 | 제조 완료 | 패널 | 제조 완료 체크, 완료일 | 제조 완료 처리 | 품질: 자체검수 입력 | 생산관리 참조 | 프로젝트의 LQC snapshot에 따라 제조 단독 또는 제조+LQC 근거로 OQC 연결 |
| 12 | 품질 | 자체검수 | 패널 | OQC 단계별 적합/부적합, 값, 사진 | OQC 체크항목 완료 및 통합 판정 또는 Pending 등록 | 품질: 전진검수 입력 | 제조, 생산관리 참조 | 패널별 단계형 검사. 상세 양식 content는 회신 대기 |
| 13 | 품질 | 전진검수 | 패널 | 단계 없는 통합 적합/부적합, 판정 근거, PUNCH LIST | 패널별 통합 판정 완료 또는 Pending 등록 | 품질: FAT 또는 물류: 포장 완료 | 영업, 생산관리 참조 | 패널당 판정 1회 |
| 14 | 품질 | FAT 선택 | 패널 | 단계 없는 통합 적합/부적합, 고객 확인, PUNCH LIST | FAT 필요 시 패널별 통합 판정 완료 또는 Pending 등록 | 물류: 포장 완료 | 영업, 생산관리 참조 | 선택 단계, 패널당 판정 1회. FAT 불필요 프로젝트는 제외 |
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
- LQC 정/부(프로젝트 생성 시 LQC 운영 중지로 고정된 프로젝트에서는 필수 지정에서 제외)
- OQC 자체검수 정/부
- 전진검수/FAT 정/부

품질 담당자는 각 검사 단계별로 정담당자와 부담당자를 지정할 수 있다. 같은 사용자가 여러 품질 단계에 중복 지정될 수 있고, 실무 정책상 필요하면 같은 사용자가 정담당자와 부담당자로 중복 지정되는 것도 허용할 수 있다. 품질 부담당자는 정담당자가 부재하거나 비활성일 때 fallback 대상으로 사용된다.

DB 원칙은 다음과 같다.

- 부서별 담당자 테이블을 여러 개 만들지 않는다.
- `project_assignees` 또는 동등한 단일 테이블에서 `responsibility_type`으로 구분한다.
- 품질도 같은 테이블에서 `responsibility_type`으로 구분한다.
- 담당자 변경 이력은 field-level audit 또는 project workflow event로 추적한다.

프로젝트 담당자 입력 권한은 다음과 같이 나눈다.

- 생산관리 권한 사용자는 기존 생산계획 수정 화면에서 생산계획·실적 연결과 모든 부서 담당자를 함께 관리한다.
- 생산관리 이외 활성 부서장은 같은 `생산계획 수정` 진입점에서 자기 부서 담당자만 지정한다. 영업·설계·구매·자재·제조·물류는 정·부 2명, 품질은 IQC·LQC·OQC·전진검수/FAT 정·부 8명이다.
- 부서장 전용 화면에는 생산계획 항목, 날짜, 실적 연결과 다른 부서 담당자를 표시하지 않는다. 프로젝트 상세 조회 화면은 기존처럼 전체 계획과 전체 담당자를 표시한다.
- 일반 사용자는 부서장 전용 진입점과 저장 API를 사용할 수 없다. 서버가 활성 상태, 부서장 여부, 소속 부서, 책임 구분과 후보 사용자 소속을 저장 시점마다 다시 검증한다.
- 프로젝트 생성 시 생산관리 이외 지원 부서의 활성 부서장에게 담당자 지정 요청을 추가한다. 이 요청은 인앱 원본을 기준으로 Teams Activity와 PWA push가 따르며, 기존 프로젝트 생성 전체 공지 메일은 중복 발송하지 않는다.

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

담당자 해석과 fallback 규칙은 다음 확정 순서를 따른다.

1. 해당 단계 Primary 또는 품질 단계 정담당자
2. 해당 단계 Secondary 또는 품질 단계 부담당자
3. 정·부담당자가 모두 없으면 해당 업무 부서의 활성 부서장 전원
4. 활성 부서장도 없으면 영업 담당자나 System Administrator로 넘기지 않고, 필요한 부서를 표시한 validation으로 업무 생성을 차단

품질 단계 fallback 예시는 다음과 같다.

- IQC 단계: QualityIQC → QualityIQCSecondary → 품질부 활성 부서장 전원 → 부서장 미등록 validation
- LQC 단계: QualityLQC → QualityLQCSecondary → 품질부 활성 부서장 전원 → 부서장 미등록 validation

부서장 fallback으로 생성된 업무는 `부서장 공유`임을 표시한다. 같은 부서의 부서장 한 명이 처리하면 같은 fallback 묶음의 나머지 업무도 자동 종료하고, 진행률은 묶음 한 건으로 계산한다.

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

인앱 알림과 내 업무 기능은 이미 구현되어 있다. 사용자에게 표시되는 알림은 인앱 수신자·가시성을 원본으로 하며 Teams Activity와 PWA push가 이를 따른다. 18단계 영업 최종 완료처럼 메일 전용으로 확정된 사건은 사용자에게 보이지 않는 내부 원장만 사용한다.

#### 6.5.1 채널별 역할 정의

| 채널 | 역할 | 정의 |
| --- | --- | --- |
| 인앱 | 기록 (Record) | 사용자에게 표시되는 알림의 원본과 수신자 snapshot |
| Teams Activity | 개입 (Interrupt) | 확정된 자동 업무·Pending·프로젝트 lifecycle을 개인별 발송 |
| 메일 | 요약/증빙 (Digest & Evidence) | 평일 요약, 필수 Pending·프로젝트 생성·L1·영업 최종 완료 발송 |
| PWA push | 모바일 개입 | 실제 인앱 가시성과 수신자를 그대로 따라 활성 기기별 파생 |

- Teams Activity와 PWA push는 실시간 채널이며 메일은 평일 요약과 명시된 필수 사건에 사용한다.
- 새 자동 Teams 공용 채널 delivery는 만들지 않는다. 기존 TeamsChannel handler·이력·관리자 조회는 보존한다.

#### 6.5.2 알림 유형 × 채널 매트릭스

이 표의 Teams 열은 event coverage 상태다. 아래의 provider/capability 완료 여부와 개별 event의 자동 연결 여부를 혼동하지 않는다.

| 알림 유형 | 인앱 | Teams | 메일 |
| --- | --- | --- | --- |
| 내 업무 생성 (일반 단계 핸드오프) | 즉시 | Activity Feed 즉시, 사용자 설정 가능 | 일일 요약에 포함 |
| 참조 알림 | 즉시 | 발송 안 함 | 일일 요약에 포함 |
| 일반·긴급 Pending, 재검사·재조치 | 즉시 | Activity Feed 즉시, 필수 | 즉시, 필수 |
| Pending 종결 | 등록 알림 수신자 snapshot에 즉시 | Activity Feed 즉시 | 발송 안 함 |
| 프로젝트 생성 | 모든 활성 사용자에게 즉시 | Activity Feed 즉시 | 모든 활성 사용자에게 즉시 |
| 프로젝트 납기·상태 변경 | 영업담당자+지정 담당자에게 즉시 | Activity Feed 즉시 | 발송 안 함 |
| 17단계 납품 완료 | 영업담당자+지정 담당자에게 즉시 | Activity Feed 즉시 | 발송 안 함 |
| 18단계 영업 최종 완료 | 사용자 표시 없음 | 발송 안 함 | 활성 영업부서 전체에게 즉시 |
| 예정일 임박 (L0, D-1 영업일) | 즉시 | Activity Feed, 사용자 설정 가능 | 발송 안 함 |
| 예정일 초과 (L1, +1일 첫 평가) | 즉시 | Activity Feed 즉시 | 즉시 |
| 일일 요약 | — | 발송 안 함 | 평일 07:30, 사용자 설정 가능 |

- 제조 중단은 별도 참고 알림을 추가하지 않고 긴급 Pending 한 건으로 통합한다.
- 일반·긴급 Pending과 프로젝트 생성 메일은 필수이며 사용자 preference로 끌 수 없다.

#### 6.5.2.1 Activity Feed provider/capability 상태

| capability | 상태 | 근거/주의 |
| --- | --- | --- |
| Graph Activity Feed provider 및 channel handler | 완료 | TASK-NOTIFY-003에서 actual 발송을 검수했다 |
| text topic + Teams deep link | 완료 | 사용자별 installedAppId 운영 의존을 제거했다 |
| recipient/access scope와 notification 연결 | 완료 | 개인 알림은 RecipientOnly, 채널 공지는 Authenticated 정책을 사용한다 |
| `/teams/activity` 및 상세 route | 완료 | Teams tab과 인앱 notification/detail을 연결한다 |
| 관리자 수동 개인/업무 배정 Activity Feed | 완료 | 선택한 EntraId 사용자별 Pending delivery를 생성한다 |
| 확정 자동 event 적용 | 완료 | TASK-NOTIFY-POLICY-001의 수신자·채널 matrix를 사용한다 |

#### 6.5.2.2 Activity Feed event coverage 상태

| event | 상태 | 현재 기준 |
| --- | --- | --- |
| 관리자 수동 개인 알림 | 적용 | TeamsActivity 채널을 선택한 경우 |
| 관리자 수동 업무 배정 | 적용 | work_item, notification, recipient, TeamsActivity delivery를 연결한다 |
| L0/L1 예정일 에스컬레이션 | 적용 | repository 기본 Teams 개인 전략은 TeamsActivity이며 L2/L3 신규 delivery는 만들지 않는다 |
| 일반·긴급 Pending·재검사·재조치·종결 | 적용 | 확정된 수신자 snapshot에 TeamsActivity delivery를 만든다 |
| 자동 단계 핸드오프 업무 생성 | 적용 | 일반 업무 Teams preference를 유지한다 |
| 프로젝트 생성·납기·상태 변경 | 적용 | 확정된 전체 활성 사용자 또는 프로젝트 담당자 범위를 사용한다 |
| 17단계 납품 완료 | 적용 | 영업담당자와 프로젝트 지정 담당자에게 발송한다 |
| 18단계 영업 최종 완료 | 제외 | 메일 전용 정책이므로 Activity Feed를 만들지 않는다 |

새 자동 event를 추가할 때는 notification 원본, recipient, delivery 생성 경로와 테스트를 함께 추가한다.

#### 6.5.3 일일 요약 메일

- 발송 시각: 대한민국 영업일 기준 평일 07:30 (`Asia/Seoul`). 활성 대한민국 공휴일에는 발송하지 않는다.
- 수신자별 개인화 1통
- 구성:
  1. 내 미완료 업무 (예정일 순 정렬, 예정일 초과 건은 상단 강조)
  2. 어제 새로 생성된 내 업무
  3. 내가 조치 담당인 오픈 Pending
  4. 참조 알림 요약 (제목 목록)
  5. 각 항목에 시스템 딥링크 포함
- 보낼 내용이 하나도 없으면 발송하지 않는다. 빈 메일은 금지한다.

#### 6.5.4 에스컬레이션 규칙

정확한 일정 원본이 연결된 미완료 업무에 적용한다.

| 단계 | 조건 | 발송 대상과 채널 |
| --- | --- | --- |
| L0 | 예정일 직전 영업일 | 현재 담당자 Teams Activity. 사용자 설정 가능 |
| L1 | 예정일 다음 날 첫 평가 | 현재 담당자 Teams Activity + 메일 |

- 긴급/차단은 에스컬레이션 단계가 아니라 Pending 필수 채널로 즉시 발송한다.
- L2/L3 코드·schema·과거 이력은 호환을 위해 보존하지만 신규 평가·delivery·설정 catalog는 만들지 않는다.
- 생산계획은 정확한 계획 항목 종료일, 구매는 구매품 입고예정일, 프로젝트 집계 업무는 미완료 구매품의 가장 이른 입고예정일을 사용한다. 정확히 연결되지 않으면 `due_date=null`이다.

#### 6.5.5 소음 방지 규칙

- 중복 억제: 동일 대상(같은 업무/Pending)에 대한 동일 유형 알림은 24시간 내 재발송하지 않는다. 에스컬레이션 단계 상승은 예외다.
- 일괄 처리 묶음: 실제 업무 행은 패널별로 유지하되 같은 operation·프로젝트·단계·수신자 조합의 인앱 원본과 외부 delivery는 1건으로 묶는다.
- 야간 억제는 적용하지 않는다. Teams 개인별 알림은 발생 시각과 무관하게 즉시 발송하는 방향을 기준으로 한다.

#### 6.5.6 구현 방향

- Teams 통합 채널 Webhook handler와 과거 이력은 보존하지만 새 자동 TeamsChannel delivery는 만들지 않는다.
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
- Phase 3: 단순 에스컬레이션 자동화 (L0·L1 운영, L2·L3 호환 이력 보존, TASK-NOTIFY-POLICY-001)

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
- 구매품목 단계(4~8)와 패널 단계(9~17)의 연결: 패널이 9단계(제조 작업)에 진입 가능한 조건은 생산관리 담당자의 명시적인 제조 투입 요청이다. 키팅 완료 여부와 자재 입고 현황은 투입 판단을 돕는 참고 정보이며 제조 시작을 차단하지 않는다.
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

생산관리 이외 부서장은 생산계획 자체를 수정하지 않는다. 프로젝트 상세의 같은 수정 진입점에서 자기 부서 담당자만 지정하며, 생산계획과 다른 부서 담당자는 조회 화면에서만 확인한다.

생산계획 기준은 다음과 같다.

- 프로젝트 단위로 관리한다.
- Item 기준으로 생산계획 단계가 자동 생성된다.
- Item별 생산계획 단계 설정이 가능하다.
- 기존 `Legacy` 생산계획 설정은 최신 설정 1개와 단일 예정일 UI를 그대로 유지한다.
- 신규 `LinkedV1` 양식은 Item별 제조 항목·생산계획 항목·실적 연결을 단일 현재 양식으로 관리한다.
- 설정 변경 이후 새 프로젝트에만 자동 반영한다.
- 기존 프로젝트에는 자동 반영하지 않는다.
- 유효한 `LinkedV1` 생산계획 양식과 제조 양식이 모두 사용 중인 Item의 새 프로젝트만 두 양식과 연결 관계를 한 세트로 snapshot한다.
- 생성 시점 model과 관계없이 프로젝트별 생산계획에서는 단계명·필수 여부·계획 시작/종료·담당자·필요 인원·생산관리 코멘트와 항목별 1:1 실적 연결을 수정할 수 있다.
- 프로젝트별 수정은 해당 프로젝트 snapshot에만 적용된다.
- master template에는 영향이 없다.
- 제조 snapshot과 자동 실적 값은 프로젝트 생산계획 화면에서 수정하지 않는다.
- 실적 시작·종료·진행률은 구매·자재·제조·품질·물류의 확정 원본 데이터를 조회 시점에 결정적으로 계산한다.
- 제조·LQC 연결은 프로젝트에 snapshot된 제조 단계의 불변 `definition_key`를 사용하며 이름·순서 변경으로 재연결하지 않는다.
- IQC 실적 연결은 검사 항목별 identity를 사용하고, OQC 실적 연결은 내부 검사 단계 수와 무관하게 패널별 최종 `OQC 합격` 사건 한 건을 사용한다.
- 생산계획 항목은 순서와 계획 시작일을 기준으로 표시한다.
- 프로젝트 상세 생산관리 section에 연결 실적·담당자·필요 인원·코멘트를 포함한 9열 생산계획표, 근거 펼침과 계획/실적 가로 막대 일정표를 표시한다.
- 기존 날짜별 체크형 생산계획 캘린더는 제거하고 계획·실적 2중 막대 일정표만 사용한다.

생산계획 완료 기준은 다음과 같다.

- `Legacy`의 기존 단일 예정일은 같은 날의 계획 시작·종료로 정규화하고, 이후 프로젝트 전용 기간으로 저장한다.
- 모든 프로젝트는 필수 항목의 계획 시작일과 종료일이 모두 입력되어야 계획 완료다.
- 일부만 입력되면 진행 중이다.
- 날짜가 전혀 없으면 미등록 또는 대기 상태로 본다.
- 담당자 지정도 workflow 완료 판정에 포함될 수 있으나, 구체 필수 담당자 기준은 TASK별로 명시한다.

## 11. 구매 기준

구매정보 입력 단위는 구매품목이다.

구매 화면 기준은 다음과 같다.

- 구매 페이지는 프로젝트 단위로 묶어 표시한다.
- 프로젝트 구매 조회·수정은 `도급 구매품`과 `사급 자재`를 탭으로 분리하고 각 유형의 건수를 표시한다.
- 구매정보에는 업체 헤더가 필요하다.
- 구매 필수 항목은 Item별로 설정할 수 있다.
- 구매 필수 항목 설정은 최신 설정 1개만 유지한다.
- V1, V2처럼 version을 사용자 화면에 누적 표시하지 않는다.
- 설정 변경 이후 새 프로젝트에만 자동 반영한다.
- 기존 프로젝트에는 자동 반영하지 않는다.
- 새 프로젝트 생성 시 Item에 맞는 구매 필수 항목 skeleton row를 자동 생성한다.
- 자동 생성 row만으로 구매 단계 완료 처리하지 않는다.
- 구매 담당자가 실제 정보를 입력하거나 확인해야 완료 판정에 반영한다.
- 도급 구매품의 발주 수량·단위는 함께 입력하거나 함께 비울 수 있다. 사급 자재의 제공 예정 수량·단위는 반드시 함께 입력한다.
- 발주·제공 예정 수량이 있으면 누적 도착 수량보다 작게 줄일 수 없고, 도착 이력이 생긴 뒤에는 단위를 바꿀 수 없다.
- 구매와 자재 입고는 별도 복사본을 만들지 않고 같은 구매 품목 identity를 사용한다.

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
2. 도착분별 IQC 자동 인계
3. 입고 확정
4. 키팅 완료 알림(선택)

자재 도착 기준:

- 구매품목 단위로 입력한다.
- 담당 부서는 자재다.
- 물류 또는 구매는 참조 알림 대상이 될 수 있다.
- 도착 등록과 같은 transaction에서 해당 도착분의 IQC 검사 회차·품질 내 업무·정/부 담당자 알림을 각각 한 건씩 생성한다.
- 사용자가 별도 IQC 요청 버튼을 누르지 않는다. 한 품목이 여러 번 도착하면 도착분마다 독립된 IQC 회차가 생성된다.
- 프로젝트 자재 탭은 구매 품목별 한 행을 기본으로 하고, 행을 열면 도착·IQC·Pending 이력을 시간순으로 표시한다.

입고 확정 기준:

- IQC 적합 후 자재가 입력한다.
- 입고 확정은 사용 가능한 자재가 되었음을 의미한다.
- 부적합품은 Pending List로 연결한다.

키팅 완료 알림 기준:

- 패널 단위로 관리한다.
- 일괄 키팅과 부분 키팅을 모두 고려한다.
- 키팅 완료는 제조팀·생산관리팀이 참고하는 준비 정보이며 제조 내 업무를 생성하지 않는다.
- 생산관리 담당자가 패널을 선택해 `제조 투입 요청`을 해야 제조 정·부 담당자에게 내 업무와 인앱 알림이 생성된다.
- 제조 투입 요청 화면에는 키팅 완료 여부와 자재 입고 수량을 함께 표시하되, 미완료·미입고가 요청과 제조 시작을 막지 않는다.
- 동일 투입 요청 재시도는 같은 결과를 반환하고 제조 업무·알림을 중복 생성하지 않는다.
- 1차 시스템에서는 별도 생산 불출 단계를 두지 않는다.
- 생산계획 예정일만으로 자동 제조 투입 알림을 발송하지 않는다.

## 13. 품질 검사 기준

검사성적서는 디지털화하는 것으로 확정됐다.

공통 원칙:

- 검사성적서를 웹사이트에 적용한다.
- 프로젝트 품질 탭은 `수입검사(IQC)`와 `후속검사`를 구분해 같은 프로젝트의 도착분별 IQC와 LQC·OQC·전진검수·FAT를 함께 조회한다. LQC는 프로젝트 생성 시 snapshot된 운영 상태에 따라 입력 또는 과거 이력 조회만 허용한다.
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
| IQC | 구매품목의 개별 도착분 | 수입검사서 체크, 값 입력, 적합/부적합 | 외함 사진 필수 | 패널과 분리된 도착분 검사 |
| LQC | 패널 또는 검사 단위 | 프로젝트에 고정된 Item별 LQC 성적서 입력 | 필수 위치 회신 대기 | Item별 운영 상태·양식을 프로젝트 생성 시 고정. 미적용 프로젝트는 진행률에서 제외하고 과거 이력만 조회 |
| OQC | 패널 | 단계별 적합/부적합 자체검수 성적서 | 필수 위치 회신 대기 | 단계형 검사, 상세 content 회신 대기 |
| 전진검수 | 패널 | 단계 없는 통합 적합/부적합, 판정 근거, PUNCH LIST | 필요 시 첨부 | 패널당 1단위 필수 단계 |
| FAT | 패널 | 단계 없는 통합 적합/부적합, 고객 확인, PUNCH LIST | 필요 시 첨부 | 패널당 1단위 선택 단계 |

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
- LQC 운영 중지로 생성된 프로젝트에서는 제조 시작 시 LQC 담당자·업무를 요구하지 않고, 제조 완료 transaction이 제조완료확인과 OQC 업무를 직접 생성한다. LQC 합격을 가장하는 record나 event는 만들지 않는다.

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
| 생산관리 | 메뉴, 목록, 프로젝트 펼침, 생산계획 조회/수정, 전체 부서 담당자 지정, 비생산관리 부서장의 자기 부서 담당자 직접 지정, 확장 담당자 구조, Excel 업로드, 모든 프로젝트의 생산계획표·프로젝트 전용 기간/실적 연결·계획 담당자·필요 인원·코멘트·자동 실적·근거·가로 막대 일정 | 운영 양식 content 입력, Change 011·TASK-PROJECT-ASSIGNEE-DELEGATION-001 운영 관찰 |
| 생산계획 | Legacy Item 단계·이력과 신규 프로젝트 snapshot 불변 보존, Item별 단일 현재 제조·계획 양식, 모든 프로젝트 항목별 1:1 실적 연결, 프로젝트 생성 transaction snapshot, 계획 시작/종료·담당자·필요 인원·코멘트·프로젝트 override, 구매·자재·제조·LQC·IQC 항목/OQC 최종 합격·전진검수/FAT·물류 원본 기반 자동 실적 projection, desktop 9열 생산계획표·mobile 카드·계획/실적 Gantt | 대량 프로젝트 성능은 실측 후 cache/query 최적화, 운영 양식 content 입력, Change 011 운영 관찰 |
| 구매 필수 항목 | Item별 필수 구매 항목 설정, 새 프로젝트 skeleton 자동 생성 | 업체/발주정보 입력 기준과 완료 판정 보강 |
| 내 업무 | 목록, KPI, 프로젝트별 그룹, 실제 입력 페이지 이동, 시작/완료 동기화 | 시작/완료 이력 관리자 화면 |
| 알림 | 전체/읽음/읽지 않음, 프로젝트별 그룹, 인앱 원본·수신자 snapshot, Teams Activity Feed actual provider와 자동 업무·Pending·프로젝트 lifecycle coverage, 프로젝트 생성 시 비생산관리 부서장 담당자 지정 요청, Gmail SMTP, PWA 활성 기기별 파생 delivery, 평일 07:30 Daily Digest, L0·L1 단순 에스컬레이션, 생산계획·구매 일정 기반 미완료 업무 due_date 동기화, 담당자 미지정 시 해당 부서 복수 부서장 공유·첫 처리자 동기화 종료, 패널별 업무/묶음 알림, Pending→Processing claim/lease·retry·attempt lineage·관리자 조회 | 실제 PWA VAPID key·외부 push service 검수, 운영 Teams manifest URL 전환, Gmail SMTP 장기 운영 적합성, terminal Failed 수동 재처리 신규 기능(Deferred) |
| workflow | 18단계 stage, 프로젝트 workflow 요약, 기존 페이지 hook, 미구현 stage workflow fallback | 후속 실제 화면 단계 연결 |
| 로그인/권한 | Microsoft 365 로그인 기반, EntraId JIT 사용자 생성, 승인 대기, bootstrap admin, 최소 사용자 관리, Dev user read-only, System Administrator 검수 사용자 전환, 로그인 상태 유지와 Figma 기본/Variant 2, dev auth/E2E 보존, PostgreSQL transaction 기반 마지막 active System Administrator 보호, purge 전용 malformed lifecycle defense-in-depth, latest-main Development controlled runtime 적용, 승인된 Figma 기반 인증 공통 shell, Desktop exact geometry와 PC viewport 등비 canvas, Loading control 제거·빨간 회전 indicator, Change 010 모바일 흑백 wireframe·지정 로그인 logo 운영 반영 | Auth break-glass 계정·복구 rehearsal, Production/Staging dev auth 및 AdminUserSwitch 비활성 검수, 실제 PC·iPhone·Android 인증 후 지정 logo 육안 검수 |
| 공휴일/영업일 | `system_holidays.holiday_type`, BusinessDayCalculator, `/api/calendar/business-days`, 생산계획 캘린더 연동, System Administrator 휴일 관리 API/UI, Excel 양식 다운로드/preview/apply, 회사휴일 Company type, UAT DB 보존 | 공식 공휴일 API service key 연동, 국가공휴일 자동 sync scheduler, 회사 자체 근무일 지정 필요성 검토, 운영 휴일 데이터 검수 |
| 관리자 | 시스템 관리 중심 관리자 홈, 사용자 관리 재사용/확장, 부서 관리, 휴일 관리 재사용, 권한 매트릭스 read-only, 기준정보 변경 이력, 업무 시작/완료 이력, 알림/에스컬레이션 조회, 발송 실패/대기 상세 추적, active escalation L0~L3 breakdown, 삭제 예정 + 7일 후 완전 삭제 시도, 복구, 일괄 삭제/복구, 삭제 보류, 부서 field-level validation. `TASK-AUDIT-001` 전체 감사 이력은 exact catalog·중앙 transaction 대표 검증을 v1 acceptance로 승인하고 모든 409를 Conflict로 보수 분류했으며 독립 재검증 PASS·Open P0/P1/P2 `0/0/0` 뒤 원격 main·Azure 공개 운영에 반영했다. | Item/포장방식/생산계획 단계/구매 필수 항목 관리자 통합 여부, role/permission 편집 UI, 삭제 예정 데이터 purge 운영 정책, 전체 감사 이력 사용자 검수·보존량 관찰 |
| UAT | 고정 Persistent UAT DB, HTTPS-only Development 5174/5081, read-only Review-safe 5190/5092, canonical/live/approved legacy ledger 28/29/1, notification claim/lease·escalation fair-ordering·last-administrator controlled UAT | TASK-UAT-001 Change 001 자동 검증·로그인·Graph actual·Teams client 수신 검수 완료, Persistent live auth mutation은 break-glass 증명 전 No-Go |
| E2E | 전용 backend/frontend 포트, 전용 DB, cleanup | 신규 업무 단계마다 시나리오 추가 |
| Repository workflow | 모든 새 Task의 semantic identity·Roadmap Sequence Gate, `NEW_FEATURE` 전용 Fable 5 deep-interview·사용자 요약 확인·Fable primary draft 전문 1회, Codex 내용·제품 방향 review 1회, 사용자 승인, 분리된 Codex 구현·독립 검증과 Codex-only 보강 작업 router. 명시된 `experiment/*` fast-track은 Fable 1차 기획·Codex review·review 기반 Fable 2차 기획·Codex 구현을 사용 | 같은 목적은 canonical Task를 재사용하고 현재 Next Gate와 다른 Task는 명시적 재정렬 승인 전 시작하지 않음. 일반 branch는 review 뒤 자동 revise 없이 종료한다. 실험 2차 기획은 planning·review·exact approval을 필수 source로 확인하고 별도 target에 atomic no-overwrite로 기록하며 local commit만 허용 |

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
- Teams Activity Feed provider/capability와 관리자 수동 경로는 TASK-NOTIFY-003에서 actual 검수했고, TASK-NOTIFY-POLICY-001이 확정 자동 event coverage와 수신자 matrix를 연결했다. 운영 전 manifest URL과 조직 앱 배포 상태를 검수한다.
- 예정일 기반 에스컬레이션은 정확한 생산계획 항목 종료일·구매품 입고예정일에서 미완료 업무 `due_date`를 동기화하고 L0·L1만 신규 평가한다. 정확한 원본이 없으면 `null`을 유지한다.
- 공휴일/영업일 기반은 구현 완료되었으며, 운영 전 연간 대한민국 공휴일/대체공휴일/임시공휴일/회사휴일 데이터를 관리자 휴일 관리 또는 공식 API sync로 검수한다.
- 공식 대한민국 공휴일 API service key 연동과 자동 sync scheduler는 후속으로 검토한다.
- NOTIFY-002의 BusinessDayCalculator를 재사용하며, L2·L3 신규 평가·delivery는 중단하고 과거 schema·이력만 보존한다.
- ADMIN-001은 시스템 관리 중심으로 구현 완료되었으며, Item/포장방식/생산계획 단계/구매 필수 항목의 관리자 통합 여부는 후속 사용자 결정으로 남긴다.
- 관리자 삭제 예정 데이터의 7일 후 purge 운영 정책과 삭제 보류 처리 모니터링은 운영 검수 후 고도화한다. 전체 field-level audit는 `TASK-AUDIT-001` Change 002에서 exact endpoint/relation catalog·중앙 PostgreSQL 대표 transaction 검증을 v1 acceptance로 승인하고 모든 409를 `Conflict`로 보수 분류했으며, 독립 재검증 PASS·Open P0/P1/P2 `0/0/0`이다.
- role/permission 편집 UI, Pending 유형 관리와 검사/제조 체크리스트 템플릿은 ADMIN-001 범위에서 제외되어 후속으로 검토한다. Terminal Failed 수동 재처리는 TASK-NOTIFY-004 정책 결정에 따라 별도 신규 기능 후보로 Deferred한다.
- Notification claim/lease, automatic retry, attempt lineage, provider 오류 분류와 escalation starvation 보정은 완료됐다. `TASK-NOTIFY-004`는 terminal `Failed`를 현재 상태 모델의 최종 상태로 유지하고 Pending retry·acknowledge·dismiss만 지원하는 `POLICY_CORRECTION_AND_DEFER`를 승인했다. Failed 수동 재처리가 필요하면 retry generation·append-only audit·provider 중복 확인을 포함한 별도 NEW_FEATURE planning을 거친다.
- TASK-AUTH-HARDEN-001의 `PURGE_GUARD_PREDICATE_UNREACHABLE`은 Change 001 REDESIGN으로 해결됐고 TASK-UAT-AUTH-HARDEN-001 Phase A~D, privacy-safe evidence, isolated HTTP, Persistent mutation-free runtime handover와 사용자 검수를 통과했다. Persistent live auth mutation은 break-glass 증명 전 No-Go다.
- Git history 개인정보는 `TASK-GOV-HISTORY-REWRITE-001`의 영향 ref `16/16` rewrite, fresh clone과 GitHub Support internal reference 제거·GC를 거쳐 P2를 해소했다. PR #50 merge 당시 사용자가 Repository를 public으로 재개했으며 encrypted backup 삭제는 별도 승인 대상이다. 2026-08-10 actual readback 기준 현재 visibility는 `PRIVATE`이고 exact 전환 시점은 이 Roadmap에서 확정하지 않는다.
- 기존 import-order 위반 9건은 범위 밖 format debt/P3 후보이며 현재 P2 Gate에 포함하지 않는다.

## 23. 향후 개발 로드맵

실행 순서는 고정 날짜가 아니라 현재 상태, 선행 의존성, 외부 blocker와 사용자 승인 Gate를 따른다. `TASK-USER-FLOW-001`은 tracked interview·planning·review·Change 004에 따라 개인 참고 문서 redraft와 게시만 승인됐고 제품 implementation 승인은 `false`다. 큐의 순서 승인과 USER-FLOW의 개인 참고 문서 승인은 개별 제품 기능 범위·정책·구현 승인을 대신하지 않는다.

현재 `experiment/*` 계보에서는 사용자의 2026-07-18 완료 판정에 따라 [실험 브랜치 Task 완료 원장](27-experiment-task-ledger.md)을 이 큐와 함께 사용한다. 아래의 `Canonical Pending / Experiment Complete`는 대표 repo·`main` 승격은 대기 중이지만 현재 experiment scope는 다시 기획·구현하지 않는다는 뜻이다. 사용자 직접 검수는 `BATCHED_FINAL`로 마지막에 일괄 수행하며 완료로 가장하지 않는다. canonical 승격이 필요하면 기능 재개발이 아니라 별도 통합·UAT Gate를 사용한다.

| Priority | Task | Task Type | Status | Planning Status | Dependencies | External Blocker | UAT Required | Next Gate |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0.1 | TASK-UAT-AUTH-HARDEN-001 | UAT_RUNTIME | Completed | Planning Approved | Phase A~D 자동 검증·runtime 적용·사용자 검수 완료 | break-glass 미증명으로 live mutation 금지 | Yes | PR #40 squash merge 승인 → TASK-GOV-002 |
| 0.2 | TASK-GOV-002 | POLICY_DECISION | Completed | Planning Approved | current checkout 비식별화·public history 조사·사용자 검수 완료 | PR #41 Ready·squash merge 승인 | No | PR #41 merge → TASK-GOV-HISTORY-REWRITE-001 |
| 0.3 | TASK-GOV-HISTORY-REWRITE-001 | SECURITY_HARDENING | Completed / PR #50 Merged / Public at Completion / Current Private | Planning·Implementation Approved | 영향 ref `16/16`, fresh clone, old clone quarantine, Support cleanup, 독립 검증·사용자 검수 완료 | Encrypted backup 삭제는 별도 승인 | No | 0.6 신규 기능 Go/No-Go 사용자 결정 |
| 0.4 | TASK-NOTIFY-004 잔여 범위 | POLICY_DECISION | Completed / PR #44 Merged | Planning Approved | claim/lease·automatic retry·attempt lineage·starvation 완료 | 없음. 수동 재처리는 별도 신규 기능으로 Deferred | No | Finding gate에서 Resolved 확인 |
| 0.4A | TASK-BACKEND-FORMAT-001 — import-order baseline 정리 | HOUSEKEEPING | Completed / Merged | Planning Approved | import-order 9건 정규화·검증·사용자 검수 완료 | 없음 | No | Finding gate에서 Resolved 확인 |
| 0.4B | TASK-DESIGN-LOGIN-001 — Entra 로그인 공통 디자인 shell | BUGFIX | Change 010 Main Merged / Azure Released | Change 010 User Approved | Change 001~009 완료. Change 010 지정 로그인 로고와 iPhone·Android 흑백 wireframe 구현·전체 Frontend·PR #83 merge·Azure 운영 release 완료 | 실제 PC·iPhone·Android 인증 후 육안 검수. Code Connect는 향후 필수 Gate에서 제외 | No | 사용자 실제 기기 검수 |
| 0.5 | TASK-GOV-FINDING-GATE-001 — 전체 P0/P1/P2 재평가 | DOCS_GOVERNANCE | Completed / PR #50 Merged | Planning Approved | Open P0/P1/P2 `0/0/0`, 독립 검증·사용자 검수 완료 | 없음 | No | 0.6 신규 기능 Go/No-Go 사용자 결정 |
| 0.6 | 신규 기능 Go/No-Go | POLICY_DECISION | Completed — User GO | N/A | Open P0/P1/P2 `0/0/0` | 없음 | No | 사용자 GO와 다음 행의 명시적 재정렬 승인 완료 |
| 0.7 | TASK-USER-FLOW-001 — 웹사이트 전체 유저플로우 설계 | NEW_FEATURE | Completed / PR #55 Merged | 개인 참고 문서 redraft·게시 완료 / 제품 구현 미승인 | Governance PR #54, USER-FLOW PR #55, Change 004, Open P0/P1/P2/P3 `0/0/0/0` | 없음 | No | 대표 repo 승격 history는 TASK-007A; experiment에서는 완료 원장에 따라 재구현 금지 |
| 1.1 | TASK-007A Pending List | NEW_FEATURE | Canonical Pending / Experiment Complete | Experiment implementation·automated validation complete / `BATCHED_FINAL` | TASK-USER-FLOW-001 사용자 내용 확인, 내 업무·알림 기반 | binary 첨부 storage 정책은 별도 후속 | Yes | experiment 재구현 금지; canonical 승격 시 별도 통합·UAT Gate |
| 1.2 | TASK-007B 병목 상태 집계 | NEW_FEATURE | Canonical Pending / Experiment Complete | Experiment implementation·automated validation complete / `BATCHED_FINAL` | TASK-007A experiment scope 완료 | 없음. 정렬 toggle은 optional P3 | Yes | experiment 재구현 금지; 최종 일괄 검수 |
| 1.3 | TASK-MOBILE-001 적응형 현장 UX | NEW_FEATURE | Canonical Pending / Experiment Complete | Experiment implementation·automated validation complete / `BATCHED_FINAL` | TASK-007A·007B experiment scope 완료 | 사진 binary 정책은 별도 후속 | Yes | experiment 재구현 금지; 최종 일괄 검수 |
| 1.3A | TASK-MOBILE-002 모바일 우선 전면 개편 | APPROVED_FEATURE_IMPLEMENTATION | Experiment Complete | Change 001~005 automated validation complete / `BATCHED_FINAL` | MOBILE-001·로그인 shell·DESIGN-001·DESIGN-000 계보 | `App.tsx` 분할은 optional housekeeping | Yes | 완료 scope 재구현 금지; 최종 일괄 검수 |
| 1.4 | TASK-HOME-001 Home MVP | NEW_FEATURE | Canonical Pending / Experiment Complete | Experiment implementation·automated validation complete / `BATCHED_FINAL` | TASK-007B·MOBILE-001 experiment scope 완료 | query/cache 최적화는 실측 시 P3 후속 | Yes | experiment 재구현 금지; 최종 일괄 검수 |
| 1.4A | TASK-HOME-002 개인화 Home·프로필 shell | NEW_FEATURE | Experiment Complete | 본체와 Change 002 전 부서 조회 메뉴·compact design implementation·automated/isolated browser validation complete / `BATCHED_FINAL` | TASK-HOME-001·MOBILE-002·DESIGN-001 experiment scope | 대표 repo·main·Persistent UAT 미반영 | Yes | 재구현 금지; 최종 일괄 검수. 후속 UX-001 A2 Full-Stack에서 HOME 회귀까지 재검증 |
| 1.4B | TASK-NOTICE-BOARD-001 Home 공지사항 게시판 | NEW_FEATURE | Experiment Complete | Fable 2-pass·local 구현·전체 자동/격리 browser 검증 완료 / `BATCHED_FINAL` | TASK-HOME-001/002·DESIGN-000 experiment scope | 대표 repo·main·Persistent UAT·실제 provider 제외 | Yes | 완료 scope 재구현 금지; 최종 일괄 사용자 검수 |
| 1.4C | TASK-NOTICE-EDITOR-001 공지 굵게·수정·첨부 | APPROVED_FEATURE_IMPLEMENTATION / UAT_RUNTIME | Change 003 Local Integration Validated / Publication Approved | 제한형 굵게, 작성자 CAS 수정·revision, 작성·편집 첨부와 인증 사용자 다운로드, migration `0073`을 구현하고 latest main Backend `496/496`·Frontend `197/197`·Full-Stack `58/58`과 사용자 검수를 완료 | TASK-NOTICE-BOARD-001·Upload Security 계약·TASK-CI-COST-001 | 실제 malware scanner/provider와 새 알림 채널 제외 | Yes | 통합 PR `CI Gate` → Change 021 Azure release |
| 2.1 | TASK-008A 자재 도착·분할 입고 | NEW_FEATURE | Canonical Pending / Experiment Complete | Experiment implementation·automated validation complete / `BATCHED_FINAL` | TASK-007A experiment scope 완료 | 운영 migration handover 미승인 | Yes | experiment 재구현 금지; canonical 승격은 별도 UAT |
| 2.2 | TASK-008B 사급 자재 추적 | NEW_FEATURE | Canonical Pending / Experiment Complete | Experiment implementation·automated validation complete / `BATCHED_FINAL` | TASK-008A experiment scope 완료 | 운영 정책 실데이터 검수 대기 | Yes | experiment 재구현 금지; 최종 일괄 검수 |
| 2.3 | TASK-009A IQC·사진·PDF | NEW_FEATURE | Canonical Pending / Experiment Complete | 본체와 Change 003 사진 필수 항목의 판정·근거 바로 아래 항목 전용 사진 입력·서버 확정 검증·사용자 검수 완료 / `USER_VALIDATION_COMPLETE` | TASK-007A·008A experiment scope 완료 | 실제 IQC 양식 content는 template 입력 후속 | Yes | 완료 scope 재구현 금지; 현업 content는 별도 change |
| 2.4 | TASK-010A 패널별 키팅·제조 투입 요청 | NEW_FEATURE | Canonical Pending / Experiment Complete | Experiment implementation·후속 Full-Stack `35/35`, Change 003 선택형 키팅·생산관리 제조 투입 요청, Change 004 생산관리 2탭, Change 005 `생산관리 / 제조 요청` 명칭·최초 입고 후 제조 투입 판단/키팅 인계·패널별 제조 요청 집계·사용자 검수 완료 / `USER_VALIDATION_COMPLETE` | TASK-008A·009A experiment scope 완료 | 마지막 panel 취소 stage 표시 정책은 P3 후속 | Yes | 완료 scope 재구현 금지 |
| 3.1 | TASK-011A 제조 체크리스트 | NEW_FEATURE | Canonical Pending / Experiment Complete | Experiment implementation·automated validation complete / `BATCHED_FINAL` | TASK-010A·007A experiment scope 완료 | 실제 제조 상세 항목은 template 후속 | Yes | experiment 재구현 금지; 양식 변경은 기존 Task change |
| 3.1A | TASK-MANUFACTURING-BATCH-001 제조 선택 패널 단계 일괄 완료 | NEW_FEATURE | Experiment Complete | Fable 2-pass 본체·Change 002~003 Codex 정정·모든 제조 단계 선택 일괄 완료·Backend `427/427`·Frontend `140/140`·isolated Full-Stack·desktop/mobile 증빙 완료·checkpoint `e6f3fa6` / `BATCHED_FINAL` | TASK-011A·TASK-EXPORT-001 experiment scope 완료 | Change 003 사용자 최종 일괄 검수, 대표 repo·main·Persistent UAT 미반영 | Yes | 재구현 금지; Change 003 최종 일괄 검수 |
| 3.2 | TASK-012A 후속 품질 | NEW_FEATURE | Canonical Pending / Experiment Complete | 본체와 Change 003~005 Checklist/Aggregate·Pending 재검사·사진 필수 LQC/OQC 항목의 inline 사진·서버 확정 검증·사용자 검수 완료 / `USER_VALIDATION_COMPLETE` | TASK-009A·011A experiment scope 완료 | 실제 LQC/OQC/FAT 양식 content는 template 입력 후속 | Yes | 완료 scope 재구현 금지; 현업 content는 별도 change |
| 3.3 | TASK-ADMIN-002 Template 관리 | NEW_FEATURE | Experiment Complete | 2-pass planning·implementation·automated/isolated browser validation complete / `BATCHED_FINAL` | TASK-009A·011A·012A experiment model | 실제 운영 양식 content 입력은 후속 change | Yes | 재구현 금지; 최종 일괄 검수 |
| 3.3D | TASK-ADMIN-003 사용자 부서·역할·부서장 연결 보정 | POLICY_DECISION / P2_REMEDIATION | Change 002 Main Merged / Azure Released / User Validation Complete | Change 002의 품질·생산관리 부서장 양식 관리 scope와 제조 부서장·일반 품질 사용자 차단을 PR #103으로 병합했다. exact main SHA `58c089993587deea30513cb6edee0b8396a1d474`의 release `31786040822`에서 migration `0078`→Backend→Frontend와 공개 보안 검사를 완료했다. | TASK-ADMIN-001 사용자·부서 관리, TASK-ADMIN-002 양식관리 binding, TASK-PRODUCTION-CONTROL-001, TASK-QUALITY-OPERATING-MODEL-001 | 없음 | Yes | 완료 scope 재구현 금지; 운영 권한 관찰 |
| 3.3I | TASK-ADMIN-001 Change 001 관리자 홈 조치 항목 정리 | BUGFIX | User Validation Complete / Publication Approved | 조치 가치가 낮은 KPI 3개를 홈에서 제거하고 승인 대기 카드가 활성 Entra·역할 없음 사용자만 여는 전용 필터로 이동하도록 구현·검증·사용자 검수 완료 | TASK-ADMIN-001·TASK-ADMIN-003 | 없음 | Yes | 단일 통합 PR `CI Gate` → Azure 공개배포 |
| 3.3A | TASK-PRODUCTION-CONTROL-001 Item별 생산계획·자동 실적·가로 막대 일정 | NEW_FEATURE / BUGFIX | Change 011 Main Merged / Azure Released / User Validation Complete · Change 012·013 Azure Released / Post-Deployment User Validation Pending · Change 014 Main Merged / Azure Released / Automated Post-Deployment Check Complete / User Validation Pending | Change 012는 이름 맞교환·삭제 후 재사용 중 일시적 unique 충돌을 transaction 내부 이름 격리로 보정했다. Change 013은 발주·입고 실적을 구매품 구분별로 연결하고 Item 제조양식 저장 시 기존 프로젝트를 동기화했다. Change 014는 배포 전부터 남은 기존 snapshot 불일치를 migration `0080`으로 보정하고, 생산계획 header가 없어도 프로젝트 실제 Item 기준으로 이후 저장 동기화를 수행한다. PR #106·main CI·Azure release `32118742009`이 통과했고, 공개 운영에서 LLP 현재 제조양식 `7`단계와 기존 프로젝트의 연결 선택지별 제조 단계 `7`개가 일치했다. | TASK-ADMIN-002·생산계획·구매·자재·제조·품질·물류 원본 데이터 | 생산계획은 프로젝트별 override를 유지한다. 제조양식은 Item 전역 현재값이며 기존 완료 execution은 변경하지 않는다. | Yes | 사용자 직접 검수에서 기존 완료 제조 이력 보존 확인 → 운영 관찰 |
| 3.3K | TASK-PROJECT-ASSIGNEE-DELEGATION-001 — 부서장 자기 부서 담당자 직접 지정 | NEW_FEATURE | Main Merged / Azure Released / User Validation Complete | 생산관리 전체 편집은 유지하고 비생산관리 활성 부서장은 자기 부서 담당자만 조회·저장한다. 프로젝트 생성 담당자 지정 요청과 서버 권한·감사·중복 방지를 구현하고 PR #101·exact main SHA·Azure release `31774236257`의 public security smoke를 통과했다. | TASK-PRODUCTION-CONTROL-001 Change 011·TASK-ADMIN-003 부서장·TASK-NOTIFY-POLICY-001 | DB migration 없음. 일반 사용자·다른 부서 mutation은 서버 차단 | Yes | 완료 scope 재구현 금지; 운영 알림·권한 관찰 |
| 3.3L | TASK-AUDIT-001 — 로그인·데이터 변경 감사 원장 | APPROVED_FEATURE_IMPLEMENTATION / POLICY_DECISION / UAT_RUNTIME / BUGFIX | Main Merged / Azure Released / Change 004 Azure Frontend Released / Post-Deployment User Validation Pending | 기존 원장은 PR `#111`과 Azure release `33137792491`로 배포됐다. 공개 검수에서 로그인 기록 미생성을 확인했고 운영 aggregate의 login endpoint 호출 `0`·Backend 저장 실패 `0`과 MSAL v5 계약 대조로 `LOGIN_SUCCESS`의 `AccountInfo` payload를 이전 `AuthenticationResult.account` 형식으로 읽은 Frontend 오류를 P1 root cause로 확정했다. Change 004는 v5 payload 직접 처리, request correlation과 로그인 시작 탭 소유권, API pending과 cross-tab 차단 marker 분리를 보정했다. PR `#113` CI `33356499110` 통과 뒤 exact main SHA `6e2b00de494995cd9901003c76912c481e4424d2`로 병합했고 Azure release `33358365813`에서 Frontend·public security `PASS`, Backend·migration `SKIPPED`를 확인했다. 별도 공개 확인은 health `200`, 익명 root·API `401/401`이다. | 기존 Entra/MSAL identity, `authorization_audit_events`, `data_export_events`, `Audit.Read.All`, migration `0083` | 156-route 1:1 matrix는 v1 제외. Azure 50명 부하·local Persistent UAT·실제 외부 알림 시험 발송 제외. Backend·migration 배포 제외 | Yes | 완전 로그아웃 → 새 Microsoft Redirect 로그인 1회와 앱 복귀 확인 → 운영 aggregate·감사 row `+1` 확인 |
| 3.3M | TASK-SITE-ACCESS-001 — 유지 세션 포함 사이트 접속 이력 | APPROVED_FEATURE_IMPLEMENTATION / P2_REMEDIATION / UAT_RUNTIME | PR #116 Main Merged / Azure Released / Automated Public Check Complete / User Validation Pending | 사용자·브라우저 client·30분 활동 창별 접속 원장, 19개 고정 메뉴, 서버/DB 권위 시각, 명시적 로그아웃 종료, 별도 coverage·시간 해석 안내와 전체 감사 이력·선택 Excel 통합을 구현했다. 공개 G2 migration `0084`를 보존하고 사이트 접속 migration `0085`를 PR #116으로 병합했다. Exact current-main `58daf6d8bfe333cb00e343a3fcc13ee4f3358183`의 Azure release `33577473523`에서 migration·Backend·Frontend·public security가 통과했고 공개 coverage·양수 summary를 확인했다. | TASK-AUDIT-001, 기존 Entra/MSAL identity, `Audit.Read.All`, migration `0083`, 공개 G2 `0084` | 사용자 목록·상세·선택 Excel·직접 로그아웃 화면 검수 대기. Persistent UAT 미적용 | Yes | 사용자 운영 화면 검수 → 보존량 관찰 |
| 3.3B | TASK-UL891-PRODUCTION-PLAN-001 실물 세트별 생산계획 | NEW_FEATURE | Change 004 Main Merged / Azure Released / User Validation Complete | Fable 2-pass 본체와 Change 002~008의 전체 세트 기본계획·실적 연결 편집·일정표 색/날짜선/테두리·담당자 표시를 통합 원격 `main` 기준선에 이식. UL891 단일 현재 설계·활성 42면 projection·migration `0068`과 제조·품질·물류 현재 순번 `1..42`·영구 code `P52` 분리를 자동·실제 화면에서 검증. Change 020 최신 main 운영 release로 통합 image 동기화 완료 | TASK-PRODUCTION-CONTROL-001·TASK-UL891-SET-001·DESIGN-000 Change 006·TASK-EXPERIMENT-PROMOTION-001 Change 002 | 없음. 운영 관찰 유지 | Yes | 완료 scope 재구현 금지; 운영 관찰 |
| 3.3C | TASK-TEAMS-PWA-001 — Teams 실행 화면·PWA 설치·브랜드 통일 | BUGFIX / P2_REMEDIATION | Change 011 Production Rollout Complete | Change 001~003·007·009·010·011 운영 rollout 완료. 새 웹 제품 logo, 로그인 보안 안내와 오산·청주 회사 footer를 반영하고 PWA·Teams icon을 보존했으며 사용자 검수·PR #93·Azure release `31452524156` 완료 | Azure Easy Auth·Teams Activity 10종·기존 웹 MSAL·PWA icon·TASK-DESIGN-LOGIN-001 Change 010·TASK-ADMIN-003 통합 branch | Web Push는 별도 NEW_FEATURE | Yes | 운영 PC·모바일 브랜드·footer 관찰 |
| 3.3H | TASK-PROJECT-PENDING-001 — LSE TASK NO·부서별 Pending·오픈/종결 구분 | APPROVED_FEATURE_IMPLEMENTATION | User Validation Complete / Publication Approved | 선택형 LSE TASK NO 생성·수정·상세, 전체 Pending의 `우리 부서+오픈`, 프로젝트 Pending의 `전체+오픈`, 조치 부서 우선·담당자 부서 fallback과 오픈/종결 표기를 구현. migration `0076`, Backend `518/518`, Frontend `211/211`, mock `2/2`, isolated Full-Stack 보정 회귀 `6/6`과 사용자 검수 완료 | TASK-007A Pending·TASK-ADMIN-003 부서·프로젝트 기본정보 | 운영 migration·공개배포는 우선순위 3과 관리자 화면 변경을 포함한 단일 통합 Gate로 진행 | Yes | 통합 PR `CI Gate` → Azure 공개배포 |
| 3.3J | TASK-PANEL-DESIGN-001 — 설계 도번·필수값·패널 열반 | NEW_FEATURE | Change 001 User Validation Complete / Publication Approved | 일반 Item의 패널별 도번·포장방식별 필수값 안내·2면 이상 출하 열반을 구현했다. 재구성 때마다 현재 열반을 패널 순서 기준 1부터 재번호화하고 설계 탭에는 `W 합계 × H 최댓값 × D 최댓값`을 표시한다. UL891 제외, 기존 흑백 wireframe·2px 일반 검정 열반 테두리·강조선 금지를 보존한다. Backend targeted `43/43`, Frontend `212/212`, targeted mock UI `1/1`, isolated Full-Stack `1/1`과 사용자 재검수 통과 | TASK-003B 패널정보·TASK-UL891-SET-001 분리 설계·DESIGN-000·우선순위 1·2 검수본 | LSE TASK NO migration `0076` 뒤 본 Task migration `0077`로 확정. 운영 반영은 앞선 검수본과 단일 통합 Gate로 진행 | Yes | 우선순위 1·2·관리자 변경과 단일 통합 PR·공개배포 |
| 3.3F | TASK-PWA-PUSH-001 — 인앱 연동 PWA 기기 푸시 | APPROVED_FEATURE_IMPLEMENTATION / UAT_RUNTIME | Change 002 Main Merged / Azure Released / User Validation Complete | 운영 VAPID Key Vault 참조와 `Enabled=true`·`DryRun=false` 보존 정의를 PR #103으로 병합하고 exact main release `31786040822`를 완료했다. 재배포 뒤 두 secret reference와 실제 발송 상태, ready·Running을 확인했다. | TASK-TEAMS-PWA-001 설치 경험·TASK-NOTIFY-004 delivery worker·현재 인앱 알림 수신자 정책 | 직원별 설치·알림 허용은 자율. 운영 secret 원문·사용자별 구독·과거 알림 소급 발송은 제외 | Yes | 완료 scope 재구현 금지; 운영 수신 관찰 |
| 3.3G | TASK-NOTIFY-POLICY-001 — 알림 운영 정책 정합화 | APPROVED_FEATURE_IMPLEMENTATION / UAT_RUNTIME | Production Rollout / User Validation Complete | 자동 업무·Pending·프로젝트 lifecycle 채널·수신자, 복수 부서장 fallback, 묶음 알림, 일정 원본 due_date, L0·L1와 평일 Digest를 migration `0075`와 함께 운영 적용했다. PWA는 실제 인앱 원본에서 파생되며 등록·허용한 사용자의 활성 기기에 같은 새 알림을 보낸다 | TASK-NOTIFY-001~005·TASK-PWA-PUSH-001·TASK-ADMIN-003 | 기존 Teams·메일 운영 설정과 PWA 자율 등록 정책을 보존 | Yes | 운영 관찰 |
| 3.3H | TASK-PROJECT-PENDING-001 — LSE TASK NO·부서별 Pending·오픈/종결 구분 | APPROVED_FEATURE_IMPLEMENTATION | User Validation Complete / Publication Approved | 선택형 LSE TASK NO 생성·수정·상세, 전체 Pending의 `우리 부서+오픈`, 프로젝트 Pending의 `전체+오픈`, 조치 부서 우선·담당자 부서 fallback과 오픈/종결 표기를 구현. migration `0076`, Backend `518/518`, Frontend `211/211`, mock `2/2`, isolated Full-Stack 보정 회귀 `6/6`과 사용자 검수 완료 | TASK-007A Pending·TASK-ADMIN-003 부서·프로젝트 기본정보 | 운영 migration·공개배포는 우선순위 3과 관리자 화면 변경을 포함한 단일 통합 Gate로 진행 | Yes | 통합 PR `CI Gate` → Azure 공개배포 |
| 3.3J | TASK-PANEL-DESIGN-001 — 설계 도번·필수값·패널 열반 | NEW_FEATURE | Change 001 User Validation Complete / Publication Approved | 일반 Item의 패널별 도번·포장방식별 필수값 안내·2면 이상 출하 열반을 구현했다. 재구성 때마다 현재 열반을 패널 순서 기준 1부터 재번호화하고 설계 탭에는 `W 합계 × H 최댓값 × D 최댓값`을 표시한다. UL891 제외, 기존 흑백 wireframe·2px 일반 검정 열반 테두리·강조선 금지를 보존한다. Backend targeted `43/43`, Frontend `212/212`, targeted mock UI `1/1`, isolated Full-Stack `1/1`과 사용자 재검수 통과 | TASK-003B 패널정보·TASK-UL891-SET-001 분리 설계·DESIGN-000·우선순위 1·2 검수본 | LSE TASK NO migration `0076` 뒤 본 Task migration `0077`로 확정. 운영 반영은 앞선 검수본과 단일 통합 Gate로 진행 | Yes | 우선순위 1·2·관리자 변경과 단일 통합 PR·공개배포 |
| 3.3E | TASK-PRIVACY-NOTICE-001 개인정보·이용 안내 | APPROVED_FEATURE_IMPLEMENTATION / POLICY_DECISION | Change 007 Local Integration Validated / Publication Approved | 로그인 후 정적 안내, 공통 footer 진입점, 부드러운 목차 이동, logo 홈 이동, 프로필 사진 선택 안내와 PWA 설치·알림 안내를 구현하고 회사 문안·현재 운영 범위를 승인. 통합에서 mobile/coarse-pointer 44px 터치 영역을 보정해 전체 `58/58` 통과 | TASK-TEAMS-PWA-001·현재 Microsoft Teams/메일 운영 계약 | 새 provider·Web Push·동의 DB·법률 자문 제외. 변경 시 P3 재검토 | Yes | 통합 PR `CI Gate` → Change 021 Azure release |
| 3.4 | TASK-PENDING-TYPE-001 Pending 유형 관리 | NEW_FEATURE | Experiment Complete | Fable 2-pass·local 구현·자동 검증·desktop/mobile 증빙 완료 / `BATCHED_FINAL` | TASK-007A experiment 완료·안정화 | catalog 설정 자체 Excel export는 P3 backlog, 대표 repo·main·Persistent UAT 미반영 | Yes | 재구현 금지; 최종 일괄 검수. 승격은 별도 UAT Task |
| 4.1 | TASK-013A 물류 | NEW_FEATURE | Canonical Pending / Experiment Complete | Experiment implementation·automated validation complete / `BATCHED_FINAL` | TASK-012A experiment scope 완료 | 실제 포장·서명본 양식은 후속 change | Yes | experiment 재구현 금지; 최종 일괄 검수 |
| 4.2 | TASK-014A 정산·완료 | NEW_FEATURE | Canonical Pending / Experiment Complete | Experiment implementation·automated validation complete / `BATCHED_FINAL` | TASK-007B·013A experiment scope 완료 | 운영 정산 정책 실데이터 검수 대기 | Yes | experiment 재구현 금지; 최종 일괄 검수 |
| 4.2A | TASK-SALES-KPI-001 영업 연간 매출 KPI | NEW_FEATURE | Experiment Complete | 본체와 Change 002 benchmark 기반 actual·target·attainment graph, automated/isolated browser validation complete / `BATCHED_FINAL` | TASK-014A·HOME-002·DESIGN-000 experiment scope | forecast·전년 비교는 계약 데이터 없음, 대표 repo·main·Persistent UAT 미반영 | Yes | 재구현 금지; 최종 일괄 검수 |
| 4.3 | TASK-EXPORT-001 Excel export | NEW_FEATURE | Experiment Complete | Change 002 20개 화면 선택 export와 Change 003 server allowlist column picker automated validation complete / `BATCHED_FINAL` | 주요 data model experiment 구현 완료 | preset·재정렬·multi-sheet는 별도 optional 후속 | Yes | 완료 scope 재구현 금지; 최종 일괄 검수 |
| 4.3A | TASK-EXPORT-002 선택 프로젝트 Excel export | NEW_FEATURE | Experiment Complete | Automated validation complete / `BATCHED_FINAL` | TASK-EXPORT-001 Phase 1 | 없음 | Yes | 재구현 금지; 최종 screenshot·파일 일괄 검수 |
| 4.4 | TASK-QR-001 — 패널 QR 발급·인쇄·인증 스캔 랜딩 | NEW_FEATURE | Canonical Pending / Experiment Complete | Experiment planning·implementation·automated validation complete / `BATCHED_FINAL` | MOBILE-001·제조 흐름·TASK-003B `qrEligible` | 대표 repo·main·Persistent UAT 미반영 | Yes | experiment 재구현 금지; 최종 일괄 검수 |
| 4.5 | TASK-WORKFLOW-CONTINUITY-001 — 실제 담당자 부서 간 연속 인계 보정 | P2_REMEDIATION / BUGFIX | Change 018 Implemented / Automated Validation Complete / User Validation Pending | Change 005~017의 연속 인계·실데이터 상태·진행률·업무 이동 계약을 유지한다. Change 018은 프로젝트 전체 흐름의 개인화되지 않은 `내 업무` 건수를 제거하고 `Requested` 표시를 `업무 요청됨`으로 바꿔 단계 상태만 남겼다. `/my-work`, 업무 생성·알림·권한과 API 호환 필드는 유지한다. | TASK-007A·008A·009A·011A·012A·013A·QR-001·E2E-FULL-SUITE-001 완료 범위 | 호환용 업무 수 API 필드는 UI 미사용. 제거·재정의는 실제 소비자 확인 뒤 별도 change | No | 사용자 화면 검수 → 게시·runtime handover 별도 승인 |
| 4.6 | TASK-ATTACHMENT-001 — Pending 조치 사진과 재검사 근거 통합 | NEW_FEATURE | Local Main Merged / Remote Unpublished | Fable 2-pass·Pending 전용 bounded Draft→Confirmed 사진·회차 사유 snapshot·append-only evidence·IQC/LQC/OQC 재검사 근거 통합·migration 0066·자동 검증·사용자 검수·local main 승격 완료 | TASK-007A·009A·012A·WORKFLOW-CONTINUITY-001 experiment 완료 | 운영 storage 용량·scanner 활성화·backup/restore rehearsal·Remote push·Persistent UAT·실제 provider는 별도 운영 범위 | Yes | 완료 scope 재구현 금지; 원격 게시·runtime handover는 별도 승인 |
| 4.7 | TASK-QUALITY-OPERATING-MODEL-001 — 구매품 구분 기반 IQC / Change 004 Item별 LQC / Change 005 구분별 IQC 방식·양식 | APPROVED_FEATURE_IMPLEMENTATION | Change 006 Main Merged / Azure Released / User Validation Complete · Change 007 Publication Approved / Post-Deployment User Validation Pending | migration `0070`·`0071`, Item별 LQC와 구분별 IQC 운영본을 유지한다. Change 007은 기존 `AllReceipts` 프로젝트도 구매품 구분 metadata를 선택·저장하게 하되 기존 전역 IQC routing과 도착 후 변경 차단을 보존한다. | TASK-009A·TASK-ADMIN-002·TASK-012A·WORKFLOW-CONTINUITY-001·PRODUCTION-CONTROL-001·TASK-CI-COST-001 기존 계약 | 기존 확정 IQC/LQC 증빙·Pending 삭제 금지. 실제 Teams·메일 발송과 Web Push는 이 Task 범위 밖 | Yes | 통합 PR `CI Gate` → Azure 공개배포 → 사용자 운영 검수 |
| 5.1 | DESIGN-000 Design foundation | P2_REMEDIATION | Change 006 Main Merged / User Validation Complete | Change 001~005 완료 foundation을 재구현하지 않고 독립 Graphite 실험의 공통 시각·구성, 장식용 왼쪽 rail 제거, table density, 클릭형 부서 disclosure와 업무 선택 전용 page 삭제를 최신 main fixed allowlist로 승격. 사용자 검수·local main merge 뒤 TASK-EXPERIMENT-PROMOTION-001 Change 002에서 Azure 원격 기준선과 통합하고 Change 003~004에서 UL891 사용자 수정까지 같은 기준선에 이식해 PR #65로 원격 main 반영 | 사용자 Graphite 구현·검수·local merge 승인, 2026-08-04 원격 통합·UL891 merge 승인 | Figma publish와 App route split은 범위 밖 | Yes | 완료 scope 재구현 금지; 다음 Gate는 통합 source Azure image 재배포 |
| 5.2 | DESIGN-001 이후 화면 통일 | NEW_FEATURE | Experiment Complete | Automated validation·페이지 screenshot complete / `BATCHED_FINAL` | 로그인 shell, 후속 MOBILE-002와 DESIGN-000 foundation이 보완 | 없음. Figma publish는 optional 후속 | Yes | 완료 화면 통일 재구현 금지; 최종 일괄 검수 |
| 5.3 | TASK-UX-001 Action Feedback | NEW_FEATURE | Experiment Complete | A1·A2 implementation, automated·isolated browser validation complete / `BATCHED_FINAL` | A1 공통 contract·내 업무·알림, A2 생산·구매·자재·IQC·키팅·패널·Excel 완료 | 대표 repo·main·Persistent UAT 미반영; 전역 toast/store·A2 밖 문자열 tone 정리는 제외 | Yes | 재구현 금지; 최종 일괄 검수 |
| 5.4 | TASK-NOTIFY-005 사용자별 알림 | NEW_FEATURE | Experiment Complete | Automated validation complete / `BATCHED_FINAL` | TASK-NOTIFY-004·TASK-UX-001 A1 experiment | 관리자 감사 조회 UI는 별도 후속 | Yes | 재구현 금지; 최종 일괄 검수 |
| 5.5 | TASK-NOTIFY-AUDIT-001 관리자 preference 감사 조회 | NEW_FEATURE | Experiment Complete | Codex 2차 기획 대체·implementation·automated/isolated browser validation complete / `BATCHED_FINAL` | TASK-NOTIFY-005 audit 원장 | 대표 repo·main·Persistent UAT 미반영 | Yes | 재구현 금지; 최종 일괄 검수 |
| 5.6 | TASK-NOTIFY-REPROCESS-001 terminal Failed 수동 재처리 | NEW_FEATURE | Experiment Complete | Codex 2차 기획 대체·implementation·automated/isolated browser validation complete / `BATCHED_FINAL` | TASK-NOTIFY-004 delivery lineage | actual provider·대표 repo·main·Persistent UAT 미반영 | Yes | 재구현 금지; 최종 일괄 검수 |
| 6.1 | TASK-AZURE-PILOT-001 — 서비스 중립 공개 파일럿 준비 | UAT_RUNTIME | Main Merged / User Validation Complete | Entra API·SPA 분리, one-shot migration·ledger gate, Production preflight·ARM64/AMD64 image 검증과 PR #59 merge 완료 | TASK-UAT-001, local main 승인 기능과 사용자 P1 구현 승인 | provider-specific 실제 runtime은 TASK-AZURE-DEPLOY-001로 이관 | Yes | 완료 scope 재구현 금지 |
| 6.2 | TASK-AZURE-DEPLOY-001 — 20일 Azure 시범 배포 | BUGFIX / UAT_RUNTIME | Change 029 Current-main Azure Released / Automated Public Check Complete / User Validation Pending | PR #116·#117을 포함한 exact current-main `58daf6d8bfe333cb00e343a3fcc13ee4f3358183`을 Azure release `33577473523`로 배포했다. Migration `0085`, Backend, Frontend와 public security가 모두 통과했고 health `200`, 익명 root·API `401/401`, G2 8월 28일 재고 `6대`, 사이트 접속 coverage·양수 summary를 확인했다. 기존 Web Push·Teams·메일·Key Vault 참조와 업무 데이터는 보존했다. | TASK-AZURE-PILOT-001, TASK-CI-COST-001, TASK-SITE-ACCESS-001, TASK-G2-OPERATIONS-002 Change 003 | 사용자 사이트 접속 화면·Excel 검수, 기존 운영 인증·알림 설정·업무 데이터 보존 | Yes | 사용자 운영 화면 검수 → 운영 관찰 |
| 6.3 | TASK-CI-COST-001 — GitHub Actions minute 최적화 | P2_REMEDIATION | Change 001 Main Merged / Actual Code PR·Azure Release Observed | 영향 영역별 Backend/Frontend/Full-Stack·Workflow Validation, Backend test와 Full-Stack 병렬화, Ruleset+동일 tree 기반 main 중복 제거, Azure 변경 image 선택·병렬 build와 migration 선택 실행을 추가했다. PR #101 실제 코드 CI는 Backend와 Full-Stack을 병렬 실행해 약 20분에 완료했고, Change 024 Azure release는 Backend·Frontend image 병렬 build와 migration skip을 실제 적용했다. | 일반 CI·Azure run 집계, TASK-AZURE-DEPLOY-001 승인형 release·rollback·public security 보존 | 최소 1주 Actions 사용량 관찰, 실패·fallback 비율 추적 | No | 실제 사용량 관찰; 이상 시 기존 safe fallback 분석 |
| 6.4 | TASK-G2-OPERATIONS-001 — G2 생산·납품·재고·제조 출근 관리 | APPROVED_FEATURE_IMPLEMENTATION / UAT_RUNTIME | Implementation Complete / Automated Validation Complete / Publication Approved / Azure Deployment Approved / User Validation Pending | 독립 G2 migration `0081`·예상값 forward-fix `0082`, 권한·API, 홈·생산/출하·제조 출근 세 화면을 구현했다. Change 003~014의 가로표·filter·생산표·출근 disclosure·pastel graph·축·baseline·tooltip·수치·날짜·공휴일·공통 단조 scale, Change 015의 graph·mobile 내부 탐색·홈 납품행, Change 016의 관리표 구분행 제거·예상 숫자 날짜 도래 초기화, Change 017의 총 생산 평균선·graph KPI, Change 018의 조별 통합 hover·평균선·오늘 기준 재고 부족분, Change 019의 카드 내부 부족분 안내에 이어 Change 020에서 빈 실사·목표의 0 저장을 차단하고 Frontend 서울 날짜·월별 단일 조회·최신 응답 우선을 적용했다. Mobile graph는 좌우 축·frame을 고정하고 내부 날짜만 첫 화면 5일 기준으로 drag하며 KPI는 아래 2열로 재배치한다. 최신 원격 `main` 통합 기준 Backend `549/549`, Frontend `230/230`, G2·migration `64/64`, isolated Full-Stack과 Production migration image ledger `82 Exact`, 빈 필수 수량·실제 `0`·UTC→서울 월 경계·월 조회 횟수·응답 역전 회귀·카드 내부 안내 visual QA를 통과했다. | `TASK-QMS-PLATFORM-001`과 purpose·data·작업공간 분리. Implementation report에 SOP·manual·checklist 포함 | 손익관리·관리자 입력/수정 이력은 사용자 명시 제외 후속 NEW_FEATURE. 사용자 화면 검수는 대기 | Yes | Ready PR 필수 CI → exact `main` SHA Azure Change 027 공개배포 → 사용자 운영 검수 |
| 6.5 | TASK-G2-OPERATIONS-002 — 납품 목표·불량·홈 임시 시뮬레이션 | NEW_FEATURE / BUGFIX / UAT_RUNTIME | Change 004 Automated Validation Complete / Publication Approved | 사용자 승인 Codex 직접 기획·구현. Additive migration `0084`로 납품 목표와 불량을 정식 데이터에 추가하고 홈 저장 없는 임시 시뮬레이션을 제공한다. Change 003 공개배포 뒤 Change 004는 2026-08-28부터 `오늘 재고 = 전일 재고 + 전일 생산 - 전일 불량 - 오늘 납품`으로 교정한다. Backend·Frontend·실제 PostgreSQL·격리 Full-Stack 회귀가 통과했고 migration·원본 데이터는 변경하지 않는다. | TASK-G2-OPERATIONS-001 완료 계약, 최초 PR #115, Change 003 PR #117 | Persistent UAT 미적용. 운영 G2 원본 데이터·schema 무변경 | Yes | Ready PR 필수 CI → exact main SHA Azure 공개배포 → 공개 G2 확인 |

Phase 1 기능에서도 loading·empty·error·success feedback, 접근성, 한글 안내, 390px/Teams narrow, page-level overflow 0과 기존 CSS variable·공통 component 우선 원칙을 적용한다. 시각 token과 브랜드 통일은 DESIGN Task로 후행한다. 공용 태블릿, 공용 기기 mode·session 정책과 sessionStorage 강제 정책은 이 큐에 포함하지 않는다.

TASK-008A와 TASK-010A는 데이터·rollback·검증 경계가 다르므로 하나의 구현 또는 PR로 묶지 않는다. 메일·알림 채널 matrix는 현재 확정 상태를 유지하며 긴급·차단 메일, 에스컬레이션 메일 또는 메일의 역할을 바꾸려면 별도 `POLICY_DECISION`이 필요하다.

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
- 목적: `work_items.due_date` 기반 L0~L3 호환 엔진을 구축했다. 현재 신규 평가 정책은 TASK-NOTIFY-POLICY-001의 L0·L1로 대체됐다.
- 포함 범위: `work_item_escalations`, L0(예정일 직전 영업일), L1(초과 즉시), L2(+2영업일), L3(+3영업일, 생산관리·영업 한정), BusinessDayCalculator 재사용, recipient resolver, 인앱 notification/recipient 생성, `notification_deliveries` 연동, Gmail SMTP Mail delivery 연동, Teams 개인 알림 dry-run delivery, 관리자 에스컬레이션 조회 API, Daily Digest 담당 프로젝트 요약
- 제외 범위(당시): Teams Activity Feed 실제 구현, Teams DM 실제 구현, 생산계획/구매 예정일 자동 due_date 동기화, due_date 입력 UI, Pending List, 알림 설정 UI, 수동 재처리 UI, 부서장/경영진 수신. 자동 동기화와 현재 평가 단계는 TASK-NOTIFY-POLICY-001에서 확정했다.
- 선행조건: TASK-NOTIFY-001, TASK-CALENDAR-001
- 주요 테스트: backend 전체 test, Notification/Escalation targeted tests, Migration tests, Authorization tests, BusinessDay tests, frontend lint/typecheck/unit/build, mock UI smoke, Full-Stack E2E, seed A/B/C/D, UAT DB persistence, UAT L0 dry-run smoke

### TASK-ADMIN-001: 관리자 기준정보 페이지

- 상태: 본체 완료 / Change 001 사용자 검수 완료·통합 게시 승인
- 목적: 시스템 관리 중심의 관리자 홈과 사용자/부서/휴일/이력/모니터 화면을 제공한다.
- 포함 범위: 관리자 홈, 사용자 관리 재사용/확장, 부서 관리, 휴일 관리 재사용, 삭제 예정/복구/일괄 action, 권한 매트릭스 read-only, 기준정보 변경 이력, 업무 시작/완료 이력, 알림 발송 상태 조회와 실패/대기 상세 추적, 에스컬레이션 상태 조회와 L0~L3 breakdown, 부서 field-level validation
- 제외 범위: Item 관리, 포장방식 관리, 생산계획 단계 관리, 구매 필수 항목 관리, 권한 편집, role master 편집, Pending/검사/제조 템플릿, due_date 정책 관리, Teams Activity actual
- 선행조건: 권한/관리자 정책 확정
- 주요 테스트: backend 전체 test, Admin targeted tests, Migration tests, Authorization tests, Calendar/Holiday tests, User/Identity tests, frontend lint/typecheck/unit/build, mock UI smoke, Full-Stack E2E, UAT admin browser/deletion smoke, secret scan
- Change 001: 관리자 홈에서 `발송 완료`, `마지막 일일 요약`, `최근 기준정보 변경` KPI와 불필요한 집계를 제거했다. 원본 알림·Daily Digest·기준정보 변경 이력 기능은 보존한다. 승인 대기 KPI는 `/admin/users?filter=approval-pending`으로 이동해 활성 Entra 사용자 중 역할이 없는 사용자만 표시하며, 일반 사용자 관리는 전체 목록을 유지한다.

### BASELINE-GOV-001: 개인정보 및 Task 거버넌스 기준선 정비

- 상태: 완료 — 사용자 승인 후 PR #21 squash merge(`3bc3ef8`)
- 목적: tracked 문서의 사용자 개인정보를 비식별화하고 모든 Task의 종료 산출물·품질 gate·검수 상태 기준을 단일 정책으로 확립한다.
- 포함 범위: 기존 동일 목적 branch read-only 비교, NOTIFY-003 문서 비식별화, [Task 종료 및 산출물 정책](12-task-completion-policy.md), Activity Feed provider/capability와 event coverage 상태 분리, 후속 Task 우선순위 등록
- 제외 범위: runtime code, dependency, migration, DB, UAT, worker, 외부 발송, 후속 기능 구현
- 선행조건: main Git 기준선 일치, 기존 WIP 보존, 동일 목적 branch의 고유 정책 비교·통합
- 산출물: [Task 정의와 검수 체크리스트](../tasks/baseline-gov-001.md), [Implementation report](../tasks/baseline-gov-001-implementation-report.md), [SOP](../tasks/baseline-gov-001-sop.md), [User manual](../tasks/baseline-gov-001-user-manual.md), 이 Roadmap update
- 완료 조건: 문서/링크/PII/secret/범위 검증 통과와 사용자 validation checklist 확인. 체크리스트 생성과 사용자 검수 완료를 구분한다.

### TASK-GOV-002: Git history 개인정보 risk decision

- 상태: `COORDINATED_HISTORY_REWRITE` 정책 선택·planning·5종 산출물 사용자 검수 완료 / PR #41 Ready·squash merge 승인
- 목적: current checkout에서 제거된 개인정보가 Git history에 남은 위험과 repository 공개 범위를 평가하고, history rewrite 필요 여부와 협업 절차를 결정한다.
- 당시 확인 결과: Repository `PUBLIC`, current checkout exact match 0, origin main 영향 1 commit/2 files, 영향 remote ref 15/18, local branch 19/20, fork/open PR/tag 0. 외부 clone·download는 완전 열거 불가. 2026-08-10 actual readback의 현재 visibility는 `PRIVATE`다.
- 승인 정책: main-only나 private-only가 아닌 coordinated all-ref rewrite. Risk owner는 `Repository owner / security owner`
- 포함 범위: 영향 commit/file/ref aggregate, visibility와 clone/fork 한계, 대안 비교, risk owner, 후속 maintenance·backup·re-clone 승인 경계
- 제외 범위: 이 Task에서 history rewrite, force push, visibility·ruleset 변경, tag/branch 삭제
- 후속 Task: `TASK-GOV-HISTORY-REWRITE-001`에서 fresh mirror, private mapping, secure backup, all-ref force push, fresh clone과 cache 검증을 별도 planning·승인
- 예상 migration: 없음
- 핵심 검수 기준: 실제 값 원문을 재노출하지 않고 결정 근거·영향·완화책·실행 승인 여부를 문서화
- 주요 위험: descendant SHA 변경, old clone 재유입, partial ref rewrite, GitHub cache 잔존. Rewrite 완료 전 `GIT_HISTORY_PERSONAL_DATA_REMAINS` P2는 Open
- 산출물: [Planning](../tasks/gov-002-planning.md), [Task와 검수 checklist](../tasks/gov-002.md), [Implementation report](../tasks/gov-002-implementation-report.md), [SOP](../tasks/gov-002-sop.md), [User manual](../tasks/gov-002-user-manual.md), 이 Roadmap update

### TASK-GOV-HISTORY-REWRITE-001: Coordinated Git history rewrite

- 상태: 영향 published ref rewrite·fresh clone·old common repository push quarantine·Support cleanup·독립 검증·사용자 검수 완료 / Task 완료 당시 Repository `PUBLIC`, 2026-08-10 현재 `PRIVATE` / cached reference `REMOVED` / PR #50 Merged
- 목적: current checkout에서 제거된 과거 개인정보를 모든 영향 published ref에서 제거하고 cache·old clone 재유입·backup 경계를 분리해 검증한다.
- 실행 결과: 영향 ref `16/16`, 예상 밖 ref 이동 0, tip tree mismatch 0, fresh-clone history exact match 0, fsck error 0
- Support 결과: completion/follow-up/closed fixed projection `1/1/1`, old cached reference `REMOVED`, page-not-found `true`
- 안전 경계: encrypted pre-rewrite backup 제한 보존, restore·삭제 별도 승인, old common repository push quarantine와 dirty worktree 보존
- 잔여 Gate: encrypted backup 삭제는 보존 경계와 별도 승인 뒤 수행
- 제품 영향: Backend·Frontend·API·DB·migration·runtime·provider 변경 0
- P2: `GIT_HISTORY_PERSONAL_DATA_REMAINS` Resolved. 외부 clone·archive 완전 회수는 증명하지 않으며 public 재개를 자동 승인하지 않는다.
- CI Finding: `FULL_STACK_E2E_PROCUREMENT_EDIT_ROW_RACE`는 `TASK-E2E-RELIABILITY-001` 보정·검증과 PR #43 병합으로 Resolved다.
- 절차 Finding: Support closure raw page snapshot과 publication push 원문 재발 각 1건은 tracked leak·secret 0을 확인하고 Support/GitHub fixed projection으로 재검증해 Resolved다.
- 산출물: [Planning](../tasks/gov-history-rewrite-001-planning.md), [Task와 검수 checklist](../tasks/gov-history-rewrite-001.md), [Implementation report](../tasks/gov-history-rewrite-001-implementation-report.md), [SOP](../tasks/gov-history-rewrite-001-sop.md), [User manual](../tasks/gov-history-rewrite-001-user-manual.md), 이 Roadmap update

### TASK-GOV-FINDING-GATE-001: 전체 P0/P1/P2 재평가

- 상태: read-only closure matrix 재평가·독립 검증·사용자 검수 완료 / Open P0/P1/P2 `0/0/0` / PR #50 Merged
- 목적: 실제 main·최근 merge·운영 read-only 기준선과 외부 blocker를 closure matrix로 대조한다.
- 선행 해소: E2E row race PR #43, Failed retry 문서 drift PR #44, Git history internal reference 제거와 GC 완료
- Task 이름: canonical ID는 `TASK-GOV-FINDING-GATE-001`이다. `TASK-GOV-P2-GATE-001`은 동일 목적의 non-canonical shorthand이며 별도 Task가 아니다.
- 결과: History·E2E row race·Failed retry·privacy-safe evidence 절차 P2는 모두 Resolved. Runtime·Persistent aggregate 정상, source TODO/FIXME/validation bypass 0
- 권고: `GO_FOR_USER_DECISION`. 신규 기능 시작 승인이 아니며 0.6의 사용자 정책 결정을 기다린다.
- 다음 Gate: 승인된 문서 squash merge → 신규 기능 Go/No-Go 별도 결정
- 산출물: [Planning](../tasks/gov-finding-gate-001-planning.md), [Task와 검수 checklist](../tasks/gov-finding-gate-001.md), [Implementation report](../tasks/gov-finding-gate-001-implementation-report.md), [SOP](../tasks/gov-finding-gate-001-sop.md), [User manual](../tasks/gov-finding-gate-001-user-manual.md), 이 Roadmap update

### TASK-DESIGN-LOGIN-001: Entra 로그인 공통 디자인 shell

- 상태: Change 001~009 구현·자동·독립 검증·사용자 전체 검수·PR #49 merge 완료 / Change 010 모바일 로그인·지정 로그인 로고 PR #83 merge·Azure 운영 release 완료 / Android 육안 검수 완료·PC·iPhone 추가 관찰 대기
- 순서 승인: 사용자가 History Support 대기 중 이 Frontend-only Task의 병렬 진행과 bounded worktree 사용을 명시 승인했다. 당시 history P2와 신규 기능 `NO_GO` 상태는 변경하지 않았다.
- 목적: 기존 Entra 인증 정책과 request/cache 동작을 보존하면서 승인된 Figma 디자인을 인증 공통 shell과 Desktop 로그인 화면에 구현한다. Change 001에 따라 Mobile은 제외하고 로그인 화면에는 Figma에 존재하는 요소만 표시한다.
- Figma 기준: node `1:175`, 1440×810 design context·screenshot·metadata·assets 재확인, variable definition 0. 로그인 상태 유지 `1:187`은 component set `1:160`의 `속성 1=베리언트2`; 기본은 white·`#737373`·icon 0, Variant 2는 `#DA2127`·white Done icon·`#282828`이다. Code Connect는 사용자 결정에 따라 향후 필수 구현·검수 Gate에서 제외한다.
- 구현: Figma 원본 장식·EMI/Microsoft asset과 공통 인증 state shell을 적용했다. 로그인 상태 유지는 클릭 시 Variant 2의 red fill·Figma Done icon·dark text로 전환하고 기존 preference/cache 의미를 유지한다. Change 008은 icon asset·크기와 나머지 style을 바꾸지 않고 Done icon만 checkbox `50% 50%`에 중앙 정렬한다. Desktop Loading은 기본 로그인과 같은 logo/title/Microsoft logo/안내/배경 canvas를 유지하되 `LOGIN`·checkbox를 제거하고 그 영역에 `#DA2127` 회전 indicator 1개를 표시한다. 다른 계정 선택은 Microsoft 로그인 화면의 provider UX에 위임하며 우리 Frontend의 중복 action·전용 request는 제거했다. Ellipse 68은 실제 pattern fill의 `-538.5/-468/876×876`, opacity 0.33으로 보정했다.
- Figma 배경·연결부: frame base `#DA2127`, `-6/0/1446×810` white 10% glass, `776/0/664.5×810` white shape, left radius 51과 shadow `-5.25/-1.5/43.05`를 재확인했다. White shape 뒤를 red frame으로 유지해 rounded corner 밖과 shadow 뒤에 red가 이어진다.
- PC 반응형: red/white flexible panel이 viewport 전체를 채우고 각 panel 내부 reference content만 등비 축소·확대한다. 1920×1080, 1440×810, 1280×720, 1024×768, 1440×600, 651×708와 live resize에서 panel coverage 100%, inner content fully visible, normalized geometry 유지, horizontal/vertical overflow 0을 확인했다.
- 보존: Microsoft 365 기본 로그인, provider 로그인 화면의 계정 선택, 로그인 상태 유지, silent token·재인증 동작. Backend·API·DB·migration·runtime configuration·dependency 변경 0
- 자동 검증: Frontend lint/typecheck/unit 66/66/build, 기본 로그인 6개 + Loading 6개 PC browser 12/12, live resize와 mock UI 1/1 통과. 기본 로그인 6개 모두 미선택 기본 variant와 클릭 후 Variant 2의 fill·border·Done icon·`13.5×13.5`·`50% 50%` 중앙 정렬·text·preference 저장을 확인했다. Loading button 0·checkbox 0·indicator 1, red/animation/48.75×48.75 geometry, background/connection contract, panel coverage 100%, overflow 0, console/request failure 0을 확인했다.
- 시각 비교: 승인 안내 포함 MAE 1.2497, exact pixel 69.3100%, channel당 차이 8 이하 97.7912%. Figma에 없는 승인 안내 영역 제외 시 MAE 1.1303, channel당 차이 8 이하 97.9200%. 렌더러 차이와 승인 추가 영역을 raw pixel 100% 동일성으로 과장하지 않는다.
- 인증 action audit: Frontend 전용 account-switch request·`select_account` prompt·handler·prop·button은 모두 제거됐다. Microsoft provider의 `다른 계정 사용`만 일반 login redirect 뒤에 남는다. 기본 로그인·상태 유지·재인증·로그아웃은 정상 또는 조건부 접근 가능하고 cached restore·silent token은 자동 기능이다. 설정 누락은 redirect 불가 fail-safe이며 orphan authentication action은 0이다.
- 독립 검증: Change 008·Change 009 PASS. Change 009는 allowlist 26/26, source hash 12/12, 최신 main Roadmap baseline, 인증 불변조건, runtime·privacy·Finding gate를 재검증해 현재 P0/P1/P2/P3 `0/0/0/0`, 해결된 P2 1로 판정했다.
- 잔여 Gate: Promotion·5176 experiment worktree 정리는 사용자 요청으로 Deferred이며 후속 HOUSEKEEPING 승인에서 수행
- Change 010: 기존 인증 계약과 Desktop geometry는 보존하고, 860px 이하 로그인·Loading을 흑백 wireframe으로 재배치하며 지정 `Asset 3@4x.png`의 원본 색상을 유지한다. iPhone 390px·Android 412px 자동·브라우저 검증 뒤 승인된 main 병합·공개배포로 이어간다.
- 산출물: [Task와 검수 checklist](../tasks/design-login-001.md), [Implementation report·User manual](../tasks/design-login-001-implementation-report.md), [화면 단위 승격 SOP](development/design-screen-promotion.md), 이 Roadmap update

### TASK-GOV-CODEX-002: Fable 5 신규 기능·Codex-only 작업 라우터

- 상태: 완료 / PR #38 squash merge
- 목적: 신규 기능만 Fable 5 deep-interview와 primary draft로 보내고, 승인된 기능 구현과 BUGFIX·P2·SECURITY·UAT·DOCS·HOUSEKEEPING·POLICY 작업은 Codex-only 조사·승인·구현·독립 검증 흐름으로 처리한다.
- 포함 범위: Root Task 유형 라우터, Fable 전용 `CLAUDE.md`, 수정 요청과 planning·review·change·implementation report 역할, Codex 세션 분리
- 제외 범위: 제품 코드, migration, dependency, runtime, Persistent UAT와 Change 008 검증을 넘는 실제 Fable 호출
- 안전 경계: Fable 5 alias `fable`, read-only 도구, 전용 fail-closed runner, Task-scoped private session·drift guard, 원문 byte equality, recursive workflow 금지
- 산출물: [Task·SOP·User manual·검수 checklist](../tasks/gov-codex-002.md), [Implementation report](../tasks/gov-codex-002-implementation-report.md), 이 Roadmap update
- 자동 검증: 새 Codex read-only session route 9/9, static router 11/11, Fable CLI read-only option 8/8, diff·actionlint·Markdown·secret/PII·allowlist 통과
- 사용자 검수: 완료
- Change 001: 신규 기능은 Fable 5 deep-interview, 사용자 요약 확인과 blocking decision 0을 먼저 통과한 뒤 Fable 5 planning을 시작한다. Codex는 안전한 relay·기록·review를 담당하며 interview 완료는 planning·implementation 승인과 분리한다.
- Change 002: 새 Task 생성 전 목표·Finding·변경 경계·불변조건·산출물의 semantic identity와 Roadmap status·dependency·external blocker·Next Gate를 대조한다. 같은 목적은 기존 canonical Task를 재사용하고, 모호하거나 순서가 다르면 명시적 재정렬 승인 전 중단한다.
- Change 003: 일반 Task는 fresh canonical clone 하나에서 branch만 전환하고, 별도 worktree는 runtime·병렬 write·고위험 rehearsal에 한정한다. Clean·process 미사용·open PR 없음·commit reachable gate로 기존 worktree 30개 중 21개를 정리해 약 4.03GB를 회수했으며 dirty 3개와 process 사용 5개는 보존했다.
- Change 004: PR #48·#49·#50의 clean inactive worktree 3개를 제거해 linked worktree를 `5→2`로 정리했다. Canonical root와 5176 디자인 실험만 보존하고 root WIP는 stash로 보존한 뒤 최신 main 기반 cleanup branch로 정규화했다.
- Change 005: Public default branch `main`에 active required-pull-request ruleset을 적용해 direct main push 금지를 서버 측에서 강제했다. 1인 개발 속도를 위해 승인·CI·최신화·review 해결은 강제하지 않는다.
- Change 006: GitHub 최상위 과거 checkout을 먼저 mode `0700` 보존 폴더로 통합해 `6→3`으로 정리했다. 후속 exact audit에서 dirty checkout 6개와 local branch 32개의 canonical 보존 필요성이 없음을 확인하고, 승인된 Docker/PostgreSQL controlled maintenance로 stale handle `4→0`을 만든 뒤 보존 폴더를 영구 삭제해 최종 `6→3→2`로 정리했다. 동일 PostgreSQL container·persistent volume·DB aggregate와 대표·디자인 runtime을 보존하고 사용자 검수·PR #52 squash merge를 완료했다.
- Change 007: 사용자가 terminal을 대신 실행하지 않는 Fable 5 전용 read-only runner와 runner prefix 전용 Rule을 구현했다.
- Change 008: 같은 Task 기준선 재조사를 줄이는 private session·drift guard·질문 최대 5개·exact cleanup을 구현했다. Session 재개 overhead는 1초였지만 장문 planning 총시간 단축은 입증하지 못했다.
- Change 009: Fable 질문·primary draft 원문을 byte-for-byte 기록하고 Codex는 원문을 수정하지 않은 채 별도 review만 작성한다.
- Change 010: 기본 흐름을 Fable primary draft 1회·Codex 내용 review 1회로 종료하고 사용자 명시 요청 없는 자동 revise를 차단한다.
- Change 011: HTTPS 5174 Vite는 대표 clone의 현재 branch를 따르고 clean branch 전환 중 server를 유지한다. Env·dependency·Vite startup 계약 변경이나 자동 갱신 실패 때만 재시작하며, 5174 실행이나 clean·reachable branch의 open PR 자체는 추가 worktree 생성 사유가 아니다. Dirty WIP는 계속 전환을 차단한다.
- Change 012: 사용자가 기존 검수·게시 선행 순서를 재정렬하고 Fable·USER-FLOW WIP의 local preservation commit, 대표 clone 선별 이식과 일반 worktree 제거를 승인했다. Push·PR·merge·branch 삭제는 제외한다.
- Change 013: 독립 검증의 P2에 따라 generic `docs/` primary draft를 planning·review 구현 승인과 분리하고 latest change의 사용자 요청·exact target으로 gate한다. USER-FLOW 전용 형식은 exact historical redraft 조합으로 한정하고 Reporting·Roadmap 상태 충돌을 해소했다. P2 `3/3` Resolved, 독립 재검증 PASS, merge 승인 상태다.
- Change 014: 사용자의 실험 개발 기본 규칙에 따라 `experiment/*`에서만 Fable 1차 planning·Codex review·review를 직접 읽는 Fable 2차 planning·Codex 구현·screenshot·local commit을 중간 승인 없이 수행한다. Runner `second-planning`은 experiment branch, 기존 planning·review, 최신 approval marker와 exact target을 fail-closed로 확인한다. 일반 branch 계약과 대표 repo·`main`·Persistent UAT·provider·push·PR·merge는 변경하지 않으며 main merge 승인은 `0/3`이다.
- Change 015: `experiment/*`의 구현·자동 검증 완료 scope를 `EXPERIMENT_COMPLETE / BATCHED_FINAL` 원장으로 고정한다. 사용자 최종 검수 대기·대표 repo 미반영·P3 후속만을 이유로 완료 Task를 Fable·새 planning·재구현에 다시 보내지 않으며, 실패 시 기존 Task의 change/bugfix로 재개한다.

### TASK-GOV-REPORTING-001: Task 시작·완료 보고 표준화

- 상태: 최초 Task 구현·검증·PR merge 완료 / Change 001 구현·자동·독립 검증·사용자 검수 완료 / 상태 충돌 P2 Resolved / merge 승인
- 목적: 모든 새 Task와 분리 Codex session이 변경 전에 실제 Repository instruction chain을 읽고, Task 종료 시 고정 10개 항목으로 완료 보고하도록 표준화한다.
- 포함 범위: Root `AGENTS.md`, Task 종료 정책, 실행 SOP, 사용자 확인 방법과 Decision Log
- 완료 보고: 수정 요약, 수정 파일, 실행 테스트, 테스트 결과, Frontend URL, Backend URL, 수동 검수 checklist, 미커밋 변경, 남은 문제, 게시 가능 여부
- 안전 경계: 적용 대상이 없는 항목도 `N/A`와 이유를 기록하며, 게시 가능 `GO`는 Git 게시 승인을 대신하지 않는다.
- Change 001: 고정 10개 항목 앞에 현재 Task·남은 일·Commit/Push/PR/Merge·중단/보류 Task·재개 조건·Roadmap next를 표시하는 `작업 현황 요약`을 추가하고, Finding은 count가 아니라 원인·영향·해소 또는 backlog 위치까지 추적한다. 사용자 검수와 독립 재검증 PASS 뒤 Governance 동시 merge 승인은 완료됐다.
- 제품 영향: Backend·Frontend·migration·runtime·Persistent UAT 변경 없음
- 산출물: [Task와 검수 checklist](../tasks/gov-reporting-001.md), [Implementation report](../tasks/gov-reporting-001-implementation-report.md), [SOP](../tasks/gov-reporting-001-sop.md), [User manual](../tasks/gov-reporting-001-user-manual.md), 이 Roadmap update

### TASK-E2E-RELIABILITY-001: 구매정보 편집 행 준비성 안정화

- 상태: 구현·자동 검증·사용자 검수 완료 / squash merge 승인
- 목적: 구매정보 편집 load가 겹칠 때 늦은 응답이 새 입력 행을 제거해 Full-Stack E2E가 간헐 실패하는 P2를 해소한다.
- Root cause: `ProcurementEditPage`에 stale-response guard가 없어 StrictMode의 겹친 load가 edit state를 덮어쓸 수 있었고, 기존 E2E의 timeout·재클릭은 해당 race를 숨기거나 중복 행을 만들 수 있었다.
- 보정: 기존 Repository request-id 패턴으로 최신 load만 반영하고, E2E는 행 추가 1회·정확한 row 증가·input 8개 준비를 결정적으로 검증한다.
- 영향: Frontend source·unit·Full-Stack E2E만 변경. Backend·API·DB·migration·runtime configuration 변경 없음
- 자동 검증: 수정 전 deterministic regression 실패 재현, 수정 후 targeted PASS, 대상 E2E 20/20, frontend unit 62/62·lint·typecheck·build, 전체 Full-Stack E2E 16/16 통과
- Change 001: 심화 검수에서 확인된 `PROCUREMENT-INITIAL-LOAD-ACTION-UNLOCKED` P2를 해소했다. project·procurement 최신 초기 load 전에는 행 추가·저장·Excel 동작을 잠그고, 상태 안내와 deterministic unit·isolated Full-Stack E2E를 추가했다. experiment `BATCHED_FINAL`, 대표 repo·main 미반영.
- 산출물: [Task와 검수 checklist](../tasks/e2e-reliability-001.md), [Implementation report](../tasks/e2e-reliability-001-implementation-report.md), [SOP](../tasks/e2e-reliability-001-sop.md), [User manual](../tasks/e2e-reliability-001-user-manual.md), 이 Roadmap update

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

- 상태/다음 순서: 최초 Task와 Change 001~006 완료 / Change 007 Pending 상세 mixed-version blank 복구·자동 검증·actual 5174 smoke·사용자 검수 완료, local `main`과 TASK-EXPERIMENT-PROMOTION-001 Change 002 원격 `main` 통합 완료 / 조치 사진 current-source 5081 handover는 migration 승인 전 보류
- 목적: HTTPS Development UAT를 안정화하고, 로그인·source/DB 노출·운영 request/upload/hosting 경계를 공개 배포 전에 fail-closed한다.
- 포함 범위: 기존 UAT 안정화 범위, Entra HTTPS 로그인과 익명 API 차단, Vite/DB loopback, security header, Host/trusted proxy, rate limit, upload malware/metadata 검사, static TLS reverse proxy, secret/DB TLS/restore/SIEM/break-glass Production startup gate
- 제외 범위: actual 운영 domain·certificate·Entra app 변경, managed DB·backup provider 설정, SIEM receiver 설정, 실제 외부 알림 발송과 운영 runtime handover
- 선행조건: TASK-E2E-ISOLATION-001 완료, persistent UAT와 HTTPS server 보존
- 예상 migration: 없음
- 핵심 검수 기준: 기존 UAT 계약 보존, 공개 Development/DB listener 없음, unsafe Production 설정 startup 실패, 안전 synthetic Production policy 통과, host/header/rate/upload 회귀와 production container build·Compose 검증, actual 운영값은 별도 handover
- Change 005 자동 검증: Production security 27/27, Backend 461/461, Frontend 143/143, Mock UI 4/4, Full-Stack E2E 55/55, dependency 취약점 0, Backend·TLS validator image 전 심각도 0, Frontend·ClamAV image Critical/High/Medium 0. Open P0/P1/P2/P3 `0/0/0/1`; 수정본이 없는 libxml2 Low 2건은 `SEC-PUBLIC-014` P3로 재검사하고 scanner Unspecified 2건은 영향 실행 파일 부재를 확인해 `SEC-PUBLIC-015 RESOLVED_NOT_AFFECTED`로 닫았다. 코드 게시 품질 gate `GO`, actual 운영 domain·Entra·managed DB·restore·SIEM handover는 `NO_GO_EXTERNAL`
- Change 001 자동·사용자 검증: trusted HTTPS root/notification/Teams/API/health 200, desktop/390px 6/6, console/request/overflow 0, PostgreSQL·Review-safe·design runtime 보존, obsolete isolated container/network 3/3 정리. 5081 Delivery worker만 활성화하고 기존 `TeamsActivityDisabled` terminal 2건을 audit로 보존했으며 신규 ManualTest 1건은 retry lineage `RetryScheduled/RetryScheduled/Sent`와 Teams client 실제 표시를 확인했다.
- Change 007 자동·runtime 검증: `/pending/` canonicalization과 구형 5081의 `actionEvidence` 누락 상세를 section 단위로 격리했다. Frontend 172/172, mock UI 4/4, build와 로그인된 5174의 dashboard·프로젝트·실제 상세·이력을 확인했다. Persistent UAT DB·5081·provider는 변경하지 않았다. Open P0/P1/P2/P3 `0/0/0/1`; live ledger 64개와 source 67개 drift는 `UAT-PENDING-007-D` P3로 후속 controlled migration·runtime handover에서 재검토한다.
- 산출물: [Task 정의와 검수 체크리스트](../tasks/uat-001-https-dev-stability.md), [Implementation report](../tasks/uat-001-implementation-report.md), [SOP](../tasks/uat-001-sop.md), [User manual](../tasks/uat-001-user-manual.md), 이 Roadmap update
- 주요 위험: Development actual provider 오발송, UAT worker 자연 변경과 E2E 영향 혼동, code readiness를 actual 운영 준비로 오판하는 위험. actual domain·Entra·certificate·managed DB·restore·SIEM 검수 전 공개 배포는 금지한다.

### TASK-AZURE-PILOT-001: 서비스 중립 공개 파일럿 준비

- 상태/다음 순서: 구현·자동 검증·사용자 검수·PR #59 main merge 완료 / provider-specific 배포는 TASK-AZURE-DEPLOY-001
- 목적: 특정 Azure 서비스를 선택하기 전에 GitHub 게시 후보, Production Entra API·SPA 분리, application과 분리된 migration과 preflight P1을 닫는다.
- 포함 범위: `ENTRA_API_CLIENT_ID`·`ENTRA_SPA_CLIENT_ID` 분리와 fail-closed build/startup, `--migrate-only`, PostgreSQL advisory lock·transaction·ledger/schema 재검증, Production Compose operations profile, privacy-safe preflight와 disposable Production image fresh/existing migration 검증
- 제외 범위: Azure hosting·managed DB·domain·WAF·SIEM·registry/OIDC·provider release/rollback workflow 선정, 실제 cloud mutation, traffic cutover, Teams·메일 actual 발송, Persistent UAT 변경과 `main` merge
- 선행조건: TASK-UAT-001 Change 005·006 사용자 검수 완료, local `main` 승인 제품 commit, 사용자 P1 구현 승인
- 예상 migration: 없음. 기존 migration 67개를 Production image에 포함해 별도 실행하며 migration SQL은 수정하지 않음
- 자동 검증: Backend 469/469, Production security 30/30, Frontend 144/144·lint/typecheck/build, Mock UI 4/4, isolated Full-Stack 55/55, preflight 4/4, Production image fresh/existing apply와 ledger Exact, ARM64·AMD64 Backend/Frontend build·Critical/High 0
- Finding: `OPS-PILOT-001`, `OPS-PILOT-003`, `OPS-PILOT-MIGRATION-001` P1 Resolved. `OPS-PILOT-002`·`OPS-PILOT-004`는 TASK-AZURE-DEPLOY-001 provider-specific 실행에서 계속 추적
- 산출물: [Identity Gate](../tasks/azure-pilot-001-identity-gate.md), [Change 001](../tasks/azure-pilot-001-change-001.md), [Implementation report](../tasks/azure-pilot-001-implementation-report.md), [SOP](../tasks/azure-pilot-001-sop.md), [User manual](../tasks/azure-pilot-001-user-manual.md), [User validation checklist](../tasks/azure-pilot-001-user-validation-checklist.md), 이 Roadmap update
- 다음 Gate: 없음. 서비스 중립 준비 scope는 닫고 TASK-AZURE-DEPLOY-001에서 20일 시범 runtime을 검증한다.

### TASK-AZURE-DEPLOY-001: 20일 Azure 시범 배포

- 상태/다음 순서: Change 022 원격 main·Azure 공개 배포와 Web Push 실제 provider·iPhone·Android 검수 완료 / Change 002 운영 문서·Azure 재배포 보존 artifact 구현·검증
- 목적: 승인된 Azure 시범 사양을 provider-specific 배포 artifact, migration·restore·traffic gate와 Teams manifest로 전환해 3개 프로젝트를 20일 동안 안전하게 시범 운영한다.
- 포함 범위: Front Door Standard custom rate limit, Container Apps Consumption Frontend/API/ClamAV, one-shot migration job, private PostgreSQL Flexible Server B2s 32 GB·PITR 14일, ACR Basic, Azure Files 5 GB, Key Vault, Log Analytics 1 GB/day cap, Application Insights, 최종 hostname·Entra·Teams manifest handover
- 제외 범위: 기존 PostgreSQL 첨부의 Blob 이관, HA, Front Door Premium managed WAF, 실제 비용 resource의 Codex 자동 생성, 사용자 승인 없는 traffic·provider 발송, 정식 운영 사양 확정
- 선행조건: TASK-AZURE-PILOT-001 PR #59 main merge, 사용자 20일 시범 구성과 비용 owner 결정
- migration: Azure 운영에 `0074`·`0075`까지 적용 완료. Change 002는 migration과 DB 변경이 없다.
- 자동 검증: Change 003 Backend 보안 집중 42/42·전체 격리 회귀 481/481, 실제 PostgreSQL runtime role 업무 CRUD 성공·schema/role/temporary/ledger mutation 거부, 기존 migration image 67 Exact, Bicep 4종 compile. Change 004 ARM JSON·OIDC image workflow 검증. Change 006 Teams package 2/2, PWA asset 1/1, Frontend 175/175·lint·typecheck·build, Azure static artifact와 local Production preview manifest/icon `200` 완료
- Finding: `AZURE-RELEASE-RUNNING-STATE-001` P1은 정상 `RunningAtMaxScale` 오판을 exact allowlist와 actual release 성공으로 `RESOLVED`했다. main Full-Stack 첫 시도의 5초 UI 표시 지연 `CI-FULLSTACK-QUALITY-REFRESH-001` P2는 격리 `1/1`과 실패 job 재실행 성공으로 `RESOLVED`했다. 성공한 release의 action/CLI 경고 2건은 `GHA-AZURE-RUNNER-WARNINGS-001` P3 backlog로 추적한다.
- 산출물: [Identity Gate](../tasks/azure-deploy-001-identity-gate.md), [Change 009](../tasks/azure-deploy-001-change-009.md), [Change 010](../tasks/azure-deploy-001-change-010.md), [Change 011](../tasks/azure-deploy-001-change-011.md), [Change 012](../tasks/azure-deploy-001-change-012.md), [Change 013](../tasks/azure-deploy-001-change-013.md), [Change 014](../tasks/azure-deploy-001-change-014.md), [Change 015](../tasks/azure-deploy-001-change-015.md), [Change 016](../tasks/azure-deploy-001-change-016.md), [Change 017](../tasks/azure-deploy-001-change-017.md), [Change 018](../tasks/azure-deploy-001-change-018.md), [Change 019](../tasks/azure-deploy-001-change-019.md), [Change 021](../tasks/azure-deploy-001-change-021.md), [Change 022](../tasks/azure-deploy-001-change-022.md), [Implementation report](../tasks/azure-deploy-001-implementation-report.md), [SOP](../tasks/azure-deploy-001-sop.md), [User validation checklist](../tasks/azure-deploy-001-user-validation-checklist.md), [Azure pilot infrastructure](../infrastructure/azure-pilot/README.md)
- 다음 Gate: Change 002 Azure artifact·Public Deployment Security 검증 후 별도 게시 승인을 받는다. 운영 runtime은 이미 활성 상태를 유지하며 이번 artifact 작업 중 변경하지 않는다.

### TASK-CI-COST-001: GitHub Actions minute 최적화

- 상태/다음 순서: Change 001은 local 구현·자동 검증, 원격 GitHub Actions `CI Gate` required check 적용·readback, PR #96 squash merge와 main SHA `58ef76ce674b9b502fd17301b2fea740dc05bec9` CI를 완료했다. 코드 변경 PR의 Backend/Full-Stack 병렬 시작, Azure 선택 release와 최소 1주 사용량 관찰을 남긴다.
- 목적: GitHub-hosted Actions 월 사용량을 줄이면서 변경 영향에 필요한 검사는 유지하고, heavy Backend test와 Full-Stack의 직렬 대기, 같은 tree의 PR/main 중복, Azure의 미변경 image·migration 반복을 제거한다.
- Change 001 포함 범위: 공통 changed-file 분류, Backend/Frontend/고위험 Full-Stack/Workflow Validation matrix, Frontend 빠른 검증 뒤 Backend heavy test와 Full-Stack 병렬 실행, 활성 Ruleset·성공 `CI Gate`·동일 tree readback 기반 main skip, Azure 마지막 성공 main release 이후 누적 diff·변경 image 병렬 build·migration 선택 실행.
- 제외 범위: 제품/API/DB schema와 migration 내용, Azure resource 사양, self-hosted runner, 테스트 자체의 parallelization 설정, 자동 운영 배포와 branch 자동 삭제.
- 안전 경계: workflow-level `paths-ignore`를 쓰지 않는다. 분류·이력·GitHub API·Ruleset readback이 모호하면 전체 검사 또는 전체 release로 fallback한다. CI trust source 자체가 바뀐 PR은 main 중복 skip 대상에서 제외한다. Azure 최신 main SHA·명시 승인·OIDC·immutable digest·baseline·rollback·public `200/401/401` 검사를 유지한다.
- 산출물: [Identity Gate](../tasks/ci-cost-001-identity-gate.md), [Change 001](../tasks/ci-cost-001-change-001.md), [Task·SOP·User manual·검수 checklist](../tasks/ci-cost-001.md), [Implementation report](../tasks/ci-cost-001-implementation-report.md), 이 Roadmap update
- 다음 Gate: 마감 문서를 동기화한 뒤 코드 변경 PR의 Backend·Full-Stack 병렬 시작과 최소 1주 사용량 추세를 관찰한다. 별도 공개배포 승인 시 Azure 선택 release와 public security smoke를 검수한다.

### TASK-TEAMS-PWA-001: Teams 실행 화면·웹 PWA 설치 경험·브랜드 통일

- 상태/다음 순서: Change 001~003·007·009·010 원격 main 병합·Azure 운영 rollout 완료 / Change 011 사용자 검수 완료 / TASK-ADMIN-003과 통합 게시·main 병합·공개배포 승인
- 목적: Teams tab 안의 iframe 로그인 실패를 보안 경계를 약화하지 않고 해소하며 Teams·웹·설치 앱·알림·문서의 사용자 표시명을 `EMI PMS`로 통일한다.
- 구현 선택: Teams는 Activity Feed와 실행 진입점으로 사용한다. 개인 tab은 React 업무 bundle을 싣지 않는 작은 정적 launcher만 표시하고, 사용자가 누르면 Microsoft 365 인증으로 보호된 외부 웹/PWA를 새 창에서 연다. NAA·OBO·신규 token session은 추가하지 않는다.
- 보안 경계: 익명 허용 대상은 launcher HTML·작은 script·브랜드 icon과 기존 health에 한정한다. 앱 shell·업무 bundle·manifest·API는 기존 Easy Auth 사전 인증을 계속 요구한다.
- PWA 설치 경험 기준선: TASK-TEAMS-PWA-001 당시에는 offline cache·Web Push 없이 설치 경험만 제공했다. Change 009부터 모바일 자동 안내는 Microsoft 인증과 앱 shell 준비 뒤에 열리며 Android는 설치 event 준비 전에도 안내·비활성 설치 버튼을 먼저 표시하고 event 도착 뒤 같은 버튼으로 native 확인창을 연다. Change 010은 Easy Auth 보호 manifest를 credential 포함 요청으로 읽게 한다. iPhone은 기존 Safari·타 브라우저 절차를 유지한다. Web Push는 별도 `TASK-PWA-PUSH-001`에서 최소 전용 Service Worker와 기기 구독으로 확장하며 offline cache는 계속 제외한다.
- 보존 계약: Teams Activity type 10개, RSC 권한, `webApplicationInfo` Activity app identity, 수신자·발송 시점·deep link, Backend bearer·역할·프로젝트 접근은 변경하지 않는다.
- Change 007: 로그인 화면은 지정 4x 가로 logo, 로그인 뒤 모든 page의 공통 desktop sidebar·mobile app bar·mobile menu는 지정 4x 내부 logo를 사용한다. 기존 흑백 wireframe을 유지하고 logo에만 원본 색상·투명 배경 예외를 적용한다.
- Change 009: PWA event listener를 MSAL 초기화보다 앞에서 시작하되 자동 안내는 인증·업무 shell 준비 뒤에만 연다. Android 안내는 event 전에도 열고 설치 버튼을 준비 상태에 따라 비활성/활성 전환하며 iPhone 안내는 그대로 유지한다. 영구 닫기는 session 단위 닫기로 바꾼다.
- Change 010: Azure Easy Auth로 보호된 same-origin PWA manifest link에 `crossorigin="use-credentials"`를 명시하고, 이 인증 계약이 빠지면 PWA asset 검사가 실패하게 한다. 팝업 정책·디자인·iPhone 절차·Backend·DB는 변경하지 않는다.
- Change 011: 사용자 제공 새 제품 logo를 로그인 뒤 공통 desktop/mobile shell과 로그인 제품명 위치에 적용하고, 로그인 왼쪽 기존 EMI logo와 PWA·Teams icon은 유지한다. 로그인에는 정보 보안 안내, 모든 웹 shell에는 `(주) 이엠아이`·오산 주소·청주캠퍼스 주소 footer를 표시한다.
- 산출물: [Identity Gate](../tasks/teams-pwa-001-identity-gate.md), [Interview](../tasks/teams-pwa-001-interview.md), [Planning](../tasks/teams-pwa-001-planning.md), [Codex review](../tasks/teams-pwa-001-review.md), [Change 001](../tasks/teams-pwa-001-change-001.md), [Change 007](../tasks/teams-pwa-001-change-007.md), [Change 008](../tasks/teams-pwa-001-change-008.md), [Change 009](../tasks/teams-pwa-001-change-009.md), [Change 011](../tasks/teams-pwa-001-change-011.md), [Implementation report](../tasks/teams-pwa-001-implementation-report.md), [User validation checklist](../tasks/teams-pwa-001-user-validation-checklist.md), 이 Roadmap update
- 게시·운영 근거: PR #86 squash merge, merge SHA `e6a446268b0ce9aa7f9492af1e0bd4eb1a76191b`, main CI run `31360415559` `3/3`, Azure release `31361630803` 성공. 배포 뒤 health `200`, 익명 root·`/api/me` `401/401`을 확인했다.
- 다음 Gate: TASK-TEAMS-PWA-001 운영 관찰은 유지한다. Web Push는 수신 정책을 확정한 별도 `TASK-PWA-PUSH-001`의 자동 검증·사용자 검수·게시 Gate에서 진행한다.

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

### TASK-UAT-NOTIFY-ESC-001: Escalation fair-ordering controlled UAT activation

- 상태/다음 순서: 구현·자동 검증·사용자 검수 완료 / PR #35 squash merge 승인 / 다음 코드 P2 `TASK-AUTH-HARDEN-001`
- 목적: PR #34의 fair ordering·candidate failure isolation을 Persistent UAT와 latest-main Development runtime에 통제 적용한다.
- Phase A: ledger 28/29/1, Pending/Processing 0/0, active escalation 0, eligible L0/L1/L2/L3와 신규 escalation·notification·delivery 후보 0을 read-only forecast로 확인
- Phase B: escalation-only temporary evaluator poll 2회, delivery·purge·digest·migration·seed·upsert·actual provider 차단, Persistent DB/provider delta 0
- ownership 예외: backend screen session 소실 후 exact process continuity·socket·cwd alias·singleton을 재확인하고 승인된 SIGTERM 1회로 graceful 종료, 광범위 종료와 SIGKILL 0
- Phase C: Preview 5185 maintenance 격리, latest-main Development 5081/5174 복구, escalation·delivery·purge worker 각 1개와 provider configuration 복원
- 관찰: backend 단독 escalation poll 2회와 frontend 이후 poll 1회에서 candidate·failure·DB·purge·provider-call-start delta 0, actual provider call 0, worker duplicate 0
- UI: 9개 route desktop/390px read-only smoke, blank/overflow/console error 0, Processing·attempt marker 확인
- 검증 제한: live candidate 0인 no-op 적용이며 101/200/201 공정성·후보 오류 뒤 tail·동시 evaluator 중복 0은 `TASK-NOTIFY-ESC-001` isolated 증빙 재사용
- 보호: Persistent ledger 28/29/1, 핵심 aggregate·timestamp 불변, PostgreSQL restart 0, backup size/mode/checksum 불변, restore 0, Review-safe 5190/5092 유지
- runtime: Development 5174/5081 UP, Review-safe 5190/5092 UP, Preview 5185 DOWN, Candidate 보존
- 산출물: [Task 정의와 checklist](../tasks/uat-notify-esc-001.md), [Implementation report](../tasks/uat-notify-esc-001-implementation-report.md), [SOP](../tasks/uat-notify-esc-001-sop.md), [User manual](../tasks/uat-notify-esc-001-user-manual.md), 이 Roadmap update
- 사용자 검수: Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #35 병합 승인 / 미체크 항목 0
- 전달 계약: at-least-once 유지, exactly-once로 확대하지 않음
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

- 상태/No-Go 순서: PR #36 구현·자동 검증·사용자 검수·squash merge 완료 / Change 001 REDESIGN 구현·자동 검증·사용자 검수 완료 / Change 001 merge 승인 / Persistent UAT 미적용
- 목적: 동시에 실행되는 관리자 비활성화·역할 제거·삭제 요청에서도 마지막 canonical active System Administrator 보호를 PostgreSQL transaction에서 보장한다.
- canonical predicate: active EntraId 사용자이며 삭제 요청·예약·purge 보류가 없고 canonical `system-administrator` role assignment가 존재한다. Dev persona와 승인 대기 사용자는 제외한다.
- 구현: target user row 다음 canonical role row를 `FOR UPDATE`로 잠그고, 같은 transaction에서 target 상태와 다른 canonical active administrator 수를 재계산한다. 감소 가능한 모든 지원 경로가 같은 guard를 사용한다.
- 적용 경로: 사용자 비활성화, 역할 전체 교체에 따른 System Administrator 제거, 삭제 예약과 bulk delete 재사용 경로는 canonical predicate를 사용한다. 즉시·background purge는 lifecycle marker를 제외하고 active Entra System Administrator 여부를 확인하는 별도 물리 삭제 방어 predicate를 사용한다.
- Change 001: 기존 purge 경로가 canonical lifecycle-null predicate와 상호 배타적이어서 방어 거부가 도달 불가능했던 `PURGE_GUARD_PREDICATE_UNREACHABLE`을 REDESIGN으로 보정했다. Malformed lifecycle state를 defense-in-depth 대상으로 유지하며, 다른 canonical administrator가 없으면 즉시 purge는 기존 HTTP 400으로 거부하고 due purge는 batch transaction 전체를 rollback한다. 다른 administrator가 있으면 기존 reference scan을 계속 적용하므로 role assignment reference는 `PurgeBlocked`가 될 수 있다.
- Change 001 검증: purge defense targeted PostgreSQL 5/5, backend 361/361, frontend 61/61·lint/typecheck/build, Mock UI 1/1, Full-Stack E2E 16/16, actionlint·문서·보안·allowlist 검증을 통과했다. Persistent read-only 전후 ledger 28/29/1, canonical active administrator 1, Pending/Processing 0/0과 runtime·PostgreSQL identity는 불변이다.
- 검증: 수정 전 isolated race에서 invariant 위반 35건을 재현했다. 수정 후 서로 다른 target 경쟁, 혼합 mutation, 동일 target, 증가/감소 경쟁, cancellation, 중간 실패와 20회 stress에서 committed active count 1 이상, partial update·unexpected deadlock 0을 확인했다.
- API/UI: 기존 HTTP 400과 `{message}` shape를 유지하며 화면 변경은 없다. Migration·schema·dependency·runtime configuration 변경도 없다.
- 운영 적용: Persistent UAT 사용자·role 데이터와 runtime은 변경하지 않았다. Merge 뒤 별도 `TASK-UAT-AUTH-HARDEN-001` controlled UAT 승인이 필요하다.
- 산출물: [Task 정의와 검수 체크리스트](../tasks/auth-harden-001.md), [Implementation report](../tasks/auth-harden-001-implementation-report.md), [SOP](../tasks/auth-harden-001-sop.md), [User manual](../tasks/auth-harden-001-user-manual.md), [Change 001](../tasks/uat-auth-harden-001-change-001.md), 이 Roadmap update
- 사용자 검수: PR #36 검수 완료 / Change 001 구현·자동 검증·사용자 검수 완료 / Change 001 merge 승인 / 미체크 항목 0
- 전체 신규 기능 개발: 기존 P2가 남아 있으므로 No-Go 유지

### TASK-UAT-AUTH-HARDEN-001: Last administrator controlled UAT

- 상태: Phase A~D 자동 검증·runtime 적용·사용자 검수 완료 / PR #40 squash merge 승인
- 완료된 선행 범위: PR #36 concurrency guard와 Change 001 purge 전용 REDESIGN이 main에 병합됐다. `PURGE_GUARD_PREDICATE_UNREACHABLE` 정책 선택은 완료됐으며 다시 Decision Pending으로 돌리지 않는다.
- Phase A/B: Collector·Aggregator·Projector와 Release build end-to-end qualification, Persistent read-only identity snapshot, synthetic PostgreSQL actual HTTP, cancellation·failure, immediate/due purge와 20회 stress를 통과했다. 대표 cross-target 결과는 성공 7·안전 거부 5·minimum final active 1이며 violation·partial update·unexpected deadlock 0이다.
- Phase C: latest-main temporary ReviewSafe backend에서 live/ready 200, DB read-only, mutation 423, worker/provider 0, read-only GET 4/4와 Persistent identity delta 0을 확인하고 정상 종료했다.
- Phase D: latest-main Development 5081/5174, escalation·delivery·purge worker 각 1과 provider 3종 configuration을 복원했다. Desktop·390px route 8/8, overflow·console error 0, provider-call-start·delivery attempt delta 0이며 Review-safe와 PostgreSQL을 보존했다.
- 최종 Persistent 상태: ledger 28/29/1, canonical active administrator 1, Pending/Processing 0/0, identity·assignment·deletion·admin log digest 불변, PostgreSQL restart 0, backup restore 0.
- 제한: Persistent live user/role/deletion mutation `NO_GO`, break-glass 복구 경로 증명 전 유일 administrator 실데이터 거부 test 금지, Direct SQL·자동 backup restore 금지
- runtime 상태: Development 5174/5081은 latest-main, Review-safe 5190/5092는 기존 read-only fallback, Preview 5185는 maintenance 격리 DOWN, 기존 Candidate는 보존했다.
- 산출물: [Task 정의와 검수 체크리스트](../tasks/uat-auth-harden-001.md), [Implementation report](../tasks/uat-auth-harden-001-implementation-report.md), [SOP](../tasks/uat-auth-harden-001-sop.md), [User manual](../tasks/uat-auth-harden-001-user-manual.md), 이 Roadmap update
- 다음 Gate: `TASK-GOV-002` read-only history risk 조사와 사용자 결정이다.

### TASK-NOTIFY-003: Teams Activity Feed 개인 알림 / 알림 운영 UX

- 상태: 완료(provider/capability 및 명시된 수동 발송 범위). 확정 자동 event 연결은 후속 TASK-NOTIFY-POLICY-001에서 완료했다.
- 목적: Teams Activity Feed actual 발송을 추가하고, 3채널 알림 운영/추적 UX를 고도화한다.
- 포함 범위: Teams Activity Feed actual provider, text topic + Teams deep link webUrl, installedAppId 운영 의존 제거, `/teams/activity` 탭, `/teams/activity/notifications/{id}` 상세, 인앱 notification 원본 구조, 개인 알림/채널 공지 접근권한, TeamsChannel/Mail/TeamsActivity 3채널 smoke, 관리자 수동 알림 발송 3모드, 업무 배정 수동 발송 시 work_item 생성, queue 방식 수동 발송, TeamsActivity/Mail 다중 수신자, display snapshot/detail, 자동/수동 알림 양식 통일, 실패/대기 확인·제외·대기 재시도, notification delivery admin handling, HTTPS local Teams test
- 제외 범위: Teams manifest/icon repo 포함, 운영 URL 확정, `projectCreated` activityType manifest 추가, 사용자별 알림 설정 UI, 실패 delivery 강제 성공 처리, delivery row hard delete, Teams DM/Bot 구현
- event coverage: 관리자 수동 개인/업무 배정은 이 Task에서 적용했고, 현재 자동 event coverage는 6.5.2.2와 TASK-NOTIFY-POLICY-001을 따른다.
- 선행조건: TASK-NOTIFY-001, TASK-NOTIFY-002, TASK-ADMIN-001, Teams 앱 승인, Graph TeamsActivity 권한 승인
- 주요 테스트: backend 전체 test, Notification/Admin targeted tests, Migration tests, frontend lint/typecheck/unit/build, mock UI smoke, Full-Stack E2E, UAT health, UAT `/teams/activity` smoke, 3채널 actual smoke, secret scan

### TASK-NOTIFY-004: 외부 알림 delivery 신뢰성 및 실패 재처리 정책

- 상태: `POLICY_CORRECTION_AND_DEFER` 승인 / 자동·독립 Codex 검증·사용자 검수 완료 / PR #44 squash merge 승인
- 완료 범위: TASK-NOTIFY-REL-001의 claim/lease·fencing·automatic retry·retryable/non-retryable 분류·attempt lineage와 TASK-NOTIFY-ESC-001의 escalation starvation 보정 및 controlled UAT
- 정책 결정: terminal `Failed`는 permanent failure 또는 retry limit 소진 후의 최종 상태로 유지한다. 현재 Backend와 UI는 Pending의 다음 시도 시각 앞당기기, Failed acknowledge/dismiss와 attempt 확인만 지원한다.
- P2 판정: Runtime 결함이 아니라 TASK-NOTIFY-REL-001 SOP가 존재하지 않는 Failed retry 절차를 가리킨 문서 drift다. `FAILED_RETRY_DOCUMENTATION_DRIFT`는 문서 정정으로 처리한다.
- 수동 재처리: Retry generation, append-only admin action, 원본·새 cycle lineage, duplicate-risk acknowledgement와 반복 제한이 필요한 별도 사용자 능력이다. 업무 필요성이 확인되면 `NEW_FEATURE`로 다시 기획한다.
- 제외 범위: Failed→Pending/replacement delivery 상태 전이, API·UI·schema·migration·runtime 변경, actual provider 호출과 Persistent UAT write
- 보장 수준: 정상 worker 경쟁은 claim/lease로 보호하지만 provider 성공 후 DB completion 전 중단은 중복 가능성이 있어 at-least-once이며 exactly-once가 아니다.
- 산출물: [Task](../tasks/notify-004.md), [Planning](../tasks/notify-004-planning.md), [Implementation report](../tasks/notify-004-implementation-report.md), [SOP](../tasks/notify-004-sop.md), [User manual](../tasks/notify-004-user-manual.md)

### TASK-UX-001: 기존 업무 화면 Action Feedback UX 확대

- 상태/권장 순서: A1·A2 `EXPERIMENT_COMPLETE / BATCHED_FINAL`; 현재 experiment에서 재구현 금지
- 목적: 저장·삭제·복구·발송 결과와 validation 오류를 사용자의 action 위치에서 즉시 이해하고 다음 행동으로 이어지게 한다.
- A1 구현 범위: 공통 `useActionFeedback`, 내 업무 완료·이동/시작, 알림 개별·전체 읽음의 구조화 loading/success/error/partial, scope 잠금, post-mutation refresh·generation guard, row/contextual placement, error/partial focus와 `aria-live`
- A2 구현 범위: 생산계획/구매/자재 도착·입고·IQC·키팅/패널/선택 Excel의 구조화 loading·success·partial·error, action scope 잠금, preserve refresh·generation guard, 편집기 복귀 후 결과 보존, field 오류 focus·`aria-describedby`·`aria-live`
- 제외 범위: 업무 규칙 변경, API 계약의 기능 확장, 알림 delivery 재처리 로직, 사용자 preference
- 선행조건: A1을 기반으로 A2 Fable 2-pass planning·구현·자동 검증을 완료했다. A1/A2는 experiment local commit이며 대표 repo·`main`·Persistent UAT에는 반영하지 않는다.
- 예상 migration: 없음 예상. API 오류 계약 보정이 필요하면 runtime 범위를 Task 안에서 별도 명시한다.
- backend/frontend 영향: Frontend 공통 hook·기존 component와 A1/A2 화면만 변경했고 Backend/API/DB/migration은 변경하지 않았다.
- 핵심 검수 기준: action 인접 loading/error, 행 제거 뒤 contextual success/partial, 첫 오류·partial focus, screen reader 안내, 중복 submit과 bulk/row 충돌 방지, 기존 목록·선택 보존, 모바일 overflow 0
- 주요 위험: 화면별 임시 구현으로 contract가 분산되는 문제, 상단 banner와 inline feedback 중복, focus 이동 회귀, A1/A2 범위 팽창
- 산출물: [A1 Fable 2차 기획](25-action-feedback-a1-plan.md), [A1 Implementation report](../tasks/ux-001-implementation-report.md), [A2 Fable 2차 기획](31-action-feedback-a2-plan.md), [A2 Implementation report](../tasks/ux-001-a2-implementation-report.md), [A2 Screenshots](../tasks/ux-001-a2-screenshots)

### TASK-NOTIFY-005: 사용자별 알림 설정

- 상태/권장 순서: `EXPERIMENT_COMPLETE / BATCHED_FINAL`; 현재 experiment에서 재구현 금지
- 목적: 사용자가 허용된 범위에서 event별 외부 채널 수신 방식을 조정하되 필수 업무 알림과 인앱 원본을 보존한다.
- 포함 범위: channel taxonomy, 사용자별 event/channel preference 저장·조회·수정, dispatcher 적용, 관리자/사용자 설정 UI, 기본값과 audit
- 제외 범위: 인앱 notification 원본 opt-out, 법적·업무상 필수 알림 해제, provider 신뢰성 재구현, 신규 외부 채널 추가
- 선행조건: TASK-NOTIFY-004 완료, 필수 알림 opt-out 금지 정책 확정, channel/event taxonomy 확정
- 구현 migration: `0041_user_notification_preferences.sql` additive profile/version·sparse opt-out·fixed-field audit 3-table. Persistent UAT 미적용.
- backend/frontend 영향: 본인/관리자 6 endpoint, expectedVersion·no-op·audit, WorkItemCreated·DailyDigest·L0 Suppressed gate, 사용자/관리자 adaptive card UI를 experiment에서 구현했다.
- 핵심 검수 기준: 필수 알림 해제 차단, 기본값 호환, event/channel별 저장과 재로그인 유지, 인앱 원본 보존, preference 변경 audit, 외부 delivery 생성 여부 검증
- 주요 위험: 필수 알림 누락, taxonomy 변경 시 기존 설정 drift, 기본값 migration 오류, 관리자 정책과 사용자 선택 충돌
- 검증: Backend 391/391, Frontend 101/101·build, migration 41개 fresh apply, 격리 desktop/390 save·reset·overflow 0, actual provider 0.
- 산출물: [Fable 2차 기획](26-notification-preferences-plan.md), [Implementation report](../tasks/notify-005-implementation-report.md), [SOP](../tasks/notify-005-sop.md), [User manual](../tasks/notify-005-user-manual.md), [Checklist/Screenshots](../tasks/notify-005.md)

### TASK-NOTIFY-POLICY-001: 알림 운영 정책 정합화

- 상태: 원격 main·Persistent migration·Azure 공개 배포·사용자 화면·실제 Web Push 기기 검수 완료
- 목적: 기존 인앱·Teams Activity·메일·PWA·Digest·에스컬레이션의 수신자와 발송 시점을 사용자 확정 정책으로 통일한다.
- 포함 범위: 일반 업무 Teams 선택, Pending 필수 3채널과 종결 snapshot, 제조 중단 단일 Pending, 프로젝트 생성·납기·상태·17단계·18단계 분리, 복수 부서장 공유·첫 처리자 동기화 종료, 패널별 업무/묶음 알림, 평일 Digest, 정확한 생산·구매 일정 기반 due_date, L0·L1, PWA 인앱 가시성 통합.
- 제외 범위: L2·L3 확대 에스컬레이션 신규 발송, 새 Teams 공용 채널 delivery, 직원 PWA 강제 등록·중앙 등록률 관리. 운영 VAPID와 실제 Web Push provider는 후속 승인으로 활성화·검수 완료했고 기존 Teams·메일 운영 provider 설정과 이력은 보존한다.
- 구현 migration: `0075_notification_policy_alignment.sql` additive source kind·fallback group·due_date backfill. L2·L3와 TeamsChannel 과거 schema·handler·이력은 보존한다.
- 핵심 검수 기준: 주요 사건별 수신자·채널 정확성, 한 Pending 원본, 부서장 전원 표시와 첫 처리자 1명, 일정 변경 시 미완료 업무만 갱신, 일괄 작업 1알림, PWA=인앱 수신자, 운영 PWA 실기기 수신·알림 상세 이동.
- 산출물: [Planning](../tasks/notify-policy-001-planning.md), [Review](../tasks/notify-policy-001-review.md), [Implementation report](../tasks/notify-policy-001-implementation-report.md), [User validation checklist](../tasks/notify-policy-001-user-validation-checklist.md).

현재 experiment Task 선택은 [실험 브랜치 Task 완료 원장](27-experiment-task-ledger.md)을 따른다. TASK-007A~014A, MOBILE-001/002, HOME-001/002, DESIGN-000/001, 현재 선택 export와 column picker, E2E 기준선, UX-001 A1/A2, NOTIFY-005, TASK-PENDING-TYPE-001, TASK-QR-001, TASK-NOTIFY-AUDIT-001과 TASK-NOTIFY-REPROCESS-001은 `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE`이므로 다시 기획·구현하지 않는다. `TASK-WORKFLOW-CONTINUITY-001` Change 017과 `TASK-ATTACHMENT-001`도 2026-07-30 사용자 검수를 완료했다. 운영 storage 용량·scanner 활성화·backup/restore rehearsal은 기능 재구현이 아니라 운영 전환 후속 범위로 남긴다. 아래 과거 실험 기록의 `BATCHED_FINAL`, 대표 repo 미반영과 “canonical TASK-007A Gate 유지” 문구는 각 시점의 역사적 snapshot이다. 2026-07-29 사용자는 당시 실험 계보의 사용자 검수 완료, 기존 데이터 초기화와 서로 분리된 `main` merge 승인 `3/3`을 확정했다. `TASK-EXPERIMENT-PROMOTION-001`은 기존 공식 UAT DB를 보존 격리하고 fresh 공식 DB에 migration `0001`~`0064`를 적용했으며, 전체 회귀와 Ready PR CI가 성공한 경우 direct push 없이 `main`에 승격한다.

### TASK-NOTIFY-AUDIT-001: 관리자 알림 설정 변경 이력

- 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- 목적: System Administrator가 기존 preference audit 원장을 기간·행동·알림 종류·사용자/부서로 조회·요약하고 선택 Excel로 보존한다.
- 구현: additive `0048`, admin-only API, shared list/summary predicate, KST 날짜 경계, 현재 계정 기준 안내, desktop table·390px card, 선택 export.
- 검증: Backend/Frontend 전체 기준선, 기능 API·Excel 테스트와 disposable Full-Stack desktop/mobile `1/1` 통과. actual provider·Persistent UAT 사용 0.
- 산출물: [Codex 2차 기획](35-notification-preference-audit-plan.md), [구현 보고서](../tasks/notify-audit-001-implementation-report.md).

### TASK-NOTIFY-REPROCESS-001: terminal Failed 수동 재처리

- 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- 목적: System Administrator가 terminal Failed만 중복 위험을 확인하고 사유를 남겨 새 generation으로 원자 재처리한다.
- 구현: additive `0049`, generation별 retry count와 전역 attempt lineage, expected-generation CAS, 최대 G5, append-only event, desktop/mobile 관리자 실행 UX와 상세 이력.
- 검증: 권한·G1→G2·stale CAS·batch 원자성·worker claim 테스트와 disposable Full-Stack `1/1` 통과. actual provider·Persistent UAT 사용 0.
- 산출물: [Codex 2차 기획](36-notification-delivery-reprocess-plan.md), [구현 보고서](../tasks/notify-reprocess-001-implementation-report.md).

### TASK-USER-FLOW-001: 웹사이트 전체 유저플로우 설계

- 상태: Interview·사용자 방향 확인·Fable redraft·Codex 내용 review·독립 재검증·CI 3/3 완료 / PR #55 squash merge 완료
- 목적: 현재 구현과 향후 Roadmap 기능을 하나의 역할별 웹사이트 흐름으로 연결해 신규 기능별 화면·내비게이션·업무 인수인계가 서로 충돌하지 않는 개인 기획 기준선을 만든다.
- 산출물 위치: `docs/13-user-flow-baseline.md`, interview·planning·review·Change 001~004·implementation report. Redraft 원문은 Fable stdout과 byte-identical하며 Codex가 수정하지 않았다.
- 최신 review 결론: 전체 흐름 지도는 개인 개발 판단 자료로 유지한다. Canonical 선언·Phase B·전수 갱신은 보류하고, `Pending → 병목 집계 → 자재 도착 → IQC → 키팅 → 제조 handoff`를 우선 검증할 제품 slice로 권고한다.
- 승인 상태: 개인 참고 문서 Fable redraft와 별도 push·PR·merge 승인 / 제품 구현·Phase B 미승인
- 독립 검증: 1차에서 Roadmap 23절 공통 서문의 상태 충돌 P2 `USER_FLOW_ROADMAP_PLANNING_PREAMBLE_STALE`를 발견해 최소 보정했고, 재검증에서 `RESOLVED`, Open P0/P1/P2/P3 `0/0/0/0`, publication `GO`를 확인했다.
- 게시 결과: Ready PR #55, Frontend·Backend·Full-Stack E2E CI `3/3` 성공, squash merge 완료. 제품 코드·runtime·DB 변경은 없다.
- 당시 대표 repo 다음 Gate는 `TASK-007A`의 별도 Fable deep-interview → planning → Codex review → 사용자 승인으로 기록됐다. 현재 experiment에서는 `TASK-007A`가 `EXPERIMENT_COMPLETE`이므로 재구현하지 않으며, 향후 대표 repo 승격은 별도 통합·UAT Task로 다룬다.

### TASK-007A: Pending List 공통 모듈

- 실험 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`. 현재 experiment 계보에서 다시 Fable planning하거나 재구현하지 않는다. binary 첨부는 별도 신규 기능 범위다.
- 목적: 부적합, PUNCH, 제조 중단, 기타 이슈를 공통 모듈로 관리
- 포함 범위: Pending 생성, 상태, 조치 담당, 코멘트, 첨부, 긴급 알림
- 제외 범위: 검사별 상세 체크리스트 전체
- 선행조건: 내 업무/알림 기반
- 주요 테스트: 생성, 조치, 재검사 요청, 권한, 중복 방지

### TASK-007B: 패널·프로젝트 병목 상태 집계

- 실험 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`. 정렬 toggle P3는 완료 범위를 다시 여는 조건이 아니다.
- 목적: Pending 차단 상태와 필수 workflow 진행률을 이용해 패널·프로젝트의 대표 병목 상태를 계산한다.
- 포함 범위: 상태 matrix, Pending 차단, 패널·프로젝트 aggregate, 기존 진행률 공식 재사용
- 제외 범위: HOME widget과 관리자용 Pending 유형 편집
- 선행조건: TASK-007A 안정화, 상태 matrix 사용자 승인
- 주요 테스트: 필수 단계 partial/all, open Pending 차단, FAT optional 분모, aggregate 권한

### TASK-MOBILE-001: 동일 URL 적응형 현장 UX

- 실험 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`. 후속 MOBILE-002 Change 005까지 좌상단 drawer·PC 관리 기능의 기본 화면 제외·핵심 판단 우선 배치와 의미 기반 모바일 shape 통일을 반영했다.
- 목적: 기존 URL과 인증 흐름을 유지하면서 모바일 내비게이션과 현장 입력·사진 업로드 기반을 제공한다.
- 포함 범위: responsive navigation, 390px/Teams narrow, 사진 압축·재시도, 접근성·overflow 기준
- 제외 범위: 공용 태블릿·공용 기기 mode, 별도 session 정책, sessionStorage 강제 정책
- 선행조건: TASK-007A·007B, 첨부 storage·보안·backup 정책
- 주요 테스트: desktop/390px/Teams narrow, 업로드 실패·재시도, 권한, page-level overflow 0

### TASK-HOME-001: PC·모바일 Home MVP

- 실험 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`. query/cache P3 최적화는 실측 문제가 있을 때만 별도 change로 연다.
- 목적: 현재 데이터로 제공 가능한 요약을 widget-slot 구조로 단계적으로 활성화한다.
- 포함 범위: PC·모바일 Home, widget slot, TASK-007B aggregate 재사용, loading·empty·error 상태
- 제외 범위: 아직 source data가 없는 예측 widget과 시각 브랜드 전면 개편
- 선행조건: TASK-007B, TASK-MOBILE-001 기반
- 주요 테스트: widget 권한, empty/error, responsive layout, aggregate 정합성

### TASK-HOME-002: 개인화 Home·프로필 shell

- 실험 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`. 실제 사용자 프로필과 effective 사용자 부서 Home을 분리했고 Change 002의 전 부서 운영 메뉴 조회·compact reference design까지 완료했으며 사용자 직접 검수는 마지막 일괄 대기다.
- 목적: 모든 업무 페이지에서 로그인 사용자 사진·부서·이름을 확인하고, Home에서 부서별 핵심 지표를 최대 3개 우선 확인한다.
- 포함 범위: actual-user profile popover/sheet와 본인 사진 upload/remove, full-height desktop sidebar, drawer 하단 개발·검수 전환, 중복 자재 shortcut 제거, 9개 부서 aggregate, 375px 모바일 재구성, 운영 메뉴 11개 전 부서 조회와 담당별 mutation gate, 참고 이미지 기반 compact white workspace
- 제외 범위: 대표 repo·main·Persistent UAT 적용, actual provider, 조직 directory 사진 sync, 범용 업무 attachment storage, 기존 업무 페이지 전면 재설계
- migration: additive `0042_user_profile_photos`; 사용자당 1 current row와 fixed-field append-only audit
- 주요 테스트: Backend `395/395`, Frontend `103/103`, mock UI E2E `2/2`, 직전 Full-Stack `38/38`, fresh-schema 사진 lifecycle·9부서 SQL, 본체·Change 002 desktop/mobile synthetic browser 증빙. Change 002 Full-Stack 재실행은 container policy로 promotion 전 P3.

### TASK-NOTICE-BOARD-001: Home 공지사항 게시판

- 실험 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`. 사용자 직접 검수는 마지막 일괄 대기다.
- 목적: Home 상단 부서 KPI와 중앙 업무 요약은 유지하고, 하단 프로젝트 병목 widget을 모든 승인된 active 사용자가 작성·조회하는 공지사항 게시판으로 교체한다.
- 포함 범위: 공지 persistence·Backend 권한/validation API, 최신 공지 Home widget, 게시판 목록·상세·작성 UX, 작성자·부서·시각 표시, desktop·390px 검증.
- 제외 범위: 프로젝트 목록·상세 병목 계산 변경, 공지 작성에 따른 내 업무·인앱 unread·Teams·메일 자동 발송, 첨부·댓글·반응, 대표 repo·main·Persistent UAT·실제 provider.
- 선행조건: TASK-HOME-001/002, DESIGN-000과 active 사용자 identity 계약.
- 주요 테스트: Backend 전체 `418/418`, Frontend 전체 `119/119`, migration·Notice API 표적 `36/36`, isolated Full-Stack `1/1`, desktop/390px screenshot 7개와 horizontal overflow 0. 구현 보고서는 `tasks/notice-board-001-implementation-report.md`다.

### TASK-008A: 자재 도착 / IQC 요청 / 입고 확정

- 목적: 구매품목 도착부터 입고 확정까지 자재 흐름 구현
- 포함 범위: 자재 도착, 분할 입고 일반화, IQC 요청, 입고 확정, 구매품목 상태
- 제외 범위: IQC 상세 성적서 전체
- 선행조건: 구매정보, Pending List 기반
- 주요 테스트: 도착·분할 입고, IQC 요청, 부적합 차단, 입고 확정
- 변경 경계: 기존 데이터 migration 필요성과 rollback은 planning에서 확인하고 별도 승인한다.
- 2026-07-17 실험 상태: `experiment/task-008a-material-receiving`에서 Fable 1차 기획, Codex review, review 기반 Fable 2차 기획과 local 구현·자동 검증을 완료했다. 사용자 검수는 `BATCHED_FINAL`로 마지막 일괄 대기다. `0030`은 isolated DB에서만 검증했으며 대표 repo·`main`·Persistent UAT·provider에는 반영하지 않았다. 현재 experiment에서 재구현하지 않는다.

### TASK-008B: 사급 자재 추적

- 목적: TASK-008A의 입고 데이터 모델을 재사용해 사급 자재의 제공·입고·잔량을 추적한다.
- 포함 범위: 사급 구분, 수량·입고 이력, 프로젝트·구매품목 연결
- 제외 범위: TASK-008A 데이터 모델 재구현과 외부 공급망 연동
- 선행조건: TASK-008A 구현·검수 완료, 사급 업무 정책 확정
- 주요 테스트: 분할 입고, 잔량, 권한, 중복·수량 무결성
- 2026-07-17 실험 상태: `experiment/task-008b-customer-supplied-materials`에서 Fable 1차 기획, Codex review, review 기반 Fable 2차 기획과 local 구현·자동 검증을 완료했다. 사용자 검수는 `BATCHED_FINAL`로 마지막 일괄 대기다. `0031`은 isolated DB에서만 검증했으며 대표 repo·`main`·Persistent UAT·provider에는 반영하지 않았다. 현재 experiment에서 재구현하지 않는다.

### TASK-009A: 검사 체크리스트 템플릿 / IQC 디지털 성적서 / PDF 출력 기반

- 목적: 검사성적서 디지털화 시작
- 포함 범위: IQC 체크리스트, 사진 필수, 결과, PDF snapshot 기반
- 제외 범위: LQC/OQC/FAT 전체
- 선행조건: 첨부파일 정책, Pending List
- 주요 테스트: 필수 사진, 값 입력, PDF 생성, 부적합 Pending
- 2026-07-17 실험 상태: `experiment/task-009a-iqc-digital-report`에서 Fable 1차 기획, Codex review, review 기반 Fable 2차 기획과 local 구현·자동 검증을 완료했다. 사용자 검수는 `BATCHED_FINAL`로 마지막 일괄 대기다. `0032`는 isolated DB에서만 검증했으며 대표 repo·`main`·Persistent UAT·provider에는 반영하지 않았다. 현재 experiment에서 재구현하지 않는다.

### TASK-010A: 선택형 키팅 완료 알림 / 생산관리 제조 투입 요청

- 목적: 키팅 여부와 무관하게 생산관리의 명시적인 투입 판단으로 패널 제조 업무를 시작하고, 키팅은 선택형 준비 정보로 유지
- 포함 범위: 선택형 키팅 완료 알림, 부분/일괄 처리, 생산관리의 패널별 제조 투입 요청, 제조 정·부 담당자 내 업무·인앱 알림
- 제외 범위: 제조 작업 체크리스트
- 선행조건: 생산계획·패널 정보·제조 정/부 담당자 지정
- 주요 테스트: 키팅 전/후/미실시 투입, 제조 업무·알림 생성, 재시도 중복 방지, 권한
- 2026-07-17 실험 상태: `experiment/task-010a-panel-kitting`에서 Fable 1차 기획, Codex review, review 기반 Fable 2차 기획과 local 구현·자동 검증을 완료했다. 당시 미실행 Full-Stack은 후속 전체 suite의 panel-kitting 포함 `35/35`로 보완했고 사용자 검수는 `BATCHED_FINAL`로 마지막 일괄 대기다. `0033`은 isolated DB에서만 검증했으며 대표 repo·`main`·Persistent UAT·provider에는 반영하지 않았다. 현재 experiment에서 재구현하지 않는다.
- 2026-07-17 실험 Change 002: 전역 `키팅` 메뉴를 제거하고 전역 `자재` 메뉴를 입고·키팅의 공통 진입점으로 유지한다. 입고 화면 내부 `패널 키팅` action과 내 업무 deep link로 키팅 화면에 진입하며, 키팅 화면에서도 전역 `자재`가 active다.
- 2026-07-21 실험 Change 003: 키팅 완료를 제조 시작 필수조건에서 선택형 준비 알림으로 변경했다. 생산관리 담당자가 키팅·입고 현황을 참고해 패널별 `제조 투입 요청`을 실행하면 제조 정·부 담당자의 내 업무와 인앱 알림이 원자적으로 생성되고, 제조는 키팅 미보고 상태에서도 요청된 업무를 시작할 수 있다. 계획일 기반 자동 요청과 외부 채널 알림은 포함하지 않는다.
- 2026-07-21 실험 Change 004: 전역 생산관리 화면을 `생산계획`과 `제조 투입` 두 탭으로 분리했다. 생산계획에는 KPI·일정·담당자·Excel을, 제조 투입에는 프로젝트별 패널 선택·키팅/입고 참고·투입 요청을 배치하고 Desktop 표와 Mobile 카드에서 같은 업무 경계를 유지한다. Backend·DB·알림 정책은 Change 003을 그대로 사용한다.

### TASK-011A: 제조 체크리스트 / 작업 시작·종료 / 제조 중단

- 목적: 제조현황 디지털화
- 포함 범위: 제조 단계, 작업 시작/종료, 제조 중단, Pending 연결
- 제외 범위: 품질 검사 상세
- 선행조건: 제조 단계 목록 확정
- 주요 테스트: 모바일 입력, 중단 등록, 권한, 이력
- 2026-07-17 실험 재정렬 승인: 사용자의 experiment fast-track standing rule과 “다음 작업 시작” 요청에 따라 canonical 다음 `TASK-007A` Gate와 무관하게 현재 실험 계보에서 `TASK-011A` 기획·구현을 진행한다. 상세 제조 표시·입력 항목과 LQC 기준은 Fable 권장안의 최소 MVP·Deferred로 분리하며 canonical queue, 대표 repo·`main`·Persistent UAT·provider는 변경하지 않는다.
- 2026-07-17 실험 상태: `experiment/task-011a-manufacturing-work`에서 Fable 1차 기획, Codex review, review 기반 Fable 2차 기획과 local 구현·자동 검증을 완료했다. 사용자 검수는 `BATCHED_FINAL`로 마지막 일괄 대기다. `0034`는 isolated PostgreSQL과 disposable Full-Stack E2E DB에서만 검증했고, 시작·4단계·중단 Pending·재개·panel LQC handoff를 완료했다. 대표 repo·`main`·Persistent UAT·provider에는 반영하지 않았으며 현재 experiment에서 재구현하지 않는다.
- 2026-07-21 실험 Change 002: `MANUFACTURING-RAPID-STAGE-SAVE-LOSS` P2를 해소했다. React render 전 ref fence로 제조 mutation을 직렬화하고 저장·refresh 중 action·project·panel 선택을 잠그며 안내를 노출했다. 동일 tick 3회 click에 POST 1건, 이어진 4/4·Pending·LQC isolated E2E를 통과했고 대표 repo·main은 미반영이다.

### TASK-MANUFACTURING-BATCH-001: 선택 패널 제조 단계 일괄 완료

- 목적: 같은 프로젝트의 여러 패널에서 실제로 함께 끝낸 제조 단계 한 건을 패널마다 반복 입력하는 현장 부담을 줄인다.
- 포함 범위: 기존 선택 Excel checkbox 공유, 양식의 모든 제조 단계 중 한 단계 선택, 선택 단계 한 건만 일괄 확인, 다른 제조 단계 상태 보존, 전 대상 원자 처리, replay·audit correlation, Desktop·Mobile 확인 sheet, 전체 흐름 네 표시명 단순화
- 제외 범위: 자동 제조 시작, 제조 전체 완료·LQC/OQC 일괄 인계, 작업시간 소급, 완료 정정, 품질·물류 batch
- 선행조건: TASK-011A 제조 execution·immutable template snapshot, TASK-EXPORT-001 선택 상태
- 주요 테스트: 임의 단계 선택, 순번·이름 일치, 권한, mixed invalid rollback, replay, event/version, 앞뒤 단계 미완료, 후속 패널별 LQC 인계
- 2026-07-24 실험 상태: Fable 1차 기획, Codex review, review 기반 Fable 2차 기획과 local 구현을 완료했다. `0056`은 batch receipt와 제조 event correlation을 additive로 추가한다. 사용자 Change 002에서 Claude/Fable 없이 “조립까지 선행 단계 누적 완료”를 “대상 한 단계만 완료”로 정정해 중간 제조 단계를 보존했다.
- 2026-07-28 실험 Change 003: 사용자가 Change 002의 “한 단계”를 조립 전용으로 좁힌 해석을 다시 정정했다. 제조 양식의 `일반/조립` 사용자 구분을 제거하고, 선택 패널의 모든 제조 단계 중 원하는 단계 한 건을 순번·표시명으로 선택해 원자적으로 완료하도록 바꿨다. 과거 DB role·receipt 이름은 snapshot 호환을 위해 유지하되 판정에는 사용하지 않는다. Backend 전체 `427/427`, Frontend 전체 `140/140`, isolated Full-Stack `1/1`, desktop·390px 증빙과 open P0/P1/P2 `0/0/0`을 확인했다. Change 003은 누적 checkpoint `e6f3fa6`에 포함됐고 대표 repo·`main`·Persistent UAT·실제 provider에는 아직 반영하지 않았다.

### TASK-012A: LQC / OQC / 전진검수 / FAT

- 목적: 후속 품질 검사 단계 구현
- 포함 범위: LQC, OQC, 전진검수, FAT 선택, PUNCH LIST
- 제외 범위: 물류 상세
- 선행조건: 검사성적서 양식, 사진 필수 위치 회신
- 주요 테스트: 검사 결과, PUNCH, 재검사, FAT optional 처리
- 2026-07-17 실험 재정렬 승인: 사용자의 experiment fast-track standing rule과 “다음 작업 시작” 요청에 따라 canonical 다음 `TASK-007A` Gate와 무관하게 현재 실험 계보에서 `TASK-012A` 기획·구현을 진행한다. 실제 고객 양식·필수 사진 위치·template 관리의 미확정 정책은 일반 v1 seed·optional 사진·후속 Task로 경계를 두며 canonical queue, 대표 repo·`main`·Persistent UAT·provider는 변경하지 않는다.
- 2026-07-17 실험 상태: `experiment/task-012a-quality-inspections`에서 Fable 1차 기획, Codex review, review 기반 Fable 2차 기획과 local 구현·자동 검증을 완료했다. 사용자 검수는 `BATCHED_FINAL`로 마지막 일괄 대기다. `0035`는 isolated PostgreSQL과 disposable Full-Stack E2E DB에서만 검증했고, panel LQC → 제조완료확인 → OQC → 고객검수 → 선택 FAT/포장 skeleton, 사진·불변 성적서·PDF, 불합격/PUNCH Pending·재검사 계약을 구현했다. 대표 repo·`main`·Persistent UAT·provider에는 반영하지 않았으며 현재 experiment에서 재구현하지 않는다.
- 2026-08-05 Change 004 운영 결정: 전역 LQC 스위치는 두지 않는다. `양식 관리 > LQC 검사`에서 Item별 운영 상태와 검사 항목을 관리하고, 프로젝트 생성 시 상태·양식 version을 불변 snapshot으로 고정한다. 기존 프로젝트는 기존 상태와 공통 양식을 유지한다. 운영 중지로 생성된 프로젝트만 새 LQC 업무·알림·필수 담당자·진행률에서 LQC를 제외하고 제조 완료 후 OQC로 직접 인계하며, 확정 성적서·사진·PDF·Pending·재검사 이력은 보존한다.

### TASK-ADMIN-002: 검사·제조 Template 관리

- 목적: 시스템 관리자와 지정된 부서장이 code 수정 없이 자기 부서 검사·제조 양식을 version으로 관리한다.
- 포함 범위: 고정 6종 catalog, Draft 생성·항목 편집·활성화·보관, 선택 Excel, 부서장 지정, 제조 활성 version snapshot
- 제외 범위: 임의 신규 양식 종류·workflow 단계 생성, 실제 회사 양식 content 확정, PDF layout builder, 과거 실행 snapshot 변경
- 선행조건: TASK-009A·011A·012A의 experiment data model
- 주요 테스트: 관리자/부서장/일반 사용자 권한, 부서 이동 scope, version lifecycle, 사용 중 template 보호, 제조 snapshot, audit
- 2026-07-19 실험 재정렬 승인: 사용자가 영업 KPI와 함께 `ADMIN-002` 구현을 명시해 기존 Deferred 순서를 override했다. 대표 repo·`main`·Persistent UAT·provider·게시 경계는 변경하지 않았다.
- 2026-07-19 실험 상태: Fable 1차 기획, Codex review, review 기반 Fable 2차 기획과 local 구현·필수 자동 검증을 완료했다. additive `0044`는 isolated PostgreSQL과 disposable Full-Stack E2E DB에서만 검증했고 고정 6종 catalog, Draft→Active→Archived 불변 lifecycle, 시스템 관리자 지정 부서장, current-department fence, 제조 시작 version lock·snapshot과 선택 Excel을 구현했다. 사용자 검수는 `BATCHED_FINAL`이며 실제 운영 양식 세부 항목은 후속 content change다. 현재 experiment에서 관리 기능을 다시 기획·구현하지 않는다.

### TASK-PENDING-TYPE-001: Pending 유형 관리

- 상태/권장 순서: `NEW_FEATURE / Experiment Complete / BATCHED_FINAL`
- 목적: 자동 workflow semantic과 사용자-facing Pending 유형 catalog를 분리해 관리자가 code 수정 없이 안전한 표시·사용 정책을 관리한다.
- 포함 후보: system semantic 보호, 유형 catalog lifecycle·정렬·label·수동 등록 노출, 권한·CAS·audit, Pending 생성/filter/detail/export 연동, desktop/mobile 관리 화면.
- 제외 범위: TASK-007A 상태·담당·코멘트·재검사·종결 재구현, 전체 role editor, 첨부 storage, 실제 provider, 대표 repo·main·Persistent UAT·게시.
- 선행조건: TASK-007A experiment 완료, Task identity `PASS_CREATE`, 사용자 experiment standing instruction.
- 핵심 검수 기준: 자동 유형 파괴 차단, 과거 의미 보존, 권한·scope drift·stale write 차단, 단일 label source, desktop/390px overflow 0.
- 2026-07-19 실험 상태: Fable 1차 기획, Codex review, review 기반 Fable 2차 기획과 local 구현·자동 검증을 완료했다. additive `0045`는 isolated fresh PostgreSQL과 기존 `0044 → 0045` upgrade에서 검증했고 system semantic 4종 보호, server-generated custom code, system administrator 전용 권한, CAS·append-only audit·원자 reorder, Pending 목록·상세·filter·manual option·선택 Excel의 단일 catalog label을 구현했다. Desktop 관리 표와 390px 조회 전용 카드 증빙을 완료했으며 사용자 검수는 `BATCHED_FINAL`이다. 대표 repo·`main`·Persistent UAT·provider·push·PR·merge에는 반영하지 않았고 현재 experiment에서 재구현하지 않는다.
- P3 backlog: catalog 설정 자체의 Excel export. 업무 Pending 선택 Excel의 유형 label 연동은 완료했으며 별도 사용자 요청 전에는 현재 Task를 다시 열지 않는다.

### TASK-013A: 물류 포장 / 출발 / 납품 완료

- 목적: 포장부터 납품 완료까지 물류 흐름 구현
- 포함 범위: Packing Unit, 포장사진, 상차사진, 거래명세서 서명본
- 제외 범위: 영업 정산
- 선행조건: 품질 완료 기준
- 주요 테스트: 포장 구성, 사진 필수, 출발, 납품 완료
- 2026-07-17 실험 재정렬 승인: 사용자의 experiment fast-track standing rule과 “다음 작업 시작” 요청에 따라 canonical 다음 `TASK-007A` Gate와 무관하게 현재 실험 계보에서 `TASK-013A` 기획·구현을 진행한다. 포장 구성 상세·서명본 형식의 미확정 정책은 Fable의 최소 MVP 권장안과 Deferred 경계로 분리하며 canonical queue, 대표 repo·`main`·Persistent UAT·provider는 변경하지 않는다.
- 2026-07-18 실험 상태: `experiment/task-013a-logistics`에서 Fable 1차 기획, Codex review, review 기반 Fable 2차 기획과 local 구현·자동 검증을 완료했다. 사용자 검수는 `BATCHED_FINAL`로 마지막 일괄 대기다. additive `0036`은 isolated PostgreSQL과 disposable Full-Stack E2E DB에서만 검증했고, 포장 단위 → 출발 묶음 → 납품 묶음의 필수 증빙·원자 확정·영업 정산 skeleton 인계와 모바일 물류 workspace를 구현했다. 대표 repo·`main`·Persistent UAT·provider에는 반영하지 않았으며 현재 experiment에서 재구현하지 않는다.

### TASK-014A: 영업 정산 / 세금계산서 / 프로젝트 완료

- 목적: 납품 후 영업 정산과 최종 프로젝트 완료 처리
- 포함 범위: 세금계산서 발행 체크, 완료 조건, 프로젝트 완료
- 제외 범위: 외부 회계 연동
- 선행조건: 납품 완료
- 주요 테스트: 완료 조건, 미납품 차단, 권한, 이력
- 2026-07-18 실험 재정렬 승인: 사용자의 experiment fast-track standing rule과 `TASK-013A` 완료 뒤 “다음작업 시작” 요청에 따라 canonical 다음 `TASK-007A` Gate와 무관하게 현재 실험 계보에서 `TASK-014A` 기획·구현을 진행한다. 세금계산서 최소 입력·정산 권한·완료 뒤 정정의 미확정 정책은 Fable의 최소 MVP 권장안과 Deferred 경계로 분리하며 canonical queue, 대표 repo·`main`·Persistent UAT·provider는 변경하지 않는다.
- 2026-07-18 실험 상태: `experiment/task-014a-sales-settlement`에서 Fable 1차 기획, Codex review, review 기반 Fable 2차 기획과 local 구현·자동 검증을 완료했다. 사용자 검수는 `BATCHED_FINAL`로 마지막 일괄 대기다. additive `0037`은 isolated PostgreSQL과 disposable Full-Stack E2E DB에서만 검증했고, project-context 세금계산서 draft → 모든 active panel 납품·open Pending 0건 재검증 → 정산·내 업무·workflow·project·audit·인앱 알림 원자 완료와 완료 후 lifecycle fence를 구현했다. 대표 repo·`main`·Persistent UAT·provider에는 반영하지 않았으며 현재 experiment에서 재구현하지 않는다.

### TASK-SALES-KPI-001: 영업 연간 매출·목표 KPI

- 목적: 영업 사용자가 Home과 전용 `영업` 화면에서 12개월 확정 매출·목표와 금액 KPI를 한눈에 판단한다.
- 포함 범위: 연도·통화 선택, 월별 확정 매출·목표 grouped bar와 경과 월 달성률 선·100% 기준선, 연간/당월/목표/달성률/잔여·초과 KPI, 월별 근거, 별도 파이프라인, 관리자 목표 CAS
- 제외 범위: 외부 회계 연동, 예측 매출의 실적 포함, 환율 환산, 기존 정산 workflow 변경
- 선행조건: TASK-014A 확정 정산 데이터와 TASK-HOME-002 개인화 Home
- 주요 테스트: 12개월 집계, 미등록 목표, project scope, 목표 권한·CAS·audit, desktop·390px, Home/영업 숫자 일치
- 2026-07-19 실험 재정렬 승인: 사용자가 영업 menu와 영업 Home을 같은 연간 graph로 바꾸도록 명시해 current experiment에서 fast-track을 승인했다. canonical queue와 대표 repo·`main`·Persistent UAT·provider·게시 경계는 변경하지 않았다.
- 2026-07-19 실험 상태: Fable 1차 기획, Codex review, review 기반 Fable 2차 기획과 local 구현·필수 자동 검증을 완료했다. additive `0043`은 isolated PostgreSQL과 disposable Full-Stack E2E DB에서만 검증했고 발행일·금액이 확정된 세금계산서만 실적으로 집계하며 예상 파이프라인은 달성률에서 분리했다. Desktop·390px 영업 Home/전용 화면 증빙을 완료했고 사용자 검수는 `BATCHED_FINAL`이다. 현재 experiment에서 재구현하지 않는다.
- 2026-07-19 Change 002: 공식 Power BI·Tableau·Salesforce target dashboard 사례를 benchmark해 의미가 약한 목표 금액선을 제거했다. 실제·목표는 같은 금액 축의 막대로 직접 비교하고, 경과 월 달성률만 선으로 연결하며 100% 기준선을 추가했다. 미래 월은 0%로 오해되지 않도록 선에서 제외했고 mobile도 4×3 block 대신 실제 12개월 SVG graph를 사용한다. forecast·전년 비교는 권위 있는 데이터 계약이 없어 Deferred한다.

### TASK-EXPORT-001: 모든 페이지 Excel 출력 공통 기능

- 목적: 조회 화면별 Excel export를 공통 구조로 제공
- 포함 범위: 현재 필터 반영, 컬럼 선택, 권한, audit
- 제외 범위: 복잡한 보고서 PDF
- 선행조건: 주요 화면 데이터 모델 안정화
- 주요 테스트: 권한, 필터, 파일 타입, 개인정보 노출 방지
- 2026-07-18 실험 재정렬 승인: 사용자의 experiment fast-track standing rule과 `TASK-014A` 완료 뒤 “다음작업 시작하라”는 요청에 따라 canonical 다음 `TASK-007A` Gate 및 `Deferred` 상태와 무관하게 현재 실험 계보에서 `TASK-EXPORT-001` 기획·구현을 진행한다. 모든 화면을 한 번에 완료로 가장하지 않고 Fable이 권장하는 서로 다른 우선 화면 vertical slice로 공통 export 구조를 증명하며, 대상 화면·컬럼 선택·row 제한·audit의 미확정 정책은 권장안과 Deferred 경계로 분리한다. canonical queue, 대표 repo·`main`·Persistent UAT·provider는 변경하지 않는다.
- 2026-07-18 Phase 1 당시 상태: `experiment/task-export-001-excel-export`에서 Fable 1차 기획, Codex review, review 기반 Fable 2차 기획과 3개 화면 vertical slice 구현·자동 검증을 완료했다. 프로젝트 목록·구매 dashboard·내 업무에 server-side `.xlsx`, 동일 filter/scope query, 매출 권한 column omission, 10,000행 cap, formula-safe text, 2-slot resource fence와 append-only `0038_data_export_events`를 적용했다. 당시 남은 다른 조회 화면은 아래 Change 002에서 20개 화면 선택 export로 완료했고 column picker는 Change 003에서 완료했다. 대표 repo·`main`·Persistent UAT·provider는 미반영이다.
- 2026-07-18 Change 002 실험 상태: `experiment/task-export-001-all-pages-selected-export`에서 업무 12개·관리자 8개 총 20개 조회 화면을 공통 선택 export registry로 고정했다. 모든 대상 화면은 row/card checkbox, 현재 목록 `전체선택` checkbox와 `선택 Excel 내보내기` action 하나만 사용하고 기존 전체 export UI와 중복 전체 선택 button은 제거했다. 공통 POST endpoint가 최대 1,000개 선택 ID의 권한·scope·현재 존재 여부를 전부 재검증하며 additive `0040` audit kind, formula-safe workbook, desktop 20·390px 20 screenshot, Backend 388·Frontend 92 test와 isolated Full-Stack E2E를 완료했다. 사용자 검수는 `BATCHED_FINAL`이고 현재 선택 export는 재구현하지 않는다. 대표 repo·`main`·push·PR·merge·Persistent UAT·provider는 미반영이다.
- 2026-07-19 Change 003 실험 상태: 사용자의 “다음 작업” 요청과 완료 원장의 named optional scope에 따라 canonical `TASK-EXPORT-001 change-003`을 재사용했다. Fable 1차 기획, Codex review, review 기반 Fable 2차 기획 뒤 20개 desktop 화면에 server-issued column picker를 적용했다. metadata·POST 검증·workbook·프로젝트 민감 매출 audit는 하나의 effective column source를 사용하고, 화면별 필수 식별 컬럼·ASCII key·중복·권한 밖·stale 선택을 fail-closed로 검증한다. mobile simple-mode와 form template custom export는 제외 상태를 유지했다. Backend `401/401`, Frontend `109/109`, disposable Full-Stack E2E `1/1`, desktop picker 20개·mobile 1개·실제 Excel 2개 증빙과 Open P0/P1/P2 `0/0/0`을 완료했다. solution 전체 format drift는 범위 밖 P3 housekeeping backlog로 분리했고 이번 변경 DataExports 검증은 통과했다. 사용자 검수는 `BATCHED_FINAL`이며 대표 repo·`main`·push·PR·merge·Persistent UAT·provider는 미반영이다.

### TASK-EXPORT-002: 선택 프로젝트 Excel 내보내기

- 목적: 프로젝트 목록에서 사용자가 여러 프로젝트를 명시적으로 선택하고 선택한 subset만 단일 Excel 파일로 내려받는다.
- 포함 범위: desktop·390px selection UX, 선택 집합 permission·scope 재검증, 기존 safe workbook·매출 column gate·resource fence·audit 재사용, 페이지·Excel screenshot.
- 제외 범위: 다른 화면의 다중 선택, 전체 filter 결과의 전 page 대량 선택, 복합 multi-sheet 보고서, column picker.
- 선행조건: `TASK-EXPORT-001` Phase 1 공통 export 기반.
- 주요 테스트: 선택 0/복수/전체, 중복·상한, scope 밖·stale 전체 차단, 선택 row만 포함, 권한별 컬럼, desktop·390px.
- 2026-07-18 실험 재정렬 승인: 사용자의 명시적 요청과 standing experiment fast-track에 따라 canonical 다음 `TASK-007A` Gate와 무관하게 `experiment/task-export-002-selected-project-export`에서 인터뷰 없이 Fable 1차 기획 → Codex review → Fable 2차 기획 → 구현·검증·screenshot·local commit을 진행한다. 대표 repo·`main`·push·PR·merge·Persistent UAT·provider와 canonical queue는 변경하지 않는다.
- 2026-07-18 실험 상태: 프로젝트 목록 desktop 행·mobile 카드에서 현재 표시된 프로젝트를 최대 100건까지 선택하고 기존 프로젝트 workbook 형식으로 내보내는 기능을 구현했다. `POST /api/projects/export/selected`는 권한·scope·soft-delete를 한 번에 재검증하고 요청 수와 조회 수가 다르면 generic 422와 file/audit 0건으로 차단한다. additive migration `0039`로 `ProjectsSelected` audit kind를 추가했으며 desktop·390px·실제 Excel screenshot, Backend 385 tests, Frontend 90 tests와 관련 isolated Full-Stack E2E를 완료했다. 사용자 screenshot·파일 검수 대기이며 대표 repo·`main`·push·PR·merge·Persistent UAT·provider와 canonical `TASK-007A` Gate는 변경하지 않는다.
- 2026-07-18 후속 관계: 이 Task의 프로젝트 vertical slice는 유지하고, 당시 제외했던 다른 조회 화면의 다중 선택은 `TASK-EXPORT-001 Change 002` 공통 20개 화면 registry로 확장 완료했다. 복합 multi-sheet 보고서와 column picker는 계속 제외한다.

### TASK-E2E-FULL-SUITE-001: 실험 계보 전체 Full-Stack 회귀 안정화

- 목적: `TASK-EXPORT-002`에서 `BACKLOG`로 남긴 `FULL-STACK-BASELINE-UNRELATED-FAILURES`를 현재 experiment HEAD의 제품 계약에 맞춰 해소한다.
- 포함 범위: Home·Pending·IQC·mobile navigation·kitting·project bottleneck·project registration·selected export fixture/selector와 프로젝트 목록 중복 전체선택 UI.
- 제외 범위: Backend 제품 계약·API·DB·migration·dependency, 대표 repo·`main`, Persistent UAT, 실제 provider, push·PR·merge.
- 2026-07-18 실험 상태: 현재 HEAD에서 전체 `25/35`와 10개 실패를 재현하고 최신 Pending 부서·구매 수량·통합 IQC·디지털 성적서·audit 증가분 계약으로 보정했다. 프로젝트 desktop header의 중복 전체선택을 제거해 선택 tray 한 개만 남겼다. Backend Release build와 `388/388`, Frontend lint(error 0)·typecheck·`92/92`·build, disposable PostgreSQL Full-Stack E2E `35/35`와 cleanup을 완료했다. 사용자 검수는 `BATCHED_FINAL`이고 현재 회귀 기준선은 완료다. 대표 repo·`main`·push·PR·merge·Persistent UAT·provider는 미반영이다.
- 2026-07-19 Change 002 실험 상태: 사용자 확정 정책에 따라 생성 알림을 관리자·조회전용 제외 운영 전 사용자로 확대하고, 생산관리 담당 지정 이후 정담당자 내 업무 시작, 단계별 정·부 알림, 납품 후 영업 알림, Pending 자동 담당·내 업무·TeamsChannel/Mail outbox, 자재 연속 흐름과 workflow 완료 정합성을 구현했다. 실제 역할 UI 입력으로 생성→회계 발행요청 Excel→최종 `18/18` 완료 `1/1`, Backend `403/403`, Frontend `109/109`, build/typecheck를 통과했다. 실제 provider·Persistent UAT·대표 repo·`main`은 미반영이다.
- 2026-07-20 Change 003 실험 상태: 프로젝트 상세의 영업·자재·제조·품질·물류 탭을 workflow-only 요약에서 부서 실데이터와 담당자 수정 진입으로 전환했다. 알림은 프로젝트 단위 읽음·최근 3건 우선 접기·열람 자동 읽음을 추가했고, 생산계획 수정은 초기 응답 완료 전 입력 잠금과 stale response 폐기를 적용했다. Frontend `109/109`, Backend 알림 통합 test `1/1`, isolated 실제 역할 lifecycle `1/1`과 desktop 8개·mobile 5개 탭 visual을 통과했다. 대표 repo·`main`·Persistent UAT·실제 provider는 미반영이다.
- 2026-07-21 Change 007 실험 상태: 실제 사용자 검수에서 재현된 IQC Pending 단절을 기준으로 프로젝트 기본 전체 흐름, 설계·구매 동시 업무, 구매 handoff 완료 조건, 도착→IQC 자동 생성, exact IQC deep link, 검사 Pending 원자 재검사·합격 종결, 자재 입고/키팅 하위 탭과 QR 선택 batch·inline preview를 보정했다. 실제 역할 UI lifecycle과 desktop/mobile visual을 통과했고 대표 repo·`main`·Persistent UAT·실제 provider는 미반영이다.
- 2026-07-22 Change 008 실험 상태: 역할별 18단계 시나리오를 구매팀 발주 수량 입력, 선택형 키팅 현황 공유, 생산관리 제조 투입 요청, LQC·OQC Checklist, 전진검수·FAT Aggregate, 현재 프로젝트 상세 8개 탭과 출하 달력월 정산으로 갱신했다. 전체 lifecycle `1/1`, 12면·6회 분할 입고·Pending 6건 stress lifecycle `1/1`, 품질 Aggregate Pending 재검사 `1/1`, IQC Pending 연속성 `1/1`을 isolated PostgreSQL에서 통과했고 임시 DB·container·network를 삭제했다.
- 2026-07-23 Change 009 실험 상태: 사용자가 Codex 재요청 없이 고정 검수 Frontend `42983`과 Backend `41166`을 함께 시작하는 macOS 더블클릭 launcher를 추가했다. 기존 검수 DB와 strict port를 유지하고 Docker·dependency preflight, PID·시작 fingerprint·cwd·command·process ancestry ownership, readiness와 중복 실행 방지를 적용했다. 미소유 listener는 종료하거나 다른 port로 우회하지 않는다.
- 2026-07-28 Change 010 실험 상태: 일반 1면과 12면 stress 실제 역할 lifecycle spec을 현재 선택형 FAT, 생산관리 업무 route·접기 입력, 프로젝트 우선 자재/IQC, LQC·OQC 파생 판정, 물류 증빙 선첨부 1회 저장·확정과 `발행 확인 저장` 계약으로 갱신했다. 일반 `1/1`은 18단계·프로젝트 완료·open Pending 0, stress `1/1`은 12면·사급 분할 6회·제조 Pending 6건·18단계 완료를 isolated PostgreSQL에서 통과하고 임시 자원을 정리했다.
- 2026-08-07 Change 011 게시 보정: PR #75 전체 CI에서 전역 Pending dashboard의 `pageSize: 100`과 suite 누적 합성 프로젝트가 결합해 새 프로젝트를 찾지 못한 P2를 확인했다. 테스트는 생성 프로젝트 ID의 `/pending?projectId=<id>`와 프로젝트 제목 heading을 사용하도록 고정하며 제품 UI·API·DB·migration·runtime은 변경하지 않는다. Targeted 격리 실행 `3/3`과 Frontend `175/175`·lint·typecheck·build를 통과했으며 PR 최신 head CI 뒤 같은 승인으로 원격 `main`에 게시한다.
- 2026-08-07 Change 012 게시 보정: PR #76 최신 head CI `3/3`과 원격 `main` 병합 뒤 merge SHA Full-Stack `55/56`에서 프로젝트별 Pending 제목이 전역 최근 100개 목록 또는 첫 Pending에 의존한 별도 P2를 확인했다. 프로젝트별 route는 exact project detail을 직접 읽고 실패 시 generic 제목 대신 retry를 제공한다. 수정 전 deterministic 실패, Frontend `177/177`, targeted `3/3`, desktop·390px overflow `0`, 전체 Full-Stack `56/56`, Backend `486/486`을 통과했고 PR #77 최신 head·merge SHA CI `3/3`과 원격 `main` 병합을 완료해 운영 release Gate를 재개했다.

### TASK-BILLING-REQUEST-001: 회계팀 세금계산서 발행요청 Excel

- 목적: 매월 1일·16일에 영업이 해당 기간 출하 완료 프로젝트를 선택해 회계팀 발행요청 자료를 즉시 만들고 요청 이력과 동일 workbook을 재다운로드한다.
- 포함 범위: 서울 반월 추천 기간, 최종 출발일 후보, checkbox 전체/개별 선택, server 재검증, 멱등 batch·snapshot·SHA-256 workbook, 요청 이력·재다운로드, 정산 발행요청 gate·회계 발행 확인 문구.
- 제외 범위: 영업 직접 세금계산서 발행, 회계팀 계정 workflow, 국세청·ERP, 실제 메일·Teams 전달, 취소·정정·수정세금계산서.
- 2026-07-19 실험 상태: Fable 1차 기획 → Codex review → Fable 2차 기획 `docs/33-billing-request-plan.md` 뒤 additive migration `0046`, Backend API/store, 영업 발행요청 화면, 정산 gate를 구현했다. 실제 lifecycle에서 출하 프로젝트 선택 Excel과 최종 완료를 검증했고 Backend `403/403`, Frontend `109/109`, typecheck/build를 통과했다. `EXPERIMENT_COMPLETE / BATCHED_FINAL`; 대표 repo·`main`·Persistent UAT·실제 provider 미반영.

### DESIGN-000 이후: 시각 토큰과 화면 통일

- DESIGN-000은 reference를 EMI 의미 체계로 투영한 CSS semantic token과 `DsPageHeader`, `DsSurface`, `DsToolbar`, `DsTabs`, `DsBadge` 공통 component를 구현했다. Shell·Home·Sales를 우선 적용했고 desktop/mobile visual regression을 완료했다.
- DESIGN-000 Change 001은 비상태 색·그라디언트·그림자를 제거하고 카드·입력·버튼·메뉴를 사각형으로 통일했다. 성공·주의·오류·진행·미읽음처럼 판단에 필요한 상태 표시만 의미색을 유지하며 기능·권한·API·DB·workflow는 변경하지 않았다.
- DESIGN-000 Change 002는 영업·생산관리·설계·구매·자재·제조·품질·물류·정산 입력을 `대상 확인 → 값 입력 → 저장` 순서와 번호 section·한 번 선택 control·하단 action bar로 통일했다. 기존 value·handler·API·필수값·권한·workflow는 유지했고 Frontend 134/134와 desktop·390px privacy-safe 검증을 통과했다.
- DESIGN-000 Change 003은 생산관리·자재·품질·물류의 메뉴 진입을 업무 선택 전용 화면으로 분리하고, 업무별 KPI·프로젝트 목록 뒤에 한 프로젝트 입력만 열도록 했다. 제조와 LQC·OQC·전진검수·FAT는 선택 프로젝트의 패널을 왼쪽 세로 목록으로 탐색한다. Pending은 단일 업무 선택 단계를 제거하고 KPI·프로젝트 목록으로 시작하며 상세를 한 프로젝트 이슈로 격리했다. 기존 API·권한·저장·상태 전이와 deep link를 유지했고 Frontend 136/136과 PC browser console error 0을 통과했으며 모바일은 사용자 요청에 따라 UX/UI 평가에서 제외했다.
- DESIGN-000 Change 004는 Change 003 PC 사용자 평가의 P1·P2를 구현했다. 1280×720에서 프로젝트·구매·자재의 실제 행을 첫 화면에 노출하고 프로젝트 상세 핵심정보·병목을 압축해 부서 탭을 첫 화면과 sticky 위치에 배치했다. 공통 breadcrumb, permission/prerequisite banner, empty state, secondary tools와 selection mode를 도입하고 제조·품질의 현재 패널 선택과 batch/export checkbox를 분리했다. 영업 Home은 당일 업무를 graph보다 먼저 표시하고, 생산계획은 접기형 단계, 양식 관리는 text preview와 draft input으로 분리했다. 기존 기능·권한·API·DB·workflow를 유지한 채 Frontend 136/136, build와 PC·390px overflow 0을 통과했다.
- DESIGN-000 Change 005는 물류 action panel과 영업 정산의 좁은 열에서 공통 입력 제목·3단계가 찌그러지던 P1을 container-aware header와 전체 열 배치로 보정했다. wireframe의 검은 활성·강조 표면은 흰 foreground를 함께 소유하도록 고정하고 물류 현재 단계의 흰 내부 표식만 검은 글자를 유지한다. Frontend 142/142, 일반·12면 stress lifecycle과 desktop·390px overflow 0, 지정 검은 표면 대비 4.5:1 미만 0건을 통과했다.
- DESIGN-000 Change 006은 독립 Graphite 실험에서 확정한 흑백 surface·type·spacing·control 계층과 공통 상태·feedback·dialog·KPI·header 구성을 최신 main 기반 승격 branch에 선택 이식했다. 생산계획·제조 투입 desktop grid는 36~40px header와 44~52px 단일 행 계약을, 내 업무·알림은 wrapper 100%·fixed-layout·detail 잔여 폭·48~52px action 행 계약을 가진다. 오류/검토 banner를 포함한 장식용 왼쪽 강조 rail은 제거했다. 기존 `operationalHubConfigs`를 source로 desktop·390px 모두 초기 접힘·부서 행 click·단일 펼침·child 직접 이동을 통일하고 업무 선택 전용 page를 삭제했으며 legacy 부서 root만 첫 실제 업무 redirect로 보존했다. Frontend `170/170`, build, 격리 mock E2E `4/4`와 사용자 검수를 완료했다. Backend·API·DB·workflow·배포는 변경하지 않았으며 local main merge가 승인됐다.
- TASK-UL891-SET-001 Change 007은 신규 UL891 프로젝트 상세 설계 탭을 조회 전용으로 만들고 세트 공통 설계와 중복되던 평면 패널 설계 영역을 제거했다. 단일 `수정` 버튼으로 별도 입력 화면에 진입하고 사용자 용어를 `임시저장`·`저장`으로 정리해 action 가까이에서 진행·결과를 확인한다. QR·비-UL891 기존 설계·Backend·DB·workflow는 유지했으며 Frontend 138/138과 desktop·390px 검수를 통과했다.
- TASK-UL891-SET-001 Change 008은 Change 007 사용자 검수에서 확인된 최종 저장 오류를 보정했다. `저장` 한 번으로 현재 form을 Draft에 갱신한 뒤 Publish하고, 규격을 Publish·패널 설계 완료 조건과 UL891 화면에서 제거했다. 기존 API·DB 호환 필드는 유지하며 Backend 실제 API 회귀·Frontend 138/138·desktop UI 검증을 통과했다.
- TASK-PRODUCTION-CONTROL-001 Change 008은 프로젝트 생산계획 항목에 선택 담당자·필요 인원·생산관리 코멘트를 추가하고 조회 제목을 `생산계획표`로 통일했다. 조회 표의 내부 실적 연결 열은 숨기고 담당자·필요 인원·코멘트를 표시하지만 1:1 연결과 자동 실적 계산은 그대로 유지한다. migration `0063`은 기존 행을 nullable로 보존하고 필요 인원 1~999와 담당자 FK를 강제한다.
- TASK-PRODUCTION-CONTROL-001 Change 010은 양식 카탈로그가 같은 현재 version을 후행 재선택하면서 빠른 `수정` 진입을 조회 상태로 되돌릴 수 있던 P2를 제거한다. Item·domain이 실제로 바뀔 때만 행과 편집 상태를 초기화하며 Backend·API·DB·권한·화면 디자인은 바꾸지 않는다.
- Change 010은 PR #81과 merge SHA main CI의 Backend·Frontend·Full-Stack `3/3`을 모두 통과해 원격 `main`에 병합됐다. Azure 운영 image 재배포는 수정 게시 승인에 포함되지 않아 별도 승인 Gate로 남긴다.
- TASK-PRODUCTION-CONTROL-001 Change 011은 생성 시점 model과 관계없이 프로젝트 자체 계획 행에 기간과 실적 1:1 연결을 저장하고, 조회를 `연결 실적`이 보이는 생산계획표와 계획·실적 2중 막대 일정표로 단일화한다. 양식 관리 기본값은 이후 생성 프로젝트에만 적용하고 Legacy 행·실행 이력과 UL891 세트 계약은 보존한다. 행 추가·삭제·재추가 저장은 활성 순번 `1..N` 재부여와 충돌 없는 임시 순번 이동으로 보정했다. PR #101 필수 CI와 사용자 검수 뒤 exact main SHA `8b19483e40655ce99c13cb470217ccddf444b1c0`로 병합하고 Azure release `31774236257`의 Backend·Frontend ready와 public security smoke를 통과했다. database migration은 없다.
- TASK-UL891-PRODUCTION-PLAN-001은 Ul891Set+LinkedV1의 공통 계획 항목·1:1 실적 연결은 한 벌로 유지하고 실제 실물 세트마다 기간·담당자·필요 인원·코멘트 overlay를 저장한다. 생산관리 탭의 전체/세트 선택이 생산계획표와 일정표를 함께 바꾸며 패널 실적은 선택 세트, 구매·자재·IQC는 프로젝트 공통으로 집계한다. migration `0064`, Backend `430/430`, Frontend `142/142`, PC·390px·일반/stress lifecycle 증빙을 완료하고 누적 checkpoint `e6f3fa6`에 포함했다.
- DESIGN-001 화면 통일 실험은 구현·자동 검증·페이지별 screenshot을 완료해 `EXPERIMENT_COMPLETE / BATCHED_FINAL`이다. 같은 전체 화면 통일을 다시 기획하지 않는다.
- DESIGN-000 token foundation도 `EXPERIMENT_COMPLETE / BATCHED_FINAL`이며 Figma file/library publish만 범위 밖이다. 후속 화면은 신규 임의 값보다 foundation token과 primitive를 먼저 사용한다.
- 기능 Task에서도 loading·empty·error·success feedback, 접근성, 한글 안내와 390px/Teams narrow 기준은 선행 적용한다.

## 24. 추적 대상 리스트

| 번호 | 항목 | 상태 | 담당/출처 | 후속 TASK | 비고 |
| --- | --- | --- | --- | --- | --- |
| 1 | 18단계 이미지 기준 확정 | 확정 | 사용자 제공 이미지/회의 결정 | TASK-006A/006B | 2번 생산관리, 3번 설계 |
| 2 | LQC 성적서 입력 방식 | 현업 조사 수신 / 정책 검토 | 품질/사용자 조사 | TASK-QUALITY-OPERATING-MODEL-001 / TASK-012A | 현재 기준서·성적서 없음. 독립 의견 비교 후 기록 수준과 검사 단위 확정 필요 |
| 3 | LQC 사진 필수 위치 | 미확정 | 품질/현업 회신 | TASK-012A | requires_photo 후보 |
| 4 | OQC 성적서 입력 방식 | 현업 조사 수신 / 정책 검토 | 품질/사용자 조사 | TASK-QUALITY-OPERATING-MODEL-001 / TASK-012A | 공통 기준·고객 요청·과거 문제의 프로젝트별 적용 및 제외/N/A 정책 확정 필요 |
| 5 | OQC 사진 필수 위치 | 미확정 | 품질/현업 회신 | TASK-012A | 저장 차단 기준 필요 |
| 6 | 자주순차표 큰 틀 | 부분 확정 | 생산관리/제조 회신 | TASK-011A | 웹 적용 확정, 상세 항목 회신 필요 |
| 7 | 제조 화면 표시 항목 | 미확정 | 제조/생산관리 회신 | TASK-011A | 항상 표시 항목 |
| 8 | 제조 팝업 표시 항목 | 미확정 | 제조/생산관리 회신 | TASK-011A | 상세 입력 팝업 후보 |
| 9 | 제조 저장-only 항목 | 미확정 | 제조/생산관리 회신 | TASK-011A | 저장되지만 상시 표시 불필요 |
| 10 | 제조 LQC 요청 기준 | 미확정 | 제조/품질 회신 | TASK-011A/012A | 자동 생성 event 기준 필요 |
| 11 | 검사성적서 PDF 양식 | 외함 IQC 확정 / 그 외 검사는 정책 검토 | 품질/고객양식/사용자 조사 | TASK-QUALITY-OPERATING-MODEL-001 / TASK-012A | 외함은 협력사 종이 검사서+EMI 확인·서명 스캔본을 회차별 보존하며 시스템 PDF를 만들지 않는다. 고객용 요약 성적서는 후속 |
| 12 | IQC 체크리스트 상세 항목 | 외함 운영 확정·구현 완료 | 품질/사용자 확정 | TASK-QUALITY-OPERATING-MODEL-001 | 신규 프로젝트는 구매품 구분 snapshot으로 외함만 스캔형 1항목 적합/부적합, 비외함은 검사 대상 아님. 기존 프로젝트는 기존 상세 IQC 유지 |
| 13 | LQC 체크리스트 상세 항목 | 현업 조사 수신 / 정책 검토 | 품질/사용자 조사 | TASK-QUALITY-OPERATING-MODEL-001 / TASK-012A | 제조 중 실제 검사 기준과 기록·성적서 수준 확정 필요 |
| 14 | OQC 체크리스트 상세 항목 | 현업 조사 수신 / 정책 검토 | 품질/사용자 조사 | TASK-QUALITY-OPERATING-MODEL-001 / TASK-012A | 프로젝트 조건·과거 문제·고객 요청의 적용 계획과 N/A/제외 이력 필요 |
| 15 | FAT 필요 여부 기본값 | 확정 | 사용자 검수/TASK-006B | TASK-006B/012A | 기본 false, 프로젝트별 선택 |
| 16 | 구매 업체 입력 방식 | 확정 | 사용자 검수/TASK-006B | TASK-006B 또는 후속 구매 TASK | 업체 header/field 포함, 업체 master는 후속 |
| 17 | Pending List 상태값 | 초안 | 사용자 논의 | TASK-007A | 등록/조치 요청/조치 중/재검사 요청/종결 |
| 18 | 조치 담당 부서 목록 | 초안 | 사용자 논의 | TASK-007A | 귀책부서 표현 금지 |
| 19 | 부적합 조치 유형 상세 | 부분 확정 | 사용자 논의 | TASK-007A/008A/012A | 반송/현장 수리 흐름 |
| 20 | 포장 구성 입력 필드 | 미확정 | 물류 회신 | TASK-013A | 포장번호, 규격, 중량 등 |
| 21 | 영업 정산 항목 | 부분 확정 | 사용자 논의 | TASK-014A | 세금계산서 완료는 확정 |
| 22 | 모든 페이지 Excel 출력 범위 | `EXPERIMENT_COMPLETE / BATCHED_FINAL` | 사용자 요청 | TASK-EXPORT-001 | 업무 12·관리자 8 화면의 checkbox 전체선택·단일 선택 export와 server allowlist column picker 완료. preset·재정렬·multi-sheet는 별도 optional이며 완료 scope를 다시 열지 않음 |
| 23 | Microsoft 365 로그인 적용 시점 | 완료 | 인프라/운영 결정 | TASK-INFRA-001 | 인증 기반 구현 완료. 운영 배포 전 실제 Entra 설정, 운영 redirect URI, Production/Staging dev auth 및 AdminUserSwitch 비활성 검수 필요 |
| 24 | 관리자 페이지 범위 | 완료 | 사용자 요청 | TASK-ADMIN-001 | 시스템 관리 중심으로 구현 완료. 업무 부서 입력 기준정보는 후속 결정 |
| 25 | 프로젝트 대표 상태 방식 | 확정 | 실무 협의 | 상태 집계 구현 TASK | 병목 기준 + 진행률 |
| 26 | 알림 채널 구성 | 정책 확정·운영 적용·실기기 검수 완료 | 사용자 확정 | TASK-NOTIFY-POLICY-001 / TASK-PWA-PUSH-001 | 자동 업무·Pending·프로젝트 lifecycle의 인앱·Teams Activity·메일·PWA 수신자와 발송 시점 확정. PWA는 설치·허용한 활성 기기에 인앱과 같은 새 알림을 보내며 미등록자는 인앱만 유지 |
| 27 | 진행률(%) 계산식 정의 | 확정 | 실무 협의 | 상태 집계 구현 TASK | 완료된 필수 workflow 단계 수 / 전체 필수 workflow 단계 수. FAT는 대상 프로젝트만 분모 포함. 프로젝트 상태 집계는 9장 기준. |
| 28 | Teams 통합 채널 생성 및 Webhook URL | 검수 완료 | 사용자 | TASK-NOTIFY-001 | 테스트 채널/Webhook actual 검수 완료. 운영 전 Webhook 재발급과 secret 주입 필요 |
| 29 | 알림 전용 메일 계정 생성 | 검수 완료 | 사용자 | TASK-NOTIFY-001 | Hiworks/M365 Graph Mail.Send 대신 Gmail SMTP 초기 경로 사용. 장기 운영 발송 수단 검토 필요 |
| 30 | Graph API 앱 등록 및 권한 승인 | 검수 완료 | 사용자 | TASK-INFRA-001 / TASK-NOTIFY-003 | 로그인 앱 등록은 INFRA-001에서 사용. Mail.Send는 기본 경로에서 제외. TeamsActivity.Send 권한 승인 및 Teams Activity actual smoke 완료 |
| 31 | 퇴사/부서이동 시 미완료 내 업무 이관 규칙 | 미확정 | 실무 협의 | TASK-INFRA-001 이후 | 담당자 부재 시 업무 귀속 처리 |
| 32 | 에스컬레이션 운영 단계 | 확정 | 사용자 확정 | TASK-NOTIFY-POLICY-001 | L0 직전 영업일 Teams, L1 다음 날 첫 평가 Teams+메일만 신규 운영. L2/L3는 schema·과거 이력만 보존 |
| 33 | dev user 담당 프로젝트/내 업무의 실계정 이관 수동 절차 | 미확정 | 실무 협의 | INFRA-001 이후 | 자동 병합 금지에 따른 후속 |
| 34 | Teams Activity Feed provider/capability | 완료 | 사용자/관리자 | TASK-NOTIFY-003 / TASK-NOTIFY-POLICY-001 | provider actual 검수와 installedAppId 의존 제거 완료. 자동 event coverage는 6.5.2.2 기준으로 연결했고 운영 전 URL 전환 필요 |
| 35 | Gmail SMTP 운영 적합성 및 공식 발송 수단 전환 | 미확정 | 사용자/총무/보안 | 운영 전 검토 | Gmail SMTP는 초기/UAT/시범운영용. 발송량, 보안, 스팸 정책과 회사 공식 발송 수단 전환 검토 |
| 36 | 운영용 Teams Webhook 재발급 | 미확정 | 사용자/운영 | 운영 배포 전 | UAT/test Webhook과 운영 Webhook을 분리하고 secret/env로만 주입 |
| 37 | 대한민국 공휴일 데이터 동기화 service key | 미확정 | 사용자/운영 | CALENDAR sync 후속 | 공식 API sync 구조는 있으나 service key 준비 전까지 관리자 Excel/manual 등록 사용 |
| 38 | 회사 휴일 연간 등록/검수 | 부분 완료 | System Administrator | 운영 전 검수 | 관리자 휴일 관리 API/UI와 Excel 일괄 등록은 구현 완료. 운영 전 연간 Company holiday 입력 필요 |
| 39 | 회사 자체 근무일 지정 필요성 | 미확정 | 사용자/운영 | CALENDAR 후속 | 이번 TASK에서는 구현하지 않음. 필요 시 휴일 override 모델 별도 검토 |
| 40 | 생산계획/구매 예정일의 work_items.due_date 동기화 | 확정·local 구현 | 사용자 확정 | TASK-NOTIFY-POLICY-001 | 생산은 정확한 plan item 종료일, 구매는 item 입고예정일, 프로젝트 집계는 미완료 구매품의 가장 이른 입고예정일을 미완료 업무에 동기화 |
| 41 | due_date 없는 기존 업무 처리 정책 | 확정·local 구현 | 사용자 확정 | TASK-NOTIFY-POLICY-001 | 정확한 일정 원본이 없거나 완료·취소·모호한 업무는 변경하지 않고 `null`이면 에스컬레이션 제외 |
| 42 | Daily Digest HTML table 개선 여부 | 미확정 | 사용자/운영 | 알림 UX 후속 | 담당 프로젝트 요약은 plain text renderer 기준으로 구현. 필요 시 HTML table 개선 |
| 43 | Item 관리자 관리 여부 | 미확정 | 사용자/운영 | ADMIN 후속 | ADMIN-001에서는 제외. Item 신규 추가/정렬/비활성화 정책은 별도 결정 필요 |
| 44 | 포장방식 기준정보화 및 size_required | 미확정 | 사용자/운영 | ADMIN/패널 후속 | ADMIN-001에서는 제외. 패널 완료 판정, 프로젝트 입력, Excel 회귀 범위 검토 필요 |
| 45 | 생산계획 단계/구매 필수 항목 관리자 통합 | 미확정 | 사용자/운영 | ADMIN 후속 | 현재는 각 업무 영역 설정으로 유지 |
| 46 | role/permission 편집 UI | 미확정 | 사용자/운영 | ADMIN 후속 | ADMIN-001은 read-only 권한 매트릭스만 제공 |
| 47 | 삭제 예정 데이터 purge 운영 정책 | 미확정 | 사용자/운영 | 운영 고도화 | 7일 후 purge worker는 구현. 보류 데이터 처리/운영 알림은 후속 검토 |
| 48 | 전체 field-level audit 확장 | Main Merged / Azure Released / Change 004 Frontend 재배포 완료 / 로그인 재검수 대기 | 사용자/운영 | TASK-AUDIT-001 | 공개 검수에서 MSAL v5 `LOGIN_SUCCESS` payload 해석 오류로 로그인 기록 API 호출이 누락됨을 확인했다. Change 004는 `AccountInfo` 직접 처리와 로그인 시작 탭 단일 소비를 보정해 Frontend `238/238`·typecheck·lint error `0`·build·독립 검증 PASS를 확인했다. PR `#113`과 Azure release `33358365813`으로 Frontend만 재배포했고 Backend·migration은 건너뛰었다. 새 대화형 로그인 1건 재검수가 남았다. |
| 49 | 관리자 모바일 UX 고도화 | 미확정 | 사용자/운영 | ADMIN 후속 | ADMIN-001은 page-level overflow 방지 기준으로 검수 |
| 50 | 외부 알림 delivery 동시성·실패 재처리 | 정책 결정·사용자 검수 완료 / PR #44 squash merge 승인 | 개발/운영 | TASK-NOTIFY-004 | claim/lease·automatic retry·attempt lineage·provider 오류 분류·starvation은 완료. Terminal Failed는 최종 상태로 유지하고 수동 재처리는 별도 신규 기능으로 Deferred |
| 51 | 기존 업무 화면 Action Feedback UX | A1·A2 `EXPERIMENT_COMPLETE / BATCHED_FINAL` | 사용자/개발 | TASK-UX-001 | 공통 hook·내 업무·알림·생산계획·구매·자재·IQC·키팅·패널·Excel 완료. 대표 repo·main·Persistent UAT 미반영 |
| 52 | 사용자별 알림 설정 | `EXPERIMENT_COMPLETE / BATCHED_FINAL` | 사용자/운영 | TASK-NOTIFY-005 | 선택 3종 sparse opt-out·필수 잠금·audit·Suppressed gate·desktop/390 완료. 대표 repo·main·Persistent UAT 미반영 |
| 92 | 개인화 Home·프로필 shell | `EXPERIMENT_COMPLETE / BATCHED_FINAL` | 사용자/개발 | TASK-HOME-002 | actual 사용자 계정 shell·본인 사진·9개 부서 핵심 지표·full-height sidebar·모바일 계정 sheet/drawer 완료. migration `0042`, 대표 repo·main·Persistent UAT 미반영 |
| 94 | Home 공지사항 게시판 | `EXPERIMENT_COMPLETE / BATCHED_FINAL` | 사용자/개발 | TASK-NOTICE-BOARD-001 | Home 상단·중앙을 보존하고 하단 병목 widget만 모든 active 사용자의 공지 작성·조회 공간으로 교체. author-only soft delete·멱등 등록·desktop/mobile 완료, 외부 알림·내 업무 자동 생성은 제외 |
| 93 | 패널 QR 발급·인증 모바일 scan landing | `EXPERIMENT_COMPLETE / BATCHED_FINAL` | 현장 사용자/개발 | TASK-QR-001 | 패널당 활성 1개·SVG/PNG·선택 인쇄·담당 업무 routing·관리자 rotation 완료. Backend `406/406`, Frontend `110/110`, QR E2E `1/1`, migration `0047`; 대표 repo·main·Persistent UAT 미반영 |
| 91 | 사용자별 알림 설정 감사 조회 UI | `EXPERIMENT_COMPLETE / BATCHED_FINAL` | 사용자/운영 | TASK-NOTIFY-AUDIT-001 | 관리자 기간·행동·알림 종류·사용자/부서 조회·요약, 현재 계정 기준 안내, desktop/mobile UI와 선택 Excel 완료. migration `0048`; 대표 repo·main·Persistent UAT 미반영 |
| 53 | Task 종료 5종 산출물과 개인정보 기준 | 완료 | BASELINE-GOV-001 | [Task 종료 및 산출물 정책](12-task-completion-policy.md) | 사용자 승인 후 PR #21 squash merge. canonical policy를 사용하고 Roadmap/AGENTS에는 세부 규칙을 중복 정의하지 않음 |
| 54 | Full-Stack E2E PostgreSQL 물리 격리 | 완료 | 개발/운영 | TASK-E2E-ISOLATION-001 | 전용 container/network/tmpfs, `emi_qms_e2e_*` guard, 외부 provider 차단, Full-Stack E2E 16개 통과. PR #22 squash merge `45fd61c` |
| 55 | HTTPS Development UAT 안정화 | 최초 Task 완료 / Change 001 자동 검증·사용자 검수 완료 / PR #48 squash merge 승인 | 개발/운영 | TASK-UAT-001 | HTTPS-only 5174, strict port/ownership, same-origin 5081 proxy, 로그인 유지·재인증·기존 알림 조회, Delivery Worker 단독 활성, Teams Activity 신규 ManualTest 1건 Sent·client 표시 확인, Review-safe·5176·persistent UAT 보존. PR #23 + PR #48 |
| 56 | Frontend dependency security | 자동 검증·사용자 검수 완료 / merge 승인 | 개발/보안 | TASK-FRONTEND-SEC-001 | Vite 7.3.6, esbuild 0.28.1, Vitest 4.1.0. Audit 전체 0, frontend/backend/E2E와 5174/5185 비교 검수 통과. PR #24 |
| 57 | Review-safe UAT | 자동 검증·사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-UAT-002 | 5092/5190, startup·worker·provider·HTTP mutation 차단, DB session read-only, schema readiness, Development UAT 분리. PR #26 |
| 58 | UAT 통합 사용자 검수 | 자동 검증·사용자 검수 완료 / merge 승인 | 사용자/개발 | UAT-VERIFY-001 | 최신 main runtime·ledger/schema/data/권한/dashboard/Review-safe/UI 기준선과 개인정보 안전 merge projection 통과. UAT 기준선 Go, 신규 기능 No-Go 유지, PR #29 병합 승인 |
| 59 | Notification delivery claim/lease | 자동 검증·사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-NOTIFY-REL-001 | Processing·SKIP LOCKED·lease/fencing·attempt audit, 정상 경쟁 provider call 1회, isolated candidate 5094/5192. Persistent UAT 0028 미적용, actual provider 호출 0, at-least-once이며 exactly-once 미보장. PR #30 |
| 60 | Escalation starvation | 구현·자동 검증·사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-NOTIFY-ESC-001 | 기존 evaluation timestamp fair ordering, 후보 오류 격리, 101/200/201 유한 poll, 중복 0. Persistent UAT worker는 disabled 유지 |
| 61 | 마지막 System Administrator 동시성 보호 | controlled UAT Phase A~D·사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-UAT-AUTH-HARDEN-001 | Privacy-safe evidence·isolated HTTP·temporary ReviewSafe·latest-main Development handover 완료. Persistent live mutation은 break-glass 증명 전 No-Go |
| 62 | Git history 개인정보 | 정책·rewrite·Support cleanup·당시 public 재개 완료 / history P2 Resolved / 현재 Private | 사용자/보안 | TASK-GOV-002 / TASK-GOV-HISTORY-REWRITE-001 | 영향 ref `16/16`, fresh clone 검증, internal reference 제거·repository GC와 cached reference `REMOVED`. Task 완료 당시 `PUBLIC`, 2026-08-10 actual readback `PRIVATE`, backup 삭제 미승인 |
| 63 | Patched frontend UAT handover | 자동 검증·사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-UAT-HANDOVER-001 | 최신 main Vite 7.3.6 frontend를 5174에 인계. Teams client 검수, Backend/PostgreSQL 보존과 DB snapshot 확인 완료. PR #25 |
| 64 | Migration ledger 전체 집합 검증 | 자동 검증·사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-DB-MIGRATION-001 | canonical 27/live 28/approved legacy 1, full-set compare, schema probe, mismatch 503, candidate 5191/5093, live row 미변경. PR #27 |
| 65 | Privacy-safe Review-safe runtime handover | 자동 검증·사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-UAT-HANDOVER-002 | merged main 5190/5092, Compatible 27/28/1, redacted browser matrix, DB read-only·423, Candidate/Persistent UAT 보존. PR #28 |
| 66 | Notification claim/lease UAT handover | 사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-UAT-HANDOVER-003 | Persistent UAT 0028, canonical 28 + approved legacy 1 = live 29, Review-safe/Development controlled handover, 승인된 ManualTest 단일 Sent lineage와 unrelated provider call 0 |
| 67 | Repository 지침·Rules 이관 | 구현·자동 검증·사용자 검수 완료 / PR #32 merge 완료 | 개발 | TASK-GOV-CODEX-001 | 전역·영역별 지침, 종료 정책, 검증 matrix, privacy-safe evidence와 command rules의 역할을 분리하고 신규 기능 기획 템플릿에서 공통 장문 규칙을 제거. Shell wrapper는 prompt하되 내부 semantic 완전 차단은 미보장 |
| 68 | Mutation worker maintenance gate | 구현·자동 검증·사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-UAT-MAINTENANCE-001 | purge 기본 true·explicit disable, 세 mutation worker 조건부 DI와 runtime projection, Phase A isolated 검증. Persistent UAT/0028 무변경 |
| 69 | Escalation fair-ordering controlled UAT | 구현·자동 검증·사용자 검수 완료 / merge 승인 | 개발/운영 | TASK-UAT-NOTIFY-ESC-001 | Phase A forecast, escalation-only Phase B poll 2회, latest-main Phase C poll 3회와 Development 5174/5081 복구. Live candidate 0, DB/provider delta 0, Preview 5185 DOWN. PR #35 |
| 70 | Fable 5 신규 기능·Codex-only 작업 라우터 | Change 001~013 merge 완료 / Change 014 fast-track·Change 015 완료 원장 local 구현 | 개발 | TASK-GOV-CODEX-002 | 일반 branch single-pass 보존. experiment 완료 scope 재선택 금지, `BATCHED_FINAL` 검수 분리, 대표 repo·main·게시 제외 |
| 71 | 운영 hosting·domain 확정 | 20일 시범 구성 승인 / 비용 Gate 대기 | 사용자/운영 | TASK-AZURE-DEPLOY-001 | Front Door Standard·Container Apps Consumption·private PostgreSQL B2s와 최종 hostname을 선택. 실제 resource·restore·TLS·rollback 증빙은 사용자 실행 뒤 검수 |
| 72 | Teams 앱 catalog 게시와 운영 URL 전환 | Local manifest·package 검증 완료 / actual update 대기 | 사용자/운영 | TASK-AZURE-DEPLOY-001 | privacy-safe manifest template·package builder 완료. 조직 catalog update와 actual provider smoke는 사용자 실행 |
| 73 | 첨부 storage·backup·restore 정책 | 미확정 | 사용자/운영/보안 | TASK-007A·MOBILE-001 | 업로드 보안, 보존 기간, restore rehearsal과 운영 storage를 기능 planning 전에 확정 |
| 74 | terminal Failed delivery 수동 재처리 범위 | `EXPERIMENT_COMPLETE / BATCHED_FINAL` | 사용자/개발/운영 | TASK-NOTIFY-REPROCESS-001 | terminal Failed만 generation 기반 CAS·원자 배치·사유·중복 위험 확인·append-only audit로 재처리. migration `0049`; 실제 provider·Persistent UAT 미적용 |
| 75 | Auth break-glass 계정과 복구 절차 | 미확정 | 사용자/보안/운영 | TASK-UAT-AUTH-HARDEN-001 | 인증 가능한 별도 복구 경로가 증명되기 전 Persistent live last-admin mutation 금지 |
| 76 | Roadmap 목표 시기 해석 | 확정 | 사용자/개발 | Roadmap 운영 | Target Window는 확정 약속이 아니며 status·dependency·external blocker·approval gate를 우선 |
| 77 | Git history coordinated rewrite 실행 | 실행·Support closure·독립 검증·사용자 검수·PR #50 merge·당시 public 재개 완료 / 현재 Private | 사용자/보안/개발 | TASK-GOV-HISTORY-REWRITE-001 | 영향 ref `16/16`, fresh clone·quarantine, internal reference 제거·GC, cached reference `REMOVED`. 2026-08-10 actual readback은 `PRIVATE`; backup 삭제는 별도 결정 |
| 78 | Azure Application Insights APM 계측 | P3 Backlog / 시범 실측 후 재평가 | 개발/운영 | AZURE-APM-001 | 현재는 Log Analytics container log를 사용. Request trace가 필요하면 Backend SDK 계측을 별도 시작 |
| 79 | Frontend production bundle 분할 | P3 Backlog / 정식 운영 성능 점검 | 개발 | FRONTEND-BUNDLE-001 | 현재 large bundle warning은 기능·build 실패가 아님. 실제 로딩 측정 후 route chunk 분할 결정 |
| 78 | Task instruction chain·완료 보고 형식 | 최초 Task merge 완료 / Change 001 상태 충돌 P2 Resolved·자동·독립 검증·사용자 검수 완료 / merge 승인 | 개발 | TASK-GOV-REPORTING-001 | 최초 Task 완료와 현재 Change 상태를 분리하고 작업 현황·Git 게시·중단 Task·Roadmap next·Finding identity를 보존 |
| 79 | Full-Stack E2E 구매정보 동적 행 timing | 본체 병합 완료 / Change 001 experiment `BATCHED_FINAL` | 개발/품질 | TASK-E2E-RELIABILITY-001 | 최신 load만 edit state에 반영하고, Change 001에서 초기 load 중 행 추가·저장·Excel을 잠그는 readiness 계약까지 보정. deterministic unit·isolated E2E 통과 |
| 80 | Backend import-order format debt | 구현·자동·독립 검증·사용자 검수·merge 완료 | 개발/품질 | TASK-BACKEND-FORMAT-001 | 정확한 Backend C# 9개 파일의 `IMPORTS=9`를 diagnostic 0으로 정리. 실행 코드·API·DB·runtime 변경 0 |
| 82 | Entra 로그인 공통 디자인 shell | 사용자 검수·승격·5174 반영·PR #49 merge 완료 | 사용자/개발/품질 | TASK-DESIGN-LOGIN-001 | 승인된 Figma 기반 auth shell과 Loading·checkbox 반영. Code Connect는 향후 필수 Gate가 아니며 5176 실험 runtime은 보존 |
| 83 | 로그인 디자인 promotion·experiment worktree 정리 | Promotion 정리 완료 / 5176 experiment 보존 | 사용자/개발 | TASK-GOV-CODEX-002 Change 004 | Clean·process 미사용·PR #49 merge 확인 뒤 promotion worktree 제거. 5176 experiment는 runtime·미게시 디자인 source로 계속 보존 |
| 84 | 전체 Finding Gate | Open P0/P1/P2 `0/0/0` / 독립 검증·사용자 검수 완료 / PR #50 merge 승인 | 사용자/개발/보안 | TASK-GOV-FINDING-GATE-001 | History·E2E·Failed retry·privacy 절차 P2 Resolved. 신규 기능은 `GO_FOR_USER_DECISION`, 자동 시작 아님 |
| 85 | Public 당시 main 서버 측 PR 강제 | Resolved — active required PR 적용·운영 문서 동기화 / 현재 Private | 사용자/운영/보안 | TASK-GOV-CODEX-002 Change 005 | 당시 Repository `PUBLIC`, default branch `main`, effective `pull_request` rule 1. 2026-08-10 actual readback은 `PRIVATE`; 승인·required status check·최신화·review 해결은 강제하지 않음 |
| 86 | Local GitHub 폴더 최종 정리 | 최종 삭제·자동·독립 검증·사용자 검수·PR #52 merge 완료 | 사용자/개발 | TASK-GOV-CODEX-002 Change 006 | 최상위 폴더 `6→3→2`. Dirty checkout 6개·local branch 32개 exact audit 뒤 보존 폴더를 영구 삭제. Docker stale handle `4→0`, 동일 PostgreSQL volume·DB aggregate·대표·디자인 runtime 보존 |
| 87 | 웹사이트 전체 유저플로우 개인 기획 자료 | 완료 / 독립 재검증·CI 3/3·PR #55 squash merge / Open P0/P1/P2/P3 `0/0/0/0` | 사용자/기획/개발 | TASK-USER-FLOW-001 Change 004 | 개인 개발 판단 자료. Fable direct-write 원문·확정/권고/미확정 경계·병렬 dependency map·vertical slice를 반영했으며 제품 구현·Phase B는 미승인 |
| 88 | Fable·USER-FLOW worktree 대표 clone 통합 | 로컬 보존·결과 커밋·일반 worktree 제거·자동·독립 검증·사용자 검수 완료 / Governance merge 승인 | 사용자/개발 | TASK-GOV-CODEX-002 Change 012 | 대표·디자인 `2/2` 복구. Governance merge 뒤 USER-FLOW를 최신 main에서 별도 처리 |
| 89 | 선택 프로젝트 Excel 내보내기 | `EXPERIMENT_COMPLETE / BATCHED_FINAL` | 사용자/개발 | TASK-EXPORT-002 | 선택 subset·전부-or-전무 scope 검증·`ProjectsSelected` audit·desktop/mobile/Excel screenshot 완료. 대표 repo·main·Persistent UAT·게시 제외 |
| 90 | 실험 계보 Full-Stack 전체 회귀 | Change 011 PR #76·Change 012 PR #77 원격 병합 및 merge SHA CI 완료 | 개발/품질 | TASK-E2E-FULL-SUITE-001 | 전역 목록 test 결합과 exact Pending route 메타데이터 P2를 project detail·fail-closed retry로 해소. Frontend 177·Full-Stack 56·Backend 486, PR 최신 head·merge SHA CI `3/3` 통과. Azure 운영 release Gate 재개 |
| 92 | 실험 Task 완료 원장과 중복 실행 방지 | 완료 원장 작성·Task selection gate 적용 | 사용자/개발 | TASK-GOV-CODEX-002 Change 015 | 완료 18 Task·A1 slice·남은 Task·P3 backlog를 분리. `BATCHED_FINAL`은 사용자 검수 완료가 아니며 완료 scope 재구현을 금지 |
| 95 | GitHub Azure release runner 경고 | P3 Backlog / 다음 배포 유지보수 | 개발/운영 | GHA-AZURE-RUNNER-WARNINGS-001 | Change 019 운영 release는 성공했다. Node.js action runtime과 Azure CLI version parse 경고는 호환 action/runner 갱신 뒤 정적·actual release 회귀로 닫는다 |
| 96 | GitHub Actions 일반 CI minute 과소비 | PR #89·main·문서 PR #90 검수 완료 / 1주 관찰 진행 | 사용자/개발/품질 | TASK-CI-COST-001 | 코드 PR 전체 품질 Gate와 Azure 수동 release를 보존했다. PR 이전 run 취소 3건, main Full-Stack skip, 문서 전용 heavy 3개 skip을 실제 확인했다. 실제 절감률은 최소 1주 관찰한다. |
| 97 | G2 납품 목표·불량·홈 임시 시뮬레이션 | Change 004 Automated Validation Complete / Publication Approved | 사용자/제조/영업/물류/개발 | TASK-G2-OPERATIONS-002 | 최초 exact main `220d1201c9dbb881fb3e5c5061871fb943c7961b`에 납품 목표·불량·홈 임시 예상값과 migration `0084`가 반영됐다. Change 003은 PR #117·Azure release `33577473523`으로 배포됐다. Change 004는 2026-08-28부터 전일 생산·불량과 당일 납품을 반영하는 수식으로 교정해 자동 검증과 게시 승인을 완료했으며 Ready PR·CI·exact main Azure 공개배포가 다음 Gate다. Schema·원본 데이터·Persistent UAT는 변경하지 않는다. |
| 98 | 유지 세션 포함 사이트 접속 감사 | PR #116 Main Merged / Azure Released / Automated Public Check Complete / User Validation Pending | 사용자/개발/운영 | TASK-SITE-ACCESS-001 | 새로고침·다른 화면 진입을 사용자+브라우저 client+30분 창으로 한 행에 묶고 명시적 로그아웃·고정 메뉴·접속 환경을 전체 감사 이력과 선택 Excel에서 확인한다. 공개 G2의 추적 97·migration `0084`를 보존하고 사이트 접속을 추적 98·migration `0085`로 병합했다. Azure release `33577473523`에서 migration·Backend·Frontend·public security와 공개 coverage·양수 summary를 확인했다. 사용자 화면·Excel 검수는 대기하며 Persistent UAT는 미적용이다. |

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
| 2026-07-18 | TASK-UX-001은 experiment fast-track에서 A1 공통 feedback과 내 업무·알림만 먼저 구현하고 A2 화면 확대를 별도 planning으로 유지 | 행이 active tab에서 사라져도 결과를 보존하면서 화면별 임시 구현과 범위 팽창을 막고, 대표 repo·main 반영 전 모바일·데스크톱 사용성을 먼저 검수하기 위함 | 23장, TASK-UX-001 |
| 2026-07-19 | TASK-UX-001 A2를 기존 A1 contract 위에 구현하고 production·procurement·materials·IQC·kitting·panel·Excel까지 experiment 완료로 닫음 | mutation 성공과 refresh/download trigger 실패를 분리하고, 편집 화면 이탈 뒤 결과·field focus·screen reader 안내를 보존하기 위함 | TASK-UX-001, `docs/31-action-feedback-a2-plan.md` |
| 2026-07-18 | TASK-NOTIFY-005 experiment는 자동 단계 업무 생성·D-1·일일 요약 3개만 sparse opt-out으로 허용하고 긴급·L1~L3·인앱·통합 채널·수동 발송은 잠근다 | 사용자 소음을 줄이면서 필수 업무 알림 누락과 dead control을 방지하고 기존 사용자 기본 delivery를 보존하기 위함 | 6장, 23장, TASK-NOTIFY-005 |
| 2026-07-18 | TASK-HOME-002는 shell의 계정 표시·사진을 actual 사용자에 고정하고 Home 부서 지표만 effective 사용자로 전환 | 관리자 검수 전환이나 Dev 사용자 전환 중 타인의 프로필 사진을 읽거나 계정 주체를 오인하지 않으면서 부서별 화면 검수를 가능하게 하기 위함 | 23장, TASK-HOME-002 |
| 2026-07-19 | TASK-HOME-002 Change 002는 운영 메뉴 11개를 모든 내부 부서에 공개하되 조회는 project scope, 입력은 기존 담당 mutation permission으로 분리하고 관리자 개인정보 메뉴는 기존 역할로 제한 | 메뉴 숨김을 입력 권한 표현으로 사용하지 않고 부서 간 진행 현황을 공유하면서 상태 변경과 민감 정보 범위를 확대하지 않기 위함 | 23장, TASK-HOME-002 Change 002 |
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
| 2026-07-13 | TASK-UAT-NOTIFY-ESC-001에서 Phase A forecast, escalation-only Phase B와 latest-main Development Phase C를 통과해 사용자 검수 대기로 전환 | Live candidate 0 시점에 worker registration·poll cadence·runtime ownership·provider 차단과 Persistent aggregate 불변을 확인하고 PR #34의 isolated 101/200/201 증빙을 controlled UAT에 연결하기 위함 | 23장, 24장, TASK-UAT-NOTIFY-ESC-001 |
| 2026-07-13 | TASK-UAT-NOTIFY-ESC-001 사용자 검수와 PR #35 squash merge를 승인 | Phase A/B/C, exact-process ownership 예외 해소, Development·Review-safe 상태, ledger 28/29/1, Pending/Processing 0/0, actual provider 호출 0, live candidate 0 제한과 at-least-once 계약을 확인하기 위함 | 23장, 24장, TASK-UAT-NOTIFY-ESC-001 |
| 2026-07-13 | TASK-AUTH-HARDEN-001 사용자 검수와 PR #36 squash merge를 승인 | 서로 다른 administrator 감소 경쟁의 성공 1·거부 1·active count 1, partial update·unexpected deadlock 0, HTTP 400·Entra 정책 불변, Persistent UAT 미적용, direct SQL 금지와 기존 범위 밖 import-order debt 9건을 확인하기 위함 | 23장, 24장, TASK-AUTH-HARDEN-001 |
| 2026-07-13 | TASK-UAT-AUTH-HARDEN-001 Change 001에서 REDESIGN과 due purge 전체 batch rollback을 승인 | Purge lifecycle과 canonical predicate가 상호 배타적이던 도달 불가능 guard를 물리 삭제 전용 predicate로 분리하고, malformed lifecycle state를 defense-in-depth로 보호하면서 기존 reference 정책과 public API를 유지하기 위함 | 23장, 24장, TASK-AUTH-HARDEN-001, Change 001 |
| 2026-07-13 | TASK-UAT-AUTH-HARDEN-001 Change 001 사용자 검수와 merge를 승인 | Purge 전용 predicate, malformed lifecycle defense-in-depth, due purge 전체 batch rollback, 기존 `PurgeBlocked` reference 정책과 전체 validation 결과를 확인하고 코드·문서를 함께 게시하기 위함 | 23장, 24장, TASK-AUTH-HARDEN-001, Change 001 |
| 2026-07-13 | TASK-GOV-CODEX-002에서 NEW_FEATURE 전용 Fable 5와 Codex-only 작업 라우터를 분리 | 신규 기능 기획과 기존 기능 보강의 역할·승인 경계를 명확히 하고 Fable의 Repository write·재귀 workflow를 차단하면서 PR #32의 canonical 안전 구조를 유지하기 위함 | 23장, 24장, 27장, TASK-GOV-CODEX-002 |
| 2026-07-13 | TASK-GOV-CODEX-002 사용자 검수와 PR #38 squash merge를 승인 | NEW_FEATURE 전용 Fable 5 planning, Codex-only 보강 흐름, 세션 분리, read-only·단일 작성자·승인 gate와 기존 Repository 안전 규칙이 함께 유지됨을 확인하기 위함 | 23장, 24장, 27장, TASK-GOV-CODEX-002 |
| 2026-07-13 | NEW_FEATURE만 Fable 5 planning을 사용하고 BUGFIX·P2_REMEDIATION·SECURITY·UAT_RUNTIME·DOCS_GOVERNANCE·HOUSEKEEPING·POLICY_DECISION은 Codex-only로 처리 | 신규 기능 기획과 기존 범위 보정의 책임·승인 경계를 분리하고 Fable의 Codex workflow 재귀 실행을 막기 위함 | AGENTS.md, CLAUDE.md, 23장 |
| 2026-07-13 | 기능 흐름을 먼저 구현하고 시각 token·브랜드 통일은 DESIGN Task로 후행 | 기능 의존성과 업무 검증을 먼저 안정화하되 모든 기능 Task에서 loading·empty·error·success feedback, 접근성, 한글 안내, 390px/Teams narrow와 overflow 0은 유지하기 위함 | 23장 |
| 2026-07-13 | 모바일 기능은 별도 URL이 아닌 동일 URL 적응형 rendering을 기본으로 사용 | 인증·deep link·Teams 진입 경로를 분리하지 않고 PC와 현장 UX를 일관되게 유지하기 위함 | 23장, TASK-MOBILE-001 |
| 2026-07-13 | Home은 widget-slot 구조로 만들고 현재 source data가 있는 widget부터 단계적으로 활성화 | 미구현 데이터와 예측 기능을 Home MVP에 섞지 않고 TASK-007B aggregate를 재사용하기 위함 | 23장, TASK-HOME-001 |
| 2026-07-13 | Roadmap 실행 순서는 고정 날짜보다 status·dependency·external blocker·planning/implementation 승인과 UAT gate를 우선 | 목표 시기를 확정 약속으로 오해하지 않고 실제 준비 상태에 따라 안전하게 다음 Task를 선택하기 위함 | 23장, 24장 |
| 2026-07-13 | TASK-008A와 TASK-010A는 별도 planning·구현·검증·rollback 단위로 유지 | 입고 데이터·migration 경계와 키팅·제조 업무 생성의 중복 방지 경계가 달라 한 PR로 묶으면 rollback과 검수 범위가 커지기 때문 | 23장 |
| 2026-07-13 | 현재 알림 채널 matrix 변경은 별도 POLICY_DECISION이 있어야 함 | 긴급·차단 메일, 에스컬레이션 메일과 Daily Digest 역할을 Roadmap 동기화만으로 변경하지 않기 위함 | 6장, 23장 |
| 2026-07-13 | TASK-UAT-AUTH-HARDEN-001 Change 001의 REDESIGN을 유지하고 controlled UAT에서 정책 결정을 다시 열지 않음 | PR #37에 병합된 purge 전용 predicate와 due purge batch rollback을 source of truth로 유지하고 남은 작업을 evidence·runtime handover에 한정하기 위함 | 23장, 24장, Change 001 |
| 2026-07-13 | TASK-GOV-ROADMAP-001 사용자 검수와 PR #39 squash merge를 승인 | PR #34~#38 이후 실제 상태, 남은 P2 Gate, dependency 중심 실행 큐, planning 미승인 상태, 공용 기기 범위 제외와 기존 알림 채널 matrix 보존을 확인하기 위함 | 21장~25장, TASK-GOV-ROADMAP-001 |
| 2026-07-13 | TASK-UAT-AUTH-HARDEN-001 Phase A~D와 5종 산출물 Draft PR 게시를 승인 | Synthetic actual HTTP로 last-admin·purge transaction을 검증하고 Persistent identity mutation 없이 latest-main Development를 적용하며, break-glass 미증명 상태의 live mutation No-Go를 유지하기 위함 | 21장~24장, TASK-UAT-AUTH-HARDEN-001 |
| 2026-07-13 | TASK-UAT-AUTH-HARDEN-001 사용자 검수와 PR #40 squash merge를 승인 | Privacy-safe evidence, isolated HTTP 경쟁·purge rollback, Persistent mutation-free runtime handover, Development·Review-safe 상태와 break-glass 미증명 live mutation No-Go를 확인하고 다음 P2를 TASK-GOV-002로 전환하기 위함 | 21장~25장, TASK-UAT-AUTH-HARDEN-001 |
| 2026-07-13 | TASK-GOV-002에서 public Git history 개인정보를 coordinated all-ref rewrite로 처리하고 risk owner를 Repository owner/security owner로 지정하며 PR #41 squash merge를 승인 | Current checkout은 비식별화됐지만 origin main과 다수 branch의 과거 개인정보가 reachable하며 private-only나 main-only 조치로는 제거가 완전하지 않기 때문. 실제 rewrite·force push는 별도 TASK-GOV-HISTORY-REWRITE-001 승인 전 금지 | 22장~25장, TASK-GOV-002 |
| 2026-07-13 | 모든 Task는 시작 전에 현재 Repository instruction chain을 읽고 종료 시 고정 10개 항목으로 완료 보고 | 과거 대화·축약 지침 의존을 막고 변경·검증·URL·검수·미커밋 상태·Finding·게시 gate를 매 Task에서 동일하게 확인하기 위함 | AGENTS.md, 23장~25장, TASK-GOV-REPORTING-001 |
| 2026-07-13 | TASK-GOV-REPORTING-001 사용자 검수와 squash merge를 승인 | instruction chain gate, 고정 10개 항목, `N/A` 사유, 검증·검수·게시 상태 분리를 Repository 표준으로 확정하기 위함 | AGENTS.md, 23장~25장, TASK-GOV-REPORTING-001 |
| 2026-07-14 | TASK-NOTIFY-004에서 terminal Failed를 현재 상태 모델의 최종 상태로 유지하고 수동 재처리를 별도 신규 기능으로 Deferred하는 `POLICY_CORRECTION_AND_DEFER`를 승인 | Automatic retry·최종 실패 가시성 계약은 이미 충족됐고, 전체 수동 재처리에는 retry generation·append-only audit·provider 중복 확인이라는 신규 능력이 필요하며 기존 SOP의 retry 안내만 실제 구현보다 앞서갔기 때문 | 22장~25장, TASK-NOTIFY-004 |
| 2026-07-14 | TASK-NOTIFY-004 사용자 검수와 PR #44 squash merge를 승인 | Terminal Failed 정책 정정, Pending retry·acknowledge·dismiss 유지, at-least-once 제한, 코드·runtime·Persistent UAT 변경 0과 별도 신규 기능 Deferred 경계를 확인하기 위함 | 22장~25장, TASK-NOTIFY-004 |
| 2026-07-14 | 신규 기능은 Fable 5가 사용자 deep-interview와 요약을 완료한 뒤 Fable 5 planning을 시작하고 Codex는 안전한 relay·기록·review를 담당 | 업무 맥락과 blocking 정책 결정을 planning 전에 Fable 주도로 고정하면서 interview·planning·implementation 승인 Gate를 분리하기 위함 | AGENTS.md, CLAUDE.md, 23장, TASK-GOV-CODEX-002 Change 001 |
| 2026-07-14 | 동일 목적 Task 방지안 B와 Roadmap Sequence Gate를 적용 | 새 Task 이름을 만들기 전에 목표·Finding·변경 경계·불변조건·산출물을 비교해 기존 canonical Task를 재사용하고, status·dependency·external blocker·Next Gate와 다른 작업은 명시적 재정렬 승인 전 시작하지 않기 위함 | AGENTS.md, 23장~25장, TASK-GOV-CODEX-002 Change 002 |
| 2026-07-14 | TASK-GOV-CODEX-002 Change 001·002 사용자 검수와 merge를 승인 | Fable 5가 deep-interview와 planning을 담당하고 Codex는 안전한 relay·review를 수행하며, Task Identity와 Roadmap Sequence Gate가 새 채팅에서도 기존 instruction chain 전체에 추가 적용됨을 확인하기 위함 | AGENTS.md, CLAUDE.md, 23장~25장, TASK-GOV-CODEX-002 |
| 2026-07-14 | GitHub Support의 history cache 처리 대기 중 기존 import-order 9건을 `TASK-BACKEND-FORMAT-001`로 먼저 계획 | 외부 blocker와 독립적인 P3 format debt를 정리하되 신규 기능 Gate와 history P2 상태는 변경하지 않기 위함 | 22장~25장, TASK-BACKEND-FORMAT-001 |
| 2026-07-14 | TASK-BACKEND-FORMAT-001 사용자 검수와 squash merge를 승인 | Backend C# 9개 파일의 import 순서만 정규화하고 format diagnostic 9→0, Backend 361/361, Frontend 62/62, Full-Stack E2E 16/16과 독립 diff 검증을 통과했음을 확인하기 위함 | 23장~25장, TASK-BACKEND-FORMAT-001 |
| 2026-07-14 | 일반 Task는 단일 canonical clone을 재사용하고 별도 worktree는 runtime·병렬 write·고위험 rehearsal에만 생성 | Task 문서는 Repository와 Git history에 누적되지만 source checkout·`node_modules`·Backend build artifact가 Task마다 영구 중복되지 않게 하기 위함 | AGENTS.md, 23장~25장, TASK-GOV-CODEX-002 Change 003 |
| 2026-07-13 | TASK-GOV-HISTORY-REWRITE-001 권장 묶음으로 temporary private, encrypted backup 7일 보존, 영향 ref explicit lease rewrite, Support 요청과 fresh-clone quarantine을 승인 | Published ref와 cache·외부 clone 경계를 분리해 개인정보 재노출과 old-history 재유입을 막고, Support 처리 전 public 재개를 자동화하지 않기 위함 | 22장~25장, TASK-GOV-HISTORY-REWRITE-001 |
| 2026-07-14 | 전체 P0/P1/P2 재평가의 canonical ID를 `TASK-GOV-FINDING-GATE-001`로 유지하고 Support 대기 중 provisional audit만 수행 | 중복 Task 생성을 방지하고 history P2가 Open인 동안 신규 기능 `NO_GO`를 유지하기 위함 | 23장~25장, TASK-GOV-FINDING-GATE-001 |
| 2026-07-14 | History Support 대기 중 `TASK-UAT-001` Change 001 병렬 실행을 승인하고 Development UAT를 HTTPS 5174 하나로 통일 | 로그인·일반 기능·알림·Teams Activity를 한 origin에서 검수하고 HTTP protocol drift와 불필요한 격리 DB port를 제거하면서 5081·5432·5190/5092·5176을 보존하기 위함 | 21장~25장, TASK-UAT-001 Change 001 |
| 2026-07-14 | `TASK-UAT-001` Change 001에서 5081 Teams Activity actual channel과 신규 ManualTest 1건 Graph 발송을 승인 | 기존 `TeamsActivityDisabled` terminal 2건은 audit로 보존하고 Delivery Worker만 활성인 상태에서 신규 delivery 1건의 retry lineage와 최종 `Sent`를 검수하면서 Escalation·Purge·다른 runtime·Persistent UAT DB/volume을 보존하기 위함 | 21장~25장, TASK-UAT-001 Change 001 |
| 2026-07-14 | `TASK-UAT-001` Change 001의 Teams client 실제 알림 수신 검수를 완료하고 merge까지 승인 | Microsoft Graph `Sent`와 사용자의 Activity Feed 실제 표시 확인을 분리해 모두 닫고 기존 terminal audit·다른 runtime·Persistent UAT 자원 보존 결과를 게시하기 위함 | 21장~25장, TASK-UAT-001 Change 001 |
| 2026-07-14 | `TASK-UAT-001` Change 001의 로그인 상태 유지·재인증과 기존 알림·Teams Activity 조회 검수를 완료하고 PR #48 squash merge 실행을 승인 | 남아 있던 사용자 검수 2건을 모두 닫고 자동 검증·actual provider·Teams client 수신·runtime 보존과 게시 gate를 하나의 완료 상태로 확정하기 위함 | 21장~25장, TASK-UAT-001 Change 001 |
| 2026-07-14 | History Support 대기 중 `TASK-DESIGN-LOGIN-001`을 별도 bounded worktree에서 병렬 진행하도록 승인 | 기존 Entra 정책·Backend·DB·runtime과 governance Task를 변경하지 않는 승인된 Frontend 디자인 구현이며, history P2와 신규 기능 `NO_GO` 상태를 그대로 보존할 수 있기 때문 | 21장~25장, TASK-DESIGN-LOGIN-001 |
| 2026-07-14 | `TASK-DESIGN-LOGIN-001` Change 001에서 Mobile을 제외하고 Desktop 로그인에는 Figma node에 존재하는 요소만 표시 | Figma가 제공하지 않은 반응형 해석과 보조 안내·다른 계정 action을 제거하고 승인된 Desktop 화면을 기준으로 exact geometry를 고정하기 위함 | 21장~25장, TASK-DESIGN-LOGIN-001 Change 001 |
| 2026-07-14 | `TASK-DESIGN-LOGIN-001` Change 002에서 Ellipse 68 pattern 위치와 PC viewport 등비 canvas를 적용 | Figma pattern fill을 빈 SVG와 사각형 mask로 근사한 차이를 제거하고 작은 PC 창에서도 1440×810 전체 화면과 요소 비율을 유지하기 위함 | 21장~25장, TASK-DESIGN-LOGIN-001 Change 002 |
| 2026-07-14 | `TASK-DESIGN-LOGIN-001` Change 003에서 전체 canvas letterbox를 red/white flexible panel 반응형으로 교체 | 16:9가 아닌 PC 창에서도 디자인 밖 빨간 여백 없이 두 Figma panel이 viewport를 채우고 내부 요소의 비율·좌표와 전체 가시성을 함께 유지하기 위함 | 21장~25장, TASK-DESIGN-LOGIN-001 Change 003 |
| 2026-07-14 | `TASK-DESIGN-LOGIN-001` Change 004에서 안내 문구를 추가하고 Figma base·glass·white shape 연결부를 재구현 | 명시 승인된 로그인 안내를 제공하고, white shape의 rounded corner 밖과 shadow 뒤에 Figma `#DA2127` red surface가 정확히 이어지게 하기 위함 | 21장~25장, TASK-DESIGN-LOGIN-001 Change 004 |
| 2026-07-14 | 완성된 Figma 화면은 5176에서 구현·검수한 뒤 화면 단위로 최신 main에 승격 | 모든 기능 완료까지 디자인을 장기 branch에 누적하지 않고, 완성·검수된 화면만 기능 source of truth인 최신 main에 안전하게 반영하기 위함. 당시 로그인 화면은 Change 007 재검수 전 승격 보류 상태였다. | 23장~25장, TASK-DESIGN-LOGIN-001 Change 005~007, 디자인 화면 단위 승격 운영 기준 |
| 2026-07-14 | `TASK-DESIGN-LOGIN-001` Change 005에서 다른 계정 선택은 Microsoft 로그인 화면에 위임하고 loading은 기본 로그인과 동일 geometry에서 안내만 변경 | Provider가 제공하는 `다른 계정 사용`을 중복 구현하지 않고, loading에 별도 미승인 element를 추가하지 않으면서 중복 인증 동작은 차단하기 위함 | 20장~25장, TASK-DESIGN-LOGIN-001 Change 005 |
| 2026-07-14 | `TASK-DESIGN-LOGIN-001` Change 006에서 Loading control을 제거하고 빨간 회전 indicator를 표시하며 화면 단위 승격 SOP를 canonical 문서로 고정 | 로그인 확인 중 중복 control을 숨기고 진행 상태를 명확히 표시하며, 5176 실험 branch 전체가 아닌 검수 완료 화면만 최신 main에 안전하게 승격하기 위함 | 20장~25장, TASK-DESIGN-LOGIN-001 Change 006, 디자인 화면 단위 승격 운영 기준 |
| 2026-07-14 | `TASK-DESIGN-LOGIN-001` Change 007에서 로그인 상태 유지 Variant 2를 구현하고 인증 action 도달 가능성을 audit | Figma 선택 상태의 red fill·white Done icon·dark text를 정확히 반영하고, Frontend account-switch 삭제가 UI 숨김이 아닌 source 제거임과 남은 인증 기능의 실제 접근 경로를 고정하기 위함 | 20장~25장, TASK-DESIGN-LOGIN-001 Change 007 |
| 2026-07-14 | `TASK-DESIGN-LOGIN-001` Change 008에서 로그인 상태 유지 Done icon만 checkbox 중앙 정렬 | 사용자 시각 검수에서 확인된 icon 오프셋을 보정하되 checkbox의 크기·색상·문구·기능과 나머지 화면을 보존하기 위함 | 20장~25장, TASK-DESIGN-LOGIN-001 Change 008 |
| 2026-07-14 | `TASK-DESIGN-LOGIN-001` 사용자 전체 검수를 완료하고 Change 009 최신 main 화면 단위 승격을 승인 | 5176 실험본 전체를 병합하지 않고 검수 완료 로그인 화면 fixed allowlist만 clean promotion branch에 이식·전체 검증하며, 5174·5176와 Backend·DB runtime 및 별도 게시 승인 경계를 보존하기 위함 | 20장~25장, TASK-DESIGN-LOGIN-001 Change 009, 디자인 화면 단위 승격 운영 기준 |
| 2026-07-15 | `TASK-DESIGN-LOGIN-001` 5174 Frontend 반영과 게시·merge를 일괄 승인 | 검증된 로그인 화면 fixed allowlist를 기능 source of truth에 승격하고 기존 Entra·Backend·DB·5176 runtime을 보존한 상태에서 HTTPS 5174 사용자 환경에 적용하기 위함 | 20장~25장, TASK-DESIGN-LOGIN-001 Change 009, 디자인 화면 단위 승격 운영 기준 |
| 2026-07-15 | `TASK-DESIGN-LOGIN-001` PR #49 squash merge 완료, Code Connect는 향후 디자인 구현 필수 Gate에서 제외 | 검수 완료 로그인 화면은 main에 승격됐고 이후 Figma 화면도 화면 단위 승격을 사용하되 seat 제한인 Code Connect 확인을 완료 조건으로 요구하지 않기 위함 | 23장~25장, TASK-DESIGN-LOGIN-001 |
| 2026-07-15 | 로그인 promotion·5176 experiment worktree 정리를 Deferred로 추적 | 현재는 그대로 보존하고 이후 runtime 소유·dirty 변경·commit reachability 확인과 별도 사용자 승인 뒤 HOUSEKEEPING으로 정리하기 위함 | 23장~25장, 추적 항목 83 |
| 2026-07-15 | GitHub Support의 internal reference 제거·repository GC 완료와 old cached reference `REMOVED`를 확인해 history P2를 Resolved로 전환 | Published ref rewrite와 fresh clone 검증에 server-side cleanup·direct-view fixed projection까지 충족됐으므로 전체 Finding Gate를 재개할 수 있기 때문. Public 재개·backup 삭제·문서 게시는 별도 결정으로 유지 | 23장~25장, TASK-GOV-HISTORY-REWRITE-001 |
| 2026-07-15 | TASK-GOV-FINDING-GATE-001 재평가에서 Open P0/P1/P2 `0/0/0`, 신규 기능 `GO_FOR_USER_DECISION`을 권고 | History·E2E·Failed retry·privacy 절차 P2의 closure 근거와 현재 runtime·Persistent aggregate를 재검증했기 때문. 사용자 Go 승인, 독립 검증과 문서 게시를 자동 수행하지 않음 | 23장~25장, TASK-GOV-FINDING-GATE-001 |
| 2026-07-15 | TASK-GOV-HISTORY-REWRITE-001과 TASK-GOV-FINDING-GATE-001의 독립 검증·사용자 검수를 완료하고 PR #50 squash merge를 승인 | 11/11 allowlist, product diff 0, P0/P1/P2/P3 `0/0/0/0`, runtime·Persistent UAT 보존과 merge Gate GO를 확인했기 때문. Public 재개·backup 삭제·worktree cleanup·신규 기능 Go는 포함하지 않음 | 23장~25장, 두 governance Task |
| 2026-07-15 | History closure와 PR #50 merge 뒤 Repository를 public으로 재개하고 clean inactive worktree 3개를 제거해 canonical root와 5176 디자인 실험만 유지 | History P2와 GitHub cached reference가 해소된 상태에서 공개 전환을 완료하고, merge된 publish·promotion·closure checkout이 계속 누적되지 않게 single canonical clone lifecycle을 회복하기 위함 | 23장~25장, TASK-GOV-HISTORY-REWRITE-001, TASK-GOV-CODEX-002 Change 004 |
| 2026-07-15 | Public default branch `main`에 required pull request만 강제하는 active Repository ruleset을 적용 | 기존 Repository 지침의 direct main push 금지를 서버 측에서 보장하되 1인 개발 속도를 위해 승인·CI·최신화·review 해결은 강제하지 않고 P3 `PUBLIC_MAIN_SERVER_SIDE_PROTECTION_ABSENT`를 해소하기 위함 | 23장~25장, TASK-GOV-CODEX-002 Change 005 |
| 2026-07-15 | TASK-GOV-CODEX-002 Change 004·005 사용자 검수를 완료하고 문서 commit·push·PR·merge를 승인 | Canonical root 정규화·5174 복구·public main required PR 적용, 운영 문서 P2 보정과 Open P0/P1/P2/P3 `0/0/0/0` 독립 검증을 모두 확인했기 때문 | 23장~25장, TASK-GOV-CODEX-002 Change 004·005 |
| 2026-07-15 | GitHub 최상위 폴더는 대표·5176 디자인·보존 컨테이너 3개로 제한 | 미커밋 또는 history 보존 사유가 있는 과거 checkout은 삭제하지 않고 한곳에 격리하고, 일반 Task는 대표 clone을 재사용해 폴더 누적을 막기 위함 | 23장~25장, TASK-GOV-CODEX-002 Change 006 |
| 2026-07-15 | 보존 컨테이너 exact audit 뒤 GitHub 최상위 폴더를 대표·5176 디자인 2개로 최종 정리 | Dirty checkout 6개·local branch 32개·local 설정·artifact가 main 또는 현재 종료 문서에 반영·대체됐거나 재생성 가능함을 확인했고, 승인된 Docker/PostgreSQL controlled maintenance로 stale handle을 해제하면서 동일 persistent volume·DB aggregate·runtime을 보존했기 때문. Repository 밖 encrypted history backup은 삭제하지 않음 | 23장~25장, TASK-GOV-CODEX-002 Change 006 |
| 2026-07-15 | TASK-GOV-CODEX-002 Change 006 사용자 검수와 PR #52 squash merge를 완료 | 최상위 폴더 `6→3→2`, stale handle `4→0`, 동일 PostgreSQL container·volume·DB aggregate, runtime URL 7/7, listener 6/6, 독립 검증과 CI 3/3 성공을 확인했기 때문. Branch·worktree와 encrypted history backup 삭제는 포함하지 않음 | 23장~25장, TASK-GOV-CODEX-002 Change 006 |
| 2026-07-15 | 0.6 신규 기능 GO와 TASK-007A보다 TASK-USER-FLOW-001 웹사이트 전체 유저플로우 설계를 먼저 수행하는 Roadmap 재정렬을 승인 | 개별 신규 기능을 구현하기 전에 현재·향후 화면, 역할, 내 업무·알림과 예외·복구 경로를 하나의 기준선으로 정리하고 후속 기능 planning의 충돌과 재작업을 줄이기 위함. 이번 승인은 interview·planning·Codex review까지만 포함하며 구현·runtime·DB·게시 승인은 포함하지 않음 | 23장~25장, TASK-USER-FLOW-001 |
| 2026-07-15 | TASK-USER-FLOW-001 Fable 5 model blocker를 `fable` alias와 전용 read-only runner로 해소하고 Round 1 질문 3건을 생성 | CLI가 `fable`을 Fable 5로 표시하며 Codex가 safe mode·Read/Glob/Grep·private output 경계를 유지한 실제 호출에서 interview 계약을 통과했기 때문. Planning·implementation과 게시 승인은 계속 분리 | 23장~25장, TASK-USER-FLOW-001, TASK-GOV-CODEX-002 Change 007 |
| 2026-07-15 | TASK-USER-FLOW-001 Phase A preview는 Fable 5가 전문을 직접 작성하고 GPT-5.6 SOL이 별도 review | Codex가 Fable 초안을 편집·반영하는 방식은 사용자 원래 계획과 달랐으므로, runner가 Fable stdout과 byte-identical한 `docs/13`을 기록하고 Finding 수정도 Fable revise로만 수행하기 위함 | 23장~25장, TASK-USER-FLOW-001 Change 001, TASK-GOV-CODEX-002 Change 009 |
| 2026-07-15 | TASK-USER-FLOW-001은 개인 개발 판단용 Fable 초안 1회와 Codex 내용 review 1회로 기획 작성 흐름 종료 | Fable·Codex가 수정 round를 반복하지 않고, Codex는 코드 일치보다 개발 방향 충돌·기능 가치·필요성·누락·우선순위를 판단하기 위함. Fable redraft·Phase B·canonical 게시와 실제 Roadmap 재정렬은 자동 승인하지 않음 | 23장~25장, TASK-USER-FLOW-001 Change 002, TASK-GOV-CODEX-002 Change 010 |
| 2026-07-15 | 대표 clone의 HTTPS 5174 Vite는 현재 branch를 따르고 branch 전환 중 유지하며 조건부로만 재시작 | Vite source watch가 일반 코드 전환을 자동 반영하는데 기존 process ownership 규칙이 대표 clone 재사용과 충돌해 일반 Task의 불필요한 server 중단·추가 worktree를 유도했기 때문 | AGENTS.md, 23장~25장, TASK-GOV-CODEX-002 Change 011 |
| 2026-07-15 | 완료 보고에 작업 현황 요약과 Git 게시·중단 Task·재개 조건·Roadmap next를 고정 표시 | 현재 Task 결과만으로는 어떤 게시가 남았고 어떤 작업이 중단됐으며 전체 종료 뒤 무엇을 시작하는지 한눈에 파악하기 어려웠기 때문 | AGENTS.md, Task 종료 정책, 23장~25장, TASK-GOV-REPORTING-001 Change 001 |
| 2026-07-16 | Fable은 Task 기준선을 private session으로 재사용하고 한 round에 관련 질문을 최대 5개까지 제시 | 질문·답변 품질은 유지하면서 반복 Repository 기준선 조사와 불필요한 왕복 시간을 줄이기 위함. Interview 문서와 현재 Repository는 계속 canonical source | AGENTS.md, CLAUDE.md, TASK-GOV-CODEX-002 Change 008 |
| 2026-07-16 | Fable이 질문·primary draft 전문을 직접 작성하고 Codex는 원문을 변경하지 않은 채 내용·제품 방향 review 1회로 종료 | Codex가 질문이나 전문을 다시 쓰지 않고 개발 방향 충돌·기능 가치·누락·우선순위를 검토하며 자동 draft-review-revise 반복을 없애기 위함 | AGENTS.md, CLAUDE.md, TASK-GOV-CODEX-002 Change 009·010 |
| 2026-07-17 | 명시된 `experiment/*` fast-track은 Fable 1차 기획 → Codex 내용 review → review 기반 Fable 2차 기획 → 2차 기획 기준 Codex 구현·검증·screenshot·local commit 순서로 수행 | 실험 개발 속도를 유지하면서도 Codex review가 실제 최종 기획에 반영되도록 하고, 일반 branch와 대표 repo·main·Persistent UAT·provider·게시 경계를 보존하기 위함 | AGENTS.md, CLAUDE.md, TASK-GOV-CODEX-002 Change 014 |
| 2026-07-18 | experiment에서 구현·필수 자동 검증·종료 산출물이 끝나고 사용자 직접 검수만 마지막에 남은 scope는 `EXPERIMENT_COMPLETE / BATCHED_FINAL`로 선택 종료 | canonical main queue의 Pending 상태 때문에 이미 구현한 TASK-007A가 다시 선택된 문제를 막고, 검수 대기·대표 repo 미반영·P3 후속을 기능 재구현 사유로 오인하지 않기 위함. 검수 완료·UAT·게시·merge 상태는 계속 분리 | AGENTS.md, 12장, 23장~25장, 27-experiment-task-ledger, TASK-GOV-CODEX-002 Change 015 |
| 2026-07-16 | TASK-USER-FLOW-001은 개인 개발 판단 자료이며 제품 구현·Fable redraft·public 게시 승인을 포함하지 않음 | 사용자 목적과 승인 경계를 분리하고 과거 `implementationApproved: true` 표기가 제품 구현 승인으로 오해되지 않게 하기 위함 | 23장~25장, TASK-USER-FLOW-001 Change 003 |
| 2026-07-16 | Fable 정책과 USER-FLOW 결과를 대표 clone에 branch별로 선별 보존하고 두 임시 worktree를 일반 제거 | 앞으로 일반 작업은 대표 폴더 한 곳에서 branch만 전환하고 디자인 5176 폴더만 별도로 유지하기 위함. Local commit은 허용하되 push·PR·merge·branch 삭제는 제외 | AGENTS.md, 23장~25장, TASK-GOV-CODEX-002 Change 012 |
| 2026-07-16 | Governance 정책은 P2 보정본 독립 재검증 뒤 먼저 merge하고 USER-FLOW는 최신 main에서 Fable redraft·내용 review·독립 검증 뒤 별도 merge | Generic Fable 계약을 먼저 canonical main에 고정한 뒤 기존 USER-FLOW 전문을 사용자 결정과 일치하게 Fable이 직접 다시 작성하고 두 게시 단위를 분리하기 위함 | AGENTS.md, CLAUDE.md, 23장~25장, TASK-GOV-CODEX-002 Change 013, TASK-USER-FLOW-001 |
| 2026-07-16 | TASK-USER-FLOW-001 Change 004의 Fable redraft를 실행하고 개인 개발 판단 자료로 내용 review | Canonical·온보딩·Phase B·전수 갱신 의무를 제거하고 병렬 업무 단위·최소 vertical slice·복구 질문·성공 신호를 보강하되 권고를 제품 구현 승인과 분리하기 위함 | 23장~25장, TASK-USER-FLOW-001 Change 004 |
| 2026-07-16 | TASK-USER-FLOW-001 독립 검증 P2를 보정하고 재검증·별도 merge 승인 완료 | Roadmap 23절의 오래된 공통 서문이 USER-FLOW planning·review·문서 승인과 충돌해 이를 후속 기능 미승인 상태와 분리했고, 재검증에서 Open P0/P1/P2/P3 `0/0/0/0`, publication `GO`를 확인했기 때문 | 23장~25장, TASK-USER-FLOW-001 Change 004 |
| 2026-07-16 | TASK-USER-FLOW-001 Ready PR #55의 CI 3/3과 squash merge를 완료하고 closure 상태를 동기화 | 다음 Task가 이미 끝난 게시 Gate를 다시 대기 상태로 읽지 않게 하고 canonical Next Gate를 TASK-007A Fable deep-interview로 전환하기 위함. 제품 구현·Phase B·branch 삭제는 포함하지 않음 | 23장~25장, TASK-USER-FLOW-001 Change 004 |
| 2026-07-18 | TASK-EXPORT-002 선택 프로젝트 Excel 내보내기를 experiment fast-track으로 진행 | 기존 TASK-EXPORT-001의 filter 결과 전체 export를 보존하면서 사용자가 명시적으로 선택한 프로젝트 subset만 파일로 만드는 신규 능력을 검증하고, canonical TASK-007A·대표 repo·main·Persistent UAT·게시 경계를 유지하기 위함 | 19장·23장~25장, TASK-EXPORT-002 |
| 2026-07-18 | TASK-EXPORT-002를 experiment branch에 구현하고 자동 검증 완료·사용자 검수 대기로 전환 | 선택 3건 중 2건 workbook, 전부-or-전무 권한/scope 차단, additive audit migration, desktop·390px·실제 Excel screenshot을 확인했으며 canonical queue와 게시 경계를 그대로 유지하기 위함 | 19장·23장~25장, TASK-EXPORT-002 구현 보고서 |
| 2026-07-18 | TASK-EXPORT-001 Change 002로 전 조회 화면의 Excel action을 선택 내보내기 하나로 통합 | 사용자가 같은 의미의 전체·선택 export button 두 개를 보지 않게 하고, 현재 목록 전체 선택도 별도 action이 아닌 checkbox로 제공하면서 업무 12개·관리자 8개 화면의 권한·scope·audit 계약을 공통화하기 위함 | 19장·23장~25장, TASK-EXPORT-001 Change 002 구현 보고서 |
| 2026-07-19 | TASK-SALES-KPI-001의 실적은 발행일·금액이 확정된 세금계산서만 사용하고 예상 파이프라인은 달성률에서 분리 | 영업 Home과 전용 화면의 금액 판단을 일치시키면서 수주 예상액이 실제 달성률을 과장하지 않게 하고, 월 목표 수정은 시스템 관리자 CAS·audit로 제한하기 위함 | 23장~25장, TASK-SALES-KPI-001 |
| 2026-07-19 | TASK-ADMIN-002는 고정 6종 양식과 Draft→Active→Archived lifecycle, 시스템 관리자 지정 부서장·현재 부서 fence를 사용 | code 없는 양식 변경을 허용하되 임의 workflow 확장·과거 snapshot 변경·부서 이동 뒤 과권한을 차단하고 새 실행에만 새 Active version을 적용하기 위함 | 23장~25장, TASK-ADMIN-002 |
| 2026-07-19 | 영업 그래프는 월 actual·target grouped bar와 경과 월 attainment line·100% 기준선을 사용하고 mobile에도 동일한 12개월 graph를 제공 | 단순 목표 금액선과 4×3 월 block을 제거해 월별 gap·달성 여부·연속 추세를 직접 판단하고 미래 월 0% 오해를 막기 위함 | 23장~25장, TASK-SALES-KPI-001 Change 002 |
| 2026-07-19 | 모바일 기본 화면은 현장 판단·처리를 우선하고 Excel·대량 관리·중복 Home widget을 제외하며 DESIGN-000 semantic token과 공통 primitive를 사용 | PC 기능을 전부 복제하지 않고 390px에서 핵심 정보 밀도와 조작성을 높이면서 desktop 기능과 서버 권한을 보존하기 위함 | 3장·23장~25장, TASK-MOBILE-002 Change 004, DESIGN-000 |
| 2026-07-19 | 모바일 도형은 배열 순환이 아니라 `surface/control/active/status/count/warning/success` 의미로 결정 | 같은 상태를 같은 모양으로 학습할 수 있게 하고 차단·완료·선택 신호가 목록 순서에 따라 바뀌지 않게 하기 위함 | 3장·23장~25장, TASK-MOBILE-002 Change 005, DESIGN-000 |
| 2026-07-19 | 선택 Excel 컬럼은 client header가 아니라 화면·사용자별 server effective registry의 key 부분집합으로 받고 필수 식별 컬럼을 잠근다 | metadata·요청 검증·workbook·민감 매출 audit의 권한 drift를 막고 stale·조작 요청을 silent fallback 없이 차단하면서 기존 미전달 client를 보존하기 위함 | 19장·23장~25장, TASK-EXPORT-001 Change 003 |
| 2026-07-19 | experiment standing instruction 아래의 “다음 작업 시작”은 완료 원장의 첫 번째 이름 있는 미완료 제품 Task 실행 지시이며 비차단 정책은 Fable 2-pass 권장안으로 자동 채택 | Change 014 fast-track보다 나중에 추가된 정책 입력 문구가 재승인 질문을 만든 회귀를 제거하고, 일반 branch·대표 repo·main·Persistent UAT·provider·destructive operation 경계를 보존하면서 실험 개발 속도를 유지하기 위함 | AGENTS.md, 12장, 23장~25장, 27-experiment-task-ledger, TASK-GOV-CODEX-002 Change 016 |
| 2026-07-19 | Pending system semantic code와 사용자-facing catalog를 분리하고 system administrator 전용 관리·server-generated custom code·CAS·inactive lifecycle을 사용 | 자동 Pending 의미와 과거 issue를 보존하면서 코드 수정 없는 표시·등록 정책 관리, 동시 관리자 stale write 차단과 목록·상세·filter·선택 Excel label 통일을 함께 달성하기 위함 | 23장~25장, 27-experiment-task-ledger, TASK-PENDING-TYPE-001 |
| 2026-07-20 | TASK-QR-001은 패널별 명시 발급·활성 QR 1개·256-bit opaque token·인증 scan landing·현재 단계 담당자 routing·관리자 사유 rotation을 사용 | QR을 인증수단이나 자동 상태 변경 수단으로 쓰지 않으면서 현장 재진입을 단순화하고, raw token의 log·audit 노출과 폐기 QR의 업무정보 노출을 차단하기 위함 | 8장, 23장~25장, 27-experiment-task-ledger, TASK-QR-001 |
| 2026-07-20 | TASK-NOTIFY-AUDIT-001은 기존 append-only preference 원장을 수정하지 않고 관리자가 현재 계정 기준으로 조회·요약·선택 Excel 보존 | 과거 조직 snapshot을 임의 추정하지 않으면서 DB 직접 조회 없이 설정 문의와 변경 책임을 확인하고, 목록·요약·Excel 필터 drift를 막기 위함 | 23장~25장, 27-experiment-task-ledger, TASK-NOTIFY-AUDIT-001 |
| 2026-07-20 | TASK-NOTIFY-REPROCESS-001은 terminal Failed delivery의 total attempt를 보존하고 generation별 retry budget만 초기화하며 사유·duplicate-risk 확인·CAS·append-only event를 필수화 | provider exactly-once가 보장되지 않는 경계에서 무한/부분/중복 재처리를 억제하고 같은 delivery의 전체 계보를 감사 가능하게 유지하기 위함 | 6장, 23장~25장, 27-experiment-task-ledger, TASK-NOTIFY-REPROCESS-001 |
| 2026-07-21 | 검사 Pending 조치 완료는 종결이 아니라 동일 transaction의 재검사 업무·정/부 알림 생성으로 정의하고, 재검사 합격만 Pending·검사·workflow를 종결 | 실제 IQC 입력이 Pending에서 중단되거나 수동 종결·중복 요청으로 부분 상태가 생기는 문제를 제거하고 내 업무에서 정확한 검사까지 연속 이동하기 위함 | 8장, 23장~25장, 27-experiment-task-ledger, TASK-WORKFLOW-CONTINUITY-001 |
| 2026-07-21 | TASK-WORKFLOW-CONTINUITY-001 Change 002에서 구매·자재·IQC를 같은 구매 품목 identity와 도착분별 검사 회차로 표시 | 사급·도급을 빠르게 구분하고 한 품목의 분할 도착과 IQC 진행을 한 행에서 추적하면서, 별도 IQC 요청이나 복제 data로 인한 누락·불일치를 막기 위함 | 11장~13장, 23장~25장, 27-experiment-task-ledger, TASK-WORKFLOW-CONTINUITY-001 Change 002 |
| 2026-07-21 | TASK-WORKFLOW-CONTINUITY-001 Change 003에서 구매 신규·변경은 자재 정/부 업무로 인계하고 도착 저장은 IQC 생성 postcondition을 확인하며 기존 누락 도착은 검사함에서 idempotent 복구 | 성공 문구와 실제 업무 생성의 불일치를 없애고, 부서 전역 화면을 프로젝트 우선으로 통일하면서 부분 실패 data도 사용자가 별도 요청 없이 회복하기 위함 | 11장~13장, 23장~25장, 27-experiment-task-ledger, TASK-WORKFLOW-CONTINUITY-001 Change 003 |
| 2026-07-21 | TASK-WORKFLOW-CONTINUITY-001 Change 004에서 발주 수량·단위는 구매팀 입력으로 고정하고, 권한 fallback이 관리자·조회전용이 아니라 자재 역할 사용자를 우선하며, 도착 등록은 실제 IQC 수신자를 확인한 뒤 같은 흐름으로 인계 | 자재 담당자가 구매 수량을 대신 입력하는 책임 혼선을 없애고, 구매 저장은 성공했지만 자재 사용자의 내 업무·알림에 보이지 않거나 도착분이 IQC에서 누락되는 거짓 성공을 차단하기 위함 | 11장~13장, 23장~25장, 27-experiment-task-ledger, TASK-WORKFLOW-CONTINUITY-001 Change 004 |
| 2026-07-21 | TASK-WORKFLOW-CONTINUITY-001 Change 005에서 comment·상태 이력을 Pending 하단 전체 폭으로 합치고 조치 완료→품질 재검사 업무·알림→불합격 재조치 알림·업무 재개 또는 합격 해제 순환을 같은 Pending으로 유지하며 Pending 상세 바로가기·업무/알림 식별·재검사 전용 검사함·해제 조건 안내를 함께 제공 | 오른쪽 보조 열에서 comment를 찾기 어렵고 재검사 업무가 일반 IQC와 구분되지 않으며 해제 경로와 불가 사유를 찾기 어렵던 문제를 없애고 품질 담당자가 정확한 회차에서 조치 근거를 보고 판정하게 하기 위함 | 8장, 23장~25장, 27-experiment-task-ledger, TASK-WORKFLOW-CONTINUITY-001 Change 005 |
| 2026-07-21 | TASK-010A Change 003에서 키팅 완료를 선택형 준비 알림으로 분리하고 생산관리의 패널별 제조 투입 요청만 제조 정·부 내 업무·인앱 알림을 생성 | 전체 키팅 없이 패널별 제조를 시작하거나 키팅을 나중에 알리는 실제 현장 흐름을 허용하면서도 투입 시점과 업무 생성 책임을 생산관리가 명시적으로 통제하고 중복 업무를 방지하기 위함 | 4장·9장·12장, 23장~25장, 27-experiment-task-ledger, TASK-010A Change 003 |
| 2026-07-21 | TASK-010A Change 004에서 생산관리 전역 화면을 `생산계획`과 `제조 투입` 두 업무 탭으로 분리 | 일정·담당자 관리와 실제 패널 투입 요청이 같은 확장 영역에 섞여 사용자가 action 목적을 구분하기 어렵던 문제를 해소하고 Mobile에서도 목적별 핵심 정보만 노출하기 위함 | 4장·9장·12장, 23장~25장, 27-experiment-task-ledger, TASK-010A Change 004 |
| 2026-07-21 | TASK-NOTICE-BOARD-001을 사용자 직접 요청으로 현재 실험 순서에 삽입하고 Home 하단 병목 widget만 공지사항 게시판으로 교체 | 프로젝트 수가 늘수록 고정된 병목 Top 5의 대표성이 낮아지는 공간을 전사 공통 정보 공유에 사용하되, 상단 부서 KPI와 중앙 업무 요약 및 프로젝트 자체 병목 계산은 보존하기 위함 | 23장~25장, 27-experiment-task-ledger, TASK-NOTICE-BOARD-001 |
| 2026-07-21 | TASK-NOTICE-BOARD-001 experiment fast-track 완료. 전용 persistence/API, Home 최신 5건, 목록·상세·작성·author-only soft delete와 desktop/mobile 검증을 종료 | Fable 2-pass 최종 계약의 blocking decision 0, Backend 418/418·Frontend 119/119·isolated Full-Stack 1/1과 Open P0/P1/P2 0을 확인했기 때문 | docs/38~40, tasks/notice-board-001-* |
| 2026-07-22 | TASK-UL891-SET-001은 UL891 신규 프로젝트를 세트 사양 version·주문 instance·개별 physical panel 계층으로 생성하고, 선택 증감·부분출하·발주 회수·프로젝트×출하 달력월 발행요청을 사용 | 동일 사양 반복 주문의 공통 이름·규격과 실제 panel별 제조·검사·FAT·QR·출하 원자를 함께 보존하고, 월이 바뀐 부분출하 청구와 발주 후 수량 감소 회수를 누락 없이 추적하기 위함 | 8장·13장·19장·23장~25장, docs/41, TASK-UL891-SET-001 |
| 2026-07-22 | TASK-UL891-SET-001 Change 002에서 프로젝트 상세는 조회 중심으로 보존하고 패널 상세를 7개 탭의 패널 업무 허브와 기존 담당 workspace exact deep link로 완성 | 패널별 실행 데이터를 한곳에서 조회하되 mutation form·권한·검증을 복제하지 않고, 패널 귀속이 없는 구매·입고·IQC를 프로젝트 공통으로 명확히 구분하기 위함 | 8장·13장·23장~25장, docs/41 9.3, TASK-UL891-SET-001 Change 002 |
| 2026-07-22 | TASK-UL891-SET-001 Change 003에서 프로젝트 상세의 영업 기본정보 중복과 자재 탭을 제거하고, 영업 탭을 영업팀 전용 마지막 탭으로 제한하며 구매 입고확정 요약과 제조·품질·물류 패널 현황을 제공 | 프로젝트 공통 정보는 한 번만 보여 주고 비담당 부서는 필요한 인계 결과만 확인하게 하면서, 패널 실행 부서는 38개 개별 패널의 현재 상태와 해당 패널 상세 업무 문맥으로 즉시 이동하게 하기 위함 | 8장·13장·23장~25장, docs/41 9.3, TASK-UL891-SET-001 Change 003 |
| 2026-07-22 | IQC는 구매품목 도착분, OQC·전진검수·FAT는 개별 패널을 처리 단위로 고정하고, OQC만 단계별 판정이며 전진검수·FAT는 패널당 통합 판정 1회로 정의 | 구매 입고검사와 완성 패널 검사를 섞지 않고 프로젝트 상세 진척률의 분모·재검사 대상을 실제 업무 단위와 일치시키기 위함. 현재 전진검수·FAT 체크리스트 구현은 `012A-AGGREGATE-DECISION` OPEN P2로 추적 | 4장·13장·23장~25장, TASK-012A Change 003 |
| 2026-07-22 | TASK-UL891-SET-001 Change 004에서 프로젝트 상세 제조·품질·물류를 `No·패널명·핵심정보·진행률` 구조와 부서별 완료 면수 KPI로 통일 | 제조는 실행 단계, 품질은 OQC 항목+전진검수+선택 FAT, 물류는 포장+출발+납품의 실제 완료 단위로 패널 및 프로젝트 전체 진척을 같은 화면에서 비교하기 위함 | 8장·13장·23장~25장, TASK-UL891-SET-001 Change 004 |
| 2026-07-22 | DESIGN-000 Change 001에서 상태 표시 외 제품 전체를 black & white·무그림자·사각형 wireframe으로 전환 | 색은 사용자의 성공·주의·오류·진행 판단에만 쓰고 구조 계층은 1px 선과 흑백 명도로 표현해 화면별 장식 색·rounded card drift를 제거하기 위함 | 23장~25장, DESIGN-000 Change 001 |
| 2026-07-22 | TASK-012A Change 004에서 LQC·OQC를 Checklist, 전진검수·FAT를 Aggregate로 고정하고 모든 품질 부적합과 재검사 적합을 같은 Pending 수명주기로 연결 | 단계 수와 무관하게 부적합은 조치 업무로 넘기고, 적합 재검사에서 검사 결과와 Pending 차단이 같은 transaction으로 해제되게 하며 기존 finalized 성적서는 보존하기 위함 | 4장·13장·23장~25장, TASK-012A Change 004, TASK-WORKFLOW-CONTINUITY-001 Change 006 |
| 2026-07-22 | TASK-UL891-SET-001 Change 005에서 프로젝트 상세 8개 탭에 공통 content container와 desktop·390px spacing을 적용 | 탭 전환마다 제목·KPI·목록 시작선과 콘텐츠 폭이 흔들리지 않게 하고 프로젝트 상세 전체를 같은 시각 rhythm으로 읽게 하기 위함 | 8장·23장~25장, TASK-UL891-SET-001 Change 005 |
| 2026-07-22 | TASK-E2E-FULL-SUITE-001 Change 008에서 실제 역할 lifecycle을 최신 구매·선택 키팅·생산관리 제조 투입·Aggregate 품질·현재 탭·달력월 정산 계약으로 갱신 | 과거 정책을 기대하는 테스트 때문에 정상 제품을 실패로 판정하지 않고 현재 확정 정책을 끝까지 실행하는 회귀 기준선을 유지하기 위함 | 23장~25장, TASK-E2E-FULL-SUITE-001 Change 008 |
| 2026-07-23 | TASK-E2E-FULL-SUITE-001 Change 009에서 고정 실험 검수 runtime의 macOS 더블클릭 통합 launcher를 제공 | server 종료 때마다 Codex에 재기동을 요청하지 않고 사용자가 직접 Frontend·Backend를 함께 켜되, 기존 DB·strict port·process ownership과 다른 runtime 보호를 유지하기 위함 | 23장~25장, 27-experiment-task-ledger, TASK-E2E-FULL-SUITE-001 Change 009 |
| 2026-07-24 | TASK-WORKFLOW-CONTINUITY-001 Change 009에서 패널 제조 시작과 LQC를 동시에 열고, 같은 패널의 제조·LQC 공동 완료→OQC, OQC→전진검수·필수 FAT 병행, 최종 품질→패널별 포장·출발·납품으로 연결 | LQC가 제조 중 단계검사라는 현장 의미와 개별 physical panel·부분출하 확정 정책을 실제 업무 생성 조건에 반영하고, 프로젝트 전체 완료가 다른 패널의 다음 단계와 개별 증빙 출하를 막지 않게 하기 위함 | 4장·13장·23장~25장, 27-experiment-task-ledger, TASK-WORKFLOW-CONTINUITY-001 Change 009 |
| 2026-07-24 | TASK-WORKFLOW-CONTINUITY-001 Change 010에서 프로젝트 18단계를 실제 구매품목·패널 상태로 집계하고 `부분 완료`를 도입하며, 선택형 키팅은 패널별 `키팅 완료 OR 생산관리 제조 투입 요청`, 과거 누락 품질 인계는 검사함 진입 시 멱등 재조정 | 완료 event가 없는 부분 LQC와 제조 투입 준비를 `미시작`으로 오표시하고 새 인계 로직 적용 전 완료 패널이 OQC 없이 잔류하는 문제를 해소하기 위함 | 4장·13장·23장~25장, 27-experiment-task-ledger, TASK-WORKFLOW-CONTINUITY-001 Change 010 |
| 2026-07-24 | TASK-WORKFLOW-CONTINUITY-001 Change 011에서 LQC·OQC 체크리스트 응답과 판정을 한 finalize transaction으로 확정하고 dialog 내부 상세 오류·중복 클릭 차단·409 복구를 제공 | 별도 응답 저장 성공 뒤 판정 거절이 version drift와 반복 409를 만들고 오류가 dialog 뒤에 숨던 사용자 검수 실패를 없애며, 거절 시 응답·version·인계를 모두 롤백하기 위함 | 4장·13장·23장~25장, 27-experiment-task-ledger, TASK-WORKFLOW-CONTINUITY-001 Change 011 |
| 2026-07-24 | TASK-WORKFLOW-CONTINUITY-001 Change 012에서 열린 Pending 코멘트를 업무 부서 전체에 허용하고 LQC·OQC 재검사를 직전 부적합 항목으로 제한하며 제조 실행 뒤 누락된 LQC 업무를 멱등 복구 | 타 부서의 협업 정보가 기록되지 않고 적합 항목까지 반복 검사하며 과거 완료 패널이 LQC 검사함에서 누락되는 사용자 검수 실패를 해소하되 조회 전용·상태 전이·품질 판정 권한은 확대하지 않기 위함 | 4장·8장·13장·23장~25장, 27-experiment-task-ledger, TASK-WORKFLOW-CONTINUITY-001 Change 012 |
| 2026-07-24 | TASK-WORKFLOW-CONTINUITY-001 Change 013에서 LQC·OQC 재검사 합격 뒤 최초 적합과 재검사 적합을 항목별 최신 유효 결과로 합성하고, 포장·출발·납품은 증빙 선첨부 뒤 한 번의 저장으로 확정하며 미완료 draft를 목록에서 자동 복구 | 재검사 대상 한 항목만 최종 결과로 남아 전체 단계가 1/1로 축소되고, 물류 draft 생성 뒤 화면을 이탈하면 대상이 queue에서 빠져 확정 경로를 잃는 사용자 검수 실패를 원본 이력·CAS·권한 계약을 유지한 채 해소하기 위함 | 4장·13장·23장~25장, 27-experiment-task-ledger, TASK-WORKFLOW-CONTINUITY-001 Change 013 |
| 2026-07-26 | TASK-WORKFLOW-CONTINUITY-001 Change 014에서 프로젝트 상세 진행률을 전체 흐름 진행률로 단일화하고, 일반 구매품 수량·단위는 선택값으로 유지하면서 공급유형별 구매 완료 조건과 required template match를 ProjectStore·WorkflowStore에 동일 적용 | 상세 초기 4단계 계산과 전체 흐름 전체 단계 계산이 달랐고, 일반 구매품 수량·단위 필수 판정과 프로젝트 요약의 품목명-only 판정이 확정 구매 정책과 서로 어긋난 결함을 해소하기 위함 | 4장·7장·11장, 27-experiment-task-ledger, TASK-WORKFLOW-CONTINUITY-001 Change 014 |
| 2026-07-26 | TASK-WORKFLOW-CONTINUITY-001 Change 015에서 상세 IQC·LQC·OQC 판정을 항목 결과에서 자동 도출하고 재검사 부적합을 같은 Pending의 `조치 요청`·업무 `시작 전`으로 되돌리며 자재 입고 확정 업무를 한 줄로 요약 | 모순된 판정 선택 노출, 조치 요청을 건너뛴 직접 조치 중 전이, 과도하게 긴 입고 확정 업무 상세를 기존 권한·이력·정/부 알림·멱등 계약 안에서 해소하기 위함 | 8장·13장·23장~25장, 27-experiment-task-ledger, TASK-WORKFLOW-CONTINUITY-001 Change 015 |
| 2026-07-26 | TASK-WORKFLOW-CONTINUITY-001 Change 016에서 포장 묶음은 유지하되 출발·납품 batch의 실제 membership과 queue를 패널별로 전환하고 기존 unit 기록을 backfill | 같은 Packing Unit의 모든 패널을 함께 출발·납품해야 했던 결함을 해소해 부분 출하·부분 납품을 허용하면서, 선택하지 않은 패널의 업무·상태·증빙과 마지막 패널 정산 인계를 보존하기 위함 | 4장·13장·17장·23장~25장, 27-experiment-task-ledger, TASK-WORKFLOW-CONTINUITY-001 Change 016 |
| 2026-07-30 | TASK-WORKFLOW-CONTINUITY-001 Change 017에서 일반 IQC 상태·정확한 내 업무 이동·Pending 참조 알림·`생산관리 / 제조 요청` 단계와 최초 입고 후 제조 투입/키팅 인계를 보정 | 정상 업무의 긴급 오표시와 일반 프로젝트 이동, 주요 부서의 Pending 누락, 자재 도착과 제조 투입 사이의 끊긴 인계를 기존 권한·중복 방지·실제 provider 경계 안에서 해소하기 위함 | 4장·8장·13장·23장~25장, 27-experiment-task-ledger, TASK-WORKFLOW-CONTINUITY-001 Change 017 |
| 2026-07-30 | TASK-ATTACHMENT-001에서 Pending 조치 사진을 담당자 전용 Draft→회차별 확정 근거로 저장하고 IQC·LQC·OQC 재검사에 최초 부적합→조치→판정 흐름으로 연결 | 조치 실물 근거의 외부 유실을 막고 재검사 담당자가 원 부적합과 조치 결과를 한 화면에서 대조하되, 확정 증빙 불변·프로젝트 접근 scope·사진 없는 기존 흐름을 보존하기 위함 | 8장·13장·23장~25장, 27-experiment-task-ledger, TASK-ATTACHMENT-001 |
| 2026-07-26 | DESIGN-000 Change 002에서 전 부서 입력을 `대상 확인 → 값 입력 → 저장` 순서, 번호 section, 한 번 선택 control과 하단 action bar로 통일 | 기존 기능·권한·API·상태 전이를 바꾸지 않고도 화면마다 달랐던 입력 시작·선택·저장 위치를 같은 방식으로 학습하고 Mobile에서는 한 열로 간단히 처리하게 하기 위함 | 23장~25장, 27-experiment-task-ledger, DESIGN-000 Change 002 |
| 2026-07-27 | DESIGN-000 Change 003에서 다중 업무 부서는 업무 선택 전용 화면, 업무별 KPI·프로젝트 목록, 단일 프로젝트 입력으로 나누고 제조·패널 품질검사의 패널 탐색을 왼쪽 세로 목록으로 통일하며 Pending은 KPI·프로젝트 목록과 한 프로젝트 상세로 단순화 | 업무와 전체 프로젝트가 한 화면에 섞여 KPI 기준과 현재 입력 대상이 불명확했던 문제와 Pending의 중복 선택·프로젝트 응답 혼입 가능성을 기존 기능·권한·API·상태 전이를 바꾸지 않고 해소하기 위함. UX/UI 평가는 사용자 지시에 따라 PC만 수행 | 23장~25장, 27-experiment-task-ledger, DESIGN-000 Change 003 |
| 2026-07-27 | DESIGN-000 Change 004에서 PC 평가서의 첫 화면 목록·상세 탭·조회 전용·다중 선택·영업 Home·생산계획·양식 관리 P1/P2 UX를 공통 component와 compact layout으로 보정 | 새 기능이나 업무 정책을 바꾸지 않고 사용자가 실제 목록과 당일 행동을 먼저 보고 권한·선행조건·현재 입력 대상·다중 선택·조회/편집 상태를 즉시 구분하게 하기 위함 | 23장~25장, 27-experiment-task-ledger, DESIGN-000 Change 004 |
| 2026-07-28 | DESIGN-000 Change 005에서 물류·정산 좁은 입력 header를 container 기준으로 재배치하고 검은 wireframe 표면이 내부 글자색을 함께 소유하게 보정 | 좁은 2열 안에서 제목 폭이 사라지는 문제와 검은 배경 위 검은 글자가 남는 cascade drift를 기능·상태·권한 변경 없이 제거하기 위함 | 23장~25장, 27-experiment-task-ledger, DESIGN-000 Change 005 |
| 2026-07-28 | TASK-E2E-FULL-SUITE-001 Change 010에서 일반·12면 stress lifecycle을 현재 업무 선택·프로젝트 우선 목록·파생 품질 판정·물류 1회 확정·정산 저장 UI로 갱신 | 삭제된 과거 control을 기대해 정상 제품을 실패로 판정하던 회귀 drift를 없애고 현재 확정 사용자 동선을 계속 완주 검증하기 위함 | 23장~25장, 27-experiment-task-ledger, TASK-E2E-FULL-SUITE-001 Change 010 |
| 2026-07-24 | TASK-UL891-SET-001 Change 006에서 종결된 품질 Pending 연결을 현재 차단 표시에서 제외하고 제조·품질·물류 목록에 실제 현재 단계 열을 추가 | OQC 합격 패널이 Pending으로 오표시되고 OQC 부적합 패널이 미래 전진검수·FAT 대기처럼 보이는 사용자 검수 실패를 없애며, 핵심정보를 재해석하지 않고 부서별 현재 단계를 바로 확인하게 하기 위함 | 8장·13장·23장~25장, 27-experiment-task-ledger, TASK-UL891-SET-001 Change 006 |
| 2026-07-27 | TASK-UL891-SET-001 Change 007에서 신규 UL891 프로젝트 상세 설계를 조회 전용으로 만들고 중복 평면 설계를 제거하며 별도 수정 화면과 `임시저장`·`저장` feedback을 제공 | 세트 공통 사양과 일반 패널 설계가 같은 화면에서 중복 입력처럼 보이고 내부 저장 용어·결과 안내가 불명확했던 사용성 문제를 Backend 계약과 QR 기능 변경 없이 해소하기 위함 | 8장·23장~25장, 27-experiment-task-ledger, TASK-UL891-SET-001 Change 007 |
| 2026-07-27 | TASK-UL891-SET-001 Change 008에서 최종 저장을 `현재 form Draft 갱신 → Publish`로 직렬화하고 규격을 UL891 Publish·설계 완료 조건과 사용자 화면에서 제거 | 임시저장을 먼저 누르지 않으면 기존 빈 Draft가 검증되고, 확정 입력값이 아닌 규격 때문에 저장·제조 인계가 막히던 사용자 검수 실패를 기존 API·DB 호환성을 보존하면서 해소하기 위함 | 8장·13장·23장~25장, 27-experiment-task-ledger, TASK-UL891-SET-001 Change 008 |
| 2026-07-24 | TASK-MANUFACTURING-BATCH-001에서 기존 선택 Excel checkbox로 같은 프로젝트의 제조 진행 패널을 골라 조립 의미 단계 한 건을 일괄 확인하고, 전체 흐름의 완료 중심 네 문구를 행동 중심 표시명으로 단순화 | 실제 여러 패널을 한 번에 조립하고 시스템 입력을 나중에 하는 현장 흐름의 반복 입력을 줄이되, 다른 제조 단계·제조 완료·LQC/OQC는 패널별 불변조건으로 남기고 다중 변경의 부분 저장을 막기 위함 | 4장·13장·23장~25장, docs/42, TASK-MANUFACTURING-BATCH-001 |
| 2026-07-24 | TASK-MANUFACTURING-BATCH-001 Change 002에서 선택 패널의 조립 단계 한 건만 일괄 완료하고 조립 전·후 다른 제조 단계는 미완료로 보존 | “조립 단계까지” 누적 완료가 중간 제조 입력을 지우는 오해를 바로잡고, 실제 조립을 한꺼번에 처리했더라도 다른 제조 기록은 담당자가 나중에 단계별로 입력할 수 있게 하기 위함 | TASK-MANUFACTURING-BATCH-001 Change 002 |
| 2026-07-28 | TASK-MANUFACTURING-BATCH-001 Change 003에서 제조 양식의 일반/조립 구분을 제거하고 모든 제조 단계 중 원하는 단계 한 건을 선택 패널에 일괄 완료 | Change 002가 “한 단계만 완료”를 “조립 단계만 완료”로 잘못 좁힌 사용자 검수 실패를 바로잡고, 앞뒤 단계·제조 완료·LQC/OQC를 보존한 채 실제로 함께 끝낸 각 제조 단계를 모두 일괄 입력할 수 있게 하기 위함 | TASK-MANUFACTURING-BATCH-001 Change 003 |
| 2026-07-28 | TASK-PRODUCTION-CONTROL-001 Change 006에서 생산계획 OQC 실적 연결을 세부 검사항목이 아닌 패널별 최종 `OQC 합격` 한 건으로 변경 | OQC 내부 단계별 판정·Pending·재검사는 품질 성적서에 남기되 생산관리 일정에서는 전진검수·FAT처럼 최종 합격 여부만 연결해 불필요한 항목 선택을 없애기 위함. 현재 양식만 aggregate로 정리하고 기존 프로젝트 세부 snapshot은 보존 | 10장·13장·23장~25장, TASK-PRODUCTION-CONTROL-001 Change 006 |
| 2026-07-28 | TASK-PRODUCTION-CONTROL-001 Change 007에서 계획 대비 실적 헤더를 밝은 중립색으로 바꾸고 계획·실적 일정표에 날짜 축과 세로 기준선을 추가 | 검은 헤더의 낮은 가독성을 해소하고 계획·실적 막대의 위치를 실제 날짜 기준으로 바로 해석할 수 있게 하기 위함. 원본 일정·실적 계산과 업무 상태는 변경하지 않음 | 10장·13장·23장~25장, TASK-PRODUCTION-CONTROL-001 Change 007 |
| 2026-07-28 | TASK-PRODUCTION-CONTROL-001 Change 008에서 생산계획 항목마다 담당자·필요 인원·생산관리 코멘트를 기록하고 조회를 8열 `생산계획표`로 변경 | 계획 담당 인력과 현장 코멘트를 프로젝트 일정 항목에서 바로 확인하되 담당자 metadata가 내 업무·알림·권한을 바꾸지 않게 하고, 내부 실적 연결 설정은 조회에서 숨겨 현장 정보 우선순위를 명확히 하기 위함 | 10장·13장·23장~25장, TASK-PRODUCTION-CONTROL-001 Change 008 |
| 2026-07-28 | TASK-UL891-PRODUCTION-PLAN-001에서 Ul891Set+LinkedV1 생산계획을 실제 실물 세트별 overlay로 분리하고 전체는 활성 세트 read-only 집계로 제공 | 세트마다 다른 생산 일정·담당 인력을 기록하면서도 프로젝트 공통 항목·실적 연결을 복제하지 않고, 제조 이후 실적은 해당 세트 패널만 계산해 프로젝트 14면과 세트 7면을 구분하기 위함 | 10장·13장·23장~25장, docs/44, TASK-UL891-PRODUCTION-PLAN-001 |
| 2026-07-27 | TASK-PRODUCTION-CONTROL-001에서 기존 프로젝트는 Legacy 또는 생성 당시 snapshot을 영구 유지하고, 유효한 Item별 단일 현재 제조·계획 양식 저장 뒤 생성되는 프로젝트만 `LinkedV1` snapshot과 부서 원본 기반 자동 실적을 사용 | 양식 변경이 진행 중 프로젝트를 소급 변경하지 않으면서 생산계획 항목을 자유롭게 교체하고, 생산관리 탭 하나에서 계획 시작·종료와 구매·자재·제조·품질·물류의 실제 진행 근거를 비교하기 위함 | 10장·14장·21장·23장~25장, docs/43, TASK-PRODUCTION-CONTROL-001 |
| 2026-07-29 | 실험 계보 사용자 검수 완료와 `main` merge 승인 `3/3`을 기록하고, 기존 공식 UAT DB를 보존 격리한 뒤 fresh 공식 DB와 experiment DB에 migration `0001`~`0064`를 적용해 Ready PR 승격을 진행 | 사용자가 기존 업무 데이터를 사용하지 않고 새로 시작하기로 확정한 상태에서 데이터 유출·혼입 없이 검증된 실험 기능을 공식 기준선으로 옮기되, Persistent UAT rollback과 실제 provider 비활성 경계를 보존하기 위함 | 23장~25장, 27-experiment-task-ledger, TASK-EXPERIMENT-PROMOTION-001 |
| 2026-07-29 | TASK-UAT-001 Change 005에서 Backend 업무 응답 cache를 전역 차단하고 Nginx HTML/asset cache와 보안 header 상속을 분리하며 Production·CI 외부 artifact를 digest/full SHA로 고정 | 공유 단말의 민감 응답 잔존, asset 응답의 보안 header 누락과 재실행 때 외부 artifact가 바뀌는 P2 위험을 닫고 검증된 공급망 기준을 재현하기 위함. Frontend 가변 package 설치는 고정 TLS validator로 대체했고 수정본이 없는 libxml2 Low 2건은 `SEC-PUBLIC-014` P3로 운영 handover 전에 재검사 | 23장~25장, TASK-UAT-001 Change 005 |
| 2026-07-30 | TASK-UAT-001 Change 006에서 Entra API·SPA 분리 app registration과 HTTPS 5174/Backend 5081 통합 실행을 복구 | 정상적인 분리 client ID를 오류로 거절하고 Vite가 명시한 5081 대신 candidate 5084를 강제하던 검수 실행 drift를 없애며, 기존 UAT DB·실제 identifier·provider를 보존한 사용자 검수 주소를 제공하기 위함 | 23장~25장, TASK-UAT-001 Change 006 |
| 2026-07-30 | TASK-UAT-001 Change 005·006 사용자 검수를 완료하고 PR #58의 `main` merge를 승인 | 실제 Microsoft 365 로그인, 주요 업무 조회·저장, 알림·내 업무와 로그아웃 cache 차단을 확인하고 열린 P0/P1/P2가 없는 공개 배포 보안 변경을 제품 기준선에 반영하기 위함. 실제 운영 domain·managed DB·SIEM handover는 별도 Gate로 유지 | 23장~25장, TASK-UAT-001 Change 005·006, PR #58 |
| 2026-07-31 | TASK-AZURE-PILOT-001에서 Azure 서비스 선정이 필요 없는 P1만 구현하고 hosting·managed DB·domain·WAF·SIEM·registry/OIDC·실제 rollback 선정은 사용자 결정까지 보류 | 파일럿 준비 속도를 유지하면서 미선정 provider를 코드로 임의 확정하지 않고, Entra API·SPA 분리·one-shot migration·preflight·GitHub 게시 후보를 서비스 중립 계약으로 먼저 검증하기 위함 | 23장~25장, TASK-AZURE-PILOT-001 |
| 2026-07-31 | TASK-AZURE-DEPLOY-001에서 20일 시범을 Front Door Standard·Container Apps Consumption·PostgreSQL B2s·ACR Basic·Azure Files·Key Vault·Log Analytics로 구성하고 비용 관련 Azure 실행은 사용자가 직접 담당 | 3개 프로젝트의 작은 사용량에 HA·Premium WAF·Blob을 넣지 않으면서 migration·restore·origin 보호와 최종 hostname을 실제 provider 계약으로 검증하고, 무료 credit과 비용 통제 권한을 사용자에게 유지하기 위함 | 23장~25장, TASK-AZURE-DEPLOY-001 |
| 2026-08-01 | TASK-AZURE-DEPLOY-001 Change 002에서 검증된 배포 코드의 Git merge와 Azure runtime에서만 실행 가능한 DB 복구·edge/인증·actual provider smoke를 분리 | 커밋된 배포 코드를 기준으로 Azure를 생성하되, 공개 traffic과 external notification은 세 `PRE_TRAFFIC_GATE` PASS 전에 fail-closed로 유지하기 위함 | 23장~25장, TASK-AZURE-DEPLOY-001 Change 002 |
| 2026-08-01 | Azure 비용 Gate를 보류하고 DESIGN-000 Change 006 Graphite 프론트엔드 승격을 먼저 수행 | 독립 실험에서 검증한 흑백 wireframe·프론트엔드 구성 통일·표 밀도·부서 accordion만 최신 main에 fixed allowlist로 이식하고 Backend·DB·배포와 원본 WIP를 보존하기 위함 | 23장~25장, DESIGN-000 Change 006 |
| 2026-08-01 | DESIGN-000 Change 006 사용자 검수를 완료하고 local `main` merge `f1f94ed`로 반영하되 원격 push·PR·배포는 보류 | 업무 선택 전용 page 삭제, 부서 행 click disclosure, 장식용 왼쪽 강조 rail 제거와 Graphite 표 밀도를 제품 기준선에 반영하고 다음 실행 순서를 TASK-AZURE-DEPLOY-001 비용 Gate로 복귀시키기 위함 | 23장~25장, 27-experiment-task-ledger, DESIGN-000 Change 006 |
| 2026-08-02 | TASK-UAT-001 Change 007에서 Pending 상세 mixed-version blank를 Frontend section 격리로 복구하고 current-source Backend handover는 보류 | live UAT ledger 64개에 source의 조치 사진 schema가 없어 승인되지 않은 migration을 실행하지 않으면서도 제목·발생 내용·담당·기한·이력을 즉시 복구하고, 조치 사진 활성화는 별도 controlled migration·5081 handover로 분리하기 위함 | 23장~25장, TASK-UAT-001 Change 007 |
| 2026-08-02 | TASK-UAT-001 Change 007 Pending 상세 복구의 사용자 검수를 완료 | 실제 5174에서 목록·상세·복귀 동선과 핵심 상세 표시를 확인해 blank 복구를 닫되, 사용자 검수 완료를 Git 게시나 조치 사진 migration 승인으로 확대하지 않기 위함 | 23장~25장, TASK-UAT-001 Change 007 |
| 2026-08-02 | TASK-UAT-001 Change 007을 구현 commit `db9cb34`와 local `main` fast-forward로 병합 | 사용자 검수 완료 뒤 승인된 Pending 상세 복구를 local 제품 기준선에 반영하되 Push·PR·remote merge·배포와 조치 사진 migration은 별도 승인 경계로 유지하기 위함 | 23장~25장, TASK-UAT-001 Change 007 |
| 2026-08-04 | 5174 local `main`을 현재 제품 기준선으로 확정하고 Azure Change 003~005가 먼저 반영된 원격 `main`과 `TASK-EXPERIMENT-PROMOTION-001 Change 002`에서 통합한 뒤 Ready PR·CI·merge를 실행 | Graphite UI·Pending 상세 복구와 Azure 보안·배포 변경 어느 쪽도 덮어쓰지 않고 하나의 원격 기준선으로 만든 뒤, 5175 UL891 변경과 통합 source 재배포를 그 기준선 위에서 순서대로 수행하기 위함 | 23장~25장, 27-experiment-task-ledger, TASK-EXPERIMENT-PROMOTION-001 Change 002 |
| 2026-08-04 | `TASK-EXPERIMENT-PROMOTION-001 Change 003`에서 5175의 UL891 사용자 수정분만 통합 원격 `main` 기준선에 이식하고 Graphite·Azure·Pending 복구를 보존한 전체 회귀 후보를 생성 | 오래된 기준선의 branch를 통째 병합해 현재 디자인·기능을 되돌리지 않고, 전체 세트 기본계획·일정표 가독성·담당자·단일 현재 설계·중복 사양·활성 42면을 migration `0068`과 함께 현재 제품 구조에서 검증하기 위함. 사용자 재검수·Git 게시·Persistent UAT·Azure image 재배포는 별도 Gate로 유지 | 23장~25장, 27-experiment-task-ledger, TASK-EXPERIMENT-PROMOTION-001 Change 003 |
| 2026-08-04 | `TASK-EXPERIMENT-PROMOTION-001 Change 004`에서 제조·품질·물류 현재 목록의 `No`를 활성 행 `1..N`으로 표시하고 영구 panel code는 보존 | 과거 구조 변경 뒤 취소 번호를 재사용하지 않아 42개 활성 패널의 마지막 code가 `P52`인 상태에서, 이력 sequence를 현재 목록 번호로 함께 보여 52개처럼 오해하게 한 표현 결함을 데이터·QR·감사 이력 변경 없이 해소하기 위함 | 23장~25장, 27-experiment-task-ledger, TASK-EXPERIMENT-PROMOTION-001 Change 004 |
| 2026-08-04 | UL891 통합 Ready PR #65의 CI 3/3 성공을 확인하고 merge commit `79b90b8`으로 원격 main에 병합 | Graphite·Azure·Pending 복구 기준선과 UL891 사용자 수정·migration `0068`·현재 42면 순번 보정을 하나의 canonical source로 확정하고, 다음 Gate를 기능 재이식이 아닌 통합 source image·migration handover로 전환하기 위함 | 23장~25장, 27-experiment-task-ledger, TASK-EXPERIMENT-PROMOTION-001 Change 003~004 |
| 2026-08-04 | `TASK-AZURE-DEPLOY-001 Change 006`에서 사용자 제공 EMI PNG를 Teams·PWA 공통 브랜드 source로 사용하고 최신 main Azure handover보다 먼저 검증 | 임시 Teams 도형 icon과 누락된 PWA install metadata를 실제 브랜드 자산으로 교체하고, 기존 Service Worker·offline cache 제외 정책과 Teams identity·권한·activity 계약을 보존한 채 배포 image에 포함하기 위함 | 11장·23장~25장, TASK-AZURE-DEPLOY-001 Change 006 |
| 2026-08-05 | `TASK-AZURE-DEPLOY-001 Change 007`에서 Change 006 원격 병합 상태를 canonical 문서에 동기화한 뒤 그 문서 PR이 포함된 최신 main Backend·Frontend image 게시를 승인 | 실제 원격 main과 문서의 게시 상태 drift를 닫고, 기존 OIDC·Environment·main ancestry·immutable digest Gate를 유지한 채 ACR image 게시와 migration·revision·Edge 변경을 분리하기 위함 | 23장~25장, TASK-AZURE-DEPLOY-001 Change 007 |
| 2026-08-05 | `TASK-AZURE-DEPLOY-001 Change 008`에서 Teams v1.19 schema에 없는 최상위 `packageName`을 제거하고 `1.0.3` package를 생성 | Teams Admin Center의 schema parse P1을 기존 manifest·Activity identity·권한·activity type·운영 URL과 icon 변경 없이 해소하고, DNS·TLS 대기 중 catalog 재등록 준비를 완료하기 위함 | 23장~25장, TASK-AZURE-DEPLOY-001 Change 008 |
| 2026-08-05 | `TASK-AZURE-DEPLOY-001 Change 009`에서 원래 계획의 수신자·event 시점으로 공개 Teams Activity type 10개와 Backend 자동 delivery를 연결하고 `1.0.4` package를 생성 | 프로젝트 생성·납기·상태, 업무 배정, 긴급/차단, 재검사와 완료 알림을 공개 운영 app에 반영하되 상세 에스컬레이션 정책·worker 활성화는 후속 기획으로 분리하기 위함 | 6.5장·23장~25장, TASK-AZURE-DEPLOY-001 Change 009 |
| 2026-08-05 | `TASK-AZURE-DEPLOY-001 Change 010`에서 Change 009 merge·Azure `0068` workload·DNS·Front Door·Teams 승인 대기 상태를 동기화한 뒤 최종 main immutable image 게시를 승인 | 문서와 실제 운영 상태의 drift를 닫고 migration `0069`·revision 교체 전에 동일한 main source의 Backend·Frontend image를 고정하기 위함 | 23장~25장, TASK-AZURE-DEPLOY-001 Change 010 |
| 2026-08-05 | `TASK-AZURE-DEPLOY-001 Change 011`에서 최종 main Backend image로 migration `0069`를 `69/69 Exact` 적용한 뒤 Backend·Frontend 최신 revision을 Healthy 상태로 교체 | migration 성공 전 application을 활성화하지 않는 Gate를 지키고, 외부 알림·Front Door·public traffic은 비활성으로 보존한 채 최신 제품 기준선만 Azure workload에 반영하기 위함 | 23장~25장, TASK-AZURE-DEPLOY-001 Change 011 |
| 2026-08-05 | `TASK-AZURE-DEPLOY-001 Change 012`에서 Azure token·가비아 TXT와 endpoint·CNAME exact match를 확인한 뒤 기존 token empty PATCH로 Front Door 재검증을 요청 | DNS 값을 다시 바꾸지 않고 공식 revalidation 경로를 실행하되 authoritative state가 Pending인 동안 TLS·route·public traffic을 시작하지 않기 위함 | 23장~25장, TASK-AZURE-DEPLOY-001 Change 012 |
| 2026-08-06 | `TASK-AZURE-DEPLOY-001 Change 013`에서 domain·managed TLS·공개 정적 화면·Entra 설정을 검증하고 Frontend Nginx의 내부 Backend routing host를 보정 | Azure 내부 ingress 식별용 HTTP Host와 application의 공개 X-Forwarded-Host를 분리해 정적 화면만 열리고 API가 404인 공개 배포 P1을 해소하기 위함 | 23장~25장, TASK-AZURE-DEPLOY-001 Change 013 |
| 2026-08-06 | `TASK-AZURE-DEPLOY-001 Change 014`에서 공개 hostname과 exact Backend internal hostname만 `AllowedHosts`에 고정하고 실제 비상 관리자 로그인을 검증 | Nginx route 보정 뒤 Host filtering `400`을 wildcard 없이 해소하고, runtime 보정을 다음 workload 재배포에서도 유지하며 bootstrap 관리자 목록의 순서가 아닌 실제 권한 동작을 확인하기 위함 | 23장~25장, TASK-AZURE-DEPLOY-001 Change 014 |
| 2026-08-06 | `TASK-AZURE-DEPLOY-001 Change 015`에서 Teams 승인 대기보다 공개 Frontend Entra 사전 인증을 우선하고 운영 `pms`에 직접 적용하되 Teams tab 실패 시 Activity 알림 전용으로 유지 | 익명 요청의 app shell·bundle·PWA asset 구조 노출을 차단하면서 Backend bearer·역할 권한과 알림 provider 경계를 보존하기 위함 | 23장~25장, TASK-AZURE-DEPLOY-001 Change 015 |
| 2026-08-06 | `TASK-AZURE-DEPLOY-001 Change 016`에서 운영 Dispatcher를 켜지 않고 bootstrap 관리자 1명에게 synthetic Teams Activity actual 1건을 직접 검증하고 Teams web 표시를 확인 | 기존 notification 원본의 대량 외부 발송을 막으면서 실제 Teams credential·Graph 권한·manifest activity·설치 사용자 대상 provider 수락과 server-side 렌더링을 검증하기 위함 | 23장~25장, TASK-AZURE-DEPLOY-001 Change 016 |
| 2026-08-06 | `TASK-AZURE-DEPLOY-001 Change 017`에서 기존 대기 알림 일괄 발송 승인 아래 Dispatcher·Teams Activity·Gmail SMTP actual을 함께 활성화 | worker만 켜서 delivery가 Disabled/DryRun으로 종결되는 것을 막고, 기존 Bicep toggle 계약대로 실제 provider와 대기열 처리를 한 revision에서 일치시키기 위함 | 23장~25장, TASK-AZURE-DEPLOY-001 Change 017 |
| 2026-08-06 | `TASK-AZURE-DEPLOY-001 Change 017`의 Teams client·메일함 실제 수신을 완료하고 Change 015~017 원격 게시 뒤 승인 게이트형 GitHub→Azure 배포 연결을 Teams SSO보다 먼저 진행 | 실제 provider 수락과 사용자 수신을 모두 닫고, 원격 `main`을 운영 배포 원본으로 만들되 GitHub 수동 운영 release에서 최신 main SHA와 명시 확인값을 제출하기 전에는 Azure 운영 변경이 시작되지 않게 하기 위함 | 23장~25장, TASK-AZURE-DEPLOY-001 Change 015~017 |
| 2026-08-06 | `TASK-AZURE-DEPLOY-001 Change 018`에서 private Repository의 실제 지원 범위에 맞춰 GitHub 수동 운영 release를 최신 `main` SHA·image·운영 배포 확인으로 fail-closed하고, migration→Backend→Frontend와 rollback을 연결 | 존재하지 않는 필수 검토자 보호를 주장하지 않으면서 자동 `push` 배포를 막고, OIDC exact resource 최소 권한과 별도 명시 실행 경계를 유지하기 위함 | 23장~25장, TASK-AZURE-DEPLOY-001 Change 018 |
| 2026-08-07 | `TASK-E2E-FULL-SUITE-001 Change 011`에서 PR #75의 Pending hub 검증을 전역 100개 목록이 아니라 생성 프로젝트별 canonical route로 고정 | 전체 suite가 100개를 넘는 합성 프로젝트를 누적할 때 전역 첫 페이지에서 대상이 제외돼 정상 제품을 실패로 판정한 P2를 test-only로 해소하고 Azure Change 018 게시 Gate를 복구하기 위함 | 23장~25장, TASK-E2E-FULL-SUITE-001 Change 011 |
| 2026-08-07 | `TASK-E2E-FULL-SUITE-001 Change 012`에서 프로젝트별 Pending 제목·코드를 exact project detail로 읽고 실패 시 generic fallback 대신 retry를 제공 | PR #76 최신 head는 통과했지만 merge SHA 누적 suite에서 대상 프로젝트가 최근 100개 밖이고 Pending 0건이면 exact route도 일반 제목으로 표시되는 제품 P2가 드러나, 전역 목록 크기를 늘리지 않고 사용자 link 계약과 Azure 운영 release 품질 Gate를 함께 복구하기 위함 | 23장~25장, TASK-E2E-FULL-SUITE-001 Change 012 |
| 2026-08-07 | `TASK-E2E-FULL-SUITE-001 Change 012` PR #77과 merge SHA CI `3/3`을 완료하고 Azure Change 018 운영 release Gate를 재개 | exact Pending route의 제품 P2가 로컬 전체 회귀뿐 아니라 PR·실제 main 기준에서도 해소됐음을 확인하고, 문서의 CI 대기 상태를 실제 게시 결과와 맞추기 위함 | 23장~25장, TASK-E2E-FULL-SUITE-001 Change 012 |
| 2026-08-07 | `TASK-AZURE-DEPLOY-001 Change 019`에서 첫 actual release의 정상 `RunningAtMaxScale` 오판을 보정하고 정지·축소·저하·미확인 상태의 mutation 전 차단을 회귀로 고정 | Azure가 정상 최대 scale revision에 반환하는 값을 허용하되 health·single revision·latest ready와 fail-closed 불변조건을 약화하지 않고 승인형 release를 완료하기 위함 | 23장~25장, TASK-AZURE-DEPLOY-001 Change 019 |
| 2026-08-07 | `TASK-AZURE-DEPLOY-001 Change 019` PR #79·main CI와 최신 main 운영 release를 완료하고 다음 Gate를 Teams SSO·새 manifest 기획으로 전환 | 정상 상태 판정 보정이 실제 migration·Backend·Frontend 교체와 공개 보안 검사까지 통과했으며, 남은 action/CLI 경고는 운영 결과에 영향 없는 P3 유지보수로 분리됐기 때문 | 23장~25장, TASK-AZURE-DEPLOY-001 Change 019 |
| 2026-08-07 | `TASK-PRODUCTION-CONTROL-001 Change 010`에서 같은 현재 양식의 후행 재선택이 빠른 편집 진입을 취소하던 P2를 우선 보정 | 문서 PR Frontend CI에서 두 차례 같은 저장 버튼 소실이 재현돼 현재 게시 Gate를 차단했으며, 사용자가 Teams SSO보다 이 최소 수정·검증·main 병합을 먼저 하도록 명시 승인했기 때문 | 23장~25장, TASK-PRODUCTION-CONTROL-001 Change 010 |
| 2026-08-07 | `TASK-PRODUCTION-CONTROL-001 Change 010` PR #81·main CI `3/3`을 확인하고 원격 `main`에 병합 | 실제 제품 경쟁과 Full-Stack fixture 두 가지 P2가 해소된 최종 head와 merge SHA를 독립적으로 검증하되, Azure 운영 재배포는 별도 승인 경계로 유지하기 위함 | 23장~25장, TASK-PRODUCTION-CONTROL-001 Change 010 |
| 2026-08-09 | `TASK-TEAMS-PWA-001 Change 001`에서 Teams를 Activity Feed 알림과 작은 외부 실행 화면으로 유지하고 실제 업무는 Entra로 보호된 웹/PWA에서 수행 | Easy Auth가 SPA보다 앞에서 shell·bundle을 차단하는 보안 구조와 Teams iframe의 redirect 제약을 동시에 보존하며 NAA·OBO·두 앱 등록 충돌 없이 사용자가 알림에서 업무를 이어가게 하기 위함 | 20장·23장~25장, TASK-TEAMS-PWA-001 Change 001 |
| 2026-08-10 | `TASK-DESIGN-LOGIN-001 Change 010` PR #83을 원격 main에 병합하고 `TASK-TEAMS-PWA-001 Change 007`에서 로그인 후 공통 shell logo를 별도 지정 asset으로 분리 | 로그인은 사용자 지정 가로형 logo, 로그인 뒤 모든 page는 사용자 지정 내부 logo를 쓰되 기존 흑백 wireframe과 Easy Auth·Teams/PWA·업무 계약을 보존한 최신 main을 한 번 공개 release하기 위함 | 23장~25장, TASK-DESIGN-LOGIN-001 Change 010, TASK-TEAMS-PWA-001 Change 007 |
| 2026-08-10 | `TASK-TEAMS-PWA-001 Change 009`에서 모바일 PWA 자동 안내를 Microsoft 인증·앱 shell 준비 뒤로 고정하고 Android 설치 event 준비 전에도 안내를 먼저 표시 | Easy Auth의 익명 차단은 유지하면서 로그인 직후 설치 행동을 놓치지 않게 하고, Chrome native 설치 정책 안에서 준비 뒤 한 번의 버튼으로 설치 확인창을 열며 영구 숨김을 방지하기 위함 | 23장~25장, TASK-TEAMS-PWA-001 Change 009 |
| 2026-08-10 | `TASK-TEAMS-PWA-001 Change 009` PR #86·main CI `3/3` 뒤 exact main SHA를 Azure release `31361630803`으로 운영에 배포 | 모바일 설치 안내 보정의 Git 게시·immutable image·migration gate·Backend/Frontend 교체와 공개 health·익명 인증 차단을 하나의 운영 근거로 닫고 다음 Gate를 실제 Android·iPhone 사용자 검수로 전환하기 위함 | 23장~25장, TASK-TEAMS-PWA-001 Change 009 |
| 2026-08-10 | 실제 기기 사용자 검수·운영 관찰과 `TASK-CI-COST-001`을 병렬 진행하고 일반 CI 비용 최적화를 구현 | GitHub Actions 월 사용량이 90%에 도달해 개발·게시 Gate 중단 위험이 생겼으므로, 제품 runtime과 Azure 수동 release를 건드리지 않고 코드 PR 품질을 보존하는 최소 workflow 보정을 먼저 검증하기 위함 | 23장~25장, TASK-CI-COST-001 |
| 2026-08-10 | GitHub Actions 사용량 100% 도달 뒤 결제 경계를 복구하고 `TASK-CI-COST-001`의 Git 게시와 `main` merge를 승인 | 현재 개발을 재개하면서도 단순 증액에 의존하지 않고 이후 모든 PR과 main push가 변경 인지형 CI 비용 정책을 자동 적용하게 하기 위함 | 23장~25장, TASK-CI-COST-001 |
| 2026-08-10 | GitHub actual readback 기준 Repository 현재 visibility를 `PRIVATE`로 동기화하고 과거 public 상태는 당시 이력으로 보존 | CI 포함 minute 과금 원인과 current Roadmap 상태의 충돌을 해소하되 history rewrite 완료 당시 공개 재개 사실과 exact visibility 전환 시점 미확정 상태를 왜곡하지 않기 위함 | 22장~25장, TASK-CI-COST-001 |
| 2026-08-10 | `TASK-CI-COST-001` PR #89의 코드 PR `5/5`와 이전 run 취소 3건을 확인해 squash merge하고 main에서 성공 4·Full-Stack skip 1을 확인 | 코드 PR 전체 품질 Gate는 보존하면서 오래된 PR 실행과 merge 뒤 Full-Stack 중복을 실제 GitHub 환경에서 제거했음을 canonical main에 확정하기 위함 | 23장~25장, TASK-CI-COST-001 |
| 2026-08-10 | `TASK-TEAMS-PWA-001 Change 010` PR #88·Azure release `31366150022` 반영 후 실제 Android Chrome 최종 사용자 검수를 완료 | Easy Auth 보호 manifest의 credential 연결이 Android 설치 event를 복구했고 인증 후 안내·버튼 활성·native 확인창·standalone이 실제 기기에서 정상임을 확정하기 위함 | 23장~25장, TASK-TEAMS-PWA-001 Change 010 |
| 2026-08-10 | `TASK-QUALITY-OPERATING-MODEL-001 Change 006`으로 Change 004·005를 신규 CI 정책이 반영된 최신 main에 선택 이식하고 전체 자동 검증을 완료 | 최근 EMI PMS·Easy Auth·PWA·Teams·Azure·CI 계약을 되돌리지 않으면서 migration `0070`·`0071`과 Item별 LQC·구분별 IQC를 승격하고, 사용자 검수 후에만 PR·CI Gate·main·Azure로 진행하기 위함 | 4장·5장·10장·13장·14장·23장~25장, TASK-QUALITY-OPERATING-MODEL-001 Change 006 |
| 2026-08-05 | `TASK-QUALITY-OPERATING-MODEL-001 Change 004`에서 전역 스위치 없이 Item별 LQC 운영 상태·검사 양식을 관리하고 프로젝트 생성 시 고정 | 설정 변경이 기존 프로젝트에 소급되지 않게 하면서 운영 중지 프로젝트는 가짜 합격 없이 제조→OQC로 진행하고, 운영 재개 프로젝트는 생성 당시 Item별 양식으로 기존 제조+LQC gate를 사용하기 위함 | 4장·5장·10장·13장·14장·23장~25장, TASK-QUALITY-OPERATING-MODEL-001 Change 004 |
| 2026-08-05 | `TASK-QUALITY-OPERATING-MODEL-001 Change 005`에서 구매품 구분별로 검사 없음·스캔형·상세형과 상세 검사 항목을 관리하고 구매품 저장·성적서 최초 생성 시점에 고정 | 구분 수가 늘어도 품질 양식 관리자가 정확한 구매품 검사를 찾고 설정하게 하면서 이미 저장된 구매품·시작된 검사·기존 외함 스캔형·legacy 상세 IQC를 소급 변경하지 않기 위함 | 5장·11장·13장·23장~25장, TASK-QUALITY-OPERATING-MODEL-001 Change 005 |
| 2026-08-11 | `TASK-QUALITY-OPERATING-MODEL-001 Change 006` 최신 main 통합본의 사용자 검수를 완료하고 병합·공개배포를 승인 | Item별 LQC와 구매품 구분별 IQC의 실제 화면·업무 흐름을 확인했으므로 최신 PR head `CI Gate` 뒤 main 병합과 migration `0070`·`0071`→Backend→Frontend 운영 교체를 진행하기 위함 | 4장·5장·10장·13장·14장·23장~25장, TASK-QUALITY-OPERATING-MODEL-001 Change 006 |
| 2026-08-11 | `TASK-QUALITY-OPERATING-MODEL-001 Change 006` PR #91을 원격 main에 병합하고 Azure release `31409582129`로 공개 운영에 배포 | 새 변경 인지형 CI의 5개 필수 Gate와 migration→Backend→Frontend 교체, 공개 health `200`, 익명 root·`/api/me` `401/401`을 모두 확인해 사용자 승인 범위를 운영 반영까지 닫기 위함 | 4장·5장·10장·13장·14장·23장~25장, TASK-QUALITY-OPERATING-MODEL-001 Change 006 |
| 2026-08-11 | `TASK-ADMIN-003`에서 사용자 부서 선택과 기본 역할을 연결하고 복수 부서장 체크를 기존 양식관리 승인 binding과 동기화하며 표준 부서명을 한글로 통일 | 운영 migration에 표준 10개 중 3개만 존재한 결함과 사용자 승인 시 부서·역할·양식 권한을 여러 화면에서 따로 지정하던 불일치를 최소 additive migration·기존 권한 경계 재사용으로 해소하기 위함. 사용자 검수·게시·운영 적용은 별도 Gate로 유지 | 23장~25장, TASK-ADMIN-001, TASK-ADMIN-002, TASK-ADMIN-003 |
| 2026-08-11 | `TASK-TEAMS-PWA-001 Change 011`에서 새 EMI PMS 웹 제품 logo·로그인 정보 보안 안내·오산 및 청주 회사 footer를 추가하고 PWA·Teams 앱 icon은 유지 | 로그인과 내부 공통 화면의 제품 식별을 최신 확정 logo로 통일하고 임직원 보안 주의와 두 사업장 정보를 모든 웹 화면에서 확인하게 하되 설치 앱·Teams catalog 자산과 인증·업무 계약은 변경하지 않기 위함. 사용자 지시에 따라 `TASK-ADMIN-003`과 같은 branch에서 함께 검수·병합한다. | 23장~25장, TASK-TEAMS-PWA-001 Change 011, TASK-ADMIN-003 |
| 2026-08-11 | `TASK-ADMIN-003`과 `TASK-TEAMS-PWA-001 Change 011` 사용자 검수를 모두 완료하고 한 PR의 main 병합·공개배포를 승인 | 부서 자동 역할·복수 부서장·양식 승인 연결과 새 웹 제품 logo·보안 안내·두 주소 footer의 실제 화면을 확인했으므로 신규 CI 정책을 통과한 exact main SHA에 migration `0072`→Backend→Frontend 순서로 운영 반영하기 위함 | 23장~25장, TASK-ADMIN-003, TASK-TEAMS-PWA-001 Change 011 |
| 2026-08-11 | `TASK-TEAMS-PWA-001 Change 011` PR #93 첫 CI의 구형 내부 로고 크기 기대값을 새 승인 자산으로 동기화 | 제품 화면·동작은 정상이고 panel kitting mock smoke 한 곳만 Change 007의 `3796×1378`을 고정해 새 로고 `1406×379`을 실패로 판정했으므로, 제품 변경 없이 회귀 기준만 최신 제품 계약으로 갱신하기 위함 | 23장~25장, TASK-TEAMS-PWA-001 Change 011 |
| 2026-08-11 | `TASK-ADMIN-003` PR #93 첫 Backend CI의 구형 영문 부서명 기대값을 한글 부서 계약으로 동기화 | 공지 API 응답은 승인 범위대로 `영업`을 반환했으나 기존 회귀 한 곳만 `Sales`를 고정해 493개 중 1개를 실패로 판정했으므로, 제품 변경 없이 해당 test expectation을 최신 사용자 표시 계약으로 갱신하기 위함 | 23장~25장, TASK-ADMIN-003 |
| 2026-08-11 | `TASK-ADMIN-003` PR #93 Full-Stack CI의 공지 작성자 부서 기대값을 한글 부서 계약으로 동기화 | 공지 상세 화면은 승인 범위대로 `품질`을 표시했으나 기존 브라우저 회귀가 `Quality`를 고정해 실패했으므로, 제품 변경 없이 해당 expectation을 갱신하고 같은 형식의 잔여 영문 부서 기대값이 없음을 확인하기 위함 | 23장~25장, TASK-ADMIN-003 |
| 2026-08-11 | `TASK-ADMIN-003`과 `TASK-TEAMS-PWA-001 Change 011`을 PR #93으로 원격 main에 병합하고 Azure release `31452524156`으로 공개배포 | PR 최신 SHA의 Change Classification·Frontend·Backend `493/493`·Full-Stack E2E·CI Gate 전체 성공을 근거로 main SHA `8ae3645d66543c0f234777cf19e8487324f21217`에 migration `0072`→Backend→Frontend를 적용하고 health `200`, 익명 root·API `401/401`을 확인하기 위함. 사용자의 중복 검사 생략 지시에 따라 동일 코드의 반복 main CI는 취소했다. | 23장~25장, TASK-ADMIN-003, TASK-TEAMS-PWA-001 Change 011 |
| 2026-08-11 | `TASK-CI-COST-001 Change 001`에서 변경 영향별 일반 CI, 검증된 PR tree의 main 재사용, Azure 변경 component 선택·병렬 release를 구현 | 최근 성공 코드 PR 평균 약 38분 42초 중 Backend와 Full-Stack이 직렬로 약 38분을 차지하고, 같은 tree의 main이 약 19분, Azure가 약 6분을 반복한 병목을 안전 fallback과 always-run `CI Gate`를 보존한 채 줄이기 위함. 원격 Ruleset 적용·Git 게시·실제 운영 release는 별도 Gate로 유지 | 23장~25장, TASK-CI-COST-001 Change 001, TASK-AZURE-DEPLOY-001 release 계약 |
| 2026-08-11 | `TASK-CI-COST-001 Change 001` PR #96 squash merge와 main CI를 완료 | GitHub Actions 출처 `CI Gate` required check를 적용한 뒤 PR workflow-only run과 main의 CI trust source 변경 safe fallback에서 제품 job 3개를 모두 생략하고 Workflow Validation·CI Gate를 수십 초 안에 성공시켜 변경 영향별 CI의 실제 동작을 확인하기 위함. Azure 실제 release는 별도 승인으로 유지 | 23장~25장, TASK-CI-COST-001 Change 001, PR #96 |
| 2026-08-11 | `TASK-NOTICE-EDITOR-001 Change 002`, `TASK-PRIVACY-NOTICE-001 Change 006`과 `TASK-AZURE-DEPLOY-001 Change 021`의 단일 통합 PR·main 병합·Azure 공개 배포를 승인 | 사용자가 공지 굵게·수정·편집 화면 전용 첨부 관리와 개인정보·이용 안내 검수본을 모두 승인했고, 새 활성 Ruleset `main-pr-only`의 PR 전용 변경·필수 `CI Gate`를 지켜 병합된 exact main SHA만 운영에 반영하기 위함 | 23장~25장, TASK-NOTICE-EDITOR-001, TASK-PRIVACY-NOTICE-001, TASK-AZURE-DEPLOY-001 Change 021 |
| 2026-08-11 | `TASK-PWA-PUSH-001`은 실제 인앱 가시성을 source of truth로 하고 활성 기기별 Web Push delivery, 현재/전체 기기 해제와 최소 Service Worker를 사용 | 휴대폰과 검사용 태블릿 등 여러 로그인 기기에서 같은 인앱 알림을 받되 Teams·메일 정책 drift, 분실 기기 잠금 화면 노출, 관리자 기기 오연결과 과거 알림 소급 발송을 막기 위함. 기본 `Enabled=false`·`DryRun=true`와 실제 provider 별도 승인 경계를 유지 | 3.3F, TASK-PWA-PUSH-001 planning·review·Change 001 |
| 2026-08-11 | `TASK-NOTIFY-POLICY-001`에서 자동 업무·Pending·프로젝트 lifecycle 채널과 수신자, 복수 부서장 fallback, 일정 원본 due_date, 평일 Digest와 L0·L1을 확정 | 인앱·Teams·메일·PWA가 서로 다른 수신자를 만들거나 제조 중단·묶음 작업이 중복 알림을 만드는 문제를 막고, 담당자 부재와 기한 알림을 실제 부서·일정 원본에 맞추기 위함. 새 TeamsChannel과 L2·L3 확대 발송은 중단하되 과거 schema·handler·이력은 보존 | 3.3G, TASK-NOTIFY-POLICY-001 planning·review·Change 001 |
| 2026-08-12 | `TASK-PWA-PUSH-001`·`TASK-NOTIFY-POLICY-001`의 사용자 화면 검수를 완료하고 원격 `main` 병합과 `TASK-AZURE-DEPLOY-001 Change 022` 공개 배포를 승인 | 구버전 영업·관리자 fallback 문구를 현재의 복수 부서장 공유·미등록 차단 규칙으로 정합화하고, migration `0074`·`0075`와 Backend·Frontend를 필수 `CI Gate` 뒤 exact main SHA로 운영 반영하기 위함. 새 Web Push 실제 provider는 운영 key·실기기 검수 전 중지·시험 모드를 유지 | 3.3F·3.3G·6.2, TASK-NOTIFY-POLICY-001 Change 002, TASK-AZURE-DEPLOY-001 Change 022 |
| 2026-08-12 | `TASK-PROJECT-PENDING-001`에서 LSE TASK NO와 부서별 Pending·오픈/종결 구분을 한 Task로 구현하고 공개배포는 우선순위 3 뒤로 묶는다 | 단순 기본정보 필드와 같은 Pending 조회 화면을 함께 검수하되, 전체 화면은 운영 부서의 오픈 업무 집중에 맞추고 프로젝트 화면은 타 부서 조치를 숨기지 않도록 전체 범위를 기본값으로 유지하기 위함. 현재 흑백 wireframe과 일반 테두리를 재사용하고 강조선은 추가하지 않는다. | 3.3H, TASK-PROJECT-PENDING-001 planning·implementation report |
| 2026-08-12 | `TASK-ADMIN-001 Change 001`로 관리자 홈을 조치 대상 중심으로 정리하고 승인 대기 사용자 전용 목록을 추가 | 완료 발송·마지막 일일 요약·최근 기준정보 변경은 관리자 홈의 즉시 조치 KPI로서 가치가 낮고, 기존 승인 대기 카드가 전체 사용자 목록으로 이동해 실제 대상을 구분하기 어려웠기 때문. 알림·Digest·변경 이력 원본 기능과 일반 전체 사용자 관리는 보존한다. | 23장~25장, TASK-ADMIN-001 Change 001 |
| 2026-08-12 | `TASK-PROJECT-PENDING-001`·`TASK-ADMIN-001 Change 001`·`TASK-PANEL-DESIGN-001 Change 001` 사용자 검수를 모두 완료하고 단일 PR의 원격 `main` 병합과 `TASK-AZURE-DEPLOY-001 Change 023` 공개 배포를 승인 | LSE TASK NO·부서 Pending, 관리자 조치 화면, 일반 Item의 도번·필수값·열반을 같은 기준선에서 검증하고 migration `0076`·`0077`과 Backend·Frontend를 필수 `CI Gate` 뒤 exact main SHA로 운영 반영하기 위함. UL891과 기존 운영 인증·알림·데이터는 보존한다. | 3.3H·3.3I·3.3J·6.2, TASK-AZURE-DEPLOY-001 Change 023 |
| 2026-08-14 | `TASK-PRODUCTION-CONTROL-001 Change 011`에서 모든 프로젝트의 전용 계획 기간·실적 1:1 연결과 단일 계획·실적 일정표를 적용하고 행 삭제 후 저장 순번 충돌을 보정 | 양식 관리 기본값을 기존 프로젝트에 소급하지 않으면서도 프로젝트 안에서 실제 계획과 연결 원본을 수정하게 하고, Legacy 체크형 달력과 현재 2중 막대 화면의 이원화 및 추가·삭제·재추가 저장 오류를 함께 해소하기 위함. UL891 세트와 기존 실행 이력은 보존하며 사용자 지시에 따라 다른 변경과 마지막 일괄 검수·게시를 대기한다. | 10장·13장·23장~25장, TASK-PRODUCTION-CONTROL-001 Change 011 |
| 2026-08-14 | `TASK-PROJECT-ASSIGNEE-DELEGATION-001`에서 생산관리 이외 부서장이 프로젝트별 자기 부서 담당자를 직접 지정하고 프로젝트 생성 시 담당자 지정 요청을 받도록 구현 | 생산관리팀이 모든 부서에 연락해 담당자를 취합·대신 입력하는 병목을 없애되, 생산계획·실적 연결과 다른 부서 담당자 수정 권한은 생산관리에 유지하기 위함. 부서장 전용 화면과 API는 자기 부서만 반환·저장하고 일반 사용자·다른 부서 mutation을 서버에서 차단하며, 생성 요청은 인앱 원본을 통해 Teams Activity·PWA가 따르게 한다. 사용자 지시에 따라 Change 011과 마지막 일괄 검수·게시를 대기한다. | 5장·6장·10장·23장~25장, TASK-PROJECT-ASSIGNEE-DELEGATION-001 |
| 2026-08-14 | `TASK-PRODUCTION-CONTROL-001 Change 011`과 `TASK-PROJECT-ASSIGNEE-DELEGATION-001`의 원격 `main` 병합·Azure 공개배포를 승인 | 모든 프로젝트의 전용 계획·실적 연결과 부서장 자기 부서 담당자 지정 화면을 실제 검수 환경에서 확인했고, 전체 자동검증은 Ready PR의 변경 인지형 `CI Gate`, 운영 반영은 병합된 exact main SHA의 수동 승인형 Azure release로 닫기 위함. 신규 migration은 없으며 Backend·Frontend만 변경 대상으로 분류한다. | 3.3A·3.3K·23장~25장, TASK-AZURE-DEPLOY-001 Change 024 |
| 2026-08-14 | PR #101과 `TASK-AZURE-DEPLOY-001 Change 024` 운영 게시를 완료 | PR 필수 CI와 main push CI를 통과한 exact main SHA `8b19483e40655ce99c13cb470217ccddf444b1c0`를 Azure release `31774236257`로 배포했다. Backend·Frontend ready와 public security smoke가 통과했고 migration은 변경 없음으로 실행하지 않았다. | 3.3A·3.3K·6.2, TASK-AZURE-DEPLOY-001 Change 024 |
| 2026-08-12 | `TASK-PWA-PUSH-001 Change 002`에서 운영 Web Push를 `Enabled=true`·`DryRun=false`로 검수 완료하고 직원별 PWA 등록은 자율로 운영 | iPhone·Android의 실제 PWA 수신과 알림 상세 이동을 확인했으므로 미설치·미허용 사용자는 인앱을 유지하고, 나중에 활성화한 사용자는 이후 새 인앱 알림부터 푸시를 받게 한다. 향후 Azure workload 전체 재배포에서도 VAPID Key Vault 참조와 실발송 상태를 잃지 않도록 Bicep·ARM·secret-scope RBAC를 동기화한다 | 3.3F·3.3G·6.2, TASK-PWA-PUSH-001 Change 002 |
| 2026-08-14 | `TASK-ADMIN-003 Change 002`에서 양식 관리를 품질·생산관리 부서장 책임으로 재정렬하고 제조 부서장·일반 품질 사용자의 화면과 mutation을 차단 | 품질 부서장은 IQC·LQC·OQC·구매품별 IQC와 LQC 운영 상태를 관리하고 생산관리 부서장은 생산계획·실적 연결과 Item별 제조 양식을 함께 관리하게 해 실제 업무 책임과 메뉴·서버 권한을 일치시키기 위함. System Administrator 전체 관리와 복수 부서장·audit·기존 snapshot은 유지한다. | 3.3D, TASK-ADMIN-002, TASK-ADMIN-003 Change 002, migration `0078` |
| 2026-08-14 | `TASK-ADMIN-003 Change 002`와 `TASK-PWA-PUSH-001 Change 002`를 단일 Ready PR로 원격 `main`에 병합하고 `TASK-AZURE-DEPLOY-001 Change 025`로 공개배포 승인 | 사용자가 두 완료 범위만 게시하고 미완성 공통 매뉴얼 작업은 보존하도록 지시했다. 필수 `CI Gate` 뒤 병합된 exact main SHA에 migration `0078`→Backend→Frontend를 적용하고 기존 Web Push 활성·Key Vault 참조와 공개 인증 차단을 확인하기 위함 | 3.3D·3.3F·6.2, TASK-AZURE-DEPLOY-001 Change 025 |
| 2026-08-14 | PR #103과 `TASK-AZURE-DEPLOY-001 Change 025` 운영 게시를 완료 | PR CI run `31784473124`와 main CI run `31786026056`이 통과했고 exact main SHA `58c089993587deea30513cb6edee0b8396a1d474`의 Azure release `31786040822`에서 migration `0078`·Backend·Frontend·public security를 완료했다. Web Push 활성·두 Key Vault 참조를 보존했고 미완성 공통 매뉴얼 작업은 변경하지 않았다. | 3.3D·3.3F·6.2, TASK-AZURE-DEPLOY-001 Change 025 |
| 2026-08-18 | `TASK-PRODUCTION-CONTROL-001 Change 012`로 프로젝트 생산계획 이름 맞교환·삭제 후 재사용 저장 오류를 보정 | 최종 활성 항목명은 유효해도 행 단위 갱신 중 partial unique index와 일시 충돌하던 문제를 같은 transaction의 내부 이름 격리로 제거한다. 최종 실제 중복 validation, 삭제 이력, 실적 연결과 UL891 계약은 유지하며 사용자 검수와 게시를 대기한다. | 3.3A, TASK-PRODUCTION-CONTROL-001 Change 012 |
| 2026-08-18 | `TASK-QUALITY-OPERATING-MODEL-001 Change 007`에서 기존 `AllReceipts` 프로젝트도 구매품 구분을 선택·저장하도록 결정·구현 | 구매품 분류 metadata와 IQC routing 정책을 분리한다. 기존 프로젝트의 구분은 선택사항이며 저장해도 모든 도착분은 기존 전역 상세 IQC를 사용하고, 신규 `CategoryBased` 프로젝트의 구분 필수·snapshot 비소급·도착 후 변경 차단은 유지한다. 사용자 검수와 게시를 대기한다. | 5장·11장·13장·23장~25장, TASK-QUALITY-OPERATING-MODEL-001 Change 007 |
| 2026-08-18 | `TASK-PRODUCTION-CONTROL-001 Change 013`에서 구매품 구분별 발주·입고 실적 연결과 Item별 제조양식의 모든 프로젝트 즉시 적용을 구현 | 생산계획에서 전체 구매품 호환을 유지하면서 구분별 실적을 선택하게 하고, 제조 기준은 프로젝트 생성 시점 복제가 아니라 Item 공통 기준으로 운영하되 이미 시작·완료된 제조 이력은 보존하기 위함. 사용자 검수와 게시를 대기한다. | 3.3A·5장·10장·13장, TASK-PRODUCTION-CONTROL-001 Change 013, migration `0079` |
| 2026-08-18 | `TASK-INFRA-001 Change 001`에서 지정된 실제 Entra 사용자만 운영 개발 검수 권한 전체를 사용하도록 구성 | 운영 Dev 인증·사용자 전환을 켜지 않고도 공개 운영 오류를 직접 재현하되, 실제 식별값은 Key Vault allowlist로만 관리하고 승인 대기·비활성·비대상 사용자의 권한을 확대하지 않기 위함. 실제 secret 입력과 workload 배포는 별도 운영 승인을 유지한다. | 23장~25장, TASK-INFRA-001 Change 001 |
| 2026-08-18 | Change 012·013, 품질 Change 007과 Infra Change 001의 원격 `main` 병합·Azure 공개배포를 승인 | 사용자가 자동검증 결과와 사용자 검수 미완료 상태를 확인한 뒤 일괄 게시를 명시 지시했다. migration `0079`와 Backend·Frontend·운영 개발 검수 allowlist를 병합된 exact main SHA에 적용하고, 사용자 운영 검수는 공개배포 후 수행한다. | 3.3A·4.7·TASK-INFRA-001 Change 001·TASK-AZURE-DEPLOY-001 |
| 2026-08-18 | `TASK-PRODUCTION-CONTROL-001 Change 014` PR #106 병합과 운영 게시를 완료 | PR CI `32116227678`과 main CI `32118673836`을 통과한 exact main SHA `d8c60ffe1317907eb5543ad785abf10b058e64e9`를 Azure release `32118742009`로 배포했다. migration `0080`, Backend 교체와 public security가 통과했고 Frontend는 변경 없음으로 유지했다. 공개 운영에서 LLP 제조양식 `7`단계와 프로젝트 생산계획의 각 제조 실적 선택지 `7`개 일치를 개인정보 없는 count로 확인했다. 사용자 직접 완료 이력 보존 검수는 남긴다. | 3.3A, TASK-PRODUCTION-CONTROL-001 Change 014, migration `0080` |
| 2026-08-18 | `TASK-WORKFLOW-CONTINUITY-001 Change 018`에서 프로젝트 전체 흐름을 단계 상태 전용으로 정리 | 프로젝트 전체 업무 기록 수가 개인의 `내 업무`처럼 표시돼 사용자가 오해하므로 상단·단계별 건수를 제거하고 `Requested`를 `업무 요청됨`으로 표시한다. 실제 개인 업무는 `/my-work`에서만 유지하며 업무 생성·알림·진행률과 API 호환 필드는 변경하지 않는다. | 4.5, TASK-WORKFLOW-CONTINUITY-001 Change 018 |
| 2026-08-19 | PR #108과 `TASK-AZURE-DEPLOY-001 Change 026` 운영 게시를 완료 | 필수 PR CI를 통과한 exact main SHA `51aba7e97a2d1fee0f9ee4b82a3f89d514171acf`를 Azure release `32197298425`로 배포했다. 검증된 PR tree의 main 중복 검사는 생략했고 Backend·Frontend image를 병렬 생성했다. Migration은 변경 없음으로 실행하지 않았으며 공개 보안 검사를 통과했다. | 4.5·6.2, TASK-WORKFLOW-CONTINUITY-001 Change 018, TASK-AZURE-DEPLOY-001 Change 026 |
| 2026-08-18 | `TASK-G2-OPERATIONS-001`을 `TASK-QMS-PLATFORM-001` Slice 1과 병렬로 기획하도록 명시적으로 재정렬 | G2는 기존 PMS 데이터와 연결하지 않는 독립 일일 생산·출하·제조 출근 관리이므로 QMS의 양식·검사·외부 portal 범위와 purpose·data·작업공간이 분리된다. 최신 원격 `main`에서 별도 기능 branch·임시 worktree를 사용하고 Fable 5 deep-interview부터 시작하며 구현·게시·운영 적용은 각각 기존 승인 Gate를 유지한다. | 6.4, TASK-G2-OPERATIONS-001 identity gate·interview |
| 2026-08-18 | `TASK-G2-OPERATIONS-001` Round 1에서 담당자별 생산 입력·미래 예상치·홈 구성을 확정하고 영업팀 전용 손익관리를 후속 추적으로 분리 | 오전·오후 담당자가 각각 또는 한꺼번에 값을 입력하고 과거·미래 날짜를 제한 없이 사용하되 미래 값은 예상치로 다룬다. 홈은 이번 달 일별 생산·납품 추이, 조별 생산량과 제조 인원 출근 현황을 제공한다. 손익관리는 이번 Task에서 메뉴 placeholder도 만들지 않고 향후 별도 NEW_FEATURE로 기획하며 영업팀에만 노출한다. | 6.4, TASK-G2-OPERATIONS-001 interview round 1 |
| 2026-08-18 | `TASK-G2-OPERATIONS-001` Round 3에서 재고수량·재고목표와 홈 그래프·출근 현황표 구성을 확정 | 홈 첫 그래프는 생산·납품 일별 막대와 재고수량 선·재고목표 고정선, 둘째 그래프는 오전조·오후조 누적 막대와 일 생산목표 고정선으로 구성한다. 출근표는 오전·오후별 EMI·도급과 단순 합계를 이번 달 일별로 표시하고 미래 값은 예상으로 구분한다. | 6.4, TASK-G2-OPERATIONS-001 interview round 3 |
| 2026-08-18 | `TASK-G2-OPERATIONS-001` Fable Round 6 확인 요약을 planning 입력으로 사용자 확인 | Round 1~5에서 확정한 권한·입력·자동 재고·실사 경계·목표 이력·홈 그래프·출근표와 손익관리 후속 제외를 하나의 요약으로 확인했고 blocking 결정 0건으로 interview를 `COMPLETED_CONFIRMED` 처리한다. 이 확인은 planning 결과 승인이나 제품 구현 승인이 아니다. | 6.4, TASK-G2-OPERATIONS-001 interview round 6 |
| 2026-08-18 | `TASK-G2-OPERATIONS-001` planning 작성자를 사용자 명시 지시로 Codex로 전환하고 별도 기획안·검토서를 작성 | Fable planning이 현재 세션 한도로 artifact 없이 실패한 뒤 사용자가 `codex 기획해봐`라고 지시했다. 표준 Fable 원문 경로는 비워 두고 사용자 확인 interview를 기준으로 Codex 기획안과 내용·제품 review를 별도 파일에 작성했으며, 사용자 resolution·구현·Git 게시 승인은 계속 대기한다. | 6.4, TASK-G2-OPERATIONS-001 Codex planning·review |
| 2026-08-18 | `TASK-G2-OPERATIONS-001` Codex 기획안·review resolution과 구현 시작을 승인 | 모든 blocking 제품 결정이 닫힌 독립 G2 일일 운영관리 계약을 최신 `origin/main` 기준으로 구현한다. 이번 승인은 제품 코드·격리 검증까지이며 commit·push·PR·merge·Persistent UAT·Azure 공개배포는 포함하지 않는다. | 6.4, TASK-G2-OPERATIONS-001 Change 001 |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` 독립 구현과 자동 검증을 완료하고 사용자 검수로 전환 | migration `0081`, 역할별 field 권한, metric별 CAS, 자동 재고·실사 경계, 적용일별 목표와 G2 세 화면을 구현했다. Backend `547/547`, Frontend `222/222`, isolated Full-Stack과 1440/390px 검증을 통과했으며 손익관리·미정정 예상값 확인은 후속으로 유지한다. Git 게시·Persistent UAT·Azure 공개배포는 별도 승인 전 실행하지 않는다. | 6.4, TASK-G2-OPERATIONS-001 implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 003으로 가로형 월간 표·날짜 필터·그래프 가독성을 보정 | 모든 보이는 월간 표를 날짜 열·항목 행으로 통일하고 홈 공용 날짜 범위와 관리 화면별 표 범위를 추가했다. 그래프 축·색상·눈금·예상 영역을 강화하고 그래프 가로 스크롤을 제거했으며 Frontend `223/223`, production build, desktop 1440·mobile 390 live browser 검증을 통과했다. 기존 API·DB·권한·재고 계산과 Git·UAT·Azure 경계는 변경하지 않는다. | 6.4, TASK-G2-OPERATIONS-001 Change 003·implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 004로 홈 생산표·출근 합계 disclosure와 그래프 시각 위계를 추가 | 홈 공용 날짜 범위를 따르는 생산 현황 표를 추가하고, 출근표는 오전·오후 합계만 기본 표시한 뒤 각 조 EMI·도급을 독립적으로 펼치게 했다. 그래프에 계열별 gradient·둥근 막대·plot 배경·예상 label·재고 point를 적용하고 Frontend `223/223`, build, desktop 1440·mobile 390 검증을 통과했다. Backend·DB·권한과 게시 경계는 변경하지 않는다. | 6.4, TASK-G2-OPERATIONS-001 Change 004·implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 005로 사용자 지정 pastel graph·생산표 재고·합계 숫자 disclosure를 반영 | 생산 주황·납품 파랑·재고 빨강·목표 파랑과 오전 연한 파랑·오후 진한 파랑·생산목표 빨강 계약을 적용했다. 공통 Graphite grayscale cascade를 G2 승인 예외로 분리하고, 생산표 목표를 재고로 교체하며 날짜별 출근 합계 숫자도 펼침 button으로 만들었다. Frontend `223/223`, build, desktop/mobile 색상·interaction 검증을 통과했다. | 6.4, TASK-G2-OPERATIONS-001 Change 005·implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 006으로 조별 생산 segment 연결과 출근 합계 cell interaction을 보정 | 오전·오후 segment를 하나의 rounded clip 안에 붙이고 outer outline으로 날짜별 일 생산 막대를 강화했다. 출근 합계 header·숫자 button의 persistent chrome을 제거하고 cell 전체 hit area·row hover·keyboard focus로 표 자체 클릭 표현을 만들었다. Frontend `223/223`, build와 live browser 검증을 통과했다. | 6.4, TASK-G2-OPERATIONS-001 Change 006·implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 007로 fixed graph axes와 막대 baseline을 확정 | 생산 파랑·납품 주황, 생산·납품 `0~100/20`, 재고 `-70~130/50`, 조별 `0~60/10`을 적용했다. 모든 막대를 top-rounded·flat-bottom path로 만들고 굵은 `0` baseline에 연결했으며 Frontend `223/223`, build, desktop/mobile tick·색상·바닥 검증을 통과했다. | 6.4, TASK-G2-OPERATIONS-001 Change 007·implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 008로 graph hover 안내와 가로 grid 대비를 보강 | 생산·납품·조별 막대와 재고·목표 선에 날짜·항목·수량 tooltip을 추가하고 plot 경계에서 위치를 보정했다. 가로 grid를 `#c5d1df/1.2px`로 한 단계 진하게 조정했으며 Frontend `223/223`, build, desktop 1440·mobile 390 실제 hover·clipping·overflow 검증을 통과했다. | 6.4, TASK-G2-OPERATIONS-001 Change 008·implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 009로 graph 상시 수치와 home 표 구성을 보강 | graph 최대 폭을 `1180px`로 늘리고 전 날짜 재고 point·수량, 실사 blue point·수량, 생산·납품 상단과 조별 segment 내부 수량을 표시했다. 목표 관리를 생산 현황 바로 위로 옮기고 pastel red 재고행·출근 오전·오후 전체 합계행을 추가했으며 Frontend `223/223`, build와 desktop/mobile 검증을 통과했다. | 6.4, TASK-G2-OPERATIONS-001 Change 009·implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 010으로 graph 숫자 밀도·재고축과 생산표 재고행 강조를 보정 | 두 graph의 축·상시 값 숫자를 줄이고 선택 기간의 모든 날짜를 표시했다. 재고축은 `0~180/20`, 재고 point는 작은 채움형 red·실사 blue로 고정했으며 생산표 재고행은 빨간 글씨 대신 pastel red 배경·굵은 상단선을 적용했다. Frontend `223/223`, build와 desktop/mobile live 화면 검증을 통과했다. | 6.4, TASK-G2-OPERATIONS-001 Change 010·implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 011로 graph 날짜·생산축과 주말·공휴일 표시를 보정 | graph 날짜를 더 작고 baseline 가까이에 배치하고 생산·납품 왼쪽 축을 `0~80/20`으로 조정했다. 주말·활성 한국 공휴일을 두 graph와 모든 G2 가로표에서 pastel red로 표시하고 과거 header `실적` 문구를 제거했으며, local 검수의 8월 일 생산목표를 전 날짜 `50대`로 맞췄다. Frontend `224/224`, build와 desktop/mobile live 화면 검증을 통과했다. | 6.4, TASK-G2-OPERATIONS-001 Change 011·implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 012로 생산·납품 막대와 재고·목표선 교차 및 휴일 표 강조를 보정 | graph 분리나 비선형 축 없이 막대 폭·opacity를 줄이고 실제·예상 재고와 재고목표 아래에 white halo를 추가했다. 모든 G2 가로표의 휴일 header 배경을 제거하고 해당 날짜 열의 header·값·합계 interaction·예상 글자를 red로 통일했으며 Frontend `224/224`, build와 desktop/mobile live 화면 검증을 통과했다. | 6.4, TASK-G2-OPERATIONS-001 Change 012·implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 013으로 생산·납품·재고의 크기 순서와 휴일 header border를 보정 | 독립 축 때문에 생산 50이 재고 90보다 높게 보이던 모순을 없애기 위해 모든 flow 계열에 `0~60=70%`, `60~180=30%` 공통 단조 scale을 적용하고 좌우 공통 tick·60 break marker·scale 안내를 추가했다. 휴일 header 아래 누락된 dark border도 평일과 같은 `1px`로 복구했으며 Frontend `224/224`, build와 desktop/mobile live 화면 검증을 통과했다. | 6.4, TASK-G2-OPERATIONS-001 Change 013·implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 014로 제조 인원 출근 관리 월간표의 세부 인원 disclosure를 홈과 통일 | 월간표는 오전·오후 합계와 하루 총원을 기본으로 표시하고 왼쪽 합계 header 또는 날짜별 합계 숫자를 누를 때 해당 조 EMI·도급만 독립적으로 펼치게 했다. 기존 입력·API·권한과 graph `//` scale marker는 유지했으며 Frontend `225/225`, build와 desktop/mobile live 화면 검증을 통과했다. | 6.4, TASK-G2-OPERATIONS-001 Change 014·implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 015로 graph annotation·tooltip·mobile 내부 탐색과 홈 생산표를 보정 | 압축 문구·`//` marker를 제거하고 생산·납품 수치 충돌과 blue 실사 `0대` hover를 고쳤다. Mobile은 좌우 숫자 축·frame·grid를 고정하고 가운데 날짜만 5일 단위로 drag하며 막대와 graph 숫자를 키웠고, 새 layer의 monochrome cascade를 해소했다. 홈 생산표에는 납품행을 추가하고 두 관리표의 중복 구분행은 제거했다. Frontend `226/226`, typecheck, lint error 0, build와 desktop live projection·mobile fixed-frame unit을 통과했다. 관리자 입력·수정 이력은 새 history 저장·권한·API가 필요한 후속 `NEW_FEATURE`로 분리한다. | 6.4, TASK-G2-OPERATIONS-001 Change 015·implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 016으로 미래 예상 숫자의 날짜 도래 자동 초기화를 구현 | migration `0082`에서 저장 당시 미래 수량을 식별하고 기존 미래값을 backfill한다. 서울 날짜가 도래한 뒤 첫 G2 조회·저장은 생산·납품·출근 7개 metric의 예상 숫자만 빈 값으로 바꾸며 예상 `0`도 초기화하고 당일 실제값·실제 `0`·CAS·재고 실사·목표는 보존한다. Backend `548/548`, Frontend `226/226`, fresh/forward migration과 TimeProvider 기반 DB 회귀를 통과했고 local 검수 DB에도 forward migration을 적용했다. | 6.4, TASK-G2-OPERATIONS-001 Change 016·implementation report, migration `0082` |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 017로 조별 총 생산량·평균선과 graph KPI를 보강 | 조별 누적 막대 위에 날짜별 총 생산량을 표시하고 선택 기간 총 생산 평균을 파스텔 청록 점선·hover로 추가했다. 첫 graph 오른쪽에는 생산·납품·재고 평균과 마지막 계산 가능일의 재고 부족분, 둘째 graph 오른쪽에는 오전·오후조 평균을 배치했으며 날짜 filter와 함께 다시 계산한다. 재고 부족분 `i`는 `재고목표 - 재고` 공식을 hover·focus로 안내하고 좁은 화면에서는 KPI를 graph 아래 2열로 재배치한다. Frontend `226/226`, typecheck, lint error 0, build와 local desktop visual projection을 통과했다. | 6.4, TASK-G2-OPERATIONS-001 Change 017·implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 018로 조별 통합 hover·평균선 layer와 KPI 기준일·색상을 보정 | 조별 막대 위 합계 label을 제거하고 날짜별 단일 hit area가 오전·오후·전체를 3줄로 안내하게 했다. 총 생산 평균선은 막대·숫자 아래의 순수 파랑 점선으로 옮겼고, KPI 왼쪽 강조선을 제거하며 오전·오후 KPI를 막대와 같은 gradient로 맞췄다. 재고 부족분은 서울 기준 오늘의 `재고목표 - 재고`로 고정하고 `i` 안내를 위쪽에 배치했다. Frontend `226/226`, typecheck, lint error 0, build와 local desktop visual projection을 통과했다. | 6.4, TASK-G2-OPERATIONS-001 Change 018·implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 019로 재고 부족분 `i` 안내를 KPI 카드 내부로 보정 | 그래프를 가리던 바깥쪽 안내 대신 재고 부족분 카드 안쪽 6px 영역에 불투명 pastel violet overlay를 표시하고 hover·keyboard focus를 함께 유지했다. 공통 Graphite normalization의 위치·배경 충돌을 G2 semantic exception으로 해소했으며 targeted `8/8`, Frontend `226/226`, typecheck, lint error 0, build와 local Desktop 카드 내부 포함·page overflow 0 visual QA를 통과했다. | 6.4, TASK-G2-OPERATIONS-001 Change 019·implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001` Change 020으로 필수 수량·서울 날짜·월 조회 순서 결함을 보정 | 빈 실사·목표를 `0`으로 저장하던 P1을 차단하고 실제 `0`은 유지했다. Frontend 초기 날짜를 Backend와 같은 서울 기준으로 통일하고 월별 effect 단일 조회·request sequence 최신 응답 우선·같은 달 자료 재사용을 적용했다. 관리자의 입력·수정 이력은 사용자 지시에 따라 별도 NEW_FEATURE로 유지했으며 Frontend `230/230`, G2·migration `64/64`, isolated Full-Stack과 local non-mutating 화면 검수를 통과했다. | 6.4, TASK-G2-OPERATIONS-001 Change 020·implementation report |
| 2026-08-19 | `TASK-G2-OPERATIONS-001`의 원격 `main` 병합과 `TASK-AZURE-DEPLOY-001 Change 027` 공개배포를 승인 | 관리자의 입력·수정 이력과 손익관리는 제외한 현재 G2 범위를 최신 원격 `main` 위에서 전체 재검증하고 Ready PR 필수 CI 뒤 squash merge한다. 병합된 exact latest `main` SHA에 migration `0081`·`0082` → Backend → Frontend를 적용하고 기존 인증·외부 알림·Key Vault 참조와 익명 접근 차단을 보존한다. 사용자 직접 화면 검수는 배포 후 대기 상태를 유지한다. | 6.2·6.4, TASK-G2-OPERATIONS-001 implementation report, TASK-AZURE-DEPLOY-001 Change 027 |
| 2026-08-28 | `TASK-AUDIT-001` 로그인·데이터 변경 감사 원장의 local 구현·일반 자동 회귀를 마쳤지만 승인된 acceptance coverage·Duplicate 분류 계약 gap으로 release candidate를 차단 | migration `0083` 후보와 endpoint 분류 `185/185`(포함 156·제외 29), relation 분류 `145/145`(추적 94·제외 51), append-only field projection, 대화형 로그인 correlation, 실패 저장 시도, 관리자 통합 조회·선택 Excel, Backend `566/566`, migration `58/58`, Frontend `235/235`, local 동시 mutation `50/50`과 desktop·375px visual을 확인했다. 독립 검증으로 actual actor 권한 원장·purge, 409 오분류, attachment MIME, multi-tab correlation, 업무 body projection 결함을 보정했으나, 포함 route 156개의 성공·no-op·rollback 1:1 실행 증빙(P1)과 Duplicate typed signal(P2)은 없다. 두 계약을 구현하거나 사용자가 명시적으로 완화하기 전에 검수·Git 게시·Persistent UAT·Azure 운영 적용을 시작하지 않는다. | 3.3L, 추적 48, TASK-AUDIT-001 implementation report |
| 2026-08-28 | `TASK-AUDIT-001` Change 002로 exact catalog+중앙 transaction 대표 검증과 보수적 409 Conflict 분류를 v1 acceptance로 승인하고 독립 재검증 PASS를 확인 | endpoint `185/185`·relation `145/145` exact 분류, 실제 PostgreSQL 성공·accepted no-op·caller rollback·audit append 실패 rollback·privacy·append-only·권한 대표 fixture와 local 동시 mutation `50/50`을 완료 기준으로 확정했다. 포함 route 156개의 1:1 matrix는 모든 route의 개별 업무 규칙을 실행 증명하지 않는다는 한계를 명시하고 v1 acceptance에서 제외했다. `Duplicate`는 Backend·DB·UI에서 제거하고 400/422 `Validation`, 409/412 `Conflict`만 사용한다. 보정 후 Backend `567/567`, migration `59/59`, Frontend `235/235`, 독립 재검증 PASS·Open P0/P1/P2 `0/0/0`, local release candidate `READY`를 확인했다. Local commit만 승인됐으며 Push·PR·main merge·Persistent UAT·Azure release는 별도 승인으로 유지한다. | 3.3L, 추적 48, TASK-AUDIT-001 Change 002·implementation report |
| 2026-08-28 | `TASK-AUDIT-001` Change 003 원격 main 병합과 Azure 공개배포를 승인 | 검증된 local release candidate를 Ready PR 필수 CI 뒤 squash merge하고 exact latest main SHA로 additive migration `0083` → Backend → Frontend를 운영 적용한다. 기존 인증·Front Door 익명 차단·외부 알림 활성 설정·Key Vault 참조와 업무 데이터를 보존하며 local Persistent UAT handover·실제 외부 알림 시험 발송은 제외한다. 공개 security smoke 뒤 사용자 직접 감사 화면 검수를 진행한다. | 3.3L, 6.2, 추적 48, TASK-AUDIT-001 Change 003·implementation report |
| 2026-08-28 | `TASK-AUDIT-001` PR `#111` main 병합과 Azure 공개배포 완료 | PR 필수 CI `33136383870`, 운영 source exact main SHA `6713e5974ad5262d87d7cc2332b27486d2487ccd`의 main CI `33137735821`, Azure release `33137792491`이 모두 통과했다. Migration `0083`·Backend·Frontend·public security smoke가 `PASS`였고 독립 공개 확인은 health `200`, 익명 root·API `401/401`이다. Local Persistent UAT handover와 실제 외부 알림 시험 발송은 실행하지 않았으며 사용자 직접 감사 화면 검수와 운영 aggregate 관찰을 다음 Gate로 유지한다. | 3.3L, 6.2, 추적 48, TASK-AUDIT-001 Change 003·implementation report |
| 2026-08-31 | `TASK-AUDIT-001` 공개 로그인 기록 미생성 P1을 Change 004로 local 보정하고 독립 검증 완료 | 개인정보 제외 운영 aggregate에서 login endpoint 호출 `0`, Backend 저장 실패 `0`을 확인했다. MSAL Browser v5의 `LOGIN_SUCCESS` payload는 `AccountInfo` 자체지만 Frontend가 이전 `AuthenticationResult.account` 형식으로 읽어 pending login 생성 전에 반환한 것이 root cause다. v5 payload 직접 처리, request correlation과 로그인 시작 탭 소유권, API pending과 cross-tab 차단 marker 분리를 추가해 auth `23/23`, Frontend `238/238`, typecheck, lint error `0`, production build와 diff check를 통과했다. 독립 재검증도 PASS·Open P0/P1/P2 `0/0/0`이며 Backend·DB·migration과 과거 로그인 소급은 없고 Push·PR·merge·Azure release는 미승인이다. | 3.3L, 추적 48, TASK-AUDIT-001 Change 004·implementation report |
| 2026-08-31 | `TASK-AUDIT-001` Change 004 공개배포 승인 | 사용자가 검증된 source/test diff의 Commit·Push·Ready PR·main merge와 exact main SHA Azure Frontend 공개배포를 명시 승인했다. Backend·migration·과거 로그인 소급과 실제 외부 알림 발송은 제외한다. | 3.3L, 6.2, 추적 48, TASK-AUDIT-001 Change 004 |
| 2026-08-31 | `TASK-AUDIT-001` Change 004 PR `#113` main 병합과 Azure Frontend 공개배포 완료 | PR CI `33356499110`, exact main SHA `6e2b00de494995cd9901003c76912c481e4424d2`의 main CI `33358318439`, Azure release `33358365813`이 통과했다. Frontend·public security는 `PASS`, Backend·migration은 `SKIPPED`이고 별도 공개 확인은 health `200`, 익명 root·API `401/401`이다. 다음 Gate는 완전 로그아웃 뒤 새 Microsoft Redirect 로그인 1건의 앱 복귀와 운영 aggregate·감사 row `+1` 확인이다. | 3.3L, 6.2, 추적 48, TASK-AUDIT-001 Change 004·implementation report |
| 2026-08-31 | `TASK-SITE-ACCESS-001`을 Roadmap 순서 변경으로 시작하고 유지 세션 포함 사이트 접속 이력을 별도 신규 기능으로 승인 | 사용자는 새로고침·다른 페이지 진입을 접속 신호로 사용하고 같은 사용자·같은 브라우저 client·30분 미만 활동을 한 행으로 묶는 방향을 확인했다. Fable interview 요약과 planning, Codex review 뒤 명시적 로그아웃 종료 권장안 A와 구현 시작을 승인했다. 기존 Login/Logout·변경 감사, `Audit.Read.All`, 개인정보 최소화와 별도 게시 승인 경계를 보존한다. 당시 추적 97 후보는 최신 main 통합 시 공개 G2와 충돌해 Change 002에서 98로 교정한다. | 3.3M, 추적 98, TASK-SITE-ACCESS-001 interview·planning·review·Change 001·002 |
| 2026-09-01 | `TASK-G2-OPERATIONS-002`를 단순 NEW_FEATURE 예외로 Codex가 직접 기획·구현하도록 승인하고 현재 Roadmap Gate보다 우선 | 납품 목표·불량 정식 데이터와 홈 저장 없는 임시 시뮬레이션의 범위·권한·수명주기를 사용자가 모두 확정해 Fable interview가 필요하지 않다. 최신 원격 main의 격리 branch에서 코드·자동 검증까지만 진행하고 Commit·Push·PR·Merge·Persistent UAT·Azure는 별도 승인으로 유지한다. | 6.5, 추적 97, TASK-G2-OPERATIONS-002 identity gate·Codex planning·review |
| 2026-09-01 | `TASK-G2-OPERATIONS-002` local 구현과 자동 검증 완료 | Additive migration `0084`, 불량 차감 재고·forecast expiry·권한/CAS, 납품 목표와 홈 임시 입력·주황 점선·예상/휴일 색·390px 날짜 입력을 Backend·Frontend·isolated Full-Stack·visual QA로 확인했다. 사용자 직접 화면 검수와 Git 게시·운영 적용은 대기한다. | 6.5, 추적 97, TASK-G2-OPERATIONS-002 implementation report |
| 2026-09-01 | `TASK-G2-OPERATIONS-002` 사용자 검수 완료와 원격 main 병합 승인 | 사용자가 격리 검수 서버에서 G2 홈과 synthetic 자료, Change 001 직접 입력 중앙 정렬·증감 버튼 제거를 확인하고 원격 main 병합을 명시 승인했다. Commit·Push·Ready PR·필수 CI·main merge를 포함하며 Persistent UAT, migration `0084` 운영 적용과 Azure 공개배포는 제외한다. | 6.5, 추적 97, TASK-G2-OPERATIONS-002 Change 001·002·implementation report |
| 2026-09-01 | `TASK-G2-OPERATIONS-002` PR #115 main 병합과 Azure 공개배포 완료 | exact main `220d1201c9dbb881fb3e5c5061871fb943c7961b`의 G2 migration `0084`, Backend, Frontend와 공개 보안 검증이 통과했다. Persistent UAT는 적용하지 않았다. | 6.5, 추적 97, TASK-G2-OPERATIONS-002 implementation report·Azure release evidence |
| 2026-09-01 | `TASK-SITE-ACCESS-001` local 구현과 자동·격리 Full-Stack·독립 검증 완료 | 최초 후보는 additive migration `0084`였으나 최신 공개본의 G2 migration과 번호가 충돌해 Change 002에서 사이트 접속 migration을 `0085`로 교정한다. 서버/DB 권위 시각과 advisory lock, 19개 고정 메뉴, bounded best-effort signal/end, 전체 감사 이력 목록·상세·별도 coverage·선택 Excel을 구현했다. strict 30분 경계·동시성·불변 원장·권한·멈춘 요청·Web Locks 미지원 및 localStorage 차단 두 탭 수렴·Desktop 1440px·Mobile 390px을 synthetic 환경에서 확인했고, 최신 main 통합 후 전체 자동·독립 검증을 다시 수행한다. 사용자 local 화면 검수는 대기이며 Git 게시·원격 main 병합은 승인, Persistent UAT·Azure 공개배포는 미승인이다. | 3.3M, 추적 98, TASK-SITE-ACCESS-001 Change 002·implementation report·SOP·User manual·checklist |
| 2026-09-01 | `TASK-SITE-ACCESS-001` Change 002로 migration `0085`·추적 `98` 교정과 최신 공개본 통합·원격 main 병합을 승인 | 공개본의 G2 추적 `97`과 `0084_g2_delivery_target_defect.sql`을 그대로 보존하고 사이트 접속을 추적 `98`, migration `0085_site_access_sessions.sql`로 변경한다. 최신 main의 G2 기능과 사이트 접속 기능을 함께 전체 회귀·각 Full-Stack·독립 검증하고 필수 CI가 통과한 동일 head만 원격 main에 병합한다. 사용자 직접 화면 검수는 대기 상태를 유지하며 Persistent UAT·운영 migration·Azure 공개배포·실제 provider mutation은 제외한다. | 3.3M, 추적 98, TASK-SITE-ACCESS-001 Change 002·implementation report |
| 2026-09-01 | `TASK-SITE-ACCESS-001` latest-main 통합과 자동·Full-Stack 재검증 완료 | 검증된 제품 통합 commit `6ca27d5f2552eb367279f2899b872f82cd03fccb`에서 공개 G2 `0084`·추적 97과 사이트 접속 `0085`·추적 98을 함께 보존했다. Backend `570/570`, Frontend `248/248`, 사이트 접속·G2 Full-Stack 각 `1/1`, build 경고·오류 `0/0`과 final desktop/mobile 증빙을 확인했다. 최종 독립 검증·Ready PR·CI Gate는 진행 전이며 Persistent UAT·Azure 배포는 제외한다. | 3.3M, 추적 98, TASK-SITE-ACCESS-001 Change 002·implementation report |
| 2026-09-01 | `TASK-SITE-ACCESS-001` 최종 독립 1차 검증에서 제품 PASS·문서 Gate P2 발견 | exact artifact commit `b514e236728741e39905c6424d3a3acaf48061cd`의 제품·통합·G2 보존과 자동 검증 연결은 PASS했다. Roadmap 3.3M·report·checklist의 게시 승인, 독립검증과 artifact commit 상태가 서로 달라 `SITE-ACCESS-FINAL-F01` P2를 열었다. 상태 문서만 실제 Gate로 동기화하고 read-only 재확인 전 게시하지 않는다. | 3.3M, 추적 98, TASK-SITE-ACCESS-001 implementation report |
| 2026-09-02 | `TASK-G2-OPERATIONS-002 Change 003` 전일 실적 기반 출하 가능 재고 수정·공개배포 승인 | 사용자는 2026-08-28 재고를 `6대`로 확정하고, 해당 날짜에 언제든 출하할 수 있는 재고가 되도록 전일 생산·납품·불량을 모두 다음 날짜로 넘기는 수식을 승인했다. 2026-08-27까지 기존 수식과 실사 우선, migration·원본 데이터 불변을 보존하며 Ready PR·필수 CI·exact main Azure Change 028과 공개 read-only 확인까지 실행한다. | 6.5, 추적 97, TASK-G2-OPERATIONS-002 Change 003, TASK-AZURE-DEPLOY-001 Change 028 |
| 2026-09-01 | `TASK-SITE-ACCESS-001` 문서 P2 1차 보정과 재확인 잔여를 현재형으로 동기화 | `aea583611cc6df79d523d752eed78c2f6f98db05`에서 Roadmap·report·SOP·checklist를 동기화했으나 독립 재확인에서 report의 “보정 commit 전”과 다음 Gate 문장이 stale인 것을 확인했다. 제품·테스트·migration은 바꾸지 않고 두 문장과 Decision Log를 교정했으며 current HEAD 최종 read-only 재확인 전 게시하지 않는다. | 3.3M, 추적 98, `SITE-ACCESS-FINAL-F01`·implementation report |
| 2026-09-01 | `TASK-SITE-ACCESS-001` 문서 P2 독립 재확인 PASS·local GO | exact `0274756ab300827d62ee385a83d66773d346b6ca`에서 Roadmap 3.3M·추적 98·Decision Log와 report의 승인·검증·artifact 상태가 일치하고 `6ca27d5` 이후 제품·테스트·migration 변경이 없음을 확인했다. `SITE-ACCESS-FINAL-F01`을 RESOLVED하고 Open P0/P1/P2 `0/0/0`으로 Push·Ready PR·CI Gate를 재개한다. | 3.3M, 추적 98, TASK-SITE-ACCESS-001 implementation report |
| 2026-09-02 | `TASK-AZURE-DEPLOY-001 Change 029` current-main 전체 공개배포 승인 | Change 028 첫 run에서 actual current-main에 사이트 접속 migration `0085`가 포함됨을 확인해 Environment 승인 전에 취소하고 운영 mutation을 0으로 유지했다. 사용자는 PR #116 사이트 접속과 PR #117 G2 수정을 포함한 exact current-main 전체 배포를 명시 승인했다. | 3.3M, 6.2, 6.5, 추적 97·98, TASK-AZURE-DEPLOY-001 Change 029 |
| 2026-09-02 | exact current-main Azure 공개배포와 공개 확인 완료 | Exact source `58daf6d8bfe333cb00e343a3fcc13ee4f3358183`의 Azure release `33577473523`에서 migration `0085`·Backend·Frontend·public security가 통과했다. 별도 확인은 health `200`, 익명 root·API `401/401`, 인증된 G2 8월 28일 재고 `6대`, 사이트 접속 coverage·양수 summary다. 기존 인증·외부 알림·Key Vault·업무/G2 원본 데이터를 보존했고 Persistent UAT와 실제 외부 알림 시험 발송은 제외했다. | 3.3M, 6.2, 6.5, 추적 97·98, TASK-AZURE-DEPLOY-001 Change 029 |
| 2026-09-02 | `TASK-G2-OPERATIONS-002 Change 004` 당일 납품 기반 재고 수정·공개배포 승인 | 사용자는 2026-08-28부터 전일 생산·불량은 다음 날짜로 넘기고 표시 날짜의 납품은 그 날짜 재고에서 차감하는 수식을 확정했다. Backend·Frontend·전체/부분/하루 단독 조회와 격리 Full-Stack 검증 뒤 Ready PR·필수 CI·exact main SHA Azure 전체 공개배포를 한 번에 진행하며 migration·운영 G2 원본 데이터·Persistent UAT는 변경하지 않는다. | 6.2, 6.5, 추적 97, TASK-G2-OPERATIONS-002 Change 004 |

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
| 키팅 완료 알림 | 선택형 자재 준비 참고 정보 | 제조 투입 조건·업무 생성 아님 |
| 제조 투입 요청 | 생산관리의 패널별 제조 착수 지시 | 제조 정·부 내 업무·인앱 알림 생성 |
| 납품 완료 | 고객 납품 완료 | 출하완료 대신 사용 |
| 영업 정산 | 납품 후 세금계산서 및 완료 처리 | 최종 단계 |
| 세금계산서·완료 | 영업 정산 완료와 프로젝트 완료 | 18단계 마지막 |

## 27. Repository 작업 지침과 제품 불변조건

개발 작업 방식은 [Root AGENTS.md](../AGENTS.md)와 경로별 하위 지침을 따른다. 종료·Finding·사용자 검수는 [Task 종료 및 산출물 정책](12-task-completion-policy.md), 변경 유형별 테스트는 [Validation Matrix](development/validation-matrix.md), 비식별 증빙은 [Privacy-safe Evidence](development/privacy-safe-evidence.md)가 canonical source다. 이 Roadmap은 해당 절차를 중복하지 않고 제품 방향, Task 상태와 결정 이력을 관리한다.

제품 변경 시 다음 불변조건을 확인한다.

- 공식 사용자 표시명은 `EMI PMS`이며 한국어 전체 이름 `EMI 프로젝트 통합관리시스템`과 영문 의미 `EMI Project Management System`은 설명 문구에서만 사용한다. 내부 `Emi.Qms` solution/namespace는 유지한다.
- 18단계 업무 순서, QR 기준, 패널 단독 용어와 필수 workflow 기반 진행률을 임의 변경하지 않는다.
- Backend stack을 전환하지 않고 권한과 업무 규칙은 서버에서 강제한다.
- 검수 사용자 전환은 Development/Testing/UAT의 System Administrator와 dev persona 범위이며 실제 Entra impersonation으로 확장하지 않는다.
- MSAL cache, MFA, 조건부 액세스와 sign-in frequency를 우회하거나 token을 앱 코드에서 직접 storage에 저장하지 않는다.
- Teams Activity, Mail/TeamsChannel 양식과 event coverage는 6장의 확정 상태를 따르며 correlation id를 사용자 메시지에 노출하지 않는다.
- 영업일 계산은 `BusinessDayCalculator`, 에스컬레이션은 TASK-NOTIFY-POLICY-001의 정확한 일정 원본 기반 `work_items.due_date`와 L0·L1 정책을 사용한다.
- 관리자 삭제는 유예·복구·참조 무결성을 보존하고 업무 부서 기준정보를 사용자 결정 없이 관리자 페이지로 통합하지 않는다.
- 사용자-facing 문구는 한글로 작성하고 확정사항·미확정사항·후속 Task를 구분한다.

Roadmap 변경 후에는 문서 link, 공식 명칭, 18단계 순서, RPP 기준값, 패널 용어, 진행률 공식, 추적 대상과 Decision Log가 유지되는지 검증한다.
