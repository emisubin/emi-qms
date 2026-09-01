All baseline checks are done. Every decision from rounds 1–3 is recorded, but the user's round-3 choice of **3 B** (store big-menu names) — which went against the recommendation — was never reconciled with **1 B** (one bundled session row where only the last-activity time updates). How multiple menu names live inside a single session row, what to record for screens outside the big menus, and how far the menu field surfaces in list/Excel are genuinely schema- and UX-blocking, so round 4 asks those three closely related questions rather than a confirmation summary.

---

Baseline is verified at this HEAD: the interview file records the round-3 answers `1 B / 2 A / 3 B / 4 A / 5 A`, the audit contracts confirm `Login`/`Logout` event types with `ClientIp`·`BrowserFamily`·`OsFamily`·`AppAccessOutcome` and idempotent receipts, the frontend top-level navigation labels include `프로젝트`·`자재`·`품질`·`영업`·`관리자`, roadmap tracking item 97 confirms the approved override, and migration `0083` is the latest referenced. Decision 3-B (menu-name level recording) was chosen against the round-3 recommendation and its storage shape inside the single bundled session row of decision 1-B was never asked, so a confirmation summary would require inventing that answer. Below is the round-4 artifact.

---

# TASK-SITE-ACCESS-001 — Deep Interview Round 4 (Fable 5)

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- interviewRound: 4
- sourceOfTruth: `tasks/site-access-001-interview.md`

다섯 개 결정 감사합니다. 접속을 한 건으로 묶고(1-B), 30분 조용하면 새 접속(2-A), 큰 메뉴 이름 수준 기록(3-B), 기존 전체 감사 이력 화면과 IP·브라우저·OS·Excel(4-A), 기록 실패는 best-effort(5-A)까지 확정됐습니다. 그런데 3번에서 메뉴 이름을 남기기로 하셨는데, 접속을 한 줄로 묶기로 한 1번과 만나면 "한 접속에서 여러 메뉴를 쓴 경우"를 어떻게 담을지가 아직 정해지지 않았습니다. 이 마지막 묶음 세 개만 여쭤보면 확인용 요약으로 넘어갈 수 있습니다. "1은 A"처럼 기호만 답하셔도 되고, 모두 권장안이 괜찮으시면 "전부 권장안대로" 한 마디면 됩니다.

### 질문 1 — 한 번의 접속에서 여러 메뉴를 쓰면 어떻게 남길까요?

예를 들어 아침에 들어와서 `프로젝트`를 보다가 `품질`로 넘어간 경우입니다.

- 필요한 이유: 접속 1건 = 감사 한 줄(1-B)로 확정하셨기 때문에, 메뉴 이름(3-B)을 그 한 줄에 어떻게 담을지에 따라 저장 구조·갱신 예외 범위·화면 의미가 정해집니다.
- 답변이 바꾸는 범위: 접속 세션 schema, append-only 예외의 폭(시각만 vs 시각+메뉴 목록), 관리자 화면·Excel의 줄 수와 표시 내용.
- 선택지:
  - A. 접속 한 줄에 방문한 큰 메뉴 이름을 모아서 담습니다(예: `프로젝트, 품질`). 이미 있는 메뉴는 다시 추가하지 않습니다. 장점: "한 접속 = 한 줄" 약속이 그대로 지켜지고 화면이 깔끔합니다. 단점: 갱신 허용 필드가 "마지막 활동 시각" 하나에서 "시각 + 방문 메뉴 목록" 둘로 넓어집니다(둘 다 비밀정보 아님).
  - B. 메뉴에 처음 들어갈 때마다 별도 줄을 하나씩 남깁니다. 장점: 순수 append-only이고 메뉴별 진입 시각도 남습니다. 단점: 한 접속이 여러 줄로 쪼개져 "한 줄로 보고 싶다"는 1-B 취지가 약해지고 화면 줄 수가 늘어납니다.
  - C. 접속 한 줄에 마지막(또는 첫) 메뉴 하나만 남깁니다. 장점: 가장 단순. 단점: 그 접속에서 어떤 영역들을 썼는지는 알 수 없어 3-B를 고르신 의미가 줄어듭니다.
- 권장안: A. "한 접속 = 한 줄"과 "어느 영역을 썼는지 보인다"를 둘 다 지키는 유일한 조합이고, 갱신 예외는 여전히 비밀정보 없는 두 필드로 한정됩니다.

### 질문 2 — 큰 메뉴에 안 속하는 화면은 뭐라고 남길까요?

홈 화면이나 개인 알림 설정처럼 `프로젝트`·`품질` 같은 큰 메뉴 밖에 있는 화면도 있습니다.

- 필요한 이유: 새로고침·페이지 이동 신호는 모든 화면에서 오는데, 큰 메뉴 밖 화면의 기록 규칙이 없으면 구현할 때 빈칸 처리 기준이 정해지지 않습니다.
- 답변이 바꾸는 범위: 메뉴 필드의 값 정의, 상세·Excel 표시, 개인정보 최소화 문구.
- 선택지:
  - A. 그런 화면도 `홈`처럼 화면 상단에 실제로 보이는 이름 수준으로 남깁니다. 장점: 접속마다 방문 영역이 빠짐없이 보입니다. 단점: 메뉴 이름 목록에 항목이 몇 개 더 생깁니다.
  - B. 큰 메뉴 밖 화면은 메뉴 기록 없이 접속 시각만 갱신합니다. 장점: 값 목록이 가장 짧습니다. 단점: 홈만 쓰고 나간 접속은 메뉴 칸이 비어 보입니다.
- 권장안: A. 어차피 메뉴 이름 수준(3-B)으로 확정하셨으니 `홈` 같은 표시 이름 몇 개가 추가되는 것이 일관되고, 빈칸의 의미를 따로 설명할 필요도 없습니다.

### 질문 3 — 방문 메뉴는 어디까지 보여줄까요?

- 필요한 이유: 4-A로 기존 `전체 감사 이력` 화면과 선택 Excel을 쓰기로 하셨는데, 방문 메뉴 칸을 목록·상세·Excel 어디에 넣을지에 따라 화면 폭(특히 좁은 모바일 화면)과 Excel 열 구성이 정해집니다.
- 답변이 바꾸는 범위: 목록 행·상세 화면·선택 Excel의 열 구성, 390px 모바일 카드 표시.
- 선택지:
  - A. 목록 행·상세·선택 Excel 모두에 "방문 메뉴" 한 칸으로 보여줍니다. 장점: 화면에서 바로 보이고 Excel 보존도 완전합니다. 단점: 목록 행이 조금 길어집니다(모바일 카드는 기존 방식대로 줄여서 표시).
  - B. 상세 화면에서만 보여주고 목록·Excel에는 넣지 않습니다. 장점: 목록이 가장 깔끔합니다. 단점: Excel로 보존하면 방문 메뉴가 빠지고, 한 건씩 열어봐야 합니다.
- 권장안: A. "필요한 범위를 Excel로 보존한다"는 이 기능의 성공 기준과 맞고, 기존 감사 화면의 열 추가 방식을 그대로 재사용할 수 있습니다.

---

- 다음 단계: 세 개 답변(또는 "전부 권장안대로")이 기록되면 Fable이 확인용 요약을 작성합니다. 남은 blocking 결정은 이 묶음이 마지막입니다.

interviewStatus: QUESTIONS_REQUIRED
planningStatus: NOT_STARTED
implementationApproved: false
