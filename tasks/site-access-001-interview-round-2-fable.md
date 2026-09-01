Baseline is verified at this HEAD: the audit store still records interactive `LOGIN_SUCCESS`/`Logout` only with interaction-ID idempotent append, and migration `0083` is the latest referenced. The user's only round-1 response was a standing instruction to re-ask the same questions in a short, natural, human conversational style without changing their order, options, or recommendations. Below is the round 2 artifact.

---

# TASK-SITE-ACCESS-001 — Deep Interview Round 2 (Fable 5)

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- interviewRound: 2
- sourceOfTruth: `tasks/site-access-001-interview.md`

Round 1에서 답변을 받지 못한 blocking 결정 1번을, 요청하신 대로 사람이 묻듯이 짧게 다시 여쭤봅니다. 질문 순서·선택지·권장안은 Round 1과 같고 의미도 바꾸지 않았습니다. 각 질문에 "1은 B" 처럼 기호만 답하셔도 되고, 모두 권장안이 괜찮으시면 "전부 권장안대로" 한 마디로 답하셔도 됩니다.

### 질문 1 — 접속 한 번을 어떻게 셀까요?

브라우저를 며칠씩 켜 두는 분도 있는데, 어디까지를 "한 번 접속"으로 볼까요?

- A. 브라우저를 닫기 전까지는 며칠이 지나도 전부 한 번으로 칩니다. (기록은 적지만 "언제 썼는지"가 안 보입니다)
- B. 한동안 안 쓰다가 다시 쓰면 그때부터 새 접속으로 칩니다. 새로고침이나 탭 여러 개는 같은 접속으로 묶습니다.
- C. 하루에 첫 사용 한 번만 남깁니다. (하루 안의 재접속은 구분이 안 됩니다)

저는 B를 권해요. "실제로 언제 사이트를 썼는지"를 보고 싶다는 목적에 가장 맞습니다.

### 질문 2 — "언제까지 썼는지"도 남길까요?

접속 시작만 남기면 "몇 시에 들어왔다"만 알 수 있어요. 마지막으로 쓴 시각도 필요하신가요?

- A. 네, 접속 건마다 마지막 활동 시각을 서버가 5분 간격 정도로만 조용히 갱신합니다. (저장량 적음, 기존 감사 기록의 append-only 원칙에 시각 필드 갱신이라는 좁은 예외 하나가 생김)
- B. 활동할 때마다 기록을 새로 쌓습니다. (원칙은 순수하게 지켜지지만 기록이 아주 많아지고 화면이 지저분해집니다)
- C. 시작 시각만 남깁니다. (가장 단순하지만 "언제까지 썼는지"는 알 수 없음)

저는 A를 권해요. 비밀정보가 아닌 시각 하나만 갱신하는 것이라 안전하고 화면도 깔끔합니다.

### 질문 3 — 로그아웃 없이 창을 닫으면 언제 "끝났다"고 볼까요?

대부분은 로그아웃 없이 창을 닫으시니까, 끝난 시점을 정하는 기준이 필요해요.

- A. 마지막 활동 후 30분이 지나면 끝난 것으로 봅니다. (일반 웹사이트들이 쓰는 방식)
- B. 같은 방식인데 기준을 2시간처럼 길게 잡습니다. (점심시간 자리 비움도 한 접속으로 묶이지만, 하루 안의 재접속 구분이 흐려짐)
- C. 창을 닫을 때 브라우저가 종료 신호를 보냅니다. (강제 종료나 인터넷 끊김에서는 신호가 유실돼 결국 A 같은 기준이 또 필요함)

저는 A(30분)를 권해요. 30분이 업무 패턴과 안 맞으면 원하시는 숫자만 알려주세요.

### 질문 4 — 어느 화면에서 보고, IP도 남길까요?

- A. 지금 쓰시는 `관리자 → 운영 → 전체 감사 이력`에 "사이트 접속" 종류를 추가하고, 로그인 기록과 똑같이 IP·브라우저·OS 계열까지 저장하고, Excel 내보내기에도 포함합니다.
- B. "사이트 접속 이력" 화면을 따로 새로 만듭니다. (메뉴·권한·Excel이 중복되고 로그인 기록과 나란히 보기 불편함)
- C. A와 같은데 IP는 저장하지 않습니다. (개인정보는 더 줄지만 로그인 기록과 대조 가치가 떨어짐)

저는 A를 권해요. 기존 화면과 권한을 그대로 재사용해서 로그인과 접속을 한 화면에서 비교할 수 있습니다.

### 질문 5 — 기록이 실패하면 사이트를 막을까요?

혹시 접속 기록 저장이 일시적으로 실패하면 어떻게 할까요?

- A. 사이트는 그대로 쓰게 두고, 실패는 서버 로그로 확인해서 고칩니다. (지금 로그인 기록과 같은 방식, 드물게 한 건 누락 가능)
- B. 기록이 안 되면 접속 자체를 막습니다. (기록은 완벽하지만 저장소 장애가 전 직원 업무 중단으로 번짐)

저는 A를 권해요. 이 기능의 목적은 차단이 아니라 "누가 언제 썼는지 보는 것"이니까요.

---

- 다음 단계: 다섯 개 답변(또는 "전부 권장안대로")이 기록되면 Fable이 확인용 요약을 작성합니다.

interviewStatus: QUESTIONS_REQUIRED
planningStatus: NOT_STARTED
implementationApproved: false
