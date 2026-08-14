# TASK-PRODUCTION-CONTROL-001 change-011

## Task Identity Gate

- purposeIdentity: 프로젝트 생성 시 고정된 생산계획을 프로젝트 안에서도 계획 항목별 기간·실적 1:1 연결 방식으로 수정하고, 조회 화면을 계획·실적 2막대 일정표로 단일화한다.
- canonicalTask: `TASK-PRODUCTION-CONTROL-001`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- branch: `feat/task-production-control-001-unified-project-plans`
- baseHead: `4520e641d5d78b40b90797b7471516485d7d6bc5`
- instructionChainRead: `true`
- roadmapExpectedTaskId: Teams SSO·새 manifest 신규 기능 기획
- roadmapSequenceMatch: `false`
- explicitRoadmapOverrideApproved: `true`
- samePurposeMatchCount: `1`
- reuseExistingTask: `true`
- gateStatus: `PASS_REUSE`
- approvalSource: 양식 관리의 기본 틀은 이후 프로젝트에만 적용하고 프로젝트 내부 수정은 해당 프로젝트 전용 계획·실적 연결로 저장하며, 기존 체크형 일정표를 삭제하고 행 삭제 저장 오류까지 함께 수정하라는 사용자 승인

## Codex 기획

### 사용자 문제와 기대 결과

1. 생성 시점이 오래된 프로젝트는 단일 예정일과 체크형 달력을 사용해 현재의 계획·실적 연결 기능을 프로젝트 안에서 수정할 수 없다.
2. 조회 화면이 기존 체크형 달력과 계획·실적 2막대 일정표로 나뉘어 같은 생산계획을 일관되게 읽기 어렵다.
3. 사용자 추가 행을 여러 개 만든 뒤 일부를 삭제하고 다시 저장하면 화면 내부의 끊긴 순번 또는 중복 순번이 서버 검증에 걸린다.

완료 후에는 생성 시점과 Item에 관계없이 프로젝트 생산계획 수정 화면에서 활성 계획 행마다 계획 기간과 실적 데이터 하나를 선택할 수 있다. 조회 화면은 계획 흰색 막대와 실적 검은색 막대를 가진 단일 일정표만 표시한다.

### 데이터·적용 정책

- 양식 관리의 Item별 계획·실적 연결은 저장 이후 생성되는 프로젝트에만 생성 시점 snapshot으로 적용한다.
- 기존 프로젝트를 현재 master 양식으로 덮어쓰거나 다른 프로젝트와 공유하지 않는다.
- 기존 `LEGACY` 프로젝트는 첫 수정 시 기존 프로젝트 행·항목명·필수 여부·담당자·코멘트·날짜를 그대로 사용한다. 기존 단일 예정일은 같은 날의 시작일·종료일로 표시하고, 저장 이후에는 해당 프로젝트만의 기간과 연결을 사용한다.
- 내부 `LEGACY` 표시는 생성 시점 provenance로 유지한다. 이를 `LINKED_V1`으로 강제 변경해 현재 master version을 소급 참조시키지 않는다.
- 고정 업무 사건(구매·자재·IQC·OQC·전진검수·FAT·포장·출발·납품)은 기존 프로젝트에서도 연결할 수 있다.
- 기존 방식 제조 프로젝트는 전역 제조 작업 양식의 안정된 항목 코드에 해당하는 현재 선택지를 제공하고, 과거·현재 실행의 같은 제조 항목을 실적으로 집계한다. LQC 세부 제조 identity를 보존하지 않은 기존 프로젝트에서는 오연결을 막기 위해 LQC 신규 연결을 운영 불가로 명확히 표시한다.

### 화면 정책

- 프로젝트 조회는 model version으로 Legacy/Linked 화면을 분기하지 않고 모두 현재 `생산계획표`, 자동 실적 근거, 계획·실적 일정표, 부서별 담당자를 사용한다.
- 조회 표·모바일 카드에 `연결 실적`을 표시해 어떤 원본 실적이 진행률을 만드는지 알 수 있게 한다.
- 기존 단일 예정일·날짜별 체크표·영업일 달력 조회와 해당 화면용 네트워크 요청을 제거한다.
- 프로젝트 수정은 모든 일반 프로젝트에서 계획 항목, 시작·종료일, 필수, 담당자, 필요 인원, 코멘트, 실적 1:1 연결을 한 행에서 수정한다.
- UL891 `LINKED_V1` 세트형의 공통 계획 구조·전체 기본계획·개별 세트 일정 계약은 유지한다.
- 현재 흑백 wireframe, 계획 흰색 막대, 실적 검은색 막대, 표 외곽·좌측 구분선·날짜 세로선 계약을 유지하고 새로운 강조선은 추가하지 않는다.

### 행 추가·삭제 저장 규칙

- 아직 저장하지 않은 추가 행을 삭제하면 요청 목록에서 즉시 제거한다.
- 이미 저장된 행을 삭제하면 삭제 표시를 유지해 서버가 비활성화한다.
- 추가·삭제 뒤 남은 활성 행은 화면에서 항상 `1..N`으로 다시 번호를 매긴다.
- 저장 직전에도 활성 행 순번을 다시 정규화하고, 서버는 전달된 번호를 신뢰하지 않고 요청의 활성 행 순서대로 `1..N`을 부여한다.
- 삭제 행은 필수값·실적 연결·순번 중복 검증에서 제외한다.

## 포함 범위

- 기존 프로젝트의 계획 기간·실적 연결 조회/저장
- 기존 제조 실행과 연결 가능한 Legacy 제조 실적 선택·projection
- 프로젝트 생산계획 읽기 화면 단일화와 체크형 달력 제거
- 계획 항목별 연결 실적 표시
- 행 추가·삭제·재추가 순번 정규화
- Backend·Frontend 집중 회귀와 기존 LinkedV1·UL891 세트 회귀

## 제외 범위

- master 양식을 기존 프로젝트에 소급 적용
- 기존 프로젝트 제조·품질 원본 이력 수정
- UL891 세트 기본값·개별 세트 정책 변경
- 생산계획 이외 workflow 진행률·알림·권한 변경
- 관리자 양식 권한 Change 002의 병합·게시
- DB migration, 운영 DB mutation, 원격 게시와 공개 배포

## 보존할 불변조건

- 프로젝트 생성 시점의 양식 snapshot과 다른 프로젝트 계획은 변경하지 않는다.
- 자동 실적은 부서 원본 fact에서 조회 시 계산하며 별도 실적 복사본을 만들지 않는다.
- 항목마다 활성 실적 연결은 정확히 하나만 허용한다.
- LQC 운영 중지, FAT 비필수, Pending 차단과 UL891 세트 범위 계산을 유지한다.
- 동시 수정 row version, 담당자 후보·인원 범위와 생산관리 수정 권한을 유지한다.

## 검증 계약

- Backend API: Legacy 프로젝트 기간·1:1 연결 저장/재조회, 과거 Legacy 제조 실행 projection, 행 삭제·재추가 순번 정규화, 삭제 행 검증 제외
- Backend 회귀: LinkedV1 프로젝트 자동 실적, UL891 세트 기본·개별 일정, master는 새 프로젝트에만 적용
- Frontend unit: model version과 무관한 통합 조회·편집, 체크형 달력 미표시, 연결 실적 표시, 행 추가·삭제·재추가 payload 순번
- Frontend lint, typecheck, 관련 unit, production build
- Full-Stack 사용자 경로: 기존 프로젝트 수정 저장 후 계획·실적 2막대 일정표 및 연결 실적 확인
- 구현 세션과 분리된 read-only 검증 후 사용자 일괄 검수 대기

## Finding

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `PRODUCTION-CONTROL-LEGACY-DUAL-UI-001` | P2 | `RESOLVED` | Legacy 프로젝트는 단일 예정일·체크 달력만 사용해 프로젝트 전용 실적 연결을 저장·조회할 수 없었다. | 기존 행과 단일 예정일 호환값을 보존하면서 기간·실적 연결 저장을 열고, 조회를 단일 생산계획표와 계획·실적 2중 막대 일정표로 통합했다. |
| `PRODUCTION-CONTROL-ROW-SEQUENCE-001` | P2 | `RESOLVED` | 삭제 행이 내부 배열에 남고 새 행이 활성 행 수로 순번을 정해 활성 순번이 중복될 수 있었다. | 미저장 행 즉시 제거, 저장 행 비활성화, Frontend 저장 전·Backend 저장 시 활성 순번 `1..N` 재부여와 충돌 없는 임시 순번 이동으로 해결했다. |

Open P0/P1/P2: `0/0/0`.

## 구현 결과

- 새 프로젝트는 계속 생성 당시 양식 snapshot을 사용하고, 기존 프로젝트는 master 양식 소급 적용 없이 자체 계획 행·기간·실적 연결을 저장한다.
- 기존 프로젝트의 제조 실적은 양식 version이 달라도 안정된 제조 항목 코드가 같으면 기존 실행 근거를 집계한다.
- 생산계획 header가 아직 없는 오래된 프로젝트도 실적 선택 목록과 제조 항목을 먼저 조회하고 첫 저장에서 프로젝트 전용 Legacy 계획을 생성한다.
- 프로젝트 조회는 생성 시점 model과 관계없이 `연결 실적`이 보이는 생산계획표와 계획 흰색·실적 검은색 2중 막대 일정표만 사용한다.
- 프로젝트 수정은 활성 계획 행마다 기간과 실적 데이터 하나를 요구한다. 기존 LQC 상세 identity가 없는 Legacy 프로젝트는 오연결을 막기 위해 LQC 신규 선택만 중지한다.
- UL891 세트 공통 구조·전체 기본계획·세트별 일정 overlay는 변경하지 않았다.
- 행 추가→일부 삭제→재추가→저장을 반복해도 활성 행은 `1..N`으로 저장되고 삭제 행은 검증과 응답에서 제외된다.
- DB schema 변경과 migration은 없다.

## 자동 검증 결과

| 검증 | 결과 |
| --- | --- |
| Backend Release build | PASS — 경고 0, 오류 0 |
| 생산계획 Backend API 전체 class | PASS — `26/26` |
| Legacy 통합 기간·연결·제조 실적·삭제 순번 집중 회귀 | PASS |
| Frontend lint | PASS — error 0, 기존 Fast Refresh warning 1 |
| Frontend typecheck | PASS |
| Frontend 전체 unit | PASS — `213/213` |
| Frontend production build | PASS — 기존 chunk size warning만 유지 |
| Full-Stack 회귀 source 동기화 | PASS — 기존 단일 예정일·체크 달력 기대를 기간·1:1 연결·2중 막대 기준으로 갱신 |
| Git diff whitespace 검사 | PASS |

## 게시 상태

- local implementation: 완료
- 사용자 검수: 대기 — 관리자 양식 권한 변경과 마지막 일괄 검수
- commit/push/PR/merge/Azure: 미승인·미실행
