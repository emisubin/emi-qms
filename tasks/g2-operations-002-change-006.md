# G2 Change 006 — 확정 월간 생산·출하 화면 구현

- 사용자 승인: 2026-10-08 PC·모바일 콘텐츠 전체 확정 및 공통 틀 선행 구현 후 "시작해". 로컬 구현·검증 승인. 원격 push/PR/merge/배포 없음.
- 기준선: 공통 틀 b709ecf, 현재 codex/unified-osan-shell worktree에서 이어감. 원래 checkout WIP 및 5174 공유 runtime 보존.
- 디자인 기준: 원래 checkout docs/design/mobile-input의 g2-pc-approved.html, g2-mobile-review.html, pattern.css 및 README. 새 시안 없이 확정 레퍼런스 재사용.
- PC: 월 조회 상단 표와 본표 그룹 열 정렬, 숫자/달성률 텍스트, 날짜별 세로 행, 다중 상세 펼침. 비고만 좌측 정렬. 행 클릭 상세, 비고 칸은 행 비고 편집.
- 모바일: 납품/생산 표시 달력, 중앙 B 상세 팝업(생산·수리·불량 및 재고·실사 표), 공통 하단 입력 메뉴/전체 입력 폼. 기본 모바일 폼 본문 세로 스크롤 없음, 입력 접근성을 위해 극단적으로 작은 화면/키보드는 가려지지 않게 보존.
- 데이터 입력 → 계획/실적/실사. 실적만 오전/오후/납품 탭. 오전=생산+수리+공유비고, 오후=생산+수리+불량+공유비고, 납품=납품+공유비고. 계획 비고/실적 비고/행 비고 독립. 기존 값 표시, 비우고 저장은 해당 값 제거, 0은 유효. 별도 삭제 버튼 없음.
- 권한: 생산계획 제조·영업, 납품계획 영업, 계획비고 제조·영업, 생산/수리/불량 실적 제조·품질, 납품실적 영업, 실사·실적비고·행비고 제조·품질·영업. 물류 포함 다른 부서 조회만. 기존 총괄 관리자 예외 보존. UI 및 서버 둘 다 확인.
- 미래: 계획·모든 비고 표시, 실제 수량·달성률·재고 공란. 예상 실적 생성/만료 삭제 로직 제거, 과거 예상 저장 데이터는 실제 실적으로 승격하지 않고 제외. 과거 재고 수식과 DB 경계 보존.
- 계획: 기간/일요일 제외/기존 계획 변경 선택, 일자별 계획 별도 저장. 기존 유효일부터 이어지는 목표 기능은 별도 기존 계약으로 유지.
- 검증: 관련 FE 테스트·타입·빌드, 서버 권한/CAS/일괄원자성/미래 거부/격리 DB, PC·390px·360px 직접 확인, 독립 reviewer. 사용자 검수·전체 최종 회귀·원격·배포 별도 상태.
- 현재: 로컬 구현·집중 자동 검증·독립 검토 완료, 사용자 검수 대기. 원격 반영·운영 migration·배포 미실행. Backend 독립 소유 agent(gpt-5.6-sol/high 요청, 실제 관측 모델 NOT_REPORTED), parent Frontend/UI/Task 소유.

- 추가 승인: DB 생성/임시 DB 정리/미사용 DB 현황을 작업 종료마다 보고하는 Root 규칙을 현재 작업 checkout 및 원래 checkout에 추가. 다른 기존 WIP 보존.

## 구현 및 검증 기록 · 2026-10-08

- 실제 공통 틀은 선행 commit b709ecf를 재사용한다. PC 날짜별 세로 표, 독립적인 여러 상세 행, 오른쪽 돋보기 월 선택, 입력 버튼 너비와 같은 메뉴를 구현했다. 모바일은 확정된 달력과 중앙 B 상세 팝업을 적용했다.
- 공통 모바일 입력 메뉴/폼은 `frontend/src/DataEntryDialog.tsx`와 `data-entry-dialog.css`로 저장했다. 업무 필드와 권한은 `G2OperationEditor`에서 연결한다. 새 별도 디자인을 만들지 않고 기존 승인 시안을 재사용했다.
- 새 일자별 계획·비고와 실적·실사를 workspace API로 연결했다. null 삭제 이력의 버전을 보존하고 0을 값으로 처리한다. 기간 입력은 일요일 제외 및 명시적인 기존 계획 변경 옵션을 사용한다.
- 미래 날짜에는 수량 입력이 없고 비고만 편집할 수 있다. 기존 forecast 생성/만료/예상 구간/점선 및 미래 임시입력을 제거했다. 과거 forecast 저장 행은 보존하되 실제 실적 계산에서 제외한다. 기존 G2 홈·목표 및 과거 재고 계산 계약은 보존한다.
- Backend: 최종 `scripts/e2e-backend-tests.sh -p:NuGetAudit=false --filter "FullyQualifiedName~BusinessSchemaSeparation_|FullyQualifiedName~G2OperationsTests"` 23/23 PASS. audit 관련 17/17 PASS. 신규·기존 schema/청주 경계, 역할별 API allow/deny, 0/null, 미래 차단, 일요일/overwrite, 후속 비고 충돌 시 앞 계획 rollback, 같은 비고 버전 동시 2요청의 1성공/1충돌을 검증했다. 기존 ImageSharp restore audit advisory 때문에 해당 검증 실행에서만 NuGetAudit=false를 사용했다. dependency나 프로젝트 보안 설정은 변경하지 않았다.
- Frontend: G2Pages20/20 + G2OperationsPage11/11 PASS (기존 사용자 응답 지연 반례 포함), TypeScript 검사·변경 경로 ESLint·제품 빌드 PASS. 기존 대형 번들 안내만 있다.
- 독립 reviewer(gpt-6-astra/high 요청, 실제 모델 NOT_REPORTED): P1 없음. P2 두 건(지운 계획의 기간 재입력 누락, 미래 실적 비고 진입 차단)을 보정하고 재검토에서 둘 다 해소, 추가 P1/P2 없음. reviewer는 실제 diff/테스트를 읽었고 중복 suite를 실행하지 않았다.
- 직접 화면 확인: 실제 React/App 공통 틀과 합성 API fixture를 사용한 5193 로컬 브라우저. PC1440×1000에서 상단/본표 그룹 경계 오차 0.02px 이내, 입력 버튼/메뉴 모두110px. 390×844 및360×640에서 달력(6주 달 포함) 페이지 overflow 없음, 상세 팝업/계획/오후 입력폼 스크롤·필드 잘림 없음. 공유 비고 탭 전환·저장 후 조회 성공, 브라우저 console warn/error 없음. 실제 휴대전화 키보드·확대 글씨는 별도 사용자 확인 필요하며 매우 낮은 높이에서는 접근성 스크롤 fallback을 둔다.
- 합성 screenshot은 작업 소유 `~/.codex/visualizations/2026/10/08/01a118d1-afc8-7351-9370-0126a196e572/g2-implementation/`에 저장했다. `desktop-1440.png`, `mobile-calendar-390.png`, `mobile-detail-390.png`, `mobile-plan-360.png`이며 Git에 포함하지 않는다. 합성 preview html/ts와 node_modules symlink도 stage하지 않는다.

## DB 현황

- 새 제품 DB 없음. 기존 청주 DB에 적용할 추가 migration0132만 작성했다. `g2_daily_values`(일별 계획), `g2_daily_notes`(계획/실적/행 비고) 테이블을 추가하고 실사 null 버전 이력을 허용한다. 운영·Persistent UAT에는 적용하지 않았다.
- backend 검증 DB 이름은 `emi_qms_e2e_20261008_`에 다음 suffix를 붙인 작업 소유 임시 DB다: `064424_93901_24488`, `064517_94309_20931`, `064609_94733_7805`, `064655_94992_31680`, `064752_95244_24605`, `064827_95478_32125`, `064935_95758_26384`, `065016_96015_30695`, `065129_96328_9449`, `065246_96695_14553`, `065436_97303_13065`, `065701_98090_10495`, `065844_98503_15541`. 각 실행 종료 시 DB 삭제 확인 및 소유 container/network 정리를 마쳤다. tmpfs라 영구 volume 없음.
- 이번 변경으로 더 이상 사용하지 않게 된 제품 DB 없음. 이전 8·9월 작업의 E2E container/network 일부가 남아 있으나 사용 여부·정리 승인 미확인이므로 불필요하다고 확정하거나 삭제하지 않았다.

## 남은 상태와 검수

- 사용자 검수: PC 월 선택·행 펼침·3종 입력·행 비고, 모바일 달력→상세→입력 메뉴→폼, 실제 담당 부서 계정 확인 대기.
- 전체 제품 회귀와 required CI는 사용자 검수 후 최종 병합 후보 단계. 원격 push/PR/merge·운영 migration·공개배포 미실행이며 이번 승인에 포함하지 않는다. 공유5174/runtime과 원래 checkout의 다른 WIP는 보존했다.

### 연결 테스트 환경 보정

- generic full-stack 첫 실행은 신규 G2 API 최초 조회에서500으로 실패했다. 원인은 single-DB 테스트 startup이 공통 migration까지만 적용하고 청주 전용0132의 계획/비고 테이블을 만들지 않는 점이었다. 실제 청주 target migration을 사용하는 backend23개 결과와 별개의 fixture 준비 문제다.
- `scripts/e2e-backend-server.sh`의 일회용 검증환경에만 canonical0132 적용 단계를 추가했다. dedicated DB/container/tmpfs safety guard, legacy migration 완료, DB명·ledger·identity·base schema 검사 뒤 원본SQL을 단일transaction으로 적용하고 table/index/nullable/permission/audit/ledger 불변을 확인한다. 공통 migration 또는 운영 startup/ledger는 바꾸지 않는다.
- 이 test compatibility adapter는 신규 G2의 legacy 제품 단일DB 지원을 추가한 것이 아니다. 제품 배포 전에는 기존 청주 business target migration0132가 적용되어야 한다. Osan/Directory에는 신규 G2 schema를 추가하지 않는다.
- 첫 full-stack DB `emi_qms_e2e_g2spec_20261008_1558`은 해당 실행에서 생성하고 종료 후 DB/container/network 정리 완료. 재검증 결과와 최종 자원은 아래에 이어 기록한다.

- Full-stack G2 연결 테스트는 최종 `g2spec_20261008_1632` 실행에서 1/1 PASS(20.6초). legacy 실사 삭제의 quantity=null/version3 및 홈 재입력 expectedVersion3 → version4를 실제 API/화면에서 확인했다. 이전 성공 실행1627도1/1 PASS였다. 잘못된 DB명/Compose project는 Docker 접근 전 exit64로 거부됨을 확인했다. adapter 독립 검토에서 P1/P2 없음.
- 마지막 교차 검토에서 legacy 실사 DELETE→재입력의 버전 초기화로 오래된 workspace 요청이 새 값을 덮어쓸 수 있는 P2를 발견했다. 두 실사 DELETE를 null/version 증가/삭제 사용자 기록으로 보정하고 기존 lock·CAS·음수 재고 검사를 유지했다. Home은 null 기록을 표시/삭제 대상으로 보지 않으면서 재입력 버전을 보존한다. 새 양쪽 count 반례를 포함한 G2OperationsTests21/21 PASS, G2Pages20/20 PASS. 보정 후 제품 빌드도 PASS.
- 추가 full-stack 임시 DB: `emi_qms_e2e_g2spec_20261008_1612`, `emi_qms_e2e_g2spec_20261008_1617`, `emi_qms_e2e_g2spec_20261008_1620`, `emi_qms_e2e_g2spec_20261008_1624`, `emi_qms_e2e_g2spec_20261008_1627`, `emi_qms_e2e_g2spec_20261008_1632`. 전부 DB 삭제 및 소유 container/network 제거 완료, 영구 volume 없음.
- ABA 검증 임시 DB: `emi_qms_e2e_20261008072618_5497_30803`(fixture 사용자 표시명 기대 실패 후 보정), `emi_qms_e2e_20261008072725_5820_11375`(21/21 성공), `emi_qms_e2e_20261008072827_6158_29257`(구형 common-only G2 migration fixture4개 실패). 전부 DB/container/network 정리 완료. 마지막 실패는 canonical0132 없는 테스트 준비 구조와 폐기된 forecast 테스트 계약을 정비해 재확인한다.

- 최종 구형 fixture 보정: `PostgreSqlMigrationTests`의 G2 store11개는 격리된 청주 target DB에 canonical0132까지 적용하도록 전환했다. common migration 이력 테스트는 기존 계약을 그대로 검증한다. 옛 forecast2개는 미래 actual 거부, legacy forecast 조회/재고 제외, 날짜 경과 후 forecast row/version 보존, 별도 actual row/version 생성으로 교체했다. 전체 해당 G2 필터12/12 PASS(새 Release build 포함, skip0). 제품 schema/routing의 추가 변경 없음.
- 독립 최종 검토: 실사 ABA P2 해소 확인, migration fixture/forecast 계약 보정에서도 추가 P1/P2 없음. reviewer는 코드/검증 계약을 읽었으며 중복 실행하지 않았다. 마지막 보정 후 G2 API21개·G2 migration12개·FE31개·full-stack1개 통과. 앞선 schema 경계/audit 검증과 화면 검증 근거는 위 기록을 따른다.
- 마지막 검증 DB `emi_qms_e2e_20261008073417_7701_20907` 및 각 테스트의 격리 Directory/청주/오산 DB는 dispose/drop 완료. 해당 container/network 제거 완료, tmpfs 영구 volume 없음. 새 제품 DB 없음, 이번 변경으로 미사용이 된 제품 DB 없음.
- Git: `codex/unified-osan-shell`에서 이 change 범위만 로컬 커밋으로 보존한다. 사용자 검수·원격 push/PR/merge·공개배포는 완료 상태가 아니다. 원래 checkout에는 요청한 DB 보고 규칙 한 줄만 추가했고 다른 WIP를 보존했다.

- 마지막 store fixture 내부 DB는 테스트별 `emi_qms_e2e_{10자리 GUID}_directory`, `_cheongju`, `_osan` 규칙으로 총33개를 생성·정리했다. harness가 GUID 이름을 출력하지 않아 삭제 후 개별 전체 이름은 복원할 수 없다. 마지막 container 자체도 제거되어 잔존 DB는 없다.
