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

### 사진 칸 여백 제거 · 2026-10-13
사용자 요청에 따라 UPG 상세/입력 썸네일의 회색 바탕·내부 경계를 제거하고 동일한 4:3 칸을 cover로 채움. 이전 contain 요구를 이번 요청으로 대체하며 비율 차이는 썸네일 중앙 잘림으로 처리. 확대 사진의 contain과 원본/저장 처리는 유지. 실제 모바일 상세에서 두 사진 모두163×122, cover 및 투명 바탕 확인. CSS diff 검사 통과. 로컬 반영, 공개 배포 없음. 신규/임시/사용중단 DB 모두 없음.

### 고정 칸 전체 사진 표시 확정 · 2026-10-13
사용자가 썸네일 비율 변형을 허용하고 고정 가로·세로 칸에 전체 사진 표시를 명시 승인. UPG 상세 썸네일 및 입력 미리보기의 cover를 fill로 변경. 원본 파일과 확대보기 contain은 유지. 실제 모바일 사진2개163×122/fill, 클릭 확대 contain 및 원본 전체 표시를 시각 확인. CSS diff 검사 통과. 로컬 반영/커밋, 공개배포 없음. 신규·임시·사용중단 DB 모두 없음.

### MES 사내 노트북 중계 · 연결 진단 1단계 · 2026-10-15
- 사용자 승인: 사내망 접속 가능 노트북 상시 운영 중계 방식 진행, Windows 관리자 권한 가능 확인. 먼저 실제 연결부터 확인하며 테이블 구조 분석은 보류. 운영 배포·네트워크 변경·MES 쓰기는 이번 범위 아님.
- 계약: MES 적합과 PMS 부적합을 연계하려는 후속 목표. 먼저 노트북용 1회 진단 도구를 준비한다. MES TCP/읽기 전용 SELECT 1 로그인, PMS HTTPS health 응답을 분리 표시. 전용 에이전트 인증·자동 실행·동기화는 후속 미구현이며 health 성공을 인증 성공으로 취급하지 않는다.
- source: codex/unified-osan-shell HEAD1531aa2. 기존 사용자 preview DB 및 frontend/runtime 보존. scripts/mes-connection-check 추가, Npgsql10.0.3 재사용. 비밀번호는 노트북 콘솔에서 숨김 입력, 파일/원문 오류/프로세스 인자로 남기지 않음. 실제 대상 설정 파일은 Git 제외.
- 기본 MES TLS Require(인증서 검증 없음), 명시 VerifyFull/Prefer 선택. 암호화 실패시 자동 평문 fallback 없음. Prefer는 사용자 직접 선택 시에만 사용하며 실제 암호화 협상 미확인을 표시. 운영 TLS 정책은 별도 확인.
- 검증·독립검토·패키지 결과는 아래에 기록. 실제 Windows 노트북 실행 및 MES/PMS 연결은 사용자 확인 대기. 신규DB/임시DB/사용중단DB 없음.
- 완료: Windows x64 self-contained 패키지 빌드, HTTP/설정 합성14개 통과. 임시 PostgreSQL17 `emi_mes_connect_synthetic`에서 정상로그인 SELECT1 성공 및 잘못된 비밀번호28P01 실패 실제 실행. 실제 공개 frontend health 응답 status=healthy 확인 후 판정 보정, backend name=live/status=ok도 허용. 인증HTML/401/302/500/일반status=ok 거부.
- 독립검토 mes_connection_review 요청gpt-6-astra/high(실제모델NOT_REPORTED) GO, 남은P0/P1/P2없음. 패키지ZIP 무결성검사 통과. Windows 실행과 실제MES 접속은 미확인. 결과파일엔 고정진단/SQLSTATE/검사시각만 저장.
- DB: 새 영구DB/사용중단DB 없음. 검증용 `emi_mes_connect_synthetic`은 소유컨테이너 `emi-mes-connect-check-20261015`의 tmpfs에서만 사용, --rm 종료로 정리 완료. 사용자previewDB 및 운영DB 미변경. 공개배포/원격반영 없음. 후속은 사용자 노트북에서 패키지 실행 결과 확인, 에이전트 인증 설계·자동시작·실제동기화 구현.

### TPM 기기·회사망 인증 시험 전송 · 2026-10-15 진행
- 승인: 사용자가 구현 시작 지시. TPM 사용준비 확인, 회사 공인IP는 대화에서 제공(실제값은 배포설정/패키지 로컬파일만). IP고정 가정, 차후 명시 관리설정으로 IP제한만 해제 가능. 자동해제 없음. 실제 운영변경/공개배포 승인은 아직 없음.
- 범위: Windows TPM 비내보내기 machine RSA2048키 생성·서명 검증·공개키 등록파일, 서명된 고정 시험메시지 POST, 서버 장비공개키/IP/신뢰proxy/시간 검사, 청주 nonce 테이블 원자적 소비 후 해시대조 응답. 개인키 업로드/소프트웨어키 fallback/MES조회·쓰기 없음.
- 구현: 신규 인증 scheme과 단일 /integrations/mes/probe 경로, 기본 비활성. 직원 인증/캠퍼스 경로 변경 없이 exact EasyAuth예외만 opt-in, nginx는 신뢰FrontDoor 경유확인 후 proxytoken/IP헤더 덮어쓰기. 활성화 전 별도review 및 승인된 배포 필요.
- 등록 경계: 관리자가 지정 노트북에서 제공 프로그램을 실행하고 생성한 공개키만 승인 등록. 공개키 파일만으로 TPM 출처를 원격 attestation했다고 주장하지 않음. machine키 최초 생성은 관리자 실행, 운영서비스권한/자동실행은 후속.
- 신규 영구DB 없음. 청주에 0136 mes_probe_receipts 테이블 추가 계획, 운영/사용자preview migration 미적용. 서버 replica간/재시작 후 재전송 방지. 유효요청 시 만료행 정리하며 idle때 남는 만료행은 다음유효요청에 정리.
- 로컬 구현 완료: backend Release/Windows x64 self-contained 빌드 성공. 인증·권한·audit 회귀 24개 및 전용 disposable DB 검증 1개 통과(8개 병렬 요청 중 1개만 소비, 새 연결에서도 replay 거부, 오산 테이블 없음). nginx 실제 syntax 검사 통과. Azure 산출물 static/4개 Bicep compile·JSON 일치 검증 통과. 기존 복구검사는 Mac LibreSSL에서 실패하여 설치된 OpenSSL3 PATH로 검증했고 통과, 해당 업무 코드는 변경하지 않음.
- 독립검토 mes_tpm_review 요청gpt-6-astra/high(실제모델 NOT_REPORTED) GO, 미해결 P0/P1/P2 없음. 수동 등록과 원격 TPM attestation 미제공 경계 확인. README 및 Windows ZIP(34.0MiB) 작성·무결성 확인. 실제 Windows TPM 실행/공개경로 시험은 미완료.
- 다음: 지정 노트북에서 관리자 실행→1번→device-registration.json 전달. 공개키와 회사 IP 배포 입력 등록 및 운영 활성화는 별도 승인 후 수행. 2번 시험은 활성화 이후 진행. MES 계정·조회·쓰기·자동 서비스는 이번 단계 범위 밖. 원격반영/공개배포 없음.
- DB 보고: 신규 영구 DB 및 사용중단 DB 없음. 청주 신규 테이블 migration만 작성, 운영/preview DB 미적용. 검증 harness DB emi_qms_e2e_20261015000516_16984_31727 및 내부 Directory/Cheongju/Osan 합성 fixture는 전용 tmpfs container emi-qms-e2e-20261015000516-16984-31727-e2e-postgres-1 종료·삭제와 함께 정리 완료.

### 전체 병합·공개배포 승인 · 2026-10-15
- 사용자: 전체 포함 범위 정리 확인 후 “좋아. 모두 확정. 전체 원격 메인에 병합하고 공개배포해.” UPG/생산연계·초기재고/사진공통/홈7일전망/모바일/자재 클릭/MES TPM+회사IP 시험전송을 함께 승인. 실제 MES 데이터 동기화·쓰기 및 상시 서비스는 제외.
- 등록 노트북 공개키 파일 검증 완료(RSA2048, SPKI SHA256과 DeviceId 일치), 실제값은 private 배포 입력으로만 사용. 회사 IP도 사용자 제공값을 private 설정으로 등록. 실제 공개 경로 성공은 배포 후 사용자 2번 실행으로 확인한다.
- 최종 기준: origin/main 1443316991a97c57890e560e64b6c5aa005da988 대비 e9a36e4 및 이 승인 기록. 별도 작업 checkout WIP/로컬 검수 파일/검수 DB는 제외·보존. 최종 required CI 및 통합 독립 검토 후 정상 release runner 사용. 공지·팝업 준비 후 실제 교체 전 저장 차단·백업 검증·migration135/136, 검증 후 저장 재개.
- 주의: 0135 도입일부터 부적합 재고는 개별 미완료 건수다. 기존 미완료품은 배포 후 초기재고로 개별 등록해야 한다. 과거 숫자 기록은 보존하며 로컬 검수 데이터를 운영으로 복사하지 않는다.
- 운영 준비: 확정 양식 공지·별도 안내 팝업을 청주·오산에 게시, prepare-only ANNOUNCED 성공. 저장 차단/업무 DB migration/앱 교체는 아직 없음. 최종 PR172 개설.
- 통합 독립 g2_full_release_review 요청gpt-6-astra/high(관측NOT_REPORTED) 누적1443316..e9a36e4 GO. 지정기기 공개키/IP 등록·활성화 승인 사실 확인 후 활성화 배포+사용자2번시험 순서 GO. 초기재고 등록 전0 표시 및 기존자료보존 안내 전달.
- 최초 CI38660504897는 기존 전이 Pkcs/Xml10.0.10의 10월13일 공개 취약점으로 restore 차단. 공식GHSA-846f-f764-76mx/GHSA-r955-8q5j-3h83 수정버전10.0.13 직접참조로 최소보정. audit/WarningsAsErrors 유지, restore/build성공 및 독립GO. 보안 경고 우회 없음.
- 신규 청주 객체의 소유권 기대목록 누락 보정: 품질3table+MES1table/view/sequence, packaged 검증에는 이전 G2workspace4table도 명시 추가. 실제 catalog를 expected로 사용하지 않음, 오산/Directory 불변. disposable 실제 BusinessSchemaSeparation_3/3 통과 및 독립GO. 최초 파일명필터는0개여서 성공으로 취급하지 않고 실제partial메서드필터로 바로잡음. 소유 임시DB emi_qms_e2e_20261015003738_61180_20993, emi_qms_e2e_20261015003842_61509_19311 및3DB fixture/컨테이너/네트워크정리완료.
- 패키지 추가검증: Linux 실제 production image에서 기존 aggregate table/권한 수 기준이0131에 머물러 실패. 승인0132–0136에 맞춰 청주table190,permission39,rolepermission123으로 보정(오산불변), 독립reviewGO. 실제image fresh/upgrade3DB·schema·drain·잘못된설정거부 모두통과. 임시DB emi_qms_e2e_20261015004316_62430_4697(실패) 및 emi_qms_e2e_20261015004537_63157_22657(통과), 내부fixture/container/network 모두정리.
- CI38661289467 frontend672/674통과·자재팝업2개10초초과. 신규품목전체클릭영역이있는92행 전체button조회 대신 within(dialog)로 대상범위 제한, 단언·92행·timeout변경없음. 별도source환경에서자재26/26통과, 독립reviewGO. 기존검수node_modules심볼릭링크보존. 변경후 최종CI대기.

- CI38662068866 frontend674/674·build통과, mockUI72/73에서 폐기된 회색 기대값 실패. 소스의 주말색과 일치하도록 기대값2줄보정, 해당Playwright1/1통과. fullstack구형숫자입력 fixture를 품질건등록/조치집계로갱신하고, 기존빈값·0·버전·부서권한·미래날짜검증유지. 품목전체7셀클릭에맞춰92행/NO92/행7버튼검증. 독립review각diff GO, 최종로컬fullstack1/1통과(19.1초), lint/typecheck/diffcheck통과. 전용임시DB emi_qms_e2e_20261015005940_67019_13865(빌드미준비), emi_qms_e2e_20261015010014_67258_23202(기대값실패), emi_qms_e2e_20261015010253_68209_3194(통과) 및소유container/network모두정리. 제품코드변경·검증생략·timeout완화없음. 최종requiredCI재실행.

- CI38663545025 frontend전체·workflow통과, fullstack62/64통과. 잔여2건: UL891 간트선의폐기된회색override기대값을기존업무CSS(#777/#c7c7c7단색)와정합화, targeted1/1통과. 공통사이드바compact36/32px가touch44px를덮던실제결함은 shared-shell+touch-optimized조건의메뉴버튼만44px로복원(일반PC·업무표·모달불변). 터치단언44유지하고실패요소진단보강. 전체mobile-first2/2통과, PC1440/터치1024실제스크린샷직접확인, lint/diff통과, 독립review3파일GO. 임시DB emi_qms_e2e_20261015012951_73047_19844, emi_qms_e2e_20261015013012_73379_32758, emi_qms_e2e_20261015013051_73708_3335 및container/network정리완료. 필수CI최종후보재실행.

- 최종 backend CI에서 품질전환 전 숫자입력 테스트11건과 Azure 선택형MES role수 기대1건을 확인. 과거값은 SQL legacy fixture로 유지하고 전체/부분/하루 재고조회·버전·데이터보존을 검증, 폐지된쓰기 거부·혼합요청원자성·감사미생성으로 계약정합화. G2 migration12/12통과(18.5초), 독립review P2 부분/하루 단언복원 후1/1통과(3.68초) 및최종GO. Azure resourceCount는 선택형MES role포함27로수정하고default-off/secret-scoped권한단언추가, Bicep재컴파일·static검사통과. 관련G2+Azure76/76통과(56초), Azure3파일독립GO. 신규제품동작·권한확대·검증생략없음. 전용임시DB emi_qms_e2e_20261015013516_74840_31793, emi_qms_e2e_20261015013832_3729_9218, emi_qms_e2e_20261015014017_13885_1416 및소유container/network정리완료. 신규영구DB/사용중단DB없음. 최종requiredCI재실행대기.

### 원격 main 병합·첫 배포 중단 및 원상복구 · 2026-10-15
- 최종 PR172 HEAD5563700 required CI38667187990 전체 성공: backend1169/1169, frontend674/674, mockUI73/73, fullstack64+business2+Osan1, workflow/CI gate. 사용자 검수·전체병합승인 범위로 PR172 병합, origin/main b1b5b4edf37db485dfe3305bfee9db708196d6f5, main CI38673244266 성공.
- 정확한 main archive에서 Linuxamd64 backend/frontend image 생성·immutable digest/source label/nonroot/SQL130+4+6+3/정적34파일22asset HTTP 검증. private Docker config의 buildx 탐색 누락으로 최초 build 시작 전 실패했으며 plugin 경로 보정 후 성공. 제품 이미지 권한 변경 없음.
- 공지 중복 게시 없이 새 점검창을 준비하고 정상 release runner 실행. 사전 논리백업에서 OSAN pg_dump가 30분 고정 제한에 걸려 PG_DUMP_FAILED/release79로 중단. 전체90분 설정과 개별30분 제한이 충돌한 원인 확인. 업무 migration/이미지 교체 전 차단됐으며 신규135/136 미적용.
- baseline1443316991a97c57890e560e64b6c5aa005da988 앱 복구 후 Azure revision Ready/Healthy와 공개health200/보호경로401 확인. 읽기전용 3DB identity/정확한 기존 migration ledger(Directory4·청주134·오산133)/Directory읽기전용권한/청주품목92/신규4table없음 확인. 독립검토한 same-release completion 실행으로 청주·오산 저장제한 해제, DB Completed 상태 재확인. 현재 공개 서비스는 기존 버전으로 정상 운영 중이며 신규 기능 배포 완료가 아님.
- 재시도 보정: data dump/restore가 설정된 verificationTimeoutSeconds를 사용하되 남은 전체 deadline으로 제한. 독립review GO, 테스트23통과/opt-in2skip. 추가 용량 확인: 3DB6283424837bytes, 기존6GiB PGDATA tmpfs 잔여약152MiB로 WAL 여유 부족; Docker 가용 약6.25GiB. 기본6GiB를 유지하는 bounded configurable tmpfs 보정 진행.
- 로컬 기존5개 preview DB 모두 tmpfs이므로 Docker 재시작 시 데이터 유실 위험 확인. 공유환경 메모리 증설·재시작은 별도 사용자 승인과 검수 데이터 백업·복원 준비 전까지 실행하지 않음. 현재 preview/DB/컨테이너 보존. 첫 시도 소유 TLS 중계 종료. 근거는 private /tmp/g2-quality-release-20261015 (secret/실데이터 Git 제외).
- 신규 영구DB 및 사용중단DB 없음. 이번 실패 시 운영 신규table 없음. 공개 배포·MES 활성화·Windows 2번 시험은 미완료.
- 백업 보정 완료: optional restoreDataSizeGiB 기본6/정수6–32, PGDATA tmpfs에만 적용. 시간·크기·잘못된입력·networknone/소유권/cleanup 검증24통과(opt-in2skip), py_compile/diffcheck 통과. 구현과 분리된 g2_full_release_review 최종 GO(운영재시작승인과 별개). Docker/운영DB 변경 없이 보정 PR 및 CI 진행.
- 백업 보정 PR173 required CI38678524997 성공(WorkflowValidation5m14s/CI Gate; 스크립트 범위로 backend/frontend/fullstack은 분류상 제외, 기능 전체검증은 앞선 PR172 근거 유지). PR173 원격main95ed993055570576355de66ab70acecce5f261ba로 병합 완료. 공개 재배포는 Docker 공유환경 변경 답변 대기. 기존5개tmpfs 검수DB 모두 보존 중, 기존 공개 버전health200/API401/점검Completed. 실패 백업 평문 작업디렉터리0·소유 복원컨테이너0·TLS중계종료 확인. 신규DB/사용중단DB없음.
- 사용자 공유환경 승인 수신: Docker 메모리를 일시12GiB로 늘려 진행. tmpfs 검수DB 백업·실제격리복원 확인 후 재시작하고 배포 후 원래 메모리설정으로 복귀한다. 5개 검수컨테이너 모두 명시Docker socket에서존재확인. main95ed993 CI38679068213 성공 및새image source/nonroot·frontend34/22·SQL130/4/6/3검증통과. 재시도는 /tmp/g2-quality-release-20261015-retry 별도시도·새release ID로 실행예정.
- 재시도 디스크 사전확인22GiB여유: 기존5벌 대형중간파일 동시보관 위험을 확인해 bundle hash보존→암호화후bundle삭제→복호화hash일치후원본임시dump삭제→추출후복호화tar삭제로 보정. sourceDB/암호화왕복/실제복원/최종sourceproof/게시gate유지. 대형사본최대3벌로감소(작은메타데이터별도). 복원내용·변조실패미게시 검증포함24통과2opt-in제외, 독립reviewGO. 검수DB5개암호화백업완료; 격리복원검증중PG17 CHECK식괄호재표현차이를확인해기존canonical schema방식으로진단중. 원본DB/Docker재시작아직없음.

- 복구 디스크 보정 PR174 required CI38681575805·main CI38682024095 통과, 원격main c58781aa027ade6ef4c4dc994c8232d674141606 병합. 기능 전체 검증은 PR172 결과 유지, 스크립트 범위 CI는 workflow/CI gate로 분류. 정확한 main source 2,443파일 hash 및 immutable backend/frontend 이미지 source/nonroot/SQL130·4·6·3/정적34파일22asset 검사 통과.
- 승인된 Docker 12GiB 변경 수행: 기존5개 검수DB 전체 암호화백업과 별도 격리 실제복원(전체schema·row/count·sequence·largeobject·globals) 검증 후 재시작. 실제Docker 메모리12528664576bytes 확인. 동일컨테이너의 동적loopback port 변경 때문에 원본복원을 쓰기 전에 정지했고, exactID/image/env/설정 보존하에 실제포트 기록만 허용하는 보정을 독립GO 후 적용. 원래5개검수DB 복원 및 데이터·권한 검증 모두통과. API5087/5188은 재시작 전부터 미실행. 배포후 fresh백업/복원검증을 다시 거쳐 원래기본8GiB로 복귀할 예정.
- final 재시도는 점검활성화에서 중단(release79): OSAN exclusive advisory lock 대기 중 Npgsql읽기timeout, 업무migration/앱교체/운영백업 시작 전. 두사업장 Failed정리 성공. 읽기검증으로 기존앱1443316·3DB정확ledger·신규table부재·공개200/401·남은잠금0 확인 후 같은release complete 수행, 두사업장 Completed·저장재개 사후검증 성공. 강제잠금해제·데이터변경 없음. 독립reviewGO 보조절차와 근거는 private final 디렉터리에 보존.
- 다음 attempt3는 같은 c58781aa 소스/image로 새release ID 사용, 공지중복게시 없음. final 복구성공 증명 및 정확한3DB집합을 검증한 후 정상release runner 재진행. MES활성화와 공개배포 완료는 아직 미확정. 신규영구DB/사용중단DB없음.

- attempt3는 점검활성화·요청종료를 통과했으나 OSAN dump약3.8GiB에서 로컬Docker 엔진이 디스크부족으로종료되어 백업gate실패/release79. 실제Docker진단은 init.log write의 no space left, 상위failureCode는 종료된engine의 OWNED_CONTAINER_CLEANUP_FAILED. 업무migration/앱교체전중단. default json-file에 바이너리stdout이 중복기록될 수있는 경로확인; 직접로그파일크기증거없으므로 디스크고갈전체원인 단정안함. task-owned일회성container에만 --log-driver none 최소보정, 독립reviewGO, unittest26개중24pass/2환경전용skip. 실제engine 합성1MiB binary stdout SHA일치 및 LogConfig.Type=none 확인, 소유probe정리.
- Docker 일반restart는 멈춘앱종료timeout. 이미engine중단상태에서 공식stop --force/start로 복구, 디스크24GiB여유·12GiB설정확인. 실패backup소유container는 auto-remove로없음. 기존암호화검수백업과검증proof를새root로보존복사하여 동일5container의빈cluster조건확인후 재복원, 전체내용검증5개통과. 기존기록보존. attempt3 same-release저장재개는실제실패checkpoint와결합한읽기검증후진행중. 신규영구DB/사용중단DB없음.

### 전체 공개배포 완료 · 2026-10-15
- attempt3의 기존 버전/3DB 읽기 검증 및 same-release completion 성공, 청주·오산 저장제한 해제 확인 후 재시도. 원격 PR175(log-driver none) required CI38688515406 및 main CI38688971347 성공, main498fceeec44ab3f4fd1eddd21ca0f3e771ee47a5 병합. 기능 전체회귀는 PR172 최종 근거 유지. 작업별 임시 컨테이너의 바이너리 stdout 중복 로그만 제거하며 전역 Docker 로그 정책은 변경하지 않음.
- attempt4는 정확한 main498fcee archive/image로 정상 release runner 실행. 공지 중복 게시 없이 새 점검창, 요청 종료, 암호화 왕복·격리 실제복원·원본 전체 대조·최종 drain을 통과하여 recovery phase verified. 운영 DB migration, backend/frontend 이미지 교체, 공개 보안 검사, 청주·오산 maintenance complete 모두 성공(release exit0). bootstrap/catalog provisioning/membership backfill은 실행하지 않음.
- 배포 후 읽기 전용 사후검증: Directory4/청주136/오산133 migration ledger가 해당 source와 정확히 일치. 청주 품목92 유지, 신규 품질3table와 MES probe1table는 청주에만 존재. 사업부 identity·runtime connect/schema·Directory 읽기전용·두사업장 이번release Completed 확인. 공개 health200, 비인증 root/API401 확인.
- 승인된 MES TPM 공개키+회사 IP 제한의 시험 전송 기능 활성화. 단일 Key Vault secret 접근 범위와 backend internal ingress 유지, 정확한 /integrations/mes/probe 경로만 기존 인증 예외 목록에 추가하고 다른 설정 불변 확인. backend Ready, 등록 공개키/IP 필수 설정 검증. 활성화 후 health200/root401/api401/서명 없는 probe POST401 통과. 실제 Windows 노트북 2번 시험은 사용자 실행 대기이며 실제 MES DB 조회·수정·동기화·상시 서비스는 이번 구현 범위 밖.
- 로컬 정리: 배포용 TLS relay 정상 종료. 최신 검수DB5개를 다시 암호화백업→격리 실제복원→원본 동일성 확인한 뒤 Docker MemoryMiB 임시12288을 원래 키 부재/기본8GiB로 원복. 동일한 원본5개 컨테이너의 새 빈 클러스터에 최신 데이터 복원 및 전체내용·권한 검증5/5 성공. 동적포트는 private pre-8g-cycle/restore-result.json에 기록. 사용자 검수파일/node_modules 링크 및 다른 checkout WIP 보존.
- 근거: private /tmp/g2-quality-release-20261015-attempt4(release-exit, postdeploy-database-check, mes-activation-result, public-post-mes-check) 및 /tmp/g2-preview-preservation-20261015/pre-8g-cycle. secret/실데이터/백업은 Git에 포함하지 않음. 배포 데이터 암호화 백업은 기존 PMS-Recovery 경로에 보존하고 평문 임시 logical-recovery 디렉터리는0 확인.
- DB 보고: 새 영구DB 없음. 기존 청주DB에 g2_nonconformances, g2_nonconformance_history, g2_quality_settings, mes_probe_receipts 추가. 소유 격리 검증DB/컨테이너는 정리, 기존5개 preview DB는 보존·복원. 더 이상 사용하지 않는 DB 없음. 남은 사용자 업무는 등록 노트북 2번 전송 시험 및 기존 미완료 제품의 개별 초기재고 등록. 로컬 검수 데이터는 운영으로 복사하지 않음.

### 사용자 승인 저장물 정리 · 2026-10-15
- 사용자 최신 운영 DB 암호화백업 외 잉여물 삭제 승인. 추가로 현재 검수DB5개 및 과거 Busbar·Change008·공급업체·LQC·UL891·패널·접근권한·오산 등 PMS 검수DB/전용볼륨 삭제를 명시 승인.
- 이전 운영 백업4세트16.04GiB 삭제. 최신 dfd45793-e908-4b10-85e3-928b55b96e89 logical-backup.cms/manifest, 기존복호화키쌍, latest-backup-verification 근거 보존. 10월7일 오산복구자료와10월12일 배포임시4폴더4.90GiB, 이번배포/검수백업/시험전송 임시7폴더2.29GiB 삭제. G2 검증로그/스크린샷92개와브라우저/호환검증/레퍼런스/cache/Playwright폴더5개삭제.
- 승인된 검수 컨테이너24개와 독점연결volume6개 삭제. 일반개발 emi-qms-postgres 및 Valhalla프로젝트 보존. PMS 로컬이미지62개 참조제거, PMS경로가명시된빌드cache30ID 선택정리(잔여의존성때문에전체삭제아님). 출처불명volume/공용baseimage/cache는보존. 전체prune없음. 공개health200확인,운영DB변경없음.
- 최초 광범위패턴파일삭제와과거DB일괄삭제는자동승인검토거절. 우회하지않고정확한파일·소유목록검증으로범위축소,과거DB는사용자추가명시승인후삭제. 시안·실험스크립트는별도답변대기. 신규DB없음. 삭제DB는local검수전용이며운영DB보존.

### 단순 / 복합·장기 구분 복원 · 2026-10-15
- 사용자 입력항목 재확인 후 구현 승인. 기존 초기재고 등록 유지(발생실적 제외), 오늘 신규건은 일반등록. 1차에 nonconformanceType(simple/complexLong)를 추가하고 조치완료/조치삭제는 그 유형을 유지. 기존 분류없는 개별건은 단순으로 읽어 초기 승계정책을 유지하며 수정 가능.
- 기존 PC/모바일 입력·검색·목록·상세를 재사용. 유형필터가 미완료/수리완료 건수에도 적용되므로 유형별 재고·수리를 확인. 생산출하 합계와 기존 권한/이력/사진/초기재고 계약 보존. 발생책임별 비율·MES 실제연동·새 요약UI는 이번범위외.
- 검증: 서버 입력거부·호환·조치/초기재고 계약, 프런트 두등록경로·필터·수정, PC/모바일 실제화면 및 독립review. 운영DB/공개배포 없음.

- 구현완료: 저장JSON/API에 nonconformanceType 추가, simple/complexLong만 허용. 분류없는 과거 저장JSON은simple로조회. 현재쓰기요청의분류누락/null/미지원값은400으로거부(새로고침안내); 구형화면수정이complexLong을simple로덮는독립review P2를해소. 실제HTTP생략PUT거부후분류/version불변검증. 조치등록/삭제에서분류유지, 수정시초기재고표식보존.
- 기존 PC/모바일 선택칸·필터·표·상세재사용. 초기재고등록에는발생실적제외안내추가. 기존발생책임필터와새분류필터는독립적이며검색버튼시적용,양쪽상태탭건수에동일적용. 새요약·수량직접입력없음. frontend fullstack 공유fixture에필수simple추가(이번전체fullstack재실행없음).
- 검증: backend G2NonconformanceTests 최종3/3(격리HTTP/DB·기존권한/초기재고/사진/동시성포함), frontend10/10, typecheck/build/변경파일eslint/diffcheck통과. 기존큰chunk build경고는남아있으며오류없음. 합성UI 실제브라우저PC1440 및390/320px 등록·상세·목록·모바일검색확인,긴복합장기표시확인,입력창가로넘침0/pageerror0. 스크린샷 /tmp/g2-classification-*.png 직접확인. UI검증서버는합성메모리자료이며실제저장검수아님.
- 독립review g2_full_release_review 기존검토자재사용,기준a7a3d13부터실제diff재검토. 구형클라이언트덮어쓰기P2보정후GO,잔여P0~P2없음. 구현위임/검토 actual모델NOT_REPORTED. 공개배포/원격병합미실행,운영DB변경없음.
- DB: 신규영구DB/테이블/migration없음. 기존청주jsonb필드추가방식. 최종검증DB emi_qms_e2e_20261015085606_4127_25999 및소유container/network정리완료. 더이상사용하지않는DB없음.


### 단순 / 복합·장기 구분 공개배포 완료 · 2026-10-16
- 사용자 로컬 확인 후 이번 변경의 병합·공개배포 승인, 공지사항 게시 제외 명시. 기존 저장제한·정상검증 절차는 유지하고 MAINTENANCE_PUBLISH_NOTICE=false 적용. DB migration/백업·복원 gate는 코드전용 범위라 실행하지 않음.
- PR176 d644f95의 CI38826190177 성공: backend1169/1169, frontend677/677, mockUI73/73, fullstack64+사업부2+오산1, workflow/CI gate. 원격main092e4946c6b09349e2fea521d64699bfef94b028 병합 및 main CI38832173582 성공(검증된 PR 재사용, 전체 중복실행 없음).
- 정확한 main archive의 Linuxamd64 이미지 생성. 앞서 검사한 후보와 최종 이미지 RootFS·실행설정 동일성 확인(소스 revision label만 변경), backend packaged fresh/upgrade·사업부DB경계 통과, frontend 비관리자101에서34개 파일/22asset HTTP 통과. 로컬 초기검사는 DOCKER_DEFAULT_PLATFORM이 Mac용postgres이미지와 충돌해 실패; 전역플랫폼 강제만 제거 후 통과. 제품 변경·검증 완화 없음.
- 정상 release runner 2026-10-16 09:55:37~10:02:21 KST exit0(6분44초). backend0000075/frontend0000065 Healthy·정확digest 확인, 공개health200/root401/api401. 청주·오산 prepare/activate/complete 6개job 모두성공, 저장제한해제. 공지 게시 없음. 120회 약3초간격 외부3경로 점검에서 비정상응답0(샘플 사이 순간중단·인증후 업무화면 전부를 무중단으로 보장하는 증거는 아님).
- 기존 MES 기기+회사IP제한, backend 내부ingress 및 startup migration=false 보존 확인. 실제 운영 데이터 등록·수정 테스트 없음. 근거 /tmp/g2-classification-release-20261016 의 CI·image·release·postdeploy·availability 기록(실데이터/secret Git제외), 임시registry로그인정리.
- DB 보고: 신규 영구DB/테이블/migration없음. 이번 로컬이미지검증의 전용 임시DB·컨테이너·네트워크·볼륨 모두 자동정리 확인. 기존 DB 사용중단없음. 사용자 preview·시안파일 보존. 이번 원격 기능반영·공개배포 완료; 이 완료기록은 후속 local commit으로 보존.


### 부적합 재고 유형별 표시 · 2026-10-16
- 사용자 분류 후 재고도 둘로 표시 요청/수정 승인. 기존 생산출하 PC 상단/일자 상세·모바일 재고표와 UPG 탭 인접 안내영역 재사용. 신규 시안·쓰기·권한·실적 합계 변경 없음.
- 일별 API에 nullable simpleDefectInventory/complexLongDefectInventory 추가. 품질 활성화 이후 발생일 이하·완료일 초과/미완료 조건으로 각각 집계, 기존 합계는 둘의 합 유지. 분류없는 과거 개별건은 단순, 활성화전 숫자재고는 단순/복합0, 미래는 모두null. 초기재고 포함·발생실적 제외 계약 유지.
- 생산출하 재고표는 두 유형을 각각 표시. 과거 합계 실사값은 임의 분할하지 않고 보존하며 모바일 병합칸에 과거 합계라고 명시. UPG 현재 전체 재고는 합계/단순/복합·장기를 함께 표시하고 검색 필터와 독립. 탭 숫자는 기존 검색조건 기준이며 loading/error때 재고0으로 오표시하지 않음.
- 검증: isolated G2NonconformanceTests3/3(발생일/완료일·초기재고·조치삭제·legacy·합계·미래), frontend 관련23/23, typecheck/eslint/build/diffcheck 통과. 기존chunk크기 경고만 유지. PC1440와 모바일390/320 합성 브라우저 실제 화면 직접확인, 입력 아닌 목록·일자상세 재고표 가로넘침없음/pageerror0. 증거 /tmp/g2-split-*; local preview는 합성 메모리자료이며 운영 실데이터 검수 아님.
- 독립 split_inventory_review 코드/테스트diff GO, P0~P2없음. 로컬 구현·검증 완료, 이번 표시 변경의 원격병합/공개배포는 미실행. 기존 5188 검수서버 소스 /tmp/g2-classification-validation/frontend에 반영.
- DB: 신규영구DB/테이블/migration없음. 임시DB emi_qms_e2e_20261016011225_36553_549 및 소유container/network 삭제확인. 사용중단DB없음. 사용자파일·다른runtime 보존.


### 생산출하 상세 표 재배치 · 2026-10-16
- 사용자 확정: 왼쪽 생산·수리 실적 유지, 가운데 G2 재고 실사/신규 부적합 수량, 오른쪽 단순/복합·장기 부적합 재고. 폐지된 부적합 실사 표시 제거.
- 기존 표/스타일 재사용, 모바일에서도 부적합 실사칸 제거하고 납품가능 재고·실사와 부적합 재고를 구분. PC/모바일 부적합재고 rows 공용화. API·DB·집계·과거 저장값은 변경하지 않음.
- 관련 프런트12/12 및 typecheck/eslint/diffcheck 통과. 최초 테스트는 기존 표 접근성 제목 기대값 불일치로1실패하여 변경제목으로 정합화 후통과. PC1440·모바일390/320 높이844 직접시각확인, popup 가로넘침없음/pageerror0. 로컬5188 합성검수 반영, 원격병합/공개배포없음. 신규DB/임시DB/사용중단DB없음.

- 상세 재고표 색 채우기(2026-10-16): 사용자 요청으로 오른쪽 부적합재고 데이터셀을 #f3f4f5로 채움, 모바일 동일 적용. 기존 헤더·테두리·타표 보존. 실제PC computed rgb(243,244,245) 및 PC/390/320 화면 확인/pageerror0. 단순스타일변경으로 새테스트없음. 로컬만 반영, 신규/임시/사용중단DB없음.

- 모바일 부적합재고 라벨 한줄 보정(2026-10-16): 사용자 요청으로 해당 표 첫열65%/nowrap 적용. 390·320px 실제 화면에서 복합·장기 부적합 재고 한줄·잘림없음 확인. PC·데이터 불변, 로컬만 반영. 신규/임시/사용중단DB없음.

- 재고 영역 연노랑 통일(2026-10-16): 사용자 요청으로 PC상단 재고현황·일별재고열·상세G2실사행·부적합재고와 모바일재고/실사 데이터셀을 공통 #fff9e6으로 변경. 기존회색채우기대체, 신규부적합실적/헤더/집계불변. PC computed색4영역 및 PC/390/320 실제화면/pageerror0 확인. 로컬만반영, 신규/임시/사용중단DB없음.


### 배포 규칙 교체 · 2026-10-16
- 사용자 승인: 앞으로 매 배포의 검증 방법을 인터넷에서 조사하여 필요한 테스트를 먼저 보고하고 사용자 확인 후 실행. 기존 고정 배포 규칙 폐기. 이번 요청은 규칙 설정만이며 배포 금지.
- Root에 단일 현행 배포 절차를 정의하고 공지/팝업 우선 및 최종 전체 회귀 기본 규칙 제거. 검증표·환경 안내의 참조 정합화. 기존 배포 SOP는 실행 규칙 효력을 폐기하고 과거 사실 참고로만 유지.
- 문서만 수정. CI/배포 스크립트/원격 보호 설정은 변경하지 않았으며 계획과 자동화 제약의 차이는 향후 사전 보고 대상으로 명시. 링크·diff·충돌 문구 확인. push/병합/배포 미실행. 신규DB·임시DB·사용중단DB 없음.


### 홈 7일 전망 보정 · 2026-10-16
- 사용자 확인 후 로컬 수정 승인. 7일 유지, 그래프 마지막 점을 눈금선 오른쪽 끝650에 맞춤. 일요일 예상 생산은 명시 계획과 무관하게0; 토요일 생산의 일요일 재고 반영·납품 차감·기존 공휴일 계획 처리는 유지. 하단은 “일 평균 N대 생산 반영”만 표시(평균 미산출은 별도 한줄 안내).
- 관련 홈 테스트13/13, typecheck·변경파일eslint 통과. 합성 로컬PC1440/모바일390/320에서7점·마지막x650·한줄설명·페이지overflow0·pageerror0 확인, PC/390 스크린샷 직접 확인. 자재 조회 오류는 해당 합성 fixture에 미구현된 자재 API이며 홈 그래프 검증과 별개. 실제 운영 데이터 검증 아님.
- local preview /tmp/g2-classification-validation/frontend에 반영. 원격push/병합/배포 없음. 신규DB·임시DB·사용중단DB 없음.


### Real local G2 review runtime - 2026-10-16
- User authorized replacing the synthetic preview with a real local API and isolated review DB. Created emi_qms_e2e_g2_live_review_20261016; container emi-qms-e2e-g2-live-review-20261016-e2e-postgres-1. Existing production/general-development DBs untouched. This tmpfs review DB survives browser reloads but not container stop/restart; retained running for user review, not cleaned up.
- Latest worktree backend Release build passed; legacy migrations 1-130 and Cheongju overlays 0132-0136 applied to the new DB. API5087 Testing/Dev with external notification providers disabled. Replaced verified synthetic Vite PID8041 with actual app entry on5188, source /tmp/g2-classification-validation/frontend, API5087. Use /g2, not local-home-review.html.
- Real API verified stocktake321 -> days/home inventory321; update456 -> days/home inventory456. Cleared verification quantity through API afterwards. Actual /g2 browser loaded empty real data with zero page errors; screenshot directly viewed. No production data copied, no deployment/push/merge. No obsolete DB.
- Setup script /tmp/g2-live-review-20261016.sh contains initial createdb; inspect existing runtime before restarting. Backend session60655 and frontend session20929 retained for review.


### Plan-gated home forecast - 2026-10-16
- User requested no expected production on dates without production plans. Home forecast now adds the historical daily average on the forecast date only when that date has a positive production plan and is not Sunday. No default weekday production. Today remains the actual inventory anchor. Delivery subtraction and actual backend inventory ledger unchanged.
- Updated existing home tests: positive Saturday, Sunday with positive plan, missing/zero plans, missing day, zero/null average, same-day delivery. Home tests13/13, changed-file eslint and typecheck passed. Actual local read-only browser PC1440/mobile390: Sunday10/18 remains53 instead of76; all seven points53 for current23production/23delivery plans. No page errors/overflow, both chart screenshots viewed.
- Local source and live frontend updated; no writes to user review data, no DB schema changes or new DB, no obsolete DB. Existing review DB retained. No push/merge/deployment.


### Materials vendor column removal - 2026-10-16
- User requested removing the vendor column and giving its width to specification. Removed C from desktop visible columns, increased F weight14 to20 using former C weight6, adjusted item header split8 to7. Excel format, stored vendor, search and detail info unchanged.
- Existing material tests26/26 passed. Real review DB has no material catalog; browser-only synthetic fixture verified17 aligned columns and no vendor header, desktop screenshot directly viewed. No DB writes/creation or schema changes; review DB retained. Local only, no deployment.
