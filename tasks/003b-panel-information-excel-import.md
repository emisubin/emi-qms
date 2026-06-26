# TASK-003B: Panel Information, Design Excel Import and QR Eligibility

## 목적

설계·영업·생산관리 사용자가 프로젝트의 활성 패널에 패널명과 W/H/D 치수를 입력하고, 설계 Excel 파일로 일괄 반영할 수 있게 한다. 이번 작업은 실제 QR 생성이 아니라 현재 데이터 기준의 QR 생성 가능 여부와 패널정보 완료 여부를 계산해 보여주는 범위다.

## 구현범위

- Design 역할과 `PanelInfo.Update` 권한 추가
- 패널번호 `No.{SequenceNumber}` 표시
- 패널명, W/H/D 직접 입력 및 여러 패널 일괄 저장
- mm/inch 입력과 표시 전환
- 포장방식별 패널정보 완료 판정
- QR 생성 가능 여부 파생 판정
- 동일 패널명 구분 표시
- 프로젝트별 설계 Excel `.xlsx` 양식 다운로드
- 설계 Excel `.xlsx` 미리보기와 전체 적용
- Excel 입력 배치 이력
- 직접 입력·Excel 입력 감사이력
- 패널정보 목록, 상세, 모바일 카드형 입력 화면
- Backend, Frontend, Full-Stack E2E 테스트

## 제외범위

- 실제 QR 이미지 생성, 인쇄, 스캔
- 원본 Excel binary 영구보관과 다운로드
- 도면·BOM Revision 관리
- 구매, 생산계획, 제조, 품질, 물류, 알림
- 프로젝트 전체 진행률
- Microsoft Entra ID, PWA Service Worker

## 패널 식별 규칙

기존 `panel_placeholders.sequence_number`를 사용자가 보는 패널번호로 사용한다. 예를 들어 `sequence_number = 1`은 `No.1`이다. 기존 `display_code`(`P01`, `P02`)는 내부 호환과 보조표시로 유지할 수 있으나 사용자가 수정할 수 없다. Excel의 `No`는 `sequence_number`와 매칭하며 행 순서로 매칭하지 않는다.

## 동일 패널명 표시 규칙

동일 프로젝트 안에서 패널명 중복을 허용하고, DB unique constraint를 만들지 않는다. 화면에서는 항상 `No.1 · PNL-1`처럼 No와 패널명을 함께 표시한다. 중복명 판정은 앞뒤 공백 제거, 연속 공백 1개 축소, 대소문자 무시 기준이며 입력 차단이 아니라 구분 보조표시다. 동일 normalized 이름을 가진 활성 패널이 2개 이상이면 `동일 명칭 2면` 배지를 제공한다.

## 포장방식별 필수값

- `WoodenCrate`: `PanelName`, `WidthMm`, `HeightMm`, `DepthMm` 모두 필요
- `StretchWrap`: `PanelName` 필요, W/H/D는 선택이지만 입력 시 세 값 모두 필요
- `HeavyDutyBox`: `PanelName` 필요, W/H/D는 선택이지만 입력 시 세 값 모두 필요
- `PackagingMethod = null`: 저장은 가능하지만 완료는 false, Excel Apply는 차단하며 포장방식 지정을 안내

## QR 생성 가능 조건

`QrEligible`은 저장 boolean이 아니라 현재 데이터에서 파생한다. true 조건은 프로젝트가 삭제되지 않았고 `Status = Active`, 패널이 `Active`, `PanelName`이 존재하는 경우다. W/H/D와 PackagingMethod는 QR 조건에 포함하지 않는다.

## 직접 입력 규칙

직접 입력은 단계적 저장을 허용한다. WoodenCrate라도 PanelName만 저장할 수 있고 이때 QR 가능은 true, 패널정보 완료는 false다. W/H/D는 모두 null이거나 모두 값이 있어야 하며 일부만 값이 있으면 400이다. 각 치수는 0보다 크고 100000mm 이하로 저장되어야 한다.

직접 입력 PATCH는 update mask 방식이다. 각 패널은 `panelNameUpdate.isChanged`와 `sizeUpdate.isChanged`로 변경 의도를 명시한다. 패널명만 변경하면 size update를 보내지 않으며 기존 canonical `WidthMm`, `HeightMm`, `DepthMm`를 유지한다. 사이즈 변경은 `sizeUpdate.inputUnit`, `width`, `height`, `depth`를 함께 보내고, 사이즈 삭제는 `sizeUpdate.clear = true`로 보낸다. 변경되지 않은 row는 요청에서 제외하는 것을 기본으로 하며, 포함되더라도 서버는 현재 DB 값과 비교해 실제 변경된 필드만 version 증가와 audit 대상으로 삼는다.

## Excel 입력 규칙

`.xlsx`만 허용하고 `.xls`, `.xlsm`, csv, 실행형·매크로 파일은 거부한다. 파일은 10MB, multipart 요청은 11MB로 제한한다. 파일 크기는 stream 복사 전에 검사하고 제한 초과 요청은 크기 제한 오류로 반환한다. ZIP entry는 2,000개, 전체 uncompressed size는 50MB, 단일 entry는 20MB, worksheet는 20개까지 허용한다. Workbook 로드 후 데이터 500행, 사용 열 64개, 사용 셀 35,000개를 초과하면 거부한다. `No`와 `panel name` 헤더는 필수이며 `w`, `h`, `d`는 포장방식에 따라 필요하다. 도번과 추가 열은 무시하고 저장하지 않는다. Formula, macro part, externalLinks, OLE object, 비정상 ZIP은 거부하며 Formula는 실행하지 않는다. 한 서버 인스턴스에서 Excel parse는 최대 2개까지만 동시에 수행하며, parse gate 대기 중 요청이 취소되면 workbook parse를 시작하지 않고 semaphore slot을 누수하지 않는다.

Worksheet 선택은 표시 상태의 worksheet만 대상으로 하며 hidden/very hidden sheet는 자동 선택하지 않는다. 이름이 `Panel Information`인 visible sheet가 정확히 하나이면 우선 사용한다. 없으면 첫 20행 안에 인식 가능한 header가 있는 visible sheet를 찾고, 후보가 정확히 하나일 때만 사용한다. 후보가 없거나 2개 이상이면 오류다. 선택된 sheet의 header row는 첫 20행 안에 있어야 하며, 정규화 후 `No`, `panel name`, `w`, `h`, `d` 같은 인식 header가 중복되거나 병합 셀이면 오류다.

Excel 양식 다운로드는 서버에서 `.xlsx` workbook으로 생성한다. Worksheet 이름은 `Panel Information`이며 헤더 순서는 `No`, `도번`, `panel name`, `w`, `h`, `d`다. 도번 열은 양식에는 포함하지만 저장하지 않는다. 활성 패널만 `sequence_number` 순서로 포함하며 Cancelled 패널은 제외한다. 양식 다운로드는 DB 값과 감사이력을 변경하지 않는다.

## mm/inch 변환

DB는 mm 기준 `numeric(12,3)`로 저장한다. 1 inch = 25.4 mm로 변환하며, 변환 후 `numeric(12,3)` 기준으로 `MidpointRounding.AwayFromZero` 반올림한다. mm 표시는 trailing zero를 제거하고 최대 3자리, inch 표시는 기본 소수점 2자리로 한다. 화면은 `DisplayUnit`과 `EditInputUnit`을 분리한다. DisplayUnit 변경은 읽기용 표현만 바꾸며 dirty 상태, 저장 요청, 감사이력을 만들지 않는다. EditInputUnit은 새 치수 입력 단위이며, 저장되지 않은 size 입력이 있으면 단위 변경을 차단하고 저장 또는 취소를 안내한다. 원본 canonical mm는 표시 문자열과 분리해 보존하고, 미수정 치수는 저장 요청으로 다시 변환하지 않는다.

## 권한

신규 `Design` 역할에는 `projects.read`, `Project.Read.All`, `PanelInfo.Update`를 부여한다. `PanelInfo.Update`는 Design, Sales, Production Planning 역할에만 부여한다. System Administrator, Manufacturing, Quality, Logistics, Read Only에는 부여하지 않는다. 조회는 모든 활성 내부 역할과 Read Only가 가능하다.

## 프로젝트 상태별 차단

입력은 프로젝트가 삭제되지 않았고 `Status = Active`, 패널이 `Active`, 사용자가 `PanelInfo.Update`를 보유한 경우에만 가능하다. OnHold, Cancelled, Completed, 삭제 프로젝트, Cancelled Panel은 서버에서 최종 차단한다. 기존 값 조회는 권한 범위 안에서 허용한다.

## 입력·수정 이력

직접 입력과 Excel 입력은 변경된 패널 필드별로 감사이력을 남긴다. 변경 전·후 공식 비교값은 canonical mm 값이다. Panel History API는 `InputSource`(`Direct`/`Excel`), `ImportBatchId`, `InputUnit`, `OriginalInputValue`, `ImportFileName`, `ImportUploadedAtUtc`를 반환한다. `OriginalInputValue`는 사용자가 실제 입력한 단위의 보조 추적값이며 canonical 업무값으로 사용하지 않는다. Excel audit은 동일 Apply 요청의 `ImportBatchId`와 `CorrelationId`로 묶이고, 직접 입력 audit은 `InputSource = Direct`, `ImportBatchId = null`이다. Migration 0005 이전 legacy audit은 metadata가 null일 수 있으며 조회와 화면 표시가 실패하면 안 된다. 기존 null에서 최초 입력만 발생한 bulk 요청은 사유가 없어도 되지만, 기존 값 변경 또는 비움이 하나라도 있으면 사유가 필수다. 전체 요청 본문, 인증 토큰, Cookie, Excel 전체 row 내용은 저장하지 않는다.

## 동시수정 규칙

저장은 transaction 안에서 project row를 먼저 `SELECT FOR UPDATE`로 잠근 뒤 대상 panel rows를 `sequence_number` 오름차순으로 잠근다. `ExpectedPanelInfoVersion`이 다르면 전체 rollback하고 409와 `다른 사용자가 패널정보를 수정했습니다. 화면을 새로고침한 후 다시 시도해 주세요.` 메시지를 반환한다.

Excel Apply는 preview 결과를 공식 적용값으로 신뢰하지 않는다. Apply 시 파일 SHA를 확인하고 파일을 다시 파싱한 뒤, transaction 안에서 project row를 잠근 최신 `Status`, `deleted_at_utc`, `PackagingMethod`와 panel row의 최신 상태·version을 확인한다. Preview의 `expectedPackagingMethod`와 잠긴 최신 포장방식이 다르면 409와 “프로젝트 포장방식이 변경되었습니다. Excel 미리보기를 다시 실행해 주세요.” 메시지를 반환한다. 포장방식이 같더라도 현재 조건으로 모든 행을 다시 검증한다.

## API

- `GET /api/projects/{projectId}/panel-information`
- `PATCH /api/projects/{projectId}/panel-information`
- `GET /api/projects/{projectId}/panel-information/history`
- `GET /api/projects/{projectId}/panel-information/import/template?unit=mm|inch`
- `POST /api/projects/{projectId}/panel-information/import/preview`
- `POST /api/projects/{projectId}/panel-information/import/apply`

## 화면

프로젝트 상세에 패널정보 영역을 추가한다. 상단에는 포장방식, 입력 단위, 표시 단위, 완료/미완료/QR 가능 수, 직접 입력, Excel 양식 다운로드, Excel 업로드, 이력을 제공한다. PC는 표 입력, 모바일은 패널별 카드 입력을 제공한다. 권한 없는 사용자는 읽기 전용이다.

## Excel Library

서버 측 `.xlsx` 파싱과 양식 생성을 위해 ClosedXML `0.105.0`을 사용한다. ClosedXML은 `.xlsx`를 지원하고 Formula를 실행하지 않고 workbook 구조를 읽고 쓸 수 있으며, NuGet package metadata의 license expression은 MIT다. Microsoft Excel 설치나 Office 자동화 의존성은 사용하지 않는다.

## Migration

`database/migrations/0005_panel_information_excel_import.sql`을 추가한다. 기존 0001~0004는 수정하지 않는다. Migration은 Design role, `PanelInfo.Update`, role permission, 패널정보 버전/수정 metadata, 치수 precision, Excel import batch, audit source 필드, 인덱스와 FK를 추가한다. 기존 프로젝트·패널·감사 데이터는 삭제하지 않는다.

## 테스트 완료조건

Backend 권한, 직접 입력, 완료/QR 판정, 동일명칭, Excel Preview/Apply, audit metadata, parser sheet/header edge, multipart/file size, parse gate cancellation, 동시수정, 포장방식 변경, migration 테스트를 포함한다. Frontend는 권한별 읽기/편집, 단위 전환, 수정사유, conflict, Excel preview/apply, audit metadata 표시, 모바일 입력을 검증한다. Full-Stack E2E는 실제 React, ASP.NET Core, 전용 PostgreSQL DB를 사용하며 API route mock을 사용하지 않는다. TASK-003B E2E는 직접 입력·동일명칭, WoodenCrate 완료조건·단위 drift 방지, Excel preview/apply/audit, 권한·상태 차단, template 다운로드를 별도 시나리오로 나누어 검증한다. CI Full-Stack E2E는 Docker Compose PostgreSQL을 `up -d --wait --wait-timeout 120 postgres`로 healthy 대기한 뒤 실행한다.
