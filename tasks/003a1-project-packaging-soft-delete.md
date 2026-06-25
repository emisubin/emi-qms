# TASK-003A-1: Project Packaging, Cancelled View and Soft Delete Alignment

## 목적

TASK-003A 프로젝트 등록 기능에 포장방식 필수 입력, 상태별 조회, 취소 목록, 논리삭제와 삭제 보관함을 추가한다. 취소는 실제 업무 중단 상태이고 삭제는 오등록·중복등록 정리를 위한 보관 처리로 분리한다.

## 구현범위

- 프로젝트 포장방식 필드와 등록·수정 validation
- 기존 프로젝트의 포장방식 미지정 호환
- 진행, 보류, 완료, 취소 상태별 목록 조회
- Sales/System Administrator 전용 삭제 보관함 조회
- Sales 전용 프로젝트 논리삭제
- 삭제 프로젝트 상세와 패널 Placeholder 조회
- 삭제 후 동일 Project Title 재사용
- 삭제·포장방식 감사이력
- Backend, Frontend, Full-Stack E2E 테스트

## 제외범위

- 패널명, W/H/D, 단위 변환, 설계 Excel 업로드
- QR 생성과 QR 스캔
- 구매, 생산계획, 제조, 품질, 물류, 알림
- 프로젝트 완료 전환
- 삭제 프로젝트 복구
- Microsoft Entra ID, PWA Service Worker, Azure 배포

## 프로젝트 상태와 삭제의 차이

- `Active`, `OnHold`, `Cancelled`, `Completed` 상태값은 유지한다.
- `Deleted`를 프로젝트 상태로 추가하지 않는다.
- 삭제 여부는 `deleted_at_utc`가 null인지로 판단한다.
- 취소 프로젝트는 `status = 'Cancelled'`이고 `deleted_at_utc is null`인 업무 중단 프로젝트이며 재활성 가능하고 title 재사용은 불가하다.
- 삭제 프로젝트는 기존 status를 유지한 채 `deleted_at_utc`가 설정된 오등록·중복등록 정리 대상이며 일반 목록에서 제외되고 삭제 보관함에서만 조회한다.

## 포장방식

- `WoodenCrate`: 목포장
- `StretchWrap`: 청랩포장
- `HeavyDutyBox`: 고강도박스포장

신규 프로젝트 등록 시 `PackagingMethod`는 필수이다. 기존 프로젝트는 migration에서 임의 백필하지 않고 null을 허용하며 화면에는 `미지정`으로 표시한다. 기존 프로젝트를 일반정보 수정으로 저장할 때는 포장방식을 선택해야 한다. 상태변경은 포장방식 미지정이어도 가능하다.

TASK-003B에서 포장방식에 따라 패널정보 완료조건을 계산할 예정이며, 이번 TASK에서는 `PanelInfoCompleted` 계산을 구현하지 않는다.

## 삭제조건

- Sales만 삭제 가능하다.
- `Active`, `OnHold`, `Cancelled` 프로젝트만 삭제 가능하다.
- `Completed` 프로젝트와 이미 삭제된 프로젝트는 삭제할 수 없다.
- 삭제 사유와 현재 PJT Title 확인 입력이 필수이다.
- 확인 title은 정규화 기준으로 현재 title과 일치해야 한다.
- 후속 업무이력이 존재하면 삭제를 차단해야 하며, 현재 TASK에서는 기본 프로젝트, Panel Placeholder, Project Audit Event를 차단 데이터로 보지 않는다.
- 삭제 Guard는 삭제 transaction의 `NpgsqlConnection`과 `NpgsqlTransaction`을 공유하며, Guard 구현은 별도 DB connection을 열지 않는다.
- 삭제 처리 순서는 project row `FOR UPDATE` 잠금, 최신 삭제·상태 검증, 모든 Guard 실행, 삭제 metadata update, 감사이력 저장, commit 순서이다.
- Guard가 차단하거나 예외가 발생하면 삭제 metadata와 삭제 감사이력은 commit되지 않는다.

## Project-scoped write protocol

후속 모듈의 모든 project-scoped write는 삭제 Guard만으로 race가 자동 해결된다고 가정하지 않는다. Panel Information, Procurement, Production Plan, Manufacturing, Quality Inspection, Nonconformity, Logistics 쓰기는 다음 공통 protocol을 따른다.

- Project row를 동일한 잠금 순서로 먼저 확인한다.
- `deleted_at_utc is null`과 프로젝트 업무 상태를 같은 transaction 안에서 검증한다.
- 하위 업무 데이터 생성·수정도 같은 transaction 안에서 처리한다.
- 삭제 transaction과 충돌하면 DB 잠금 순서에 따라 삭제 또는 업무 쓰기 중 일관된 한 결과만 commit한다.
- 삭제와 면수변경이 동시에 실행되면 삭제 선행 시 면수변경은 404/409가 되고, 면수변경 선행 시 패널과 감사이력이 완전하게 저장된 뒤 삭제될 수 있다.

## 권한

- `Project.Delete`: Sales만 보유
- `Project.Deleted.Read`: Sales, System Administrator만 보유
- System Administrator는 삭제 보관함과 판매금액을 조회할 수 있지만 프로젝트 삭제는 불가하다.
- 기타 내부 역할과 Read Only는 일반 프로젝트 조회만 가능하며 삭제 보관함과 삭제 API는 403이다.

## API

- `GET /api/projects?status=Active|OnHold|Completed|Cancelled`
- `POST /api/projects/{projectId}/delete`
- `GET /api/deleted-projects`
- `GET /api/deleted-projects/{projectId}`

일반 프로젝트 API는 항상 `deleted_at_utc is null` 조건을 적용한다. 삭제 프로젝트 복구 API는 없다.

## 화면

- 프로젝트 목록 탭: 진행, 보류, 완료, 취소
- Sales/System Administrator 전용 삭제 보관함 탭
- 등록·수정 폼의 포장방식 필수 선택
- 상세 화면의 포장방식 표시
- Sales 전용 삭제 버튼과 삭제 Dialog
- 삭제 보관함 목록과 삭제 프로젝트 상세 읽기 전용 화면
- 목록 탭, 검색, 필터, 페이지 변경으로 새 조회가 시작되면 이전 조회는 abort하고, 늦게 도착한 stale 응답·오류는 화면 상태를 변경하지 않는다.

## 감사이력

포장방식 변경은 `ProjectFieldUpdated`, `FieldName = PackagingMethod`로 기록한다. 프로젝트 삭제는 `ProjectDeleted`로 기록하고 삭제 당시 Status, ProjectTitle, PackagingMethod를 별도 감사 이벤트로 남긴다. 판매금액은 삭제 감사이력에 중복 저장하지 않는다. 삭제 감사이력은 Sales와 System Administrator만 조회한다.

삭제시각은 DB `UPDATE ... RETURNING deleted_at_utc`로 반환된 값이 공식 기준이다. `projects.deleted_at_utc`, 삭제 API 응답, `ProjectDeleted` 감사이력의 `DeletedAtUtc` 값은 같은 DB 저장값에서 생성한다.

## Migration

`0004_project_packaging_soft_delete.sql`을 추가한다. 기존 `0001`, `0002`, `0003` migration은 수정하지 않는다. `projects`에 `packaging_method`, `deleted_at_utc`, `deleted_by_user_id`, `delete_reason`, `deleted_correlation_id`를 추가하고, title unique index를 `deleted_at_utc is null` partial unique index로 교체한다.

## 테스트 완료조건

- 신규 프로젝트 포장방식 validation과 세 enum 값 등록 성공
- 기존 null 포장방식 조회와 수정 시 필수검증
- 포장방식 변경 감사이력
- 상태별 목록과 취소 목록
- Sales 삭제 성공, Admin/타 역할 삭제 403
- 삭제 사유·title 확인 validation
- Completed/중복 삭제 차단
- 삭제 후 일반 목록 제외와 삭제 보관함 조회
- 삭제 후 동일 Project Title 신규 등록 성공
- 취소/완료 Project Title 중복은 계속 409
- 삭제 프로젝트 쓰기 API 차단
- 패널 Placeholder 실제 삭제 없음
- 삭제·수정, 삭제·취소 동시 요청 일관성
- 삭제·면수증가, 삭제·삭제 동시 요청 일관성
- Guard 허용·차단·예외와 동일 transaction context 검증
- 목록 탭 stale response 무시 검증
- DB 삭제시각과 감사이력 삭제시각 정합성
- 0001→0002→0003→0004 migration과 기존 0003 DB의 0004 적용
- Frontend unit, mock UI smoke, Full-Stack E2E 통과
