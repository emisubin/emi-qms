# TASK-ATTACHMENT-001 — Codex 내용 Review

## 결론

Fable 1차 기획은 사용자가 요구한 `최초 부적합 근거 → 조치 근거 → 재검사 판정` 흐름을 기존 Pending·IQC·패널 품질 불변조건과 충돌 없이 설계했다. 범용 attachment service로 확대하지 않고 Pending 전용 bounded table을 추가하는 후보 A를 채택한다. 구현을 막는 결정은 없다.

## 유지

1. Pending 전용 `Draft → Confirmed(round)` 사진 lifecycle과 조치 완료 transaction의 원자 확정.
2. JPEG/PNG magic-byte 검사, 장당 5MB, 회차당 5장·15MB, Pending 누적 25장.
3. Draft는 현재 조치 담당자만 추가·삭제하고, Confirmed는 DB trigger로 update/delete를 차단한다.
4. 기존 IQC·패널 품질 report 사진은 복사·수정하지 않고 읽기 전용 참조로 재검사 화면에 표시한다.
5. 사진 0장 조치 완료를 허용해 기존 text-only Pending을 보존한다.
6. content endpoint는 인증·프로젝트 접근 scope·`Pending.Read`를 모두 검증한다.
7. 기존 알림·Teams·메일·PDF·Excel 계약은 변경하지 않는다.

## 추가

1. 확정 전 Draft 사진은 현재 조치 담당자에게만 노출한다. 다른 부서와 품질 담당자는 `Confirmed` 사진만 조회한다. 미확정·삭제 가능 사진이 공식 근거처럼 소비되는 혼동과 불필요한 노출을 막는다.
2. 사진 API는 `operationId + expectedPendingVersion`을 필수로 받고, 성공 시 Pending version을 증가시킨다. 응답은 최신 Pending 상세 또는 새 version을 반환해 연속 촬영의 CAS 충돌을 복구할 수 있게 한다.
3. 사진 reference DTO는 `sourceKind`, `reportId/pendingId`, `photoId`, metadata만 제공하고 binary·내부 storage 경로를 포함하지 않는다. content는 각 권한 검증 endpoint에서만 반환한다.
4. 조치 회차의 설명은 `InProgress → ReinspectionRequested` 전환 history의 reason을 snapshot으로 사진 그룹과 함께 반환한다. 이후 코멘트 변경이 과거 조치 근거를 바꾸지 않게 한다.
5. 업로드의 동일 `operationId` 재시도는 같은 결과를 반환하고, 동일 hash의 별도 operation은 명확한 중복 오류로 차단한다.
6. 재검사 근거 사진 한 장의 content 로딩 실패가 판정 자체를 막지는 않되, 화면에 `사진을 불러오지 못함`을 표시해 누락을 숨기지 않는다.

## 보류

1. 범용 attachment service와 기존 IQC·패널 품질 사진 table 통합.
2. 외부 object storage/CDN, 바이러스 스캐너 연동, 운영 용량 산정과 restore rehearsal.
3. Pending 등록·코멘트 첨부, PDF·문서 파일, Excel/PDF 출력 포함.
4. 알림·외부 provider에 사진을 첨부하거나 URL을 발송하는 기능.

## 제거

1. 다른 부서가 Draft 사진까지 보는 1차 기획의 표시안. Confirmed 근거만 공유한다.
2. 사진 row만으로 조치 사유를 매번 동적으로 조합하는 방식. 회차 확정 시 조치 사유 snapshot을 고정한다.

## 권장 구현 순서

1. additive migration: Pending 조치 사진 table·operation receipt·constraint·immutability trigger.
2. Backend 업로드·Draft 삭제·content 조회와 Pending 상세 회차 projection.
3. 기존 조치 완료 transition에 Draft 사진 원자 확정·회차 snapshot 결합.
4. IQC/패널 품질 재검사 source DTO에 원 부적합 사진·최신 확정 조치 회차를 연결.
5. Pending 상세 입력과 두 재검사 화면을 세로 근거 흐름으로 구성.
6. 권한·MIME 위장·용량·상한·동시성·확정 불변·text-only 회귀를 검증.

## Resolution

- 유지: Fable 후보 A와 상한·보존·backfill 권장안 전부.
- 추가: Draft 비공개, operation/version 계약, reference DTO, 조치 사유 snapshot, 로딩 실패 안내.
- 보류: 운영 storage·restore rehearsal 등 experiment 밖 운영 범위.
- 제거: 다른 부서 Draft 조회.
- openBlockingDecisionCount: `0`
- secondPlanningRecommendation: `PROCEED`
