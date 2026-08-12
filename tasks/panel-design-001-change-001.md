# TASK-PANEL-DESIGN-001 Change 001 — 열반 번호·용어·전체 크기 보정

- taskType: `BUGFIX`
- approvalSource: `USER_EXPLICIT_CORRECTION_REQUEST`
- approvalDate: `2026-08-12`
- canonicalTaskId: `TASK-PANEL-DESIGN-001`
- branch: `feat/task-panel-design-001-grouping`
- instructionChainRead: `true`
- taskIdentityGate: `PASS_REUSE`
- roadmapSequenceMatch: `true`
- implementationApproved: `true`
- commitApproved: `true`
- publicationApproved: `true`
- approvalScope: `USER_VALIDATION_COMPLETE_MAIN_MERGE_AND_PUBLIC_DEPLOYMENT`

## 확인된 증상과 원인

1. 새 패널 관계를 만들 때 기존 번호의 최댓값에 1을 더해 임시 번호를 그대로 표시했기 때문에, 열반을 다시 구성할수록 번호가 계속 증가했다.
2. 사용자 업무 용어는 `열반`인데 화면과 한글 validation·감사 표시는 `묶음`으로 구현되어 있었다.
3. 열반 크기에서 W 합계만 표시해 사용자가 출하 상태의 전체 최외곽 W/H/D를 한눈에 확인할 수 없었다.

## 승인된 수정 범위

1. 열반 생성·재구성·해제 후 현재 활성 열반을 구성 패널의 첫 순번 기준으로 정렬하고 `열반 1, 2, 3…`으로 항상 다시 번호를 매긴다.
2. 번호 재정렬로 바뀐 기존 열반 구성원도 같은 저장 요청에 포함해 화면과 저장값을 일치시킨다.
3. 패널 출하 관계를 뜻하는 사용자 노출 용어를 `묶음`에서 `열반`으로 변경한다. 내부 field·schema 이름은 호환성을 위해 유지한다.
4. 열반 크기는 `구성 패널 W 합계 × H 최댓값 × D 최댓값`으로 표시한다. 어느 패널이든 W/H/D가 비어 있으면 값을 추정하지 않고 `사이즈 입력 필요`로 표시한다.
5. 입력 화면의 저장 전 미리보기와 프로젝트 설계 탭의 PC·모바일 결과 표시에 같은 계산식을 적용한다.

## 보존할 불변조건

- UL891은 기존 세트 구조를 사용하며 일반 Item 열반 기능에서 제외한다.
- 각 패널의 개별 W/H/D 원본은 변경하지 않는다.
- 열반은 활성 패널 2면 이상이며 한 패널은 하나의 열반에만 속한다.
- 서버 권한·원자 저장·동시성·감사 이력 계약을 유지한다.
- 기존 흑백 wireframe, 일반 테두리와 강조선 금지 규격을 유지한다.
- 제조·품질·물류 workflow와 포장·출발·납품에서 사용하는 별도 `묶음` 개념은 변경하지 않는다.

## 검증 계획

- Frontend unit: 열반 1·2 생성 후 1을 해제하면 남은 열반이 1로 재번호화되어 저장되는지 확인
- Frontend unit·mock UI: `1700 × 1800 × 400 mm` 전체 열반 크기와 용어 확인
- Backend targeted: 한글 validation 변경과 기존 원자 저장·UL891 차단 회귀 확인
- Targeted Full-Stack: 입력·저장·설계 상세 전체 열반 크기 확인
- 실제 검수 runtime: 저장된 일반 Item 결과와 UL891 제외 상태 확인
