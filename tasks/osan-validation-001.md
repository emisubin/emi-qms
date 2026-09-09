# TASK-OSAN-VALIDATION-001 — 오산 격리 통합 검증과 운영 준비

- taskType: `UAT_RUNTIME`
- status: `IN_PROGRESS`
- parentTask: `TASK-OSAN-PILOT-001`
- implementationApproved: true — 현재 승인 제품의 회귀 검증·테스트 보정에 한정
- runtimeMutationApproved: true — 기존 검수 API 갱신·전용 합성 테스트 환경에 한정, 운영 개통 제외
- gitPublicationApproved: false
- 선행조건: TASK-OSAN-ISOLATION/ACCESS/PROJECT/PROGRESS/DASHBOARD-001 각각 자동 검증 완료와 격리 검증 실행 승인

## 2026-09-09 최종 후보 검증 — 현재 상태

사용자가 최신 5186 화면·갱신된 5096 API의 최종 검수를 완료했다. 제품 기준선은 `98c435b`이며 이 절이 아래 최초 계획 당시의 구현·검수 미실행 상태를 대체한다. 기존 승인에 따른 격리 합성 환경 최종 코드 회귀를 완료했다. 최초 실패와 한정 재검증은 아래 결과에 구분한다. 기존 검수 데이터와 사용자 WIP는 보존하며 원격 반영·배포는 포함하지 않는다.

Backend 전체(임시 PostgreSQL), Frontend unit·lint·build·mock E2E, 일반 full-stack과 사업부·오산 전용 suite, 변경 분류·CI gate·Azure artifact 정적 검증을 확인한다. 과거 홈 제목·탭·레이아웃을 기대한 E2E는 승인된 현재 UI 기준으로 보정한다. 제품 코드는 사용자 검수 후보를 유지한다. 운영 복구 rehearsal·실제 계정 smoke·개통 P2/P3는 이번 코드 회귀와 별도 추적한다.

### 최종 후보 실행 결과

- Frontend unit: 39개 파일, 331 tests PASS. lint 오류 0(기존 main·임시 login-review의 Fast Refresh 경고 2), TypeScript 포함 build PASS. 수정된 E2E 3개 파일의 집중 lint도 PASS.
- Mock browser: 최초 13개 중 10 PASS, 3개는 이전 홈 제목·메뉴·삭제된 상세 탭 등 과거 UI 기대 때문에 실패. 현재 승인 계약으로 오산 기대만 보정한 뒤 해당 두 spec의 4개 tests PASS. 전체 13개 최종 통과 근거를 최초 성공분과 보정 재검증으로 연결한다. 등록값·사업부 전환·청주 구성·대상 이동·가로 넘침 검증은 유지했다.
- 일반 full-stack: 64/64 PASS, 12.5분. 12면 혼합 자재·지연 입고·반복 Pending·18개 workflow의 최종 완료와 청주 주요 업무 포함. 해당 실행 임시 DB·Compose container/network 정리 확인.
- 사업부 접근 전용 full-stack 1/1 PASS, 오산 등록 전용 full-stack 1/1 PASS. 각 실행의 3 DB·제한 역할·서버·Compose 정리 확인. 오산 생성 후 청주 프로젝트 수 불변 확인.
- shell syntax, change-scope, main-PR-CI, CI-gate 및 Azure artifact static validation PASS. Bicep compile은 이번 정적 검사에 미포함이며 원격 required CI 결과는 아직 없다.
- Backend 전체: 최초 sandbox 내부 pipe 실패 및 DB 설정 없는 실행은 유효 결과에서 제외하고 중단했다. 기존 e2e-safety를 사용한 전용 임시 PostgreSQL에서 전체 591개 중 589 PASS·2 FAIL·skip 0으로 종료했다(약 1시간 2분). 기본 logger 무출력 구간에 실제 runner의 CPU/JIT 활동을 확인했다. `PanelHistory_RequiresAuditReadAll(dev-production)`은 선행 프로젝트 생성 응답의 projectId 파싱, `DirectInput_PanelNameOnlyUpdate_DoesNotDriftCanonicalSizeOrAuditSize`는 임시 DB 정리 시 연결 timeout으로 실패했다. 호스트 load 및 DB 처리량 포화를 관측했지만 환경성 실패로 확정하지 않는다. 생성 상태/body를 확인하는 최소 테스트 진단을 추가하고 별도 빌드·임시 DB에서 실패 멤버 두 개(Theory 포함 7 cases)를 한정 재검증했다. 7/7 PASS·skip 0, 40초로 종료했고 임시 DB·Compose 정리도 확인했다. 최초 통과분과 실패 범위 재검증을 합쳐 591개 고유 테스트의 통과 근거를 확보했다. 첫 생성 응답의 실제 상태/body는 최초 로그에 없어 정확한 원인을 확정하지 않으며, 부하 관측과 재검증에서 재현되지 않은 사실을 함께 보존한다.
- 독립 검토: 기존 DASH-R1/P1·HOME-R1/P2 해소 및 재검토 근거를 유지한다. reviewer가 최종 제품 `98c435b`와 CI/검증표를 대조해 추가 제품 P0–P2를 발견하지 않았고, 누락된 mock E2E 및 과거 계약 기대를 지적했다. UI 보정은 Frontend 테스트 3파일, 실패 진단은 Backend 테스트 1파일에 한정하며 parent가 diff와 실제 재검증을 확인했다. 제품 코드는 변경하지 않았다.
- 검수 환경: 5186/5096 health 200. 사용자 최종 입력 후 샘플은 대상 2, 완료 14/14, 상태 Completed, 사진 14이며 마지막 완료는 2026-09-09 17:01:39 KST로 회귀 시작 전이다. API 재기동 직후 보존 hash 일치 기록과 이후 사용자 입력을 구분한다.
- 증거: `/private/tmp/osan-final-*.log`, 이번 실행의 생성 화면·워크북 127개는 `/private/tmp/osan-final-artifacts`에 보존하고 기존 tracked 자료는 복원했다. 임시 로그인 검수 entry 등 기존 WIP는 변경·커밋하지 않는다.

현재 사용자 검수와 이번 코드 회귀는 완료했다. Task status는 남은 운영 준비 범위 때문에 IN_PROGRESS로 유지한다. 사용자 검수 완료는 원격 push·PR·main 병합·Azure 배포 승인이 아니다. 코드 회귀가 통과해도 이 Task의 운영 복구 rehearsal, 기존 OSAN-003-RUNTIME-VALIDATION 및 실계정 개통 검증은 미완료로 별도 유지한다.

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
