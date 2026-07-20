# TASK-QR-001 — 패널 QR 생성·스캔 랜딩 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5_EXPERIMENT_FAST_TRACK`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 현재 `experiment/*` branch에서 사용자가 요청한 실제 패널 QR 생성과 QR 스캔 landing 기능의 interview source of truth다. 사용자는 사용자-facing interview와 중간 승인을 생략하고 `Fable 1차 기획 → Codex 내용 review → Fable 2차 기획 → Codex 구현·검증·screenshot → local commit`까지 이어가도록 명시했다. 비차단 제품 선택은 Fable의 Repository 근거 권장안을 자동 채택한다. 대표 repo, GitHub `main`, push·PR·merge, Persistent UAT, 실제 Microsoft Entra tenant와 외부 provider는 제외한다.

## Task Identity Gate

- proposedTaskId: `TASK-QR-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-QR-001`
- roadmapNextGate: `TASK-QR-001`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-QR-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `QR 스캔 landing`
- policyInputResolution: `FABLE_RECOMMENDATION_AUTO_ADOPT`
- gateStatus: `PASS_CREATE`

### Purpose identity

- 업무 목표: 기존 `qrEligible` 파생 상태를 실제 패널별 QR 발급으로 연결하고, 현장 사용자가 QR을 스캔하면 인증 후 그 패널의 역할별 현재 업무 또는 종합현황으로 빠르게 이동하게 한다.
- Root Finding 또는 정책 결정: Repository에는 QR 생성 가능 조건만 있고 실제 QR 식별자·발급·표시·인쇄·스캔 landing이 없다. 공개 landing 여부는 미확정이지만 확정 요구사항은 미로그인 사용자를 Microsoft 365 로그인으로 보내고 인증 후에만 데이터를 보여 주도록 한다.
- 변경·검증 경계: 안정적인 불투명 token, 패널당 단일 활성 QR, 발급·조회·다운로드 또는 인쇄 UX, 인증 경계, scan landing의 project scope·역할별 route 결정, 감사·오류·모바일 UX, isolated DB/API/E2E를 포함한다.
- 보존할 불변조건: QR payload에 고객사·프로젝트·패널명·내부 UUID 등 민감·업무정보 직접 포함 금지, 프로젝트·패널 활성과 기존 `qrEligible` 기준 유지, 모든 업무 데이터는 인증·권한 검사 후 반환, QR 스캔은 업무 상태를 변경하지 않음, 다른 부서는 조회만 가능하고 쓰기는 담당 권한을 유지, `main`·Persistent UAT·실제 provider 불변.
- 예상 산출물: Fable 1차 planning, Codex review, review 기반 Fable 2차 planning, additive DB/API/UI/QR image/tests, desktop/mobile 생성 화면과 스캔 landing screenshot, Implementation report, local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR·Issue

기존 `TASK-003B`와 panel API는 `qrEligible`만 계산하며 실제 QR 생성·토큰·landing을 명시적으로 제외한다. Roadmap과 완료 원장은 QR 스캔 landing을 첫 번째 미완료 제품 Task로 지정한다. GitHub 연결 조회와 local/remote refs에서 같은 목적의 Task·PR·Issue는 확인되지 않았다. 선행 MOBILE·제조·품질·물류·전체 상세 흐름은 experiment 완료 상태다.

## 사용자 실행 지시

- 요청일: 2026-07-20
- 요청: 현재 누적 작업을 커밋하고 QR 생성 작업을 바로 시작한다.
- standing rule: 이 experiment branch의 신규 기능은 인터뷰·중간 승인 없이 Fable 2-pass 권장안과 Codex 구현을 끝까지 진행한다.
- 게시 경계: local experiment 작업만 승인. `main` merge 승인 `0/3`.

## 1. 업무 문제와 기대 결과

- 현재 상태: 설계가 패널명을 입력하면 화면에 `QR 생성 가능`만 표시되고, 현장에서 부착하거나 스캔할 실제 QR은 없다.
- 해결할 문제: 패널과 QR을 안정적으로 1:1 연결하고, QR 이미지 배포와 스캔 이후 인증·업무 진입을 안전하고 빠르게 제공해야 한다.
- 성공 결과: 권한 있는 사용자가 생성 가능한 패널의 QR을 발급·확인·저장 또는 인쇄하고, 현장 사용자는 휴대폰 카메라로 스캔해 로그인 후 3회 이내에 현재 담당 업무를 확인하거나 시작한다.

## 2. 확정된 Repository 계약

- 시스템 생성 가능 조건: project Active·not deleted, panel Active, panel name 존재. 생산계획·IQC·현장 부착 여부는 발급 가능 조건이 아니다.
- 현장 부착: 자재팀이 외함 첫 입고 때 Product Tag를 부착하고, 품질팀이 IQC 적합 뒤 그 위에 QR을 부착한다. IQC 불합격이면 현장 부착하지 않는다.
- 한 패널당 QR 하나, 발급 뒤 활성 유지. 완료 후 비활성 정책은 이번 기획에서 기존 계약을 바꾸지 않는 최소안을 권장한다.
- QR에는 업무정보를 직접 넣지 않고 추측 불가능한 임의 token만 넣는다.
- 미로그인 사용자는 Microsoft 365 로그인으로 이동한다. 로그인 후 패널과 사용자의 권한·역할을 확인한다.
- 제조 역할은 제조, 품질 역할은 품질, 물류 역할은 물류, 영업·관리자는 패널 종합현황으로 이동한다. 여러 역할은 활성 역할 또는 최소 역할 선택 UX를 사용한다.
- 모든 활성 사내 사용자는 전체 부서 데이터를 조회할 수 있지만 수정은 해당 업무 담당 권한만 가능하다.
- QR 스캔은 매 동작마다 요구하지 않고 업무 진입·대상 확인 수단으로 사용한다.

## 3. Fable이 권장해야 할 비차단 선택

| 번호 | 결정 대상 | 비교할 경계 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- |
| 1 | 발급 시점과 actor | eligibility 충족 시 자동 발급 vs 권한 있는 사용자의 명시 발급 | Fable 권장안 자동 채택 | No |
| 2 | token과 URL 수명 | 영구 opaque token, rotation·재발급, 완료 후 동작 | Fable 권장안 자동 채택 | No |
| 3 | 생성·출력 UX | 단일 패널 QR, 여러 패널 선택 일괄 다운로드/인쇄, 파일 형식 | Fable 권장안 자동 채택 | No |
| 4 | 미로그인 landing | 데이터 없는 안내 후 로그인, 즉시 로그인 redirect, return URL 보존 | Fable 권장안 자동 채택 | No |
| 5 | 로그인 후 route | 역할별 고정 page, 현재 미완료 stage 우선, 다중 역할 선택 | Fable 권장안 자동 채택 | No |
| 6 | 현장 부착 상태 | 발급과 별도 추적 여부, IQC 부착 책임 표시의 MVP 경계 | Fable 권장안 자동 채택 | No |
| 7 | 분실·오염 복구 | 같은 token 재출력, 보안 사고 시 rotation과 이전 token 폐기 | Fable 권장안 자동 채택 | No |

## 4. 정상·예외·복구 흐름

- 정상 생성: 설계/프로젝트 상세의 QR 가능 패널 확인 → 단일 또는 선택 발급 → QR 이미지/라벨 확인 → 다운로드 또는 인쇄 → 동일 패널 재방문 시 같은 활성 QR 재사용.
- 정상 스캔: `/q/{opaqueToken}` 진입 → 업무 데이터 없는 인증 gate → 로그인 return URL → 서버 token·panel·project·scope 검사 → 역할·현재 workflow에 맞는 landing → 담당 권한이 있으면 기존 화면에서 수정, 아니면 조회 전용.
- 예외: 잘못된 token, 폐기 token, 삭제 project, 비활성 panel, 접근 제한 사용자, 다중 역할, 완료 project, 네트워크 재시도, 동시 발급을 명확히 처리한다.
- 복구: 반복 발급 요청은 한 QR로 수렴하고, 재출력은 새 token을 만들지 않는다. rotation이 필요하면 명시 권한·사유·감사와 이전 token 무효화를 적용하는 최소안을 권장한다.

## 5. Data·보안·감사

- 후보 data: panel QR record, opaque token digest 또는 안전한 lookup representation, status, issued actor/time, rotated/revoked metadata, operation id/version.
- 실제 QR payload에는 public origin의 scan URL과 opaque token만 둔다. 고객사·PJT Code·Title·panel name·내부 UUID를 넣지 않는다.
- anonymous endpoint는 redirect 판단에 필요한 최소 응답만 제공하고 panel 존재·상태·프로젝트 정보를 노출하지 않는다.
- authenticated resolve는 기존 project scope와 role/permission을 서버에서 검사하고, 조회·발급·rotation·다운로드/인쇄 관련 감사를 남긴다.
- 실제 Entra tenant 설정·운영 domain·외부 short URL service는 제외하고 local development 인증 shell로 검증한다.

## 6. UX·모바일·접근성

- PC: 프로젝트 상세의 패널 목록/설계 데이터에 QR 발급 상태와 선택 action, 단일 QR preview와 print/download, 발급 actor/time을 compact하게 배치한다.
- 모바일: PC table을 축소 복제하지 않고 스캔 landing을 최우선으로 한다. 패널 identity·현재 상태·내가 할 일·primary action을 한 화면에 보여 주고 복잡한 관리 action은 숨기거나 후순위로 둔다.
- loading·empty·error·success, 중복 submit 잠금, 키보드/focus, QR 이미지 대체 텍스트, 390px page overflow 0을 포함한다.
- 디자인은 현재 WITHUS 계열 semantic token·공통 component·얇은 divider·절제된 shadow·blue active accent를 재사용한다.

## 7. 포함·제외 범위

### 포함

- eligibility를 재사용한 패널별 실제 QR 발급과 단일 활성 record
- QR SVG/PNG 또는 인쇄 가능한 안전한 생성 형식과 단일/선택 사용자 흐름
- anonymous-safe scan entry, 로그인 return URL, authenticated resolve
- 역할·현재 workflow 기반 landing과 조회/수정 권한 유지
- 동시성·멱등·감사·오류·재출력 최소 계약
- desktop/390px 화면, backend/frontend/migration/E2E 검증

### 제외

- 실제 Microsoft Entra tenant·운영 redirect URI·public domain 배포
- 외부 QR/short URL SaaS, 실제 라벨 프린터·재고 시스템 연동
- QR 스캔만으로 자동 업무 완료·상태 변경
- 프로젝트 완료 후 일괄 폐기 정책 변경, 대규모 라벨 template 관리자
- 대표 repo·`main`·Persistent UAT·push·PR·merge

## 8. 성공 기준

- 같은 패널의 동시·반복 발급이 하나의 활성 QR로 수렴하고 QR에는 opaque token 외 업무정보가 없다.
- 권한 있는 사용자가 단일/선택 패널 QR을 화면에서 확인하고 현장 부착용 파일 또는 인쇄 결과를 얻는다.
- anonymous scan에서 업무정보가 노출되지 않고 로그인 뒤 원래 QR 목적지로 복귀한다.
- 로그인 사용자는 역할과 패널의 현재 workflow에 맞는 landing을 보고, 담당 권한이 없으면 조회 전용을 유지한다.
- invalid·revoked·deleted·inactive·restricted 상태가 서로 구분되는 안전한 안내로 처리된다.
- Backend/Frontend tests, fresh/existing isolated migration, desktop/mobile E2E와 QR decode 검증이 통과한다.

## 9. Fable 확인용 요약

- 문제: 현재는 `QR 생성 가능` 표시만 있고 실제 QR·스캔 진입이 없다.
- 확정 경계: opaque token, 인증 후 데이터 노출, 패널당 하나, 역할별 landing, 쓰기 권한 유지, local experiment only.
- 비차단 결정: 자동/명시 발급, 출력 형식, token rotation, 다중 역할, 현장 부착 상태는 Fable 권장안을 자동 채택한다.
- Fable 판정: `COMPLETED_CONFIRMED`.

## 10. 사용자 확인

- [x] 사용자가 experiment branch의 interview·중간 승인을 생략했다.
- [x] 사용자 요청과 기존 확정 계약만 기록했다.
- [x] 비차단 선택은 Fable 권장안 자동 채택으로 남겼다.
- [x] blocking decision은 0이다.

- `interviewStatus: COMPLETED_CONFIRMED`
- `userConfirmed: true`
- `openBlockingDecisionCount: 0`
- `planningApproved: false`
- `implementationApproved: false`
