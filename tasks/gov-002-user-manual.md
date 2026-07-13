# TASK-GOV-002 User Manual — Repository history 개인정보 정책

## 1. 사용자 기능 영향

제품 화면, 업무 흐름, API와 UAT 데이터에는 변경이 없다. 이 Task는 Repository history 개인정보 처리 정책만 결정한다.

## 2. 결정된 내용

Public Git history에 남은 과거 실제 사용자 이름을 coordinated history rewrite로 제거하기로 했다.

- Current checkout은 이미 비식별화됐다.
- 과거 history는 아직 변경하지 않았다.
- Main만이 아니라 영향이 있는 모든 published branch를 함께 처리한다.
- 실제 실행은 별도 Task와 사용자 승인 뒤 진행한다.

## 3. 현재 해야 할 일

일반 제품 사용자는 할 일이 없다.

Repository 사용자는 후속 rewrite maintenance가 공지되기 전까지 다음을 지킨다.

- 과거 identity 원문을 issue, PR, chat 또는 문서에 복사하지 않는다.
- Old commit의 matching line을 공유하지 않는다.
- 임의 `filter-repo`, force push와 branch 삭제를 하지 않는다.
- Secure backup이나 clone을 임의 배포하지 않는다.

## 4. Rewrite가 시작되면 예상되는 영향

- Commit SHA가 바뀐다.
- 기존 clone과 worktree는 폐기 후 fresh clone이 필요할 수 있다.
- Open branch와 local WIP는 별도 이관 절차가 필요하다.
- CI, deployment와 automation의 checkout을 다시 연결해야 할 수 있다.
- 일부 과거 PR·commit link가 더 이상 유효하지 않을 수 있다.

Maintenance 공지와 명시된 절차가 오기 전에는 기존 clone을 임의로 reset하지 않는다.

## 5. Rewrite의 한계

GitHub의 reachable history를 정리해도 과거에 누군가 만든 clone, archive, screenshot과 개인 backup을 원격에서 회수할 수 없다. Fork count 0도 외부 복제본이 없다는 보장은 아니다.

## 6. 왜 Repository를 삭제하지 않나요?

Repository 삭제·재생성은 issue, PR, Actions, integration와 audit continuity를 크게 손상한다. 현재 Finding은 coordinated all-ref rewrite로 더 작은 범위에서 처리할 수 있다.

## 7. 왜 private 전환만으로 끝내지 않나요?

Private 전환은 향후 익명 접근을 줄이지만 old history를 제거하지 않는다. 필요하면 rewrite 전 containment로 선택할 수 있지만 단독 완료안은 아니다.

## 8. 문제가 발견되면

다음 상황에서는 Repository owner 또는 security owner에게 알린다.

- 다른 개인정보·secret을 발견함
- Old commit 또는 clone이 다시 push됨
- Rewrite 뒤 old history에 접근 가능함
- Fresh clone·CI·branch protection이 정상화되지 않음
- 실제 사용자 이름이 current checkout에 다시 나타남

원문을 알림 메시지에 복사하지 말고 category와 발견 위치의 Repository 내부 path만 전달한다.

## 9. 사용자 검수 체크리스트

- [x] 제품 기능과 UAT 데이터 변경이 없음을 확인
- [x] Current checkout과 past history 상태가 다름을 이해
- [x] Coordinated all-ref rewrite 정책에 동의
- [x] 외부 clone 회수는 보장되지 않음을 이해
- [x] Rewrite·force push가 아직 실행되지 않았음을 확인
- [x] 후속 maintenance와 re-clone 승인이 별도임을 확인
- [x] Raw 개인정보를 재공유하지 않는 원칙에 동의
- [x] PR #41 Ready 전환·squash merge를 승인
