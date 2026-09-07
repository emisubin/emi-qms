# Database

PostgreSQL 스키마, 마이그레이션, 개발용 가짜 시드 데이터를 관리합니다. 실제 고객 데이터는 포함하지 않습니다.

## Migrations

- `migrations/0001_identity_authorization_foundation.sql`: TASK-002 사용자, 부서, 역할, 권한, 프로젝트, 사용자별 프로젝트 접근권한 기반 테이블과 운영 필수 역할·권한 기준정보를 생성합니다.
- `migrations/0002_permission_scope_alignment.sql`: TASK-002A 전체 프로젝트 조회권한과 민감정보 조회권한 기준을 정렬합니다.
- `migrations/0003_project_panel_foundation.sql`: TASK-003A 프로젝트 등록, 패널 Placeholder, 프로젝트 감사이력, 영업 쓰기 권한을 추가합니다. 적용 전에 legacy `projects.name`을 정규화했을 때 중복 Project Title이 있으면 명확한 오류로 중단하며 이름을 자동 변경하지 않습니다.
- `migrations/0004_project_packaging_soft_delete.sql`: TASK-003A-1 포장방식, 프로젝트 논리삭제, 삭제 보관함 권한을 추가하고 Project Title unique index를 삭제되지 않은 프로젝트 대상 partial unique index로 교체합니다.
- `migrations/0042_user_profile_photos.sql`: TASK-HOME-002 사용자당 현재 프로필 사진 1개와 hash·크기·MIME 기반 append-only 감사 원장을 추가합니다. 사진 원문은 5MB로 제한하며 사용자 purge 때만 transaction-local scope로 감사 cascade를 허용합니다.

Development 환경에서 백엔드가 시작될 때 `Database:ApplyMigrationsOnStartup` 설정이 켜져 있으면 마이그레이션을 적용합니다. 개발용 가짜 사용자와 프로젝트는 schema migration에 포함하지 않고, Development/Testing 환경에서 `DevelopmentData:SeedEnabled` 또는 `DEV_DATA_SEED_ENABLED`가 명시적으로 `true`일 때만 seeder가 생성합니다. 자동 테스트도 같은 마이그레이션과 seeder를 실제 PostgreSQL에 적용해 검증합니다.

## Cheongju/Osan database isolation

`BusinessUnits:Enabled=true`에서는 같은 PostgreSQL 서버에 directory, Cheongju business, Osan business database를 각각 둡니다. Business database는 기존 `migrations/` 원장을 독립적으로 적용하고 `0086_business_unit_database_identity`로 명시된 사업장에 결속합니다. Directory database는 별도 `directory-migrations/` 원장만 사용합니다. Database 이름, 역할, identity marker, exact migration ledger가 일치하지 않으면 요청과 worker가 다른 database로 대체하지 않고 중단합니다.

설정 구조는 `backend/src/Emi.Qms.Api/appsettings.BusinessUnits.example.json`을 따릅니다. 정상 API runtime에는 세 runtime connection만 배포합니다. Migration job에는 세 migration connection을, role bootstrap job에는 runtime, migration, administrator connection을 해당 작업 동안에만 제공합니다. Runtime, migrator 역할은 database별로 모두 달라야 하며 runtime 역할은 다른 사업장 database에 연결할 수 없습니다. Directory runtime 역할에는 membership와 overall administrator를 읽을 권한만 있고 변경 권한은 없습니다.

신규 환경의 적용 순서는 다음과 같습니다.

1. `--bootstrap-database-roles`로 세 database의 bounded 역할과 교차 database 차단을 설정합니다.
2. `--migrate-only`로 directory 원장과 두 business 원장을 각각 적용하고 database identity를 결속합니다.
3. 기존 Cheongju 사용자를 directory에 옮길 때만 `BusinessUnits:MembershipBackfill:ApprovedUserIds`에 검토한 ID를 명시하고 `--backfill-business-unit-memberships`를 실행합니다. Overall administrator 지정은 그 목록의 부분집합인 `OverallAdministratorUserIds`에 별도로 명시합니다.
4. `/health/ready`와 review-safe 상태가 세 database identity와 ledger를 모두 통과한 뒤 정상 API runtime을 시작합니다.

기존 단일 database 모드는 `BusinessUnits:Enabled=false`로 명시하며 기존 `QmsDatabase` 또는 `DATABASE_HOST` 계열 설정을 그대로 사용합니다. 다중 database 모드에는 기본 사업장이나 실패 시 fallback이 없습니다. 개발 시드는 `BusinessUnits:DevelopmentSeedUnits`에 명시된 business database에만 적용됩니다.

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
