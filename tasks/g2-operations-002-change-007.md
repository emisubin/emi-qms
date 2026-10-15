# G2 Change 007 — 자재 일일 실사 엑셀 업로드

- 승인: 2026-10-08 사용자 요청. G2 아래 새 메뉴명은 “자재”. “G2 재고_10.07 현황.xlsx” 기준 개발. 자재팀이 매일 퇴근 전 모든 자재 실사 후 엑셀에 기록하고 PMS 업로드로 자동 입력하는 것이 목표다.
- 기준선: codex/unified-osan-shell fe7ba93. 기존 생산/출하 및 청주 메뉴 아이콘 완료분 보존. 운영/공유 runtime·DB·원격 게시 변경 승인은 없음.
- 현재 단계: 2026-10-09 승인 방향으로 기능 구현 및 영향 검증 진행. 아래 최신 계약이 초기 조사 당시 확인 대기를 해소한다. 운영/공유 환경 반영과 사용자 검수는 미실행.

## 확인된 파일 구조

- 약16MiB, 43개 시트(표시31/숨김12). 최신 재고 시트명은 `26-10-07오후 `이며 마지막 공백이 있다. 품목92행, 분류 사급/도급/부자재. 원본 파일이나 실재고를 저장소에 복사하지 않는다.
- 상단 기준일 셀은 TODAY()이므로 실사 기준일로 사용할 수 없다. 시트명의 날짜가 원본 이력 기준이며 업로드 화면에서 대상 시트/실사일을 표시해야 한다.
- 기본정보: 구분·업체명·품명·품목코드·규격·단위·1SET수량. 실사 입력 열은 제작중(N), 키팅대차(O), 적재랙(P), 자재실(T), ERP(U), 이동중(V). 최신 ERP열은 숨김/모두빈칸. 전날 V열은 IQC이므로 과거 형식의 의미를 자동으로 바꾸지 않는다.
- 제조합계=N+O+P, 전체재고=제조합계+T+U+V. 생산가능대수는 해당 원수량 합계를 1SET수량으로 나누어 버림한다. 구역별 버림 결과를 더하면 전체 계산과 다를 수 있다.
- 최신 수량 입력 셀에는 수식 없음. 합계/생산가능대수 수식은 PMS에서 재계산 대상. 숨김 부품 참고 시트와 이미지는 재고 행이 아니다.
- 83/86행의 리본지 두 항목은 같은 품목코드이나 품명/규격이 다름. 합산 또는 별도 보존 여부를 사용자에게 질문했다. 코드만으로 임의 합산하거나 덮어쓰지 않는다.

## 초기 조사 시 확인 대기 (아래 최신 승인으로 해소)

- 일일 전수 실사 파일의 빈 재고 셀: 0 처리 또는 미입력 오류. 답변 전 저장 의미를 확정하지 않는다.
- 동일코드 리본지 두 행: 별도 항목 유지 또는 합산. 답변 전 품목 식별/집계를 확정하지 않는다.
- 기존 180 안전재고의 수정 권한/정책은 아직 답변 없음. 기존 조건부 서식 120/130/180을 임의 정책으로 적용하지 않는다.

## 구현 방향 및 검증 경계

- 기존 오산 공통 메뉴/표/업로드 dialog 구성 재사용. 원본 파일 업로드 → 날짜/대상시트/행 오류 확인 → 일일 실사 반영 흐름. 수동 전사 없이 수량 자동 연결. 엑셀 원본은 변경하지 않는다.
- 기존 OsanProjectExcelParser는 5MiB/20시트/수식거부 정책이므로 직접 적용할 수 없다. 공통 동작은 재사용하되 이 양식에 맞는 제한된 파서가 필요하다. 새 업로드 구현이 기존 프로젝트 업로드의 제한을 바꾸지 않게 한다.
- 실제 기능 구현 시 청주 DB/자재 담당 쓰기/전체 G2 조회의 서버 경계, 기준일별 이력, 동일일 재업로드와 동시 요청, 빈칸·0·중복코드·앞자리0·수식/시트 선택 반례를 확인한다. 권한과 재업로드 세부 계약은 구현 전 확정 근거를 보충한다.
- 관련 자동검증과 합성 PC/모바일 화면 검증, 권한·DB/동시성 변경의 독립 검토가 필요하다. 실제 첨부 파일은 read-only 호환성 확인만 수행한다.
- DB: 현재 새 DB·테이블·임시 DB·미사용 DB 없음. 코드·migration·원격·배포 변경 없음.


## 2026-10-09 확정 계약과 구현

사용자 최종 요청: “좋아. 일단 정해진 방향으로 구현해봐.” 이전 대화에서 확정한 revision28 자재 디자인과 기존 오산 공통 shell/제목을 재사용한다. 새 UI 후보/재디자인은 범위 밖이다.

- 고정 92개 품목. 같은 코드의 리본지 행은 별개 ID로 유지. 품목 정보·포장단위·이미지는 최초 1회 등록하고 매일 복제하지 않는다. 1SET는 G2 한 대의 소요량. 향후 생산/구매 연계는 이번 범위에서 구현하지 않는다.
- 청주 기존 DB 전용 `g2_material_items`와 `g2_material_counts`. counts PK=(실사일,품목ID). 수량 nullable numeric(16,4), 최종 저장자·최종 저장시각만 유지. 변경 이력과 저장마다 쌓이는 버전 없음.
- 저장되지 않은 날짜를 조회하면 92개 고정 품목과 빈 실사 입력칸을 반환하며 DB행은 만들지 않는다. 빈칸=null, 0=실사값0. 기존 값을 비우고 저장하면 null로 수정.
- 직접 입력·엑셀 가져오기 모두 화면 draft를 편집하고 저장 버튼에서 동일 API로 전체 92개를 저장. 엑셀 원본 보관 없음. 같은 날짜는 transaction+일자 lock으로 전체값 upsert. 마지막 완료 저장 snapshot이 남고 동시 저장 행이 섞이지 않음.
- 쓰기/가져오기: 활성 자재부서만 서버 검사. G2Read 사용자 조회/품목정보 허용. 숨김 ERP(U)는 직접 입력 화면에 노출하지 않고 가져오기·저장 시 보존한다.
- 날짜 KST 오늘까지 최근 달력 6개월. 오늘의 6개월 전 날짜는 포함하고 그 이전만 매일 삭제. 품목 catalog는 정리 대상 아님. 예:10월9일이면4월9일 유지,4월8일 이전삭제.
- 정리 worker는 `G2Materials:Retention:Enabled` 기본 true, 명시 false로 비활성. 모든 mutation worker를 끄는 환경은 `G2Materials__Retention__Enabled=false`도 설정해야 한다. ReviewSafe 강제 비활성. 실제 등록과 runtime status 동일 정책.
- Q=N+O+P, R=trunc(Q/H), W=trunc((T+U+V)/H), Y=Q+T+U+V, Z=AA=trunc(Y/H). 소수 합산은 정수 스케일로 처리하여 0.3+0.6+0.1의 내림 오류 방지. 원본 AA<180 색상 규칙·E4:E83 중복 코드 규칙 재사용. 새로운 안전재고 수정 정책 추가 안 함.
- 원본 Excel 열·색·병합·구분선과 최종 회색 채우기 #F3F4F5 적용. 일반/굵은 선 색 동일 #D5DAE1. 하나의 표, 두 행 sticky header, 전체 페이지 세로 스크롤. 품목정보 팝업까지만, 이미지 확대 없음.

### 품목 최초 등록 준비 및 배포 경계

`python3 scripts/prepare-g2-material-catalog.py <workbook> --sheet <approved-sheet> --output-dir <private-outside-repository>`는 catalog.json, catalog.sql, images만 생성하며 DB에 접속하지 않는다. 실재고는 추출/초기화 SQL에 포함하지 않는다. 원본과 생성된 실제 자료는 commit하지 않는다.

실제 환경 반영 순서: 승인된 청주 DB migration0133 → private catalog.sql 1회 실행(청주 DB identity 및 빈 catalog guard) → images를 서버의 비공개 경로에 배치하고 `G2Materials:ImageRoot` 지정 → 사용자 검수. 이 순서는 준비한 적용 방법이며 실행하지 않았다. 기존 runtime·운영/UAT DB·provider·원격 게시를 바꾸지 않았다.

### 검증 및 보정

- 초기 backend build 경고/오류0. 자재/parser/migration/임시DB 영향12개 통과. 실제 첨부 workbook을 읽어 92×6 수량을 원본과 비교, 불일치0. 날짜시트31개 인식. 원본 수정/서버 영구 보관 없음.
- frontend 자재 workflow와 G2navigation8개 통과: 전체92 저장, 검색 중 숨은 행 보존, ERP 보존, null/0 구분, import draftonly 및 빈칸 덮어쓰기, 탭시트 변경, 저장실패 draft 유지, 읽기전용, 소수 계산, 과대 입력 렌더 보호.
- 독립 검토 요청 모델 gpt-6-astra/high, 실제 모델 NOT_REPORTED. 검토 지적: 감사 mutation route coverage 누락(P1), null 항목 요청(P2), 소수 버림(P2), retention worker activation/status 불일치(P2). 해당 보정 구현. API route는 무이력 저장/draft-only import 계약으로 감사 대상에서 명시 제외.
- 보정 후 backend G2Material/AuditMutationCoverage/MaintenanceWorkerGate/ReviewSafe 영향33개 통과. 실제 app startup 포함.
- 합성 화면 검증용 별도 frontend 8780, source는 이 worktree, DB/provider 없음. 기존8774시안과 공유 runtime은 유지. PC/390px 및 popup 검증 결과는 아래 완료 기록에 기재.

### DB·Git·남은 작업

- 새 영구 DB: 없음. 청주에 추가할 테이블2개 migration만 작성, 운영/UAT 적용 전.
- 검증 전용 DB: 이번 실행 합성 DB fixture 종료 시 삭제. 전용 `emi-g2-material-test-20261009` 컨테이너도 종료/삭제 완료.
- 더 이상 사용하지 않는 기존 DB: 없음. 임의 기존 DB/volume 삭제 없음.
- 원격 push/PR/merge/배포 없음. 독립 검토 최종 확인·화면 검증 후 범위 내 local commit 예정. 이후 사용자 검수와 명시 승인된 환경 반영이 남음.


## 2026-10-09 개발 완료 기록

- 구현·영향 자동 검증·독립 검토 완료. reviewer 최종 미해결 P0/P1/P2 없음. 사용자 검수/공유 DB 적용/원격 반영/배포는 미실행.
- frontend typecheck 및 변경 파일 lint 오류/경고0, workflow/navigation8개 통과, production Vite build 성공(기존 대형 bundle 경고 유지).
- 합성92품목 실제 App/공통 shell을 Chrome에서 확인: PC 행25px/19열/색/열너비, 직접수정→저장완료→같은날 재조회 수량유지, 페이지 스크롤 sticky header/분류경계, 품목정보 및 가져오기 팝업. 390×844에서 popup 여백/가독성 및 page overflow 없음 확인. 좁은 표는 가로스크롤, 페이지 세로스크롤 유지. console error0. 이 화면은 메모리 응답을 사용했으며 실제DB E2E로 주장하지 않는다.
- 기존 wireframe 전역 important가 승인표 색/행높이를 덮는 문제를 발견하여 자재 콘텐츠 ID 범위 안에서만 스타일 격리. shell/공통 제목에는 영향 없음. 시안/실품목 데이터는 코드에 복사하지 않음.
- 합성 시각 증빙은 저장소 밖 conversation visualizations의 `g2-material-implemented-pc.png`에 보관(사용자 결과 확인 목적, 실제 재고/개인정보 없음). 임시8780 검증 서버 종료 및 이번 검증용 local-material-review 파일 정리. 기존 local-g2/local-shell WIP와8774시안은 보존.
- 다음 검수: 승인된 검수 환경에 migration/catalog/images 적용 후 자재 계정으로 신규날짜 직접입력 저장→Excel덮어쓰기 저장→직접수정 재저장, 비자재조회만, 날짜변경/재조회,180 미만 조건색 확인. 합성자동검증과 실제사용자검수는 별개.
- 이번 작업 범위의 local commit만 수행. push/PR/merge/배포 없음.

## 2026-10-09 두 메뉴 통합 로컬 검수 서버 시작

- 사용자 요청: “지금 로컬에 구현된 2개의 메뉴 같이 들어있는거지? 서버 열어서 보여줘”. 현재 e462ce1 source로 실제 backend/DB 연결 검수 서버 시작 승인.
- 실행: 기존 `scripts/e2e-business-unit-access-full-stack.sh --review-server`, E2E_RUN_ID=g2review20261009. frontend http://127.0.0.1:5178, backend127.0.0.1:5086. 실행 session35733. 작업 소유 container `emi-qms-e2e-g2review20261009-e2e-postgres-1`, tmpfs. 기존 container/서버 변경 없음.
- 검수 DB 생성: `emi_qms_e2e_g2review20261009_directory`(계정/사업부), `emi_qms_e2e_g2review20261009_cheongju`(두 G2 메뉴), `emi_qms_e2e_g2review20261009_osan`(사업부 격리용). 서버 이용 중이므로 유지. 이 서버의 정상 종료 cleanup이 소유 DB/container를 정리하므로 실제 업무 데이터 입력용이 아니다.
- 정상 schema bootstrap/migration 완료. 청주 검수 DB에 private catalog.sql 최초 적용(92품목, 실사수량0행), G2Materials:ImageRoot private images 경로. 합성 dev-admin 계정을 활성 자재부서로 연결하고 표시명 G2 로컬 검수로 설정. 운영/실사용자 권한 변경 없음.
- 외부 provider와 삭제 worker 비활성. G2Materials__Retention__Enabled=false. 공개 PMS·운영 DB 미변경, 새 영구DB/불필요 기존DB 없음.
- 확인: /health/ready200, 생산 days31, 자재items92/canEdittrue. in-app browser에서 dev-admin선택 후 생산/출하 화면 확인→G2 공통 메뉴 펼침→자재 이동, 자재입력460칸/엑셀가져오기 활성 확인. 실제저장 API 연결이며 이전 mockpreview가 아니다. 이번 시작확인에서 실사 수량은 입력하지 않음.
- 사용자에게 production/materials 직접 링크 제공. 검수 서버는 열어둔다. 이 기록은 서버 시작 상태이며 사용자 검수 수락/운영 배포 완료를 뜻하지 않는다.

## 2026-10-09 모바일 확정 시안 로컬 통합 완료

- 사용자 승인: 모바일 revision3 확정, 코드를 로컬에 그대로 이전. BoxHero 목록/상세 탭 배치와 PMS 공통 모바일 틀. 2열 수량(총재고/G2생산가능대수), 180미만 빨강, 제조/자재 Part 입력. 모바일 저장/Excel 버튼 제거, 변경 즉시 자동 저장. PC 수동저장/가져오기 유지.
- 기존 같은 날짜 전체92개 snapshot API와 서버 자재부서 권한/청주 DB 경계 재사용. FE 단일 직렬 저장, 연속입력은 최신 대기 snapshot으로 합침. 실패 시 draft 유지/명시 재시도, 날짜 변경 차단. 새 migration/DB 없음.
- 실제 데이터는 코드에 복사하지 않음. 승인 CSS/마크업만 통합. 기존 runtime5178 소스 재사용. 아래 자동검증·시각검증·독립검토 완료. 사용자 로컬 검수는 대기.

- 승인된 mobile.html/mobile.js/mobile.css의 목록·상세·입력 폼을 React로 이전하고 기존 오산 공통 메뉴/제목을 재사용. 860px 이하 모바일, PC 기존 표 유지. 숫자 없는 품목은 빈칸, 179는 빨강/180은 일반색. 인증된 품목 이미지 API 사용.
- 모바일 입력 시 기존 92행 저장 API로 즉시 저장한다. 연속 입력을 직렬 처리하고 마지막 값까지 완료한 뒤 저장 완료를 표시한다. 숨김 ERP와 다른 품목 값을 보존하며 빈칸은 null, 0은 0이다. 위 PC 저장 버튼 계약은 유지하고 모바일만 이번 사용자 승인으로 자동 저장한다.
- 저장 중 SPA 이동·로그아웃·사업부/검수 계정 전환을 차단하여 대기 입력 유실을 방지한다. 저장 실패 후에는 재시도하거나 명시적으로 미저장 변경을 버리고 이동할 수 있다. PC 수동 저장 중 모바일 폭으로 변경하면 해당 요청 완료 전 입력을 차단한다.
- 관련 테스트 17개 통과: 페이지13개, 저장 큐3개, 이동 보호1개. 연속 입력/최신값 보존, 실패 재시도, null/0, ERP/92행 보존, 권한, 180 경계, 이동 차단, PC 저장 중 화면 폭 전환 포함. TypeScript 검사·변경 파일 lint 통과. Vite production build 성공, 기존 bundle 크기 경고 유지.
- 독립 reviewer 최종 GO, 미해결 P0/P1/P2 없음. 요청 모델 gpt-6-astra/high, 실제 모델 NOT_REPORTED. 대기 저장 중 이동 유실·PC 저장과 모바일 저장 경합·실패 후 이동 불가 지적을 보정했다. 이후 ref 선언 위치만 lint 요구에 맞춰 이동했으며 동작 변경 없음.
- 실제 로컬5178/5086 검수 서버에서 당일 첫 품목179 입력→저장 완료→새로고침 값 유지 확인 후 빈칸으로 복원/저장 완료 확인. 기존 청주 검수 DB의 당일 snapshot을 사용했으며 운영 데이터는 변경하지 않았다. PC 수동 저장/Excel 버튼 유지, 390×844 목록 가로 넘침 없음, 360×640 입력 폼 내부 스크롤/하단 잘림 없음 직접 확인. 실제 휴대폰 키보드/OS 글자 확대 검증은 미실행.
- 새 영구 DB·새 임시 DB·migration·더 이상 사용하지 않는 DB 없음. 기존 g2review20261009 검수 DB 3개는 위 서버 사용자 검수용으로 계속 유지하며 이번에 삭제하지 않았다. 실품목/원본 파일은 commit하지 않는다.
- 범위 내 local commit으로 보관. 원격 push/PR/merge/배포 없음. 실제 통합 화면 사용자 검수와 이후 승인된 환경 반영은 남아 있다.

## 2026-10-09 엑셀 가져오기 Part 구분

- 사용자 요청에 따라 엑셀 미리보기 표를 기존 디자인의 2단 헤더로 변경. 제조 Part(제작중/키팅대차/적재 랙), 자재 Part(자재실 합계/이동중), 품목은 두 행 병합. 데이터 매핑·저장·권한 로직 변경 없음.
- 자재 페이지 기존 테스트13개와 해당 파일 lint 통과. 실제5178 로컬의 10월7일 Excel 미리보기에서 병합 헤더와 열 정렬을 직접 확인. 표 반영/DB 저장은 실행하지 않음. 헤더만 잘라 저장소 밖 증빙 보관.
- 새 DB·임시 DB·미사용 DB 없음. 기존 검수 서버 유지. local commit만 수행하며 원격/배포 없음.

## 2026-10-09 엑셀 미리보기 확장

- 사용자 요청: 팝업 확대, 품목정보 전체 표시, 품목정보/제조/자재 Part 사이 굵은 구분선. 기존 확정 표를 재사용하여 구분/NO/업체명/품명/품목코드/규격명/단위/1SET 8열과 실사5열 표시. 등록 품목정보를 기존 ID로 연결하며 저장 로직은 변경하지 않음.
- 가져오기 팝업만 최대1440px/화면 좌우 여백40px. 긴 정보는 줄바꿈, 규격명 왼쪽 정렬. 기존 색 #d5dae1의 2px 경계선을 collapse 표에 적용하여 중복 없이 연결. 공통 reset의 col 너비 초기화를 대상 CSS에서 보정.
- 관련 기존 테스트13개 통과, 수정 TSX lint 통과. 실제 로컬 Excel 미리보기에서 품목13열/긴규격/가로 넘침 없음/2px 연속 경계/취소 및 반영 버튼 노출을 시각 확인. 높이가 낮은 창에서는 미리보기 높이를 제한. 추가 헤더 이미지 파일 저장은 브라우저 캡처 시간 초과로 미보관(화면 관찰은 완료).
- 새 영구/임시 DB, 미사용 DB 없음. DB 저장 미실행, 기존 검수 서버 유지. 범위 local commit, 원격 반영/배포 없음.

## 2026-10-09 이름 독립 가져오기·엄격 구조 검증 (사용자 결정 대기)

- 승인: 파일명/시트명 날짜 규칙 의존 제거. 양식 하나 자동선택, 복수 시트 사용자 선택, 시트명 날짜와 PMS 실사일 불일치 확인 후 표 반영. 추가 답변: 서식(색/테두리/너비/숨김)은 허용하며 열·품목·수식 등 구조 변경만 차단.
- 현재 WIP: G2MaterialExcel/Contracts, G2MaterialsPage/g2Materials, backend/frontend 관련 테스트. 헤더 전체·품목/포장정보·병합·행별 계산식·추가 값 검사, 시트/셀 위치 오류. 원본의 표 밖 AD57 단독 점은 레이아웃 잔여값으로 허용. 날짜는 nullable 힌트, 후보 선택 전 items 없음. 수량/숨김ERP 기존6개 필드·92행 저장 계약 유지. 권한/DB/migration 변경 없음.
- 서버 테스트27개, UI 테스트15개, frontend typecheck/lint 통과. 독립 reviewer: 추가 P0/P1/P2 없음이나 아래 원본 계약 충돌로 GO 보류. 요청 gpt-6-astra/high, 관측 NOT_REPORTED.
- 실제 원본 read-only 호환성 검사에서 Q11/Q12 제조실 합계만 계산식 없는 직접 입력값임을 발견. 기존 import는 이 값을 읽지 않았음. strict 수식 검사에서 원본 차단. 사용자에게 (1) 원본을 N/O/P입력+Q합계수식으로 정규화 (2) 두 품목 직접합계 예외지원 중 선택 질문, 답변 전 관련 계약 변경 보류.
- 독립 ZIP/XML 전체 확인에서도 계산열의 수식누락은 위2개뿐. 원본을 변경하지 않고 메모리에서 두 칸에 N+O+P를 넣은 탐색 검증은92행/후보29/적합1 통과. 이는 원본 그대로 통과 증거가 아님. private /tmp/g2-strict-compat runner·catalog 사용, 실제자료는git제외.
- runtime5178/5086은 기존 backend 버전 유지. 새 backend 검수/재기동·최종commit은 위 답변 후 진행. UI는 worktree HMR 대상이므로 WIP 존재. DB 생성/변경/임시DB 신규/미사용DB 없음. 기존 검수DB3개와서버 유지. 원격/배포 없음.


## 2026-10-11 직접 입력된 계산값 허용·가져오기 검증 완료

- 사용자 결정: 구조 변경만 차단하되, 계산 셀에 수식 대신 직접 입력된 숫자는 모두 읽고 안내한다. 앞선 Q11/Q12 결정 대기를 해소했다. 파일명/시트명 및 색·너비·숨김 등 서식 변경은 허용한다. 헤더·92품목·포장정보·병합·수식 구조 변경은 시트/셀 위치와 함께 차단한다. 복수 적합 시트 선택 및 시트명 날짜와 PMS 실사일 불일치 확인을 지원한다.
- Q/R/S/W/X/Y/Z/AA 계산열의 유효 숫자를 보존하고 가져오기 안내 및 해당 품목에 표시한다. 관련 원천 실사값을 수정하면 영향받는 계산값만 해제하고 원래 Excel 계산으로 전환한다. 다른 Part의 독립 값은 보존한다. 파일 자체는 보관하지 않는다.
- 기존 청주 g2_material_counts에 nullable calculation_overrides JSONB를 추가하는 migration0134 작성. 기존 날짜/품목 데이터에서 함께 갱신하며 수정 이력이나 새 업무 DB는 만들지 않는다. 기존 권한·92행 snapshot·동시성 계약 유지.
- 검증: backend G2MaterialTests36개, frontend 페이지17개, DB/migration10개 통과. 원본 무수정 읽기92행/후보29개/적합1개, Q11/Q12 숫자 원본 일치 및 안내2건 확인. frontend typecheck/lint/build 및 backend Release build 성공. Vite 기존 큰 bundle 경고 유지. 독립 reviewer GO, 미해결 P0/P1/P2 없음(요청 gpt-6-astra/high, 관측 NOT_REPORTED).
- 실제5180 PC에서 원본 미리보기와 Q11/Q12 안내2건, Part 구분 및 팝업 하단 버튼 노출을 화면으로 확인했다. 추가 이미지 파일 저장과 모바일 새 안내 문구 시각 확인은 브라우저 CDP 연결 오류로 미완료. 모바일 viewport 원복 요청은 성공했다. 원본은 검수 브라우저의 미저장 표에만 반영했고 저장/입력 이벤트를 실행하지 않았다. 자동검증과 실제 휴대폰 검수는 별개다.
- 새 검수 서버: frontend5180/backend5088, session9979, source는 이 worktree. 전용 g2import20261010 container/tmpfs 사용. 외부 provider 및 retention 비활성. 새 임시 DB emi_qms_e2e_g2import20261010_directory(계정/사업부), emi_qms_e2e_g2import20261010_cheongju(G2 검수), emi_qms_e2e_g2import20261010_osan(사업부 격리) 생성 후 검수용 유지. 기존5178/5086과 g2review20261009 DB3개는 변경/삭제하지 않았다.
- 자동 DB 검증의 emi_qms_e2e_5db0fe17a5_directory/cheongju/osan은 fixture 종료로 정리, 소유 container/network/tmpfs도 정리 확인. 새 영구 DB 및 더 이상 사용하지 않는 DB 없음. 원본/실재고/비공개 catalog는 commit하지 않는다.
- 범위 파일만 local commit. 원격 push/PR/merge/배포 및 운영 DB migration 적용 없음. 남은 작업: 사용자 검수, 모바일 새 안내 시각 확인, 명시 승인 후 대상 환경 반영.


## 2026-10-11 계산값 일치 검증으로 정책 수정

- 사용자 승인: 수식 대신 입력된 숫자는 정상 수식 결과와 같으면 안내 없이 허용, 다르면 합계 불일치 안내 후 저장 차단. 기존 직접 계산값 유지 정책을 대체한다.
- import는 원천 실사 수량으로 8개 계산열의 정상값을 구해 직접 입력된 숫자를 비교한다. 엑셀 숫자 정밀도를 고려한 G15 비교를 사용하며 실사 원천 수량4자리 제한과 계산값 정밀도를 분리했다. 불일치는 시트/셀/품목/입력값/정상값을 안내하고 파일 전체 표 반영을 막는다. 서버 저장에서도 기존 호환 필드의 불일치를 write 전 거부한다. 일치한 계산값은 별도 보존하지 않고 원천 수량만 저장한다.
- PC/모바일 직접 계산값 안내 및 관련 스타일 제거, 화면 계산은 원천 수량만 사용. calculation_overrides 컬럼과 nullable API 필드는 이전 버전 호환을 위해 보존하되 새 저장은 null이다. 기존 migration0134/데이터를 삭제하지 않았다. 별도 새 migration 없음.
- backend parser/DB 테스트43개 통과(실제 임시DB, skip0), frontend17개 통과, typecheck/lint/build 성공. 반복 소수1/3 정상 허용과 근접 불일치 거부, 8열 정상/불일치, 저장 거부 후 기존 데이터 보존 포함. 원본 무수정92행/적합시트1개/안내 및 예외값 없음 확인; 원본Q11/Q12 모두 수식 결과와 일치. 독립 review GO, 미해결P1/P2 없음.
- 기본 backend 실행은 기존 ImageSharp3.1.11 NU1902/NU1903 감사 경고의 오류 승격으로 중단됨. 검증 재실행에만 WarningsNotAsErrors=NU1902;NU1903을 전달해 경고를 표시한 채43개 통과. 저장소 보안/CI 설정 및 패키지 버전 미변경. 기존 Vite bundle 크기 경고 유지.
- 실제5180 PC 기본 조회 및 가져오기 팝업 모양 직접 확인. 해당 runtime backend는 재기동하지 않아 이번 불일치 API의 브라우저 종단 확인 및 모바일 시각 확인은 미실행. 기존 검수 DB 보존을 위해 소유 서버 종료 cleanup을 실행하지 않음. 기존5178/5180 검수 서버의 backend 갱신은 남아 있음.
- 새 영구DB/미사용DB 없음. 자동검증 전용 emi_qms_e2e_20261011012030_17995_18230 및 fixture별 사업부 임시DB는 테스트 cleanup으로 정리, 소유 container/network/tmpfs 제거 확인. 앞선 명령 인자 오류 실행의20261011012013_17336_6031 임시 환경도 cleanup 완료. 기존 검수DB는 유지. calculation_overrides는 신규 업무 저장 용도가 없어졌으나 호환용으로 남으며 임의drop하지 않음.
- 범위 local commit만 수행. 원격 push/PR/merge/운영 배포 없음.


## 2026-10-11 최신 로컬 검수 서버 열기

- 사용자 요청에 따라71578f7 backend와 현재 frontend로5182/5090 검수 서버 실행(session49198). 기존5178/5180 서버는 종료 시 DB cleanup되는 구조라 보존했다.
- 소유 container emi-qms-e2e-g2equal20261011-e2e-postgres-1와 tmpfs. 새 임시DB emi_qms_e2e_g2equal20261011_directory(계정/사업부), emi_qms_e2e_g2equal20261011_cheongju(G2 두 메뉴 검수), emi_qms_e2e_g2equal20261011_osan(사업부 격리) 생성 후 유지. 새 영구DB 및 미사용DB 없음.
- 기존 private catalog92개와 이미지 경로 적용, 합성dev-admin을자재부서로 설정. 실사값 미입력. 외부provider/retention삭제worker 비활성. 운영/기존검수 데이터 미변경.
- in-app browser에서5182/g2/materials 열고 최신 자재92개 목록/빈실사값/이미지 표시 직접 확인. 사용자 확인용 탭 유지. 생산출하관리는 동일App의/g2/operations 경로(현재 App route 확인), 별도 최신 화면 재검수는 하지 않음. 원격/배포 없음.

## 2026-10-11 엑셀 직접 가져오기와 공통 예외 팝업

- 사용자 확정: 기존 파일 선택/미리보기 가져오기 팝업을 삭제. 페이지의 엑셀 가져오기 버튼에서 파일 선택기를 직접 열고, 검증 후 표에 반영하며 페이지 저장에서만 DB 저장. 승인 시안 import-exceptions.html의 기존 G2 공통 회색 제목/흰 본문/우측 버튼/EMI 빨강을 재사용.
- 유효 시트 여러 개는 선택 팝업, 날짜 불일치/날짜 미확인/미저장 입력값은 확인 팝업. 여러 확인 사유는 한 창에 함께 표시. 오류는 공통 오류 팝업으로 보여주고 기존 draft 유지. 인식 가능한 서버 오류의 시트·셀·품목·입력값·정상값을 구분해 표시하며 원문 fallback 보존. 파일/시트 선택 중에는 중복 작업 차단, 같은 파일 재선택 가능.
- 이전 compact import dialog, preview table 및 전용 스타일 삭제. 서버 parser/API/권한/DB 구조 및 모바일 자동저장 계약은 미변경. 이전 로컬 compact 팝업 미커밋 변경을 이번 승인 흐름으로 대체.
- 관련 frontend20 tests 통과: 직접 선택/정상 자동 반영, 시트 선택, 날짜 미확인, 날짜+미저장 확인 취소, blank 교체 및 후속 저장, 전체 시트 오류/재시도, 파일 형식/통신 오류, 기존 모바일 저장 및 읽기 권한 계약 포함. ESLint 통과. 최종 build 결과는 아래 추가.
- 5182 로컬에서 실제 원본 선택과 공통 날짜+미저장 확인 팝업/취소, 손상 합성 파일 오류를 직접 확인. 1440×900 및900×700 PC 확인, 브라우저에서 DB 저장은 실행하지 않음. 일부 예외 분기는 자동 테스트와 승인 시안 확인이며 모든 예외에 대한 실제 서버 브라우저 재현을 의미하지 않음.
- 시각 증거: visualizations/2026/10/08/01a118d1-afc8-7351-9370-0126a196e572/g2-material-mockups/import-direct-local.png (팝업만 캡처). 별도 시안은 기존 공통 UI 재사용이며 추가 레퍼런스 선정 없음.
- 새 영구DB/새 검증용DB/미사용DB 없음. 기존5182 검수용DB 유지, DB 정리 실행 없음. local commit 범위이며 push/PR/merge/운영 배포 없음. 사용자 로컬 검수 대기.
- 최종 typecheck/Vite build 성공(기존 chunk 크기 경고만 유지). frontend20 tests/ESLint 성공. 이전 compact popup과 미리보기 전용 선택자 미사용 잔재 제거 확인.

## 2026-10-11 자재 표 합계 헤더 정리

- 사용자 요청에 따라 PC 자재 원본 표의 총재고 합계→총재고, 생산가능대수 합계→생산가능 대수로 변경. 제조+자재 합계 표시 열 제거, 합계 그룹2열/전체18열 및 구분행 colspan/열너비 정합성 반영.
- 제거한 AA 표시열의180대 미만 조건부 서식을 Z 생산가능 대수에 이동. 중복 품목코드 서식과 계산/API/원본Excel 양식 검증 계약은 유지.
- 실제5182 PC 화면에서18개 열/헤더/66·102·86 등180미만 마지막열 강조 확인, ESLint 및diff check 통과. 자동 검증 결과 아래 추가. 모바일/DB/서버 변경 없음.
- 새DB/검증용 임시DB/미사용DB 없음, 기존 검수DB 유지. local commit만 수행하며 원격/배포 없음.
- 기존 자재 페이지 관련20 tests 통과. 표시 열만 변경하여 별도 전체 build/서버 회귀는 반복하지 않음.

## 2026-10-11 표준 양식 다운로드 및 양식 시트 처리

- 사용자 지정 순서 양식 다운로드→엑셀 가져오기→저장 적용. 사용자 첨부 빈 실사 양식을 frontend asset으로 그대로 제공(92개 품목/이미지/수식 포함, 실사 원천값 모두 빈칸). 원본 xlsx 내용 미수정, 파일명 G2 자재 엑셀 양식.xlsx.
- 시트명 trim 후 정확히 양식이고 날짜가 없으면 날짜 확인을 생략하고 현재 실사일 draft에 반영. 날짜가 있는 시트와 그 외 날짜 미확인 시트는 기존 확인 유지. 미저장 입력값 교체 확인 및 페이지 저장의 DB 반영 분리는 유지.
- frontend22 tests/ESLint/typecheck/build 성공(기존 chunk 크기 경고만 유지). 실제5182 브라우저 다운로드 파일과 원본 asset 바이트 동일 확인. 내려받은 파일 재선택 시 실제 parser 통과/날짜 팝업0개/가져오기 완료 상태 확인, DB 저장 실행 없음. 다운로드 원본 SHA256 07fa96a1e5e24e49e519c9c36944e8f902fbcd30f094d21aa9d23ba21869cbce.
- 새DB/임시DB/미사용DB 없음. 기존 검수DB 보존, 로컬 커밋만 진행. 원격/운영 배포 없음.

## 2026-10-11 G2 홈 3종 디자인 비교 시안

- 사용자 요청: 최근 작업일 기준 G2 홈 시안 3개, 서로 다른 구성. 생산/납품 달성률, 180대 미만 자재, 제품 안전재고180대 및 납품 후 전망, 기준일 불량률 표시. 출근은 별도 항목 없이 생산 영역의 작은 요약으로 표시.
- 앞서 제시한 공식 화면을 바탕으로 1안 네이버웍스 요약 영역+Power BI 게이지, 2안 Zoho의 주영역/우측 목록+Salesforce 바늘형 게이지, 3안 이카운트 연속 표+Odoo 재고 보고서 구성. 공통 OsanShell/청주 색감 재사용, 시안 사본에서 G2 홈 선택 상태만 조정.
- 산출물: /Users/parksubin/.codex/visualizations/2026/10/08/01a118d1-afc8-7351-9370-0126a196e572/g2-home-mockups/index.html, home.css, home.js, home-01/02/03.png. 시안 전용 localhost8778, session66423. 앱5182 및 backend/DB 미변경. 예시값만 사용.
- 예시 가정: 생산160/계획180, 납품150/150, 신규불량4/총생산160=2.5%, 현재고420에서 예정납품120씩3회 차감, 10/14 잔여60으로180미만. 생산 실적 분모 정의 및 미래 생산 포함 여부는 미확정이며 시안에서 명시, 업무 계산정책 확정으로 해석하지 않는다.
- 공식 출처: https://help.worksmobile.com/ko/admin-guides/manage-service/attendance/attendance-home/ ; https://www.zoho.com/inventory/help/reports/dashboard.html ; https://www.ecount.com/kr/ecount/product/inventory_location-management ; https://www.odoo.com/documentation/18.0/applications/inventory_and_mrp/inventory/warehouses_storage/reporting/forecast.html ; https://learn.microsoft.com/en-us/power-bi/visuals/power-bi-visualization-radial-gauge-charts ; https://trailhead.salesforce.com/content/learn/projects/build-an-app-to-track-your-trailblazer-journey/create-a-report-a-report-chart-and-a-dashboard . 가이드 https://help.tableau.com/current/pro/desktop/en-us/what_chart_example.htm . 원본과의 차이는 G2 항목/공통색/출근 요약/180기준 및 생산 미반영 전망이다.
- 브라우저에서 3안 전체 화면, 시안 링크 전환, 390px 가로 넘침 없음 확인. 모바일 세부 시안 확정은 별도. 디자인 선택 대기, 실제 G2 홈 기능 구현/DB 연동/배포 없음. 신규DB/임시DB/미사용DB 없음, 기존 검수DB 보존.

## 2026-10-11 G2 홈 시안 재작성: 요소 자체를 구분

- 사용자 피드백: 기존3안은 배치 차이만 있으므로 각 요소 디자인도 모두 다르게 재작성 요청. 기존 시안 승인이 아니며 선택 대기 상태 유지.
- 기존공통 shell/색만 재사용하고 revised.js/revised.css에 3개 별도 마크업 작성. 1안 원형 진행/중앙불량률/무채움 점선 재고/품목 카드, 2안 바늘게이지/가로불량 요약/현재고-납품=예상재고 수식과 계단면적/조건부서식 표, 3안 큰 달성률 숫자/불량수량 대비 생산수량과 비율/납품일별 잔여재고 목록/번호와 수량을 정렬한 자재 목록. 출근은 모두 생산 아래 작은 글씨.
- 앞서 확인한 공식 Power BI/네이버광고 카드, Salesforce 게이지, Zoho 품목별 요약과 목록, 이카운트 안전재고표, Odoo 재고요약/그래프를 표현근거로 사용. 원형은 공식 재고 대시보드의 도넛 표현을 달성/잔여 비율로 조정. 새 계산정책 없음. 샘플값/예측가정 유지.
- localhost8778/?view=1&revision=2 에서1~3전환. 캡처 home-v2-01/02/03.png는 기존 시안과 별도 보존. PC3개/390px3개 직접 관찰, 레이블 줄바꿈 보정, 콘솔오류없음. PC 디자인 선택용이며 모바일 세부 확정 아님.
- 실제 앱/DB/기존runtime 미변경. 신규DB/임시DB/미사용DB 없음. 작업기록 local commit만, 원격/배포 없음.

## 2026-10-11 초기 홈 1안 추가: 4종 비교

- 사용자 요청에 따라 최초 균형 요약형을04번으로 복원, 현행01~03 디자인 보존. compare.js가04에 기존 home.css/복원 original.js,01~03에 revised.css/js를 선택. 상단4개 링크/선택 상태/본문 전환 브라우저 확인, 콘솔오류 없음. localhost8778/?view=4&revision=3, home-four-original.png.
- 공통 틀/제품 정책/실제앱/DB 변경 없음. 신규DB/임시DB/미사용DB 없음. 작업기록 로컬커밋만, 원격/배포 없음.

## 2026-10-11 4안 자재 목록 밀도 보정

- 사용자가4안을 선호하고 자재 행 높이 축소 및 재고 부족분/생산 가능 대수 병기를 요청. 기존4안 카드/색/나머지 요소를 유지하고 자재 목록만36px 행의3열 표로 변경. original.js/home.css 수정, revision4. 기존01~03 유지.
- 부족분은180×1set소요량−현재자재재고, 생산 가능 대수는현재재고÷1set소요량의 정수부로 예시 표시. A/B/C 소요량2/4/1개 및 현재고240/600/170개는 합성값으로 명시. 실제 데이터/계산 코드 연결 없음.
- PC1440px 및 기본 좁은 창에서 열/숫자 겹침 없이 직접 확인. home-four-compact.png 캡처. 시안만 수정, 실제앱/DB/배포 미변경. 신규DB/임시DB/미사용DB 없음.

## 2026-10-11 4안 차트 색상 보정

- 사용자 지시로 평균 생산 실적을 통한 전망 계산은 추후로 보류. 평균 기간 미확정, 실제 평균/예측 계산 구현 없음.
-4안 생산 게이지 파랑(#477DC0), 납품 게이지 청록(#278C82), 재고 전망 파란 선/점+옅은 파랑(#EDF3FB) 채움 적용. 미달 지점 빨강 유지, 기존 시안 레이아웃/데이터 유지. revision5 및 home-four-color.png. PC 화면 직접 확인 완료.
- 실제PMS/DB/배포 미변경. 신규DB/임시DB/미사용DB 없음.

## 2026-10-11 생산·납품 게이지 90% 색상 기준

- 사용자 요청: 생산/납품 공통으로90%미만 EMI빨강(#d9232d),90%이상 청록(#278C82). original.js 달성률 조건으로 적용. revision6, 현재예시88.9%빨강/100%청록 직접 시각 확인(home-four-threshold.png). 재고 그래프 유지.
- 시안만변경. 평균생산 예측 보류 유지. 신규DB/임시DB/미사용DB 없음, 실제PMS/배포 변경없음.

- 2026-10-11 추가 보정: 생산/납품 실적 수량에도 게이지와 동일한90%조건 색상 적용(미만 EMI빨강,이상 청록). 계획수량 기본색 유지. revision7, home-four-actual-color.png PC시각확인. 시안만변경, 신규/임시/미사용DB 없음. 실제앱/배포 미변경.

- 2026-10-11 위치 교환: 사용자 요청으로 생산/납품 게이지 내부에 실적/계획 수량과 설명, 아래에 달성률% 배치. 기존90%색상 기준과 실적숫자 색 보존. 접근성 레이블에 실적/계획 포함. revision8 및 home-four-swapped.png PC시각확인. 실제PMS/배포 변경없음, 신규/임시/미사용DB 없음.

- 2026-10-11 수치 표시 간소화: 게이지 내부 실적수량만 큰 숫자로, 아래에 달성률% (계획 N대) 한줄표시. 실적/게이지90%색상 유지. revision9, home-four-actual-only.png PC직접확인. 실제앱/배포 및 DB변경 없음, 신규/임시/미사용DB 없음.

- 2026-10-11 게이지 실적 단위 추가:160대/150대로 표시하고 사용자 후속 요청에 따라 단위 대만 검정(#282828), 숫자는90%조건 색상 유지. revision11, home-four-black-unit.png 직접 확인. 시안만 변경, 신규/임시/미사용DB 없음. 실제앱/배포 미변경.

- 2026-10-11 실적 숫자와 검정 단위 대 사이 SVG간격5 추가. revision12, home-four-unit-spacing.png 직접확인. 시안만변경, 신규/임시/미사용DB 없음.

## 2026-10-11 확정 G2 홈 로컬 이식

- 사용자 승인:4안 revision12 코드 그대로 로컬 구현, 기존 홈 내용 전부 삭제. 확정 시안 markup/SVG/CSS를 React로 이식하고 기존 OsanMenuHeading·공통셸 사용. 기존 홈 전용G2Charts/HomePreview/ProductionSummaryTable 및118개 미사용 스타일 규칙 삭제. 기존 홈 기능 테스트17개를 새 계약 검증으로 교체하고 출근 공용 검증은 보존.
- 조회 전용 홈: 생산/납품 달성률90%미만 EMI빨강·이상 청록, 큰 실적 숫자와 검정 단위5간격, 달성률(계획) 아래표시. 출근 작은 요약, 신규불량/생산량, 자재180대미만·부족 자재수량, 등록 납품계획 차감 전망. 평균생산 추가는 보류 유지. 입력기능/API쓰기 없음.
- 기존청주 API 연결. 서버 today 이전 최근작업일(일요일/등록공휴일 제외,토요일 포함; 명시 생산계획/생산실적 작업 예외)은 미입력이라고 과거 입력일로 건너뛰지 않음. 납품 조회기간은 서버today+30일까지 화면에 명시. 자재실사는 동일기준일, 전부빈 품목은 미입력안내/계산제외, ERP 등 부분빈칸은 공용자재계산과 동일처리. 실제 평균/예시수량 주입 없음.
- 독립review home_review 요청모델gpt-6-astra/high, 실제모델NOT_REPORTED. ERP부분빈칸 제외·빈카탈로그 정상표시 P2 두건 수정 및 반례테스트 추가; 재검토 새P1/P2없음.
- typecheck/대상ESLint/build 성공(기존 큰chunk 경고). 새홈·기존출근·메뉴이동·생산·자재 5파일45tests 통과. 실제5182/g2 조회(최근작업일10/10 미입력,자재92품목전부미입력) 확인, 예시데이터는 별도임시페이지로PC1232콘텐츠폭/390px 확인 후 임시2파일삭제. 브라우저콘솔error없음. 캡처 local-home-populated.png / local-home-live.png. 전역스타일 간섭은 기존승인메뉴의scope reset방식으로 격리.
- 기존5182/5090 runtime·DB 보존. 신규DB없음,새임시DB없음(기존검수DB보존·정리미실행),미사용DB없음. 실제DB데이터수정/삭제없음. 사용자검수대기,원격push/merge/배포없음.

## 2026-10-11 G2 네 메뉴 공통 페이지 프레임

- 사용자 승인: 홈·생산/출하·출근·자재의 공통 부분을 하나로 묶고 중복 코드 삭제, 업무 콘텐츠만 개별 유지. 홈 미달 자재 열은 품목명/규격/재고 부족분/생산 가능 대수.
- G2PageFrame에서 기존 OsanMenuHeading을 재사용하고 배경·바깥 여백·제목/설명·제목 옆 action·topbar 폭 보정 공통 소유. 각 메뉴 제목 markup과 page padding/background/topbar 중복 삭제. 모바일 생산은 공통 frame fitMobile 옵션으로 화면 높이 유지. 자재 모바일 중복 내부 좌우 여백 제거. 홈 고아 CSS 접두사와 미사용 시안 외곽 규칙 제거.
- API/권한/DB/계산/저장 동작 변경 없음. 새 기능·재디자인 없이 확정 콘텐츠 재사용.
- typecheck, 대상 ESLint, build 통과(기존 큰 chunk 경고만). 기존5파일45tests 통과. 실제5182에서1440x1000/390x844 네 메뉴 확인: desktop 제목 x232/y147, mobile x20/y79; 공통 흰색 배경, page 가로 넘침 없음. 출근 가로 월별 표는 기존 내부 scroll 유지. 모바일 생산 document height844로 viewport 내 달력 유지, 데이터 입력 메뉴 열림 확인. 콘솔 error없음. /tmp/g2-common-frame-home.png 캡처.
- 독립 g2_frame_review: 요청 gpt-6-astra/high, 실제모델 NOT_REPORTED. 확정 P0-P2없음. 모바일 자재 중복 padding 우려 보정 후 추가 CSS 검토에도 새 Finding없음.
- 로컬 구현/검증 완료, 사용자 검수 대기. 원격 push/merge/배포 없음. 기존5182/5090 runtime 보존.
- 신규 DB 없음, 새 검증용 임시 DB 없음(기존 검수 DB 보존·정리 미실행), 미사용 DB 없음. DB 데이터 변경 없음.

## 2026-10-12 확정 제조 인원관리 PC·모바일 로컬 이식

- 사용자 PC/모바일 시안 확정 및 코드 그대로 로컬 구현 승인. PC 세로 날짜 표/최근 입력일 인원 요약, 모바일 달력과 세로 가운데 데이터/상세 팝업 총원 우측 정렬, 공통 입력 폼의 오전·오후 탭. 오전+오후 하루 총원 합산 확정.
- 기존 청주 attendance API·권한·변경필드/expectedVersion·빈칸 null/0 저장 계약 유지. 이전 상단 입력/가로표 제거. 실제 G2PageFrame/DataEntryDialog 재사용, 생산/출하에서 조회월·달력·탭 공용 추출.
- mockup의 예시값과 고정 오늘 날짜는 이식하지 않음. 로컬 검증과 독립 review 후 결과 기록 예정. 원격/배포 승인 범위 아님.

- 구현: G2AttendancePage PC 날짜 세로 표와 최신 입력일 요약, mobile 달력/상세와 AM·PM 입력 폼. G2SharedControls의 월선택/월제목/달력/탭 및 useG2Mobile을 기존 생산출하와 공동 사용. 기존 G2ManagementPages 인라인 편집/가로표 코드 및 사용 종료된 입력 전용 CSS 삭제. 예시 전용 shell은 실제 G2PageFrame/Osan 공통 shell로 대체.
- 검증: typecheck, 변경파일 ESLint 경고0, build 통과(기존 대형chunk 경고). G2Pages/G2OperationsPage/G2Navigation 3파일22tests 통과. 저장 변경필드 expectedVersion, null/0, 탭draft, conflict 보존, 중복제출 방지, 읽기권한/runtime, 미래blank, 늦은 날짜응답, 403/retry 검증. 초기 오류 테스트 생성인자 보정 후 통과. 첫 build는 sandbox 출력경로 권한으로 실패했고 승인된 escalation으로 동일 build 재실행 성공.
- 시각 검증: 실제5182/g2/attendance PC1440×1000 표 가운데정렬/월선택/입력폼, 390×844 달력·상세 총원우측·폼 확인. PC title x232, 가로넘침 없음. 생산출하390px 공유달력/입력메뉴/오후탭 회귀 화면 정상, 두메뉴 console error없음. 실제 로컬 날짜의 미입력 상태 조회; 실DB 쓰기는 하지 않고 저장계약은 mock API 자동테스트로 확인. 캡처 attendance-local-pc.png (시안 폴더).
- 독립 attendance_review: 요청 gpt-6-astra/high, 실제 모델 NOT_REPORTED. data-row 누락에 따른 PC 정렬과 월초 테스트 미래날짜 P2 2건 보정. 수정분 재검토에서 기존2건 해소/새 P0–P2없음. 마지막 PC totals 8px 간격은 공통리셋에 유지되도록 !important 보정.
- 상태: 로컬 구현/관련 검증 완료, 사용자 로컬 검수 대기. 기존5182/5090 source/runtime 유지. 원격push/PR/merge/배포 없음.
- DB: 신규DB 없음, 새 검증용 임시DB 없음(기존 검수 DB 보존, 정리 미실행), 미사용으로 전환된DB 없음. schema/migration/실제데이터 변경 없음.

## 2026-10-12 G2 모바일 입력 폼 내용 높이 적용

- 사용자: 모바일 입력 팝업의 불필요한 빈 공간 원인 확인 후 전체 G2 입력 폼 내용 높이/버튼 바로 아래/작은 화면·키보드에서는 본문만 스크롤 방식 승인.
- 공통 g2-mobile-input.css 및 useG2InputViewport를 생산·출하/출근 DataEntryDialog form과 자재 실사 입력에 연결. 860px 이하만 적용, PC/상세/월선택/입력메뉴는 보존. 기존 확정 색감/필드/탭/버튼과 저장·권한·DB 계약 변경 없음. 원본 승인 snapshot은 보존하고 새 승인 변경은 단일 공통 파일에서 소유.
- 검증: G2Pages/G2OperationsPage/G2MaterialsPage 3파일 43tests 통과, 변경 TS 파일 ESLint 및 typecheck 통과, build 통과(기존 대형 chunk 경고), git diff --check 통과.
- 시각 검증: 실제 로컬 5182에서 제조 인원 입력 390×844 내용 높이 약442px, 자재 실사 입력 390×844 내용 높이 팝업 확인. 생산 실적 390×400에서 본문 스크롤과 하단 버튼 노출 확인. 실제 모바일 OS 키보드는 미검증이며 visualViewport 이벤트 대응 구현과 짧은 viewport 검증을 구분함. 캡처: g2-home-mockups/attendance-mobile-compact-input.png. 실데이터 입력/저장 없음.
- 독립 attendance_review: 변경4파일 검토 확정 P0–P2 없음. 저장·권한·자동저장 계약 보존 및 viewport listener 정리 확인.
- 상태: 로컬 구현/관련 검증 완료, 사용자 검수 대기. 원격 push/PR/merge/배포 없음.
- DB: 신규 DB 없음, 새 검증용 임시 DB 없음(기존 검수 DB 유지, 정리 미실행), 미사용 전환 DB 없음. schema/migration/실데이터 변경 없음.

## 2026-10-12 청주 왼쪽 공통 메뉴 글자 중심 구성

- 사용자: 레퍼런스 1번 이카운트 글자 중심 메뉴 선택. 청주 desktop 공통 메뉴의 장식 아이콘 제거, 기존 계층·들여쓰기·선택 배경 보존, 일반 글자와 선택 글자 굵기 구분. 메뉴 이동/권한/오산/모바일 메뉴는 변경하지 않음.
- 실제 사례: https://www.ecount.com/kr/ecount/product/erp_customizing 의 My Page 공개 화면. 아이콘 없는 텍스트 목록과 선택 강조를 따르되 이카운트 파란색/그라데이션 대신 확정된 EMI 색을 유지. 보조 제작 가이드: https://www.carbondesignsystem.com/building-blocks/foundations/icons/guidelines (아이콘과 글자의 일관성). 레퍼런스는 직전 대화에서 실제 이미지 확인/제시 후 선택받음.
- 사용 종료된 CheongjuNavigationIcon 구현 삭제. 펼침 화살표/알림 배지는 기능 표시로 유지.
- 검증: App.navigation/G2Navigation 9tests 통과, typecheck/ESLint/diff-check 통과. 실제5182 청주 메뉴 펼침 및 생산/출하 페이지 이동·선택배경 확인. 메뉴 장식 SVG 0개, 일반500/선택750 글자굵기 확인. 메뉴 권한/이동 계약 변경 없는 작은 가역 UI 수정으로 직접 검증. 캡처 cheongju-text-navigation.png.
- 상태: 로컬 구현·검증 완료, 사용자 검수 대기. 원격push/PR/merge/배포 없음.
- DB: 신규/새 임시/미사용 전환 DB 모두 없음. 기존 DB 유지, 정리 작업 없음.

## 2026-10-12 큰 메뉴 동그라미 표시

- 사용자 후속 승인: 청주 왼쪽 공통 메뉴의 큰 메뉴 앞에만 동그라미 추가. 상위 app-nav-button label에 5px 단색 원을 표시하고 하위 메뉴·분류 제목은 텍스트 유지. 선택 시 기존 EMI 글자색을 함께 따름. CSS만 변경, 메뉴 이동·권한·DB 변경 없음.
- 검증: 실제5182 desktop G2 펼침 상태에서 상위 메뉴 원/하위 메뉴 미표시와 선택색 확인, diff-check 통과. CSS 장식만 변경하여 자동테스트 재실행 생략. 캡처 cheongju-menu-dots.png. 로컬 반영만, 배포 없음. 신규·임시·미사용 전환DB 없음, DB 정리 없음.

## 2026-10-12 누적 변경 병합·공개배포 준비

- 사용자: 전체 변경사항 확인 후 `좋아. 이거 병합하고 공개배포 하자.` 명시 승인. 공지 내용·점검 팝업에 대한 자동 승인 검토 거절 후 구체 안내했고 `점검시간은 상관없어. 그냥 배포해` 추가 승인. 기존 승인된 PC·모바일 G2 4메뉴 및 청주 공통셸 범위.
- 운영 공지 제목 `2026.10.12 업데이트 공지`, G2 홈/생산·출하·인원/자재/청주 메뉴 안내, 저장 제한·서버 교체 일시 접속 중단 안내, `박수빈 올림`. 기존 maintenance job의 prepare-only로 청주·오산 공지/별도 팝업 등록 `ANNOUNCED` 성공. 이 시점에는 저장 제한 활성화·서버 중단·migration·배포 없음.
- 최종 독립 검토의 P2: 자재 retention worker가 maintenance lease를 사용하지 않아 점검 중 삭제 가능. 청주 lease를 prune 전체 동안 보유하고 차단 시 당일 완료 표시 없이 재시도하도록 보정. 합성 DB에서 Failed 상태 삭제0, 해제 후 삭제/6개월 경계 보존, 진행 중 prune을 exclusive drain이 기다림 확인. G2MaterialTests 38/38, G2MaterialDatabaseTests 5/5 통과. 생성한 disposable DB/container/network/storage 정리 완료.
- 배포 품목 이미지: 기존 승인 빈 양식 asset을 backend resource로 포함하며 ImageRoot 미설정 때만 인증된 기존 image endpoint에서 읽음. 명시 ImageRoot 우선/DB ImageKeys 경계 보존. 새 저장소/실사자료 포함 없음. compiled assembly 이미지47개 키·SHA256가 private 원본과 모두 일치; 단위검증1/1 통과. 독립 검토 확정 P0–P2 없음.
- 운영 최초 92품목 등록 경로 준비: 동일 내장 빈 양식에서 metadata만 추출하는 청주전용 CLI 및 명시적 배포 옵션. 현재 배포의 저장 차단 상태·청주 DB identity/실제 이름·migration 원장·빈 catalog·배타 잠금을 확인한 transaction에서 등록. 실사수량 쓰기/기존품목 덮어쓰기 없음. 실패 시 배포 차단 유지, 후속 배포 기본값 false. 코드/합성 검증 진행 중, 실제 운영 등록 미실행.
- 기존 Azure PostgreSQL 복구 read-only preflight 통과. 공지/배포별 민감한 실행 설정과 복구 증거는 저장소 밖 private 폴더에서 관리. 신규 영구DB/미사용 전환DB 없음. 임시 검증DB는 정리하며 기존 로컬 검수DB 유지.
- 현재 상태: 로컬 보정·배포 도구 검증 및 독립 검토 진행 중. 아직 원격 push/PR/main 병합/공개 앱 교체 없음. 최종 검증/원격/배포 결과는 아래에 이어 기록.

- 배포 보정 최종 검증: backend build 성공, 내장양식92품목 테스트 및 private기준 전체metadata digest 비교 통과, 신규 catalog disposable DB 2/2 및 retention DB 5/5 통과. 이미지47개 전체hash 검증 통과. runner 합성147/147, shellcheck/actionlint/diff-check 통과. macOS 기본 LibreSSL의 CMS 복호화 검증 실패는 요구된 OpenSSL3으로 실행해 해결했으며 제품 암호화 로직을 낮추지 않음. 테스트용 DB/container/network 모두 정리.
- 독립 g2_release_review(요청 gpt-6-astra/high, 실제 NOT_REPORTED): 기존 retention P2 해소, 이미지/최초등록CLI/배포runner 추가 확정 P0–P2없음. 실제 DB 및 runner 검증 통과 조건 충족. 사용자 검수 완료 누적 변경과 이 배포 선행 보정을 한 PR로 올리고 필수CI를 최종 회귀로 실행.

- PR #169 / 첫 최종 CI `38154176539`: Frontend 및 Workflow Validation 통과. Backend/Full-stack는 기존 테스트용 ZXing ImageSharp binding의 전이 의존성 `SixLabors.ImageSharp 3.1.11`에 새로 보고된 NU1902/NU1903이 warning-as-error로 차단. 검사 억제 대신 기존 Magick.NET과 ZXing.Net 직접 참조로 테스트의 이미지 fixture/QR 픽셀 해독을 유지하고 취약 의존성을 제거하는 보정 진행. 운영 앱 코드·업무 계약 변경 없음. 공지/팝업은 Announced 유지, 저장 차단/실제 배포 미실행.
- CI 보정 완료: 테스트만 ImageSharp binding을 제거하고 Magick.NET/RGB24 + ZXing.Net 0.16.11로 전환. PNG RGBA8·메타데이터 없는 fixture와 업로드/다운로드 원본bytes 비교 및 QR실제픽셀/정확URL 검증 유지. restore `-warnaserror` 통과/의존성그래프 ImageSharp없음, QR2/2·사진형식/손상2/2·세DB 통합1/1 통과. Busbar2건은 로컬 전용DB설정 부재로 skip(성공아님), 기존CI 전용harness 확인 대상. 관련일회용DB/container/network 정리 완료. 독립review 추가P0–P2없음. 제품코드/권한/운영데이터 변경없음.

- 두 번째 CI `38155220087`: Frontend·Workflow Validation 통과. Full-stack 64개 중63개 통과, G2 1개가 폐기된 구형 홈/입력 selector를 참조해 실패. 제품 화면을 되돌리지 않고 승인된 새 홈·생산/출하·출근·자재 PC/390px 모바일 흐름으로 해당 테스트를 갱신했다. 기존 권한403·경쟁200/409·미래실적400/null·desktop 실적5칸 공란·실사0→4→null(version3)→6(version4) 계약을 유지했다. catalog는 loopback/E2E DB·Compose 경계를 검사한 전용 DB에92개 합성값만 준비한다.
- 보정 검증: 집중 full-stack1/1(28.6초), ESLint·TypeScript·diff-check 통과. PC/모바일 화면7개 직접확인, 콘솔오류/가로넘침 없음. 최종 임시DB `emi_qms_e2e_20261011165603_56260_10529`와 소유container/network 정리 완료. 작성자와 별도 reviewer가 계약·diff 확인. 전체 원격CI는 후속commit에서 재실행하며 아직 병합·실제배포 완료가 아니다.
- 배포 복구 준비: 기존 자동Full 백업은UTC23:40대 완료되어 현재작업창에맞지 않으므로, 기존SOP의 VerifiedLogicalDatabaseBackup 경로와 이미 승인된 동일Mac 복구폴더·키·ClamAV 제한relay를 사용하도록 준비. PG16.14 고정image·OpenSSL3·키암복호화·TLS·용량·기존 승인오산 예외snapshot 확인. relay는 준비검사 후정상종료. DB 전체크기 메타데이터만조회했으며 실제dump/운영migration/앱교체 미실행. 신규영구DB/미사용전환DB 없음.

- 세 번째 CI `38157746410`: Frontend/Workflow Validation/Full-stack64개 및 추가 사업장분리2건 통과. Backend1108개 중1106통과/2실패: 승인된0133의 자재2테이블이 BusinessSchemaOwnershipTests 청주 기대목록에서 누락됨. 엄격한 전체목록 Assert.Equal을 유지하고 g2_material_counts/items 두 literal만 추가, 제품/migration/다른사업장 계약 변경없음. 실패2건 집중DB검증2/2(44초)/diff-check통과, 독립reviewGO. 임시DB `emi_qms_e2e_20261011174525_71495_3622` 및 각사업장fixture DB/container/network 정리확인. 후속commit에서 필수CI 재실행. 아직병합/운영migration/앱교체없음.
- 검증대기에 따른 점검창갱신: 기존운영image/source 일치·readiness·공개200/401/401을 확인하고 지원CLI로 기존안내를 사업장별 종료한 뒤 동일본문의 popup-only 새release를 준비. 기존영구공지를 보존하며 새3시간안내를 검증후private 실행설정으로승격. 실행결과는 private window-refresh-pending/log에 보관; 일반배포runner는 새안내양쪽 일치확인 전 진행하지않음.


## 2026-10-12 병합 완료와 첫 운영 배포 복구

- 최종 PR CI `38161171433`: Backend1108/1108, Full-stack64/64 및 사업장 경계2건, Frontend/Workflow Validation/CI Gate 통과. 사용자 병합·공개배포 승인 범위에서 PR #169를 정상 merge했고 main `5e181b02a79a834fd30b4334165e442d1f7c8228`의 CI `38164710620` 성공 확인. 검증 완료 PR과 동일 tree 재사용 정책 적용.
- 첫 운영 release `944c9f3a-3087-4e5e-884a-c14d80fc8078`: 저장 차단·요청 drain 후 논리 백업 및 일회용 복원검증 진행. 약30분 공통 예산 경계에서 checkpoint wait `AZURE_READ_TIMEOUT`, driver `RESTORED_INVENTORY_FAILED`로 실패, exit79. 원본 3DB dump와 로컬 복원 이후 inventory 단계였으며 오류는 timeout과 SQL 실패를 함께 표현하므로 데이터 불일치로 단정하지 않음. candidate/verified 증거 없이 migration 전 차단된 사실 확인.
- 운영 migration/92품목 등록/신규 앱 이미지 교체는 실행되지 않음. runner의 기존 이미지 복원 및 양 사업장 maintenance fail 실행 모두 성공. 이후 정상 readiness/공개200·401·401/기존 이미지 exact ledger 검사를 거치는 complete-only 복구 adapter를 독립 reviewer가 GO 판정. 실제 정상화 결과는 후속 기록.
- 실패 실행 소유 plaintext 백업 작업폴더 및 일회용 복원 container 정리 확인. 소유 relay 정상 종료. 기존 로컬 검수 DB/runtime은 유지. 신규 영구 DB와 미사용 전환 DB 없음.
- 같은 배포 목표의 최소 운영 보정: 논리 백업에만 명시적 유한 예산 최대7200초를 허용하고 기본값1800초·Azure Full 상한1800초·공지 종료 제한·개별 명령 제한·모든 복구 증거 요구를 보존. 운영 상태 변경 없이 코드/관련 반례 검증 및 독립 검토 후 새 release/checkpoint로 진행. 실패 checkpoint나 runner 재실행 금지.

- 기존 서비스 정상화 완료: 기존 immutable 앱 이미지·Healthy/Ready·공개 health200/root401/api401 확인 후 청주 `maintenance-s3t32cj`, 오산 `maintenance-w402eir` complete 모두 Succeeded. 저장 제한 해제 완료. 실패 release 종료 후 새배포 준비 중이며 배포 완료로 표시하지 않음. 다음 실행 private 설정은 checkpoint/driver5400초, 3시간 새안내·시작전120분 잔여·fresh relay 잔여6600초 이상을 확인하도록 구성. 독립 adapter 검토 GO.

- 제한시간 보정 검증: OpenSSL3 PATH에서 checkpoint33/33 통과, logical driver21실행 중19통과·기존Docker통합2건skip(성공으로 간주하지 않음), Python구문/diffcheck 통과. 독립 g2_release_review(요청 gpt-6-astra/high, 실제NOT_REPORTED) 계약·실제diff·기존보호/신규경계 검토GO, 확정P0–P2없음. 기본예산·AzureFull·개별명령·공지끝·증거요구 보존 확인. 새검증DB 생성없음. 필요운영보정만 후속PR로 진행.


## 2026-10-12 복구 예산 보정 병합·두 번째 배포 결과

- 후속PR #170(`da2948f`) 필수 Workflow Validation/CI Gate 통과(`38168354905`). 변경분이 운영 도구뿐이므로 제품 회귀는 분류 정책상 미실행이며 앞선 #169 증거를 유지. 정상 병합main `1bb8e7a681985bc99c69d9302250b6d1393a1692`, mainCI `38168661156` 성공.
- 새release `007e9713-5679-44d9-a229-e3e3f3a591ac`를 새3시간 popup-only 안내로 준비하고 실행. checkpoint/driver5400초·fresh relay·시작전공지120분잔여를 확인. 백업수집·암호화사본실복원·identity/ledger/schema/data/권한/원본불변성·최종drain재검증까지 통과, checkpoint `VERIFIED_LOGICAL_DATABASE_BACKUP` 및 암호화backup/manifest 보관. 임시복원container/plaintext는 정리.
- Directory migration `migration-0axq0b6` 성공 후 청주 `migration-4psca0o`가 SQL파일 열거 중 UnauthorizedAccessException으로 실패. 실제 root원인: 저장소 밖 로컬 image build adapter의 umask077+Python tar data filter가 공개Git archive의 디렉터리를0700으로 만들어, USER app이 business-migrations/cheongju에 접근할 수 없었음. 제품SQL문제아님. 청주는 ApplyTargetAsync/DB연결 전 실패, 오산/품목등록/앱교체 미실행. Directory는 동일migration목록의 권한reconcile을 수행했으므로 전체시도를 DB무변경이라고 기록하지 않음.
- runner exit74/정리종료 확인 후 읽기전용 감사에서 기존 eb19a3f catalog와 D4/C131/O133 전체원장 exact·DBidentity·청주새테이블부재·runtime CONNECT/USAGE·Directory전체publictable SELECT/비DML 및 현재release Failed 확인. 권한reconcile코드는 old→main변경없음. 독립review 조건부복구GO, 감사container고정이름소유P2는 UUID/label확인정리로해소. 기존exactrevision/image활성화→ready/public→oldimagecomplete adapter실행 중.
- 수정은 공개Git archive디렉터리0755정규화뿐이며 동일main source를 별도 `-permissions1`태그/digest로 재빌드. 기존badbackend actualappUID/networknone/read-only probe에서 business2폴더접근거부재현, frontend기존이미지내부HTTP static34/assets22정상확인. 수정이미지동일probe 및기존서비스정상화확인후 새release/checkpoint정상runner로 재배포예정. 실패runner/기존checkpoint 재사용안함.

- 두 번째 실패 복구 완료: fresh read-only 감사 통과 후 기존 backend--0000070/frontend--0000061을 활성화, readiness/public200·401·401 및 기존image complete 양사업장Succeeded 확인. 사용자가 사용할 수 있는 기존서비스/저장기능 재개 완료. relay종료 및 감사container정리 확인.
- 수정 이미지 검증: 같은main1bb8e7a의 backend `sha256:1bde52073c52e1b7c20395f082a3a717de7bb6f102fc5d4e4ec955958aaeccd0`, frontend `sha256:91ef6414c727421bf7b51546979d2b82afef7cfe2db1c58f062283622d2c48a6`. backend USERapp/networknone/read-only에서141 SQL모두열거/EOF읽기, frontend UID101/networknone 내부loopback HTTP34파일(assets22포함)전부성공. probecontainer정리완료. source코드/DB계약변경없음.
- 세 번째 실행 release `58649ad1-8c10-4de1-9d66-376ad17e579f`: 새popup-only3시간안내 양사업장prepare/verify성공, freshTLSrelay/6600초잔여·공지120분잔여·기존mainCI증거확인후 새checkpoint정상runner 시작. 이미검증된 이전backup은 보관만하고 새중단구간의증거를다시만듦. 최종결과는후속기록. 공간확보를위해 본작업의 완료된중복빌드temp소스/압축본만정리했고 운영backup/기존검수DB는유지.


## 2026-10-12 공개배포 완료

- release `58649ad1-8c10-4de1-9d66-376ad17e579f` 정상 종료(exit0), 한국시간 06:46:52. 배포 source main `1bb8e7a681985bc99c69d9302250b6d1393a1692`, 앞서 기록한 permissions1 immutable 이미지와 일치. backend--0000071/frontend--0000062 latest=ready/Succeeded. 이후 기록 commit은 운영 이미지 source와 구분.
- 새 암호화 백업 실제 복원/전체 검증/최종 drain 통과 후 Directory/Cheongju/Osan migration 성공. 청주 catalog job migration-fbk3tct 성공, 빈 양식 metadata92개 등록, 예시 실사 수량 입력 없음. 양사업장 점검 종료 maintenance-7n3dxmq/maintenance-y1esvn6 성공, 저장 제한 해제.
- runner Migration/Backend/Frontend/PublicSecurity PASS. 별도 읽기 전용 감사: identity/전체원장 D4/C134/O133/runtime CONNECT·USAGE/Directory SELECT·비DML/catalog92/양사업장 해당release Completed 확인. 공개 https://pms.emiinc.co.kr/health/live 200, root·/api/me 비로그인401. /health는 공개health 경로가 아니며401; 최종 검증은 /health/live 사용.
- 한계: 운영 로그인 후 실제 데이터 입력은 미실행. 승인된 로컬 PC·모바일 검수, PR169 backend1108/1108/fullstack64/64+경계2건, PR170 운영도구 CI, 운영 배포 후 검증을 구분함.
- 일회용 복원DB/container, 평문 임시폴더, 이미지probe/감사container 정리. 최종 TLS relay exit0, 완료3회 실행의 임시 자격정보JSON 제거. 암호화 backup/manifest와 비민감 증거 보존. 기존 검수DB/runtime/사용자WIP 유지.
- DB 보고: 신규 영구DB 없음. 기존 청주DB에 g2_daily_values/g2_daily_notes/g2_material_items/g2_material_counts 적용. 검증용 Directory·청주·오산 일회용 복원DB는 소유container와 함께 정리 완료. 미사용 전환DB 없음. 기존 다른 검수DB는 정리 대상에서 제외.
- 승인된 기능 병합·공개배포 완료. 두 차례 실패는 기존서비스 복구 후 보정했고 최종 성공으로 종료. 운영 배포 잔여 작업 없음.


## 2026-10-12 자재 관리자 입력 권한 누락 보정

- 사용자 요청: 관리자는 모든 메뉴 업무 데이터를 입력할 수 있어야 한다는 규칙을 지속 적용하도록 명시하고, G2 자재의 누락을 수정. 현재 기준 b5de757, codex/unified-osan-shell. 이번 요청은 수정·관련 검증이며 새 운영 배포는 미실행.
- 원인: 자재 CanEdit가 활성 자재팀 소속만 검사하고 local system-administrator 역할을 누락. 화면 canEdit, Excel import, Save의 동일 판정 지점 보정. 일반 타부서 조회 전용, 활성계정·사업부·점검·값 검증 유지. migration/DB신설 불필요.
- Root AGENTS에 관리자 예외와 실제 역할 기반 성공/거부/회수 반례 검증을 의무화. 관리부서와 관리자 역할을 구분. 구현 및 독립 검토/검증 결과는 아래에 추가.

- 구현: G2MaterialStore의 공통 CanEdit를 활성 사용자 + (선택 청주DB의 실제 system-administrator 역할 또는 활성 자재팀)로 보정. Read의 canEdit/Excel import/Save가 동일 판정. PC·모바일은 해당 서버응답을 사용하므로 화면 코드 변경 없음.
- 검증: G2Material 집중 backend48/48(실패·skip0), G2MaterialsPage frontend22/22, diff-check 통과. 영업부 실제 관리자/비활성 부서의 활성 관리자 저장 성공, 저장자 기록, 비활성 사용자/역할 회수/무권한 관리부 거부 및 값 불변 확인. frontend는 PC저장/Excel 가져오기/모바일 자동저장·조회전용 기존 흐름 검증. 운영 로그인·실데이터 저장은 미실행.
- 독립 material_admin_review: 요청 gpt-6-astra/high, 실제모델 NOT_REPORTED. b5de757 기준 WIP 코드·계약 검토GO, P0–P2없음. 테스트 결과는 부모가 최종 확인.
- DB: 신규 영구DB 없음, migration 없음. 전용 emi-qms-e2e-g2admin20261012 컨테이너의 emi_qms_e2e_g2admin20261012 및 emi_qms_e2e_50ed61765d/emi_qms_e2e_d1ecebed37 접두사의 directory·cheongju·osan fixture6DB 사용. fixture자동삭제 확인 후 소유container/tmpfs 삭제로 전부정리. 기존 검수DB/runtime 유지, 미사용 전환DB 없음.
- 상태: 규칙 추가/로컬 구현/검증/독립 검토 완료. 이번 권한 보정의 원격push·병합·공개배포는 미실행.


## 2026-10-12 최신 실사일 조회와 날짜 즉시 전환

- 사용자 요청: 홈 미달 자재는 최신 실사일, 자재 첫 진입도 최신 실사일로 조회, 날짜 선택 즉시 조회. 기준70c0aef. 최신은 유효 보관기간 내 count_date의 최댓값이며 과거 실사 수정시각과 무관. 저장 이력이 없으면 홈은 데이터없음, 자재는 오늘 빈표. 전체 빈칸으로 저장한 실사도 최신 저장일로 유지.
- 서버 G2Read/청주DB 범위의 latest 조회를 두 화면에서 재사용. 자재 PC조회버튼 제거, 기존 날짜선택 UI 재사용. 미저장 PC 확인/모바일 자동저장잠금 및 늦은응답차단 유지. 생산·납품의 최근 작업일 산정은 유지, 자재만 독립된 실사일 표시. 새 디자인/migration/운영데이터 변경 없음.

- 구현·검증 완료: backend G2Material50/50(실패·skip0), frontend G2MaterialsPage/G2HomeDashboard/G2Navigation37/37, TypeScript/변경파일ESLint/diff-check 통과. 최초 최신일과 저장대상 일치, 미저장 취소, 최초 응답보다 늦게 도착한 요청 무시, 생산일과 다른 자재실사일을 검증.
- 시각 검증: 실제 컴포넌트를 임시 합성 프런트5193에서 PC1440×960/모바일390×844로 렌더링. 실사일10/7 최초선택, PC10/8선택 즉시조회/조회버튼0, 홈 생산10/10과 자재10/7 분리 확인. /tmp/g2-latest-{pc,mobile,home}.png 직접열람. 운영로그인/실데이터 쓰기는 하지 않았으며 전체 공통셸이 아닌 해당 컴포넌트 합성 검증임. 기존 디자인 변경 없음. 임시검증 진입파일 제거/서버 종료.
- 독립 material_admin_review: 기존 reviewer 맥락을 재사용하되70c0aef부터 실제9개 변경경로와 계약을 새로검토, GO/P0–P2없음. 요청 gpt-6-astra/high, 실제모델 NOT_REPORTED. 최종 테스트결과는 부모확인.
- DB: 신규 영구DB/migration/미사용전환DB 없음. 새검증용 emi-qms-e2e-g2latest20261012 컨테이너/emi_qms_e2e_g2latest20261012 기본DB와 emi_qms_e2e_{0b31a143c2,ce5b1b482c,d7624a89cf}_{directory,cheongju,osan}9개fixture 사용. fixture자동삭제 및 소유container/tmpfs제거로 모두정리. 기존검수DB/runtime보존.
- 로컬 구현/검증/검토 완료. 관리자 보정과 이번 최신일 변경은 아직 공개배포하지 않았음.

## 2026-10-12 후속 배포 범위 추가: 청주 G2 전용 일반 메뉴

- 사용자가 관리자 보정·최신실사 변경의 병합/공개배포를 승인한 뒤, 청주 G2 외 메뉴는 준비중으로 진입차단하고 첫화면을 G2로 변경하도록 추가 요청. 이어서 관리자만 모든 메뉴 진입 허용으로 확정. 같은 PR171에 추가하여 함께 배포하며 오산은 유지.
- 청주 선택 사업부의 effective system-administrator만 메뉴 제한을 우회. PC/모바일 disabled+준비중, 직접 URL/뒤로가기는 공통 mount 경계에서 차단. 서버 capability에서도 일반 사용자 비G2 업무 API 거부. G2·휴일조회·계정/사업부전환·점검공지·공지조회·개인기기푸시·감사 인프라 유지. 기존 endpoint 권한과 selected-local-profile 경계 유지.
- 사용자 재고 수식 최종확정: 오늘 재고 = 어제 생산 + 어제 재고 + 어제 수리 - 어제 불량 - 오늘 납품. 실제 운영 source와 현재 코드가 이미 2026-09-10부터 이 방식이므로 10월12일 이후도 동일. 별도수식/migration/과거데이터 재작성 없이 기존 실사override·초기재고없음·빈실적0 계약 유지. G2InventoryCalculator 집중7/7 및 독립검토 일치 확인.
- 서버 새gate29개 최초통과, 사업부격리의 기존 일반사용자 프로젝트200 기대가 새정책403과 충돌. 성공격리 검증의 actor만 실제관리자로 바꾸고 다른사업부거부/첨부조회/데이터불변 단언 유지. ActualRole-only 사용자전환·effective role회수 반례추가 후32/32(실패/skip0)통과. 실제HTTP 일반프로젝트GET/POST403, 관리자GET200, 일반G2/휴일200 및3DB경계 실행.
- 검증DB: emi_qms_e2e_g2menu20261012 / emi_qms_e2e_g2menu20261012b와 각 실행의3DBfixture는 전용 tmpfs 컨테이너와 함께 모두정리. 신규 영구DB/migration/미사용전환DB 없음.
- 기존 PR171의63edf91 CI는 새범위 반영 전 취소. 이전운영image/backend--0000071/frontend--0000062 유지, 아직새코드병합/배포전. 최종frontend검증/시각확인/review/필수CI 및 정상no-migration배포 결과는 아래추가기록.

- 추가계약 정합화: 기존 단일DB 기능검증 환경은 server BusinessUnits.Enabled=false라 메뉴gate가 적용되지 않는데 UI가 legacy를CHEONGJU로fallback하여다르게막는것을발견. 실제선택·비legacy청주의 공통 BusinessUnitMenuPolicy를 server gate와 /me.businessUnitAccess.g2OnlyForNonAdministrators에공유. 관리자에게도캠퍼스정책true반환, effective역할예외는별도. 오산/null/legacy/미선택false; client필드를변조해도servergate유지. 기존부서별권한fullstack검증을우회하지않고원형보존하며실제3DB브라우저에신규정책검증추가.
- 해당서버보정35/35(실패/skip0)통과. emi_qms_e2e_g2menu20261012c 및 내부3DBfixture/container/network정리확인. earlier179개합성관리자기반검증은중간결과이며최종API정책기반검증결과로갱신예정.
- 최종프론트 관련고유159개통과(초기기존148 + 실제청주재조회fixture보완후 G2 11/공용mobile3 재실행,3중복), mock사업부6/6, 타입/변경파일lint/diffcheck통과. 기존App/App.navigation대량fixture수정은원복하여원형유지. 실제3DB브라우저2/2통과 및합성PC·390px에서준비중/deeplink/관리자활성/모바일문구한줄직접관찰. 전용5194합성서버/진입파일제거,5197mock/harness종료. 기존사용자WIP/runtime보존.
- 독립 material_admin_review는63edf91부터 최신실제diff와서버35/35·실제3DB2/2증거를확인하여계약/코드품질GO,P0–P2없음. 요청gpt-6-astra/high,실제NOT_REPORTED. 이전review맥락재사용하되새기준선검토. 일반사용자실3DB persona는별도dev-materialsDirectory identity/청주membership만추가,기존승인/오산부서권한시나리오보존.
- 일반3DB브라우저검증DB emi_qms_e2e_g2menubrowser20261012_{directory,cheongju,osan}/6bounded roles와소유tmpfs·process·compose정리완료. 최초5086점유시도는자원생성전차단,기존서버보존후확인된5097/5197사용.
- 운영안내갱신완료: 현재운영exactimage/ready/public검증후기존Announced release를종료하고새범위공지/팝업 f9c069bf-64f3-4dd2-99c9-17b814bf8cf2 양사업장prepare/verify성공. 새코드활성화/앱교체는아직미실행,서비스와저장정상유지. 정상runner의no-migration배포준비이며 DB구조변경없음.


## 2026-10-12 관리자 예외·최신 실사·청주 메뉴 제한 배포 완료

- 최종 계약: 실제 선택한 청주 사업부에서 일반 사용자는 G2만 이용하고 나머지 업무 메뉴는 준비중으로 차단한다. 활성 local system-administrator만 전체 메뉴에 진입한다. 청주 기본 화면은 관리자 포함 G2 홈이며, 오산과 기존 업무별 권한은 유지한다. 관리자 자재 입력 보정과 최신 실사일 조회·날짜 즉시 전환도 함께 배포했다.
- 최종 PR171 source `ecad6391cbb3def2575980572e241560161990c1`: CI `38187401602` attempt2 전체 성공. Backend 1,144/1,144, frontend unit 665/665, mock UI 73/73, full-stack 64/64, 실제 사업부 브라우저 2/2, 오산 프로젝트 1/1 통과. 첫 attempt는 일반64·사업부2 통과 후 오산 preview 시작 시 port42458 점유로 중단되어 오산 단언은 미실행이었다. Backend 완료 후 같은 SHA의 실패 job만 1회 재실행했고 성공했다. 점유 주체는 확인되지 않았으며 제품 결함이나 특정 원인으로 단정하지 않았다. 독립 reviewer도 이 제한된 재실행을 허용 가능한 검증으로 확인했다.
- 추가 로컬 증거: 전체 사업부 경계65/65 및 오산 실제3DB 브라우저1/1 통과. `emi_qms_e2e_g2menuallboundary20261012`와 내부 fixture, `emi_qms_e2e_g2osanverify20261012_{directory,cheongju,osan}` 및 소유 tmpfs/container/process/roles 정리 확인. CI의 일회용3DB 정리도 성공했다.
- 사용자 기존 승인으로 PR171 정상 병합. main `1443316991a97c57890e560e64b6c5aa005da988`, main CI `38192616117` 성공. 운영 배포 파일은 이 정확한 main Git archive에서 생성했고, 공개 소스 디렉터리0755 정규화를 유지했다. 실제 실행 사용자로 backend SQL141파일 읽기, frontend HTTP34파일/assets22 통과 후 소유 probe컨테이너를 정리했다.
- release `f9c069bf-64f3-4dd2-99c9-17b814bf8cf2` 정상 종료(exit0), 한국시간 2026-10-12 10:46:06. 기존 준비 공지를 검증하고 양사업장 activate, backend/frontend 교체, 정상 응답 확인 후 양사업장 complete 성공. Migration/bootstrap/backfill/catalog provision은 미실행이며 운영 DB 구조·재고 수량을 재작성하지 않았다.
- 최종 backend--0000072/frontend--0000063 모두 latest=ready/Succeeded. backend digest `sha256:d386ac54b37e2ec1547f88059d5e948d94142323fd5d28e00a9630cb893092a0`, frontend digest `sha256:fd0c9c329235cc23006a178fea7e84c48f16a2c0c1db6339cf7c285ec3b32318`. 별도 읽기 전용 최종 확인에서 정확한 이미지 일치, 공개 /health/live200·/401·/api/me401, 점검 종료 maintenance-p6q0hiy/maintenance-9gbivy3 성공을 확인했다. 저장 제한 해제 완료.
- 한계: 운영 로그인으로 실제 재고를 입력하는 검증은 하지 않았다. 합성 데이터 권한/PC/모바일/사업부 검증과 배포 후 준비·접속 검증을 구분한다. 추가 기록 commit은 위 운영 이미지 source와 별개다.
- DB 보고: 신규 영구 DB 없음, 더 이상 사용하지 않게 된 DB 없음. 이번 검증용 임시 DB와 소유 컨테이너는 모두 정리했고 기존 검수 DB·사용자 WIP·공유 runtime은 보존했다. 요청 범위의 구현·검증·병합·공개배포 완료.


## 2026-10-12 자재 품목 정보 클릭 영역 보정

- 사용자 확정: 품명 밑줄 제거, NO 포함 B~H 품목 정보 영역 전체 클릭으로 기존 팝업 열기. 병합 구분 A와 제조·자재·합계 영역은 제외. 같은 행의 해당 영역 hover 배경색 변경.
- 기존 g2m-item-link 밑줄 스타일 제거, 정보 셀 전면의 공통 버튼과 영역 강조로 교체. 기존 표 높이·규격 왼쪽 정렬·말줄임·원본 셀 채우기는 유지. 키보드는 행당 품명 한 곳에 진입하고 동일 강조/기존 팝업을 사용. 모바일 화면 변경 없음.
- 검증: 자재 페이지 기존26/26, TypeScript/변경파일ESLint/diffcheck 통과. 합성 데이터 실제 컴포넌트 PC1280 화면에서 밑줄 없음, 정보7개 셀 강조와 구분/수량영역 제외, NO 포함 모든7열 클릭 팝업과 구분/재고클릭 비팝업을 확인. 강조의 시각 확인은 keyboard focus 상태이며 다른 사용자의 브라우저별 hover 재현 검증은 미실행. 임시 합성 진입파일/탭 정리, 기존5182서버와 사용자 WIP 보존.
- 로컬 구현 완료. 이번 보정의 원격push·병합·공개배포는 하지 않음. 신규 DB/검증용 DB/미사용 전환 DB 없음.
