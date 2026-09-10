# TASK-AZURE-DEPLOY-001 Change035 — 운영 사진 업로드 복구

2026-09-10 사용자가 사진 업로드503·EXIF422 두 문제의 수정과 즉시 공개배포를 명시 승인했다. PHOTO Change001이 제품계약과검증을 소유한다. 최신main e1bae57 기준으로기존청주/G2/오산업무데이터·권한·provider를 보존한다.

연결설정 긴internal ingress FQDN→동일환경 서비스이름 `clamav` 한항목만 운영에먼저보정했다. 별도Frontend컨테이너 PONG/INSTREAM OK와 공개 합성xlsx 미리보기 성공으로 실제API검사경로복구를 확인했다. Backendrevision45: 이전image `sha256:e85436218880e65442280aee855ccff982f82550186d868175f131e3ca5c9584` 유지, Healthy/traffic100%.

코드배포 rollback기준은 이 정상연결설정과Backendimage이며, Frontendrevision33/image `sha256:28d6cf2265ddf7f044c1b23317cec5a2145d23c1574f283b81f5ba29fbd6f144`를보존한다. 사진 메타데이터 보정과 추가 승인된 컬러 표시 보정은 독립review/관련검증/requiredCI 후 정확한main으로기존manualworkflow배포한다. 검사비활성·fail-open·DB초기화·시험프로젝트생성·provider발송은 하지않는다.

현재 상태: 운영 연결 복구, 코드 구현·직접 검증·독립 검토 완료. required CI·원격 main 병합·코드 공개배포 대기.
