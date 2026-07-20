# TASK-QR-001 — 패널 QR 생성·스캔 랜딩 구현 보고서

## 1. 상태와 기준선

- instructionChainRead: `true`
- taskType: `NEW_FEATURE`
- taskIdentityGate: `PASS_CREATE`
- roadmapSequenceMatch: `true`
- branch: `experiment/task-home-002-personalized-shell`
- startHead: `0b5b40be2b1967ec14a9eab0f05a6f2db4e969b2`
- finalImplementationSource: [Fable 2차 기획](../docs/34-qr-scan-landing-plan.md)
- planningHistory: [1차 기획](qr-001-planning.md), [Codex review](qr-001-review.md), [Change 001](qr-001-change-001.md)
- experimentStatus: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- userValidation: `사용자 검수 대기 — 마지막 일괄 검수`
- Git scope: 이 보고서를 포함한 local experiment commit만 허용. push·PR·merge 미승인, main merge 승인 `0/3`
- runtime scope: isolated PostgreSQL·disposable Full-Stack E2E만 변경. Persistent UAT와 실제 provider 미적용

## 2. 해결한 업무 문제

기존 패널 정보는 이름이 입력되면 `qrEligible`만 계산했으며 실제 QR 발급·출력·스캔 경로는 없었다. 이번 구현은 설계 담당자가 적격 패널에 QR을 명시 발급하고 여러 패널을 선택해 인쇄할 수 있게 했다. 현장 사용자는 QR을 스캔한 뒤 인증을 거쳐 패널의 현재 단계와 본인 수정 가능 여부를 확인하고, 담당 부서·현재 단계·기존 권한이 모두 맞을 때만 현재 업무로 이동한다.

## 3. 포함·제외 범위

포함 범위는 패널당 활성 QR 1개, 256-bit opaque token, SVG/PNG 서버 렌더링, 같은 프로젝트 내 최대 50개 선택 인쇄, 모바일 우선 인증 scan landing, 현재 단계별 업무 route, 관리자 사유 기반 rotation, append-only QR event, migration `0047`과 실제 QR decode 검증이다.

제외 범위는 QR을 인증수단으로 사용하는 기능, 익명 업무 데이터 API, 스캔에 의한 workflow 자동 변경, IQC 뒤 현장 부착 완료 상태, 완료 프로젝트 QR 자동 비활성, 프린터·라벨 template 관리, 외부 short URL/QR provider, 대표 repo·`main`, Persistent UAT, 실제 Entra tenant·provider, push·PR·merge다.

## 4. 전체 아키텍처와 영향

| 영역 | 구현·영향 |
| --- | --- |
| DB/Migration | `0047_panel_qr_codes.sql`: 활성/폐기 QR, panel별 활성 1개 partial unique, token unique, append-only event와 purge 예외 trigger |
| Backend | `PanelQr` namespace의 발급·조회·rotation·이미지·선택 인쇄·resolve endpoint/store/renderer |
| QR payload | configured frontend origin의 `/q/{43자 base64url token}`만 인코딩. 고객·프로젝트·패널 업무정보는 QR에 직접 포함하지 않음 |
| 권한 | 발급은 기존 `PanelInfoUpdate`, rotation은 System Administrator, 조회·render는 기존 project scope, 현재 업무 수정은 기존 부서별 write permission을 재사용 |
| Frontend | 프로젝트 상세 설계 탭의 QR manager·preview·선택 인쇄와 `/q/{token}` 모바일 landing |
| 감사 | `Issued`, `Rotated`, `ImageRendered`, `PrintSheetRendered`, `ResolveSucceeded`와 bounded 유효-token 상태 조회. raw token·scan URL은 event detail에 기록하지 않음 |
| 기존 기능 회귀 | 기존 `qrEligible`, 18단계 workflow, 부서별 업무 route와 System Administrator 업무 입력 비우회 원칙 유지 |

## 5. 핵심 보안·제품 결정

- QR은 인증수단이 아니라 비밀이 아닌 위치 식별자다. 어떤 스캔도 인증 전 업무 데이터를 반환하지 않는다.
- token은 CSPRNG 32바이트를 base64url padding 없이 인코딩한다. QR 재출력을 위해 QR record에만 보존하고 log·audit detail·오류 메시지에는 남기지 않는다.
- 최초 구현의 `GET /api/qr/resolve/{token}`은 ASP.NET request log에 raw token이 노출되는 것을 테스트에서 확인했다. public scan route `/q/{token}`은 유지하되, Backend resolve를 인증된 `POST /api/qr/resolve` JSON body로 바꿔 access log 노출을 제거했다. 이는 2차 기획의 log 위생 불변조건을 지키기 위한 구현상 보안 보정이다.
- 동시에 같은 패널을 발급해도 row lock·partial unique index로 활성 QR 1개에 수렴한다.
- 담당 업무 action은 현재 workflow 단계의 담당 부서, 로그인 사용자 부서, 기존 write permission이 모두 일치할 때만 제공한다. 나머지는 프로젝트 종합현황으로 이동한다.
- rotation은 System Administrator만 사유와 함께 수행하며, 기존 QR 폐기와 신규 QR 발급이 한 transaction에서 이뤄진다.
- 선택 인쇄는 같은 프로젝트의 기발급 활성 QR 1~50개만 허용하고 하나라도 stale이면 전체 실패한다.

## 6. 실제 변경 파일

- DB: `database/migrations/0047_panel_qr_codes.sql`
- Backend: `backend/src/Emi.Qms.Api/PanelQr/*`, `Program.cs`, `Emi.Qms.Api.csproj`
- Backend test: `PanelInformationApiTests.cs`, `PostgreSqlMigrationTests.cs`, test project package
- Frontend: `PanelQrManager.tsx`, `QrScanLandingPage.tsx`, `panelQr.ts`, `api.ts`, `App.tsx`, `styles.css`
- Full-Stack E2E: `frontend/e2e/full-stack/panel-qr.full-stack.spec.ts`
- Planning·governance: `tasks/qr-001-*`, `docs/34-qr-scan-landing-plan.md`, Product Roadmap, experiment ledger
- Visual evidence: [desktop/mobile screenshot 폴더](qr-001-screenshots/)

## 7. 사용자 흐름

### 설계 담당자

1. 프로젝트 상세의 `설계` 탭에서 패널 정보를 입력한다.
2. QR 관리 영역에서 적격 패널의 `QR 발급`을 누른다.
3. `보기`에서 실제 QR을 확인하고 PNG로 저장하거나 다시 인쇄한다.
4. 여러 발급 패널을 checkbox로 선택하고 `선택 QR 인쇄`로 최대 50개를 한 장에 구성한다.

### 현장 담당자

1. 모바일로 QR을 스캔해 `/q/{token}`에 진입한다.
2. 미로그인 상태면 기존 로그인 절차를 거치고 같은 QR 경로로 복귀한다.
3. 패널·프로젝트·현재 단계와 `내 권한`을 한 화면에서 확인한다.
4. 현재 단계의 담당 부서와 본인 부서·write permission이 일치하면 `현재 업무 계속하기`, 아니면 `프로젝트 종합현황 보기`를 사용한다.

### System Administrator

1. QR preview에서 재발급 사유를 입력한다.
2. `폐기 후 새 QR 발급`을 실행하면 기존 QR은 즉시 폐기되고 새 QR이 활성화된다.
3. 폐기된 QR을 스캔하면 프로젝트·패널 정보 없이 `더 이상 사용할 수 없는 QR` 안내만 표시된다.

## 8. 검증 결과

| 검증 | 적용 | 결과 | 근거 |
| --- | --- | --- | --- |
| Backend Release build | Yes | PASS | warning 0, error 0 |
| QR backend focused test | Yes | `3/3` PASS | 발급 수렴·이미지·rotation·인증·stale 인쇄·PNG exact decode |
| Migration test class | Yes | `34/34` PASS | fresh `0001 → 0047`, migration count·최신 파일 계약 |
| Backend 전체 test | Yes | `406/406` PASS | Release 전체 suite |
| Frontend lint | Yes | PASS | 신규 error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend typecheck/build | Yes | PASS | type error 0, production build complete. 기존 bundle-size warning 유지 |
| Frontend unit | Yes | `110/110` PASS | 14 files |
| QR Full-Stack E2E | Yes | `1/1` PASS | isolated PostgreSQL에서 실제 입력·3개 발급·SVG preview·선택 인쇄·390px scan·현재 업무 이동·rotation·폐기 화면 |
| QR decode | Yes | PASS | 생성 PNG를 test-only ZXing decoder로 읽어 configured `/q/{token}`과 정확히 일치 확인 |
| SVG/browser render | Yes | PASS | isolated Playwright에서 서버 SVG를 실제 blob image로 표시 |
| Desktop/390px visual | Yes | PASS | synthetic screenshot 4개 직접 확인 |
| Persistent UAT | No | N/A | 사용자 미승인·실험 경계로 적용 금지 |
| 실제 provider | No | N/A | 외부 QR/provider 호출 없음 |
| CI | No | N/A | push·PR 미승인 local experiment commit |

## 9. Finding gate

| Finding | Severity | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `QR-F-001 TOKEN_ACCESS_LOG` | P1 | `RESOLVED` | path parameter resolve가 raw token을 기본 request log에 남김 | Backend resolve를 인증된 POST body로 변경하고 server log에 이전 token이 없음을 테스트 |
| `QR-F-002 CONCURRENT_ISSUE` | P1 | `RESOLVED` | 동시 발급이 활성 QR을 둘 이상 만들 수 있음 | panel row lock·partial unique index·재조회 수렴 테스트 |
| `QR-F-003 STALE_PRINT` | P2 | `RESOLVED` | 선택 뒤 rotation된 QR이 인쇄 sheet에 섞일 수 있음 | 서버가 동일 project·active 상태를 전부 재검증하고 stale이면 전체 `409` |
| `QR-F-004 REVOKED_IDENTITY` | P2 | `RESOLVED` | 폐기 QR에서 project/panel 정보가 노출될 수 있음 | 폐기 응답·모바일 화면에서 업무 identity 제거 |
| `QR-F-005 RELEASE_BINARY` | 검증 환경 | `RESOLVED` | E2E helper가 Release `--no-build`를 사용해 첫 실행에서 이전 binary의 `404` 발생 | 현재 branch Release를 명시 build한 뒤 isolated E2E 재실행 `1/1` |

Open P0/P1/P2는 `0/0/0`이다.

## 10. 시행착오 및 폐기한 접근

- raw token을 API path에 전달하는 방식은 request logging 위생을 만족하지 못해 폐기했다.
- 첫 E2E는 project 상세의 넓은 범위 `보기` locator가 전체 흐름 버튼을 눌렀다. QR manager 범위의 exact locator로 좁혀 실제 QR preview를 검증했다.
- 첫 Full-Stack runtime은 최신 Release binary가 아니어서 QR route가 `404`였다. source/runtime 불일치를 확인한 뒤 동일 branch Release build로 갱신하고 disposable runtime만 다시 기동했다.
- SVG를 별도 raster decoder로 변환하는 추가 dependency는 도입하지 않았다. PNG payload는 exact decode로 검증하고 SVG는 실제 browser render·header·payload renderer 공통 source로 검증했다.

## 11. SOP — 운영 승격·복구

1. 이 experiment commit을 대표 repo나 Persistent UAT에 직접 복사·적용하지 않는다. 별도 승격/UAT Task와 main merge 분리 승인 3회를 먼저 확인한다.
2. 승인된 승격에서는 운영 scan origin·Entra redirect URI·same-origin `/q/` 복귀 계약을 먼저 확정한다.
3. disposable clone DB에서 기존 최신 migration에서 `0047` 추가 적용과 fresh 적용을 모두 rehearsal한다.
4. Persistent UAT maintenance window에서 migration을 적용하고 readiness, 적격 발급, 인증 scan, role별 current-work routing, rotation을 synthetic project로 확인한다.
5. QR token·scan URL이 application/access/security log와 audit detail에 남지 않는지 환경별 logging 설정까지 재검증한다.
6. DB down migration은 제공하지 않는다. QR을 현장에 출력한 뒤 record를 제거하면 추적 의미가 깨지므로 새 migration 번호의 forward-fix를 사용한다.
7. 잘못 발급한 QR은 record 삭제가 아니라 사유 기반 rotation으로 폐기한다. application rollback 시 DB `0047`은 유지하고 이전 application이 새 table을 무시하도록 한다.

## 12. 사용자 검수 체크리스트

자동 검증 상태는 `완료`, 사용자 직접 검수 상태는 `사용자 검수 대기 — 마지막 일괄 검수`다.

- [x] 설계 담당자 적격 패널 3개 실제 발급·preview 확인
- [x] checkbox 전체 선택과 3개 QR 인쇄 sheet 확인
- [x] 실제 PNG decode payload가 configured scan URL과 정확히 일치 확인
- [x] 생산 담당자 390px landing과 현재 업무 이동 확인
- [x] 관리자 rotation 뒤 이전 QR 폐기 화면 확인
- [x] 익명 resolve 차단·raw token log 부재·stale 인쇄 전체 실패 확인
- [ ] 사용자가 마지막 일괄 검수에서 PC 발급·선택 인쇄 확인
- [ ] 사용자가 마지막 일괄 검수에서 실제 휴대전화 스캔과 정보 밀도 확인
- [ ] 승격을 선택할 경우 별도 UAT Task에서 운영 domain·Entra 복귀·실제 출력물 검증

## 13. 개인정보·secret 검토

Screenshot과 E2E data는 synthetic 프로젝트·패널과 development persona만 사용했다. 실제 사용자·고객·프로젝트 정보, raw QR token 문자열, tenant/client/object id, secret, provider payload는 문서·event·screenshot에 기록하지 않았다. QR bitmap에는 disposable test token이 들어 있지만 텍스트로 노출하지 않고 해당 isolated DB는 E2E 종료 시 제거했다.

## 14. Known issue·잔여 위험·후속

- 실제 스마트폰 카메라·운영 domain·Entra redirect 복귀·물리 라벨 인쇄 품질은 Persistent UAT/운영 승격 범위다.
- 현장 QR 부착 완료 상태, 라벨 template·프린터 연결, 완료 프로젝트 QR 비활성 정책은 이번 Task에서 의도적으로 제외했다.
- Frontend의 기존 Fast Refresh warning 1건과 production bundle-size warning은 신규 QR 결함이 아니며 기존 디자인 housekeeping backlog를 유지한다.
- 다음 이름 있는 experiment 제품 후보는 `TASK-NOTIFY-005` 후속 관리자 preference 감사 조회 UI다.

## 15. 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 |
| SOP | 완료 | 이 문서 `11. SOP` |
| User manual | 완료 | 이 문서 `7. 사용자 흐름` |
| Roadmap update | 완료 | [Product Roadmap](../docs/00-product-roadmap.md), [experiment ledger](../docs/27-experiment-task-ledger.md) |
| User validation checklist | 작성·자동 검증 완료·사용자 검수 대기 | 이 문서 `12. 사용자 검수 체크리스트` |

## 16. 작업·게시·중단·재개 상태

- 작업 현황: QR planning 2-pass, 구현, 자동 검증, privacy-safe screenshot과 종료 산출물 완료.
- Git 게시: local experiment commit만 수행. push·PR·merge 없음.
- 중단 Task: 없음. 대표 repo 승격·Persistent UAT·실제 provider는 시작하지 않은 별도 범위다.
- 재개 조건: 사용자 최종 일괄 검수 실패가 기록되면 `TASK-QR-001`의 다음 change 또는 확인된 bugfix로 재개한다.
- Roadmap next: `TASK-NOTIFY-005` 후속 관리자 preference 감사 조회 UI.

## 17. Claude/Fable 사용량

Fable 1차 기획 전후와 2차 기획 전후의 5시간·주간 전체 모델·주간 Fable 수치는 [Change 001](qr-001-change-001.md)에 기록했다. 구현 종료 projection은 5시간 현재 세션 `27% 사용 / 73% 잔여 / 16:39 KST 초기화`, 주간 전체 모델 `29% 사용 / 71% 잔여 / 07-25 07:59 KST 초기화`, 주간 Fable `57% 사용 / 43% 잔여 / 초기화 parse 불가`다.

Task 종료 시 전용 Fable private session과 transcript는 runner `cleanup`으로 제거했으며 `sessionsRemoved=1`, `transcriptsRemoved=1`, `transcriptsMissing=0`을 확인했다.
