# TASK-PANEL-DESIGN-001 — 설계 도번·필수값·패널 묶음 구현 기획

> 상태: 사용자 구현 지시 확인 / Codex 기획

- taskType: `NEW_FEATURE`
- planningOwner: `CODEX_USER_EXPLICIT_OVERRIDE`
- implementationApproved: true
- publicationApproved: false

## 1. 목표

일반 Item 설계 담당자가 기존 패널정보 입력 화면에서 패널별 도번과 개별 최외곽 W/H/D를 기록하고, 함께 출하할 패널을 묶은 뒤 프로젝트 설계 탭에서 개별 크기와 묶음 W 합계를 한눈에 확인한다.

## 2. 확인된 사용자 요구

- 패널명 오른쪽에 `도번`을 입력한다.
- 사이즈 헤더의 `i` 도움말은 `포장 업무에 필요한 패널의 최외곽 사이즈를 기재해주세요.`를 표시한다.
- 프로젝트마다 설계 필수 입력값을 화면 진입 즉시 확인한다.
- UL891을 제외한 Item에서 여러 패널을 한 묶음으로 지정한다.
- 상세 설계 탭은 묶인 패널을 굵은 일반 테두리로 묶어 표시한다.
- 개별 패널 크기와 묶음 크기를 모두 표시하고 묶음 크기는 W만 더한다.
- 현재 흑백 wireframe과 일반 테두리를 재사용하며 왼쪽 강조선이나 AI식 강조 박스를 추가하지 않는다.

## 3. Codex 구현 결정

1. 패널 checkbox 선택 후 `선택 패널 묶기`로 새 묶음을 만들고 `선택 묶음 해제`로 선택 패널이 속한 묶음 전체를 해제한다.
2. 한 패널은 한 묶음에만 포함하며 활성 패널 기준 묶음은 최소 2면이다.
3. 묶음 번호는 프로젝트 내부 식별값이며 사용자는 `묶음 1`, `묶음 2`처럼 본다.
4. 도번과 묶음은 선택 설계정보다. 기존 프로젝트의 완료 판정을 바꾸지 않도록 설계 완료 필수값에는 추가하지 않는다.
5. 기존 프로젝트는 `도번 없음·묶음 없음`으로 보존하고 자동 묶음이나 소급 추정은 하지 않는다.
6. 묶음 W는 활성 구성 패널의 `WidthMm` 합계다. 하나라도 W가 없으면 숫자를 추정하지 않고 `W 입력 필요`로 표시한다.
7. H/D는 합산·최댓값 처리하지 않고 각 패널의 개별 크기만 표시한다.
8. 취소 패널은 활성 묶음 표시와 W 합산에서 제외한다. 활성 구성원이 1면만 남으면 화면에서는 묶음으로 표시하지 않는다.
9. 도번·패널명·사이즈·묶음 변경은 기존 패널정보 PATCH 한 번과 같은 transaction에서 처리한다. 하나라도 validation/CAS가 실패하면 전체 rollback한다.
10. 기존 `PanelInfo.Update` 권한, 활성 프로젝트/패널 제한, 수정사유와 field audit, `panelInfoVersion` 충돌 계약을 재사용한다.
11. Excel의 기존 `도번` 열을 실제 저장·미리보기·감사 대상으로 전환한다. 묶음 편집은 다중 선택 맥락이 더 명확한 웹 화면에서만 제공한다.

## 4. UX 구조

### 입력 화면

- 1단계 상단에 포장방식별 필수값을 평면 안내로 표시한다.
  - 일반 포장: `필수 — 패널명`, `선택 — 도번·W/H/D·패널 묶음`
  - 목포장: `필수 — 패널명·W/H/D`, `선택 — 도번·패널 묶음`
- 2단계 표: `선택 / No / 패널명 / 도번 / W / H / D / 묶음 / 패널정보 / QR`.
- 사이즈 제목 옆 keyboard-focus 가능한 `i` 도움말을 둔다.
- 체크한 활성 패널이 2면 이상이면 묶기, 기존 묶음 구성원을 선택하면 묶음 해제가 가능하다.
- 묶음 변경은 저장 전 form 상태이며 기존 `직접 입력 저장`으로 함께 확정한다.
- Mobile은 기존 카드 구조에 도번, 묶음 선택·상태를 세로 배치한다.

### 상세 설계 탭

- 표 header에 도번을 추가한다.
- 묶음은 굵은 검은 일반 테두리의 하나의 block으로 표시하고 block 상단에 구성 No와 `묶음 W 합계`를 표시한다.
- 묶음 안에서도 각 패널의 도번·패널명·개별 W/H/D·상태를 유지한다.
- 묶이지 않은 패널은 기존 단일 행/카드 표시를 유지한다.

## 5. Data·API·감사

- additive migration으로 `panel_placeholders.drawing_number`와 `panel_group_number`를 추가한다.
- 도번은 trim 후 최대 200자, 묶음 번호는 양의 정수 또는 null이다.
- 기존 패널정보 request item에 `drawingNumberUpdate`, `groupNumberUpdate` update mask를 추가한다.
- response는 panel별 도번·묶음 번호와 project-level 묶음 요약을 제공한다.
- field audit에 `DrawingNumber`, `PanelGroupNumber`를 추가하고 기존 correlation/input source 계약을 유지한다.
- Excel template의 도번 셀에는 현재 값을 채우고 parser/preview/apply가 도번을 저장한다.
- UL891 프로젝트의 묶음 update는 서버가 한글 validation으로 차단한다.

## 6. 포함·제외

### 포함

- migration, Backend contract/domain/store, Excel template/parser/preview/apply
- Frontend type/API/form/table/card/상세 묶음 표시
- Backend·Frontend·migration·격리 Full-Stack·Desktop/390px 검증
- Implementation report, 사용자 검수 checklist, Roadmap 갱신

### 제외

- UL891 설계·세트·code 변경
- 도번/묶음을 설계 완료 필수값으로 변경
- H/D 합산, 패널 간 간격·프레임·포장 여유 계산
- 제조·품질·물류 상태나 묶음 단위 작업 전환
- 신규 알림·첨부·PDF
- 운영 DB·Azure runtime·commit·push·PR·merge·공개배포

## 7. 검증과 완료 기준

- 도번 직접 입력·수정·비움, Excel 신규/변경, 감사와 409 rollback
- 묶음 생성·재구성·해제, 중복 소속/1면 묶음/UL891 차단, W 합산·W 누락 표시
- 기존 패널명·사이즈·QR·완료 판정·Excel 회귀
- Frontend loading/empty/error/success, 권한/비활성 상태, Desktop/390px overflow와 접근성
- 기존 migration 적용 DB와 fresh DB 모두 통과
- 열린 P0/P1/P2 0, 사용자 검수 서버 제공

## 8. 통합 주의사항

현재 원격 main의 마지막 migration은 `0075`이며 우선순위 1·2 검수본이 `0076`을 사용한다. 단일 통합 계약에 따라 LSE TASK NO migration은 `0076`, 본 Task migration은 `0077`로 확정한다. 이미 공개된 migration은 수정하지 않는다.
