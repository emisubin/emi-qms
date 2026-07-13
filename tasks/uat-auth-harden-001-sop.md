# TASK-UAT-AUTH-HARDEN-001 SOP

## 1. 목적

마지막 active System Administrator 보호가 적용된 Development runtime을 운영하고, identity mutation 없이 상태를 점검하며, 장애 시 Persistent data를 임의 조정하지 않고 안전하게 격리하는 절차다.

## 2. 현재 환경

| 환경 | 주소 | 상태 |
| --- | --- | --- |
| Development frontend | `https://localhost:5174` | latest-main, mutation 가능 |
| Development backend | `http://127.0.0.1:5081` | latest-main, 정상 worker/provider |
| Review-safe frontend | `https://localhost:5190` | read-only fallback |
| Review-safe backend | `http://127.0.0.1:5092` | DB read-only, mutation 423 |
| Preview 5185 | N/A | maintenance 격리로 DOWN |

## 3. Canonical administrator 점검

실제 identity 원문 대신 다음 aggregate만 확인한다.

- Canonical role singleton: 1
- Canonical active Entra administrator: 1 이상
- Orphan/duplicate assignment: 0
- Deletion requested/scheduled/purge blocked administrator count
- Identity digest changed/unchanged
- Admin log count/max timestamp changed/unchanged

Dev persona, 승인 대기 사용자와 복구 snapshot은 canonical count에 포함하지 않는다.

## 4. 정상 last-admin 거부

마지막 administrator를 비활성화·System Administrator role 제거·삭제 예약하려는 요청은 HTTP 400과 단일 `message` shape로 거부되는 것이 정상이다. SQLSTATE, query, lock 정보나 내부 identity가 응답에 나타나면 보안 Finding으로 처리한다.

동시 감소에서는 안전한 요청만 commit되고 마지막 administrator를 제거하게 되는 요청은 거부될 수 있다. 이는 오류가 아니라 불변조건 보호 결과다.

## 5. Purge 의미

- 정상 삭제 lifecycle의 last-admin protection은 삭제 예약 transaction에서 수행한다.
- Immediate/due purge guard는 lifecycle marker와 active administrator 상태가 비정상적으로 공존하는 malformed state를 physical delete 직전에 차단하는 defense-in-depth다.
- Due purge의 last-admin defense rejection은 batch 전체 rollback이다.
- 다른 canonical administrator가 있어 guard가 허용돼도 기존 reference scan에 따라 `PurgeBlocked`가 될 수 있다.

## 6. 일상 read-only 점검

1. Development와 Review-safe live/ready가 200인지 확인한다.
2. Development runtime source가 승인된 latest-main인지 확인한다.
3. Review-safe가 `databaseReadOnly=true`, `mutationAllowed=false`인지 확인한다.
4. Ledger가 `28/29/1`인지 확인한다.
5. Canonical active administrator가 1 이상인지 확인한다.
6. Pending/Processing, due purge와 unknown writer를 aggregate로 확인한다.
7. PostgreSQL health와 restart 0을 확인한다.
8. 실제 identity, raw response와 provider credential은 출력하지 않는다.

## 7. Runtime ownership

Runtime 종료·교체 전 listener, executable, command, cwd, session ancestry와 process continuity가 모두 expected owner인지 확인한다. PID file 하나만으로 소유권을 추정하지 않는다. 광범위 `pkill`, `killall`과 SIGKILL은 사용하지 않는다.

## 8. Worker와 provider

Official Development에서는 escalation·delivery·purge worker가 각각 한 instance여야 한다. Review-safe에서는 mutation worker와 actual provider가 모두 false여야 한다.

Provider 상태는 configured/enabled boolean만 확인한다. Credential, URL, recipient와 request/response 원문은 출력하지 않는다. Test notification은 별도 승인 없이 생성하지 않는다.

## 9. Lock wait·timeout 대응

- 정상 동시 감소는 canonical role row에서 직렬화될 수 있다.
- Cancellation, lock timeout과 DB 장애를 last-admin domain failure로 바꾸지 않는다.
- Request가 실패하면 user/role/deletion partial state와 leaked transaction이 0인지 확인한다.
- Lock key, SQL과 identity를 로그나 보고에 기록하지 않는다.

## 10. 장애 대응

### Development startup 실패

- 신규 5174/5081 exact owner만 정상 종료한다.
- Review-safe를 유지한다.
- Persistent user/role 데이터를 수정하지 않는다.
- 구 runtime 복구 또는 forward-fix는 별도 승인받는다.

### Canonical count 또는 identity digest 변화

- 모든 writer를 격리한다.
- Identity mutation과 purge를 중단한다.
- 자동 복구, Direct SQL과 backup restore를 수행하지 않는다.
- 승인된 break-glass/DB 복구 결정을 요청한다.

### Unexpected provider 또는 notification delta

- Development frontend/backend를 exact ownership으로 정상 종료한다.
- 생성 row를 삭제·제외·강제 성공하지 않는다.
- Review-safe만 제공하고 별도 데이터 처리 결정을 요청한다.

### Review-safe 장애

- Development 공개 유지 여부를 별도 판단한다.
- PostgreSQL integrity와 restart를 확인한다.
- 안전성이 확인되기 전 mutation을 수행하지 않는다.

## 11. Rollback 원칙

- Runtime rollback과 DB rollback을 분리한다.
- Migration/schema 변경이 없으므로 UAT 적용 자체의 DB migration rollback은 없다.
- Persistent backup restore는 자동 rollback이 아니다.
- Direct SQL user/role 복구는 기본 경로가 아니며 별도 명시 승인·transaction·audit가 필요하다.
- 이미 생성된 audit/lifecycle row를 임의 삭제하지 않는다.

## 12. Persistent live mutation 재검토 조건

다음이 모두 충족되기 전까지 `NO_GO`다.

- 별도 controlled Entra administrator 존재
- 실제 인증·authorization 성공 확인
- 복구 절차 rehearsal
- Identity mutation maintenance window
- Unknown writer·write transaction 0
- Fresh backup과 복구 승인 경계
- 어느 administrator가 남아도 운영 가능한 업무 승인
- 실제 user/role/deletion mutation 별도 승인

## 13. 개인정보 안전 증빙

보고에는 boolean, count, fixed enum, HTTP status와 short SHA만 사용한다. 실제 이름, email/UPN, user·role ID, raw DB/API/DOM/log, SQL, lock metadata와 credential을 기록하지 않는다.

Task-owned raw artifact는 private mode로 수집하고 projection 뒤 삭제한다. Repository tracked/staged/retained count가 0인지 확인한다.

## 14. 금지사항

- Persistent 유일 administrator 실데이터 test
- 승인 없는 user/role/deletion mutation
- Direct SQL identity 변경
- 자동 backup restore
- Review-safe mutation worker/provider 활성화
- 광범위 process 종료
- Raw evidence의 terminal 출력 또는 commit
- Last-admin 보호를 DB trigger까지 보장한다고 표현

## 15. 사용자 검수 절차

1. Development 5174의 주요 화면을 read-only로 연다.
2. 관리자 사용자 화면의 기존 데이터 표시를 확인하되 저장하지 않는다.
3. Review-safe 5190에서 mutation control이 비활성임을 확인한다.
4. 동시 요청에서 한 요청이 안전하게 거부될 수 있음을 확인한다.
5. Persistent live identity mutation이 미수행임을 확인한다.
6. 이상이 없으면 PR Ready·merge를 별도 승인한다.

## 16. 사용자 검수 상태

- 사용자 검수: 완료
- PR #40: Ready 전환·squash merge 승인
- Persistent live user/role/deletion mutation: `NO_GO` 유지
- Direct SQL과 자동 backup restore: 금지 유지
