# 청주·오산 실행 프로그램과 접속 권한 분리 설계

- 상태: **기존 backend1개·업무 모듈/DB 구조 분리 방향 사용자 확정 / 운영 미적용**, 2026-09-29. 사용자가 비용을 이유로 Container App2개 추가안을 채택하지 않고 현재 앱 수를 유지하도록 요청한 뒤 “업무 처리와 DB 구조를 분리하면 됨 — 기존 백엔드 1개 유지”를 선택했다. **§1~9는 이전 제안과 그 근거이며 실행 승인안이 아니다. 현재 설계는 §10의 A를 따른다.** 동일 프로세스가 양쪽 접속 정보를 보유한다는 설명을 들은 후의 선택이며, 실행 프로그램별 credential 격리까지 요구하지 않는다. 새 Azure 자원 생성·권한 변경·운영 migration·배포는 실행하지 않는다.
- 소유: [Change031](azure-deploy-001-change-031.md). [확정한 표 범위와 열 설계](azure-deploy-001-change-031-table-ownership.md)를 전제로 한다. §6의 관리 작업 기록표 추가는 이전 제안이며 §10의 현재 설계에는 포함하지 않는다.
- 기준: 운영 backend `backend--0000068`, image digest `eee3a695f98161af78d692f6d7bc89e278e96ed02f0c252162c63ecc900c8b6f`, source `02028f2739af3da197a047008f448b3f3e95d230`. Azure 앱·identity·RBAC·job과 DB 권한 metadata를 읽기 전용으로 확인했다. 현재 checkout의 오래된 앱 코드를 운영 기준으로 사용하지 않았다.

## 1. 판단

**같은 PostgreSQL 서버·Azure 내부망을 유지하고, 청주 업무·오산 업무·공통 로그인/관리를 별도 실행 프로그램으로 나누는 안을 권고한다.** 청주 프로그램에는 오산 DB의 암호도, 그 암호를 가져올 권한도, 오산 업무 API 호출 기능도 두지 않는다. 오산도 대칭이다. 같은 소스 저장소와 공용 라이브러리를 사용할 수 있지만 각 실행 시 등록하는 API·worker·접속 대상은 고정한다.

현재도 DB 계정 자체는 분리되어 있다. 부족한 부분은 하나의 backend가 세 계정의 접속 정보를 모두 가진다는 점이다. 현재 일반 업무 데이터 유출이 확인됐다는 뜻은 아니다.

## 2. 현재 운영에서 확인한 사실

| 항목 | 관측 |
| --- | --- |
| backend | 내부 전용 Container App 1개, 1 vCPU/2 GiB, Consumption, 최소/최대 replica 1/1 |
| frontend | 외부 입구 Container App 1개, 0.25 vCPU/0.5 GiB, 최소/최대 1/2, revision `frontend--0000059` |
| ClamAV | 내부 검사 앱, 2 vCPU/4 GiB, 최소/최대 1/1. 업무 DB 접속 identity 없음 |
| Azure 환경 | 위 앱들은 같은 Container Apps environment 사용 |
| backend 비밀값 | 청주·오산·Directory runtime 연결을 모두 참조. Migration/admin 연결 값은 일반 backend secret 목록에 없음 |
| backend Azure identity | 동일 identity에 세 DB runtime 비밀값을 읽는 개별 Key Vault Secrets User 권한이 있음 |
| frontend Azure identity | 조회한 역할은 ACR 이미지 읽기와 입구 인증 비밀값2개 읽기. DB 비밀값 권한 없음 |
| Key Vault | Azure RBAC 사용. 현재 backend에 vault 전체 Secrets User 대신 개별 비밀값 단위 권한이 부여됨 |
| 관리 jobs | migration, database-role-bootstrap, business-unit-member-backfill, maintenance가 모두 Manual. 다중 DB 접속 정보가 각 목적에 맞게 함께 설정돼 있음 |
| 파일 | backend/frontend 영구 volume mount 없음. 오산 진행 사진은 migration0089/0094의 bytea 열로 자기 DB에 저장. 청주 부스바 공개 Blob 권한은 현재 backend identity에 별도로 있음 |

현재 DB 계정의 유효 CONNECT 권한을 `has_database_privilege`로 조회한 결과:

| DB 계정 | 청주 DB | 오산 DB | Directory DB |
| --- | --- | --- | --- |
| `pms_app` | 허용 | 거부 | 거부 |
| `pms_osan_app` | 거부 | 허용 | 거부 |
| `pms_directory_app` | 거부 | 거부 | 허용 |

세 계정 모두 superuser/CREATEROLE/CREATEDB/BYPASSRLS가 false이고 직접 상속 역할이 없으며 각 DB 소유 역할의 구성원도 아니다. 각자의 정상 DB 연결은 성공했다. 반대 DB로 실제 로그인을 시도하는 거부 시험은 수행하지 않았고, 이 표는 현재 권한 catalog 판정이다.

Directory runtime은 단순 조회 전용 계정이 아니다. 표의 직접 쓰기는 제한되어도 identity 등록·갱신, 사용자관리 Begin/Publish 등의 SECURITY DEFINER 함수 실행 권한이 있으며 실제 코드가 이를 사용한다. 이 계정을 양 업무 앱에 그대로 배포하면 공통 사용자 관리의 변경 능력까지 남는다.

## 3. 권고 실행 구성과 자원 재사용

| 실행 역할 | 자원 방안 | 허용하는 직접 DB 접근 |
| --- | --- | --- |
| 공통 입구·정적 화면 제공 | 기존 frontend 재사용 | 없음 |
| 청주 API + 청주 background worker | 기존 backend를 청주 전용으로 전환 | 청주 runtime + 새 청주용 Directory 조회 계정 |
| 오산 API + 오산 background worker | 별도 Container App 1개 추가 | 오산 runtime + 새 오산용 Directory 조회 계정 |
| 공통 로그인·총괄 사용자 관리 | 별도 Container App 1개 추가 | Directory 관리 runtime만 |
| 바이러스 검사 | 기존 ClamAV 재사용 | 없음 |

따라서 권고안은 **기존 backend 1개를 역할3개로 분리하여 Container App 2개를 추가**한다. 앱별 Azure identity도 분리한다. PostgreSQL 서버·업무 DB·Directory DB·Container Apps environment·VNet·ACR·Front Door·ClamAV는 재사용 대상으로 둔다. 청주 Blob 게시 기능·외부 연동 비밀값은 청주에만 둔다. 공용 메일/푸시 설정이 필요한 경우에도 해당 기능을 실행하는 앱의 필요한 항목만 허용한다.

같은 앱에 세 컨테이너를 넣거나 한 프로세스에서 연결 문자열을 선택하는 방식은 권고하지 않는다. 앱별 identity·배포·worker·점검 범위를 명확히 나누기 위해서다. Microsoft도 서비스별 별도 Container App을 기본 권고하며, 한 앱의 여러 컨테이너는 네트워크·저장소·수명주기를 공유한다고 설명한다. [Containers in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/containers)

이 구성은 사업부별 데이터/앱 권한 분리다. PostgreSQL 서버·네트워크·공통 로그인 장애까지 물리적으로 독립시키는 구성은 아니다. DB 서버를 같이 쓰는 데 따른 장애·자원 경합은 남는다.

### 비용 판단

새 DB 서버나 VM/VPN을 구매하는 구성은 필요하지 않다. 다만 현재 서비스는 정액 VM의 남는 공간을 나눠 쓰는 형태가 아니라 Consumption Container Apps다. 별도 앱의 실행량은 CPU·메모리·실행 시간 등에 따라 계산되므로 **앱2개 추가가 무료라고 확정할 수 없다.** 무료 할당량도 앱마다 새로 생기는 것이 아니라 구독 단위다. [Azure Container Apps billing](https://learn.microsoft.com/en-us/azure/container-apps/billing)

현재 backend의1 vCPU/2 GiB를 단순히 세 앱에 똑같이 복제하는 계획으로 잡지 않는다. 시험 환경에서 역할별 메모리·부하를 측정하고 최소 크기를 정한다. 정기 알림 worker가 들어 있는 청주·오산 앱을 비용 때문에 무조건0개로 줄이면 알림 실행이 멈출 수 있으므로 기존 발송 계약을 보존한다. 운영 크기·예상 추가 비용은 그 결과로 산정하며, 이 문서는 금액 확정이나 유료 자원 생성 승인이 아니다.

## 4. 고정 경로와 DB 권한

### 프론트엔드와 API

현재는 frontend의 단일 `/api` 목적지와 `X-Qms-Business-Unit` 선택값을 함께 사용한다. 목표는 같은 공개 주소 아래 다음처럼 경로와 화면 실행을 고정하는 것이다. 이름은 설계용이다.

| 화면/요청 | 고정 목적지 |
| --- | --- |
| `/cheongju/` 화면, `/cheongju/api/…` | 청주 전용 API |
| `/osan/` 화면, `/osan/api/…` | 오산 전용 API |
| `/access/` 공통 로그인·총괄 관리, `/access/api/…` | 공통 로그인/관리 API |

기존 frontend 실행 자원은 공통 입구와 정적 자산 제공 역할로 둔다. 청주·오산 업무 화면은 별도 시작점과 고정 API 설정으로 실행한다. 공통 입구는 경로별 목적지만 전달하고 사업부 업무를 대신 수행하지 않는다. 공통 자산을 재사용할 수 있으며 새 도메인은 필수 조건이 아니다.

- 총괄의 사업부 전환은 해당 화면으로 이동하여 앱 상태를 다시 시작한다. 기존 저장 중 전환 금지·진행 중 읽기 취소·이전 화면 데이터 무효화 계약을 보존한다.
- 각 API는 시작 설정의 사업부와 DB 이름·identity·자격증명이 일치해야 실행한다. 요청 헤더·본문·query로 대상을 바꾸지 않는다. 다른 사업부 selector는 거부하며 상대 업무 endpoint 자체를 등록하지 않는다.
- gateway는 허용된 경로를 고정 backend로만 전달한다. 사용자가 준 host/대상 URL/header를 목적지 선택에 사용하지 않는다. 공통 관리의 내부 endpoint는 공개 gateway에서 전달하지 않는다.
- 기존 `/api`의 사업부 자동 선택은 종료한다. 예전 즐겨찾기·메일/Teams 링크·사진/첨부 다운로드·로그인 복귀 경로·WebPush service worker 경로를 함께 보정한다. 상대 사업부를 추측해서 mutation을 재전송하지 않는다.
- Microsoft 365 로그인 자체와 현재 사용자 권한은 유지한다. 공개 API의 인증 설정 재사용 가능 여부와 내부 API의 별도 application permission은 구현 시 검증한다. CORS나 경로 구분만으로 권한을 보장한다고 하지 않는다.

### 접속 계정·비밀값

| 프로그램 | 업무 DB 계정 | Directory 계정 | 금지 |
| --- | --- | --- | --- |
| 청주 | 기존 `pms_app` | 새 `pms_directory_cheongju_reader`(가칭) | 오산·Directory 관리·migration/admin 비밀값 |
| 오산 | 기존 `pms_osan_app` | 새 `pms_directory_osan_reader`(가칭) | 청주·Directory 관리·migration/admin 비밀값 |
| 공통 관리 | 없음 | 기존 `pms_directory_app`을 필요한 함수로 한정 | 양 업무 DB 비밀값·일반 업무 API 중계 |

새 Directory reader는 identity·소속·총괄 상태·schema identity/원장 및 자기 사업부 관리 작업의 검증용 정보만 읽는다. 상대 사업부 local profile을 담은 JSON 전체를 노출하지 않고 고정 사업부 projection으로 제한한다. 전체 표 SELECT와 모든 함수 EXECUTE를 일괄 부여하지 않는다. 등록·소속 변경·Publish 함수의 실행 권한은 없다. 일반 로그인의 Directory 갱신은 §5 공통 서비스가 담당한다.

앱별 managed identity를 사용하고 자기 비밀값에만 읽기 권한을 부여한다. 기존 backend identity의 오산·Directory 관리 권한도 최종 전환 때 회수해야 한다. 환경변수에서 연결 문자열만 빼고 Key Vault 권한을 남겨두면 충분하지 않다. 옛 활성 revision·secret reference·레거시 연결 fallback·관리 권한·역할 상속도 함께 확인한다. [Managed identities](https://learn.microsoft.com/en-us/azure/container-apps/managed-identity)

기존 Key Vault의 비밀값 단위 ACL을 우선 재사용하는 최소 변경안이다. Microsoft의 일반 기본 권고는 앱/환경별 vault 분리이므로 같은 vault를 쓰는 이 선택을 그 기본 권고와 동일하다고 설명하지 않는다. 상위 범위의 권한까지 검증해 상대 비밀값 읽기를 차단하며, vault 분리는 향후 독립 운영 필요에 따라 판단한다. [Key Vault RBAC](https://learn.microsoft.com/en-us/azure/key-vault/general/rbac-guide)

DB의 PUBLIC CONNECT/함수 EXECUTE 기본 권한, 역할 상속, 소유권, SECURITY DEFINER 실행 권한을 포함해 유효 권한으로 판정한다. 기존 자기 DB 전용 runtime 역할을 재사용하되 관리자·migrator를 일반 앱에 배포하지 않는다. [PostgreSQL privileges](https://www.postgresql.org/docs/current/ddl-priv.html)

## 5. 공통 로그인과 총괄 사용자 관리

### 로그인

현재 `EntraClaimsTransformation`은 인증 때마다 Directory 등록/갱신 함수를 호출한다. 이 쓰기를 공통 로그인 서비스로 옮긴다. 검증된 Entra token의 identity로만 등록/갱신하며, 사용자 입력의 임의 oid·역할로 실행하지 않는다. 업무 API는 Directory의 자기 사업부 소속·총괄 상태와 자기 DB의 local profile을 확인한다. 신규 미승인·소속 없음·local profile 준비 중은 기존처럼 구분한다. 직접 업무 링크로 들어온 신규 사용자도 공통 로그인 처리를 거친 뒤 원래 사업부 경로로 돌아오게 한다.

공통 서비스 장애 시 이미 준비된 사용자의 업무 요청이 매번 공통 HTTP 서비스를 거칠 필요는 없도록 Directory reader를 둔다. Directory DB 자체가 확인되지 않으면 권한을 추측해 허용하지 않는다. health는 자기 업무 DB와 실제 공통 의존성을 구분하고 반대 업무 DB는 검사하지 않는다.

### 총괄 관리

기존 총괄 권한과 통합 사용자 관리 결과를 유지한다. 공통 관리 서비스는 Directory만 직접 열고, 각 사업부 backend의 제한된 내부 API를 호출한다. 허용 작업은 local 사용자 프로필·부서·역할·준비 상태 조회/검증 및 해당 프로필 적용이다. 프로젝트·진행·사진·내보내기 등의 일반 업무 API는 중계하지 않는다. 청주와 오산 backend 사이의 직접 호출도 없다.

내부 호출은 공통관리 managed identity의 token 서명·issuer/tenant·audience·호출 identity·명시적 application permission을 검증한다. 브라우저의 일반 사용자 token이나 업무 backend identity에는 이 권한을 주지 않는다. snapshot 조회와 부서/역할 사전검증은 작업 생성 전에도 필요하므로 공통관리 identity·현재 총괄 actor·고정 사업부의 제한된 관리조회 권한으로 검증한다. 실제 적용 요청에는 Directory에 저장된 작업의 actor·대상·버전·불변 내용과 일치하는지도 추가로 요구한다. 조회를 위해 가짜 operation을 만들지 않는다. 내부망 주소라는 사실만으로 허용하지 않는다. [Microsoft identity claims validation](https://learn.microsoft.com/en-us/entra/identity-platform/claims-validation)

관리 작업 순서는 기존 의미를 보존한다.

1. 공통 서비스가 현재 총괄 권한·expected version·마지막 관리자 보호를 확인하고 Directory에 작업을 시작한다. 기존 소속 회수의 선반영과 작업별 고유 번호/내용 검증을 유지한다.
2. 각 업무 API가 자기 DB에서만 local profile·권한·부서장·WebPush 비활성화·감사와 적용 기록을 처리한다.
3. 필요한 모든 사업부의 적용 완료가 확인된 후 Directory의 새 소속/총괄 권한을 Publish한다.
4. 중간 실패·응답 유실은 기존 Preparing/RetryRequired 흐름과 같은 작업 번호의 재시도로 처리한다. 성공 여부가 모호한 요청을 성공으로 간주하지 않고, 자동으로 옛 권한을 복구하지 않는다.

이것은 여러 DB를 한 번에 commit하는 전체 원자적 트랜잭션이 아니다. 재시도·버전 충돌·identity 일치·마지막 총괄/사업부 관리자 보호·감사 actor/correlation의 기존 계약을 유지한다. 총괄 해제나 actor 상태 변화 시 재시도 조건도 기존 Directory 함수 계약을 기준으로 검증한다.

## 6. 추가가 필요한 작은 표 1개

**새 제안: 각 업무 DB에 `user_access_apply_operations`(가칭) 1개씩 추가한다.** 동일 이름이어도 데이터는 각 DB 안에 독립 저장한다. 앞서 확정한 청주182개·오산56개에 이것을 포함하면 목표는 **청주183개·오산57개**가 된다. 이 증가는 아직 사용자 확정된 표 범위로 기록하지 않는다. Directory는 기존 `directory_user_access_operations`를 사용하므로 추가 표를 제안하지 않는다.

이유는 서버 분리 후 “local DB 저장 성공 → HTTP 응답 유실 → 재요청”이 가능하기 때문이다. Directory의 호출 성공 기록만으로는 local commit 성공 여부를 알 수 없다. 현재 local 적용 코드는 operation ID를 감사 correlation에 사용하지만 적용 완료/버전 검증 원장으로 사용하지 않는다. 기존 감사 JSON을 억지로 실행 상태 저장소로 재사용하지 않는다.

| 저장 정보 | 용도 |
| --- | --- |
| operation_id | 같은 관리 요청을 식별하는 기본키 |
| target_user_id | 변경 대상. 아직 local profile이 없는 비활성 사용자도 기록 가능 |
| directory_expected_version | 더 오래된 요청이 최신 상태를 덮어쓰지 못하도록 비교 |
| request_hash, local_payload_hash | 같은 작업 번호로 다른 내용을 적용하는 요청 거부 |
| actor_user_id | Directory 원장과 실행 주체 대조 |
| applied_at_utc, result_json | 적용 시각과 최소 완료 결과. 불필요한 개인정보/비밀값 없음 |

프로필 변경과 적용 기록 INSERT를 **같은 local transaction**에서 commit한다. 사용자 단위 잠금 아래 동일 operation+내용이면 기존 완료 결과를 반환하고 변경/알림 부수 동작을 반복하지 않는다. 이는 과거 완료 결과의 재응답이며 현재 프로필을 다시 적용하거나 현재 상태를 보증하는 결과가 아니다. 다른 내용 재사용·옛 버전 요청은 거부한다.

신규 적용은 고정 사업부와 payload 사업부, Directory의 현재 access_version과 expected_version, 적용 가능한 진행 상태의 동일 operation, actor·target·hash·해당 사업부 payload 일치를 모두 확인한다. local transaction 안에서 사용자 identity와 마지막 관리자 보호·유효 부서/역할 등의 현재 불변조건도 다시 확인한다. Directory 버전이 이미 더 진행된 요청은 새 local 변경을 일으키지 않는다. 사업부별로 건너뛴 Directory 버전이 있을 수 있으므로 로컬 버전이 반드시1씩 증가한다고 가정하지 않고, 로컬 최댓값만으로 현재 Directory 검증을 대신하지 않는다. 아직 존재하지 않는 local 사용자에 대한 비활성 적용도 완료 기록은 남긴다.

서버를 계속 한 프로그램으로 두면 이 HTTP 경계는 줄일 수 있지만 요청한 실행 분리 조건을 만족하지 않는다. 기록을 생략하면 재시도 시 중복 처리/옛 권한 덮어쓰기 위험을 수용해야 하므로 권고하지 않는다. 과거 이력을 임의로 만들어 채우지 않고 새 운영 전환 기준부터 사용한다. DB 복구로 원장과 profile 시점이 어긋나는 경우는 일반 재시도로 강제 진행하지 않고 별도 복구 검사 대상으로 둔다.

## 7. worker·migration·점검·외부 저장소

- 청주 worker는 청주 target만, 오산 worker는 오산 target만 가진다. 기존 NotificationDeliveryWorker/NotificationEscalationWorker의 양 사업부 순회와 DeploymentMaintenanceLease의 다중 DB 잠금을 제거한다. 공통 서비스에는 업무 worker를 등록하지 않는다.
- 일반 앱의 migration 시작 적용은 끄고, 청주·오산·Directory migration을 대상별 수동 job과 credential로 실행한다. 하나의 job에 모든 비밀값을 둔 채 선택 파라미터만 바꾸는 방식은 최종안으로 삼지 않는다. 기존130개 업무 migration 원장과 Directory 이력은 보존한다.
- schema 검사·health·권한 reconcile도 자기 catalog/target에 맞춘다. 공통 함수 EXECUTE 일괄 부여가 새 Directory reader나 관리 권한을 다시 넓히지 않게 한다.
- 유지보수는 사업부별 drain·잠금·검증으로 나눈다. 공통 배포 도구가 청주와 오산 작업을 각각 명시 실행할 수는 있지만 청주 앱 시작/배포가 오산 DB에 접속하면 안 된다.
- 초기 역할 설정·복구·일회성 이관은 일반 앱과 분리된 운영 작업으로 둔다. 기존 통합 bootstrap/backfill/maintenance job은 무심코 재실행해 예전 권한을 복구하지 않도록 전환 계획에 포함한다. 여기서 job 실행/삭제를 승인하거나 실행하지 않는다.
- 사진·첨부 조회/수정/출력 경로도 고정 사업부 API를 사용한다. 오산 사진은 자기 DB의 기존 내용/이력을 보존한다. 청주 Blob 게시 권한과 이카운트 작업은 오산·공통관리 서비스에 배포하지 않는다.

## 8. 구현 순서와 완료 증거

1. 최신 운영 기준과 현행 하네스를 함께 보존한 구현 branch에서 서비스 역할·고정 설정·catalog·권한 표를 구현한다. 현재 checkout이 운영보다 오래된 사실을 무시하고 바로 수정하지 않는다.
2. 공통 로그인/관리의 이전, 내부 인증, 로컬 적용 기록, 고정 프론트엔드/API 경로를 구현한다. 동작 호환 단계에서는 기존 표를 보존하고, 새 코드가 제거 예정 표/열 없이 동작하도록 한다.
3. disposable PostgreSQL의 합성 데이터로 이전130단계→목표 구조와 새 설치를 확인한다. 새 local 기록표를 포함하는 경우 확정 표 수를 명세에 반영한다.
4. 앱별 자원 크기·운영 예상 비용, 최소 권한·Entra 설정·명확한 job 대상, 백업/복구와 전환 구간을 구체적인 운영 적용 계획에 담는다.
5. 실제 적용 범위가 확정된 뒤에만 새 앱/identity/권한·호환 코드·경로를 전환한다. 옛 다중 DB backend/worker의 접근과 세션이 끝났는지 확인한 후 잔존 값 preflight와 구조 정리를 한다. DROP/열 제거 전후의 rollback 가능 범위를 구분한다. 제거 후에는 옛 이미지만 되돌려도 복구된다고 설명하지 않는다.

| 필수 확인 | 기대 결과 |
| --- | --- |
| 청주 계정→오산, 오산 계정→청주 및 업무 계정→관리 권한 | 실제 접속/함수 실행 거부 |
| 청주 Azure identity→오산/관리 비밀값 읽기 및 역방향 | 유효 RBAC와 실제 거부 확인. 값은 로그에 남기지 않음 |
| 다른 사업부 selector·전용 endpoint·위조 host | 상대 DB/API 호출이 발생하지 않음 |
| 일반 사용자/업무 identity→내부 사용자관리 API | 인증/권한 거부 |
| 신규 로그인·미승인·프로필 준비 중·총괄 전환 | 기존 상태 구분과 권한 보존 |
| local commit 후 응답 유실·동일 요청·지연된 옛 요청 | 중복 부수 동작·최신 프로필 덮어쓰기·새 권한 조기 Publish 없음 |
| 한쪽 DB/API 장애 | 다른 사업부 health/worker/잠금이 반대 DB를 순회하지 않음. 공통 Directory 장애는 별도 표시 |
| 프로젝트·진행·사진·알림·공지·감사·내보내기 | 정리된 자기 schema로 정상 작동, provider는 시험용 사용 |

현재 수행한 것은 Azure/DB 읽기 전용 현황 확인과 설계 검토다. 위 거부·분산 실패·기능·용량 시험은 아직 실행하지 않았다. 운영 데이터/구조·권한·앱 배포·자원·사용자 터널 변경은 없다.

## 9. 주요 코드 근거와 독립 검토

배포 source `backend/src/Emi.Qms.Api/` 기준:

- `DatabaseConnectionStringProvider.cs:31,47`: 요청 및 명시 target 선택. `BusinessUnits/BusinessUnitResolver.cs:31,114`: Directory membership과 요청 selector.
- `Authorization/EntraClaimsTransformation.cs:49`, `BusinessUnits/BusinessUnitDirectoryStore.cs:43`: 인증 시 Directory 등록/갱신 쓰기.
- `BusinessUnits/BusinessUnitAccessAdministrationStore.cs:89,195,237,410`: 양 DB snapshot·적용·Directory Publish·local transaction.
- `DatabaseRuntimePrivilegeManager.cs:39,136,179`: CONNECT, Directory 표 제한 및 함수 실행 권한. 실제 catalog 조회로 runtime3개의 CONNECT 및 Directory 함수 EXECUTE를 대조했다.
- `Notifications/NotificationDeliveryWorker.cs:27`, `NotificationEscalationWorker.cs:27`, `DeploymentMaintenance/DeploymentMaintenanceLease.cs:24`: 다중 target worker/잠금.
- `DatabaseHealthChecker.cs:20`, `DatabaseMigrationRunner.cs:75`: 다중 target 검사/적용.
- repo 기준 `database/directory-migrations/0004_overall_administrator_access.sql:143,170,288,373`: operation 내용/버전·동시 작업·Publish·완료 버전. `frontend/src/api.ts:211,3755`: 단일 API 주소와 사업부 header. `infrastructure/azure-pilot/nginx.conf.template:42`: 현행 단일 backend proxy.

작성과 분리된 기존 `review_table_ownership` 맥락을 재사용해 이번 source·요구사항을 기준으로 검토했다(요청 GPT-6-astra/high, 관측 NOT_REPORTED). 세 역할 분리를 타당하게 평가하면서 로그인 쓰기 이전, 제한된 local 관리 조회, 내부 identity/operation 검증, 권한 Publish 순서, local 중복/지연 적용 방지, 프론트엔드 전환 보존을 필수 조건으로 지적했고 반영했다. 최종 문서의 새 세부 조건을 좁게 확인한 결과에 따라 관리조회/사전검증과 실제 적용의 인증 조건을 구분하고 현재 Directory 버전·작업 상태·local 불변조건 및 과거 완료 결과 재응답 의미를 명시했다. 검토자의 Azure/DB 실행 검증을 뜻하지 않는다.

## 10. 기존 앱 수·용량을 기준으로 한 재검토 — 2026-09-29

### 확정 조건

사용자 요청은 “컨테이너 앱은 그대로 사용하는 방향으로 다시 생각”이다. backend/frontend/ClamAV의 현재 앱 수를 유지하고, backend1 vCPU/2 GiB·replica1을 재설계의 용량 기준으로 삼는다. 사전 동의 없이 용량·replica를 늘리거나 frontend/ClamAV에 업무 backend를 옮겨 비용을 숨기지 않는다. DB별 표/열 구조는 계속 독립시킬 수 있다.

분리 수준에 대한 질문에 사용자는 “업무 처리와 DB 구조를 분리하면 됨 — 기존 백엔드 1개 유지”를 선택했다. 아래 A를 현재 설계로 확정한다. B와 신규 앱·내부 HTTP 관리 호출·그 도입을 이유로 한 local 적용원장 추가는 이번 범위에서 제외한다.

앞선 월 비용의 큰 증가 예시는 현재 크기를 세 앱에 그대로 복제한 계산이었다. 실제 추가 비용이 확정됐거나 단지 앱 개수 때문에 세 배로 청구된다는 뜻은 아니다. 새 안도 같은 용량/개수만으로 실제 청구액이 완전히 같다고 보장하지 않는다. 작업/대기 시간 비율·로그·DB 부하 변화는 검증해야 한다.

### A. 기존 backend 프로그램 하나 안에서 업무 모듈 분리 — 선택한 방향

| 경로/모듈 | 허용 역할 |
| --- | --- |
| 청주 화면 → 청주 전용 업무 모듈 | 청주 고정 데이터 접근 객체로 emi_qms만 처리 |
| 오산 화면 → 오산 전용 업무 모듈 | 오산 고정 데이터 접근 객체로 emi_qms_osan만 처리 |
| 공통 로그인/총괄 관리 모듈 | Directory와 각 사업부의 제한된 사용자관리 인터페이스만 사용 |

앱·실행 컨테이너·.NET 프로세스를 하나로 유지한다. 단순히 폴더 이름만 나누지 않고 endpoint 등록, 서비스 의존 관계, 쿼리, background worker, migration catalog를 업무별로 고정한다. 일반 업무 코드에는 임의 사업부를 선택하는 provider나 양쪽 연결 설정을 전달하지 않는다. 공통 사용자관리 모듈만 local 프로필·부서·역할·준비 상태의 제한된 인터페이스를 호출하며 일반 업무 데이터 중계는 금지한다.

필수 보정은 다음과 같다.

- 청주/오산/공통 관리 API 경로를 고정한다. 헤더를 바꿔 같은 업무 endpoint의 DB를 바꾸는 방식은 제거한다. 사용자 소속·역할 확인은 그대로 필요하다.
- 청주/오산 store는 서로 다른 고정 DB 접근 객체만 받는다. 신규 코드의 반대 업무 모듈 참조·공통 설정 직접 읽기·임의 target 선택을 구조 검사와 실제 실행 검증으로 확인한다. 등록/조회/수정/삭제/사진/출력까지 같은 기준을 적용한다.
- 알림과 점검 잠금은 사업부별 실행으로 나누고 오류 처리도 분리한다. 한쪽 DB 실패 때문에 다른 쪽 worker를 건너뛰지 않게 한다. 공통 상태 점검은 두 상태를 구분해 표시하되, 한쪽 실패 때문에 공유 앱 전체가 준비 실패 처리되어 다른 업무까지 차단되는 문제를 보정한다. 단일 프로세스의 장애·배포·재시작·자원 경합은 공유된다는 한계가 남는다.
- 현재 자기 DB만 CONNECT할 수 있는 runtime 계정은 유지한다. migration은 운영 도구에서 대상 DB를 명시하고 해당 schema만 적용한다. 같은 구조/원장을 모두 기대하는 기존 catalog·health·권한 reconcile을 분리한다.
- 로그인/총괄 관리는 같은 프로세스의 별도 책임으로 남긴다. 새 서버 간 HTTP 호출을 만들지 않는다. 기존 Directory 작업 번호·내용 hash·버전·회수 선반영·local 적용·Publish 및 재시도 계약을 유지한다.

**보장 한계:** A는 일반 업무의 실행 경로와 데이터 사용을 코드·DB 계정·검증으로 분리하는 안이다. 같은 프로세스의 설정·메모리·Azure identity에는 양쪽 접속 능력이 남는다. 고정 데이터 접근 객체는 오배선을 막는 구조이며, 임의 코드가 실행돼도 상대 credential을 가져올 수 없다는 별도 보안 경계가 아니다. 따라서 이전의 “청주 전용 실행 프로그램이 상대 credential조차 갖지 않는다”는 조건을 충족했다고 표현하지 않는다. 사용자는 이 차이를 설명받고 업무 처리/DB 구조 분리를 선택했다.

### B. 같은 Container App 안에 여러 실행 컨테이너 — 이번 범위 미채택

Azure는 하나의 앱에 여러 컨테이너를 넣는 구성을 지원한다. 앱 수를 유지하면서 프로세스별 환경변수에 자기 DB 접속 정보만 전달할 수 있다. 그러나 앱 단위 비밀값·managed identity·공유 네트워크·배포/확장 경계를 함께 검토해야 하며 독립 앱과 동일한 격리라고 단정하지 않는다. [공식 다중 컨테이너 설명](https://learn.microsoft.com/en-us/azure/container-apps/containers#multiple-containers)

공식 문서의 identity lifecycle None은 컨테이너 코드에 identity token을 제공하지 않고 플랫폼의 Key Vault secret/이미지 pull 등에 사용하게 하는 옵션이다. Init/Main/All/None 단계 구분이며 이름별 main 컨테이너 격리 설정으로 해석하면 안 된다. 현재 청주 부스바 Blob 게시 코드는 main에서 managed identity token을 사용하므로 일괄 None으로 바꾸면 기존 기능에 영향을 준다. 개별 secret 주입·identity 분리·Blob 인증·내부 호출 인증과 재시도까지 해결한 뒤에야 후보의 실현 가능성을 확정할 수 있다. [공식 identity 가용성 제어](https://learn.microsoft.com/en-us/azure/container-apps/managed-identity#control-managed-identity-availability)

컨테이너가 늘면 각 런타임의 기본 메모리도 필요하다. 앱 수를 유지하더라도 총 CPU/메모리가 늘면 비용은 증가한다. B가 현재1 vCPU/2 GiB에서 충분한지 아직 시험하지 않았다. 비용·구현 복잡도 및 사용자가 선택한 분리 수준에 따라 이번에는 A를 사용하며 B를 구현하지 않는다.

### 표 수와 검증 상태

현재 표 범위는 **청주182개·오산56개**로 유지한다. §6의 local 적용 원장 추가(183/57)는 이번 설계에 포함하지 않는다. local commit 뒤 Directory Publish 실패는 현재 코드에도 있었으므로 이를 새 HTTP 구조에서 처음 생긴 문제라고 설명한 앞선 근거는 범위를 좁혀 이해해야 한다. A에서도 동일 작업의 동시 재시도·늦은 과거 요청은 기존 Directory 작업의 직렬화·현재 상태 재검증으로 처리 가능한지 확인한다. 향후 정확한 local 완료 재응답을 위한 원장 추가가 필요해진다면 기존 복구 계약 강화라는 별도 근거로 제안하며 이번 승인에 포함시키지 않는다.

독립 검토는 기존 review_table_ownership가 이번 비용 조건과 배포 source를 기준으로 수행했다(요청 GPT-6-astra/high, 관측 NOT_REPORTED). A의 고정 모듈·계정 경계는 타당한 후보이나 동일 프로세스의 credential 격리는 불가능하며, 183/57을 필수 조건으로 삼을 수 없다는 지적을 반영했다. B의 Azure 특성은 parent가 공식 문서로 추가 확인했으며 실제 플랫폼 격리/부하 시험은 미수행이다. 이번에는 설계 기록만 수정했고 코드·운영 DB·Azure 설정·사용자 터널은 변경하지 않았다.
