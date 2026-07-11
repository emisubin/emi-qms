# TASK-GOV-CODEX-001 — Repository 지침·Rules 이관

## 1. 목적

Task 프롬프트에 반복되던 공통 개발 원칙을 Repository 지침, canonical 정책, 개발 검증 문서와 Codex Rules로 분리한다. 신규 Task 문서는 목표·범위·완료 기준과 Task 고유 위험에 집중한다.

## 2. 배경

기존 Root `AGENTS.md`에는 전역 판단, 포트·DB 운영값과 모든 테스트 명령이 함께 있었다. Product Roadmap 27장도 제품 불변조건과 Git·DB·게시 절차를 중복했다. 이 구조는 규칙 변경 시 여러 장문 프롬프트와 문서를 동시에 수정해야 하고 충돌 가능성을 높였다.

## 3. 규칙 분류

| 분류 | Canonical 위치 |
| --- | --- |
| 전역 판단과 작업 방식 | Root `AGENTS.md` |
| Backend/Frontend/Scripts 판단 | 각 하위 `AGENTS.md` |
| Finding·5종 산출물·검수·게시 gate | `docs/12-task-completion-policy.md` |
| 제품 방향·Task 상태·결정 | `docs/00-product-roadmap.md` |
| 변경 유형별 테스트 | `docs/development/validation-matrix.md` |
| 개인정보 안전 증빙 | `docs/development/privacy-safe-evidence.md` |
| 위험 명령 prompt/forbidden | `.codex/rules/project-safety.rules` |
| 신규 기능 기획 | `tasks/_templates/new-feature-planning-template.md` |

## 4. 포함 범위

- Root `AGENTS.md` 전역 원칙 중심 재작성
- backend/frontend/scripts 하위 지침 추가
- 종료 정책의 validation/privacy canonical link 추가
- Roadmap 27장의 개발 절차 중복 제거와 제품 불변조건 유지
- Validation Matrix와 Privacy-safe Evidence 추가
- project-local Codex Rules와 inline match/not_match 추가
- 신규 기능 기획 template 추가
- 새 Codex session instruction-source dry run과 execpolicy test

## 5. 제외 범위

- runtime, application code, migration, dependency와 lockfile 변경
- Persistent UAT, E2E DB와 실행 중 runtime 변경
- 기존 Task 역사 문서의 공통 블록 일괄 rewrite
- user/global Codex config 강제 변경
- merge, runtime/DB 적용과 global Codex 설정의 Repository 포함

## 6. 완료 기준

- Root 지침에 전역 규칙만 남음
- 하위 경로별 지침이 적용됨
- 종료 정책과 Roadmap 역할이 분리됨
- 위험 명령이 prompt 또는 forbidden으로 판정됨
- 신규 기능 template가 공통 규칙을 링크하고 Task 고유 내용에 집중함
- 기존 개발 원칙 누락·문서 link 충돌이 없음
- user validation checklist는 자동 검증과 별도 상태로 관리됨

## 7. 5종 산출물 상태

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | `tasks/repository-instruction-migration-implementation-report.md` | 작성됨 / 자동 검증·사용자 검수 완료 / Draft PR 게시 승인 |
| SOP | `tasks/repository-instruction-migration-sop.md` | 작성됨 / 사용자 검수 완료 |
| User manual | `tasks/repository-instruction-migration-user-manual.md` | 작성됨 / 사용자 검수 완료 |
| Roadmap update | `docs/00-product-roadmap.md` | 구현·자동 검증·사용자 검수 완료 / Draft PR 게시 대상 |
| User validation checklist | 이 문서 8장 | Checklist 작성됨 / 사용자 검수 완료 / 미체크 0 |

## 8. 사용자 검수 체크리스트

- [x] Root `AGENTS.md`가 전역 판단에 집중하는지 확인
- [x] Backend/Frontend/Scripts 지침 위치와 책임이 이해되는지 확인
- [x] 종료 정책과 Roadmap 역할 분리가 이해되는지 확인
- [x] Validation Matrix로 Task별 테스트를 선택할 수 있는지 확인
- [x] Privacy-safe Evidence의 출력 제한에 동의
- [x] Rules의 forbidden/prompt 구분에 동의
- [x] project-local Rules는 trusted project와 새 session/restart가 필요함을 이해
- [x] shell wrapper는 prompt되지만 내부 명령의 semantic 완전 차단을 보장하지 않음을 이해
- [x] 신규 기능 template가 공통 장문 규칙을 반복하지 않는지 확인
- [x] 기존 역사 Task 문서는 변경하지 않은 범위에 동의
- [x] SOP를 반복 실행할 수 있는지 확인
- [x] User manual을 이해할 수 있는지 확인
- [x] Draft PR 게시를 승인하고 merge는 별도 승인임을 확인
