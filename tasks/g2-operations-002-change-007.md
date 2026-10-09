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
