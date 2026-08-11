Baseline refresh is complete. The round-3 answers retracted the contact-injection premise entirely, and the recorded policy correction — department name plus shared business contact channels written directly into the static notice text, managed through the normal revision-and-deploy procedure — closes the last open decision chain. I re-read the full canonical interview, all three raw round artifacts, the identity gate, the privacy-evidence policy, the completion policy, the roadmap, and confirmed by search that no privacy-notice page exists yet in the frontend source. No blocking question remains, so this round is the confirmation summary.

---

# TASK-PRIVACY-NOTICE-001 — Deep Interview Round 4 (Fable 5) — 확인용 요약

## 기준선 확인

Round 3 사용자 답변과 정책 정정을 canonical interview에서 다시 읽고, 다음을 Repository에서 표적 확인했다.

1. **주입 전제 철회 반영** — Round 3의 질문 3건(주입 방식·누락 동작·주입 범위)은 사용자 정정으로 모두 `N/A`가 되었고, 대체 정책(담당 부서명과 공용 업무 연락처를 정적 문안에 직접 기재, 변경은 일반 문안 개정·배포 절차)이 canonical interview에 기록되어 있다. 이 정책은 그 자체로 완결적이어서 추가 기술 결정을 요구하지 않는다.
2. **신규 화면 여부 재확인** — frontend 소스에서 개인정보 처리방침·이용수칙 관련 화면·문안은 검색 결과 0건으로, 이번 Task가 기획할 단일 안내 페이지는 기존 구현과 중복되지 않는다.
3. **범위 문구 정정 필요 확인** — 사용자 정정은 기존 제외 범위 항목("실제 회사명·담당자 이름·이메일·전화번호 같은 운영 원문 기록")과 표현이 충돌한다. 아래 요약에서 이 항목을 정정된 경계로 다시 서술했으므로, 사용자 확인이 곧 범위 문구 정정의 확인이다.

남은 blocking 결정은 확인되지 않았다. 아래 요약을 확인해 주시면 interview를 `COMPLETED_CONFIRMED`로 전환하고 planning 입력으로 사용한다.

## 해결할 문제

실제 임직원 계정·업무 데이터·사진/첨부·알림 이력을 처리하는 사내 시범 운영에, 개인정보 처리방침·권리 행사 안내·사내 이용수칙을 앱 안에서 상시 확인할 사용자 화면이 없다. 포괄 동의나 미구현 Web Push 표현 없이, 실제 처리 사실과 법적 최소요건에 맞는 고지 체계를 기획한다.

## 확정한 정책 (사용자 답변 기준)

1. **정적 문안으로 시작 (1-C)** — 문안은 Frontend 코드에 포함해 배포하고, 앱 내 버전 관리 승격은 후속 Task 결정으로 남긴다. DB·migration·runtime 변경 없음.
2. **권리 행사는 안내만 (2-A)** — 열람·정정·삭제·처리정지의 담당 창구·요청 방법·처리 기한을 안내 페이지에 기재하고, 앱 내 접수·추적 기능은 만들지 않는다.
3. **현재 동의 항목 0건 (3-A)** — 현재 처리 항목은 모두 필수 업무 근거로 고지만 하고, "선택 기능 도입 시 별도 동의" 원칙을 문안에 명시한다. 이 판단의 회사·보호책임자 확인은 planning의 배포 전 확인 항목으로 유지한다.
4. **알림 문구 (4-B + R2-1-A)** — 현재 제공 채널(인앱 알림함·Teams·메일)만 사실대로 기술하고, "모바일 푸시 알림 준비 중" 예고는 설치 안내·공지에만 넣는다. 처리방침에는 예고를 병기하지 않고 "새 채널 도입 시 방침 갱신·필요 시 별도 동의" 원칙만 담는다.
5. **로그인 후 전용 진입 (5-A)** — 계정 메뉴와 기존 사이드바/모바일 메뉴 footer를 재사용해 상시 진입점을 두고, 로그인 화면과 인증 경계는 변경하지 않는다. 로그인 전 공개 필요성은 회사 판단으로 남긴다.
6. **단일 안내 페이지 (R2-2-A)** — "개인정보·이용 안내" 한 페이지에 처리방침(권리 행사 section 포함)과 이용수칙을 구분된 section·anchor 목차로 배치하고 문서별 시행일을 표기한다.
7. **개정·정정 절차 (R2-3-A)** — 페이지 안에 시행일·변경 요약의 이력 목록을 두고 이전 전문은 Git 이력으로 보존한다. 개정·정정 시 기존 홈 공지 기능으로 알리고, 잘못된 문안은 이전 검증 revision rollback + 정정 공지로 처리한다.
8. **연락처 표기 (Round 3 사용자 정정, 최종)** — 담당 부서명과 공용 이메일·전화번호 등 업무용 공개 연락처를 정적 문안에 직접 기재한다. 빌드·런타임 주입, placeholder, 누락 시 배포 gate는 두지 않으며, 연락처 변경은 일반 문안 개정·배포 절차로 처리한다. "문안이 없거나 시행일·연락 창구가 비어 있으면 게시 완료로 표시하지 않는다"는 원칙은 기계 gate가 아니라 게시 전 확인 checklist로 유지한다.

## 포함·제외 범위 (정정 반영)

- 포함: 단일 안내 페이지와 상시 진입점, 권리 행사·문의 안내 동선, 사내 이용수칙, PWA 설치 안내·공지의 알림 문구 연결(푸시 예고 포함), 개정 이력·개정 공지·rollback 절차, 시행일 표기, 모바일 390px·접근성 검증 계획.
- 제외(정정): 법률 자문을 대체하는 최종 적합성 보증, **개인 담당자 실명·개인 연락처·계정 credential·secret의 기록**(공용 업무 연락처의 제품 문안 기재는 허용으로 정정됨), 신규 Web Push·Service Worker·구독 구현, provider 변경·실제 발송, 제품 코드·DB·migration·runtime 변경과 Git 게시.
- 경계 유지: interview·planning·review 등 Task 문서와 검증 증빙에는 계속 실제 연락처 원문을 기재하지 않는다(Privacy-safe Evidence 적용 범위 유지). 공용 연락처 원문이 제품 문안 소스에 들어가는 것은 이번 정정으로 허용된 유일한 위치이며, 구현 단계에서 privacy 검사가 이 화면 문안을 오탐하지 않도록 하는 처리는 planning의 구현 참고 항목으로 전달한다.

## Deferred 비차단 결정 (planning의 회사 확인·후속 항목)

- 문안의 회사 승인 주체와 게시 책임 구분
- 실제 공용 연락처 값과 담당 부서 표기의 회사 확정
- 데이터 유형별 최종 보유기간과 시범 운영 종료 시 처리 방침
- 수탁·제3자 제공·국외 이전 목록의 실제 계약 대조
- 처리방침에 법인 정식 명칭 별도 기재 필요 여부
- 앱 내 버전 관리 승격, Web Push 도입 시 동의 설계, 로그인 전 공개 route 필요성

## 성공 기준

- 사용자 승인 가능한 기획안: 페이지 구조·진입점·문안 골격·개정 절차·조건부 동의 경계가 위 확정 정책대로 정의된다.
- 불변조건: 기존 인증·권한·업무·알림 계약 무변경, 미구현 Web Push를 제공한다고 표시하지 않음, Task 문서에 실제 개인정보·secret 미기재.
- 검증 분리: 문서 contract 검사와 향후 구현용 desktop/390px·접근성·route 회귀 계획을 정의하고, Fable 요약 확인 → planning → Codex review → 구현 승인을 각각 분리한다.

## 사용자 확인 요청

canonical interview 10절의 확인 항목 6개를 검토해 주십시오. 확인되면 Codex가 `interviewStatus: COMPLETED_CONFIRMED`, `userConfirmed: true`, `openBlockingDecisionCount: 0`으로 기록하고, planning은 그 뒤에만 시작한다. 이 확인은 planning 승인이나 구현 승인이 아니다.

---

- interviewStatus: SUMMARY_CONFIRMATION_REQUIRED
- planningStatus: NOT_STARTED
- implementationApproved: false
