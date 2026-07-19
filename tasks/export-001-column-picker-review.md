# TASK-EXPORT-001 Change 003 — Codex 내용 Review

## 1. Review 결론

- 결론: `APPROVE_WITH_RESOLUTION`
- 대상: [Fable 1차 기획](export-001-column-picker-planning.md)
- 사용자 문제 적합성: 고정 workbook에서 필요 없는 열을 매번 삭제하는 반복 비용을 직접 줄인다.
- Roadmap 정합성: Product Roadmap·실험 완료 원장의 정확한 optional Next Gate다.
- 구현 가능성: 현재 단일 POST·20개 screen registry·공통 tray·서버 `ExcelColumn<T>` 구조 위에 bounded metadata와 optional column key를 추가할 수 있다.
- blocking decision: `0`
- 구현 source: 이 review를 반영한 Fable 2차 기획만 최종 계약으로 사용한다.

## 2. 제품 방향 Review

### 유지

1. 서버 allowlist 기반 metadata + 기존 POST optional `columns`
   - 사용자 유연성을 제공하면서 client가 header·selector·cell value를 정의하지 못하게 한다.
   - 기존 client가 `columns`를 보내지 않으면 현재 기본 workbook을 그대로 생성해 호환 비용이 낮다.
2. 기존 화면 read permission·scope 재사용
   - export 전용 권한을 중복 생성하지 않고 화면에서 볼 수 있는 것 이하만 내려받는 원칙을 유지한다.
3. 화면 체류 중 임시 상태, persistence 제외
   - 서버 preset·migration 없이 사용자가 한 작업 흐름 안에서만 선택을 재사용한다.
4. mobile simple-mode 보존
   - 이번 기능은 desktop 대량 관리 편의이며, 모바일 현장 화면에 Excel·checkbox를 다시 넣지 않는다.
5. 단일 export action·기존 선택 lifecycle·formula/resource/audit 계약 보존
   - 완료된 Change 002를 재설계하지 않고 필요한 능력만 얹는다.

### 추가

1. 20개 화면별 필수 업무 식별 컬럼 matrix
   - 1차 기획은 “필수 식별 컬럼 1개 이상”만 정했지만 구현자가 화면마다 임의 선택하면 workbook 이해 가능성이 달라진다.
   - 2차 기획은 각 screen의 필수 column key·한글 label을 표로 고정한다. 내부 GUID가 아니라 화면에서 사용자가 행을 식별하는 표시 코드·제목·날짜 중 최소 조합이어야 한다.
2. Column key 입력 경계
   - 요청은 `null/미전달`, 빈 배열, 중복, 미지원, 권한 상실, 필수 누락을 구분하되 사용자에게 내부 key를 노출하지 않는 generic 422를 사용한다.
   - key는 server-issued ASCII kebab-case, ordinal exact match로 고정하고 trim·case-fold 자동 보정을 하지 않는다.
   - 요청 column 수는 해당 screen 허용 수 이하, key 길이는 64 bytes 이하로 제한해 body abuse와 ambiguous normalization을 차단한다.
3. Single source 보장
   - metadata와 workbook filtering이 서로 다른 목록을 읽지 않도록 동일 `GetEffectiveColumns(screen,user)` 결과를 사용한다.
   - 프로젝트 매출 permission도 metadata·POST validation·실제 workbook·audit flag 네 지점이 동일 effective definition을 사용한다.
4. Stale metadata 복구
   - metadata version field나 별도 persistence는 추가하지 않는다.
   - POST 422 뒤 picker를 다시 열 때 해당 screen cache를 폐기하고 재조회한다. 일부 열을 silent drop하지 않는다.
5. 증빙 효율
   - 공통 component 존재만으로 20개 화면 적용을 주장하지 않고 registry 전체 contract를 자동 검증한다.
   - screenshot은 20개 desktop picker 진입 상태를 남기되, 실제 workbook 재파싱·Excel 시각 증빙은 업무 1개·관리자 1개 대표로 제한한다. 모바일은 기능 부재·overflow 0 대표 증빙으로 충분하다.

### 보류

1. 사용자별 preset·server/localStorage persistence
   - stale schema·개인화 migration·복구 정책 비용이 현재 편의 가치보다 크다.
2. 컬럼 순서 drag, 이름 변경, 계산식·신규 사용자 정의 열
   - allowlist 부분집합 선택보다 권한·workbook 의미 계약이 크게 확장된다.
3. form template custom export picker
   - 공통 registry가 아닌 별도 endpoint·workbook이므로 이번 20개 화면 계약에 섞지 않는다.
4. multi-sheet·CSV/PDF·async storage·필터 전체 결과 선택
   - 다른 사용자 능력과 운영 경계다.

### 제거·수정

1. “popover를 닫은 뒤에만 export” 강제
   - 사용자가 열 선택을 마친 뒤 별도로 닫는 단계는 가치가 없고 keyboard·pointer flow를 늘린다.
   - export button은 picker panel 안 footer 또는 panel과 인접한 기존 action에서 현재 선택으로 즉시 실행할 수 있어야 한다. 실행 시 panel은 닫고 focus/feedback을 기존 action 계약으로 넘긴다.
2. metadata 실패 시 모호한 상태 유지
   - 최초 metadata 실패라면 기존 기본 export를 계속 허용한다.
   - 이전에 custom selection이 있었는데 refresh가 실패한 경우에는 stale key를 보내지 않고 기본값 사용 여부를 명확히 표시한다. 조용한 fallback으로 사용자가 고른 열과 다른 파일을 만들지 않는다.
3. Fable 원문 앞 설명 문장
   - 1차 artifact가 H1 전에 영어 baseline 설명을 포함했다. 내용은 유효하지만 최종 2차 기획은 첫 byte가 H1이어야 하며 preface를 포함하지 않는다.

## 3. Finding과 Resolution

| Finding | Severity | 상태 | 영향 | 2차 기획 Resolution |
| --- | --- | --- | --- | --- |
| `COLUMN-REQUIRED-MATRIX-UNSPECIFIED` | P2 | `RESOLVED_FOR_REDRAFT` | 화면별 필수 열을 구현자가 임의 선택할 수 있음 | 20개 screen 필수 key·label matrix 고정 |
| `COLUMN-KEY-BOUNDARY-INCOMPLETE` | P2 | `RESOLVED_FOR_REDRAFT` | trim/case·길이·개수 규칙이 모호해 silent normalization·body abuse 가능 | ASCII kebab-case·ordinal exact·64 bytes·허용 count 이하 |
| `COLUMN-METADATA-DRIFT` | P1 | `RESOLVED_FOR_REDRAFT` | metadata와 workbook 검증 목록이 다르면 권한 열 노출 또는 정상 요청 거부 | 하나의 effective column source를 네 지점에서 재사용 |
| `COLUMN-POPOVER-EXTRA-STEP` | P3 | `RESOLVED_FOR_REDRAFT` | 닫기 후 export 강제가 작업 흐름을 늘림 | 선택 후 즉시 export 가능, 실행 시 focus/feedback handoff |
| `COLUMN-STALE-FALLBACK-AMBIGUOUS` | P2 | `RESOLVED_FOR_REDRAFT` | metadata 실패 시 사용자 선택과 다른 기본 파일이 조용히 생성될 수 있음 | stale selection 폐기·명시 안내·사용자 action으로 기본 export |
| `FABLE-FIRST-PLAN-PREFACE` | P3 | `RESOLVED_FOR_REDRAFT` | 최종 artifact 형식 오염 위험 | 2차 기획 첫 byte H1, preface 0 |

Open P0/P1/P2는 `0/0/0`이며 위 항목은 2차 기획에 반영될 때 구현 Go다.

## 4. 권장 개발 순서

1. 서버 effective column registry와 20-screen contract test.
2. metadata GET·POST request boundary·권한/필수/중복/길이 검증.
3. workbook 부분집합·프로젝트 매출 audit flag·기존 columns 미전달 회귀.
4. Frontend metadata API와 공통 picker state·접근성·stale recovery.
5. registry 전수·대표 workbook Full-Stack E2E, desktop/mobile screenshot.
6. 전체 Backend·Frontend 회귀, Finding·privacy·산출물 gate.

## 5. Review resolution 승인 상태

- 유지·추가·보류·제거 resolution: 실험 fast-track standing rule로 자동 채택.
- 2차 기획 요청: 승인.
- 구현 승인: Fable 2차 기획의 `openBlockingDecisionCount: 0` 조건부 승인.
- local commit: 승인.
- push·PR·merge·Persistent UAT·provider: 미승인.
- main merge 승인: `0/3`.
