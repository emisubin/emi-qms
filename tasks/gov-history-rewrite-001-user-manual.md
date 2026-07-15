# TASK-GOV-HISTORY-REWRITE-001 User Manual

## 1. 사용자 영향

제품 화면, API, DB와 업무 데이터에는 변경이 없다. 변경 대상은 Git repository history와 GitHub visibility다.

Cached pull-request reference 제거와 별도 public 재개 결정은 완료됐다. Repository는 public이며 default branch `main`에는 pull request를 필수로 하는 active ruleset이 적용돼 있다. 승인 review와 required status check는 1인 개발 속도 정책에 따라 강제하지 않는다.

## 2. 개발자·기여자가 해야 할 일

1. Rewrite 이전 clone에서 push하지 않는다.
2. Rewritten origin으로부터 fresh clone을 사용한다.
3. Old branch를 merge, rebase 또는 cherry-pick하지 않는다.
4. 미게시 변경이 있으면 원 소유자와 검토한 privacy-safe patch만 fresh clone에 다시 적용한다.
5. Push가 quarantine 오류로 거부되면 old clone을 다시 활성화하지 말고 repository owner에게 연락한다.

## 3. 운영자가 해야 할 일

- GitHub Support 완료 결과와 cached reference `REMOVED` 상태를 기록한다.
- Repository의 public 상태와 `main` required-pull-request ruleset을 유지하고, visibility나 보호 정책 변경은 별도 승인을 받는다.
- Encrypted backup을 생성 시점부터 7일 보존한다.
- 별도 승인 없이 backup을 restore·삭제하지 않는다.
- Existing runtime worktree는 별도 runtime handover 전 삭제하지 않는다.

## 4. 제품 사용자가 해야 할 일

N/A. 제품 기능과 Persistent UAT 데이터는 바뀌지 않았으므로 애플리케이션 사용자 조치는 없다.

## 5. 문의해야 하는 상황

- Old clone에서 push가 가능하거나 old commit이 origin에 다시 보이는 경우
- GitHub cached pull-request 화면에서 제거 대상 history가 계속 보이는 경우
- Fresh clone의 current tip tree가 기존 제품 source와 다르게 보이는 경우
- Repository visibility나 `main` required-pull-request ruleset이 승인 없이 변경된 경우
- Backup mode·checksum·decrypt 확인이 실패한 경우

실제 개인정보, commit URL, full commit ID와 raw GitHub metadata를 일반 채널에 붙이지 않는다.

## 6. 사용자 검수 체크리스트

- [x] Repository가 현재 public이며 `main`에 required pull request가 적용됐는지 확인
- [x] Fresh clone 사용 원칙과 old clone push quarantine 이해
- [x] Product runtime·DB 영향 없음 확인
- [x] Support 완료와 cached reference `REMOVED` 확인
- [x] 별도 승인 없는 visibility·main 보호 정책 변경 금지 확인
- [x] Backup 제한 보존과 restore·삭제 별도 승인 확인
- [x] Cached reference 제거와 history closure 뒤 public 재개를 별도로 결정·수행했는지 확인
