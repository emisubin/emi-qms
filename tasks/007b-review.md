# TASK-007B — 패널·프로젝트 병목 상태 집계 Codex 내용 Review

- reviewOwner: `CODEX`
- reviewSource: `tasks/007b-planning.md`
- reviewStatus: `RESOLVED_FOR_EXPERIMENT_IMPLEMENTATION`
- experimentalImplementationApprovalSource: `USER_STANDING_EXPERIMENT_DIRECTIVE`
- canonicalMainApproval: false
- mainMergeApprovalCount: 0/3

## 결론

Fable 기획의 계산형 aggregate 권장안은 현재 Repository와 사용자 문제에 맞다. 프로젝트 사용자가 진행률, 패널 구간과 open Pending을 따로 대조하는 탐색 비용을 줄이면서 원본 workflow·Pending 계약을 변경하지 않는다. 이번 실험은 기존 프로젝트 목록·상세 응답을 additive 확장하고 서버에서 정렬·권한을 강제하는 vertical slice로 구현한다.

## 유지

- 대표 병목과 open Pending을 서로 다른 의미로 표시한다. Pending은 프로젝트 상태를 `중단`으로 바꾸지 않는다.
- `Closed` 제외 Pending을 open으로 집계하고 재검사 대기·긴급 건수를 병기한다.
- 동률 패널은 임의의 한 패널을 고르지 않고 `구간 + n면`으로 표시한다.
- 기존 진행률과 FAT optional 분모를 그대로 사용하고 Frontend에서 재계산하지 않는다.
- 프로젝트 목록·상세 응답을 additive 확장하고 Pending 목록에 프로젝트 filter를 추가한다.

## 추가

- 패널 원본은 7개 coarse stage만 가지므로 화면과 API에서 `정확한 18단계`가 아니라 `병목 구간`으로 명명한다.
- Pending 파생 필드는 `Pending.Read`가 있을 때만 채우고 권한이 없으면 nullable field를 생략한다. 권한이 없는 사용자의 정렬에도 Pending 존재를 사용하지 않는다.
- 기본 정렬은 기존 lifecycle 상태를 보존한 뒤 같은 상태 그룹에서 `open Pending → 더 이른 병목 구간 → 기존 납기일` 순으로 서버가 pagination 전에 계산한다.
- 계산 불가 입력은 `Uncertain`, 활성 패널이 없으면 `NoData`, 완료 프로젝트는 `Completed`로 명시해 거짓 완료를 피한다.
- 프로젝트 상세에 `다음 확인 대상`, open·재검사·긴급 수치와 7개 패널 구간 matrix를 한 surface로 제공한다.

## 보류

- 사용자가 선택하는 정렬 toggle은 핵심 가치 검증 뒤 추가한다. 이번 실험은 서버 기본 병목 정렬 한 가지로 제한한다.
- persisted snapshot은 실제 목록 성능 문제를 측정한 뒤 후속 Task로 판단한다.
- 패널 단위 Pending 귀속과 blocked flag는 Pending model 확장이므로 TASK-007A 후속 신규 기능으로 분리한다.
- Home widget은 TASK-HOME-001에서 aggregate 계약을 재사용한다.

## 제거

- 패널 7개 구간을 임의의 단일 18단계 번호로 환원하는 표시.
- Frontend가 workflow·Pending 원본을 받아 aggregate와 정렬을 자체 계산하는 구조.
- 이번 Task의 신규 table, trigger, cache invalidation과 알림 발송.

## 구현 순서

1. 병목 API 계약과 상태 matrix helper
2. 프로젝트 목록·상세 SQL의 계산형 aggregate·권한·서버 정렬
3. Pending 목록의 project filter와 filter summary
4. 프로젝트 목록·상세·Pending Frontend deep link와 responsive surface
5. Backend·Frontend·isolated full-stack tests
6. Desktop·390px synthetic screenshot과 implementation report

## Finding과 resolution

| ID | Severity | 상태 | 내용 | Resolution |
| --- | --- | --- | --- | --- |
| `007B-PANEL-GRANULARITY` | P2 | `RESOLVED` | 패널은 18단계가 아닌 7개 구간만 저장해 정확한 단계 표시가 불가능 | `병목 구간` label과 고정 7구간 matrix만 사용 |
| `007B-PAGINATION-SORT` | P2 | `RESOLVED` | page 조회 후 client 정렬은 전체 우선순위를 깨뜨림 | Backend SQL에서 pagination 전에 권한-aware 병목 정렬 적용 |
| `007B-PENDING-LEAK` | P2 | `RESOLVED` | Pending 권한 없는 사용자에게 count 또는 정렬 순서로 존재가 노출될 수 있음 | `Pending.Read`를 endpoint에서 전달하고 field·정렬 양쪽에 적용 |
| `007B-SNAPSHOT` | P3 | `RESOLVED` | persisted aggregate는 stale·migration·이중상태 위험이 있음 | Fable 권장안 A인 계산형 조회 채택, 성능 실측 전 저장하지 않음 |
| `007B-SORT-TOGGLE` | P3 | `BACKLOG` | 기존 정렬 전환 선택 UI는 핵심 slice에 필수 아님 | 기본 병목 정렬 검수 후 후속 change 후보 |

## 구현 판정

현재 experiment branch 구현은 `GO`다. 사용자는 Fable 권장안·review resolution·구현을 별도 승인 왕복 없이 진행하도록 명시했다. 이 판정은 push, PR, 대표 repo 또는 main merge 승인이 아니다.
