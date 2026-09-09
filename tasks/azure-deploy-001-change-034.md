# TASK-AZURE-DEPLOY-001 Change 034 — 오산 엑셀 프로젝트 등록 공개배포

2026-09-10 사용자 검수 후 원격 main 병합·공개배포 승인. 제품 범위는 PROJECT Change006·007의 양식 다운로드, 클릭 편집, 유효행 부분 등록, 확인 후 중복 등록이다. 현재 후보 branch는 `codex/osan-project-excel`, 배포 전 main과 기존 성공 release source는 `f5b6f71`(run34336573630)이다.

기존 3 DB·권한·업무 데이터·사진·provider 설정을 보존한다. Business 추가 migration0090은 오산 코드 index만 변경하며 기존 행 삭제나 bootstrap/backfill은 없다. Required CI 전체 회귀 및 독립 검토 완료 후 PR 병합, exact main의 수동 Azure release에서 migration→Backend→Frontend→공개 검증 순으로 진행한다. 새 자원·권한 확대·DB 초기화·실제 알림 발송은 없다.

복구 기준은 Backend revision41/digest `sha256:eb0db4582ea9add2ac51517d264658517effbcae72ced372678b32c60e84bfa3`, Frontend revision30/digest `sha256:74e374f2e83445c2bcac46f68b3390febff8358e68e8442c6143823e1b1d08ff`이다. 실패 시 앱을 직전 immutable image로 복구하며 schema는 down하지 않는다. Change032·033의 3 DB 복구 증거를 재사용한다. migration0090의 기존 행 보존은 disposable 테스트와 로컬 검수 DB9개 테이블 전후 해시 일치로 확인했다.

현재 상태: 사전 점검·최종 후보 검증 중. 원격 병합·배포 미실행.
