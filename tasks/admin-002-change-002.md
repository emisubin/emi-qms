# TASK-ADMIN-002 Change 002 — 양식 편집·저장 진입 복구

## Task Identity Gate

- instructionChainRead: `true`
- taskType: `BUGFIX`
- canonicalTaskId: `TASK-ADMIN-002`
- reuseExistingTask: `true`
- roadmapSequenceMatch: `true`
- gateStatus: `PASS_REUSE`
- branch: `experiment/task-home-002-personalized-shell`
- startHead: `2247643c8b28eb6cedd18b43e72d8b64aac53fe4`

## 사용자 검수 Finding

- 증상: 양식 관리의 기본 Active 버전 화면에 `편집`, `저장` 버튼이 보이지 않아 양식을 바꿀 수 없는 것으로 인식된다.
- 원인: 기존 UI는 중앙의 `새 초안`으로만 편집을 시작하고, Draft를 선택한 뒤에만 하단에 `초안 저장`을 조건부 표시했다. Backend의 Draft 생성·저장 API와 권한은 정상이다.
- 영향: 사용자가 정상 편집 경로를 발견하기 어렵고, Active 불변 정책이 기능 미제공으로 오인된다.

## 승인 범위

- Active/Archived 화면에도 `편집`, `저장`을 항상 노출한다.
- `편집`은 기존 Draft가 있으면 가장 최신 Draft를 열고, 없으면 Active를 새 Draft로 복제한다.
- Draft에서만 입력과 `저장`을 활성화한다.
- Active/Archived 직접 수정 금지, Draft→Active lifecycle, 권한·감사·기존 snapshot 불변은 유지한다.
- 대표 repo·`main`·Persistent UAT·push·PR·merge는 변경하지 않는다.

## 검증 계약

- 기본 Active 화면에서 `편집` 활성, `저장` 비활성 상태가 보인다.
- `편집` 후 Draft가 선택되고 입력·`저장`이 활성화된다.
- `저장`은 기존 Draft item 저장 API를 사용하며 성공 피드백을 표시한다.
- Desktop과 390px에서 주요 버튼이 화면 밖으로 밀리지 않는다.
