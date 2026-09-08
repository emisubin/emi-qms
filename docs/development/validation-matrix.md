# 변경에 맞는 검증

[Root 지침](../../AGENTS.md)의 실행 권한 안에서 검증한다. 아래는 선택 기준이며 매 Task에 전부 실행할 checklist가 아니다.

## 개발 중 직접 검증

| 실제 변경 | 필요한 확인 | 대표 실패·경계 |
| --- | --- | --- |
| 문서·지침 문안 | 의미/참조·diff·범위·secret 확인 | 같은 행동의 승인/완료 규칙 충돌, 과거 기록 재실행 |
| config·명령 정책·CI 분류 | 구문·지원 설정·규칙 판정·해당 분류/gate tests | 읽기 과잉 차단, 보호 옵션 변형, 혼합/알 수 없는 변경 누락 |
| 좁은 UI/스타일 | 관련 기존 test와 실제 변경 화면 | 행 밀도·정렬·탭·좁은 화면 overflow |
| FE/BE 동작 | 재현·성공·실패·경계의 관련 tests, 영향받는 lint/typecheck/build | 늦은 응답, 중복 제출, 빈 값/0, API 오류 |
| 로그인·사업부·부서·역할 | 합성 계정으로 실제 서버 allow/deny, readiness·역할 출처·3-DB 전환 | 부서 이동 잔여 자동 권한, 잘못된 DB fallback, 미승인 접근 |
| migration/schema | 격리된 새 DB·기존 schema/합성 데이터 적용, catalog·ledger·identity·복구 | version 숫자만 비교, 부분 적용·호환성 실패 |
| 동시성·완료 상태 | 경쟁·재전송·stale version·부분 실패·취소 | 중복 처리, 부분 commit, 마지막 포장과 프로젝트 상태 불일치 |
| runtime/provider | 해당 ownership·readiness·DB/worker/provider guard 및 실패 복구 | 다른 process 종료, 실제 발송, UAT cleanup, 응답 불명확 재실행 |

가역적이고 영향이 작은 문구 수정에 구현을 그대로 복사한 새 테스트를 만들지 않는다. 반대로 위험한 실제 분기 검증을 “테스트는 한 번만”이라는 이유로 생략하지 않는다. 미실행 이유와 사용자에게 필요한 한계만 Task에 남긴다.

## 반례의 근거

조건부 동작은 **발동 입력 → 실제 경로 실행 → 관찰한 결과**를 연결한다. 새 테스트는 잡아야 할 오동작·구현과 독립적인 기대값·관찰 결과가 있어야 한다.

- 부서 이동: 승인된 역할 표로 기대 allow/deny를 정한다. 권한 계산 함수를 기대값 계산에도 사용하지 않는다.
- 사업부 격리: 잘못된 요청을 서버에서 거부하고 다른 사업부 저장소를 읽거나 쓰지 않았음을 확인한다. 선택창 숨김만으로 통과하지 않는다.
- 오산 후속 진행: 선택한 1~6단계와 개별 순차 진행의 차이, 포장 선행조건, 일괄 실패 rollback, 마지막 대상/프로젝트 완료의 원자성을 확인한다.

실제 내부 권한·분기는 유지하고 외부 provider만 fake/dry-run으로 대체한다. 재현 버그는 보정 전 실패/후 성공을 우선 확인한다. 이미 작성한 코드를 절차 위반만으로 폐기하지 않되 원래 결함을 잡는 증거를 남긴다.

## 명령 선택

실행 전 현재 script·package의 실제 옵션·대상·환경을 확인한다. 이 목록은 명령 전체를 매번 실행하라는 지시가 아니다.

| 필요 | 기존 진입점 |
| --- | --- |
| Backend compile·관련 tests | `dotnet build backend/Emi.Qms.sln --configuration Release`, `dotnet test backend/Emi.Qms.sln --configuration Release --filter <관련 필터>` |
| Frontend 관련 tests | `corepack pnpm --dir frontend exec vitest run <관련 파일>` |
| Frontend lint/type/build | `corepack pnpm --dir frontend run lint`, `typecheck`, `build` |
| mock browser | `corepack pnpm --dir frontend run e2e:mock -- <관련 spec>` |
| 실제 API/DB browser | `scripts/e2e-full-stack.sh`와 해당 config |
| 사업부·오산 | 최신 main의 `scripts/test-business-unit-isolation.sh`, `scripts/e2e-business-unit-access-full-stack.sh`, `scripts/e2e-osan-project-registration-full-stack.sh` |
| CI·release guard | `scripts/test-change-scope.sh`, `scripts/test-ci-gate.sh`, `scripts/test-main-pr-ci.sh`, 변경 시 `scripts/test-azure-pilot-release.sh` |
| Codex 명령 규칙 | `codex execpolicy check --rules .codex/rules/project-safety.rules -- <평가할 명령 인자>` |

`--no-build`는 그 source의 build가 존재할 때만 사용한다. 이 checkout에 없는 최신 main script를 있다고 주장하거나 다른 branch의 구현을 실행 증거로 쓰지 않는다. 일반 full-stack config가 사업부·오산 전용 spec을 제외할 수 있으므로 실행 목록을 확인한다.

## 화면 확인

UI 변경에는 실제 route·상태·viewport를 열고 확인한 근거가 필요하다. 청주 재사용이면 같은 조건에서 비교한다. 영향받는 loading/empty/error/success·권한·disabled 상태와 필요한 desktop/좁은 폭을 선택하고 blank page·console/request 오류·overflow를 확인한다.

screenshot 생성과 직접 열어 본 검증을 구분한다. 실화면 관찰·합성 증빙·보관은 [증거 정책](privacy-safe-evidence.md)을 따른다. 실행/관찰 불가면 눈으로 확인했다고 기록하지 않는다.

## 전체 회귀와 증거 재사용

개발 중 직접 검증 → 사용자 검수 → 최종 병합 후보의 전체 회귀 책임 실행 순서다. 마지막 실행은 CI가 맡을 수 있다. parent와 reviewer는 같은 결과를 읽고 동일 suite를 각각 재실행하지 않는다.

- 코드 SHA/tree, 검사 범위/명령, 관련 설정·dependency·DB fixture·runtime profile과 결과를 연결한다.
- 관련 코드·환경·실패 수정이 바뀌면 영향받는 결과를 갱신한다. 무관한 문서 변경은 제품 전체 재실행 사유가 아니다.
- required CI와 제품 전체 회귀는 다르다. 전체 회귀를 약속했다면 Backend·Frontend·일반 full-stack·해당 전용 suite의 실제 실행 여부를 확인하고 누락분만 보충한다. skipped는 PASS가 아니다.
- main에서 PR CI를 재사용할 때는 기존 `verify-main-pr-ci.sh`의 tree·ruleset·trust 조건을 유지한다.
- 문서만 변경한 작업에 제품 전체 회귀를 추가하지 않는다. 실행 설정/분류 변경에는 그 동작의 집중 검증을 적용한다.
- 보고가 잘렸으면 원본 증거 위치부터 확인한다. 증거가 없거나 기준선이 다르면 필요한 검증을 수행하고 미확인 상태를 남긴다.

검토·Finding·local commit의 완료 판정은 [완료 정책](../12-task-completion-policy.md)에서 한 번 정한다.
