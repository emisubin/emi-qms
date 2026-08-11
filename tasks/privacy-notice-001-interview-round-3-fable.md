Baseline refresh is complete. Round 2 answers `1A · 2A · 3A · 4B` are recorded, and the user's choice of deploy-time injection (4B) over the recommended fixed public-channel wording opens three tightly coupled follow-up decisions — injection mechanism, missing-value behavior, and injection scope — that the planning draft cannot fix alone. I confirmed the repository's actual deploy-time configuration precedents (build-time `VITE_*` variables and fail-closed build-arg validation in the production Dockerfiles) to ground the options. Below is the round-3 artifact.

# TASK-PRIVACY-NOTICE-001 — Deep Interview Round 3 (Fable 5)

## 기준선 확인

Round 2 답변 `1A · 2A · 3A · 4B`를 canonical interview에서 다시 읽고, 다음을 Repository에서 표적 확인했다.

1. **배포 시점 주입의 기존 패턴 (`frontend/Dockerfile.production`, `frontend/Dockerfile.azure`)** — 운영 frontend image는 이미 `ARG → ENV` build argument로 Azure 인증 설정을 받고, 빌드 단계에서 빈 값·형식 오류를 검사해 실패시키는 fail-closed 검증 스크립트를 내장하고 있다. "배포 시 주입"(Round 2의 4B)을 이 기존 패턴 위에 얹을 수 있다.
2. **Frontend 설정 소비 방식 (`frontend/src/auth.ts`, `frontend/src/api.ts`)** — 현재 frontend는 빌드 시점 `VITE_*` 환경변수만 읽으며, 서버에서 설정값을 내려받는 런타임 config 조회 패턴은 존재하지 않는다. 런타임 주입을 선택하면 신규 API가 필요하다.
3. **게시 완료 validation 원칙 (canonical interview 3절)** — "문안이 없거나 시행일·연락 창구가 비어 있으면 운영 게시를 완료로 표시하지 않는다"가 이미 합의되어 있다. 4B에서 주입이 누락됐을 때의 동작을 정하지 않으면 이 원칙을 기계적으로 보장할 수 없다.

이번 round의 세 질문은 모두 4B(연락처 배포 시 주입) 확정이 연 후속 결정으로, 주입 계약을 고정해야 planning이 문안 골격·배포 검증·rollback 절차를 작성할 수 있다. 이 세 결정 외에 남은 blocking 항목은 확인되지 않았으므로, 답변이 기록되면 다음 round는 확인용 요약이 될 것으로 예상한다.

### 질문 1 — 연락처 주입은 빌드 시점 방식으로 할까요, 런타임 조회 방식으로 할까요

- **필요한 이유**: 4B의 "배포 설정으로 주입"은 두 가지로 구현할 수 있고, 선택에 따라 이번 Task의 기술 범위가 달라진다. 빌드 시점 주입은 기존 Dockerfile build argument 패턴의 확장으로 끝나지만, 런타임 조회는 존재하지 않는 신규 Backend 설정 API를 만들어야 한다.
- **답변이 바꾸는 범위**: Backend API 신설 여부, 배포 절차 문서의 변수 준비 단계, 연락처 변경 시 재배포 필요 여부, 검증 지점(이미지 빌드 vs 런타임 화면).

| 선택지 | 내용 | 장점 | 단점 |
| --- | --- | --- | --- |
| A. 빌드 시점 주입 | 기존 운영 image의 `ARG → ENV → 빌드 전 검증` 패턴에 연락 창구 변수를 추가하고, 문안 component가 빌드 시점 값을 읽음 | 기존 Azure 배포 절차·검증 패턴 재사용으로 신규 구조 없음. Backend·runtime 무변경(이 Task 제외 범위와 일치). 값 검증을 빌드 단계에서 끝낼 수 있음 | 연락 창구 변경 시 이미지 재빌드·재배포 필요(문안 개정도 어차피 재배포이므로 실질 부담은 낮음) |
| B. 런타임 config 조회 | Backend가 연락 창구 값을 내려주는 신규 설정 endpoint를 만들고 화면이 조회 | 재빌드 없이 값 변경 가능 | 신규 API·Backend 설정 관리·조회 실패 상태 UX가 범위에 추가됨. 현재 frontend에 선례 없는 패턴. 시범 운영 규모 대비 과잉 |

- **권장안**: **A**. 정적 문안(1C)에서는 문안 개정 자체가 재배포이므로 연락처만 재배포 없이 바꿀 수 있는 이점이 작고, 기존 build argument 검증 패턴을 그대로 확장하는 A가 "기존 패턴 우선 재사용" 원칙과 이 Task의 runtime 무변경 경계에 맞다.

### 질문 2 — 주입 값이 누락되면 배포를 실패시킬까요, 화면에 대체 문구를 보여줄까요

- **필요한 이유**: Round 2에서 B(주입)의 핵심 위험으로 "설정 누락 시 빈 연락처 노출"을 확인했고 사용자가 그 위험을 안고 B를 선택했다. 이제 누락 시 동작을 정해야 "연락 창구가 비어 있으면 게시 완료로 표시하지 않는다"는 기존 validation 원칙을 어디서 강제할지 planning이 확정할 수 있다.
- **답변이 바꾸는 범위**: 이미지 빌드 검증 스크립트의 검사 항목, 배포 checklist·SOP의 변수 준비 단계, 문안 화면의 fallback 상태 존재 여부, 390px 검증 대상 상태 수.

| 선택지 | 내용 | 장점 | 단점 |
| --- | --- | --- | --- |
| A. 빌드 fail-closed | 운영 빌드에서 연락 창구 값이 비어 있거나 형식이 틀리면 이미지 빌드를 실패시킴(기존 Azure 변수 검증과 동일 위치·동일 방식). Development 환경은 명시적 synthetic 기본값 사용 | 빈 연락처가 사용자에게 노출될 경로가 원천 차단됨. 기존 validation 원칙을 사람 checklist가 아니라 기계 gate로 강제. 화면에 fallback 상태가 없어 검증 범위 최소 | 배포 pipeline에 변수 준비가 선행 조건이 됨(누락 시 배포 자체가 멈춤) |
| B. 화면 fallback 문구 | 값이 없으면 "창구 준비 중, 담당 부서로 문의" 같은 대체 문구를 표시하고 배포는 성공시킴 | 배포가 변수 준비와 분리되어 항상 성공 | 법정 필수 표기(권리 행사 창구)가 빠진 문안이 게시될 수 있어 기존 validation 원칙과 충돌. fallback 상태의 문안·검증이 추가로 필요 |

- **권장안**: **A**. 처리방침에서 연락 창구는 있어도 되고 없어도 되는 장식이 아니라 법정 필수 표기이므로, 누락된 채 게시되는 상태 자체를 만들지 않는 것이 맞다. 기존 운영 image가 이미 같은 방식으로 인증 변수를 검증하고 있어 추가 구조 없이 검사 항목만 늘리면 된다.

### 질문 3 — 주입 대상은 실제 연락 주소만으로 최소화할까요, 부서·역할명까지 전부 변수로 뺄까요

- **필요한 이유**: Repository 불변조건은 회사 domain이 포함된 이메일 주소 같은 운영 원문의 tracked 기록을 금지하지만, 담당 부서·역할명 같은 비식별 표기는 금지 대상이 아니다(제품 문서가 이미 부서별 역할 구조를 공개적으로 기술한다). 주입 변수의 경계를 정해야 planning이 문안 골격에서 "코드에 남는 문장"과 "placeholder"를 확정할 수 있다.
- **답변이 바꾸는 범위**: 문안 골격의 placeholder 수와 위치, 빌드 검증 항목 수, 문안 변경 이력(Git)에서 추적 가능한 범위, 배포 시 회사가 준비해야 할 값 목록.

| 선택지 | 내용 | 장점 | 단점 |
| --- | --- | --- | --- |
| A. 운영 원문만 최소 주입 | tracked 금지 값(공용 메일함 주소·Teams 채널 링크 등 회사 domain 포함 연락 주소, 필요 시 법인 정식 명칭)만 변수로 주입. 담당 부서·역할명, 문안 구조와 시행일은 정적 문안에 유지 | 변수·검증 항목 최소. 문안 대부분이 Git 이력으로 추적됨(Round 2 질문 3의 변경 이력 결정과 정합). 문안 검토 시 placeholder가 적어 읽기 쉬움 | 부서명 개편 시에는 문안 개정(코드 변경)이 필요함 |
| B. 표기 전체 주입 | 부서·역할명·창구 안내 문구까지 모두 변수로 주입 | 조직 개편에도 코드 무변경 | placeholder가 많아 문안 원문 검토·Git 이력 추적이 어려움. 빌드 검증 항목과 누락 위험 증가. 불변조건상 뺄 필요가 없는 값까지 빼는 과잉 |

- **권장안**: **A**. 주입은 "Repository에 기록할 수 없는 값"으로 한정하는 것이 4B의 취지(운영 원문 미기록)를 정확히 만족하면서 정적 문안(1C)의 장점인 Git 기반 변경 이력을 최대한 보존한다. 시스템 표시명 `EMI PMS`는 이미 Repository 공식 표기이므로 주입 대상이 아니며, 처리방침에 법인 정식 명칭이 별도로 필요한지는 회사 확인 항목으로 planning에 남긴다.

---

- interviewStatus: QUESTIONS_REQUIRED
- planningStatus: NOT_STARTED
- implementationApproved: false
