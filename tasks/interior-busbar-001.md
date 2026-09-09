# TASK-INTERIOR-BUSBAR-001 — 청주 인테리어 부스바

## 현재 범위·승인

2026-09-09 사용자가 대화에서 확정한 전체 신설 계획의 구현을 명시 요청했다. 범위는 전용 메뉴/권한/기준정보 → 구매·입고·재고 → 생산계획·사진 자동완료 → 프로젝트·분할출하 → 독립 QR 게시 → 기본 통합 검수 → 마지막 이카운트 발주 조회 연동이다. 실제 자원 생성·운영 migration·provider 호출·외부 게시·push/PR/merge/배포는 이번 로컬 구현 승인과 별개다.

## 작업 기준

- source `f5b6f71` (시작 시 origin/main), branch `codex/interior-busbar`, worktree `/private/tmp/emi-interior-busbar`.
- 원본 checkout과 타 작업 WIP/runtime 보존. 현재 v2 Root/Backend/Frontend/Scripts 정책 적용.
- root: 조정, 독립 게시 provider·테스트, 통합 검증, 마지막 이카운트 연동. backend 담당: schema/core API/store/권한/tests. frontend 담당: UI/client/tests. 서로 다른 파일 소유.

## 보존할 업무 계약

- 활성 청주 조회, 별도 담당자·총괄만 입력/사유 정정. 외주 작업자 명단은 로그인 계정과 분리.
- 제품군 공용 수량재고, 제품군 하나/프로젝트 등록 건, 제품별 사진·번호 분리. 프로젝트명 중복 허용; 실제 확인 없는 제품번호 출하 귀속 금지.
- 도급/사급은 자재 속성, 공통 프로젝트 코드 대상 발주 자재 전부 관리. 발주 수신은 재고 불변, 분할 입고 시 증가.
- 버전 BOM으로 생산완료 자재 자동 차감. 자재 음수 허용, 완제품 음수 금지. BOM 미설정 제품은 완료 금지.
- 앞/뒤 사진 등록: 직접 촬영·기존 이미지 허용, 두 번째 필수 사진의 서버 등록 시각(한국시간 표시)이 제조시간. 두 장이면 자동 완료; 실제 작업자명과 등록 관리자 구분. 1장/업로드실패는 미완료. 생산·BOM snapshot·완제품/자재 증감 원자적 1회.
- 일별 제품군 목표, 계획 없음/초과 생산 허용. 부분 출하 시 재고·요청 잔여 초과 금지, 누적 전량 시 완료. 납기 자동출하/예약 없음.
- 기초재고·정정은 이력 보존. 취소는 과거 BOM으로 복원. 사진 정정·QR재출력은 재고·최초 제조시간 불변.
- 외부 사이트는 제품번호·앞뒤 사진·제조일시·작업자만 정적 게시. PMS 호출/로그인/내부 사진주소 의존 없음. 생산완료와 게시 분리, 성공 전 QR출력 금지, 재시도·최신revision 보호, 취소 시 공개중지.
- 이카운트 수신은 마지막 구현; 공통 코드 단방향 발주조회만. 반복수신 중복방지, 입고보존, 변경충돌표시. 외부 실제 사양을 검증하지 않은 wire contract를 사실로 만들지 않음.
- 현장 품질검사는 현행 유지, PMS 판정/성적서·실물반품/재작업/원가정산 제외.

## 검증·완료 조건

격리 합성 DB의 migration fresh/existing, 실제 서버 권한/타사업부 거부, 재고 경합·idempotency·snapshot취소·부분출하, 두 사진의 서버시간과 자동완료를 검증한다. 외부 provider는 fake/local로 독립 HTML·failure/retry/latest revision 확인. UI는 desktop/390px을 직접 확인한다. 권한/DB/동시성/공개 경계는 작성과 분리된 독립 검토 필요. 사용자 검수 뒤 최종 후보 전체 회귀 책임 실행 1회; required CI 별도 유지.

## 구현 결과·현재 상태

| 범위 | 현재 결과 |
| --- | --- |
| Task 1 전용 메뉴·권한·기준정보 | 구현 및 관련 자동 검증 완료. 청주 전용 메뉴/서버 권한, 담당자 role, 제품군·자재·외주 작업자·공통 코드·버전 BOM |
| Task 2 구매·입고·재고 | 구현 및 관련 자동 검증 완료. 직접/엑셀 발주, 분할 입고, 기초재고·보정·원장 역분개 |
| Task 3 생산·사진 | 구현 및 관련 자동 검증 완료. 날짜별 계획, 사진 두 장 자동 완료, 서버 제조시간, BOM snapshot, 작업자/등록 관리자 분리 |
| Task 4 프로젝트·출하 | 구현 및 관련 자동 검증 완료. 식별자 기반 엑셀 preview/apply, 제품군 공용 재고, 분할 출하·완료/취소 재개 |
| Task 5 독립 조회 사이트·QR | 코드·로컬 독립 페이지 검증 완료. 실제 Azure 자원 생성·설정·게시와 공개 주소 검수는 미실행 |
| Task 6 기본 통합 검수 | 합성 DB·실제 HTTP 사진 API·mock 브라우저 흐름 검증 완료. 실제 기기 카메라·제품 매칭·프린터/QR 현장 검수는 대기 |
| Task 7 마지막 이카운트 연동 | **미구현·자료 대기.** 공식 세부 API 문서와 비식별 발주 응답 샘플을 요청했다. 실제 wire contract를 추측하여 연결하지 않았다 |

발주·자재의 이카운트 품목 코드와 공통 프로젝트 코드를 직접 입력할 수 있으며, 자동 연동 준비 미완료 상태를 화면에 표시한다. 공식 [이카운트 Open API 안내](https://www.ecount.com/kr/ecount/product/erp_open-api)에서 발주서 조회 제공과 로그인 후 세부 매뉴얼 확인을 검증했다(2026-09-09). Task 7은 발주/행의 안정적 식별자, 공통 프로젝트 필드, 수정·취소 표현, pagination/제한을 실제 문서·샘플과 대조한 뒤 진행한다. 완료한 내부 업무에 실제 연동 완료 표시를 붙이지 않는다.

## 검증 증거

- Backend Release build 및 `FullyQualifiedName~InteriorBusbar|FullyQualifiedName~AuditMutation`: **36개 통과, 실패/skip 0**. 전용 `busbar_test` DB(127.0.0.1:55490)의 매 테스트 고유 schema와 합성 계정만 사용했다.
- 추가 DB 경계 테스트 **2개 통과, 실패/skip 0**: 게시 worker에 오산/Directory identity DB를 연결하면 제품을 읽거나 외부 sink를 호출하기 전에 거부. 서버 관련 검증 총 **38개**(36개 실행 + 신규 경계 2개 실행).
- 실제 HTTP 검증: 익명 차단, 기존 부서 입력 거부, 담당자 입력/권한 회수 즉시 거부, trusted OSAN 컨텍스트의 GET/POST가 실제 사업부 middleware에서 403. 실제 multipart의 잘못된 이미지 거부 → 앞면 미완료 → 뒷면 자동 완료 → 제조시각/등록자/재고 → 중복 재고 방지 → 미게시 QR 409.
- 재고 검증: BOM 당시 소모량으로 취소, 자재 음수, BOM 누락 rollback, 30개 재고 동시 20개 출하 경쟁, 60개를 20개씩 3회 출하 및 취소 후 재개, Excel 전부 적용/전부 rollback, 계획 변경 전 값 보존.
- 게시 검증: 실패해도 생산 유지, 정정 후 최신 내용 재시도, 동일 토큰으로 취소 페이지, safe error, HTML escape, 한국시간, Azure conditional header/412, 지연된 과거 PUT보다 최신 PUT이 먼저 저장되면 과거 요청 거부.
- 독립 페이지: 생성 HTML을 `file://`로 별도 Chromium에서 열고 모든 HTTP(S)를 차단. 외부 요청 **0**, 두 이미지 로딩(각 320px) 성공, 제조시간 표시, 390px 가로 넘침 없음. 합성 증거 `/private/tmp/emi-busbar-public.html`, `/private/tmp/emi-busbar-public-390.png` 직접 확인. Azure 실제 게시/네트워크 장애 검수를 대신하지 않는다.
- Migration: 격리 컨테이너 `emi-busbar-test-20260909` 안의 별도 생성 DB에서 **fresh 90개 / 기존 0089→0090 업그레이드** 적용 통과. 기존 sentinel 보존, 담당자 role·설정 singleton 확인. 스크립트 `/private/tmp/emi-busbar-verify-migrations.py`; 검증용으로 생성한 두 DB만 종료 후 삭제했다.
- Frontend: `pnpm typecheck`, `pnpm build`, 변경 파일 ESLint 통과. 기존 bundle 크기 경고는 유지. Playwright 기본 5개 통과 후 리뷰 영향 신규 2개(BOM 최신값/미완료 정정·취소) 통과. PC/390px과 사진 완료 화면 직접 시각 확인. 합성 증거 `/private/tmp/emi-busbar-*.png`.
- 사용자 검수 후의 최종 후보 전체 회귀와 required CI는 아직 실행하지 않았다.

## 독립 검토

요청 모델 GPT-6 Astra/high, 도구의 실제 모델 별도 관측값은 `NOT_REPORTED`. 작성과 분리된 `/root/busbar_review`가 base `f5b6f71`부터 누적 WIP/신규 파일을 검토했다. P0/P1 없음. P2 BOM 편집값의 과거 버전 잔류, P2 Draft 정정·취소 미노출, P3 계획 감사 before 누락을 모두 보정했다. 수정분 재검토에서 모두 해소, 새 Finding 없음. 테스트 실행은 parent/구현자의 증거이며 reviewer가 별도 실행했다고 기록하지 않는다.

재검토 내용 hash: page `b6e29c1441c1b1fda687dfc3c2150bf6d2eabaed45160bb71119a8cff4b47d6c`, store `842e5bfecc5a30892cf2985ca4173f27912f574806f5ef77353aaef8e1f31705`, migration `8f07b337b72f9ed65283a81df11e51fd0e3d17e6a17f70e3fb17691e68f92245`.

## 사용·운영 준비

1. 승인된 대상에 migration 배포 후 관리 화면에서 별도 `인테리어 부스바 담당자` 역할을 부여한다. 기존 부서 역할만으로 입력 권한이 생기지 않는다.
2. 기준정보에서 제품군·자재 코드/단위/사급·도급·외주 작업자·공통 프로젝트 코드를 입력한다. 제품군당 1개 기준 소요량을 설정하고 도입 전 재고를 기초재고로 등록한다.
3. 생산·사진 화면에서 제품군/실제 작업자를 고르고 앞·뒤 사진을 등록한다. 두 번째 등록으로 완료되며 게시 상태가 성공하면 같은 제품번호의 QR을 출력한다. 납품 프로젝트에서 실제 출하 수량을 나눠 입력한다.
4. 잘못된 기록은 사유를 입력해 정정·취소한다. 완료 제품 사진 변경과 작업자 정정은 기존 제조시간을 보존하고 같은 공개 주소를 다시 게시한다.

외부 게시 기본값은 비활성이며 ReviewSafe에서는 켜지지 않는다. 실제 게시 승인을 받은 뒤 **PMS 사진 저장소와 별개인 Azure Storage 계정**의 정적 웹사이트를 준비한다. HTML에 정규화된 사진 두 장을 포함하여 한 blob의 조건부 교체로 사진/본문 버전을 일치시킨다. 공개 페이지/사진은 PMS 서버에 의존하지 않는다.

설정은 secret-safe 환경 구성으로 제공한다(실제 비밀/URL 값을 저장소에 기록하지 않음).

- `InteriorBusbar:Publication:Enabled`: 명시적 `true`로 게시 worker 실행.
- `InteriorBusbar:Publication:PublicBaseUrl`: 해당 계정의 `https://<account>.z<번호>.web.core.windows.net/` 기본 주소.
- `InteriorBusbar:Publication:BlobEndpoint`: 같은 계정의 `https://<account>.blob.core.windows.net/` 기본 주소.
- `InteriorBusbar:Publication:SasToken`: `$web` 게시 blob의 HEAD와 생성/교체에 필요한 읽기·생성·쓰기 범위의 비밀. 별도 secret 저장소로 공급하며 로그·Task에 남기지 않는다.

worker는 청주 DB만 명시적으로 선택하고 사업부 identity를 확인한다. 5초마다 대기 제품 한 건을 처리하며 실패 건은 내부에서 재요청한다. 현재는 최신 내용 보장을 위해 게시 중 동일 재고 mutation lock을 최대 게시 timeout 20초 동안 보유하므로 게시 장애 시 입력 지연이 생길 수 있다. 성공 전 QR 출력은 제공하지 않는다. 실제 Azure 설정/접근권한/만료/게시 실패 모니터링은 배포 전에 확인해야 한다.

## 남은 일·Git 상태

- Task 7: 세부 API 문서와 비식별 응답 샘플 확보 → 필드/식별자 확정 → 단방향 조회/중복·변경·취소 충돌/재시도 구현 → 합성 및 승인된 연동 환경 검증.
- 사용자 검수: 실제 촬영/사진 선택/제품 매칭/임시 스티커·라벨 출력 동선, 청주 담당 권한, 업무 수량 검수.
- 실제 Azure 독립 사이트 준비·게시, 공유 runtime handover, 운영 migration/배포는 미실행. 생성한 로컬 검증 자원 외 실제 provider/운영 데이터 변경 없음.
- branch `codex/interior-busbar`; 검증·독립 검토를 마친 내부 업무 변경은 로컬 commit으로 보존한다. push/PR/merge/운영 배포 미실행. 원본 checkout의 타 작업 WIP는 보존했다.

## 2026-09-09 로컬 검수 서버 공개

사용자가 “서버 열어서 보여줘”라고 요청하여 `8b15e48` source를 별도 검수 서버로 실행했다. Backend `http://127.0.0.1:5097`, Frontend `http://127.0.0.1:5197/interior-busbar`. DB는 이 작업 소유 컨테이너 `emi-busbar-test-20260909`의 신규 `busbar_review_20260909`이며 기존 테스트 DB 및 공유/운영 DB는 변경하지 않았다. 전체 migration 및 개발용 합성 계정 seed 후 readiness OK, 부스바 workspace 200/canWrite=true 확인. 부스바 기준정보/업무 데이터는 빈 상태다.

Codex 브라우저에서 개발 검수 계정 `dev-admin`을 선택하고 전용 화면 6개 탭·빈 현황 표시를 확인해 열어 두었다. 외부 게시·이카운트·알림 발송 worker는 비활성. Backend PID 43408, Frontend launcher PID 43409. 로그/PID 파일은 `/private/tmp/emi-busbar-review-{backend,frontend}.{log,pid}`. 다른 작업의 5096 서버는 보존했다. 사용자 검수 완료를 의미하지 않으며 원격/운영 배포는 없다.

## Change 002 — 계획에서 미완료 제품 선생성

사용자가 확인 후 수정 구현을 승인했다. 계획 수량만큼 작업자·사진·정식번호 없는 제품을 먼저 생성하고, 관리자가 기존 제품을 열어 실제 작업자와 앞·뒤 사진을 입력한다. 두 장 등록이 완료될 때 기존 제조시간·재고 원자처리와 제품번호 발급을 유지한다. 생성/촬영 UI를 분리하고 완료번호를 눈에 띄게 표시한다.

계획 재저장은 중복 생성하지 않는다. 수량 증가는 차이만 추가하고 감소는 작업자/사진 등록 전 미완료 제품만 취소 이력으로 보존한다. 작업을 시작한 제품 수 아래로 줄이거나 생성 제품의 제품군/계획일을 바꾸는 요청은 거부하여 기존 실물 식별을 보존한다. 기존 수동 생성/완료 제품은 임의로 계획에 연결하지 않는다. 0090은 이미 검수 DB에 적용되었으므로 additive 0091을 사용한다. 적용 대상은 현재 5097/5197의 별도 합성 검수 환경이며 외부 게시·이카운트는 계속 비활성이다.

상태: 구현·관련 검증 진행 중. 기존 사용자 검수 기록과 타 작업 서버는 보존.

Change 002 결과: 구현·관련 자동 검증·독립 검토 완료, 현재 검수 서버 반영. 계획 저장 시 수량별 Draft 생성, planId 필터/임시 순번, 사진 등록 시 작업자 선택, 두 장 완료 번호 강조와 화면 이동을 적용했다. Backend 관련 41개 통과(실패/skip 0), frontend 기본/신규 9개 및 리뷰 반례 2개 통과, typecheck/대상 ESLint 통과. 0091을 포함한 migration 91개 fresh/upgrade 적용과 기존 sentinel 보존을 확인했다. 독립 reviewer `/root/plan_review`(요청 Astra/high, 실제 모델 NOT_REPORTED)의 유일한 P2 R1(업로드 중 계획 변경 후 과거 조회가 덮음)은 최신 load ref로 보정하고 필터 직접 변경/탭 제품보기 두 반례를 통과했으며 재검토에서 해소 판정했다.

5097 Backend를 새 binary로 갱신(PID 47761, 개발 계정 재seed 비활성)하고 readiness와 실제 API를 확인했다. 기존 계획 30개에 미착수 제품 30개를 준비했고 기존 수동 제품 2개의 상태·번호·미연결 상태를 보존했다. 현재 5197에서 계획→제품 보기→작업자 선택·앞뒤 사진 화면을 직접 열어 확인했다. 제품 선택 시 사진 영역으로 이동하고 고정 헤더가 내용을 가리지 않게 위치를 조정했다. 실제 사진을 대신 등록하거나 기존 제품을 완료/취소하지 않았다. 사용자 최종 검수·원격 push/merge·실제 외부 게시/배포는 여전히 미실행이다.
