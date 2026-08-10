# TASK-CI-COST-001 — GitHub Actions minute 최적화

## 상태와 승인

- Task type: `P2_REMEDIATION`
- 상태: `LOCAL_IMPLEMENTATION_COMPLETE / AUTOMATED_VALIDATION_COMPLETE / PUBLICATION_APPROVED / GITHUB_VALIDATION_PENDING`
- 조사 결과와 권장 최소안: 사용자 보고 완료
- 구현 승인: 완료
- Roadmap 병렬 진행 승인: 완료
- Git 게시·PR·`main` merge 승인: 2026-08-10 사용자 명시 승인 완료

## 해결할 Finding

`CI-MINUTES-OVERCONSUMPTION-001`은 GitHub-hosted Actions 월 사용량이 90% 경고 뒤 100%에 도달한 상태에서 일반 CI가 다음 비용을 반복하는 P2다.

- 같은 PR의 이전 commit run을 계속 실행한다.
- 문서·Task 증빙 변경에도 Backend·Frontend·Full-Stack을 모두 실행한다.
- PR에서 전체 검증한 merge 결과에 대해 `main` Full-Stack을 다시 실행한다.
- Backend 또는 Frontend가 실패해도 Full-Stack이 동시에 runner를 사용한다.
- pnpm store를 매 job에서 다시 내려받는다.

2026-08-01~2026-08-10 privacy-safe 집계 기준으로 workflow run은 73건, 일반 CI는 62건이며 PR 37건·push 25건·수동 release 11건이다. 최근 일반 CI의 job 합산 billable 추정은 Backend 약 18분, Frontend 약 4분, Full-Stack 약 16분으로 run당 약 38분이다. GitHub billing export가 아닌 run duration 기반 추정이므로 실제 청구 분과 동일하다고 단정하지 않는다.

## 승인된 구현 계약

### 실행 매트릭스

| 변경/이벤트 | 변경 분류 | Backend | Frontend | Full-Stack | CI Gate |
| --- | --- | --- | --- | --- | --- |
| 문서·Task 증빙만 변경한 PR | documentation-only | Skip | Skip | Skip | Run |
| 코드·설정·workflow PR | code | Run | Run | 선행 2개 성공 뒤 Run | Run |
| 문서·Task 증빙만 변경한 `main` push | documentation-only | Skip | Skip | Skip | Run |
| 코드·설정·workflow `main` push | code | Run | Run | PR 결과를 신뢰해 Skip | Run |
| diff 기준 SHA가 없거나 분류가 모호함 | fail-safe | Run | Run | PR이면 Run | Run |

### 비용 절감 장치

1. 같은 PR 번호의 이전 실행은 새 commit이 오면 취소한다. `main` push와 Azure 수동 release는 취소하지 않는다.
2. workflow 자체는 항상 시작하고 내부 분류 job이 문서 전용 여부를 판정한다. top-level `paths-ignore`는 필수 체크가 Pending으로 남을 수 있어 사용하지 않는다.
3. Full-Stack은 `Backend`와 `Frontend`가 모두 성공한 코드 PR에서만 실행한다.
4. `CI Gate`는 `always()`로 실행해 필요한 job의 실패·취소·예상 밖 skip을 하나의 결론으로 집계한다.
5. pnpm content-addressable store만 lockfile hash로 cache한다. `node_modules`는 cache하지 않고 `--frozen-lockfile` 검증을 유지한다.
6. 관찰된 정상 시간보다 충분히 큰 timeout으로 무한 대기·stuck runner 비용만 차단한다.

### 문서 전용 allowlist

다음 파일만 바뀐 경우에만 무거운 검증을 생략한다.

- Markdown 문서
- `docs/`와 `tasks/` 아래의 PNG/JPEG/WebP/GIF/SVG/PDF/XLSX/CSV/JSON 증빙
- `FILE_INVENTORY.txt`
- GitHub issue template

그 외 root 설정, `.github/workflows`, `backend`, `frontend`, `scripts`, `infrastructure`, dependency·lockfile와 알 수 없는 확장자는 모두 코드 변경으로 처리한다. changed file 이름 원문은 Actions summary나 Task 증빙에 출력하지 않고 count와 fixed enum만 기록한다.

## 포함·제외 범위

포함:

- `.github/workflows/ci.yml`
- `TASK-CI-COST-001` identity·계약·implementation report
- Product Roadmap 실행 큐·추적 항목·Decision Log

제외:

- `.github/workflows/azure-pilot-images.yml`
- Azure resource, 배포, DNS, runtime 또는 provider mutation
- Backend·Frontend 제품 코드, API, DB와 migration
- GitHub ruleset·required check 설정 변경
- self-hosted runner 도입 또는 Actions 유료 증액
- Repository visibility·GitHub ruleset 변경과 branch 자동 삭제

## 개발 SOP

1. 기능·설정 변경은 PR에서 `Backend`, `Frontend`, `Full-Stack E2E`, `CI Gate`를 확인한다.
2. 문서 전용 PR은 `Change Classification`과 `CI Gate` 성공을 확인한다.
3. 새 commit으로 이전 PR run이 취소되는 것은 정상이다. 최신 head의 `CI Gate`만 품질 판정에 사용한다.
4. `main`에서는 Backend·Frontend와 `CI Gate`를 확인한다. Full-Stack skip은 PR 검증 중복 제거 정책이다.
5. 분류가 잘못됐다고 의심되면 changed file 원문을 공개 보고에 복사하지 말고 분류 count·fixed enum과 workflow source를 점검한다.
6. 향후 required status check를 설정할 때는 조건부 개별 job 대신 항상 실행되는 `CI Gate`를 사용한다.

## User manual

N/A — 제품 사용자의 화면·업무 방식은 바뀌지 않는다. 개발자·운영자의 확인 방법은 위 `개발 SOP`가 canonical 안내다.

## 사용자 검수 checklist

상태: `자동 검증 완료 / 게시·main merge 승인 완료 / 실제 GitHub 검수 대기`

- [ ] 문서 전용 PR에서 무거운 3개 job이 skip되고 `CI Gate`가 성공한다.
- [ ] 코드 PR에서 Backend·Frontend 성공 뒤 Full-Stack과 `CI Gate`가 성공한다.
- [ ] 같은 PR에 새 commit을 올리면 이전 실행만 취소된다.
- [ ] 코드가 `main`에 merge되면 Backend·Frontend·`CI Gate`가 실행되고 Full-Stack은 skip된다.
- [x] Azure 수동 release workflow의 trigger·승인·concurrency가 변경되지 않았다 — local diff exact 확인.
- [ ] 실제 GitHub Actions 사용량 감소는 최소 1주 관찰 뒤 평가한다.

## Rollback

`.github/workflows/ci.yml`을 이 Task 이전 `origin/main` 버전으로 되돌리면 된다. rollback은 workflow만 대상으로 하며 Azure runtime·DB·제품 데이터 복구가 필요하지 않다.
