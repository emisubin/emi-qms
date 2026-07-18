# TASK-HOME-002 — Codex 1차 기획 검토

> 검토 대상: `tasks/home-002-planning.md`
> 검토 관점: 사용자 문제·제품 방향·실제 Repository 경계·운영 부담
> 결론: **조건부 유지 — 아래 resolution을 2차 기획에 반영하면 experiment 구현 가능**

## 1. 사용자 문제와 기대 결과

Fable 1차 기획은 사용자의 핵심 요구를 정확히 잡았다. 지금 공통 셸은 실제 로그인 사용자보다 개발·검수 selector와 중복 자재 shortcut이 더 눈에 띄고, Home은 부서와 무관한 공통 요약만 제공한다. 따라서 모든 업무 화면의 계정 identity, profile photo 변경, full-height navigation, selector 재배치, 부서별 Home 지표를 하나의 `TASK-HOME-002`로 묶는 것은 목적 정합성이 있다.

다만 “로그인된 사용자”와 “검수 전환으로 현재 보고 있는 사용자”는 같은 개념이 아니다. 상단 identity와 사진 변경 주체는 `actualUser`, Home 지표와 업무 권한은 `effectiveUser`를 기준으로 해야 한다. 이 구분이 UI·API·테스트에 명시되지 않으면 System Administrator가 다른 사람 사진을 바꾸거나 지표 소유자를 오인할 수 있다.

## 2. 제품 방향·Roadmap 정합성

- 기존 `TASK-HOME-001`의 공통 4개 widget과 모바일 우선 원칙을 보존하므로 제품 방향과 맞는다.
- 사용자가 이번 작업을 Roadmap 순서보다 먼저 수행하도록 명시했고 `tasks/home-002-change-001.md`에 override가 고정되어 있으므로 experiment fast-track의 순서 gate는 통과한다.
- reference screenshot의 핵심은 파란색 복제가 아니라 얇은 상단 header, viewport 전체 높이의 밝은 sidebar, compact control, 낮은 shadow와 넓은 content canvas다. EMI red·white token으로 번역하는 방향이 적절하다.
- profile binary는 운영 storage 정책을 새로 확정하는 방식이 아니라 사용자당 현재 사진 1장·5MB 상한의 bounded DB row로 제한할 때에만 experiment 범위에서 허용한다. 대표 repo·Persistent UAT 승격 전 storage·backup·retention 결정은 계속 별도 blocker다.

## 3. 기능별 판정

| 기능 | 판정 | 근거·resolution |
| --- | --- | --- |
| 모든 페이지의 사용자 사진·부서명·이름 | 유지 | 실제 로그인 계정인 `actualUser`를 표시한다. 검수 전환 중에는 `effectiveUser`를 “현재 검수 화면”으로 별도 작은 문구/기존 banner에 표시한다. |
| Desktop account popover | 유지 | avatar trigger, 바깥 클릭·Escape·focus 복귀, 업로드/로그아웃을 포함한다. 별도 modal로 키우지 않는다. |
| Mobile account sheet | 유지 | 기존 오른쪽 `상태` trigger를 avatar trigger로 교체한다. API/DB 상태는 계정의 주목도를 해치지 않도록 보조 영역으로 축소하거나 기존 개발 상태 정보로 유지한다. |
| popover 안 avatar 클릭 업로드 | 유지 | 숨은 file input을 label/button으로 연결하고 JPEG/PNG·5MB를 client에서 선검사하되 서버가 최종 검증한다. 성공 전 기존 사진을 유지한다. |
| 사진 제거 | 추가 유지 | 사용자가 실수로 올린 사진을 되돌릴 수 있어 lifecycle상 필요하다. popover의 보조 action으로 둔다. |
| dev/검수 selector 재배치 | 유지 | desktop sidebar footer와 mobile drawer footer로 이동한다. 기존 노출 조건과 전환 동작은 바꾸지 않는다. |
| sidebar full-height | 유지 | desktop만 viewport 고정 높이와 내부 navigation scroll/footer 고정 구조를 사용한다. mobile은 기존 drawer다. |
| topbar 자재 shortcut 제거 | 유지 | 왼쪽/모바일 navigation의 자재 메뉴가 canonical 진입점이다. |
| 부서별 Home 지표 | 유지 | `effectiveUser`의 부서와 permission으로 서버에서 3개 이하의 핵심 집계만 반환한다. 공통 widget은 그대로 둔다. |
| 신규 aggregate endpoint | 유지 | Frontend의 부서별 다중 목록 API 호출을 피한다. endpoint는 현재 부서에 해당하는 bounded query만 실행하고 기존 목록 payload를 조합하지 않는다. |
| 사용자당 DB bytea 사진 | 조건부 유지 | 한 사용자 1행, 5MB, cascade purge, hash/version을 둔다. 실제 외부 storage나 Graph permission은 도입하지 않는다. |
| 별도 photo audit table | 유지 | bytes·파일명·email을 남기지 않고 action, hash, 크기, MIME, actor, 시각만 append-only로 기록한다. |
| client crop/resize | 보류 | 현재 Repository에 공통 사진 압축/crop 구현이 확인되지 않았고 EXIF·canvas 처리까지 범위가 커진다. v1은 `object-fit: cover` 표시와 서버 한도만 적용한다. |
| Graph profile photo | 보류 | 신규 외부 permission·provider·동기화 정책이 필요하므로 별도 `NEW_FEATURE`다. |
| 모든 업무 페이지 정보구조 재설계 | 제거 | 이번 reference 적용 범위는 공통 셸과 Home이다. 페이지별 업무 화면을 동시에 재설계하면 검증 범위가 과도해진다. |

## 4. Repository 대조 Finding과 resolution

### F1 — `/api/me`는 department code만 노출한다

현재 `CurrentUserResponse`와 `CurrentUserPrincipalResponse`는 `Department` code만 내보내며 `UserAuthorizationProfile` 자체에는 `DepartmentName`이 이미 있다.

**Resolution:** 기존 `department` 필드는 호환성을 위해 유지하고 principal과 top-level에 `departmentName`을 additive로 추가한다. `actualUser`와 `effectiveUser` 각각의 표시명을 독립적으로 projection한다. 사진 cache invalidation에는 bytes 자체가 아니라 `profilePhotoVersion` 또는 content hash 기반 opaque version만 `/api/me`에 추가한다.

### F2 — actual/effective self-scope를 분리해야 한다

현재 `/api/me`는 effective profile을 top-level로 반환하고 actual profile을 nested field로 제공한다. Dev mode에서는 둘이 같지만 Entra admin test switch에서는 다르다.

**Resolution:** profile photo `GET/PUT/DELETE`의 대상은 `ActualUserId` claim, 없으면 `UserId` claim으로 결정한다. request에서 user id를 받지 않는다. Home metrics는 effective `UserId`, department와 permissions/access scope를 사용한다. Frontend identity badge는 `actualUser`, Home 인사·지표는 `effectiveUser`를 사용한다.

### F3 — 기존 사진 검증은 signature sniff 수준이다

IQC·품질·물류 사진은 5MB 상한과 JPEG/PNG signature를 검사하지만 완전한 pixel decode 유틸이나 image library dependency는 없다. 따라서 1차 기획의 “decode 가능성 검증”을 그대로 구현 완료 조건으로 쓰면 실제 계약보다 강한 주장을 하게 된다.

**Resolution:** 신규 공통 profile image validator는 MIME declaration을 신뢰하지 않고 bytes로 JPEG/PNG를 판정하며 최소 구조·종료 marker와 합리적인 image dimension을 확인한다. 별도 decoder package를 추가하지 않는 한 “완전 decode 검증”으로 보고하지 않는다. 유효하지 않은 파일은 저장 전 400 validation으로 거부한다.

### F4 — 사용자 purge guard와 cascade의 관계

`AdminScheduledDeletionService`는 업무·감사 참조가 있으면 user purge를 보류하고, 참조가 없을 때 `qms_users`를 삭제한다.

**Resolution:** 현재 사진 테이블은 `qms_users(id) on delete cascade`로 삭제한다. append-only photo audit도 개인정보 lifecycle상 사용자 purge와 함께 cascade되도록 하되, 일반 사진 제거에서는 audit을 보존한다. purge guard의 참조 column 탐색 대상에 photo owner/actor가 들어가 영구적으로 purge를 막지 않도록 photo tables는 self-owned cascade 예외로 설계한다.

### F5 — 지표 데이터 원천은 이미 있지만 contract가 서로 다르다

Repository에는 project dashboard, production planning summary, procurement dashboard, material receipt summary, manufacturing queue, quality inspection queue, logistics queue, admin dashboard가 존재한다. 이들을 Frontend에서 병렬 호출하면 payload·권한·오류 상태가 부서마다 달라진다.

**Resolution:** 신규 `HomeMetricsStore`는 부서당 최대 3개 metric만 계산하고 하나의 fixed response로 반환한다. SQL이나 기존 store 호출은 해당 부서 경로만 실행한다. 모든 count는 기존 project access scope·permission invariant를 그대로 적용한다. 최소 mapping은 다음과 같다.

- administration/System Administrator: 승인 대기 사용자, 발송 실패, active escalation
- sales: 담당 active 프로젝트, 임박 납기, 정산 대기
- design: 패널 정보 미완료 프로젝트, 패널 정보 미완료 panel, 설계 단계 진행 중
- production-planning: 계획 미등록, 계획 중, 담당 미지정
- procurement: 입고 예정 대기, 입고 지연, 입고 완료
- materials: 도착 등록 대기, IQC 대기, 키팅 대기 panel
- manufacturing: 제조 대기, 진행 중, 차단
- quality: 검사 대기, 재검/조치 대기, 판정 완료
- logistics: 포장 대기, 출발 대기, 배송 완료 대기
- readonly/부서 없음/허용 metric 없음: 부서 지표 미표시

지표 label·tone·destination은 서버가 arbitrary URL을 반환하지 않고 allowlisted `destinationKey`만 반환한다. Frontend가 key를 기존 `View`로 변환한다.

### F6 — 승인 대기 화면은 현재 공통 shell 전에 차단될 수 있다

1차 기획은 승인 대기 사용자에게 identity surface를 보여 주는 것을 전제로 하지만 현재 인증 gate가 업무 shell 진입을 차단한다.

**Resolution:** 이번 Task에서 인증 gate 구조를 풀지 않는다. 승인 대기 사용자는 기존 access 화면의 identity·logout 계약을 유지하고 profile mutation은 금지한다. “모든 페이지”는 active 업무 shell route를 뜻하며 승인 대기 전용 화면의 재설계는 제외한다.

### F7 — mutation guard와 cache 계약

ReviewSafe middleware는 mutation을 차단한다. avatar bytes를 browser object URL로 표시하면 사용자 전환·photo version 변경 때 revoke가 필요하다.

**Resolution:** ReviewSafe에서는 업로드·제거 control을 disabled하고 이유를 표시하며 서버 guard를 최종 기준으로 유지한다. GET은 `ETag`와 private cache header를 사용하고, Frontend object URL은 effect cleanup에서 revoke한다. 전환 generation guard로 늦은 photo/metrics 응답을 폐기한다.

## 5. 권장 개발 순서

1. `0042` additive migration과 profile photo store/validator/API, `/api/me` additive projection, purge cascade 테스트.
2. Home metrics fixed contract·store·endpoint와 부서/permission/access-scope 테스트.
3. Frontend API/types, actual-user identity badge, desktop popover, mobile account sheet, upload/remove feedback.
4. selector를 sidebar/drawer footer로 이동하고 중복 자재 shortcut 제거, full-height compact shell CSS.
5. Home department metrics + reference 기반 compact layout, 기존 HOME-001 widget 회귀.
6. backend/frontend 전체 검증, isolated fresh/existing migration, desktop/390px synthetic screenshot, privacy-safe 보고.

## 6. 최종 resolution

- `유지`: account identity, popover/sheet, self photo lifecycle, selector footer, full-height sidebar, 중복 자재 제거, 부서별 Home 지표, A+C+E 구조.
- `추가`: actual/effective 명시 분리, additive departmentName/photoVersion, allowlisted destination key, structural image validation, cascade/purge 계약, object URL cleanup.
- `보류`: crop/resize, Graph 사진, 운영 storage·retention 결정, photo audit 관리자 조회 UI.
- `제거`: 승인 대기 gate 재설계, 전체 업무 페이지 정보 구조 재설계, Frontend의 부서별 목록 API 병렬 조합.

`openBlockingDecisionCount=0`. 위 resolution은 사용자 standing experiment rule의 권장안 자동 채택 범위이며, Fable 2차 기획의 입력으로 사용한다. 대표 repo·`main`·Persistent UAT·push·PR·merge는 계속 금지한다.
