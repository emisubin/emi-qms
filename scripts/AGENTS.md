# Scripts AGENTS.md

이 파일은 `scripts/` 아래 작업에 적용되며 Root [AGENTS.md](../AGENTS.md)를 보완한다.

## Shell과 실패 처리

- 기존 script의 Bash/PowerShell 대상 플랫폼과 문법을 유지한다.
- Bash script는 명시적인 strict mode, 인용된 변수, 안정적인 path 계산과 실패 시 non-zero 종료를 사용한다.
- secret, connection string, token과 certificate/private key 원문을 stdout/stderr에 출력하지 않는다.
- 임시 파일은 Task 전용 경로와 ownership을 확인하고 cleanup 범위를 명시한다.

## Process ownership과 readiness

- port 점유만으로 process를 종료하지 않는다. listener PID, cwd 경계, command type, session과 PID file을 함께 확인한다.
- ownership이 하나라도 불일치하면 해당 process를 종료하지 않고 stable failure로 보고한다.
- strict port를 사용하고 요청한 port가 점유됐을 때 자동 fallback하지 않는다.
- HTTP/HTTPS protocol과 live/ready를 구분하며 단순 process 존재를 startup 성공으로 판정하지 않는다.
- runtime 교체 전 rollback 정보와 기존 runtime 보존 상태를 기록한다.

## Persistent UAT와 E2E 분리

- Persistent UAT DB/container/volume은 reset·truncate·drop·cleanup 대상이 아니다.
- Full-Stack E2E는 실행별 전용 PostgreSQL container/network/tmpfs와 `emi_qms_e2e_*` DB guard를 사용한다.
- data command 전에 DB 이름, container와 connection target을 fail-closed로 검사한다.
- cleanup은 이번 실행이 만든 E2E 자원으로 제한하고 다른 runtime, worktree 또는 사용자 파일을 삭제하지 않는다.
- actual provider와 `.env.notify-local`은 명시적인 실제 발송 Task 외에는 로드하지 않는다.

현재 공통 환경 기준은 다음과 같다. Task별 candidate/preview port는 해당 SOP와 실제 startup script를 source of truth로 사용한다.

| 환경 | Backend | Frontend | DB |
| --- | ---: | ---: | --- |
| 수동 UAT | 5081 | 5174 | `emi_qms_uat_005a` |
| Full-Stack E2E | 5082 | 5175 | `emi_qms_e2e_*` |
| Figma 디자인 검증 | N/A | 5176 | N/A |

## Script 검증

- shell syntax, actionlint, occupied-port/ownership/protocol 실패 경로와 cleanup 잔여 자원 검증을 수행한다.
- runtime/Persistent UAT 영향이 있는 script는 before/after PID·container·volume·aggregate를 비식별 projection으로 비교한다.
- 전체 검증 기준은 [Validation Matrix](../docs/development/validation-matrix.md)를 따른다.
