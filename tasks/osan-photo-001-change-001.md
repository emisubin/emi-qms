# TASK-OSAN-PHOTO-001 Change001 — 운영 사진 업로드·컬러 표시 복구

2026-09-10 사용자가 운영의 사진 업로드503/메타데이터422 두 결함 수정·원격main병합·공개배포를 명시 승인했다. 최신 운영/main e1bae57 기준 격리 worktree /private/tmp/emi-osan-photo-upload-fix, branch codex/osan-photo-upload-fix에서 진행한다. 기존 엑셀/진행관리·청주/G2·사업부권한·데이터·provider를 보존한다.

1. Scanner: API가 내부 ingress FQDN:3310 TCP연결에서20초timeout으로503. ClamAV 프로세스/시그니처 정상. 같은환경의 ClamAV 및 Frontend 컨테이너에서 short service name clamav:3310 PING=PONG, FQDN은timeout. 배포정의와 운영의 ScannerHost 한항목을 service name으로 보정한다. 안전검사/fail-closed는 유지한다.
2. 사진: 공통 미들웨어가 EXIF가 존재하는 모든JPEG를422로거부한다. 오산 완료사진에 한정해 위치·촬영기기정보를 자동정리하되 사진방향·품질과 개수/용량·서버검증·악성검사·영구보존·재시도계약을 유지한다. 다른업로드는 기존정책유지.
3. 컬러: 사용자가 추가로 흑백 표시 수정과 컬러 원본 보존을 요청했다. `wireframe.css`의 공통 `.app-shell img` 흑백·대비 필터가 완료 사진과 업로드 미리보기에 적용되는 원인을 확인했다. 오산 진행 상세 이미지에 한정해 필터를 해제한다. 저장 시에도 재인코딩·축소 없이 압축 픽셀과 ICC 등 색상 정보를 보존한다.

검증: scanner same-environment 실제PING/INSTREAM, 정상·악성/검사불가 반례, 메타데이터정리/방향보존/손상입력·한도, 실제보안미들웨어통합, 독립review 및 requiredCI. 운영시험은 원본업무데이터를 변경하지 않는다. 배포 후migration불필요 여부·새revision과publichealth를확인한다.

현재 상태: 구현·전체 제품 검증·독립 검토·사용자 검수·main 병합·공개배포 완료. 아래 시간 초과 기록은 당시 이력으로 보존한다.

최종 마감: 배포 main edd2763의 Backend 610/610(실패0·skip0), Frontend352, UI14, Full-stack64+사업부격리1+오산등록1 PASS. 기존 CI34449665561은 테스트 성공 후 job 시간 제한으로 취소되었다. 제품 변경 없는 CI 보정 PR135는 정책 검증·required Gate PASS(34452789466) 후 main7a03167에 정상 병합했다. 관리자 예외를 재사용하지 않았다. 50분 설정으로 전체 테스트를 다시 실행했다는 의미는 아니다. 사용자 컬러 수정 완료 확인 및 독립 증거 review GO. 추가 제품 배포는 불필요하다.

연결 복구 완료: Bicep·JSON ScannerHost를 `clamav` 서비스 이름으로 보정했다. 정적/컴파일/생성JSON일치 PASS, 독립review GO. 운영 backend의 해당 환경변수 하나만 변경하여 revision45가 Ready/Healthy로 올라왔다. 공개 로그인 세션에서 합성xlsx 미리보기 업로드가 정상 완료되어 실제API→ClamAV→파서 연결을 확인했다(프로젝트 등록0). 기존 이미지 e854362…와 검사활성/fail-closed/metadata/provider 설정을 보존했다. 긴FQDN에 대한 timeout 원인과 shortname PONG/INSTREAM OK는 별도Frontend컨테이너에서도 재현했다.

컬러 표시 직접 검증: 실제 `OsanProgressPage`와 제품 CSS를 사용한 합성 사진 화면에서 1440px·390px의 저장 사진 및 업로드 미리보기를 확인했다. 네 상태의 screenshot을 직접 열어 컬러를 확인했고, 이미지 computed filter=none, natural dimensions=600×360, page error=0이었다. frontend typecheck PASS. 합성 화면·이미지·screenshot은 `/private/tmp`의 Task 전용 임시 자료이며 commit하지 않는다. 실제 운영 사진·프로젝트 데이터는 검증용으로 변경하지 않았다.

원본 보존 구현: JPEG 압축 데이터·ICC/Adobe 색상 정보 및 PNG 픽셀·색상 chunk를 재인코딩 없이 유지하고, 위치·기기 등 메타데이터를 정리한다. 방향은 최소 Orientation 정보만 남긴다. 원본이 악성 검사와 기존 파일 크기 제한을 통과한 뒤 정리하며, 저장 내용 기준 해시를 사용한다. 보안 검토의 EXIF 구조/방향값 검증 P2를 보정했다. IFD0 최소 위치·IFD 길이/포인터·Orientation 타입/개수/값·중복 EXIF를 검사하고, 최종 제품 소스 독립 재검토에서 새 P0–P2 없이 GO를 받았다. PNG 직접 반례를 포함한 테스트와 최종 증거 재검토도 통과했다.

최종 소스 운영 실행환경 확인: `backend/Dockerfile.production`의 고정 Linux amd64·distroless 이미지 빌드 PASS. app 사용자·읽기 전용·network none에서 컬러 JPEG/PNG 및 Orientation6·촬영기기 메타데이터가 포함된 JPEG/PNG 4종의 실제 validator PASS. 촬영기기 문자열 제거도 확인했다. 합성 fixture 생성 도구가 중복 EXIF를 넣은 첫 시도는 거부됐으며, 중복 없는 정상 fixture로 보정 후 네 파일 모두 통과했다. 독립 검토는 구현자와 분리된 기존 reviewer 맥락을 재사용했다.

최종 직접 검증: validator 2/2, 공통 UploadSecurity 5/5, 실제 전용 3DB HTTP `ThreeDatabaseBoundary_EnforcesRoutingRolesWorkersAndPendingLogin` 1/1(31.4초) PASS. HTTP는 metadata rejection 활성 상태에서 원본 EXIF JPEG의 byte-exact 스캔·정리본 저장/조회·일괄 사진 연결, marker 경로 infected422·unavailable503 및 DB 상태 불변을 검증했다. 마지막 child-IFD 포인터 보정 후 validator 재실행 PASS, Release 빌드 경고0/오류0, diff-check PASS. 테스트 DB는 fixture가 자동 정리했다. 독립 최종 코드·증거 GO, 남은 P0–P2 없음.

후속 순서 변경: 사용자가 선배포 후 전체 CI 지속과 임시 main 보호 예외·즉시 원복을 명시 승인했다. PR133/main edd2763 병합 및 보호 규칙 원복 완료. 원격 CI·선배포의 현재 상태는 Azure Change035에 기록한다. 커밋된 최종 e96d0aa 소스로 Linux 이미지를 다시 빌드하고 사진 4종 validator PASS를 확인했다.

2026-09-10 후속 확인: PR CI34448818679 Backend는 GitHub annotation `The job has exceeded the maximum execution time of 35m0s`로 취소되어 CI Gate가 실패했다. Frontend·Workflow Validation·Full-Stack E2E는 성공했다. main CI34449665561도 같은 세 항목은 성공했으며 Backend는 아직 진행 중이다. 확인된 실패 원인은 job 시간 제한이며 사진 기능의 assertion 실패는 이번 출력에서 확인되지 않았다. 검증을 재실행·취소하거나 운영을 롤백하지 않았고, 사용자에게 시간 초과와 전체 검증 미완료를 알렸다. main 결과를 계속 확인한다.

2026-09-10 재확인: 사용자가 업로드 미리보기와 결과의 흑백을 재신고했으나, 공개 오산 저장 사진의 실제 DOM에서 이미지 및 모든 상위 요소 filter=none을 확인한 뒤 사용자가 수정 완료를 확인했다. 추가 제품 변경은 하지 않는다. main CI34449665561의 최종 로그는 Backend 610/610 PASS, 실패0·skip0(33분47초)를 증명하지만 job 전체는 35분 제한으로 cancelled다. 다른 제품 검증 job도 PASS이며 최종 CI Gate 완료는 별도로 남아 있다. Backend 한도만 50분으로 보정하고 전체 테스트 명령에 normal 로그를 추가한다. 테스트 대상·실패 판정·required gate는 유지한다. actionlint·분류/main CI 계약·workflow 테스트 PASS, 독립 reviewer GO. 보정된 CI 정상 완료 후 마감한다.
