# 최초 유지보수 기능 도입: 1회 중단 배포

사용자가 명시적으로 승인한 최초 1회 대체 절차다. 대화 사전 공지, 조회·로그인을 포함한 일시 중단, 구 처리 종료, migration·교체·검증·재개를 수행한다. 이후 배포는 기존 `deploy-azure-pilot-release.sh`의 공지·팝업·저장 제한을 그대로 사용한다. 이 runner는 일반 workflow에 연결하지 않는다.

## 실행 전

- 승인된 exact main SHA와 required CI를 확인하고 Backend/Frontend digest image를 먼저 게시한다. 이 runner는 build·push·merge하지 않는다.
- 유지보수 Manual job은 미리 준비한다. 기존 Backend의 Production 환경과 Key Vault secret 참조, runtime 연결, 기존 migration identity/연결을 재사용한다. identity·registry·secret 권한은 job 준비 담당자가 검증한다. container 이름은 job 이름과 같아야 하며 저장된 `Maintenance__*` 환경값은 없어야 한다.
- job 실행은 CLI 분기에서 종료하므로 웹 서버와 hosted worker를 시작하지 않는다. `--maintenance-complete`는 runtime 연결과 Directory·청주·오산 exact ledger를 검증한다.
- actor는 두 사업장에 존재하는 활성 사용자여야 한다. 구 worker의 provider 처리·lease 불확정 건이 있다면 사전에 확인한다. 임의 상태 reset이나 실제 시험 발송은 하지 않는다.
- 운영자가 사전 검토한 읽기 전용 Python 진단 파일을 준비한다. 중단 후 provider Processing/유효 lease와 열린 backend transaction이 없음을 확인할 때만 종료 코드 0을 반환해야 한다. 연결 실패·조회 실패·알 수 없는 결과는 nonzero로 처리하고 SQL reset이나 데이터 보정을 하지 않는다. standalone 파일로 작성하고 필요한 설정은 환경변수에서 받는다.
- restore/PITR 기준선과 승인된 영향 범위를 확인한다. 이전 image는 migration 이후 복구 수단으로 가정하지 않는다.

## 입력과 실행

`scripts/bootstrap-azure-maintenance.sh`를 실행한다. 필수 환경값은 다음과 같다. 실제 식별자·공지 내용은 추적 파일에 넣지 않는다.

```
FIRST_MAINTENANCE_ROLLOUT_APPROVED=true
FIRST_ROLLOUT_DRAIN_CHECK_FILE=/absolute/private/path/read-only-drain-check.py
SOURCE_SHA
AZURE_SUBSCRIPTION_ID AZURE_RESOURCE_GROUP ACR_LOGIN_SERVER PUBLIC_HOSTNAME
BACKEND_APP_NAME FRONTEND_APP_NAME MIGRATION_JOB_NAME MAINTENANCE_JOB_NAME
BACKEND_RELEASE_IMAGE FRONTEND_RELEASE_IMAGE
MAINTENANCE_RELEASE_ID MAINTENANCE_ACTOR_USER_ID MAINTENANCE_TITLE MAINTENANCE_BODY
MAINTENANCE_STARTS_AT_UTC MAINTENANCE_EXPECTED_ENDS_AT_UTC
```

이미지는 해당 registry의 `pms-backend@sha256:…`, `pms-frontend@sha256:…`만 허용한다. 시각은 timezone을 포함하며 종료 예정은 실행 시점보다 뒤여야 한다. `FIRST_ROLLOUT_POLL_ATTEMPTS` 기본값 90, 간격 기본값 10초다. 테스트 실행파일 교체는 별도 test flag와 synthetic hostname을 동시에 요구한다.

drain helper는 필수다. 절대 경로의 실제 `.py` 파일이어야 하며 실행 사용자 소유이고 group/other 쓰기 권한이 없어야 한다. symlink·누락 파일·Python syntax 오류는 Azure 변경 전에 거절한다. shell 명령이나 임의 interpreter override는 받지 않는다. 사전 읽은 파일 내용을 private 임시 파일로 복사한 뒤 현재 Python interpreter로 실행하여 실행 중 경로 교체 영향을 막는다. 파일의 읽기 전용성과 진단 범위는 운영자가 검토해야 하며 소유권 검사가 내용을 보증하지는 않는다.

## 실행 순서와 증거

1. subscription·Single revision·immutable baseline·ready 상태·공개 `200/401/401`·Manual job과 실행 중복을 확인한다.
2. frontend, backend 순으로 active revision을 deactivate하고 모든 revision의 replica가 0인지 확인한다. 단순 scale-to-zero를 사용하지 않는다. Azure는 SIGTERM 이후 제한 시간 내 종료되지 않는 컨테이너를 강제 종료할 수 있으므로 replica 0은 provider 처리의 성공을 보장하지 않는다.
3. 양 앱 replica 0 확인 후 필수 drain helper를 실행한다. nonzero면 migration image update/start를 하지 않고 구 revision을 복구한다. 성공 시에만 기존 migration job의 image를 교체하고 단일 execution의 결과를 추적한다. 시작 요청 직전에 migration 경계를 기록한다. start 응답이 불확정해도 자동 재실행하지 않는다.
4. 새 digest의 유지보수 prepare/activate를 실행한다. 영구 공지는 중단 중 생성되어 재개 시 조회된다. 일반 공지 팝업은 켜지 않는다.
5. Backend, Frontend image만 교체하고 각각 exact ready revision을 확인한다. Frontend 교체 시 조회·로그인은 재개될 수 있으며 저장은 유지보수 gate가 막는다. 공개 보안 smoke 후 complete가 exact ledger를 검사하고 저장을 재개한다.
6. 출력된 private 임시 폴더의 `baseline.json`과 `events.jsonl`로 실행 SHA·이전 image/revision·job execution을 확인한다. baseline은 secret 값 없이 secretRef만 보존한다. 실행별 유지보수 payload는 성공·실패 모두 즉시 제거한다. 증거 폴더는 자동 삭제하지 않으며 필요 기간 보관 후 소유자가 정리한다.

Azure CLI 2.88.0의 `start_containerappjob_execution_yaml`은 `JobExecutionTemplate` 형식의 YAML/JSON을 받는다. runner는 기존 job template을 보존하고 image·args·release 환경만 덮어써 임시 JSON으로 전달한다. secret 값을 조회하거나 출력하지 않는다.

## 실패와 재개

- migration 시작 요청 전 실패: 구 backend/frontend revision을 다시 활성화하고 readiness·공개 보안을 확인한다.
- migration 시작 요청 이후 실패: frontend/backend의 모든 active revision을 중단하고 zero replica를 재확인한다. 이전 image rollback과 DB 역migration은 하지 않는다. 중단 검증 실패도 증거에 남긴다.
- 실패 시 기존 execution, 세 DB ledger, 두 maintenance 상태를 확인한 뒤 승인 범위의 forward fix를 정한다. 이 runner를 그대로 재실행하지 않는다. prepare는 동일 release ID에 대해 중복 오류를 내므로 부분 완료를 성공으로 간주하지 않는다.
- 최종 성공 후 두 사업장의 공지 내용과 완료 상태, 사용자 로그인·관련 화면을 확인하고 실제 중단·재개 시각 및 남은 검수를 Task에 기록한다.

관련 원칙: [배포 SOP](../../tasks/azure-deploy-001-sop.md), [Azure 종료 수명주기](https://learn.microsoft.com/en-us/azure/container-apps/application-lifecycle-management).
