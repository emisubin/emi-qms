# TASK-ADMIN-002 Change 002 Implementation report — 양식 편집·저장 진입 복구

## 1. 요약과 상태

- 상태: `IMPLEMENTED / AUTOMATED_VALIDATION_COMPLETE / USER_VALIDATION_PENDING`
- 증상: 양식 관리 기본 화면에 `편집`, `저장` 버튼이 없어 기능을 사용할 수 없는 것으로 보였다.
- 원인: 기존 구현은 `새 초안`이 편집 진입 역할을 대신했고, `초안 저장`은 Draft를 선택한 뒤 하단에만 조건부로 표시했다.
- 해결: 편집 영역 상단에 `편집`, `저장`을 항상 노출하고, Active 보호를 유지한 채 `편집`이 Draft 진입을 담당하게 통일했다.
- 대표 repo·`main`·Persistent UAT·Backend·DB: 변경 없음
- main merge 승인: `0/3`

## 2. 사용자 동작

1. Active 또는 Archived 버전을 조회하면 `편집`은 활성, `저장`은 비활성 상태로 보인다.
2. `편집`을 누르면 기존 Draft가 있을 때 가장 최신 Draft를 열고, 없을 때 Active를 복제해 새 Draft를 만든다.
3. Draft가 열리면 항목 입력과 `저장`이 활성화된다.
4. 저장 뒤에도 Draft 상태를 유지하며, 별도의 `활성화`를 눌러야 이후 새 업무에 적용된다.

Active/Archived 직접 수정 금지, Draft-only 저장, 기존 검사·제조 snapshot 불변과 서버 권한은 바꾸지 않았다.

## 3. 변경 파일

- `frontend/src/FormTemplateManagementPage.tsx`: 편집·저장 상시 노출, 기존 Draft 재개, Draft 생성·저장 상태 연결
- `frontend/src/styles.css`: Desktop·Mobile 편집 상단 action 정렬
- `frontend/tests/FormTemplateManagementPage.test.tsx`: Active→Draft→저장 회귀 테스트
- `frontend/e2e/full-stack/sales-kpi-form-templates.full-stack.spec.ts`: E2E 버튼 계약 갱신
- `tasks/admin-002-change-002.md`: 승인 범위와 불변조건
- 이 문서: 실제 구현·검증·남은 사용자 검수 기록

## 4. 검증 결과

| 검증 | 결과 |
| --- | --- |
| 신규 양식 편집 회귀 테스트 | `1/1` 성공 |
| Frontend 전체 unit | `136/136` 성공 |
| Frontend typecheck | 성공 |
| Frontend lint | error `0`, 기존 `main.tsx` Fast Refresh warning `1` |
| Frontend production build | 성공, 기존 chunk-size warning 유지 |
| 고정 검수 runtime Desktop | 양식 화면 표시, `편집` 활성·`저장` 비활성 확인 |
| 390px adaptive browser | `편집`·`저장` 표시, horizontal overflow `0` |

고정 검수 DB에 검증용 Draft나 양식 내용 변경을 남기지 않았다. 실제 POST/PUT 전환은 격리 unit에서 검증했다.

## 5. Finding gate

| ID | Severity | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `ADMIN-002-HIDDEN-EDIT-ENTRY` | P2 | `RESOLVED` | 기술 용어인 `새 초안`이 편집 진입을 대신하고 저장은 조건부로 숨겨져 기능 부재로 인식됨 | `편집`·`저장` 상시 노출, 안전한 Draft 진입으로 통일 |

Open P0/P1/P2: `0/0/0`.

## 6. 사용자 검수

- [ ] `dev-admin`으로 `양식 관리` 진입
- [ ] Active 버전에서 `편집` 활성·`저장` 비활성 확인
- [ ] `편집` 클릭 후 항목 입력과 `저장` 활성 확인
- [ ] 항목을 변경해 `저장` 후 성공 안내 확인
- [ ] 필요 시 `활성화` 전까지 운영 양식이 바뀌지 않는지 확인

## 7. 게시·Rollback

- local working tree에만 반영했다. commit·push·PR·merge는 수행하지 않았다.
- 되돌릴 때는 이 Change의 Frontend·test·문서 diff만 제거한다.
- 대표 repo와 `main`은 그대로이며 merge 승인 `0/3`이다.
