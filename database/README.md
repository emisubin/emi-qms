# Database

PostgreSQL 스키마, 마이그레이션, 개발용 가짜 시드 데이터를 관리합니다. 실제 고객 데이터는 포함하지 않습니다.

## Migrations

- `migrations/0001_identity_authorization_foundation.sql`: TASK-002 사용자, 부서, 역할, 권한, 프로젝트, 사용자별 프로젝트 접근권한 기반 테이블과 운영 필수 역할·권한 기준정보를 생성합니다.

Development 환경에서 백엔드가 시작될 때 `Database:ApplyMigrationsOnStartup` 설정이 켜져 있으면 마이그레이션을 적용합니다. 개발용 가짜 사용자와 프로젝트는 schema migration에 포함하지 않고, Development/Testing 환경에서 `DevelopmentData:SeedEnabled` 또는 `DEV_DATA_SEED_ENABLED`가 명시적으로 `true`일 때만 seeder가 생성합니다. 자동 테스트도 같은 마이그레이션과 seeder를 실제 PostgreSQL에 적용해 검증합니다.
