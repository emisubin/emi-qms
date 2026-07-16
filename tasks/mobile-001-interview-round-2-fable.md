Baseline verification is complete: the shell already has a permission-filtered navigation list (내 업무·프로젝트·Pending·생산관리·구매·자재·알림·관리자) with badge counts on 내 업무/알림, an inline mobile nav grid, URL-based view routing, and per-page `useIsMobileViewport()` card variants — all consistent with round 1. All four blocking decisions (1-A·2-A·3-A·4-A) are recorded as adopted in the canonical interview with `openBlockingDecisionCount: 0`, and no new blocking gap remains, so round 2 is the confirmation summary.

# TASK-MOBILE-001 — Fable 5 Deep Interview Round 2 (확인용 요약)

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- interviewRound: 2

기준선 재확인 요약: 최신 interview 문서와 현재 코드를 다시 대조했다. shell 내비게이션 목록은 권한 조건으로 필터링되어 최대 8개 항목(내 업무·프로젝트·Pending·생산관리·구매·자재·알림·관리자)을 구성하고, 내 업무·알림 배지 count가 이미 shell 상태로 존재하며, 860px 이하에서 콘텐츠 상단 grid형 공통 모바일 내비게이션이 표시된다. view는 URL 기반으로 해석되어 동일 URL·Teams deep link 계약이 성립하고, 다수 화면이 `useIsMobileViewport()` 카드형 변형을 개별 구현한다. Round 1의 4개 blocking 결정은 사전 사용자 지시에 따라 모두 Fable 권장안(1-A·2-A·3-A·4-A)으로 채택 기록되었고, 추가 blocking 질문은 남아 있지 않다.

## Fable 확인용 요약

- 해결할 문제: 기존 URL·인증·Teams deep link와 서버 권한을 그대로 유지한 채, 390px와 Teams narrow에서 현장 사용자가 내 업무·Pending·프로젝트 핵심 업무를 빠르게 찾고 원본 업무 화면으로 한 손 흐름에 가깝게 진입하도록 공통 적응형 UX 기반을 만든다.
- 권장 범위 (사용자 채택 완료):
  - **사진 업로드 제외 (1-A)**: storage·보존·검역·backup 정책이 external blocker(Roadmap 추적 항목 73)로 미확정이므로 이번 Task에서 binary 사진 저장과 client 업로드 계약을 모두 제외하고, 정책 확정을 선행조건으로 하는 별도 NEW_FEATURE Task로 분리한다.
  - **하단 고정 tab bar + 더보기 sheet (2-A)**: 모바일에서 핵심 4~5개 항목(내 업무·Pending·프로젝트·알림 중심)을 하단 고정 tab bar로 제공하고, 나머지 메뉴는 "더보기" sheet에서 기존 권한 조건 그대로 노출한다. 현재 상단 grid 방식의 콘텐츠 밀림과 엄지 도달 문제를 해소한다.
  - **신규 요약 화면 없음 (3-A)**: 기존 내 업무·Pending·프로젝트 화면 자체를 모바일 우선으로 정비하고 내비게이션 배지로 우선순위를 전달한다. 요약 조망 화면은 TASK-HOME-001에 남긴다.
  - **완료 기준 화면 한정 (4-A)**: shell 전역 기준(내비게이션·safe-area·page-level overflow 0)과 현장 핵심 화면(내 업무, Pending 목록·상세, 프로젝트 목록·상세·병목)만 이번 Task의 390px·Teams narrow 완료 기준으로 확정하고, 관리자·설정 화면은 회귀 없음만 확인한다.
- 확정한 정책·불변조건:
  - 동일 URL·기존 인증·Teams deep link·서버 권한·18단계·Pending·병목 계약을 변경하지 않는다.
  - 모바일 shell은 새로운 mutation source가 아니며 원본 권한·상태·audit보다 넓은 정보나 count를 노출하지 않는다. 배지는 기존 shell badge 집계(내 업무 요청 count, 미읽음 알림 count)만 재사용한다.
  - 한 요약 데이터 실패가 전체 내비게이션을 막지 않으며, migration 없는 Frontend/common-contract slice로 유지한다.
  - 검증은 isolated tests와 synthetic screenshot만 사용하고 Persistent UAT·실제 provider·canonical runtime을 변경하지 않는다.
- 명시적 제외: 별도 모바일 URL·별도 인증/session, 공용 태블릿·공용 기기 mode, sessionStorage 강제 정책, 실제 Teams/Mail provider 발송, Persistent UAT migration·write·runtime handover, 정책 미확정 상태의 binary 사진 저장과 client 업로드 계약.
- Deferred 비차단 결정 (planning에서 Fable 권장안으로 구체화):
  - tab bar 핵심 항목의 최종 구성과 권한별 항목 수 축소 시 배치(권한상 항목이 적은 역할의 tab 구성 포함)
  - safe-area(bottom inset) 처리 방식과 화면 하단 기존 고정 요소와의 겹침 회피 세부
  - "더보기" sheet의 열림 방식·keyboard focus 순서 세부
  - Roadmap의 TASK-MOBILE-001 포함 범위 문구("사진 압축·재시도")와 이번 1-A 결정의 정합화는 Roadmap 갱신 항목으로 Codex에 전달
- 성공 기준:
  - 업무 결과: 390px·Teams narrow에서 동일 URL로 내 업무·Pending·프로젝트 핵심 화면에 빠르게 진입하고 이전 맥락으로 복귀할 수 있다.
  - 자동 검증: desktop·390px·Teams narrow layout, keyboard focus, 44px 이상 touch target, route·back context 유지, 대상 화면 page-level horizontal overflow 0.
  - 사용자 검수: synthetic screenshot으로 하단 내비게이션·배지·현장 핵심 화면의 가독성과 action 진입을 확인한다.
- Fable 판정: `SUMMARY_CONFIRMATION_REQUIRED`

## 사용자 확인 요청

위 요약이 정확하면 interview 문서의 사용자 확인 항목 6개를 체크하고 `interviewStatus: COMPLETED_CONFIRMED`, `userConfirmed: true`, `openBlockingDecisionCount: 0`으로 갱신한 뒤 planning 단계로 진행한다. 이 확인은 planning 승인이나 구현 승인이 아니다.

---

- interviewStatus: SUMMARY_CONFIRMATION_REQUIRED
- planningStatus: NOT_STARTED
- implementationApproved: false
