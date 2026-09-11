# 오산 부서별 입력 권한 및 관리자 필터

## 범위와 승인
2026-09-11 사용자 확정: 총괄 관리자는 청주·오산 모든 데이터 조회/입력, 오산 승인 사용자는 오산 모든 데이터 조회. 오산 영업·생산관리는 프로젝트 신규/엑셀 등록, 제조·품질은 전체 7단계 진행 입력. 프로젝트 수정/삭제와 사진 수정 승인은 관리자 전용 유지. 사용자 관리/승인대기 목록에 사업부·부서 동시 필터. 구현·원격 main 병합·공개배포 명시 승인 받음. 사용자 실제 화면 검수는 아직 미실행.

## 기준선과 방법
origin/main f4c630cd27a40e550f1c6e9a9e2be217c56476fb, codex/osan-department-permissions. 승인된 로컬 프로필의 오산 유효 권한을 서버에서 계산해 인증 claims와 /api/me에 공통 반영. 청주 및 승인/활성/사업부 경계 유지. DB migration 없이 기존 저장 역할의 출처 보존. 원본 작업공간 WIP 및 5193 로그인 시안 runtime 보존.

## 검증과 현재 상태
구현·관련 자동검증·독립검토·합성 PC/모바일 직접 확인 완료. 원격 CI/병합/공개배포 진행 예정. 사용자 실제 화면 검수 미실행.

## 구현 및 검증 기록
- DbIdentityStore 공통 반환 지점에서 활성·projects.read가 있는 오산 프로필의 Project.Read.All을 부여하고, 비관리자의 Project.Create/manufacturing.update는 부서 기준으로 재계산. 기존 역할·권한 저장 데이터 및 청주는 불변. 총괄의 기존 관리자 권한 경로 유지.
- 통합 사용자 관리/승인대기 화면에 사업부·부서 AND 필터, 미지정, 초기화, 조건 불일치 빈 상태 추가. 서로 다른 사업부의 부서를 교차 매칭하지 않고 저장된 활성 소속을 기준으로 필터링.
- Frontend focused 26/26, typecheck/build PASS(기존 번들 크기 경고), 변경 파일 lint 오류0. Backend 부서별 집중11/11 PASS. 실제 disposable3DB suite4/5 PASS 후 신규 fixture 테이블명 오타를 수정하고 실패한 HTTP matrix/import case1/1 PASS. 청주·오산·Directory 경계 및 inactive/read revocation 반례 포함. 소유 임시 DB/container/network 정리 완료.
- 독립 reviewer /root/osan_permissions_review (요청 gpt-6-astra/high, 실제 메타데이터 NOT_REPORTED) 누적 권한·필터·검증 코드 검토 GO, P0–P2 없음. 자동검증/사용자검수와 구분.
- 합성 화면용 5194는 기존 Osan preview 소유로 확인해 보존. 새 검증 포트5196 사용. 초기 mock CORS가5173에 고정돼 실패했고 요청 origin을 사용하는 mock으로 보정. 실 API 운영 CORS 변경 없음. 화면 최종 검증 통과(아래 기록).

- 최종 mock browser4/4 PASS. 사용자 관리 desktop1280 및 승인대기390px 합성 screenshot을 직접 열어 필터·행·줄바꿈을 확인. 390px page overflow 없음, 표 내부 가로스크롤 유지. 합성 증거는 /private/tmp/emi-osan-dept-browser, 비추적. 초기 mock CORS 실패는 수정된 helper에서 해소.

## 원격 검증 보정
PR138 head ec4747f, CI34547954410 Frontend 기존 QualityInspectionsPage finalize case 실패. 첫 클릭 뒤 초기 draft effect가 반영돼 첫 응답empty/둘째pass 상태, 서버호출0과 필수입력오류로 확인. mock 초기 render를 async act로 완료한 뒤 입력하고 두 항목의 selected 상태를 검증하도록 테스트만 보정. 기존 server error/atomic payload/중복재시도 assertions 유지. 관련4PASS. 독립 reviewer 원인/보정 타당 확인. 로컬 초기 frontend 전체409PASS도 확인. 원격 새 후보 전체 검증 예정.

CI34548342390 frontend409+browser16PASS, full-stack64+BU1+Osan1PASS. Backend629/630PASS, 실제HTTP QR 이미지 ZXing 위치탐지 결과null1건. 독립 reviewer 입력이 순수 생성 심볼임을 확인. 고정 seed20260911 합성URL1,000개를 실제 PNG로 만들어 비교: 일반TryHarder 위치탐지31건null, PureBarcode 모드1,000개 모두 정확URL해독PASS. HTTP 경계 테스트 reader만 입력형태에 맞는 PureBarcode=true로 보정; 실제 응답bytes/독립해독/non-null/exactURL/대상2개구분 assertion 유지. 기존 renderer 일반탐지 테스트는 변경하지 않음. 제품QR 생성 코드 변경 없음. 진단코드·통계는 /private/tmp 비추적 보존. 실패한3DB case 재검증 후 최종 원격CI 예정.

QR 보정 후 실제3DB ThreeDatabaseBoundary1/1PASS 및 소유 DB/container/network 정리 완료. 독립 reviewer 최종 delta GO.
