# TASK-OSAN-ACCESS-001 Change 009 — 승인 대기 KPI와 사용자 목록 정합성

- 목표: 관리자 KPI 1건과 통합 승인 대기 목록 5명의 불일치를 바로잡는다. 일반 홈의 같은 지표와 필터 제목·빈 상태도 정렬한다.
- 승인: 2026-09-10 사용자가 “해당 오류 바로 수정 구현하고 공개배포해”라고 명시. 해당 수정의 구현·검증과 공개 배포에 필요한 branch 게시·PR·병합·release 범위. 실제 계정·권한·DB 데이터 보정은 제외한다. 사용자 수정 화면 검수는 별도 미완료 상태다.
- 기준선: `edd2763927c8a7aec000b31581912387b7a70027`, 직전 성공 공개 release `34449697383`. Branch `codex/admin-approval-kpi`, 전용 worktree `/private/tmp/emi-admin-approval-kpi`. 원래 checkout WIP 보존. v2 AGENTS가 기준선에 포함돼 있다.
- 원인: KPI는 선택 사업부의 활성 Entra/역할 없음 SQL을 사용하고 총괄 목록은 Directory membership와 local readiness를 사용한다.
- 구현: 공용 count service가 통합 목록과 동일한 총괄 context·claim·identity 확인 뒤 그 snapshot을 재사용한다. 그 외에는 기존 local 사용자 snapshot의 ApprovalPending을 센다. 두 KPI의 중복 SQL 집계를 제거하고 기존 endpoint 권한을 유지한다. 승인 기준·계정·사업부 접근 정책은 변경하지 않는다.
- 완료 조건: 관리자·일반 홈 KPI와 해당 사용자 목록 건수가 일치, local/overall 범위 보존, 부서 누락 및 소속 없는 사용자 포함, 승인 후 count 감소, 승인 대기 제목과 0건 안내 표시. 기존 알림 KPI 보존.
- 검증: Backend local API 및 3-DB 합성 반례, 기존 지표 store 검증, Frontend 관련 tests 및 실제 합성 화면 desktop/좁은 폭, 작성과 분리한 GPT-6 High review, required CI, release readiness·공개 인증 경계·실제 KPI/목록 대조.
- Backend 검증: Release build 경고/오류 0. 격리 합성 PostgreSQL에서 local 역할 있음/부서 없음·비활성 제외 API, 관리자 응답 shape, 3-DB 총괄 및 비총괄 목록/KPI 일치·승인 후 감소, 알림 처리 count, 부서별 Home store 검증 통과(고유 5 cases). 최초 Home SQL 열 수 변경으로 발생한 500은 ReadCountsAsync의 실제 FieldCount 처리로 보정했고 영향 검증을 재실행해 통과했다. 모든 실행 소유 임시 DB/container/network 정리 완료.
- Frontend 검증: 관련 suite 초기 354/354, 최종 BusinessUnitAccess 22/22와 Home 링크·관리자 KPI focused 2/2, typecheck PASS. 합성 Playwright desktop/390px 목록·빈 상태 1/1 PASS와 생성 screenshot 직접 시각 확인. 페이지 overflow 없음. 일반 홈 KPI도 승인 대기 필터로 이동한다.
- 독립 검토: 작성과 분리한 GPT-6 Astra High reviewer `/root/review`, 실제 모델 응답 별도 미노출(NOT_REPORTED). 누적 diff+신규 service+검증 근거 검토 PASS, 남은 P0/P1/P2 없음. Home 링크 Finding은 필터 연결과 클릭 test로 해결. 최종 App.tsx SHA-256 `534f25582029d974c82c2d2dcf4e5c4258f0f4f679ab716b8b2b64b41f660895`.
- 자동 테스트와 직접 관찰은 합성 데이터만 사용했다. 실제 사용자·권한 데이터 mutation 없음.
- 사용자 검수: 수정 결과의 직접 검수는 미실행. 즉시 수정·공개배포 요청에 따라 실행하되 사용자 검수 완료로 표현하지 않는다.
- 현재 상태: 구현·관련 검증·독립 검토 완료. 원격 CI·병합·공개 배포 미완료.

- 원격 검증 진행: PR #134, 최초 head `4a4c5005abe5e3cf37b3bbd0510a920c9a3643ce`, CI `34452218525`에서 Frontend 단위 355/355·mock browser 15/15 PASS. Backend와 full-stack 실행 중 main에 PR #135가 병합됐다.
- 기준선 보정: main `7a03167a8ae7bb71c135cdec90545e2e792fa5b6`의 Backend CI 35→50분 및 normal logger, 기존 사진 Task 기록만 통합했다. 이전 전체 검증 610/610 PASS 뒤 job 35분 timeout 이력이 원인이다. 이번 제품 diff는 변하지 않았고 제품 관련 로컬 검증·검토 근거는 유효하다. 새 head의 required CI로 최종 판정한다.
