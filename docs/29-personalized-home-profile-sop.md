# TASK-HOME-002 운영 SOP — 개인화 홈·프로필 shell

## 1. 목적과 적용 범위

로그인 사용자의 프로필 사진·부서·이름을 공통 shell에 표시하고, 부서별 Home 핵심 지표와 프로필 사진 저장을 운영한다. 이 문서는 `0042_user_profile_photos.sql`이 적용된 환경에만 사용한다. 현재 experiment 구현에는 적용됐지만 대표 repo·`main`·Persistent UAT에는 아직 적용하지 않았다.

## 2. 배포 전 확인

- Backend·Frontend가 같은 TASK-HOME-002 source를 사용해야 한다.
- migration ledger의 다음 번호가 `0042_user_profile_photos`인지 확인한다.
- 기존 migration 파일을 수정하지 않고 `0042`를 additive로 적용한다.
- 외부 provider 변경은 없으며 별도 credential도 추가하지 않는다.
- 대표 환경 적용은 별도 UAT/runtime handover와 rollback 승인 범위에서 수행한다.

## 3. 정상 동작 확인

1. `/api/me`가 실제 사용자와 유효 사용자 각각의 `departmentName`, `profilePhotoVersion`을 반환하는지 확인한다.
2. 모든 업무 페이지에서 우측 상단에 실제 로그인 사용자의 사진·부서·이름이 표시되는지 확인한다.
3. 프로필 팝업에서 사진 변경·제거와 Microsoft 365 로그아웃이 동작하는지 확인한다. Dev 인증은 실제 세션 로그아웃이 없어 버튼이 비활성화된다.
4. `/api/home/department-metrics`가 현재 유효 사용자의 부서와 권한 범위에 맞는 지표를 최대 3개 반환하는지 확인한다.
5. 모바일에서는 좌상단 메뉴 drawer, 우상단 계정 시트, drawer 하단 개발·검수 사용자 전환을 확인한다.

## 4. 프로필 사진 정책

- 허용 형식: JPEG, PNG
- 최대 크기: 5MB
- 허용 가로·세로: 각각 1~8192px
- 저장 범위: 사용자당 현재 사진 1개
- 감사 범위: `Upload`, `Replace`, `Remove`와 hash·크기·MIME만 기록하며 사진 원문은 감사 테이블에 복제하지 않는다.
- 본인 scope는 실제 로그인 사용자 claim만 사용한다. 관리자 대리 사용자 전환은 다른 사용자의 사진을 읽거나 바꾸지 않는다.

## 5. 장애 확인과 복구

- 사진이 보이지 않으면 `GET /api/me/profile-photo`의 404와 오류를 구분한다. 404는 기본 이니셜 avatar를 사용하는 정상 상태다.
- 잘못된 이미지·용량 초과는 400 validation으로 거부한다. 원문을 로그에 남기지 않는다.
- 부서 지표 하나가 실패하면 Home의 기존 내 업무·프로젝트·알림 widget은 계속 사용할 수 있어야 한다.
- migration 문제는 `0042`를 삭제하거나 수정하지 않고 새 번호의 forward-fix migration으로 복구한다.
- 코드 rollback은 experiment commit의 revert commit을 사용한다. 이미 적용된 schema는 destructive rollback하지 않는다.

## 6. 사용자 삭제와 보존

관리자 사용자 purge 시 프로필 사진과 프로필 감사 이벤트는 같은 transaction에서 cascade 삭제된다. append-only audit trigger는 `AdminScheduledDeletionService`가 설정한 transaction-local purge scope에서만 삭제를 허용한다. 다른 업무 참조가 있으면 기존 정책대로 purge를 보류한다.

## 7. Change 002 — 전 부서 운영 메뉴 조회

- 모든 내부 부서에는 운영 메뉴 11개를 동일 순서로 표시한다. 메뉴를 숨겨 입력 권한을 표현하지 않는다.
- 조회는 `projects.read`와 기존 project access scope를 사용한다. `Project.Read.All`, 판매금액·삭제·감사 조회 권한을 새로 부여하지 않는다.
- 입력은 각 화면의 기존 mutation permission으로 통제한다. UI 비활성화만 신뢰하지 말고 API의 `POST/PUT/PATCH/DELETE` 403을 함께 확인한다.
- `관리자` 메뉴는 사용자·권한·감사 개인정보를 포함하므로 기존 관리자 역할에만 표시한다.
- 운영 점검 시 영업 등 비담당 역할로 자재·IQC 목록 GET이 200인지, 같은 역할의 도착 등록·검사 판정이 403인지 한 쌍으로 확인한다.
