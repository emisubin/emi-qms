# TASK-OSAN-PILOT-001 — 기획 문서화 작업 보고

## 1. 해결한 업무 문제와 현재 상태

사용자가 승인한 오산 기획을 대화에만 남기지 않고, [기획안](osan-pilot-001-planning.md)과 [Task별 순서](osan-pilot-001.md)로 연결한다. 제품 구현 보고가 아니라 이번 문서화 작업의 수행 기록이다.

- 승인된 대화 기획: 문서화
- 문서·Task·Roadmap 작성: 완료 — 신규 Markdown 12개와 기존 Roadmap 추가
- 독립 GPT-6 내용 검토: 1회 완료 — 내용 명확화 P2 2건 반영, 운영 준비 P3 1건 연결
- 문서 검증: PASS — local links 64개, 끊어진 링크·중복 제목 0, diff check 통과
- 제품 코드·DB·runtime·provider: 변경 없음
- Task 1 사용자 검수: 완료 — `USER_EXPLICIT_APPROVAL_2026-09-06`
- Task 2 local commit: 완료 — `2e29938f754f3d95444df2b341a921cfd1fca43f`
- Task 3: 구현·자동 검증·parent review·fresh GPT-6 High 독립 재검증 완료 — 마지막 일괄 사용자 검수 대기
- Push·PR·Merge: 미수행·미승인

## 2. 실제 변경과 기술적 결정

[요구사항 원장](osan-pilot-001-interview.md)에 사용자 정정·확인·최종 승인을 보존하고, 원래 첨부 MD는 수정하지 않는다. 승인된 업무 범위와 신규 기술 제안을 구분한다.

기존 서버 안의 별도 업무 DB, 자유 제품명과 오산 공통 양식, 포장 시 원자적 자동 완료, 중단/펜딩의 서버 차단, 권한 있는 전체 집계가 핵심이다. 데이터 분리·접근 관리·프로젝트·진행·현황·격리 검증을 각각 Task로 나누고, 운영 적용은 기존 TASK-AZURE-DEPLOY-001을 재사용한다.

변경 allowlist는 새 osan 기획/Task/handoff 문서와 docs/00-product-roadmap.md다. 기존 25개 dirty/untracked 파일의 기준 digest를 기록하고 Roadmap의 승인된 추가 구역 이외 기존 WIP의 내용 보존을 검사한다. 기존 지침 수정 상태·첨부 원문을 정리하거나 commit하지 않는다.

Backend/Frontend/API/DB/migration/UI 영향은 기획 대상으로만 분석했다. 실행 중 process·DB·배포·provider를 관측하거나 변경하지 않았다. 문서만 변경하므로 제품 build·E2E·운영 검수 성공을 주장하지 않는다.

## 3. 검증과 독립 검토

| 검증 | 상태 | 범위 |
| --- | --- | --- |
| diff whitespace | PASS | 이번 문서 + 누적 diff check, 신규 문서 별도 줄 끝 공백 확인 |
| Markdown local links/anchors·heading | PASS | 신규 Markdown 12개 및 Roadmap 오산 링크 64개, 끊어진 링크 0·anchor 링크 0·신규 문서 중복 heading 0 |
| 승인 범위·Task 의존성·용어 | PASS | 8개 입력·7단계·전체 포장·DB/서버 구분; 상위 문서 조정 + 6개 신규 구현/검증 + 기존 Azure 배포 Task 재사용 |
| privacy/secret | PASS | 추가 내용 검토와 개인 절대 경로·이메일·private key·JWT·DB URI 지정 패턴 0; 실제 계정·업무 데이터 사용 없음 |
| 기존 WIP 보존 | PASS | Roadmap 외 기존 24개 파일 hash 일치, Roadmap 오산 추가 3구역 제거 후 시작 전 hash와 일치 |
| 독립 내용 review | 완료 | fresh GPT-6 High read-only 1회, 고정 18파일 manifest 전후 동일; 구현 검증 아님 |
| 제품/운영 검증 | 미실행 | 문서 작성 범위 밖 |

독립 검토는 요청 모델과 관측 모델을 구분하며, 내용 검토 결과는 [review](osan-pilot-001-review.md)에 기록한다. 작성 결과를 검토하는 동안 대상 문서에 동시 write하지 않는다.

요청 모델은 gpt-6-astra/high이며 도구가 실제 모델을 별도 반환하지 않아 observed는 NOT_REPORTED다. 독립 검토의 고정 manifest SHA-256은 28da4e485883788e24da9a51cc189d45f1b5e1925c7387df3eec2c8a420336bd이며 검토 전후 동일했다. 이후 parent는 review 전문 기록(링크만 상대 경로화), resolution의 Task 3/4 연결, Task 6/운영 인계의 P3 안내, 상태 metadata·보고 갱신만 수행했다. 기획 본문을 재작성하지 않았고 내용 review를 반복 호출하지 않았다. 이 후속 문서 차이는 parent가 link·scope·privacy·상태를 확인했으며 제품 독립 검증 완료로 표현하지 않는다.

자동 승인 검토가 복합 Python 검사와 shasum 명령을 실행 전에 거절했다. 승인 정책을 완화하지 않았고 해당 명령은 미실행이다. 허용된 단순 읽기 전용 Python hash 조회, rg metadata와 native orchestration 내 링크 대조로 같은 문서 검사 목적을 완료했다.

기존 Roadmap의 시작 전 SHA-256과 오산 추가 구역을 제거한 값은 모두 3dc8808096717eba3c913dec6e09b3b67798fd521b789791bba12980b971cd2d다. 기존 지침·템플릿·runner·보고서와 사용자 첨부는 이번 작업에서 변경하지 않았다. Staged 파일 0, 이번 신규 코드·migration·runtime 파일 0이다.

## 4. 시행착오와 폐기한 접근

서버 공유를 업무 DB 공유로 오해하지 않도록 두 경계를 분리해 기술했다. 처음의 DB 서버 분리 가능성은 사용자의 재확인에 따라 기본안에서 제외했다.

기존 제조의 중단/펜딩과 후속 검사·물류 인계를 오산에 그대로 연결하는 접근은 사용자의 명시 제외와 충돌하므로 채택하지 않는다. 제품명마다 청주 Item을 생성하거나 플랫폼 전체를 복제하는 방법도 제외했다.

운영 적용용 신규 Task를 별도로 만드는 초안은 기존 Azure 배포 Task와 책임이 중복되어 폐기하고 실행 전 최신 change를 여는 handoff로 바꾼다.

## 5. 사용자 검수와 남은 항목

| Finding | 심각도 | 최신 문서 상태 | 원인·해소/후속 |
| --- | --- | --- | --- |
| OSAN-REVIEW-001 | P2 | RESOLVED | 기존 DB는 Title unique이고 코드 index는 일반 index였다. Task 3에 오산 코드 unique/Title profile 분리와 경쟁 테스트를 명확히 기록 |
| OSAN-REVIEW-002 | P2 | RESOLVED | 개별 next·일괄 임의 단계 의미가 달랐다. Task 4에 1~6 동작 보존과 포장 선행조건·전체 rollback을 명확히 기록 |
| OSAN-REVIEW-003 | P3 | BACKLOG | 오입력 신고·담당자·승인 전 변경 금지 안내를 Task 6·운영 인계로 연결. 정정/재개 기능은 추가하지 않음 |

Open 문서 P0/P1/P2는 0/0/0이다. RESOLVED는 문서의 모호함 해소이며 제품 코드 수정·검증이나 review resolution의 사용자 승인을 뜻하지 않는다.

사용자가 2026-09-06 “승인.”으로 직전에 제시된 8개 항목과 Task 1 결과를 검수했다. Source는 `USER_EXPLICIT_APPROVAL_2026-09-06`이다.

- [x] 같은 EMI PMS를 사용하면서 청주·오산 업무 데이터는 분리한다.
- [x] 오산 프로젝트 등록은 합의한 8개 필드를 사용한다.
- [x] 입력 수량만큼 진행 대상 item을 생성한다.
- [x] 모든 대상은 고정 7단계를 순서대로 거치며 단계를 건너뛰지 않는다.
- [x] 진행 처리는 개별 처리와 일괄 처리를 모두 제공한다.
- [x] 모든 대상의 마지막 포장이 끝나면 프로젝트가 자동 완료된다.
- [x] 권한 있는 사용자가 전체 현황을 확인하는 대시보드를 제공한다.
- [x] 오산 흐름에는 중단·펜딩 상태를 두지 않는다.

이 사용자 검수 완료는 Task 1 결과와 후속 오산 제품 계약에 대한 확인이다. Task 2는 구현·자동 검증을 마쳤고 화면 검수는 사용자의 지시에 따라 마지막 일괄 검수에서 진행한다. Task 3는 별도 구현 승인을 받아 구현·자동 검증·parent review와 fresh GPT-6 High 독립 재검증을 마쳤고 open P0/P1/P2/P3 `0/0/0/0`, `GO`다. 화면 검수는 같은 마지막 일괄 검수에 남긴다. 아직 구현하지 않은 Task 4~5의 제품 동작, Change 003 Docker runtime, 실제 Azure DB·Persistent UAT·provider와 운영 개통을 완료로 만들지 않는다.

## 6. SOP·사용자 안내와 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 문서화 작업 결과 기록 완료 |
| SOP | 이 문서 아래 문서 사용 절차·운영 handoff | 문서 흐름 작성됨; 실제 운영 검증 전 |
| User manual | 기획안 3~7절 | 예정 사용자 흐름; 구현 전 안내 |
| Roadmap update | 제품 Roadmap 23장 6.6·추적 99·Decision Log·상위 Task | 작성 완료 |
| User validation checklist | 이 문서 5절·기획안 11절 | Task 1 COMPLETE — USER_EXPLICIT_APPROVAL_2026-09-06; 후속 제품·runtime 검증 별도 |

문서 사용 절차: 기획과 review resolution 확인 → 첫 데이터 분리 Task의 최신 코드/allowlist·검증 범위 승인 → 순차 구현·테스트·독립 검증 → 사용자 검수 → 기존 Azure 배포 Task의 승인된 개통 change. 각 Task가 범위 내 검증을 수행하며 통합 Task에 전부 미루지 않는다.

## 7. 복구와 Git 상태

이번 문서 수정의 rollback은 새 문서를 승인된 경로에서 제거하고 Roadmap의 이번 추가 구역만 되돌리는 것이다. 기존 Roadmap WIP 전체 checkout이나 사용자 첨부 삭제는 하지 않는다. 이번 작업에서는 rollback·cleanup·Git mutation을 실행하지 않는다.

Task 2 `TASK-OSAN-ACCESS-001`은 총괄 소속 관리, 사업부별 local 권한, 탭별 전환·요청 무효화, 오산 제한 shell과 신규 Entra 대기 흐름을 로컬 구현했다. Backend 전체 576/576, 실제 3개 DB 격리·동시성 2/2, Frontend 284/284, mock browser 2/2와 실제 3개 DB browser 1/1을 통과했다. Fresh GPT-6 High 검토에서 확인된 모든 P1/P2를 같은 Change에서 보정해 최종 open P0/P1/P2는 `0/0/0`, 판정은 `GO`다. [Task 2 구현 보고](osan-access-001-implementation-report.md)에 변경·Finding·검증 결과를 추적하며 local commit `2e29938f754f3d95444df2b341a921cfd1fca43f`로 고정했다. 사용자 화면 검수는 마지막 일괄 검수로 이관했다. Task 3은 8개 입력의 오산 등록·목록·상세, 수량별 대상과 7단계 snapshot, DB 중복·재시도·감사·격리를 구현해 Backend 582/582, Frontend 291/291, mock browser 1/1과 실제 3개 DB full-stack 1/1을 통과했다. Parent review P2 네 건과 fresh verifier P2 세 건을 모두 보정했고 최종 open P0/P1/P2/P3는 `0/0/0/0`, 판정은 `GO`다. 허용 범위를 자동 local commit하며 사용자 화면 검수는 마지막 일괄 검수에 남긴다. Task 1의 Docker 동적 검증 P2는 별도로 Task 6에서 Azure·Persistent UAT 개통 전에 다시 확인한다. 기존 검사 container와 image의 legacy 자원 2개는 사용자가 직접 삭제할 예정이며 P3 `USER_MANUAL_ACTION_PLANNED`, 결과 대기 상태다. 기존 GOV-CODEX-002 Change 017~020은 사용자 검수·Git 게시 미실행 상태로 보존한다. Push·PR·merge·실제 provider·Persistent UAT·운영 적용은 승인되거나 수행되지 않았다.
