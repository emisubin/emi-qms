# 하네스 v2 검증과 전환 결과

소유 기록은 [TASK-GOV-CODEX-002 Change 024](../../tasks/gov-codex-002-change-024.md)다. 문서 표현 정리만이 아니라 활성 지침·모델 기본값·명령 정책·관련 CI 분류를 교체하는 변경이다. 제품 기능이나 운영 환경의 재검증 보고서는 아니다.

## 재구축 범위

- 새 실행 경로: Root → 필요한 영역 지침·현재 Task → 관련 검증 → 위험에 맞는 검토 → 결과 기록·local commit.
- 새 원문으로 교체: Root/하위 지침, 모델·실행 방식, 완료·검증·개인정보 정책, Roadmap/실험 인덱스, 시작 안내·CLAUDE adapter·디자인 승격·작업 양식·보고 정책 안내.
- 보존: 24개 교체 전 원문과 기존 제품 Task·승인·작성 모델·배포 이력. [보존 목록](../archive/harness-v1-2026-09-09/README.md).
- 실제 코드 범위: config/rules 및 하네스 변경의 CI 분류/분류 tests. 제품 코드·migration·dependency·runtime·DB·provider·전역 스킬은 제외.

## 권한·재량 변화

| 넓어진 판단 범위 | 유지한 경계 |
| --- | --- |
| GPT-6 직접 구현, 선택적인 Sol과 강도 선택 | 사용자 지정 모델 준수, 실제 모델 관측 구분 |
| 실질 선행조건을 지키는 사용자 지정 작업 우선 | 업무·권한·운영 선행조건 무시 금지 |
| 명확한 구현 지시로 진행, 중복 인터뷰/승인 제거 | 기획만 요청하면 구현하지 않음. 미확정 제품·권한 결정은 확인 |
| 영향에 맞는 테스트와 작은 변경의 직접 검토 | 권한·DB·동시성·CI 등의 반례와 독립 검토, required CI |
| 필요한 PR·실화면 최소 관찰 | secret/실데이터 원문 보관·외부 게시·운영 mutation 권한 없음 |
| 같은 Task 현재 기록 갱신 | 과거 승인·원문·다른 환경의 실행 권한 이전 금지 |

명시적인 main 병합 승인 횟수는 Root 한 곳에서 유지한다. 전역 승인 정책이나 도구 차단을 낮추지 않았다. 기존 psql prompt는 임의 SQL을 안전한 조회로 판단할 수 없는 prefix 한계 때문에 유지했으며, 일반 shell/PR 조회의 불필요한 project prompt와 구분한다.

## 동작 사례 검토 범위

아래 10개 시나리오는 parent 문서 대조와 작성/구현에서 분리된 reviewer의 내용 검토를 통과했다. 제품을 실제 변경하거나 배포한 시험 결과가 아니라 지침에 따른 행동 판단이다.

| 사례 | 확인할 정책 경계 |
| --- | --- |
| 기존 화면의 행 높이만 수정 요청 | 직접 구현·실제 관련 화면, 불필요한 기획/전체 suite/모델 사슬 없음 |
| 기획만 요청 | 필요한 결정과 계획에서 종료, 제품 구현으로 확대 없음 |
| 부서 이동 후 권한 잔존 수정 | readiness/role provenance의 실제 서버 반례와 독립 review |
| 오산 다음 단계 개발 | 배포한 접근·생성은 재구현하지 않고 진행 Task 계약부터 |
| 완료 실험 + 마지막 사용자 검수 대기 | 개발 완료와 검수를 구분하고 같은 기능 재구현 없음 |
| 맥락 압축 뒤 재개 | 기존 Task·branch/diff·진행 session에서 이어감, 중복 Task/실행 없음 |
| 이미 승인된 local 구현 완료 | 관련 검증·필요 review 뒤 scoped commit, 새 commit 승인 질문 없음 |
| 운영 migration·main 병합·공개배포 | 해당 대상/행동의 승인·실제 검수/CI·복구 경계 유지 |
| 도구 정책 거부 | 확인된 이유·미완료만 보고, 명령 변형/전역 설정으로 우회 없음 |
| 테스트 결과 출력 잘림 | 원본 증거 먼저 확인, 무조건 재실행하거나 보이지 않는 PASS 추정 금지 |

## 실행 증거

| 검사 | 현재 결과·한계 |
| --- | --- |
| parent 설계→활성 문서 대조 | 계약·채택/제외·기록 소유권 및 실제 config/rules/분류 코드 diff 대조 완료 |
| Git whitespace | 구현본과 독립 검토 기준선에서 통과. 결과 기록 후 scoped 최종 검사 적용 |
| 제품 코드·migration·runtime 설정 diff | 해당 제품 경로 변경 없음 확인. 하네스 config·분류 코드와 구분 |
| config | `codex features list` exit 0, multi_agent stable true. 공식 지원 키 대조, 강제 Sol/xhigh 제거·새 기본값 확인 |
| script syntax·정적 검사 | `bash -n` 및 ShellCheck, 대상 script 2개 각각 PASS |
| 변경 분류 | `bash scripts/test-change-scope.sh` → `changeScopeTests=PASS`, 45 assertions. harness-only·제품 혼합·미지정 rules·다른/nested snapshot·기존 계약·rename 반례 포함 |
| 실행 규칙 | `codex execpolicy check`로 명령을 데이터로 평가. 일반 조회/PR 읽기/shell wrapper/legacy runner는 project match 없음, 정상 push·psql·merge는 prompt, 대표 main/force push·volume/dropdb는 forbidden. ref 뒤 force는 generic push prompt이며 prefix 한계로 명시 |
| 문서 참조 | 신규 연결 경로·원문 위치 및 기존 Roadmap backlink 8개를 직접 대조. 자동 전체 링크 검사는 미실행 |
| checksum·보조 검색 | 일부 읽기/저장 보조 명령이 실행 전 policy/never 거부. 아래에 기록 |
| 제품 전체 회귀·브라우저·DB·Azure | 하네스 범위이므로 미실행. 실제 PMS 화면/로그인/배포 성공을 주장하지 않음 |

## 실행환경 제한

이 작업에서 원격 main Roadmap의 별도 파일 저장, archive 전체 SHA256 수집, 일부 정책 키워드 묶음 검색, `git reset --hard`의 개별 execpolicy 평가 요청이 실행 도구의 `approval required by policy, but AskForApproval is set to Never`로 거부됐다. 정확한 내부 판정 규칙은 확인되지 않았다. 다른 명령·도구·변경한 rules로 해당 행동을 다시 실행하지 않았다. reset의 별도 평가는 미실행이며, 다른 성공한 규칙 로딩에서 inline match/not_match 검증이 된 사실과 구분한다.

로컬 원문 24개 복사는 성공했지만 별도 checksum manifest는 없다. 원격 기준은 실제 확인한 고정 SHA·자료로 연결했다. 자동 전체 링크/해시 검사나 도구의 전체 정책 통과를 PASS로 표시하지 않는다. 이 제한은 새로운 하네스가 현재 실행 도구의 정책을 해제했다는 주장을 막으며, 독립적인 허용 범위의 구현·검토는 계속한다.

## 독립 검토와 적용

구현 위임은 `harness_v2_runtime`, 요청 `gpt-5.6-sol/xhigh`, 관측 `NOT_REPORTED`다. 위 script/config/execpolicy 검사는 Sol이 실제 실행했고 parent와 reviewer는 해당 결과를 읽었으며 같은 검사를 반복 실행하지 않았다.

독립 검토는 `governance_audit`, 요청 역할 `gpt-6-astra/high`, 관측 `NOT_REPORTED`다. 새 agent 생성 한도로 작성/구현과 분리된 기존 감사 맥락을 재사용했다. 새 spawn 또는 과거 GO 재사용이 아니다. 구현자 설명을 읽기 전에 네 실행 파일을 직접 읽은 뒤 승인 설계·현재 전체 활성 문서·실제 diff·반례와 전달된 검증 근거를 대조했다.

- 고정 기준: HEAD `86dfd9f3887841723525d1b8a55fbe8cffe0b627`, `fix/task-gov-codex-002-instruction-clarity`, Change024 allowlist의 현재 내용. 검토 중 대상 쓰기 없음.
- 판정: **계약 충족 GO / 구현 품질 GO**, 신규 actionable P0/P1/P2/P3 `0/0/0/0`.
- 검토 한계: 운영·제품 동적 검증, 원문 전수 byte/hash 동일성, 전체 자동 링크 검증, 실제 신규 Codex 실행과 시간/비용 개선은 미검증.
- 원래 CI scripts/e2e 분류 순서 후보는 이번 새 회귀가 아니며 [Roadmap](../00-product-roadmap.md)의 CI 후속에 연결했다. 기존 관찰을 새 Finding으로 재개하거나 제품 CI를 이번 범위에 섞지 않았다.
- 검토 후 수정은 이 결과와 Task의 실제 상태 기록뿐이다. 구현/승인 계약은 동결본과 같으며 전체 제품 테스트·독립 검토를 다시 시작하지 않는다.

분류 테스트는 기존 방식대로 실행 전용 합성 Git fixture를 생성하고 synthetic commit·EXIT cleanup을 했다. 제품 Repository commit이나 공유 runtime·DB·사용자 보류 자원의 정리와 다르다. 기존 사용자 WIP·runner와 전역 스킬은 편집·stage에서 제외했다.

새 설정과 rules는 신뢰된 프로젝트의 새 Codex 실행에서 로드될 수 있다. 이 대화의 실제 모델·도구 권한이 바뀌었다거나 향후 모든 실행이 문제없이 작동한다는 뜻은 아니다. 실제 다음 코딩 작업의 소요 시간·중복 질문·검증 누락은 후속 사용으로 관찰한다. 새 hook·플러그인·영구 메모 시스템·프로젝트 스킬 bundle은 반복 필요가 입증되지 않아 추가하지 않았다.

이번 구현자에게서 개인 스킬의 추가 검토 연쇄를 시작하려는 행동이 관측돼 parent가 중단하고 예정된 통합 검토로 모았다. 추가 reviewer 생성은 한도로 실패했으며 별도 검토 gate로 인정하지 않았다. 새 문구가 모든 모델의 실제 실행을 강제한다는 증거로 과장하지 않는다.
