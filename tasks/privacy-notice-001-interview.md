# TASK-PRIVACY-NOTICE-001 — 사내 개인정보·이용 안내 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- orchestrationOwner: `CODEX`
- interviewRound: 4
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 Fable 5가 사용자와 진행하는 deep-interview를 round별로 고정한다. Codex는 Fable 질문과 사용자 답변을 전달·기록하지만 업무 질문을 대신 만들거나 답하지 않는다. Interview 완료는 planning 또는 구현 승인이 아니다.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `QUESTIONS_REQUIRED` | 0 | 대기 | Fable 질문 생성 |
| 1 | `QUESTIONS_REQUIRED` | 5 | `1C · 2A · 3A · 4B · 5A` | Fable 후속 round |
| 2 | `QUESTIONS_REQUIRED` | 4 | `1A · 2A · 3A · 4B` | Fable 후속 round |
| 3 | `QUESTIONS_REQUIRED` | 3 | 연락처 배포 주입 전제 철회·정적 문안에서 자유롭게 관리 | Fable 확인용 요약 |
| 4 | `COMPLETED_CONFIRMED` | 0 | `요약 확인` | Fable planning |

- Fable 원문: [Round 1](privacy-notice-001-interview-round-1-fable.md)
- Fable 원문: [Round 2](privacy-notice-001-interview-round-2-fable.md)
- Fable 원문: [Round 3](privacy-notice-001-interview-round-3-fable.md)
- Fable 원문: [Round 4 확인용 요약](privacy-notice-001-interview-round-4-fable.md)
- 사용자 답변(2026-08-11, Round 1): `4번은 B로 하고, 나머지는 모두 Fable 권장안으로 채택한다.`
- 사용자 답변(2026-08-11, Round 2): `4번은 B로 하고, 나머지는 권장안으로 채택한다.`
- 사용자 답변(2026-08-11, Round 3): `연락처를 왜 이렇게까지 관리해? 사내 서비스인데?? 그냥 자유롭게 풀어도 될 것 같은데.`
- 사용자 확인(2026-08-11): `요약 확인`
- 사용자 정책 정정: 연락처를 secret처럼 빌드·런타임 설정으로 주입하거나 누락 시 별도 배포 gate를 두지 않는다. 담당 부서명과 공용 이메일·전화번호 등 업무용 공개 연락처를 정적 처리방침 문안에 직접 기재하고, 변경 시 일반 문안 개정·배포 절차로 관리한다. 개인 계정 credential·secret은 계속 제외한다.
- Runner 형식 계약: 각 interview artifact의 질문 heading은 round 안에서 `### 질문 1`부터 `### 질문 5` 사이의 local 번호를 사용한다. 이전 round의 질문 번호를 이어 쓰지 않는다.
- Round 2 첫 호출 상태: Fable이 누적 번호 `질문 6~8`을 사용해 runner가 `FABLE_READONLY_QUESTION_COUNT_INVALID`로 저장을 거부했다. Repository artifact는 생성되지 않았으며 같은 round를 형식 계약에 맞춰 재호출한다.

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: EMI PMS는 회사 Microsoft 365 계정으로 로그인하는 사내 서비스이며 실제 Azure 환경에서 시범 운영 중이다. 사용자는 공지사항과 별도의 PWA 설치 안내 팝업을 준비했고, 모바일 설치 후 알림을 받을 수 있다는 이용 안내도 제공하려 한다.
- 해결할 문제: 실제 임직원 계정·업무 데이터·사진/첨부·알림 이력을 처리하지만 개인정보 처리방침, 권리 행사·문의 안내와 사내 이용수칙을 앱 안에서 확인할 사용자-facing 체계가 없다. 동의가 필요한 처리와 단순 고지·업무상 필수 처리를 구분해야 한다.
- 현재 우회 방식: 공지사항과 설치 팝업으로 시작·시범 운영·설치 방법을 안내하지만 개인정보 처리 목적·항목·보유기간·권리 행사·수탁/이전 여부를 지속적으로 조회할 기준 페이지는 없다.
- 성공했을 때 사용자가 할 수 있는 일: 모든 사내 사용자가 PC·모바일·PWA에서 현재 적용되는 개인정보 처리방침과 내부 이용수칙을 확인하고, 개인정보 문의·열람·정정·삭제·처리정지 요청 경로를 찾으며, 선택 동의가 필요한 항목만 분리해 판단할 수 있다.
- 하지 않을 경우 영향: 시범 운영이라는 이유로 법적 고지와 권리 행사 경로가 누락되고, 포괄 동의 또는 실제 제공하지 않는 Web Push 표현으로 사용자 혼선과 운영 위험이 생길 수 있다.

### Codex 사전 법적 검토 기준선

다음은 Fable이 법률 판단을 대신하지 않도록 제품 기획 입력으로 제공하는 Codex 조사 결과다. 최종 문안과 회사 정책 적합성은 개인정보 보호책임자 또는 전문 검토가 필요하다.

- 실제 임직원 개인정보를 처리하는 시범 운영에도 개인정보 보호법 적용이 배제되지 않는다.
- 개인정보 처리방침 공개는 필수 축이다. 최소한 처리 목적·항목·보유기간, 제3자 제공, 파기, 위탁, 권리 행사, 보호책임자, 안전성 확보조치와 변경 이력 등 실제 해당 내용을 반영해야 한다.
- 열람·정정·삭제·처리정지 등 권리 행사 방법과 접수 창구는 제공해야 한다. 별도 페이지는 법정 형식의 필수라기보다 사용성상 권장 구조다.
- 동의는 모든 처리에 일괄 적용하지 않는다. 법령·계약·정당한 업무 목적 등 다른 처리 근거가 있는 필수 업무정보와, 동의를 근거로 하는 선택 항목을 분리한다. 동의가 필요하면 선택 항목·목적·보유기간·거부권과 불이익을 구분한다.
- 위탁, 제3자 제공과 국외 이전은 실제 계약·데이터 흐름이 있을 때 개인정보 처리방침 또는 별도 고지에 반영한다.
- 사내 서비스 이용약관은 자동 필수로 단정하지 않는다. 대신 업무 목적, 계정 공유 금지, 최소 열람·다운로드, 무단 반출 금지, 사고 신고 등 사내 시스템 이용수칙을 권장한다.
- 쿠키 전용 페이지는 현재 확인된 범위에서 별도 필수로 보지 않으며, 실제 사용하는 인증·필수 저장과 분석 도구가 있다면 처리방침 또는 관련 안내에 반영한다.

공식 근거:

- 개인정보 보호법 제30조: https://law.go.kr/LSW/lsLinkCommonInfo.do?lsJoLnkSeq=1033214957
- 개인정보보호위원회 개인정보 처리방침 작성지침: https://www.pipc.go.kr/np/cop/bbs/selectBoardArticle.do?bbsId=BS074&mCode=C020010000&nttId=12021
- 개인정보 보호법 제15조: https://www.law.go.kr/lsLinkCommonInfo.do?chrClsCd=010202&lsJoLnkSeq=1029335387
- 개인정보 보호법 제22조: https://law.go.kr/lsLinkCommonInfo.do?chrClsCd=010202&lsJoLnkSeq=1033215029
- 개인정보 보호법 제26조: https://law.go.kr/LSW/LsiJoLinkP.do?docType=JO&joNo=002600000&languageType=KO&lsNm=%EA%B0%9C%EC%9D%B8%EC%A0%95%EB%B3%B4+%EB%B3%B4%ED%98%B8%EB%B2%95&paras=1
- 개인정보 보호법 제28조의8: https://www.law.go.kr/lsLinkCommonInfo.do?chrClsCd=010202&lsJoLnkSeq=1029334953
- 개인정보 보호법 제35조: https://law.go.kr/LSW/lsLinkCommonInfo.do?chrClsCd=010202&lsJoLnkSeq=1016143207
- 개인정보 보호법 제37조: https://www.law.go.kr/LSW/lsLawLinkInfo.do?chrClsCd=010202&lsJoLnkSeq=900079171

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 모든 승인된 사내 사용자 | 최신 방침·권리 행사·이용수칙 조회 | 사용자 공통 공개 문안 | 없음 | 현재 적용 버전·시행일 확인 가능 |
| 개인정보 문의 담당자/운영자 | 문의·권리 행사 접수 경로 운영 | 접수에 필요한 최소 정보 | 후속 운영 절차에 따름 | 접수·처리 이력과 접근 제한 필요 여부 결정 |
| 문안 관리자 | 정책 초안·시행 버전 관리 여부 결정 | 정책 버전·변경 이력 | 구현 범위에서 별도 확정 | 회사 승인 주체·게시 책임 구분 필요 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: 로그인한 사용자가 공통 진입점에서 최신 개인정보 처리방침·권리 행사·사내 이용수칙을 조회한다. PWA 설치 안내는 설치 방법과 실제 제공되는 알림 채널을 별도 설명한다.
- validation 실패: 문안이 없거나 시행일·연락 창구가 비어 있으면 운영 게시를 완료로 표시하지 않는다.
- 동시 처리·중복: 정책 문안의 버전 관리·승인 방식은 미확정이다.
- 취소·재시도·복구: 잘못 게시된 문안의 이전 버전 복구와 정정 고지 방식은 미확정이다.
- 부분 실패와 rollback: 정적 페이지 배포 실패 시 기존 앱 기능과 인증을 보존하고 이전 검증된 문안·Frontend revision으로 되돌리는 방식을 고려한다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: 현재 앱은 이름, 업무용 이메일/계정 식별정보, 부서·역할, 프로필 사진, 프로젝트·업무·감사 이력, 업로드 사진/파일과 알림 설정·전달 이력을 처리한다. 구체적인 실제 값은 기획 문서에 기록하지 않는다.
- 상태 전이: 정책 문안의 Draft/Approved/Effective/Retired 같은 관리 상태가 필요한지는 미확정이다. 최소 정적 문서로 시작하는 대안과 비교한다.
- 보존·감사·삭제: 계정·업무·첨부·감사·알림 데이터별 실제 보유기간과 시범 운영 종료 시 처리 방침을 회사가 확정해야 한다.
- attachment·Excel·PDF: 정책 페이지 자체에는 첨부가 필수는 아니다. 기존 업무 첨부·Excel/PDF에 포함되는 개인정보 범주는 처리방침에 반영한다.
- 외부 연동·notification: Microsoft Entra/Azure, Teams Activity와 Gmail 메일 전달이 현재 범위다. OS 수준 Web Push·Service Worker·push 구독은 구현되지 않은 별도 신규 기능이다.
- migration·기존 데이터: 조회 전용 정적 문안이면 migration이 필요 없을 수 있다. 관리자 버전 관리·동의 이력을 추가하면 신규 데이터 모델과 migration이 필요하다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 로그인·계정 영역, 공통 footer 또는 메뉴에서 항상 재진입 가능한 구조를 검토한다. 최초 안내 공지/PWA 팝업은 상세 정책 페이지 링크를 제공할 수 있다.
- loading·empty·error·success feedback: 정적 문안은 안전한 fallback과 시행일 표시가 필요하다. 권리 행사 접수 기능을 만들 경우 접수 성공·실패·처리 상태 UX가 추가로 필요하다.
- 접근성·390px·Teams narrow: 긴 법적 문안을 제목·목차·접기보다 읽기 쉬운 section과 anchor로 구성하고 키보드·스크린리더·390px 가로 overflow 0을 검증한다.
- UAT와 rollout: 문안의 회사 승인, 실제 수탁/이전·보유기간 대조, PC·390px·PWA·Teams launcher 경로 검수를 구현 승인과 분리한다.
- rollback과 운영자 대응: 잘못된 문안·연락처·시행일의 정정 게시, 이전 버전 보존과 사용자 공지 기준을 결정한다.

## 6. 포함·제외 범위

### 포함

- 개인정보 처리방침 사용자 화면과 상시 진입점
- 개인정보 권리 행사·문의 안내의 화면/동선
- 사내 시스템 이용수칙의 화면/동선
- PWA 설치 안내와 실제 알림 채널 표현의 연결
- 조건부 동의가 필요한 경우의 분리 원칙과 대안
- 시행일·변경 이력·운영 승인·모바일 접근성·rollout/rollback 기획

### 제외

- 법률 자문을 대체하는 최종 법적 적합성 보증
- 실제 회사명·담당자 이름·이메일·전화번호 같은 운영 원문 기록
- 신규 Web Push·Service Worker·구독 lifecycle 구현
- Azure·Teams·메일 provider 변경 또는 실제 발송
- 제품 코드·DB·migration·runtime 변경과 Git 게시

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 정책 문안을 정적 배포할지 앱에서 버전 관리할지 | Fable Round 1 원문 참조 | C. 정적으로 시작 + 승격 예약 | C. 권장안 채택 | No |
| 2 | 권리 행사를 안내만 할지 앱 내 접수까지 제공할지 | Fable Round 1 원문 참조 | A. 안내만 | A. 권장안 채택 | No |
| 3 | 현재 처리 항목 중 동의를 받을 선택 항목이 실제로 있는지 | Fable Round 1 원문 참조 | A. 현재는 동의 항목 0건, 회사 확인 Gate 유지 | A. 권장안 채택 | No |
| 4 | PWA 설치 안내와 공지의 알림 문구를 어떻게 표현할지 | Fable Round 1 원문 참조 | A. 현재 채널만 정확히 표현 | B. 현재 채널 안내 + 모바일 푸시 준비 중 예고 | No |
| 5 | 정책 페이지 진입점과 로그인 전 공개 여부 | Fable Round 1 원문 참조 | A. 로그인 후 전용으로 시작, 공개 필요성은 회사 판단 | A. 권장안 채택 | No |
| 6 | `모바일 푸시 준비 중` 예고를 어디까지 표시할지 | Fable Round 2 원문 참조 | A. 안내·공지에만 예고 | A. 권장안 채택 | No |
| 7 | 처리방침·권리 행사 안내·이용수칙을 한 페이지로 묶을지 | Fable Round 2 원문 참조 | A. 단일 안내 페이지 | A. 권장안 채택 | No |
| 8 | 문안 개정 시 변경 이력 표시와 사용자 공지 방법 | Fable Round 2 원문 참조 | A. 이력 목록 + 개정 공지 | A. 권장안 채택 | No |
| 9 | 권리 행사 창구·보호책임자 연락처 표기 방법 | Fable Round 2 원문 참조 | A. 부서·역할명 + 공용 창구 | 정정: 담당 부서명 + 공용 연락처를 정적 문안에 직접 기재 | No |
| 10 | 연락처 주입을 빌드 시점 또는 런타임 조회로 할지 | Fable Round 3 원문 참조 | A. 빌드 시점 주입 | N/A — 주입 전제 철회 | No |
| 11 | 주입 값 누락 시 배포 실패 또는 화면 대체 문구를 사용할지 | Fable Round 3 원문 참조 | A. 빌드 fail-closed | N/A — 주입 전제 철회 | No |
| 12 | 주입 대상을 운영 원문만으로 최소화할지 | Fable Round 3 원문 참조 | A. 운영 원문만 최소 주입 | N/A — 주입 전제 철회 | No |

## 8. Fable 확인용 요약

- 해결할 문제: 사내 시범 운영에 필요한 개인정보 고지·권리 행사·내부 이용 안내를 현재 앱 기능과 법적 최소요건에 맞게 구성한다.
- 권장 범위: 단일 정적 안내 페이지와 로그인 후 상시 진입점, 권리 행사 안내, 사내 이용수칙, 현재 알림 채널과 설치 안내·공지의 모바일 푸시 준비 중 예고, 개정 이력·공지·rollback 절차를 기획한다.
- 확정한 정책: 포괄 동의를 기본값으로 사용하지 않는다. 현재 동의 대상은 0건으로 두되 회사·보호책임자 확인 Gate를 유지한다. 담당 부서명과 공용 업무 연락처는 정적 제품 문안에 직접 기재하고 일반 문안 개정으로 관리한다.
- 명시적 제외: 법률 자문 대체, 개인 담당자 실명·개인 연락처·credential·secret 기록, 신규 Web Push 구현, runtime/provider/DB 변경과 제품 구현.
- Deferred 비차단 결정: 회사 승인 주체, 실제 공용 연락처, 보유기간, 수탁·제3자 제공·국외 이전, 법인 정식 명칭, 앱 내 버전 관리, Web Push 동의 설계와 로그인 전 공개 route는 회사 확인·후속 항목으로 둔다.
- Fable 판정: `SUMMARY_CONFIRMATION_REQUIRED`

## 9. 성공 기준

- 업무 결과: 필요한 페이지·진입점·문안 소유권·변경 고지·권리 행사 흐름과 조건부 동의 경계를 사용자가 승인할 수 있는 기획안이 나온다.
- 권한·데이터 불변조건: 기존 인증·권한·업무·알림 계약을 변경하지 않고 실제 개인정보·secret을 기록하지 않는다.
- 자동 검증: 문서 contract·링크·privacy-safe 검사와 향후 구현용 desktop/390px·접근성·route 회귀 계획이 정의된다.
- 사용자 검수: Fable 요약 확인, Fable primary planning, Codex 비교 review와 최종 구현 범위 승인 상태를 분리한다.

## 10. 사용자 확인

- [x] 업무 문제와 기대 결과가 정확하다.
- [x] 대상 역할과 권한이 정확하다.
- [x] 정상·예외·복구 흐름이 정확하다.
- [x] 포함·제외 범위가 정확하다.
- [x] Blocking 결정이 남아 있지 않다.
- [x] Fable 5가 작성한 이 요약을 planning 입력으로 사용하는 데 동의한다.

사용자 확인 후에만 다음 상태로 바꾼다.

- `interviewStatus: COMPLETED_CONFIRMED`
- `userConfirmed: true`
- `openBlockingDecisionCount: 0`
- `planningApproved: false`
- `implementationApproved: false`
