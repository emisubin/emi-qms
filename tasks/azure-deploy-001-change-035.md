# TASK-AZURE-DEPLOY-001 Change035 — 운영 사진 업로드 복구

2026-09-10 사용자가 사진 업로드503·EXIF422 두 문제의 수정과 즉시 공개배포를 명시 승인했다. PHOTO Change001이 제품계약과검증을 소유한다. 최신main e1bae57 기준으로기존청주/G2/오산업무데이터·권한·provider를 보존한다.

연결설정 긴internal ingress FQDN→동일환경 서비스이름 `clamav` 한항목만 운영에먼저보정했다. 별도Frontend컨테이너 PONG/INSTREAM OK와 공개 합성xlsx 미리보기 성공으로 실제API검사경로복구를 확인했다. Backendrevision45: 이전image `sha256:e85436218880e65442280aee855ccff982f82550186d868175f131e3ca5c9584` 유지, Healthy/traffic100%.

코드배포 rollback기준은 이 정상연결설정과Backendimage이며, Frontendrevision33/image `sha256:28d6cf2265ddf7f044c1b23317cec5a2145d23c1574f283b81f5ba29fbd6f144`를보존한다. 사진 메타데이터 보정과 추가 승인된 컬러 표시 보정은 독립review/관련검증/requiredCI 후 정확한main으로기존manualworkflow배포한다. 검사비활성·fail-open·DB초기화·시험프로젝트생성·provider발송은 하지않는다.

현재 상태: 운영 연결 복구, 코드 구현·직접 검증·독립 검토·원격 main 병합·선배포 완료. PR CI는 Backend 35분 시간 초과로 미완료이며 main CI는 진행 중이다. 전체 검증 완료 판정은 보류한다.

사용자 후속 승인: 전체 CI 대기 시간이 길어 선배포 후 검증 지속을 요청했고, main ruleset의 임시 관리자 예외 추가·PR 병합 직후 원복까지 별도로 명시 승인했다. 전체 관리자 역할보다 범위를 좁혀 현재 인증된 관리자 사용자만 `pull_request` 모드로 잠시 허용했다. 서버 측 예외가 열린 구간에는 PR133 병합·규칙 복구만 실행했다. 다른 보호 규칙은 유지했고, 원본과 복구 후 ruleset의 수정 가능한 모든 필드가 동일하며 bypass actor 0임을 확인했다.

PR133의 exact head `e96d0aa9e74729e6a1a1f87976de38692aed207e`를 main `edd2763927c8a7aec000b31581912387b7a70027`로 병합했다. 부모 e1bae57/e96d0aa 및 tree 동일 확인. PR CI `34448818679`를 취소하지 않았고 main CI `34449665561`도 자동 실행 중이다. 승인된 선배포 workflow `34449697383`을 이 main으로 실행했다. 전체 검증 통과 전 배포라는 예외 상태를 유지하며 결과가 나오기 전 검증 완료로 보고하지 않는다.

선배포 workflow 성공. Backend revision46/image `sha256:1027d2eaa8f0ba8862fee07ea734d17565df11fd8954c26f21e092661362115d`, Frontend revision34/image `sha256:6f4c5b5ecbcecc2b5ef98795beb41705c8bb5e7918cc83ef6555ef4599d29544`로 교체했다. 두 앱 latest=ready, Healthy/active 확인. 공개 health200·익명 API401, 로그인된 공개 사이트에서 새 사진 CSS `filter:none` 적용을 확인했다. ScannerHost=clamav, 검사 활성·fail-closed를 유지했다. 운영 프로젝트·사진 기록을 검증용으로 새로 생성하지 않았다.

2026-09-10 후속 확인: PR CI34448818679 Backend가 설정된 35분 한도를 넘겨 GitHub에 의해 취소되고 CI Gate가 실패했다. main CI34449665561의 Backend는 진행 중이며, 두 실행의 Frontend·Workflow Validation·Full-Stack E2E는 성공했다. 시간 초과 자체를 제품 결함이나 전체 테스트 통과로 해석하지 않는다. 새 운영 장애 근거는 확인되지 않아 선배포 버전을 유지하고 main 검증을 계속 확인한다. 사용자에게 이 상태를 통지했으며 같은 상태를 반복 통지하지 않는다.
