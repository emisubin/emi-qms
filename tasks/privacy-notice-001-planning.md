# TASK-PRIVACY-NOTICE-001 — 사내 개인정보·이용 안내 페이지 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/privacy-notice-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 실제 임직원 계정·업무 데이터·사진/첨부·알림 이력을 처리하는 사내 시범 운영에, 개인정보 처리방침·권리 행사 안내·사내 이용수칙을 앱 안에서 상시 확인할 사용자 화면이 없다.
- 대상 사용자·역할: 모든 승인된 사내 사용자(조회 전용), 개인정보 문의 담당 부서(안내된 공용 창구 운영), 문안 관리자(개정·게시 책임, 회사 확정 대상).
- 정상 흐름: 로그인한 사용자가 상시 진입점에서 단일 안내 페이지를 열어 최신 처리방침·권리 행사 방법·이용수칙과 시행일·변경 이력을 확인한다.
- 예외·복구 흐름: 잘못된 문안·연락처·시행일은 이전 검증 Frontend revision으로 rollback하고 홈 공지로 정정을 알린다. 문안·시행일·연락 창구가 비어 있으면 운영 게시를 완료로 표시하지 않는다(게시 전 확인 checklist).
- 확정한 정책과 명시적 제외 (Round 1~3 답변과 Round 3 정정, Round 4 확인 기준):
  1. 정적 문안으로 시작(1-C). 앱 내 버전 관리 승격은 후속 결정. DB·migration·runtime 무변경.
  2. 권리 행사는 안내만(2-A). 앱 내 접수·추적 기능은 만들지 않는다.
  3. 현재 동의 항목 0건(3-A). 모든 현재 처리를 필수 업무 근거로 고지하고 “선택 기능 도입 시 별도 동의” 원칙만 명시. 이 판단의 회사·보호책임자 확인은 배포 전 checklist로 유지.
  4. 알림 문구(4-B + R2-1-A). 처리방침·알림 설정에는 현재 제공 채널(인앱·Teams Activity·메일)만 기술하고, “모바일 푸시 준비 중” 예고는 설치 안내·공지에만 넣는다.
  5. 로그인 후 전용 진입(5-A). 인증 경계 무변경. 로그인 전 공개 route는 회사 판단으로 유보.
  6. 단일 안내 페이지(R2-2-A). 처리방침(권리 행사 section 포함)과 이용수칙을 구분된 section·anchor 목차로 배치.
  7. 개정·정정 절차(R2-3-A). 페이지 내 변경 이력 목록 + Git 이력 보존 + 홈 공지 개정 알림 + rollback·정정 공지 단일 절차.
  8. 연락처 표기(Round 3 사용자 정정, 최종). 담당 부서명과 공용 이메일·전화번호 등 업무용 공개 연락처를 정적 문안에 직접 기재한다. 빌드·런타임 주입, placeholder, 누락 시 배포 gate는 두지 않는다.
  - 명시적 제외: 법률 자문 대체, 개인 담당자 실명·개인 연락처·credential·secret 기록, 신규 Web Push·Service Worker·구독 구현, provider 변경·실제 발송, 이 기획 Task 안에서의 제품 코드·DB·runtime 변경과 Git 게시.
- planning으로 넘긴 비차단 미결정 사항: 16절의 8개 항목(회사 승인 주체, 공용 연락처 실제 값, 보유기간, 수탁·제3자 제공·국외 이전 대조, 법인 정식 명칭, 버전 관리 승격, Web Push 동의 설계, 로그인 전 공개 route).

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

모든 사내 사용자가 PC·모바일·PWA에서 로그인 후 상시 진입점을 통해 현재 적용되는 개인정보 처리방침, 권리 행사 방법과 사내 이용수칙을 시행일·변경 이력과 함께 확인할 수 있다.

## 2. 배경과 해결할 업무 문제

- 현재 사용자는 회사 Microsoft 365 계정으로 로그인해 실제 Azure 환경의 시범 운영 서비스를 사용하며, 시작 안내는 홈 공지와 PWA 설치 안내 dialog로만 제공된다.
- 개인정보 처리 목적·항목·보유기간·권리 행사·수탁/이전 여부를 지속적으로 조회할 기준 페이지가 없어, 법적 고지와 권리 행사 경로가 누락된 상태로 시범 운영이 진행될 위험이 있다.
- 현재 우회 방식은 공지·설치 팝업의 일회성 안내뿐이며, 상시 재조회가 불가능하다.
- 이 기능이 없으면 포괄 동의 또는 실제 제공하지 않는 Web Push 표현으로 사용자 혼선과 운영 위험이 생길 수 있고, 정보주체 권리 행사 창구가 사실상 부재하게 된다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 모든 승인된 사내 사용자 | 최신 문안·시행일·변경 이력·권리 행사 방법 조회 | 사용자 공통 공개 문안 | 없음 (조회 전용 화면) |
| 개인정보 문의 담당 부서 | 문안에 안내된 공용 창구로 접수 응대 | 접수에 필요한 최소 정보(시스템 밖 절차) | 없음 (앱 내 접수 기능 없음) |
| 문안 관리자(회사 확정 대상) | 문안 개정·게시·정정 판단 | 문안 원문·Git 변경 이력 | 코드 개정·배포 절차를 통한 문안 변경 |

신규 서버 권한 능력은 없다. 페이지는 기존 인증 경계(로그인 후) 안의 공통 조회 화면이며 역할별 차등 표시가 없다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 상시 조회

1. 사용자가 계정 메뉴 또는 메뉴 footer의 `개인정보·이용 안내` 진입점을 누른다.
2. 시스템이 단일 안내 페이지를 열고 상단에 문서별 시행일과 anchor 목차를 표시한다.
3. 사용자는 처리방침(권리 행사 section 포함)과 사내 이용수칙을 section 단위로 읽고, 권리 행사가 필요하면 문안에 기재된 담당 부서·공용 창구로 요청한다.

### 시나리오 B — 개정 인지

1. 문안 관리자가 개정 문안을 일반 개정·배포 절차로 게시하고 홈 공지에 개정 공지를 등록한다.
2. 사용자가 홈 공지에서 개정 사실과 요약을 확인하고 공지의 안내에 따라 안내 페이지로 이동한다.
3. 사용자는 페이지의 변경 이력 목록에서 시행일과 변경 요약을 확인한다.

### 시나리오 C — 잘못된 문안 정정

1. 운영자가 게시된 문안의 오류(연락처·시행일 등)를 발견한다.
2. 이전 검증된 Frontend revision으로 rollback하거나 정정 문안을 배포한다.
3. 홈 공지로 정정 사실을 알리고, 변경 이력 목록에 정정 내역을 남긴다.

## 5. 기능 요구사항

### 필수

- [ ] 로그인 후 접근하는 단일 `개인정보·이용 안내` 화면(신규 view/route)과 anchor 목차
- [ ] 개인정보 처리방침 section: 처리 목적·항목·보유기간, 제3자 제공, 위탁, 파기, 권리 행사 방법·창구, 보호책임자(부서·역할명), 안전성 확보조치, 알림 채널 기술(인앱·Teams Activity·메일만), “새 채널 도입 시 방침 갱신·필요 시 별도 동의” 원칙, 변경 이력 목록
- [ ] 권리 행사 안내 section: 열람·정정·삭제·처리정지의 요청 방법, 담당 부서명·공용 연락처, 처리 기한 안내 (앱 내 접수 기능 없음)
- [ ] 사내 이용수칙 section: 업무 목적 사용, 계정 공유 금지, 최소 열람·다운로드, 무단 반출 금지, 보안 사고 신고
- [ ] 문서별 시행일 표기와 변경 이력 목록(시행일·변경 요약)
- [ ] 상시 진입점: 계정 popover(`AccountProfilePanel`)의 링크와 데스크톱 사이드바·모바일 메뉴 footer 영역의 링크
- [ ] PWA 설치 안내 dialog 문구 갱신: 현재 알림 채널 사실 기술 + “모바일 푸시 알림 준비 중” 예고 (`PwaInstallExperience.tsx`)
- [ ] 개정·정정 시 홈 공지(기존 NoticeBoard) 게시 절차와 rollback 절차의 SOP 문서화

### 선택

- [ ] 홈 공지 최초 안내 게시(운영 시점에 관리자가 콘텐츠로 등록, 코드 변경 아님)
- [ ] 설치 안내 dialog에서 안내 페이지로의 링크

### 명시적 제외

- [ ] 법률 자문을 대체하는 최종 법적 적합성 보증
- [ ] 개인 담당자 실명·개인 연락처·credential·secret의 기록 (공용 업무 연락처의 제품 문안 기재만 허용)
- [ ] 앱 내 문안 버전 관리·동의 UI·동의 이력 저장·권리 행사 접수 기능
- [ ] 신규 Web Push·Service Worker·push 구독 lifecycle
- [ ] 미인증 공개 route 신설, 인증·권한·알림 계약 변경
- [ ] Backend·DB·migration·runtime·외부 provider 변경

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 개인정보·이용 안내(신규) | 계정 popover 링크, 사이드바 footer 링크, 모바일 메뉴 footer 링크, 홈 공지 내 링크 | 문서별 시행일, anchor 목차, 처리방침·권리 행사·이용수칙 section, 변경 이력 목록 | section 이동(anchor), 문안 열람 | 정적 조회 화면으로 loading/error 상태 없음. mutation 없음 |
| PWA 설치 안내 dialog(기존 수정) | 자동 안내, 계정 메뉴 재진입 | 기존 설치 단계 + 현재 알림 채널 문구 + 푸시 준비 중 예고 | 기존과 동일 | 기존 feedback 유지 |

확인할 UX 항목:

- 긴 법적 문안을 접기 대신 제목·목차·anchor로 구성해 현재 위치와 다음 이동이 명확한가.
- heading 구조(h1→h2→h3)와 landmark가 스크린리더에서 문서 구조를 전달하는가. anchor 이동 시 focus가 대상 section으로 이동하는가.
- 390px과 Teams narrow pane에서 page-level horizontal overflow 0인가. 표 형태 문안(처리 항목·보유기간 등)은 좁은 화면에서 내부 scroll 또는 목록 전환으로 처리하는가.
- 진입점 라벨이 계정 메뉴·메뉴 footer에서 일관되고, 현재 사이드바·모바일 메뉴 footer의 기존 콘텐츠(ShellSwitchControls, 기본 안내 문구)와 시각적으로 충돌하지 않는가.
- 설치 안내의 “준비 중” 예고가 일정·기기 범위를 약속하지 않는 표현인가.

## 7. 업무 규칙과 불변조건

- 기존 Microsoft 365 인증·서버 권한·업무 workflow·알림 수신자와 발송 시점을 변경하지 않는다. 새 화면은 로그인 후 조회 전용이다.
- 처리방침에는 현재 실제 제공하는 처리·채널만 기술한다. 미구현 Web Push를 제공한다고 표시하지 않으며, “준비 중” 예고는 설치 안내·공지에만 둔다.
- 포괄 동의를 기본값으로 사용하지 않는다. 현재 동의 항목은 0건이며, 선택 기능 도입 시 별도 동의 원칙만 문안에 명시한다.
- 문안에는 개인 담당자 실명·개인 연락처를 쓰지 않는다. 담당 부서명과 공용 업무 연락처만 기재한다.
- 문안·시행일·연락 창구가 비어 있으면 운영 게시를 완료로 표시하지 않는다. 이 원칙은 기계 gate가 아니라 게시 전 확인 checklist로 강제한다(Round 3 사용자 정정).
- 실제 공용 연락처 원문이 허용되는 위치는 제품 문안 소스(신규 페이지 component) 하나뿐이다. interview·planning·review·implementation report·검증 증빙에는 계속 기재하지 않는다(Privacy-safe Evidence 유지).
- 승인·완료된 문안 이력을 덮어쓰지 않는다. 개정은 새 시행일·변경 이력 행 추가와 Git 이력으로 누적한다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 안내 문안 | Frontend 코드에 포함된 정적 한국어 문안 | 신규 (코드 내 콘텐츠) | Git commit 이력으로 전문 보존 |
| 시행일·변경 이력 | 문안 내 표기(문서별 시행일, 이력 목록) | 신규 (코드 내 콘텐츠) | 이력 행은 삭제하지 않고 누적 |
| 개정 공지 | 홈 공지 게시물 | 기존 NoticeBoard 재사용 | 기존 공지 보존 정책 따름 |

DB 테이블·API 자원·클라이언트 저장소 신설은 없다. 문안 lifecycle은 시스템 상태가 아니라 개발·배포 절차다:

```text
개정 초안(branch) → 회사 검토·승인 → 배포(시행) → [오류 시] 이전 검증 revision rollback + 정정 공지
```

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 기존 인증(로그인 후에만 SPA 접근)만 해당하며 이번 범위에서 변경 없음.
- 필요한 조회와 mutation: 없음. 신규 endpoint를 만들지 않는다.
- 권한·validation: 신규 권한 능력 없음.
- transaction·동시성·idempotency: 해당 없음.
- audit trail: 문안 변경은 Git 이력, 개정 공지는 기존 공지 기록으로 추적.
- 외부 provider 영향: 없음. Teams·메일 발송 계약 무변경.

## 10. Frontend 고려사항

- route/component: `View` union(`frontend/src/App.tsx`)에 신규 kind(예: `privacy-notice`) 추가, URL 경로·뒤로가기 연동은 기존 view 전환 패턴을 따른다. 문안은 신규 페이지 component 파일(예: `PrivacyNoticePage`)로 분리해 `App.tsx` 비대화를 피한다.
- 진입점: `AccountProfilePanel`의 설치 안내 버튼(`account-install-button`) 인근에 안내 링크 추가, `AppNavigation`/`AppMobileNavigation`의 footer 영역(`app-sidebar-footer`, `mobile-menu-footer`)에 링크 배치. footer는 현재 `shellSwitchControls`를 받으므로 기존 콘텐츠와 병행 배치 방식을 구현 시 확정한다.
- loading/empty/error/success: 정적 화면으로 비동기 상태 없음. target-not-found는 기존 view fallback을 따른다.
- 공통 Action Feedback: mutation이 없어 적용 대상 아님.
- 접근성: heading 계층, anchor 목차의 keyboard 접근, anchor 이동 시 focus 관리, 색 대비.
- 390px/mobile/narrow pane: page-level overflow 0, 긴 문단·표의 좁은 화면 배치, PWA standalone과 Teams tab에서의 스크롤 확인.
- 설치 안내 문구: `PwaInstallExperience.tsx`의 설명 문단에 현재 알림 채널 사실 기술과 “모바일 푸시 준비 중” 예고를 추가하고 기존 test(`frontend/tests/pwa-install.test.tsx`)를 갱신한다.
- privacy 검사 오탐 방지: 공용 연락처 원문이 들어가는 신규 페이지 component는 secret/PII 검사에서 승인된 예외 위치로 명시해, 검사 약화 없이 이 파일·문안만 허용되도록 구현 시 처리한다.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 업무 흐름과 무관한 조회 화면. 알림 설정 화면(`NotificationPreferencesPage`)의 채널 표기와 처리방침의 채널 기술이 일치해야 한다(인앱·Teams Activity·메일).
- 권한/관리자: 신규 관리자 기능 없음. 개정 공지는 기존 공지 권한 체계를 그대로 사용.
- Excel/PDF/첨부: 페이지 자체 첨부 없음. 기존 업무 첨부·Excel/PDF에 포함되는 개인정보 범주를 처리방침 문안에 반영한다.
- Teams/Mail: 발송 계약 무변경. 문안은 채널 사실 기술만 포함.
- 삭제·복구/감사: 문안 복구는 Git revision rollback + 정정 공지 절차로 처리.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A. 정적 단일 페이지 + 기존 진입점 재사용 (권장·interview 확정) | 문안을 신규 component에 정적 포함, 계정 메뉴·메뉴 footer 진입, 개정은 코드 배포와 홈 공지 | DB·migration·Backend 무변경. rollback 단순. 최소 범위로 법적 공백 해소 | 문안 수정마다 배포 필요. 게시 권한이 개발 흐름에 묶임 |
| B. 앱 내 버전 관리(NoticeBoard 유사 관리 화면) | Draft/Effective 버전을 관리자 화면에서 게시 | 배포 없이 개정 가능, 이력 자동 축적 | 신규 데이터 모델·migration·권한 필요. 시범 운영 대비 과대 범위 |
| C. 문서별 페이지 분리 | 처리방침과 이용수칙을 별도 route로 분리 | 문서 성격·개정 주기 분리 명확 | route·진입점·검증 범위 2배. interview에서 단일 페이지로 확정 |

권장안은 A이며 interview Round 1(1-C)·Round 2(R2-2-A)에서 사용자가 확정했다. B는 후속 승격 결정(16절 6번), C는 문서가 실제로 길어질 때의 후속 분리 옵션으로 남긴다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. DB 조회·쓰기 없음.
- migration 필요 여부: 없음.
- 외부 발송/실제 데이터 영향: 없음. 개정 공지는 운영 시점에 관리자가 기존 공지 기능으로 게시.
- runtime 교체 여부: 이 기획 Task는 없음. 구현 결과의 운영 반영은 기존 승인형 release workflow와 별도 게시 승인을 따른다.
- 추가 사용자 승인 필요 작업: 문안의 회사(보호책임자) 확인, 실제 공용 연락처 값 확정·기재, 구현 승인, commit·push·PR·merge·운영 배포 각각의 명시적 승인.

## 14. 검증 계획

- 최소 테스트: `corepack pnpm --dir frontend run lint` / `typecheck` / `test` / `build`. 신규 페이지 unit test(`frontend/tests/` 규약, section heading·anchor 목차·시행일·변경 이력 렌더 확인), `App.navigation` 계열 test에 진입점·view 전환 추가, `pwa-install.test.tsx` 문구 갱신.
- 영향 영역 회귀: 사용자 UX 변경 기준으로 desktop·390px에서 신규 페이지와 진입점(사이드바·모바일 메뉴·계정 popover)의 overflow 0, console/request 오류 0, 기존 메뉴·설치 안내 동작 회귀 확인. raw DOM·screenshot 대신 Privacy-safe Evidence projection 사용, 실험 계보 요구 시 synthetic desktop/mobile 시각 증빙.
- PR/CI: 문서·frontend 변경 분류에 따른 표준 CI. changed-file allowlist와 PII/secret 검사(신규 문안 파일의 승인된 연락처 예외 처리 포함).
- 사용자 검수: ① 문안 내용의 회사·보호책임자 확인(동의 0건 판단·보유기간·수탁/이전·연락처 값 포함), ② PC·390px·PWA standalone·Teams 실행 경로에서 페이지·진입점 육안 확인, ③ 설치 안내 문구 확인. 자동 검증 완료와 사용자 검수 완료를 분리 관리한다.

## 15. 완료 기준

- 기능/권한/데이터: 로그인 후 단일 안내 페이지와 3개 진입점이 동작하고, Backend·DB·권한·알림 계약 diff 0.
- UX: 390px·Teams narrow overflow 0, anchor 목차·접근성 확인, 설치 안내 문구가 확정 정책(4-B + R2-1-A)과 일치.
- 자동 테스트: 14절 최소 테스트 전건 통과, 미실행 항목은 이유와 함께 기록.
- 5종 산출물: implementation report, 개정·정정·rollback SOP, 사용자 안내(문안 자체가 사용자-facing 문서이므로 canonical 위치 지정), Roadmap update, user validation checklist의 상태·위치 추적.
- 사용자 검수 상태: 문안 회사 확인과 화면 검수가 완료되기 전에는 `사용자 검수 대기`를 유지.
- PR 상태: 검수 대기 중에는 Draft, merge·운영 배포는 별도 승인 1회 경계 유지.

## 16. 미결정 사항

모두 interview에서 명시적으로 deferred된 비차단 결정이다. 1~5번은 구현·게시 전에, 6~8번은 후속 Task 시점에 확정하면 된다.

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 문안의 회사 승인 주체와 게시 책임을 누구로 확정하나 | 보호책임자 지정 부서 / 경영 승인 병행 | 대기 |
| 2 | 문안에 기재할 실제 담당 부서명과 공용 연락처 값 | 회사 확정 값 (구현 change에서 기재) | 대기 |
| 3 | 데이터 유형별 최종 보유기간과 시범 운영 종료 시 처리 방침 | 회사 확정 후 문안 반영 | 대기 |
| 4 | 수탁·제3자 제공·국외 이전 목록의 실제 계약 대조 결과 | 해당 없음 확인 / 목록 기재 | 대기 |
| 5 | 처리방침에 법인 정식 명칭 별도 기재 필요 여부 | 시스템 표시명만 / 법인명 병기 | 대기 |
| 6 | 앱 내 문안 버전 관리 승격 여부와 시점 | 유지(정적) / 후속 NEW_FEATURE | 대기 (후속) |
| 7 | Web Push 도입 시 동의·수신 정책 설계 | 별도 NEW_FEATURE에서 결정 | 대기 (후속) |
| 8 | 로그인 전 공개 route(미인증 문안 공개) 필요 여부 | 불필요 / 별도 NEW_FEATURE | 대기 (후속) |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: 없음.
- Frontend: `frontend/src/App.tsx`(View union·경로 해석·진입점·render 연결), 신규 안내 페이지 component 파일, `frontend/src/PwaInstallExperience.tsx`(문구), 기존 stylesheet의 페이지 스타일.
- DB/Migration: 없음.
- Tests/Scripts: `frontend/tests/`의 신규 페이지 test, navigation test 추가분, `pwa-install.test.tsx` 갱신. 필요 시 PII 검사 예외 목록 갱신.
- Docs: Product Roadmap 상태 갱신, 개정·정정·rollback SOP, implementation report와 user validation checklist.

## 18. Roadmap 연결

- 선행 Task: `TASK-AZURE-DEPLOY-001`(운영 배포 기반), `TASK-TEAMS-PWA-001`(설치 안내 경험), `TASK-NOTICE-BOARD-001`(개정 공지 채널) — 모두 해당 scope 완료 상태를 재사용하며 재구현하지 않는다.
- 후속 Task: Web Push `NEW_FEATURE`(동의 설계 포함), 앱 내 문안 버전 관리 승격, 로그인 전 공개 route(필요 시).
- 현재 Go/No-Go: Roadmap 실행 큐에 없는 별도 승인 제품 Task로, Task Identity Gate `PASS_CREATE`와 사용자 명시 요청이 기록되어 있다. 구현 착수는 이 planning과 Codex review에 대한 사용자 승인 이후다.
- 별도 Task로 분리할 항목: 16절 6~8번, 권리 행사 앱 내 접수 시스템화(수요 확인 시).

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-08-11 | Round 1 답변 `1C · 2A · 3A · 4B · 5A` | 정적 시작·안내만·동의 0건·푸시 예고 포함·로그인 후 전용 |
| 2026-08-11 | Round 2 답변 `1A · 2A · 3A · 4B` | 예고는 안내·공지 한정, 단일 페이지, 이력+개정 공지, 연락처 주입(이후 철회) |
| 2026-08-11 | Round 3 정정 | 연락처 주입 전제 철회. 부서명·공용 연락처를 정적 문안에 직접 기재 |
| 2026-08-11 | Round 4 `요약 확인` | interview `COMPLETED_CONFIRMED`, planning 입력 확정 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

아래 초안은 Codex 내용 review와 사용자 planning 승인, 그리고 16절 1~5번의 회사 확정(최소한 문안 기재에 필요한 값) 이후에만 유효하다.

1. 새 구현 세션에서 instruction chain gate를 수행하고 승인된 branch·allowlist를 확인한다.
2. Frontend만 변경한다: 신규 안내 페이지 component와 `View` kind·경로 연결을 추가하고, 계정 popover·사이드바 footer·모바일 메뉴 footer에 진입 링크를 배치한다. 기존 footer 콘텐츠와의 병행 배치를 깨뜨리지 않는다.
3. 문안은 7절 불변조건을 그대로 반영한다: 현재 채널만 기술, 동의 0건 고지 원칙, 부서명·공용 연락처 직접 기재(회사 확정 값), 문서별 시행일·변경 이력 목록. 개인 실명·credential·미구현 기능 표현을 넣지 않는다.
4. `PwaInstallExperience.tsx` 설명 문구에 현재 채널 안내와 “모바일 푸시 준비 중” 예고를 추가하고 관련 test를 갱신한다. 처리방침에는 예고를 병기하지 않는다.
5. PII/secret 검사에서 신규 문안 파일의 공용 연락처만 승인된 예외로 처리하고 검사 자체를 약화하지 않는다. Task 문서·증빙에는 연락처 원문을 기재하지 않는다.
6. 14절 검증 계획(lint·typecheck·test·build, desktop/390px 회귀)을 실행하고 결과와 미실행 항목을 implementation report에 분리 기록한다.
7. 개정·정정·rollback SOP와 게시 전 확인 checklist(문안·시행일·연락 창구 비어 있음 금지)를 작성한다.
8. Persistent UAT·실제 provider·push·PR·merge·운영 배포는 수행하지 않고 사용자 검수 대기로 종료한다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 8
