# G2 UPG 부적합 현황 — 로컬 구현

## 승인과 범위
2026-10-12 사용자: PC/모바일 시안 확정 후 '로컬 구현 해봐. 디자인은 코드 그대로 옮겨.'
기준 worktree codex/unified-osan-shell, HEAD 43cf3d4. 기존 untracked local review 진입과 node_modules 보존. 원격 게시/병합/배포 승인 없음.

- G2 > 품질관리 > UPG 부적합 현황, 수냉기구물은 후속 별도 기획(준비중).
- 제품 한 대 한 건. 1차: 시리얼, 발생일, 발생책임(생산팀 불량/협력사 불량), 내용, 사진1장이상 모두필수.
- 2차: 수리완료일(오늘기본), 조치내용, 사진1장이상 모두필수. 저장시 수리완료.
- 조회 모든부서, 등록/수정/삭제 품질 및 실제관리자. 서버에서 최종권한확인.
- 2차삭제시 미완료. 2차가있으면1차삭제불가. 별도 전체삭제/완료토글없음.
- 수정/삭제 이력3개월 보존, 이후 과거정보 및 참조없는사진 영구정리. 현행정보보존.
- 초기재고 개별등록: 부적합발생집계 제외, 미완료재고 및 완료시 수리집계 포함. 종료는 사용자추가확인 후 기능제거.
- 생산/출하관리 합계는 연결하고 신규 숫자 불량/수리입력 및 불량재고실사는 제거. 납품가능재고실사 유지. 과거기록 유지.

## 디자인 기준
확정원본 upg-ecount.html (PC revision7 상당), upg-mobile.html revision4. source folder /Users/parksubin/.codex/visualizations/2026/10/08/01a118d1-afc8-7351-9370-0126a196e572/g2-home-mockups.
- 요약없음. PC 검색 한줄, 검색/Enter시에만적용. 기간전체기본.
- 모바일 목록날짜없음, 시리얼/발생유형/상태 첫줄, 내용다음줄.
- 상세상단 시리얼/상태/발생유형 한줄, 1차제목옆작은수정삭제, 발생일맨오른쪽. 2차도동일.
- 팝업공통스타일 유지, 사진은 실제업로드파일로 연결.

## 검증 계획/진행
아래 구현 및 검증 결과를 따른다.
DB운영변경/공유runtime교체/실제provider사용/배포는 없음. 신규테이블과 검증용DB 이름/정리결과는 완료시기록.


## 구현 및 검증 결과 · 2026-10-12
- 실제 공통 G2PageFrame·내비게이션에 /g2/quality/upg 연결. 수냉기구물은 준비중 비활성.
- 승인 HTML 원본은 frontend/src/g2-approved/upg-pc-approved.html 및 upg-mobile-approved.html로 보존. 내용 CSS를 scope 처리하고 shell 스타일 충돌만 차단. 신규 레퍼런스/재디자인 없음.
- 서버 API는 현재 품질 부서/실제 관리자 권한을 DB에서 검사. 버전 기반 동시 수정 보호, 사진 ID 보존과 인증된 사진 조회, 유효 이미지 디코딩, 정규화 시리얼 중복 거부, 완료일 선후관계 검사.
- 청주 신규 migration0135: g2_nonconformances(현재 정보/사진), g2_nonconformance_history(수정·삭제 snapshot,3개월), g2_quality_settings(한 번 저장하는 서울 기준 도입일), g2_projected_metrics view(기존 실적+등록 건수). 별도 신규 업무 DB 없음.
- 초기재고는 신규 발생 집계 제외. 과거 숫자 실적 보존+개별 건수 합산. 도입일 이전 재고/실사 조회 보존, 이후 재고는 개별 미완료 건. 납품가능재고 실사 유지.
- frontend 영향 tests: 신규 UPG7, operations11, navigation11, home10 = 총39개 통과. 신규7+operations11 및 navigation11은 최종 보정 후 재실행. frontend build/typecheck·변경파일 eslint 통과.
- Playwright Chromium 실제 React 화면 PC1440×1000/모바일390×844 목록·상세 관찰. pageerror0, 모바일 page overflow 없음. /tmp/upg-local-pc.png, /tmp/upg-local-pc-detail.png, /tmp/upg-local-mobile.png, /tmp/upg-local-mobile-detail.png 직접 열어 확인. 비교 원본: localhost8781 upg-ecount.html. 표행36px/헤더38px 확인. 자료는 합성만 사용.
- 검수용 localhost5187은 전용 frontend/source이며 local-upg-review.* 합성 fetch entry 사용. 운영 연결/실제 DB 저장 화면 검수로 취급하지 않음. 기존 runtime·기존 local-*-review 파일 보존. 새 임시 검수 entry는 commit 제외.
- 독립 reviewer /root/upg_review (요청 gpt-6-astra/high, 관측모델 NOT_REPORTED): P2 R1과거실사조회,R2유효사진검증,R3날짜선후,R4준비중메뉴를 제기했고 보정 후 추가 P0/P1/P2 없음. 마지막 backend 실행 결과는 아래에 추가.
- 구현 사용자 검수/전체 최종병합회귀/원격push·PR·병합·공개배포 미실행. 운영/Persistent UAT DB 변경 없음. 더 이상 사용하지 않는 DB 없음.


### 최종 서버 검증 / DB 자원
- backend36/36(UPG+생산출하+자재DB+감사경로), 도입일 보정 후23/23(UPG+생산출하), 보안 반례 보강 후2/2 UPG lifecycle 통과. build 성공.
- 실제 HTTP 사진조회/등록/조치/삭제 성공, 제조쓰기403, 비로그인사진401, 중복409, 1차삭제제한400. OSAN context로 Cheongju 목록/사진/쓰기 business_unit_module_mismatch 거부. 수정경쟁·비활성/회수관리자·사진실제디코딩·3개월삭제 검증.
- 로그 /tmp/upg-backend-tests-final.log, /tmp/upg-backend-tests-activation.log, /tmp/upg-backend-tests-security.log. 독립review R1–R4 해소 확인. 이후 마지막 변경은 한국어 오류 용어 부적합으로 통일(동작 변화 없음).
- 마지막 검증용DB emi_qms_e2e_20261012074537_36344_6359 및 컨테이너 emi-qms-e2e-20261012074537-36344-6359-e2e-postgres-1, 네트워크 정리 확인. 앞선 실행의 소유 임시DB/컨테이너도 자동 정리. fixture의 임시 Directory/Cheongju/Osan DB도 dispose와 tmpfs컨테이너 삭제로 정리.
- 신규 영구DB 없음. 기존청주 DB용3테이블+view migration 작성만 완료, 영구DB에 미적용. 더 이상 사용하지 않는 DB 없음(과거실적·실사 테이블 조회용 유지).

### 로컬 서버 열기 · 2026-10-12 후속 요청
사용자 요청으로 d7d001c 소스의 실제 API+frontend를 신규 전용 검수 환경에 기동했다. Frontend http://127.0.0.1:5188/g2/quality/upg, API5087. 기본 dev-quality 계정, 외부 provider 비활성. 브라우저 heading/등록버튼 및 pageerror0 확인, health와 품질 목록 API200. 목록은 빈 DB에서 시작하며 실제 등록/수정 가능.
- 신규 검수용 임시DB: emi_qms_e2e_upg_preview_20261012 (현재 사용자 검수용으로 실행·유지 중, 아직 정리하지 않음). 컨테이너 emi-qms-e2e-upg-preview-20261012-e2e-postgres-1, tmpfs. 기존 영구DB/운영DB 변경 없음, 미사용DB 없음.
- 표준 e2e 안전검사와 legacy-shaped 격리 fixture 방식 사용: common migration130 + 청주0132–0135 overlay. 실제 영구3DB 배포 환경으로 취급하지 않는다.
- 소유 실행 /tmp/upg-preview-start.sh, execsession92793; 종료trap은 소유 backend/frontend 및 전용 Compose 자원을 정리한다. 소유 로그 /tmp/upg-preview-{build,migrate,backend,frontend}.log. 영구 보관용 입력 환경이 아니므로 검수 종료 시 입력도 함께 정리된다.
- 기존 합성 preview5187와 원래 mock8781은 유지. 사용자에게 연 주소는 실제API 연결5188이다. 원격/배포 변화 없음.

### 품질관리 하위 메뉴 · 2026-10-12
사용자 요청: 품질관리 클릭 시 펼침/접힘, 메뉴명 UPG·수냉기구물로 축약. 기존 공통 하위메뉴 스타일을 재사용하여 PC nested disclosure와 모바일 depth별 펼침 상태 적용. 페이지 제목은 유지, 수냉기구물 준비중 유지. navigation11개·typecheck·변경파일eslint 통과, 실제 localhost5188 PC/390px 모바일 펼침/접힘/비활성 동작과 화면 확인. DB 생성/미사용DB 변화 없음; 검수 임시DB는 앞 절대로 실행 유지.

### G2 홈·모바일 공통 보정 · 2026-10-12 진행
사용자 “모두 수정해” 승인. 공개 화면 조사에서 홈 SVG에 공통 grayscale 필터 적용, 홈 제품재고가 최근작업일 재고로 표시, 30일 전망에 생산 미반영, 모바일 계획 기간 입력 intrinsic width 초과 확인.
- 목표: 승인 색상 복원, 오늘 재고와 오늘~월말 일별 전망, 전체 과거 생산실적의 일평균을 예상 추가생산으로 반영, 모바일 입력공통 너비 보정. 실제 재고/실적 저장 및 DB schema 변경 없음.
- 평균: 서버 오늘 이전에 생산실적이 입력된 날짜 전체, 오전+오후 합계의 일평균. 미입력 제외·명시0 포함·오늘/미래 제외. 예정 생산일은 명시 계획 우선, 미입력은 토요일 포함·일요일/공휴일 제외. 기존 재고 계산처럼 전날 예상생산을 다음날 재고에 반영. 오늘 재고에는 납품을 다시 차감하지 않음.
- 검증 예정: 평균 집계 서버 반례, 전망 날짜/0/미입력/휴일/월말, 공통스타일이 적용된 실제 화면 색상, 320/375/390 입력창과 데스크톱 보존. 로컬 구현만, 원격·배포 제외.

#### 보정 완료 및 검증
- 공개 공통 grayscale의 홈 SVG/차트 예외 적용, 빨강·청록·파랑 시각 확인. 오늘 재고/월말까지 일별 점/추가생산 표시 구현. backend home 응답에 averageDailyProduction/productionSampleDays 추가(기존 업무 DB 조회만).
- frontend 관련56개 통과, 최종 홈12개 재확인, typecheck·변경파일 eslint·production build 통과(기존 bundle size 경고 유지). backend 평균 및 G2Operations22개 통과.
- Chromium320/375/390의 계획·실적·실사·출근·품질 입력 모두 내부 가로넘침0. 자재320 긴 품목명 상세/입력 Chromium+WebKit 넘침0 및 input min-width0/max-width100% 확인. PC1440/mobile390 홈 색상 필터none3개, page overflow0. 합성 screenshot /tmp/g2-home-fixed-{desktop,mobile}.png, /tmp/g2-fixed-320-계획 입력.png, /tmp/g2-fixed-320-품질.png, /tmp/g2-material-audit-webkit.png 직접 열어 확인. 화면/데이터 실제 저장 없음.
- 독립review /root/forecast_review 요청gpt-6-astra/high(관측모델NOT_REPORTED): 자재 doubled-ID reset 우선순위 P2 R1 보정, 재검토 남은P0/P1/P2없음. 이후 긴 품목명 wrapping 추가는 부모가 Chromium/WebKit 반례로 직접 확인. 월말 하루 그래프의 정상재고 대기문구도 보정.
- localhost5188은 같은 소스 HMR 유지, API5087은 최신빌드로 소유backend만 교체. 새PID44805/session29351, /tmp/upg-preview-backend-refresh.sh. 기존프론트PID37600/부모37464 유지. DB reset/migration/seed 없음. 실제 health ready와 home 평균null/표본0 확인(검수DB는 비어있음). 종료 시 새backend를 별도로 종료해야 함.
- 신규 업무DB 없음, 테스트DB emi_qms_e2e_g2_forecast_average_20261012와 내부3DB fixture/컨테이너는 정리 완료. 기존 검수DB emi_qms_e2e_upg_preview_20261012는 사용자 확인용 유지. 더 이상 쓰지 않는 DB 없음. 원격반영·병합·공개배포 미실행, 사용자 검수 대기.

### 재고 전망 7일 변경 · 2026-10-13
사용자 승인으로 월말 종료를 오늘 포함7일(오늘~오늘+6일)로 변경. 월 경계와 무관하게7개 일별 점을 표시하며 기존 평균생산/납품 차감/오늘재고 출발은 유지. 조회 종료일·공휴일 범위·안내기간이 같은 종료일을 사용. DB/API 변경 없음. 이전 월말 기록은 당시 이력이며 현재 계약은 이 절이 우선.
- 검증: 홈13개 통과, TypeScript 통과, diffcheck 통과. 실제 공통틀 PC1440/모바일390 합성 화면에서7일 점·색상·가로넘침0 확인. 10/29~11/4 월경계 테스트 포함. 로컬5188 HMR 반영, 배포 미실행. 신규/미사용DB 없음, 이번 검증DB 생성 없음; 기존previewDB 유지.

### 과거 공통 테마 폐기·사진 재사용 규칙 · 2026-10-13 진행
사용자 승인: 과거 공통 스타일 전부 삭제, 앞으로 사진 업로드 기능은 검증된 코드를 그대로 재사용하도록 규칙화. Graphite wireframe 로딩/파일과 이를 강제하던 테스트·불필요 예외를 제거한다. 현행 확정 G2/오산 공통틀과 기능상 필요한 레이아웃은 현행 소유 스타일에 한정하여 보존하며 구 테마를 이름만 바꾸어 옮기지 않는다. 사진 공통화 원칙은 Root AGENTS에 기록; 이번 요청은 규칙 설정이며 UPG 업로드 흐름 자체 재구현은 포함하지 않는다.
- 검증: 로딩/스타일 의존성·관련 화면/공통컴포넌트 테스트, PC/모바일 G2·오산/로그인 화면 및 사진 색상, 독립 검토. 기존 사용자 preview DB/사진은 읽기만 하고 변경·초기화하지 않는다. 원격 게시·배포 제외.

#### 과거 공통 테마 폐기 완료 · 2026-10-13
- wireframe.css 3,150줄과 main/인증 fixture import, 구 테마 전용 unit2개 삭제. 전역 흑백/강제색상/배경 초기화 및 --wire 의존 제거. 공통 입력·상태·선택 컴포넌트의 기능적 배치만 tokens.css, 현행 메뉴·로고·회사정보는 shared-shell.css에 유지. EMI 빨강/중립색 토큰 사용.
- Root AGENTS 사진 업로드·미리보기·조회 실제 재사용 규칙 기록. 이번 범위는 규칙 설정이며 UPG 업로드 자체 재구현 아님.
- frontend 고유117개(기존메뉴/사진29+G2 71+공통DS17), 오산 대표 mock E2E3개 통과. 타입검사/빌드 확인, 기존 bundle크기 경고. auth-shell.spec.ts의 기능·기하검증을 보존해 원본 유지했으며 과거 흑백 기대값은 이번 미실행/미현행화. 현재 인증 로딩은 PC1440/mobile390 직접 시각 확인(넘침0/aria-busy).
- PC1440/mobile390 G2·공통틀 비교, 320px 계획/실적/실사/인원/품질 입력5종 넘침0. 실제 UPG사진2개 로딩성공/filter:none, 조치사진원색 확인. 1차사진은 필터해제후에도 비어보이며 별도원인 미확정. 사용자데이터 수정 없음. 합성증거 /tmp/theme-final-*, /tmp/theme-input-320-*, /tmp/theme-auth-loading-*, /tmp/theme-osan-e2e.
- 독립review retire_theme_review 요청 gpt-6-astra/high(실제모델NOT_REPORTED), HEAD55c32fe 대비 전체WIP와 신규test 검토 GO, 남은P0/P1/P2없음. 중간 auth test 축약은 거부하고 원본복원.
- 종료돼 있던 API5087은 기존 refresh script로 재기동(PID59076/session22743), health200. migration/seed/reset없음, 사용자case유지 확인. frontend5188 유지. 신규/미사용DB없음, 이번 임시DB 생성없음. 기존 emi_qms_e2e_upg_preview_20261012 검수용 유지·미정리. 원격push/병합/배포 없음.

### 품질 사진 첨부 공용화 · 2026-10-13
사용자 승인: 품질 사진 업로드를 파일 첨부 대신 사진 첨부로 교체. 확정된 UPG 등록·수정·상세 배치를 유지하고 오산 촬영/사진 선택, 미리보기, 개별 제거, 저장 사진 조회/확대/재시도 코드를 실제 재사용한다. 현재 사용자 검수 DB의 기존 부적합·사진은 보존하며 원격 반영·배포는 제외한다.
- 프런트: OsanPhotoPreview/SavedPhoto의 구현을 공용 PhotoPreview/SavedPhotoView로 추출, 오산 wrapper와 UPG 모두 호출. OsanRecordPhoto 재사용. 파일명 목록 UI 및 스타일 제거. 공용 사진 선택 검증은 각 업무 기존 제한 유지(UPG 10장·장당5MB·단계20MB, 오산5장·40MiB). 기존사진과 새사진 합산. 빈 MIME은 octet-stream으로 서버 실물 판독에 전달.
- 서버: 기존 오산 이미지 검사·메타데이터 제거·HEIC 미리보기·악성코드 검사 재사용, 처리된 바이트 저장. 기존 Id 사진 유지 경로·캠퍼스/권한/버전 계약 보존. 공개 byteSize와 인증된 UPG HEIC preview 추가. 별도 DB/schema 없음.
- 프런트 검증: 관련38개 테스트, TypeScript, production build, 오산 mock E2E3개. 빌드 기존 bundle크기 경고 유지. PC1440·모바일390/320 사진첨부/상세/확대 합성 화면 확인, 가로넘침0, 미리보기 정상색, 모바일 상세사진2열 유지. /tmp/upg-photo-*.png. 실제 사용자 사진/데이터 저장 없음.
- 서버 검증·독립검토·로컬 runtime 반영 결과는 아래에 기록한다.
- 완료: 서버 Debug/Release 빌드, 비DB2개·전용Postgres 통합1개 통과. WebP 원본검사→PNG처리바이트저장, infected422/검사장애503, 유지사진재검사방지, 권한거부 및 품질/관리자, HEIC 저장·원본/미리보기 조회 실제분기 확인. 독립review upg_photo_reuse_review 요청gpt-6-astra/high(실제모델NOT_REPORTED) 최종GO, 남은P0/P1/P2없음.
- 로컬5188 반영, API5087만 같은 환경으로 재시작 PID62985/session63343, health200. migration/seed/reset없음. 사용자는 새로고침 후 확인 가능. 기존 사용자case/사진 수정 없음. 병합·push·공개배포 미실행.
- DB: 새 업무DB/미사용DB 없음. 검증용 emi_qms_e2e_20261013000936_62658_1841 기반 임시DB·container·network 자동정리 완료. 기존 emi_qms_e2e_upg_preview_20261012는 사용자 검수용으로 유지.

### 공용 사진 장당 제한·JPG·표시 보정 · 2026-10-13
사용자 확인 후 수정 승인: 별도 장당5MB 제한 삭제, JPG 형식 지원 보완, 흰색/투명 사진과 PC 상세 이미지 넘침 수정. UPG10장/합계20MB, 오산5장/합계40MiB 및 실제 이미지·보안 검사 계약은 유지.
- 공용 선택검증의 perFile 제한 제거, image/jpg를 image/jpeg로 정규화, 오산/UPG 선택기에 JPG 명시. 서버 제한·preview 요청크기·Base64 상한은 단계 합계와 정합화하며 JPG 별칭을 공용 검증에서 처리.
- 공용 사진 표면에 단색 회색 배경 적용(사진 원본 변화 없음). Osan PC height:auto!important 규칙을 실제 오산 화면에 한정하여 UPG 썸네일 침범 제거. 오산 이력 사진의 밝은 배경도 공용 배경이 보이도록 transparent 적용.
- frontend40개, Osan mock E2E3개, 타입/lint/build 통과(기존bundle경고). 실제 사용자 사진 읽기만 하여 PC1440에서 이미지197/120px가82px 칸에 맞게82px로 표시됨 확인. 모바일390 가로넘침0, 필터none, 사진로딩정상. 공용 오산 이력 합성사진 PC160/mobile150px 칸 일치, 흰색도형 구분 확인; 임시 fixture 삭제. 기존 사용자DB/사진 수정 없음.
- 서버 검증 완료: Debug/Release 빌드, G2비DB2개·공용오산 size/HDR13개·전용Postgres통합1개 통과. 처리된 결과도5MB초과인 유효PNG 저장/조회/유지편집, 유효사진 합계20MB초과시scan전거부, image/jpg JSON저장/multipartpreview 확인. 독립review upg_photo_reuse_review gpt-6-astra/high 요청(실제모델NOT_REPORTED) GO, 미해결P0/P1/P2없음.
- 로컬5188 적용 및 API5087 재기동 PID66934/session39807, health200. 기존DB/사진 유지, migration/seed/reset없음. 원격push/병합/배포 미실행.
- DB: 새 업무DB·미사용DB 없음. 검증용 emi_qms_e2e_20261013003111_66234_2927 및 emi_qms_e2e_20261013003247_66679_2947 기반 DB/container/network 모두 정리. 기존 emi_qms_e2e_upg_preview_20261012는 사용자 검수용 유지.

### 카메라 사진 칸 비율 · 2026-10-13
사용자 요청에 따라 UPG 상세 및 입력/수정 미리보기의 가로로 긴 고정 높이를 4:3 사진 칸으로 변경. 사진 자체는 contain으로 원래 비율을 보존하고 세로사진도 잘리지 않게 표시. 회색 배경 유지, 다른 업무 사진 레이아웃 변경 없음.
- 실제 로컬 PC1440 상세163×122, 모바일390 상세160×120, 수정미리보기142×107 확인. 상세가로넘침0, 원본사진 비율·범위 확인. 수정창은 취소해 실제 저장 없음. 작은CSS보정으로 직접 시각검증/diffcheck 수행. 새DB·임시DB·미사용DB없음. 기존 검수DB유지. 로컬반영, 공개배포미실행.

### 실제 카메라 JPEG 거부 원인 분석·공용 보정 · 2026-10-13 진행
사용자가 지정한 다운로드 폴더 20260824_142209.jpg를 읽기 전용으로 단계별 진단. 원본3,694,835바이트는 일반Magick디코더에서 경고없이4000×3000 JPEG로 판독됨. PMS는 OsanJpegContainer의 Unindexed MP trailer 분기에서 거부: MPF기본사진3,653,325바이트와HDR보조사진41,255바이트 이후 정상SEF255바이트 존재. (1)/(2)복사본은 원본과 동일. 메모리에서SEF구간만 분리한 대조군은 공용전체검사PASS로 인과확인. 원본변경/저장/업로드없음.
- 수정 범위: 파일명/해시/고정255 예외가 아니라 공용JPEG컨테이너에 SEF 구조검증 추가. 헤더/푸터/길이/인덱스/레코드 범위를 검증하고 정상 카메라부가정보를 이미지와 구분; 임의후행바이트·손상·범위침범은 계속거부. 기존 metadata제거 흐름과 실제RGB/HDR/ICC/방향계약 보존. 원본을 테스트fixture나Git에 넣지 않음.
- 형식 근거: AndroidX Media3 SefReader의 공식 공개구현(https://github.com/androidx/media/blob/release/libraries/extractor/src/main/java/androidx/media3/extractor/mp4/SefReader.java), SEFT푸터/SEFH헤더/역방향인덱스 구조 참고. 해당구현은MP4용이며JPEG에대한적용/검증은PMS가소유.
- 원격반영·배포 제외. 기존 사용자previewDB미접촉. 검증/완료결과추가기록 예정.

- 완료: `OsanSamsungSefTrailer` 공용 구조 검사 추가 및 JPEG/MPF 연결. 범위·중복·겹침·전체 coverage를 먼저 검사하고 레코드 이름을 처리한다. 정상 SEF만 기존 정제 단계에서 제거하며 임의 후행 데이터는 계속 거부한다. 단일 JPEG 정제 입력도 검증된 이미지 경계로 제한했다.
- 검증: Release build 경고/오류 0, 관련 회귀 23/23 통과. 독립 review GO(P0/P1/P2 없음). 담당자와 부모가 실제 원본을 자르지 않고 독립 진단하여 공용 validator PASS, 2개 프레임 및 ICC/XMP/HDR 유지 확인. 원본 SHA 대조 동일. 실제 원본은 Git/fixture/DB에 저장하지 않았다.
- 로컬 반영: 기존 API 5087만 refresh, health/ready 200. 사용자 preview DB 초기화·시딩·migration 없음. UI 5188 유지. 사용자 실제 저장 재시도는 미실행이며 원격 push/병합/배포 없음.
- DB: 신규 DB 없음, 검증용 임시 DB 없음(정리 대상 없음), 더 이상 사용하지 않는 DB 없음.

### 사진 영역 플로우 격자형 적용 · 2026-10-13
사용자가 선택한 1번 플로우 이미지 묶음 디자인을 UPG 상세·입력 사진 영역에 적용. PC 3열/모바일 2열, 간격 4px, 카드 외곽 padding/border 제거, 4:3 및 contain 유지, 밝은 중립 바탕과 최소 경계 적용. 공용 업로드·조회 동작과 오산 스타일은 변경 없음. 기존 flex 강제 규칙을 제거하여 격자와 충돌하지 않도록 함.
- 레퍼런스: 플로우 공식 실제 채팅 이미지 묶음 화면 https://support.flow.team/ko/flow/4409761935513 (버전 미표기). 사진 밀집 배치를 따르고 PMS의 4:3 전체 보기·단계별 사진 구분·PC3/모바일2열로 조정. 사용성 보조: IBM Carbon https://v10.carbondesignsystem.com/components/file-uploader/usage/ 의 명시적 업로드/제거 조작 유지.
- 검증: 실제 상세 PC1280/모바일390 화면 확인, 합성 5장·흰 배경·세로 사진 및 입력 미리보기 검증. 모바일 grid 2열, clientWidth=scrollWidth로 사진 영역 가로 넘침 없음. 임시 합성 파일 제거 완료. CSS 변경으로 별도 기능 테스트/전체회귀는 실행하지 않음. 로컬 HMR 반영, 공개배포 없음.
- 신규 DB/임시 DB/사용 중단 DB 모두 없음. 기존 사용자 데이터 수정 없음.
