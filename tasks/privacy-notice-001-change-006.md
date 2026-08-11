# TASK-PRIVACY-NOTICE-001 Change 006 — 최종 문안·화면·운영 적용 승인

## 승인과 기준선

- canonicalTaskId: `TASK-PRIVACY-NOTICE-001`
- taskType: `POLICY_DECISION / UAT_RUNTIME`
- instructionChainRead: `true`
- gateStatus: `PASS_REUSE`
- userApproval: 2026-08-11 사용자가 지금까지의 개인정보·이용 안내 화면과 문안 수정 결과를 모두 승인하고 `main` 병합·Azure 공개 배포를 명시했다.
- approvalOwner: 회사가 제공한 담당 부서·연락 창구와 보유 기준을 포함한 현재 사내 서비스 운영 문안의 적용 결정
- mergeApproved: `true` — TASK-AZURE-DEPLOY-001 Change 021의 단일 통합 PR에 한정
- productionReleaseApproved: `true` — 병합된 정확한 최신 `main` SHA에 한정

## 확정 정책

- 개인정보·이용 안내는 로그인한 사내 사용자에게 제공한다. 로그인 전 공개 페이지는 현재 범위에 추가하지 않는다.
- 계정·조직 정보, 업무 파일·기록과 알림·접속·보안 정보의 보유 기준은 회사 결정대로 `사내 규정에 따름`으로 표시한다.
- 기존 Microsoft Teams·메일 사용과 관련 외부 서비스 검토는 회사가 이미 완료한 운영 전제로 유지하고 새 provider·새 개인정보 이전을 추가하지 않는다.
- 프로필 사진은 선택 기능이며, 파일 선택 직전에 비저장형 안내·동의를 표시한다. 기존 사진을 강제 삭제하거나 업무 접근을 제한하지 않고 다음 변경 시 같은 안내를 적용한다.
- 향후 모바일 푸시 가능성은 PWA 이용 안내에만 표시하며 현재 Web Push·브라우저 알림 권한 요청을 구현하지 않는다.

## 기존 Finding resolution

- `PRIV-PLAN-001` P1: `RESOLVED`. 회사가 제공한 보유 기준과 현재 정적 문안의 적용을 최종 승인했다.
- `PRIV-PLAN-004` P2: `RESOLVED`. 로그인 후 전용 route를 현재 운영 범위로 승인했다.
- `PRIV-PLAN-005` P2: `RESOLVED_FOR_CURRENT_SCOPE`. 기존 Teams·메일 외부 서비스 검토 완료와 새 provider·전송 추가 없음이 확인됐고 현재 정적 문안을 승인했다.
- 법령·사내 규정·Microsoft 계약·새 provider·처리 목적이 바뀌면 회사 담당자가 문안을 재검토하는 운영 항목을 P3로 유지한다. 이 승인은 법률 자문이나 영구적 법적 적합성 보증을 뜻하지 않는다.

## 게시 Gate

- 최신 `origin/main` 통합 후보에서 전체 Frontend 회귀와 실제 Full-Stack smoke를 실행한다.
- GitHub `main-pr-only` Ruleset의 Ready PR·필수 `CI Gate`를 통과한 뒤에만 병합한다.
- 병합된 정확한 `main` SHA의 Azure Frontend를 교체하고 공개 health·익명 인증 차단을 확인한다.
