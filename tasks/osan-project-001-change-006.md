# TASK-OSAN-PROJECT-001 Change 006 — 엑셀 양식과 프로젝트 일괄 등록

## 범위와 승인

2026-09-09 사용자가 오산 프로젝트 엑셀 양식과 업로드 기능 추가를 요청했다. 기존 PROJECT Task의 후속 change로 구현·관련 검증·로컬 커밋까지 진행한다. 원격 게시·main 병합·공개배포는 이번 기능에 대해 별도 승인 범위다. 기준선은 이미 배포된 main `f5b6f71`, 작업 branch는 `codex/osan-project-excel`이다. 앞선 배포 결과 문서 커밋 `b591036`은 별도 branch에 보존하며 이번 변경에 섞지 않는다.

## 구현 계약

- 오산 프로젝트 메뉴의 기존 신규 등록 권한으로 엑셀 업로드 화면을 연다. 양식 다운로드→파일 선택→행별 미리보기→일괄 등록 순서다.
- `.xlsx` 한 행이 한 프로젝트다. 프로젝트 Title·프로젝트 코드·거래처·PO No·W/O No·납기일·제품명·수량의 기존 8개 항목을 사용한다. PO/W/O만 선택이다. 텍스트 코드·문서 번호의 앞자리 0, 대소문자와 내부 공백을 보존한다.
- 파일 5MiB, 최대 100개 프로젝트, 파일 전체 수량 1,000개로 제한한다. 프로젝트별 기존 수량·문자 길이·코드 비교 규칙을 그대로 적용한다. 날짜는 실제 엑셀 날짜 또는 명확한 YYYY-MM-DD 입력을 사용한다.
- 미리보기는 저장하지 않는다. 오류 행·파일 오류를 표시하고, 모든 행이 유효할 때만 하나의 transaction으로 등록한다. 기존 코드/파일 내 중복, 동시 생성 충돌과 중간 실패는 전체 rollback한다. 기존 프로젝트 수정·진행 이력 가져오기는 포함하지 않는다.
- 서버는 실제 선택 사업부 OSAN, projects.read와 Project.Create를 모두 확인한다. 파일 원문 hash와 등록 요청 ID로 파일 변경·중복 재시도를 검사한다. 수식·매크로·외부 링크·비정상 압축 파일을 거부한다.
- 기존 프로젝트·수량 대상·7단계 snapshot·접근 연결 생성 코드를 재사용한다. 등록 시 진행은 시작 전이며 청주/Directory 데이터나 실제 provider를 변경하지 않는다.

## 검증과 현재 상태

구현과 관련 자동 검증을 완료했다. 최종 증거 검토에서 요청한 중간 저장 실패 rollback·권한 회수 후 replay 거부 검증도 보완해 통과했다. DB 검증에는 disposable 합성 환경을 사용했고 사용자 검수·원격 반영·배포는 대기 상태다. 기존 공유 검수 API나 공개 환경은 이번 기능으로 갱신하지 않았다.

- Frontend: 기존 오산 등록·사업부 접근 및 새 dialog 테스트 37개, 실제 공통 API client의 multipart/사업부 context/읽기모드 거부 테스트 2개 PASS. 관련 ESLint 및 TypeScript 포함 production build PASS(기존 bundle 크기 안내 유지).
- 합성 browser: 기존 청주·오산 등록/목록/상세 계약과 새 Excel download→오류 미리보기→파일 재선택→등록→목록 갱신 2 tests PASS. 초기 검증 selector가 오류 요약/표 또는 loading/success를 함께 찾던 문제를 좁혀 재검증했다. 실제 PC1440·모바일390 이미지를 열어 확인했고 표 내부 scroll·page overflow 없음, 등록 중 Enter/Tab/Escape 포커스·중복 제출·닫기 차단을 확인했다. 합성 이미지 `/private/tmp/osan-excel-review/`에 보관하며 commit하지 않는다.
- Backend: 관련 테스트 8개 PASS(파일 파싱 1, 격리 PostgreSQL 일괄 저장 1, 실제 3DB HTTP 1, endpoint catalog 1, audit registry 4). HTTP에서 template→multipart preview→apply→상세 GET의 대상·7단계를 확인하고 등록 권한 403, 청주 접근 거부, 잘못된 method/인접 경로 거부, ReviewSafe 423을 확인했다. 초기 HTTP 실패는 테스트 계정의 명시적 오산 소속 설정을 보정해 해결했으며 제품 권한은 완화하지 않았다. 테스트용 disposable DB 잔여 0을 확인했다.
- 독립 review: EXCEL-R1 import middleware 경로, R2 소수 수량의 표시 반올림, R3 수량 합계 overflow, R4 전체 workbook 입력 상한, R5 역순 동시 batch lock 순서를 보정했다. R4는 ZIP 크기를 XML 파싱 전에 검사하고 content type으로 실제 worksheet part를 찾아 확장자와 무관하게 검사한다. 독립 reviewer가 R1~R5 해소와 신규 P0~P2 없음, 코드 GO를 확인했다. apply 감사 등록과 preview 비저장 예외도 별도 GO를 받았다.
- 실제 서버 생성 양식을 별도로 열어 8개 항목, 빈 입력 행, 안내 문구와 화면 배치를 확인했다. 양식 검사용 산출물은 추적 테스트의 고정 경로 쓰기나 commit에 포함하지 않는다.
- 최종 API Release build는 경고 0·오류 0, `git diff --check` PASS. 전체 회귀와 required CI는 이번 로컬 구현 단계에서 실행하지 않았으며 향후 최종 병합 후보에서 수행한다. 이번 기능은 `codex/osan-project-excel`에 범위 파일만 로컬 커밋한다.
- 최종 보완: 정상 HTTP replay가 같은 ID 목록을 반환한 뒤 프로젝트 접근 연결을 회수하면 403이고 본문에 ID가 없음을 확인했다. `Project.Read.All`을 제거해 실제 개별 접근 분기를 실행했다. 두 번째 프로젝트 insert에 합성 실패를 발생시켜 첫 번째 저장까지 포함한 projects·targets·steps·access·events·operations 전후 건수가 같고 신규 프로젝트/root operation 잔여 0임을 확인했다. 해당 2 tests 재실행 PASS, 테스트 Release build 경고 0·오류 0. 독립 reviewer는 두 테스트 설계와 기존 코드 GO를 확인했다.

## 검수 화면 제공 — 2026-09-09

사용자의 “보여줘” 요청으로 기존 격리 검수 환경을 제품 커밋 `88bee7e`로 기동했다. 시작 전 5186/5096 listener가 없음을 확인했다. 소스는 `/private/tmp/emi-osan-progress-photo`, UI는 http://127.0.0.1:5186/projects, API는 5096이다. 기존 `/private/tmp/emi-osan-preview-20260909/api.env`를 사용하되 seed·startup migration을 명시적으로 끄고 기존 외부 provider 비활성 설정을 유지했다. API 실행 세션32597, frontend 세션4547이며 재개 시 실제 상태를 다시 확인한다. 프록시 health ready 200, 기존 완료 샘플 대상2/진행률100% 조회, 오산 엑셀 업로드 창과 실제 양식 다운로드 성공을 브라우저에서 확인했다. 새 프로젝트 등록은 사용자 검수에 남겼으며 DB 초기화·원격 반영·공개배포는 하지 않았다.
