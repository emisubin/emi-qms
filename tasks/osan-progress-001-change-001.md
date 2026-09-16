# TASK-OSAN-PROGRESS-001 · 이상 처리 및 오산 개인 알림 설정

## 현재 범위와 승인

2026-09-16 사용자 구현 시작 승인. 기준선 `c173552`, branch `codex/osan-stage-issues-notification-preferences`, 작업 폴더 `/private/tmp/emi-osan-stage-issues`. 기존 작업 폴더의 WIP·runtime을 보존한다. 원격 push·병합·배포와 기존 DB 변경은 이번 승인에 포함하지 않는다.

- 정상 완료 초록, 미조치 이상 회사 빨강 `#DA2127`, 미완료/일부 완료 회색. 체크/원 안 숫자를 제거하고 얇은 회색 연결선을 사용한다. 기존 카드 크기·정보 구성 유지. 가능 수량/다음 작업 외곽선, 동작검사 상시 가능, 포장 대기 표시 시안 승인.
- 제조/품질의 기존 진행 입력 권한으로 이상 등록·추가 기록·조치 완료. 코멘트 필수 최대 1000자. 이상 사진 선택, 조치 완료는 현행 완료 사진 필수 및 관리자 코멘트만 저장 예외. 기존 5장·총 40MiB 원본/색상 보존 정책 유지.
- 대상·단계당 열린 이상 1건. 미완료/완료 단계에 등록 가능하나 포장 완료 대상은 제외. 등록하면 해당 단계만 미완료, 진행률 감소. 후속 완료는 유지. 열린 이상은 이전 단계 완료 대신 다음 단계 진행을 허용한다.
- 동작검사(5)는 앞 단계와 독립적으로 완료 가능. 출하검사는 앞 5단계 각각 완료 또는 열린 이상 필요. 포장은 대상의 앞 6단계 전부 완료+열린 이상 없음 필요. 다른 정상 대상은 독립적으로 포장 가능. 전 대상 포장 완료 시 프로젝트 자동 완료 유지.
- 조치 완료는 이상 종료와 해당 단계 완료를 원자적으로 처리. 일반 완료·사진 수정·반려는 열린 이상 동안 제한. 관리자 초기화는 해당 단계 이상을 초기화로 종료하며 미완료/진행률 하향, 후속 완료와 모든 이력 보존. 초기화 알림 없음.
- 기존 상세 화면을 재설계하지 않고 버튼/입력만 추가. 기존 이력에 등록·추가·조치·초기화 당시 사진/코멘트/사용자/시간 보존.
- 이상 등록·조치 완료는 기존 수신자 규칙(오산 사용자/관리자/총괄 관리자)에 인앱·메일·기기 푸시. 조치 완료는 일반 완료 알림 중복 발송 안 함. 기존 메일 형식/한국 시간/사진 링크/대상 단계 연결 유지.
- 프로필 메뉴에 오산 전용 알림 설정 팝업: 7종류(ProjectCreated, StepCompleted, StepRejected, StepEdited, StepIssueRegistered, StepIssueResolved, ProjectCompleted)별 메일/푸시. StepCompleted에만 7단계 상세 설정. 상위 off는 하위 선택 보존. 본인 계정만 변경, 청주와 분리, 인앱 기록 유지. 기본값은 기존 수신 동작 보존. 확정 시안 `/private/tmp/osan-stepper-preview/notifications.html`.

## 구현·검증 상태

구현 및 직접 검증 완료, 사용자 검수 대기. 원격 반영·배포·운영 DB 변경 없음.

- 기존 상세 화면에 이상 등록/기록 추가/조치 완료를 연결하고, 단계 이력과 상태·진행률·작업 가능 판단을 서버와 화면에 함께 반영했다. 추가 기록은 알림을 재발송하지 않는다.
- 알림 설정은 본인 계정에 저장하며, 생성 시점과 발송 직전 모두 설정을 적용한다. 끈 알림은 인앱 기록을 유지하고 재활성화해도 과거 메일/푸시를 재발송하지 않는다.
- backend: 전용 일회용 DB 실행 5회에서 각 테스트의 최신 결과 기준 60개 통과. 신규 이상·중복/경쟁·조회 일관성·권한·기존 사진/이력 회귀·실제 3DB 경계·0102/0103 기존 데이터 보존·메일/푸시 생성 및 발송 직전 억제 포함. 초기 실패는 수정 후 해당 범위를 재검증했다. 결과 `/private/tmp/osan-stage-issues-test-results/`.
- frontend: 관련 직접 검증 최초 71건 통과, 최종 변경 후 관련 5개 파일 55건 통과. 타입 검사 및 빌드 통과, lint 오류 없음(기존 fast-refresh 경고 1건). 결과 `/private/tmp/osan-issue-frontend-final.log` 등.
- 독립 검토의 알림 metadata 제약 충돌·조치 완료 선행조건·null 입력·조회 일관성 4건을 모두 보정했고 재검토에서 잔여 P1/P2 없음. 실제 3DB 화면에서 발견한 새 API 허용 목록 누락도 보정하고 경계 검증을 추가했다.
- 실제 PC와 390px에서 프로필 설정/단계별 설정/저장, 이상 등록/조치 완료/이력, 기존 상세 배치와 스텝퍼를 확인했다. 공통 CSS가 원형·상태색을 지우는 충돌과 모바일 설정 팝업보다 메뉴 버튼이 앞으로 나오는 문제를 보정했다.
- 검수 환경: `/private/tmp/emi-osan-stage-issues` source, UI `http://127.0.0.1:5231`, API `5230`, tmpfs 일회용 Directory/청주/오산 DB, 합성 프로젝트/사용자, 외부 메일·푸시 provider 비활성. 원래 checkout/WIP·공유 UAT·운영 데이터 미변경. 환경 종료 시 합성 데이터가 사라진다.
- 사용자 검수, 최종 병합 후보 전체 회귀, 실제 기기 푸시/메일 운영 발송 확인은 미실행. 병합·공개배포는 별도 명시 승인 범위다. 관련 변경만 로컬 커밋 대상으로 한다.

### 사용자 검수 보정 · 납기 HOLD 배치

프로젝트 수정의 HOLD 체크박스에 일반 입력란 세로 배치가 적용되어 문구와 떨어지던 문제를 전용 가로 정렬로 보정했다. 체크박스 18px, 라벨 클릭 영역 최소 44px, 안내 문구 바로 아래 배치. 1280px/390px 실제 수정 화면 확인. 저장 처리·권한·데이터는 변경하지 않았으며 스타일 보정이라 전체 제품 테스트는 실행하지 않았다.

### 사용자 검수 보정 · 오산 로고 통일

오산 PC 사이드바와 모바일 공통 헤더를 기존 EMI 단독 이미지로 통일했다. 모바일의 특정 화면에만 적용하던 조건을 오산 전체로 확장했다. 청주 로고는 유지. PC/모바일 실제 화면 확인 및 타입 검사 통과.

오산 PC 로고 크기 추가 보정: 사이드바 너비 전체를 차지하던 로고를 사용자 추가 요청에 따라 80×29px로 축소하고 원본 비율 유지. PC 실제 화면 확인. 모바일 로고 크기는 기존 유지.

## 사용자 검수 완료 및 공개배포 승인 · 2026-09-16

사용자가 최종 로고 축소까지 확인하고 이번 변경의 원격 main 병합·공개배포를 명시 승인했다. 인테리어 부스바는 제외한다. 원격 main c173552와 동일 기준선에서 시작한 오산 커밋만 게시하며 기존 원본 checkout WIP는 포함하지 않는다. 최종 후보 전체 회귀는 PR CI가 책임 실행하고 required CI를 우회하지 않는다. 운영에는 추가 migration0102/0103과 Backend/Frontend를 기존 수동 release로 적용하며 bootstrap·membership backfill·DB초기화·provider설정변경은 실행하지 않는다. 실제 원격/배포 결과는 후속 기록한다.

### 최종 CI 감사 분류 보정 · 2026-09-16

- PR #144 첫 CI는 Frontend·Full-Stack E2E·Workflow 검증을 통과했고 Backend 680개 중 679개 통과, 신규 relation 감사 분류 1개가 실패했다.
- 미병합 migration 0102/0103의 mutable 원본 3개(`osan_stage_issues`, 개인 알림 preference/profile)에 기존 중앙 감사 trigger를 추가했다. 발송 식별 메타데이터 `osan_notification_events`는 기존 notifications와 같은 생성물 분류로 명시했다.
- 감사 relation 목록·실제 trigger 수 검증과 오산 preference API 감사 생성/청주 미기록, stage issue 실제 감사 생성 검증을 보강했다. 중앙 개인정보 projection 정책은 유지한다.
- 독립 diff review에 차단 finding 없음. 관련 직접 검증과 보정 커밋의 required CI를 다시 확인하고 통과 후 배포한다.

## 원격 main 병합·공개배포 완료 · 2026-09-16

- 사용자 승인 범위의 오산 변경만 PR #144로 병합했다. 최종 후보 `dd257f6`, main merge `70305eb24e3cd965a8b670eea3c28e0682c20489`. 인테리어 부스바 파일·커밋 및 원본 checkout WIP는 포함하지 않았다.
- 감사 보정 직접 검증 17개 통과. 최종 CI `35051921481`에서 Backend 680/680, Frontend, Workflow Validation, Full-Stack E2E(브라우저·사업부 접근·오산 프로젝트 등록), CI Gate 모두 통과. main CI도 성공했다.
- Azure release `35054251716` 성공. 첫 Frontend 작업의 GitHub OIDC 토큰 발급 실패는 운영 변경 전에 발생했으며, 실패 작업 재실행에서 인증·이미지 생성·배포 모두 성공했다. 인증/비밀값/보호 규칙 변경 없음.
- migration `migration-0zi6xo7` 성공. Backend `backend--0000054`, Frontend `frontend--0000047`: latest=ready, provisioning Succeeded, 새 revision 트래픽 100%. ClamAV 기존 revision 유지.
- 공개 `/health/live` 200, 익명 `/` 및 `/api/projects` 401로 기존 접근 차단 유지. 배포 workflow의 migration/backend/frontend/public-security 검증 모두 PASS.
- bootstrap·membership backfill·drop/reset·기존 데이터 정정은 실행하지 않았다. 기존 데이터 보존은 migration 회귀와 운영 migration 성공 근거이며 운영 사진·이력을 전수 대조한 것은 아니다. 실제 사용자 메일/푸시를 인위적으로 발송하지 않았으므로 새 기능의 실기기 수신 확인은 실제 사용 중 확인한다.
- 배포 결과 기록은 배포 후 문서 전용 로컬 커밋으로 남긴다. 제품 변경은 원격 main 및 공개 환경에 반영 완료했다.

### 알림 설정 메뉴 정렬 보정 · 2026-09-16

사용자가 공개배포까지 승인한 작은 UI 보정: 프로필 메뉴의 오산 `알림 설정` 버튼을 가운데 정렬하고 우측 장식 화살표를 제거했다. PC 및 390px 모바일 실제 화면에서 확인했고 타입 검사 통과. 알림 설정 동작·API·DB 변경 없음. 필수 CI 후 프런트엔드만 공개배포한다.

같은 배포 전 사용자 추가 요청으로 오산 알림 목록의 중복 `알림 설정` 버튼을 제거했다. 오산에서 기존 `/notification-settings`로 접근하면 알림 목록으로 replace 이동하며 기존 설정 페이지는 렌더링하지 않는다. 청주 기존 설정 페이지와 오산 프로필 팝업은 유지한다. PC·390px 알림 목록 및 구 주소 이동 직접 확인, 관련 navigation/청주 설정/오산 팝업 검증과 타입·lint 확인.

### 알림 설정 진입점 정리 공개배포 완료 · 2026-09-16

PR #145 최종 후보 `aa04850`의 required CI `35056864730`(Backend·Frontend·Full-Stack E2E·CI Gate) 통과 후 main `7cbfbc8ae1380d81b63249a9b96118f365495fb4`로 병합했다. Azure release `35059281384` 성공. Frontend `frontend--0000048` latest=ready, Succeeded, 트래픽 100%; Backend `backend--0000054` 유지. Migration/Backend는 SKIPPED, Frontend/PublicSecurity PASS. 공개 health 200·익명 API 401 확인. 가운데 정렬·화살표 제거와 오산 중복 설정 버튼/기존 페이지 진입 제거를 함께 배포했으며 청주 설정과 오산 프로필 팝업은 유지했다. 이 완료 기록은 문서 전용 로컬 커밋으로 보존한다.

## 2026-09-16 후속 구현: Gate 및 공정 진행 요청
- 사용자 구현 승인 및 요청 팝업 시안 확정. 기존 Gate 완료 위치에서 미조치 이상은 조치 완료로 전환. 혼합 패널의 정상 완료 동선 보존.
- 신규 프로젝트 수량 1 고정, 생성·수정·엑셀 수량 입력 제거. 기존 다중 대상·사진·이력 보존.
- 이상 등록 → 공정 이상 발생(회사색), 단계 완료 기능 → Gate 완료, 가능 N대 → 진행 대기. 화면·설정·알림·이력 모두 같은 용어. 지난 납기+완료 D-day는 (납품완료).
- 관리자만 공정 진행 요청: 오산 담당자 복수 선택, 선택 대상에게만 인앱/메일/푸시, 요청자·수신자·시간 이력. 업무 배정·상태 변경 없음. 시안 /private/tmp/osan-request-preview/index.html 구성 확정.
- 오산 알림 설정은 관리자 공통 정책. 개인 설정 보존·미적용, 초기 모두 켜짐, 일반 사용자 버튼은 비활성화+관리자 안내. 서버 GET/PUT도 관리자만 허용. 청주 설정과 인앱 기록 유지.
- branch codex/osan-gate-requests, 제품 기준 main 7cbfbc8과 일치하는 작업 공간. 구현·관련 검증 승인, 이번 원격 병합·배포 미실행.
- 상태: 로컬 구현·직접 검증 완료, 사용자 검수 대기. 이번 변경의 원격 반영·공개배포 없음.
- 구현: 신규 수량 1을 서버에서도 강제하고 구 엑셀의 수량 1 초과를 행 오류로 거부한다. 기존 프로젝트의 수량·대상·진행 이력은 유지한다. 관리자 공통 알림 정책은 0104의 별도 테이블에 저장하며 개인 정책 행은 보존한다. 0105는 공정 요청·수신자 snapshot·이력/알림 이벤트를 추가한다. 실제 provider 호출 없이 선택 수신자 전달 큐까지 검증했다.
- 검증: 프런트 관련 92건, 서버 권한·공통 알림·프로젝트/엑셀·감사·요청/경쟁 47건, 실제 Directory/청주/오산 3DB 경계 1건, 알림 양식 14건 통과. 타입·lint·프런트 빌드 및 Release 서버 빌드 통과. 프런트 빌드의 기존 큰 chunk 경고는 남아 있다. 사용자 검수 뒤 최종 후보 전체 회귀/required CI는 아직 미실행.
- 독립 검토의 P1 W/O 열명 오류와 P2 혼합 대상 실패 요청 재사용을 수정했다. 실제 등록·알림 생성 테스트 및 실패 대상 A→대상 B 전환 회귀로 보정 확인. 후속 reviewer 재개는 도구 thread limit으로 실행하지 못했으며 담당자가 두 finding의 수정과 결과를 확인했다.
- 화면 확인: PC 및 390px 모바일 공정 요청 팝업, 수신자 선택·요청 저장·수신 알림·요청 이력, 이상 등록→기존 위치 조치 완료→완료/진행률 14%, 관리자 공통 설정·단계별 설정, 일반 사용자 버튼 비활성화를 직접 확인했다. 기존 알림 분류 표시도 Gate/이상/조치/요청으로 보정했다. 과거 저장된 알림 본문은 이력으로 보존한다.
- 검수 환경: http://127.0.0.1:5241/ (API 5240), source /private/tmp/emi-osan-stage-issues, 독립 tmpfs PostgreSQL 3DB·합성 프로젝트·외부 provider 비활성화. 기존 5231 및 인테리어 부스바 runtime/WIP는 변경하지 않았다. 최종 서버 빌드로 새 검수 runtime을 기동했다.

### 홈·진행 현황 공통 관리 목록과 HOLD · 2026-09-16

사용자 승인: 프로젝트 메뉴는 완료건을 포함한 전체 원장, 홈/진행 현황은 같은 관리 대상·정렬·집계. 두 업무 화면은 지난 납기의 완료 프로젝트를 제외하고 HOLD를 포함하되 하단에 표시한다. 일반 프로젝트와 HOLD 각각 납기순이며 페이지 분할 전에 서버에서 정렬한다. 요약 명칭은 관리 대상 / 공정 시작 전 / 공정 진행 중 / 포장완료 / HOLD. HOLD는 전체에 포함하고 다른 세 상태와 중복 집계하지 않는다. 상태 필터에 HOLD를 추가했다. 프로젝트 원장도 HOLD 하단 배치 및 상태 표시·필터를 적용하고 원래 업무 상태/진행 기록은 보존한다. 저장된 공정 상태·DB migration·운영 데이터 변경 없음.

구현·직접 검증 완료: 실제 DB 조회/납기 경계/접근 범위/페이지/HOLD 보존 테스트 3건과 관련 프런트 28건 통과, Release 서버 빌드·타입 검사 통과. PC·390px에서 홈/진행 현황 동일 집계(3건 중 HOLD 1건)와 이른 납기 HOLD의 하단 표시를 직접 확인했다. 검수 http://127.0.0.1:5243/ (API 5242), 별도 합성 3DB, 외부 발송 비활성화. 기존 검수 환경 5241은 보존했다. 사용자 검수·원격 병합·공개배포는 미실행.

### Combined Osan and interior busbar release - 2026-09-16
User explicitly approved merging and deploying both changes together. Integration base: production main7cbfbc8, Osan18dff87, busbarb5654f8. Isolated branch codex/osan-busbar-release preserves both existing review runtimes and unrelated WIP. App retains both Osan notification styling and busbar shared theme; lockfile retains jsQR and ZXing.
Only unpublished busbar migrations0090-0099 are renumbered0106-0115. Existing production migrations and Osan0104/0105 remain intact. New0116 adds central audit capture for mutable busbar relations; classification/menu/latest-version tests updated. Original busbar review database and historical ledger are untouched. CI gets an explicitly disposable busbar PostgreSQL service so database tests execute instead of skipping.
Integration validation ongoing. Independent reviewer spawn and existing reviewer resume both rejected with agent thread limit; integration independent review remains incomplete. Direct verification is not independent review. No main merge or production deployment yet. No database reset, local review-data import or artificial ERP transaction is authorized/needed. Production provider readiness is checked separately. Final whole regression belongs to integrated PR CI.

Release gate update: initial branch push was rejected before execution by automatic approval review (remote trust not yet established, failed integration tests awaiting rerun, independent review incomplete). Read-only GitHub verification confirms origin is the same existing repository emisubin/emi-qms, public, current viewer ADMIN, previous PR145 merged main7cbfbc8. No alternate push path used. User decision requested for this one independent-review exception or review in a fresh conversation; pending response. Azure PostgreSQL Ready with14-day backup retention. Existing backend54/frontend48 and public data unchanged. Busbar production publication RBAC and Ecount configuration are currently absent and need completing as part of the approved deployment, after release gates.

Local integration verification complete: Release backend and frontend builds, typecheck, lint(0errors; existing FastRefresh warning), actionlint, change-scope/CI-gate/main-PR-policy tests passed. Backend integrated audit/migration/busbar/work-request suite309/309,0skips,17m13s passed. An extra diagnostic rerun started just as the first suite completed; it was cancelled as redundant after ownership verification (not counted as pass). New0103-to0116 upgrade test1/1 passed with exact existing user/project snapshot preservation, empty new busbar project/ERP queues and actual central audit metadata capture. Its first fixture omitted the migration ledger; corrected to use the real previous-version runner, no product workaround.
Frontend unit suite487 unique tests have passing evidence across initial run and focused reruns: initial473/487;5 affected files112/112;App87/90 then remaining3 passed. All product code unchanged during reruns; one long administrator navigation scenario exceeded10s and passed with a30s diagnostic timeout in13.45s. Preserve the original failure/time evidence; required remote CI remains unexecuted. Synthetic browser2/2 passed for six busbar workspaces at1440/1200/1101/390 and linked/legacy shipment detail/photos; parent directly inspected1440home and390project screenshots. Existing prototypes/runtimes/data preserved.
Next: obtain independent-review resolution; push exact final candidate to existing origin and create PR, run required complete CI incl. general and3DB/Osan full-stack suites, address any failures, normal protected main merge, trusted manual Azure release, busbar management-identity publication and Ecount secret-reference setup, verify readiness/security and report database structure. No approval to bypass CI or main protection. No remote or production mutation performed.
