# TASK-UL891-SET-001 Change 002 — 패널 상세 업무 허브 완성

## 1. Gate와 기준선

- instructionChainRead: `true`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- taskIdentityGate: `PASS_REUSE`
- canonicalTaskId: `TASK-UL891-SET-001`
- roadmapSequenceMatch: `false`
- explicitRoadmapOverrideApproved: `true`
- branch: `experiment/task-home-002-personalized-shell`
- startHead: `83662623e32becfc4f41b085642af747c18f2ac3`
- finalPlanningSource: `docs/41-ul891-panel-set-plan.md` 9.3·15장
- representativeRepoMainChanged: `false`
- persistentUatChanged: `false`
- mainMergeApprovalCount: `0/3`

사용자가 프로젝트 상세와 패널 상세의 조회·입력 방식을 검토한 뒤 최종 권장안 구현을 직접 지시했다. 이 변경은 신규 상태·권한·외부 연동을 추가하지 않고, 이미 확정된 패널 상세 `projection + exact deep link`, 중복 mutation form 금지 계약을 실제 화면에 완성한다.

## 2. 문제와 원인

현재 프로젝트 상세는 부서별 실제 입력 데이터를 조회하고 담당 업무 화면으로 이동할 수 있지만, 패널 상세는 기본 설계값과 UL891 세트 문맥만 보여준다. 제조·품질·물류·키팅처럼 패널 ID를 실행 원자로 사용하는 데이터가 흩어져 있어 사용자가 패널 하나의 전체 이력과 다음 행동을 확인하려면 여러 화면을 직접 오가야 한다.

근본 원인은 최종 기획 9.3의 패널 상세 projection과 정확한 업무 deep link가 구현 보고서에서 세트 문맥 표시까지만 완료된 것이다. 기존 부서 API와 입력 화면은 이미 존재하므로 새로운 저장 API나 중복 form을 만들 이유는 없다.

## 3. 최종 UX 계약

### 3.1 프로젝트 상세

- 현재 9개 탭과 조회 projection을 유지한다.
- 복잡한 입력은 기존 별도 수정·업무 페이지가 authoritative source다.
- 프로젝트 상세에서 저장 form을 팝업으로 복제하지 않는다.
- 저장 후 기존 프로젝트 문맥과 해당 탭으로 돌아가는 계약을 보존한다.

### 3.2 패널 상세

패널 상세를 다음 탭의 패널 중심 업무 허브로 구성한다.

1. `요약`: 프로젝트·세트/인스턴스·패널 상태와 부서별 현재 상태
2. `설계`: 패널명·규격·치수·설계 완료와 설계 수정 진입
3. `자재·키팅`: 프로젝트 공통 구매/입고 안내, 해당 패널 키팅 상태와 정확한 패널 업무 진입
4. `제조`: 제조 상태·4단계·담당/내 업무 상태·최근 event와 정확한 패널 업무 진입
5. `품질`: LQC·OQC·입회검사·FAT의 판정·Pending·evidence 요약과 단계별 정확한 패널 업무 진입
6. `물류`: 해당 패널이 포함된 Packing Unit·출발·납품 이력과 정확한 패널 업무 진입
7. `QR·이력`: 해당 패널의 QR 발급 상태와 QR 관리 진입, 패널 관련 실행 이력 요약

### 3.3 입력 방식

- 짧은 확인·사유 입력만 기존 업무 화면의 dialog/drawer를 사용한다.
- 설계·제조·품질·물류처럼 정보량이 많은 입력은 기존 전체 폭 업무 페이지를 사용한다.
- 패널 상세의 `업무 처리` 버튼은 `projectId + panelId + stage`를 전달해 대상 패널을 바로 연다.
- 권한이 없는 사용자는 같은 데이터를 조회할 수 있지만 버튼 문구는 `업무 화면에서 조회`로 표시한다.
- Backend 권한·상태·동시성 검증은 변경하지 않는다.

### 3.4 프로젝트 공통 데이터 경계

- 구매품목·자재 입고는 현재 패널/BOM 자동 귀속이 없는 프로젝트 공통 데이터다.
- 패널 상세에서 임의로 패널에 귀속시키지 않고 `프로젝트 공통`이라고 표시한다.
- 구매·입고 상세는 프로젝트 구매/자재 탭 또는 기존 자재 업무 화면으로 연결한다.
- 패널에 직접 연결된 키팅·제조·검사·물류·QR만 패널 상세에 필터링한다.

## 4. 구현 범위

- 기존 부서 조회 loader를 재사용해 패널 ID 기준으로 projection을 필터링한다.
- 패널 상세 route에 선택 탭 query를 추가해 새로고침·뒤로가기에서도 문맥을 보존한다.
- 패널별 상태 요약·빈 상태·부분 조회 실패를 명확히 표시한다.
- desktop과 390px 모바일에서 동일한 정보 구조를 사용하되 모바일은 탭과 카드 밀도를 적응형으로 정리한다.
- Frontend 단위 테스트에 패널 탭, 프로젝트 공통 표시, 패널별 필터, exact deep link, 조회전용 문구를 추가한다.

## 5. 제외 범위

- 신규 DB migration·상태·권한·API
- 구매품목/자재의 패널 자동 귀속 또는 BOM
- 패널 상세에 부서별 mutation form 복제
- 신규 알림·내 업무·실제 provider
- 대표 repo·`main`·push·PR·merge·Persistent UAT

## 6. 검증 계획

- Frontend lint, typecheck, unit, production build
- 기존 UL891·프로젝트 상세·부서 업무 회귀 테스트
- synthetic 프로젝트로 desktop·390px 패널 상세 각 탭과 exact deep link 확인
- page-level horizontal overflow 0 확인
- `git diff --check`

## 7. 완료 기준

- 패널 상세에서 패널별 설계·키팅·제조·품질·물류·QR 상태와 이력을 한 화면 체계로 조회한다.
- 담당자는 대상 패널이 미리 선택된 기존 authoritative 업무 화면으로 바로 이동한다.
- 프로젝트 공통 데이터와 패널 직접 데이터가 혼동되지 않는다.
- 중복 mutation form·신규 저장 계약·권한 우회가 없다.
- Open P0/P1/P2가 0이고 자동 검증과 desktop/mobile visual을 통과한다.
