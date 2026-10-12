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
