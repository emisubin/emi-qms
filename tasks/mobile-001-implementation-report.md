# TASK-MOBILE-001 — 동일 URL 적응형 현장 UX 실험 구현 보고서

> 상태: 실험 구현·자동 검증·독립 read-only 검토 완료 / 사용자 검수 대기
> 기준 branch: `experiment/task-mobile-001-adaptive-field-ux`
> 실험 branch base SHA: `e177369e28e45225d9257bf5bc6c5e3e7cb7bc6d`
> canonical `main` SHA: `b8f3e2104074d05c2e71999c08a7374e8729f68f`
> canonical 반영: 미승인 — 대표 repo, GitHub `main`, Persistent UAT와 5174 runtime을 변경하지 않음

## 1. 목적과 범위

기존 URL과 권한 계약을 유지하면서 860px 이하 현장 화면에서 내 업무·프로젝트·Pending·알림을 하단 고정 tab으로 빠르게 오가고, 나머지 허용 메뉴는 접근 가능한 더보기 sheet에서 여는 적응형 UX를 구현했다.

### 포함

- 기존 `navigationItems`에서 파생한 권한 기반 모바일 tab과 더보기
- 현재 위치, 기존 배지와 screen reader용 건수 안내
- safe-area, 콘텐츠 하단 예약과 44px 이상 touch target
- 더보기 첫 항목 focus, focus containment, Esc·배경 닫기와 trigger focus 복귀
- 내 업무, 프로젝트 목록·상세, Pending 목록·상세, 알림, 480px narrow 검증과 screenshot
- Frontend-only 구현과 격리 full-stack E2E

### 제외

- 별도 모바일 URL·인증·session·API·Backend·migration
- 신규 Home형 요약 화면과 QR landing
- 관리자·설정 화면의 전면 모바일 재설계
- 사진 촬영·압축·offline queue·업로드 재시도
- 대표 repo·GitHub `main`·Persistent UAT·provider·canonical runtime 변경

## 2. 기획·Review 결정

- Fable 5 read-only runner가 2회 interview 원문과 planning 전문을 작성했다.
- 실험 branch 지침에 따라 Fable 권장안 `1-A · 2-A · 3-A · 4-A`와 planning의 비차단 권장안 4개를 자동 채택했다.
- 핵심 tab은 `내 업무`, `프로젝트`, 권한 보유 시 `Pending`, `알림`이며 나머지는 더보기에 둔다.
- tab과 더보기 모두 기존 권한 필터 결과를 재사용해 별도 권한 목록을 만들지 않는다.
- 사진 기능은 storage·검역·보존·backup·restore 정책을 선점하지 않도록 별도 `NEW_FEATURE`로 보류했다.
- 요청한 `GPT 5.6 Sol` model selector는 현재 실행 환경에 노출되지 않아 해당 모델을 사용했다고 주장하지 않는다. Codex 내용 review 뒤 별도 `sol_review` Codex sub-agent가 구현 diff를 read-only로 검토했고, touch target P2를 발견·해소한 뒤 PASS했다.

## 3. 구현 구조

### Frontend shell

- `AppMobileNavigation`이 기존 `navigationItems`를 핵심 tab과 더보기 항목으로 presentation 분할한다.
- 활성 core route는 해당 tab, 더보기 소속 route는 더보기 trigger에 `aria-current="page"`를 표시한다.
- 더보기는 `role="dialog"`, `aria-modal`, focus containment, 첫 항목 focus, Esc·backdrop close, trigger focus 복귀와 body scroll lock을 제공한다.
- 모바일 배지는 화면 숫자와 별도의 visually hidden 건수 텍스트를 제공한다.

### Responsive·touch

- `viewport-fit=cover`와 `env(safe-area-inset-*)`를 사용하고 하단 bar 실제 영역만큼 shell 여백을 예약한다.
- 860px 이하 button·일반 input·select와 Pending 상세 뒤로가기 action을 최소 44px로 보정한다.
- 390px·480px에서 핵심 route의 page-level horizontal overflow가 0인지 E2E에서 확인한다.

### Backend·DB

- N/A — API, domain, schema와 migration을 변경하지 않았다.
- E2E는 전용 PostgreSQL tmpfs를 생성하고 종료 시 database·container·network를 제거했다.

## 4. 실제 변경 파일

| 경로 | 역할 |
| --- | --- |
| `frontend/index.html` | safe-area를 위한 `viewport-fit=cover` |
| `frontend/src/App.tsx` | 권한 파생 하단 tab·더보기 sheet·focus/scroll 동작 |
| `frontend/src/styles.css` | login 기반 red·white 모바일 nav, safe-area, 44px touch target |
| `frontend/tests/App.test.tsx` | 권한별 tab, active state, 더보기 focus·Esc·복귀 unit test |
| `frontend/e2e/full-stack/mobile-adaptive-navigation.full-stack.spec.ts` | 6개 핵심 route·480px narrow·overflow·touch target·screenshot E2E |
| `tasks/mobile-001-interview*.md` | Fable 질문·요약 원문과 사용자 권장안 자동 채택 기록 |
| `tasks/mobile-001-planning.md` | Fable 5 primary planning |
| `tasks/mobile-001-review.md` | Codex 내용 review와 Finding resolution |
| `tasks/mobile-001-change-001.md` | 실험 구현·local commit과 canonical 차단 경계 |
| `tasks/mobile-001-screenshots/*.png` | synthetic 화면 증빙 8장 |

## 5. 실행한 검증과 결과

| 검증 | 결과 | 비고 |
| --- | --- | --- |
| Frontend typecheck | PASS | TypeScript 오류 0 |
| Frontend lint | PASS | 오류 0, 기존 `main.tsx` Fast Refresh 경고 1 |
| Frontend 전체 unit test | PASS | 68/68 |
| Frontend build | PASS | build 성공, 기존 500 kB chunk 경고 유지 |
| TASK-MOBILE-001 full-stack E2E | PASS | 1/1, 6개 핵심 route+480px, touch target·overflow·focus·screenshot |
| TASK-007A Pending full-stack 회귀 | PASS | 2/2, 격리 DB 정리 완료 |
| TASK-007B bottleneck full-stack 회귀 | PASS | 1/1, 격리 DB 정리 완료 |
| `git diff --check` | PASS | whitespace 오류 없음 |
| 독립 read-only diff review | PASS | P2 1건 발견 후 재검토 RESOLVED, open P0/P1/P2 0 |
| Persistent UAT·provider | 미실행 | 실험 범위 밖이며 canonical 환경 보존 |
| 사용자 직접 검수 | 대기 | 본 screenshot과 checklist로 판정 예정 |

## 6. Privacy·Secret 검토

- E2E와 screenshot은 고정 개발 역할과 synthetic 프로젝트·Pending만 사용했다.
- 실제 사용자·고객·tenant/client/object ID·credential·token·provider payload를 tracked 산출물에 기록하지 않았다.
- `.env*`, 인증서, dependency와 Persistent UAT data를 변경하지 않았다.

## 7. Finding

| ID | Severity | 상태 | 원인·영향 | 해소·후속 위치 |
| --- | --- | --- | --- | --- |
| `MOBILE-SHEET-FOCUS` | P2 | `RESOLVED` | modal 선언과 실제 keyboard 동작 불일치 위험 | focus containment·첫 항목 focus·Esc·복귀 구현 및 unit/E2E 검증 |
| `MOBILE-SAFE-AREA` | P2 | `RESOLVED` | fixed bar가 iOS/Teams 하단이나 마지막 action을 가릴 수 있음 | viewport-fit, safe-area inset과 content reservation 적용 |
| `MOBILE-PERMISSION-DRIFT` | P2 | `RESOLVED` | core/더보기 권한 조건 중복 시 drift 위험 | 기존 `navigationItems` 한 배열에서 presentation만 분할 |
| `MOBILE-CORE-TOUCH-TARGET` | P2 | `RESOLVED` | 첫 구현이 nav만 44px로 보장해 핵심 화면의 42px action을 놓침 | ≤860px action 보정과 6개 route bounding-box E2E 추가 |
| `MOBILE-BADGE-A11Y` | P3 | `RESOLVED` | 시각 배지 건수가 screen reader에 전달되지 않음 | visually hidden 건수 텍스트 추가 |
| `MOBILE-PHOTO-BLOCKER` | P2 | `RESOLVED` | 미확정 storage 정책을 upload UI가 선점할 위험 | 사진 기능 전체를 별도 NEW_FEATURE로 분리 |
| `MOBILE-REVIEWER-AVAILABILITY` | P3 | `BACKLOG` | `GPT 5.6 Sol` 선택 기능이 현재 환경에 없어 동일 모델 검증을 증명할 수 없음 | Codex 내용 review+별도 read-only sub-agent 검증 완료; canonical 채택 전 요구 모델이 제공되면 재검토 |

Open P0/P1/P2는 `0/0/0`이다. P3 backlog는 실험 local commit을 막지 않지만 canonical 게시 전 재평가한다.

## 8. Rollback·복구

- 대표 repo와 `main`은 이미 보존돼 있으므로 실험을 채택하지 않으면 이 branch commit만 보관하거나 승인 범위에서 worktree를 정리한다.
- Backend·migration·Persistent data·provider mutation이 없어 운영 rollback은 없다.
- 모바일 nav 회귀 시 이 실험 commit을 대표 repo에 적용하지 않는 것이 기본 rollback이다.

## 9. SOP

1. branch가 `experiment/task-mobile-001-adaptive-field-ux`인지 확인한다.
2. Frontend typecheck·lint·unit·build를 실행한다.
3. `bash scripts/e2e-full-stack.sh frontend/e2e/full-stack/mobile-adaptive-navigation.full-stack.spec.ts`를 실행한다.
4. E2E 종료 시 전용 DB·container·network 제거를 확인한다.
5. 390px에서 내 업무·프로젝트·Pending·알림·더보기를, 480px에서 프로젝트 목록을 synthetic data로 검수한다.
6. 대표 repo·`main`·5174·Persistent UAT에는 적용하지 않는다.

## 10. User manual

1. 좁은 화면 하단에서 `내 업무`, `프로젝트`, `Pending`, `알림`을 선택한다.
2. 현재 메뉴는 빨간 배경과 하단 표시선으로 구분된다.
3. 숫자 배지는 내 업무 요청 건수 또는 읽지 않은 알림 수를 뜻한다.
4. `더보기`를 누르면 현재 역할에서 허용된 나머지 메뉴가 열린다.
5. 더보기는 항목 선택, `×`, 바깥 영역, `Esc`로 닫을 수 있다.
6. Desktop 861px 이상에서는 기존 sidebar를 그대로 사용한다.

## 11. User validation checklist

상태: `자동 검증 완료` / `사용자 검수 대기`

### 자동 확인

- [x] 권한 보유 actor에게 Pending tab이 보이고 미보유 actor에게는 숨겨짐
- [x] core route와 더보기 route의 현재 위치가 표시됨
- [x] 더보기 첫 항목 focus, focus containment, Esc와 trigger focus 복귀
- [x] 390px 6개 핵심 route와 480px narrow에서 horizontal overflow 0
- [x] 핵심 action과 하단 nav button이 최소 44×44px
- [x] Frontend 68/68·TASK-MOBILE E2E 1/1·007A 2/2·007B 1/1

### 사용자 직접 확인

- [ ] 핵심 tab 4개와 더보기 분리가 실제 현장 우선순위에 맞다.
- [ ] 390px에서 하단 bar가 마지막 action을 가리지 않는다.
- [ ] red·white surface와 카드 밀도가 로그인 화면의 느낌과 자연스럽게 이어진다.
- [ ] 더보기 sheet의 메뉴 순서와 닫기 동작이 이해하기 쉽다.
- [ ] 이 실험을 계속 수정할지 대표 repo 채택 후보로 둘지 결정한다.

## 12. 화면 증빙

- [내 업무 · 390px](mobile-001-screenshots/01-my-work-mobile-390.png)
- [프로젝트 목록 · 390px](mobile-001-screenshots/02-project-list-mobile-390.png)
- [프로젝트 상세 · 390px](mobile-001-screenshots/03-project-detail-mobile-390.png)
- [Pending 목록 · 390px](mobile-001-screenshots/04-pending-list-mobile-390.png)
- [Pending 상세 · 390px](mobile-001-screenshots/05-pending-detail-mobile-390.png)
- [알림 · 390px](mobile-001-screenshots/06-notifications-mobile-390.png)
- [더보기 sheet · 390px](mobile-001-screenshots/07-more-sheet-mobile-390.png)
- [프로젝트 목록 · 480px narrow](mobile-001-screenshots/08-project-list-narrow-480.png)

## 13. 5종 종료 산출물 추적

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | `tasks/mobile-001-implementation-report.md` |
| SOP | 작성됨 | 이 문서 `9. SOP` |
| User manual | 작성됨 | 이 문서 `10. User manual` |
| Roadmap update | N/A — 실험 전용 | canonical Roadmap과 대표 repo를 보존하라는 사용자 지침에 따라 미수정 |
| User validation checklist | 자동 검증 완료·사용자 검수 대기 | 이 문서 `11. User validation checklist` |

## 14. 시행착오와 해소

- in-app browser용 장시간 격리 runtime 시작은 현재 실행 정책에서 추가 승인을 요구했으나 승인 요청이 금지된 환경이어서, 저장소가 제공하는 격리 Playwright full-stack runner로 동일 browser 검수를 수행했다.
- 첫 screenshot E2E는 dialog accessible name을 `더보기 메뉴`로 기대했지만 실제 label은 `더 많은 업무 메뉴`여서 selector를 실제 접근성 계약에 맞췄다.
- 독립 review가 하단 nav 외 핵심 화면 action의 42px 잔존을 발견해 44px로 보정하고 route별 실측 E2E를 추가했다.
- screenshot 캡처 전에 font ready·2 frame·scroll top을 고정해 route 전환 직후 visual artifact와 이전 scroll 위치를 제거했다.
- 모든 full-stack run은 성공·실패와 무관하게 전용 database·container·network를 정리했다.

## 15. 사용자 검수 결과와 Roadmap

자동 검증, 독립 read-only 검토와 synthetic 화면 캡처는 완료했으나 사용자 직접 검수는 아직 대기다. 대표 repo와 canonical Roadmap은 변경하지 않았으므로 공식 다음 Gate는 계속 `TASK-007A Fable deep-interview → planning → Codex review → 사용자 승인`이다. 실험 Roadmap상 다음 후보는 `TASK-HOME-001`이지만, 이 실험 결과와 local commit은 canonical 승인이나 merge를 대신하지 않는다.
