# 4. 역할·권한표

권한은 Permission과 Authorization Policy로 검사합니다. Role Code 문자열을 여러 코드 위치에서 직접 비교하지 않습니다.

## 공통 원칙

- 모든 활성 사내 사용자는 모든 프로젝트를 조회할 수 있습니다.
- 모든 활성 사내 사용자는 모든 부서 기능을 조회할 수 있습니다.
- 등록·수정·완료·취소는 해당 업무 담당 부서만 가능합니다.
- UI 메뉴나 버튼 숨김은 보조 수단이며, 모든 쓰기 API는 서버에서 권한을 검사합니다.
- `UserProjectAccess`는 삭제하지 않고 향후 외부 사용자 또는 제한 계정을 위해 유지합니다.

| 역할 | 주요 조회 | 주요 입력·수정 | 승인·완료 |
|---|---|---|---|
| System Administrator | 전체 프로젝트, 전체 부서 기능, 민감정보 | 사용자, 역할, 기준정보 | 권한 변경·정정 관리 |
| Sales | 전체 프로젝트, 전체 부서 기능, 판매금액, 제조 소요시간 | 프로젝트 등록·수정, 면수 변경, 보류·취소·해제 | 프로젝트 완료 기준 확인 |
| Production Planning | 전체 프로젝트, 전체 부서 기능 | 생산계획, 프로젝트별 업무 담당자 지정 | 생산계획 완료 |
| Manufacturing | 전체 프로젝트, 전체 부서 기능 | 제조단계 시작·종료, 작업 중단 사유, 제조 부적합 조치 | 제조단계 완료, 조치 완료 |
| Quality | 전체 프로젝트, 전체 부서 기능 | 검사결과, 측정값, 사진, NCR, 재검사 | 검사 완료, 품질 승인 |
| Logistics | 전체 프로젝트, 전체 부서 기능 | 물류정보 | 물류 단계 완료 |
| Read Only | 전체 프로젝트, 전체 부서 기능 | 없음 | 없음 |

설계와 구매 역할은 확정 업무 역할로 문서화하지만, 현재 0001 기준 Role Code에는 아직 없습니다. TASK-003A 프로젝트 등록 기능은 Sales 권한으로 진행하고, Design Role + `PanelInfo.Update`는 TASK-003B 설계 패널정보 기능 구현 전에 필수로 추가합니다. Procurement Role + `ProcurementPlan.Update`는 구매 기능 TASK 전에 추가합니다.

## Permission 기준

| Permission | 허용 역할 | 비고 |
|---|---|---|
| `projects.read` | 내부 업무 역할, Read Only, 제한 계정 | 프로젝트 조회 기본 권한 |
| `Project.Read.All` | System Administrator, Sales, Production Planning, Manufacturing, Quality, Logistics, Read Only | 활성 사내 사용자의 전체 프로젝트 조회 |
| `projects.access.all` | 없음 | TASK-002 0001 호환을 위해 남은 legacy/deprecated 권한입니다. 새 Endpoint와 Policy는 전체 프로젝트 조회에 이 권한을 사용하지 않습니다. |
| `projects.manage` | System Administrator, Sales | 프로젝트 등록·수정·보류·취소·해제 |
| `production.plan` | legacy | 초기 문서 호환용 legacy 권한입니다. 신규 생산계획 수정 API는 사용하지 않습니다. |
| `ProductionPlan.Update` | Production Planning | 생산계획과 프로젝트 담당자 지정 입력 |
| `manufacturing.update` | System Administrator, Manufacturing | 제조정보 입력·수정 |
| `quality.inspect` | System Administrator, Quality | 품질 검사 입력 |
| `quality.approve` | System Administrator, Quality | 품질 승인 |
| `logistics.ship` | System Administrator, Logistics | 물류정보 입력 |
| `users.manage` | System Administrator | 사용자·역할 관리 |
| `Project.SalesAmount.Read` | System Administrator, Sales | 판매금액 민감정보 조회 |
| `Manufacturing.WorkTime.Read` | System Administrator, Sales | 제조 소요시간 민감정보 조회 |

`Project.Read.All`이 없는 제한 계정은 `UserProjectAccess`에 배정된 프로젝트만 조회할 수 있습니다. `Project.Read.All`은 쓰기권한이 아니며, 프로젝트 쓰기는 `projects.manage` 같은 별도 Permission으로 검사합니다.

`projects.access.all`은 현재 전체 프로젝트 조회를 허용하지 않습니다. 이 legacy 권한은 의존성 확인 후 별도 제거 Migration이 필요할 수 있습니다.

## 필수 서버 권한 테스트

- 모든 활성 내부 역할과 Read Only가 프로젝트 A/B를 모두 조회할 수 있어야 함
- 제한 계정은 배정 프로젝트만 조회하고 미배정 프로젝트는 거부되어야 함
- 비활성 사용자는 인증 실패 처리되어야 함
- 제조 사용자는 프로젝트를 조회할 수 있지만 프로젝트 수정은 거부되어야 함
- 품질 사용자는 제조정보를 조회할 수 있지만 제조정보 수정은 거부되어야 함
- Sales와 System Administrator만 판매금액과 제조 소요시간을 조회할 수 있어야 함
- UI에서 메뉴를 숨겨도 직접 API 호출은 거부되어야 함
