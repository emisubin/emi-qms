# Script·CI·실행환경 작업 계약

[Root 지침](../AGENTS.md)의 승인 경계를 따른다. 공통 지침에 과거 runtime을 항상 시작·갱신하라는 명령을 두지 않는다.

## 실행과 실패

- 기존 Bash/PowerShell 대상 플랫폼을 유지한다. Bash는 strict mode, 인용된 변수, 안정적인 경로와 non-zero 실패를 사용한다.
- secret·token·connection string·private key를 로그에 출력하지 않는다. 단순 조회에도 과도한 shell wrapper를 만들지 않는다.
- timeout이면 기존 session/process와 결과부터 확인한다. 배포·migration·전송을 응답 불명확 상태에서 다시 실행하지 않는다.
- 준비 상태는 process 존재와 구분하고 HTTP/HTTPS·liveness/readiness를 실제 확인한다.

## 자원과 DB

- process 종료 전 PID, cwd/source, command, session/owner를 확인한다. 포트만 보고 다른 process를 죽이지 않는다. 요청 포트가 점유되면 자동으로 다른 포트로 바꾸지 않는다.
- 공유 runtime 교체는 승인된 대상과 rollback/source/configuration을 확인한다. 이미 실행 중인 Backend가 파일 변경을 자동 반영했다고 가정하지 않는다.
- Persistent UAT/운영 DB·container·volume은 테스트 cleanup 대상이 아니다. 실행별 E2E DB·container·network/storage와 합성 데이터를 사용하고 실제 target/owner를 fail-closed로 검사한다.
- cleanup은 명시된 승인 범위의 이번 실행 소유 자원만 대상으로 한다. 사용자가 나중에 직접 지우기로 한 자원은 기록만 유지한다.
- 실제 provider와 `.env.notify-local`은 명시된 실제 발송 작업 밖에서 로드하지 않는다.
- 환경별 기동·주소·DB·보존 조건은 해당 Task SOP에서 찾고 실행 전에 관측한다. 안내 경로는 [제품·환경 안내](../docs/development/pms-project-guide.md)에 있다.

## 검증과 CI

- 변경에 해당하는 syntax/actionlint·정상/실패·ownership·strict port·protocol·DB guard·cleanup 반례를 선택한다. [검증표](../docs/development/validation-matrix.md)를 복사하지 않는다.
- CI 분류는 영향 있는 suite를 포함하고 알 수 없는 변경은 fail-safe로 다룬다. skipped job을 실행 성공으로 기록하지 않는다.
- policy 규칙 검사는 명령을 데이터로 평가한다. 보호 동작이 차단되는지 확인하려고 실제 삭제·push·DB mutation을 실행하지 않는다.
- 운영 승인·실행 정책·CI required check는 서로 다른 장치다. Markdown 변경만으로 강제 보호가 실제 적용됐다고 주장하지 않는다.
