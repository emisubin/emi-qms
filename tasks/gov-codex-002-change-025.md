# TASK-GOV-CODEX-002 Change 025 — v2만 실행 기준으로 사용

- 요청: 2026-09-09 사용자 “작업 지시 역할에서는 모두 제외하고 새로운 하네스만 사용할 수 있도록 해.” 이어서 오산 잔여 개발의 구체적인 안내 요청.
- 같은 목적의 TASK-GOV-CODEX-002 재사용. 기준 HEAD `ee47f6ba4206935760c1fa8e27a47f031cf5cd6f`, 기존 branch와 제품/runner WIP 보존.
- 범위: Root의 하네스 적용 목록과 과거 절차의 비적용을 명시하고 CLAUDE의 legacy 실행 경로를 제외. 제품 계약·실제 승인·운영 안전 경계·전역 설정·스킬 원문·runner 코드는 미변경.
- 완료 조건: 과거 Task의 업무 계약은 읽되 고정 Sol·중복 승인·5종 산출물·옛 runtime 지시를 다시 적용하지 않음. branch 전환에서도 v2 포함을 확인. 상위 실행 제한과 별도 게시 승인은 보존.
- 변경: `AGENTS.md`, `CLAUDE.md`, 본 Change와 canonical Task의 현재 링크. 작은 문서 명확화이므로 직접 diff/참조 확인을 적용하고 별도 모델 위임·제품 회귀·독립 review는 추가하지 않음.
- 확인: 현재 repository의 자동 지침/설정 진입점과 최신 main의 오산 Progress/Dashboard/Validation Task를 직접 대조. 원격 main은 `c3a3c79374babc840dca054bd1a05237a2c49685`. 오산 endpoint는 목록·상세·생성이고 진행/현황 Task는 PLANNED로 유지됨.
- 결과: v2 전용 실행 기준 명확화 완료. Local commit은 이 변경의 Git 이력으로 추적. 사용자 결과 확인 대기, push·PR·merge·배포 미수행. 오산 기능의 구현은 이번 안내와 구분하며 아직 시작하지 않음.
- 다음 제품 작업: 최신 main 제품 코드에 v2를 포함한 안전한 작업 기준을 준비하고 TASK-OSAN-PROGRESS-001 → DASHBOARD-001 → 후속 VALIDATION-001 순으로 진행. 현재 dirty checkout을 임의 전환·정리하지 않음.
