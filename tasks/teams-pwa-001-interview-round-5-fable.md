Baseline is fully refreshed against the canonical interview and current code: all Round 4 answers are recorded, the roadmap Next Gate still matches this task, and the old-name locations in frontend, Teams manifest template, and backend mail/PDF/Excel strings match what the answers cover. No blocking decision remains, so Round 5 is the confirmation summary.

---

# TASK-TEAMS-PWA-001 — Deep Interview Round 5 (Fable 5) — 확인용 요약

Round 4에서 마지막 blocking 주제였던 이름·문구 글자 계약이 모두 확정되었다: 한국어 전체 이름은 `B`(붙여쓰기), 이름 배치는 `B`(모든 이름 칸 `EMI PMS` 단일화), 설명 문구는 `A`(권장 문안, 붙여쓰기 반영), 교체 범위는 `A`(메일·PDF·Excel까지 이번 Task에서 전부). Repository를 다시 대조한 결과 이 답변들과 충돌하는 변경은 없으며, 남은 blocking 질문이 없다. 아래 요약을 확인해 달라. 이 요약이 planning의 입력이 된다.

## 1. 해결할 업무 문제와 기대 결과

- 문제: Teams 앱에서 화면이 안 열리거나 로그인 버튼이 작동하지 않아 Teams는 알림 확인용으로만 쓰이고, Teams(`PMS`)·설치 아이콘(`EMI QMS`)·열린 화면(`EMI 프로젝트 통합관리시스템`)의 이름이 서로 달라 하나의 제품으로 보이지 않는다.
- 기대 결과: Teams 안에서는 Teams에 로그인된 조직 계정으로 자동 진입(Teams SSO)하고, 웹에서는 `EMI PMS`를 설치형 앱(PWA)으로 설치·실행하며, 모든 사용자 표면이 같은 이름·아이콘·권한으로 이어진다.

## 2. 확정된 결정 전체 (Round 2~4)

1. **Teams SSO 실패 복구 — 단계적 fallback**: silent SSO → Teams 안 대화형 인증(팝업) → "브라우저에서 열기" 안내 순서. 최종 fallback으로 기존 웹 로그인 경로를 항상 보존한다.
2. **Teams 안 계정 — Teams 조직 계정 고정**: Teams tab에서는 계정 선택·로그아웃 UI를 숨기고 Teams 로그인 계정만 사용한다. 웹 표면의 기존 세션 기억·계정 선택 동작은 바꾸지 않는다.
3. **이름 글자 계약**:
   - 모든 사용자-facing 이름 칸(Teams 짧은 이름·전체 이름·탭 이름, web manifest name·short_name, 브라우저 제목·홈 화면 이름, 로그인·상단 화면 제목)은 `EMI PMS`로 단일화한다.
   - 한국어 전체 이름 `EMI 프로젝트 통합관리시스템`(붙여쓰기)과 영문 의미 `EMI Project Management System`은 설명 문구에서 사용한다.
   - Teams developer name(만든 곳 이름)은 `EMI`.
   - 내부 개발용 이름(`Emi.Qms` solution·namespace)은 변경하지 않으며 사용자 표면에 노출하지 않는다.
4. **설명 문구 계약**:
   - 짧은 설명: `EMI 프로젝트 업무와 알림을 한 곳에서 확인합니다.`
   - 전체 설명·웹 설치 설명: `프로젝트 생성부터 생산관리, 구매, 제조, 품질, 물류와 정산까지 연결하는 EMI 프로젝트 통합관리시스템(EMI PMS)입니다.`
5. **이름 교체 범위 — 전 표면 일괄**: Teams manifest·웹 설치 설명서·브라우저 제목·앱 화면에 더해, 알림 메일 발신자 표시명·본문 머리글, Teams 개인 알림 대체 제목, IQC·품질검사 PDF 문서 정보, 휴일 일괄 등록 Excel 머리글의 옛 이름(`EMI QMS`, `프로젝트 통합관리시스템` 등)을 이번 Task에서 모두 새 체계로 교체한다. 알림 event·수신자·발송 정책은 변경하지 않고, 메일 발신자 표시명 같은 운영 설정값의 실제 반영은 별도 승인 rollout 경계를 유지한다.
6. **PWA 범위 — 설치 경험까지**: Service Worker·오프라인 cache 없이 이름·아이콘 정비와 설치 안내 UX를 제공한다. Android·PC는 브라우저 설치 신호(`beforeinstallprompt` — 브라우저가 "설치 안내를 띄워도 된다"고 알려주는 신호) 기반 간단한 설치 동작, iPhone은 홈 화면 수동 추가 안내를 제공한다. 아이콘은 흰 바탕의 빨간 EMI 로고로 통일한다.
7. **모바일 Web Push — 별도 후속 `NEW_FEATURE`로 분리**: 이번 Task에는 포함하지 않는다. 이번 Task의 iPhone 홈 화면 추가 안내는 후속 푸시의 전제 조건이 되도록 설계한다.
8. **필수 검증 기준**: Android·iPhone 설치형 웹앱의 설치·실행·진입은 통과 필수, Teams 모바일 앱은 확인 항목. PC 부서용 Teams 데스크톱·웹과 PC PWA 설치는 통과 필수. 제조·품질 부서의 모바일 우선 사용 비중을 반영한 기준이다.

## 3. 대상 사용자와 권한

- 일반 사용자: Teams 또는 설치형 PWA에서 로그인해 기존 앱 역할·프로젝트 접근 범위의 업무만 수행한다.
- 승인 대기 사용자: 인증 뒤 기존 승인 대기 안내를 그대로 본다.
- System Administrator: 기존 관리 기능 범위를 유지하며, 실제 Teams/Entra 배포는 별도 승인이다.
- 불변조건: Backend JWT Bearer·앱 내부 역할 검증이 authoritative하고, Entra 조건부 액세스·MFA를 우회하지 않으며, token 원문을 저장하지 않는다.

## 4. 정상·예외·복구 흐름

- 정상: Teams context 감지 → silent SSO → 앱 권한 확인 → 업무 화면. 웹·설치형 앱은 기존 MSAL 로그인 유지.
- 예외: 최초 동의·MFA·조건부 액세스 실패 시 Teams 안 팝업 인증, 그래도 실패하면 외부 브라우저 안내. 상태별(초기화·SSO 시도·권한 확인·실패 안내) 화면을 분리한다.
- 복구·rollback: Teams catalog/Entra 설정과 웹 배포가 어긋나면 기존 웹 로그인·Teams Activity 알림 전용 운영으로 되돌린다. rollout은 웹 반영·검증 → Teams catalog/Entra 반영(별도 승인) 순서다.

## 5. 포함·제외 범위

- 포함: Teams tab SSO와 단계적 fallback, 새 Teams manifest 계약(이름·설명·developer name·탭 이름), 웹 PWA 설치 경험과 안내 UX, `EMI PMS` 이름·문구 전 표면 교체(메일·PDF·Excel 포함), 흰 바탕 빨간 EMI 로고 아이콘, 기존 Activity deep link·웹 인증·앱 역할 연계, UAT·rollout·rollback 계획.
- 제외: Web Push(후속 `NEW_FEATURE`), 알림 수신자·발송 정책 재설계, 신규 Teams Bot·DM·채널, `Emi.Qms` 내부 rename, 실제 Entra·Teams Admin Center·Azure 운영 변경, 구현·DB migration·운영 배포.

## 6. 후속 Task로 넘긴 비차단 결정 (planning에 전달)

- 푸시 발송 대상 알림 유형·기본값, 사용자별 푸시 설정과 해제 불가 범위, 권한 요청 시점·거절 복구·기기별 구독 lifecycle, 푸시 클릭 deep link, 푸시 수신 전용 최소 Service Worker·구독 저장소·migration·네 번째 delivery channel.

## 7. 성공 기준

- Teams와 설치형 웹 어디서든 같은 `EMI PMS`로 진입하고 권한에 맞는 업무를 이어간다.
- 옛 이름이 사용자 표면(화면·설치·메일·PDF·Excel)에 남지 않는다.
- 기존 Backend authorization·승인 대기·조건부 액세스·알림 event identity가 보존된다.
- 자동 검증·사용자 검수 세부 목록은 planning에서 확정하되, 2절 8항의 필수 검증 기준을 차단 기준으로 사용한다.

## 8. 사용자 확인 요청

아래 항목이 모두 정확하면 확인해 달라. 확인되면 Codex가 interview를 `COMPLETED_CONFIRMED`, `userConfirmed: true`, `openBlockingDecisionCount: 0`으로 기록하고, 그 뒤에 planning 초안을 작성한다. 이 확인은 planning 승인이나 구현 승인이 아니다.

- [ ] 업무 문제와 기대 결과가 정확하다.
- [ ] 대상 역할과 권한이 정확하다.
- [ ] 정상·예외·복구 흐름이 정확하다.
- [ ] 포함·제외 범위가 정확하다.
- [ ] Blocking 결정이 남아 있지 않다.
- [ ] 이 요약을 planning 입력으로 사용하는 데 동의한다.

수정할 부분이 있으면 해당 절 번호와 함께 알려 달라. 그 부분만 반영해 다시 확인을 요청한다.

---

- interviewStatus: SUMMARY_CONFIRMATION_REQUIRED
- planningStatus: NOT_STARTED
- implementationApproved: false
