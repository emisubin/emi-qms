# TASK-E2E-FULL-SUITE-001 change-002

## Gate

- instructionChainRead: true
- taskType: P2_REMEDIATION
- canonicalTaskId: TASK-E2E-FULL-SUITE-001
- gateStatus: PASS_REUSE
- purposeIdentity: 실제 담당자 입력 기반 프로젝트 전 수명주기 검수에서 확인된 알림·내 업무·workflow 완료·상세 UX 불일치 보정
- roadmapSequenceMatch: false
- explicitRoadmapOverrideApproved: true
- overrideSource: 사용자가 experiment branch에서 아래 보정안 전체를 즉시 구현하고 전 수명주기를 재검수하도록 명시
- sourceBranch: experiment/task-home-002-personalized-shell
- mainMutationAllowed: false
- remoteMutationAllowed: false
- actualProviderAllowed: false

## 사용자 수정 지시

1. 프로젝트 생성 알림은 관리자와 조회 전용 사용자를 제외한 영업·생산관리·설계·구매·자재·제조·품질·물류 활성 사용자에게 한 번만 전송한다.
2. 프로젝트 생성 직후에는 생산관리 전체가 알림으로 인지하고, 생산관리 팀이 프로젝트 정·부 담당자를 저장한 뒤 생산관리 정담당자 내 업무에서 workflow를 시작한다.
3. 이후 단계 업무 알림은 해당 프로젝트 단계의 정·부 담당자에게만 보낸다. 정담당자는 내 업무를 수행하고 부담당자는 참조 알림을 받는다.
4. 납품 완료 뒤 영업 정·부 담당자에게 인앱 알림을 보내고, 영업 정담당자 내 업무에 다음 작업을 생성한다.
5. 업무 생성과 알림은 같은 work item과 event를 참조하고 고정 idempotency key로 동일 이벤트 중복 생성을 막는다.
6. Pending 담당자가 비어 있으면 프로젝트 담당자와 조치 부서를 기준으로 정담당자를 자동 지정한다. 정담당자 내 업무와 정·부 담당자 인앱 알림을 동시에 생성한다.
7. Pending 알림은 우선순위와 관계없이 Teams 채널과 담당자 메일 delivery 후보를 생성하되, 이 실험에서는 provider를 실제 호출하지 않는다.
8. 자재 도착·IQC·입고 확정·마감은 프로젝트 workflow 완료 근거와 일치시키고 자재 화면에서는 품목별 연속 흐름으로 표시한다.
9. 프로젝트 상세에 영업·생산관리·설계·구매·자재·제조·품질·물류 탭을 모두 제공하고 각 부서 workflow 상태와 전용 업무 진입점을 한 프로젝트 안에서 확인한다.
10. 버튼과 상태 문구는 실제 사용자가 수행하는 행위를 기준으로 통일한다. 품질 단계별 입력 구조는 유지한다.

## 불변조건

- `work_items`는 실제 수행 업무, `notifications`는 참조·주의 환기라는 계약을 유지한다.
- Secondary는 Primary의 자동 대체나 공동 완료자가 아니라 참조·fallback 역할을 유지한다.
- 동일 이벤트는 idempotency key로 한 번만 생성한다.
- 완료 판정은 실제 단계 완료 근거와 workflow 18단계 집계가 일치해야 한다.
- main, origin/main, push, PR, merge, Persistent UAT, 실제 Teams·메일 provider는 변경하지 않는다.

## 검증 계획

- backend 단위·통합 테스트와 frontend build/lint/test
- 개발 runtime에서 영업 담당자가 프로젝트를 생성하고 각 실제 담당자가 입력하는 전 수명주기 Playwright 재검수
- 각 단계의 정·부 담당자 알림, 정담당자 내 업무, Pending Teams·Mail delivery 후보, 최종 18/18 완료를 API와 화면 양쪽에서 확인
- 프로젝트 상세 단계별 스크린샷은 Repository 고정 폴더가 아닌 최종 채팅 첨부용으로만 캡처
