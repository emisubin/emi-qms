# Database

PostgreSQL 스키마, 마이그레이션, 개발용 가짜 시드 데이터를 관리합니다. 실제 고객 데이터는 포함하지 않습니다.

## Migrations

- `migrations/0001_identity_authorization_foundation.sql`: TASK-002 사용자, 부서, 역할, 권한, 프로젝트, 사용자별 프로젝트 접근권한 기반 테이블과 운영 필수 역할·권한 기준정보를 생성합니다.
- `migrations/0002_permission_scope_alignment.sql`: TASK-002A 전체 프로젝트 조회권한과 민감정보 조회권한 기준을 정렬합니다.
- `migrations/0003_project_panel_foundation.sql`: TASK-003A 프로젝트 등록, 패널 Placeholder, 프로젝트 감사이력, 영업 쓰기 권한을 추가합니다. 적용 전에 legacy `projects.name`을 정규화했을 때 중복 Project Title이 있으면 명확한 오류로 중단하며 이름을 자동 변경하지 않습니다.
- `migrations/0004_project_packaging_soft_delete.sql`: TASK-003A-1 포장방식, 프로젝트 논리삭제, 삭제 보관함 권한을 추가하고 Project Title unique index를 삭제되지 않은 프로젝트 대상 partial unique index로 교체합니다.

Development 환경에서 백엔드가 시작될 때 `Database:ApplyMigrationsOnStartup` 설정이 켜져 있으면 마이그레이션을 적용합니다. 개발용 가짜 사용자와 프로젝트는 schema migration에 포함하지 않고, Development/Testing 환경에서 `DevelopmentData:SeedEnabled` 또는 `DEV_DATA_SEED_ENABLED`가 명시적으로 `true`일 때만 seeder가 생성합니다. 자동 테스트도 같은 마이그레이션과 seeder를 실제 PostgreSQL에 적용해 검증합니다.

## 0003 적용 전 legacy Project Title 중복 확인

0003은 `trim`, 연속 공백 1개 축소, 대소문자 무시 기준으로 Project Title 유일성을 강제합니다. 기존 `projects.name` 데이터가 있는 DB는 0003 적용 전에 다음 SQL로 중복 여부를 확인합니다.

```sql
select normalized_title, count(*) as duplicate_count
from (
    select upper(regexp_replace(btrim(name), '\s+', ' ', 'g')) as normalized_title
    from projects
) normalized_projects
group by normalized_title
having count(*) > 1;
```

중복이 발견되면 업무 담당자가 올바른 프로젝트명을 결정해 legacy 데이터를 정리한 뒤 0003을 다시 적용합니다. Migration은 프로젝트명을 자동 변경하거나 삭제하지 않습니다.

## 0004 Project Title unique index 변경

0004 이후 Project Title 중복검사는 삭제되지 않은 프로젝트만 대상으로 합니다.

- `deleted_at_utc is null`: Active, OnHold, Cancelled, Completed 모두 중복검사 대상입니다.
- `deleted_at_utc is not null`: 삭제 보관함 대상이며 동일 Project Title을 신규 프로젝트에서 다시 사용할 수 있습니다.
- 취소는 업무 중단 상태이고 삭제가 아니므로 취소 프로젝트의 Project Title은 계속 재사용할 수 없습니다.

0004는 기존 프로젝트의 `packaging_method`를 임의로 백필하지 않습니다. 기존 행은 null이 가능하며, 신규 API 등록과 일반정보 수정 저장 시에는 서버가 포장방식 선택을 요구합니다.
