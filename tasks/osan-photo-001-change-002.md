# TASK-OSAN-PHOTO-001 Change002 — 전체 40MiB 사진 용량 정책

2026-09-14 사용자 승인: 자동 압축·해상도 축소 없이 장당 5MiB 제한을 없애고 사진 전체만 40MiB로 제한한다. 기존 최대 5장·JPEG/PNG·악성코드 검사·민감 메타데이터 제거와 원본 픽셀 보존은 유지한다. 사진 수정은 유지 사진과 새 사진의 합계를 적용한다. 이번 변경의 원격 병합·공개배포 승인은 아직 없다.

기준선 origin/main d76790e, branch codex/osan-photo-total-40mib. 다른 작업 WIP/runtime은 보존한다. 서버 파서·검증·저장, 화면 제한·안내, 오산에 한정한 scanner 파일 한도40MiB, 요청 transport42MiB 및 신규0099 DB 제약 보정을 구현한다. 기존 migration은 수정하지 않는다.

실제 첨부 두 JPG는 각각8590934/9710149bytes로 종전5MiB 초과를 확인했다. 변경 후 용량은 통과하나 기존 JPEG 구조 검증에서 둘 다 여전히 거부된다. 이는 별도 원본 호환성 결함이며 이번 용량 정책 변경만으로 사진 저장이 해결됐다고 보고하지 않는다. 원본 사진은 저장소·증빙에 복사하지 않는다.

검증: 진행 중. frontend 경계·실제 선택/원본전송, 서버 정확40MiB/초과·5MiB초과 성공, scan 범위·실패차단, DB additive 제약, PC/390px 화면 및 독립 검토를 확인한다. 공개 데이터 수정·배포 없음.

## 검증 결과

- Frontend 관련35개 테스트 통과: 첨부 파일과 같은 용량2개(실제 사진 미포함)의 선택·미리보기 요소·버튼 활성·동일 File객체 전송, 정확40MiB/1byte초과·유지+신규 합계. typecheck/build 통과. lint 오류0, 기존 main.tsx Fast Refresh 경고1; 번들크기 경고는 기존 수준.
- Backend 관련11개 통과(실패/skip0): 실제 파서의6MiB·정확40MiB 합성 PNG byte보존, 합계초과, scoped scanner40MiB 허용/일반32MiB 제한/감염422/불가503, 기존 손상·메타데이터 검사. 최초 테스트의 DI 누락/합성 PNG 시간메타데이터 기대값은 fixture에서 보정 후 통과.
- 동일11개 중 실제 disposable DB 테스트: 전체migration 적용과20MiB 원본 저장, 유지20MiB+신규20MiB+1byte 거부, 정확40MiB 수정 성공·원본/수정 byte비교. 전용 임시DB/container만 정리 완료. HTTP endpoint 대신 store를 직접 호출한 DB 검증이다.
- 실제 HTTP multipart40MiB를 완료·사진수정 두 endpoint로 보내 scanner에 도달하고 fake 감염 판정422로 차단됨을2개 추가 테스트로 확인했다. 실제 운영 nginx/ClamAV 네트워크 검증은 미실행.
- Chromium 및 WebKit26.0 로컬 브라우저에서 사용자 첨부2개 JPEG의 미리보기2개·업로드 활성 확인.40MiB초과 오류·버튼 비활성도 확인. PC1440/모바일390의 합성 화면을 직접 열어 안내·버튼을 확인했다. 실제 아이폰 기기 검수와 운영 저장은 미실행.
- 독립 GPT-6 high 요청 reviewer photo_limit_review(실제모델 NOT_REPORTED) GO. 초기 DB5MiB 제약P1은0099와 실제DB 테스트로 해소. 원격 push/PR/main병합/공개배포 없음.

## 남은 별도 호환성 결함

첨부 JPEG2개 모두 MPF와 HDR gain-map 표식을 포함한다. 변경 후 용량은 허용되고 브라우저 미리보기는 정상이나, 기존 서버JPEG 검사가 부가 이미지 구조를 거부한다. 이 변경은 용량정책만 구현하며 해당 원본의 최종 저장 성공 또는 전체 모바일 결함 해결로 표현하지 않는다. 민감메타데이터 제거/원본해상도 보존 계약을 유지하는 별도 보정과 검증이 필요하다.
