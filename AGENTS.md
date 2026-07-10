# AGENTS.md

## 프로젝트 개요

이 프로젝트는 EMI 통합정보시스템입니다.
목표는 프로젝트, 생산관리, 구매, 자재, 제조, 품질, 물류, 영업 정산을 18단계 업무 흐름으로 관리하는 것입니다.

## 기본 원칙

- main 브랜치에 직접 작업하지 않는다.
- Commit, Push, PR, Merge는 명시 요청이 있을 때만 수행한다.
- 기존 UAT DB를 drop/truncate하지 않는다.
- Docker volume을 삭제하지 않는다.
- 기존 migration 0001~현재 main 반영 migration은 수정하지 않는다.
- feature branch에서 새 migration은 추가 가능하다.
- 실제 API key, DB password, 인증서, 개인정보를 commit하지 않는다.
- 테스트용 placeholder만 허용한다.

## 브랜치 규칙

- 기능 개발: feat/<task-id>-<short-name>
- 버그 수정: fix/<task-id>-<short-name>
- 디자인 실험: experiment/<purpose>

## 포트 규칙

- UAT Backend: 5081
- UAT Frontend: 5174
- Full-Stack E2E Backend: 5082
- Full-Stack E2E Frontend: 5175
- Figma 디자인 테스트 Frontend: 5176

## DB 규칙

- 수동 검수 DB: emi_qms_uat_005a
- E2E DB: emi_qms_e2e_* 패턴
- UAT DB는 삭제 금지
- E2E DB는 테스트 후 삭제 가능

## 테스트 규칙

Frontend만 수정한 경우:
- git diff --check
- frontend lint
- frontend typecheck
- frontend unit test
- frontend build

Backend 수정한 경우:
- backend Release build
- backend 전체 test
- 관련 filter test

Migration 수정한 경우:
- migration test
- 기존 DB 적용 test
- 신규 DB 적용 test

게시 전:
- frontend 전체
- backend 전체
- Full-Stack E2E
- seed 격리 A/B/C/D
- UAT DB persistence
- secret/PII scan

## Task 종료 정책

모든 Task는 [Task 종료 및 산출물 정책](docs/12-task-completion-policy.md)을 canonical source로 따른다. 종료 전 5종 산출물 상태, Finding gate, 자동 검증과 사용자 검수 상태의 구분, 개인정보·secret 검사와 Commit/PR/Merge 전 검증을 확인하며, 이 절에는 세부 규칙을 중복 정의하지 않는다.

## 완료 보고 형식

1. 수정 요약
2. 수정한 파일
3. 실행한 테스트
4. 테스트 결과
5. Frontend URL
6. Backend URL
7. 수동 검수 체크리스트
8. 미커밋 변경사항
9. 남은 문제
10. 게시 가능 여부
