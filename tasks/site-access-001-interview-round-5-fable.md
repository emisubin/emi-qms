All baseline checks pass and every blocking decision from rounds 1–4 is now answered and mutually consistent, so round 5 is the confirmation summary rather than more questions. Here is the round-5 artifact:

---

Baseline is verified at this HEAD: the interview file records round-3 answers `1 B / 2 A / 3 B / 4 A / 5 A` and round-4 answers `1 A / 2 A / 3 A`, roadmap entry 3.3M and tracking item 97 confirm `Deep Interview In Progress` with the explicitly approved sequence override, migration `0083` remains the latest in the migrations directory, and the audit ledger contract (`Login`/`Logout` event types with `client_ip`·`browser_family`·`os_family`, idempotent conflict-based appends, `Audit.Read.All` admin query) matches every fact used below. The round-4 reconciliation of menu-name recording inside a single bundled session row was the last blocking bundle and all three answers are recorded, so no blocking question remains. Below is the round-5 confirmation summary.

---

# TASK-SITE-ACCESS-001 — Deep Interview Round 5 확인용 요약 (Fable 5)

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- interviewRound: 5
- sourceOfTruth: `tasks/site-access-001-interview.md`

네 개 round의 답변이 모두 기록되어 더 여쭤볼 blocking 결정이 없습니다. 아래 요약이 맞는지 확인해 주시면 이 내용을 그대로 기획(planning) 입력으로 사용합니다. 고칠 부분이 있으면 해당 항목만 짚어 주시면 됩니다.

## 해결할 문제

- 지금은 새 Microsoft 대화형 로그인과 데이터 변경만 감사 화면에 남고, 로그인 상태가 유지된 채 PMS에 들어온 접속은 관리자가 확인할 수 없습니다.
- 이 기능이 끝나면 관리자는 로그인 방식과 무관하게 "누가 언제부터 언제까지 PMS를 썼고 어느 영역을 봤는지"를 전체 감사 이력에서 조회하고 필요한 범위를 Excel로 보존할 수 있습니다.

## 확정한 결정

1. **기록 시점**: 새로고침하거나 다른 페이지에 들어간 시점을 접속 신호로 삼습니다.
2. **접속 묶음 (3-1 B)**: 진행 중인 접속이 없을 때만 "접속 시작" 한 줄을 만들고, 이후 새로고침·페이지 이동·다중 탭·자동 token 갱신은 같은 행의 마지막 활동 시각만 갱신합니다.
3. **만료 기준 (3-2 A)**: 명시적 로그아웃은 종료로 확정하고, 그 외에는 마지막 활동 뒤 30분 조용하면 접속이 끝난 것으로 봅니다. 그 뒤 사용은 새 접속 한 줄입니다.
4. **방문 영역 기록 (3-3 B + 4-1 A + 4-2 A)**: 전체 주소나 업무 식별자는 저장하지 않고, 한 접속 행에 방문한 큰 메뉴·화면의 표시 이름(`프로젝트`, `품질`, `홈`, `알림` 등)을 중복 없이 모아서 담습니다. append-only의 갱신 예외는 "마지막 활동 시각"과 "방문 메뉴 목록" 두 필드로만 한정하며 둘 다 비밀정보가 아닙니다.
5. **보는 화면 (3-4 A + 4-3 A)**: 기존 `관리자 → 운영 → 전체 감사 이력`에 사건 종류 `사이트 접속`을 추가하고, 로그인 기록과 동일하게 IP·브라우저·OS 계열을 저장하며, 방문 메뉴를 목록·상세·선택 Excel 모두에 표시합니다. 좁은 모바일 화면의 카드는 기존 방식대로 줄여서 배치합니다.
6. **실패 처리 (3-5 A)**: 접속 기록이 실패해도 PMS 사용은 막지 않는 best-effort로 두고, 실패는 서버 오류 로그로 관찰해 forward-fix합니다.

## 권한과 불변조건

- 조회는 기존 `Audit.Read.All` 권한의 System Administrator 전용이며 접속 원장 수정·삭제 기능은 없습니다.
- 서버가 인증된 요청의 실제 사용자 기준으로 접속을 확정하고, 다른 사용자의 접속을 만들거나 조작하는 요청은 거절합니다.
- 기존 대화형 `Login`·명시적 `Logout` 사건의 의미와 기존 업무 권한은 바뀌지 않습니다.
- 비밀번호·token·Authorization header·cookie·raw request/response·화면 내용은 저장하지 않습니다.

## 데이터·migration·rollout

- additive migration만 사용하고 과거 접속을 추정·소급 생성하지 않습니다. migration 번호·schema는 planning 승인 뒤 최신 main(현재 최신은 `0083`)에서 확정합니다.
- 만료는 클라이언트 종료 신호에 의존하지 않고 서버가 조회 시 계산하거나 다음 접속 때 확정합니다.
- 검증은 isolated 자동 검증(권한·동시 탭·새로고침·30분 timeout·Excel·desktop/390px) 후 운영에서 접속 1건과 aggregate 증가를 privacy-safe 근거로 분리 검수합니다. Persistent UAT·Azure 배포는 별도 승인 Gate입니다.
- application rollback 시에도 신규 접속 원장은 삭제하지 않고 forward-fix합니다.

## 명시적 제외

- Entra 자체 sign-in log 연동, 페이지별 클릭·키 입력·모든 HTTP 요청 수집, 전체 주소·업무 식별자 저장, 과거 접속 소급 생성, 기존 업무 권한 확대와 신규 알림 채널.

## Deferred 비차단 결정

- 없음.

## 확인 요청

아래가 모두 맞으면 "확인" 한 마디로 답해 주세요. 확인되면 interview를 `COMPLETED_CONFIRMED`로 바꾸고 planning 작성으로 넘어갑니다.

- [ ] 업무 문제와 기대 결과가 정확하다.
- [ ] 대상 역할과 권한이 정확하다.
- [ ] 접속 묶음·30분 만료·방문 메뉴 기록·표시·실패 처리 결정이 정확하다.
- [ ] 포함·제외 범위가 정확하다.
- [ ] Blocking 결정이 남아 있지 않다.
- [ ] 이 요약을 planning 입력으로 사용하는 데 동의한다.

---

- 다음 단계: 사용자가 이 요약을 확인하면 Codex가 interview 문서를 `COMPLETED_CONFIRMED`, `userConfirmed: true`, `openBlockingDecisionCount: 0`으로 기록한 뒤 Fable planning을 시작합니다.

interviewStatus: SUMMARY_CONFIRMATION_REQUIRED
planningStatus: NOT_STARTED
implementationApproved: false
