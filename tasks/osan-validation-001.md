# TASK-OSAN-VALIDATION-001 — 오산 격리 통합 검증과 운영 준비

- taskType: `UAT_RUNTIME`
- status: `PLANNED`
- parentTask: `TASK-OSAN-PILOT-001`
- implementationApproved: false
- runtimeMutationApproved: false
- gitPublicationApproved: false
- 선행조건: TASK-OSAN-ISOLATION/ACCESS/PROJECT/PROGRESS/DASHBOARD-001 각각 자동 검증 완료와 격리 검증 실행 승인

## 목적과 계약

두 사업부의 실제 API·DB·화면·worker 연결과 청주 회귀·복구 가능성을 하나의 격리 환경에서 입증한다.

[승인된 업무 기획](osan-pilot-001-planning.md), [독립 review resolution](osan-pilot-001-review.md), [상위 Task·순서](osan-pilot-001.md)를 함께 따른다. 이 파일은 별도 신규 인터뷰나 기획 재작성 요청이 아니다. 기획 문서화 승인은 실제 구현·runtime 실행 승인이 아니다.

## 포함 범위

- 기존 청주를 대표하는 synthetic existing DB와 fresh 오산·directory DB를 같은 전용 시험 서버에 준비한다. Persistent UAT를 사용하거나 초기화하지 않는다.
- 기존/fresh migrations·runtime 계정 격리·role/CONNECT·schema compatibility와 데이터 보존 검증을 수행한다.
- 전체 사용자 journey, 동일 ID 교차접근, 두 탭 전환, worker 실패/중복·외부 발송 차단, 동시 최종 완료를 Full-Stack으로 검증한다.
- Backend/Frontend 전체 회귀와 청주 G2·제조·품질·물류 영향 검증, desktop/390px synthetic 시각 검증을 완료한다.
- 서버 단위 복구본에서 오산 DB만 추출/검증하는 격리 rehearsal과 청주 DB 불변 확인을 수행한다.
- 명확한 운영 bootstrap·compatibility·capacity 사전 점검·개통·rollback 절차와 사용자 검수 checklist를 준비한다.

## 조사·변경 경계

격리 E2E/통합 tests·fixtures·migration/backup rehearsal 도구, SOP·사용자 검수·report. 제품 결함은 소유 Task change로 환류.

실행 시 최신 instruction chain·Task identity·Roadmap·branch/runtime 상태를 읽고 exact 파일 allowlist와 검증 명령을 고정한다. 기존 WIP가 남은 현 branch에서 제품 개발을 자동 시작하거나 사용자의 WIP를 정리하지 않는다.

## 완료 기준

- [ ] 각 Task의 성공/실패/경쟁 계약과 cross-business 데이터 경계 모두 통과.
- [ ] 복구 rehearsal 이후 청주 synthetic 데이터·사용자 ID·G2 불변.
- [ ] 운영 provider 호출·Persistent UAT mutation 0, 테스트 자원만 ownership 확인 후 cleanup.
- [ ] 자동 검증과 사용자의 실제 검수 상태를 분리하고 미실행/위험/P3를 기록.
- [ ] 독립 verifier가 고정 기준선·계약·diff·테스트를 read-only 검증.

### 이관된 P2 — OSAN-003-RUNTIME-VALIDATION

`TASK-OSAN-ISOLATION-001 Change 003`의 Docker 정상·주입 실패·TERM 자동 cleanup 검증은 현재 Codex 실행환경의 자동 정책이 process 시작 전에 반복 거부해 이 Task로 이관됐다. 이는 성공으로 간주하지 않는다.

격리 통합 검증에서 같은 wrapper를 실행 가능한 환경으로 검증한다. 정상 exit `0`, 주입 실패 exit `97`, TERM exit `143`과 각 실행의 temp/container/image/database/Compose container/network/volume 잔여 `0`을 확인한다. 다른 runtime·사용자 Docker 자원, Persistent UAT와 실제 provider는 cleanup 대상에 포함하지 않는다. 이 P2는 Azure·Persistent UAT 개통 전에 해소하거나 당시 사용자에게 실제 위험·완화책과 결과를 다시 보고해야 한다.

## 다음 Task에 전달할 내용

### 운영 준비 backlog — OSAN-REVIEW-003 / P3

개통 전 사용자 안내에 잘못된 수량·단계·최종 포장을 발견했을 때의 신고 경로, 사업부 관리자 접수와 총괄의 범위 판단, 승인 전 변경 금지 경계를 명시한다. 추가 체크나 프로젝트 재생성으로 이력을 숨기거나 직접 DB 수정으로 복구하는 것을 기본 절차로 안내하지 않는다. 실제 정정이 필요하면 별도 승인 계약에 권한·사유·감사·상태/집계·알림 처리와 검증을 포함한다. 이 Task는 안내·책임 연결만 소유하고 정정·재개 기능을 자동 추가하지 않는다.

검증된 code/artifact 기준선과 운영 handoff를 기존 TASK-AZURE-DEPLOY-001의 오산 개통 change로 전달한다. 이 Task는 Azure 개통 승인이나 완료를 만들지 않는다.

실제 구현 결과·SOP·사용자 안내·검수 checklist·Roadmap 상태는 이 Task의 구현 보고에서 추적한다. 현재는 구현/테스트 미실행, 사용자 검수 적용 전이다. Sol xhigh가 승인 범위의 구현·테스트·범위 내 보정을 맡고 parent 및 fresh GPT-6 High가 검토한다. 모든 품질·Git·운영 gate는 Root 지침을 따른다.
