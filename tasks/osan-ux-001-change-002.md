# TASK-OSAN-UX-001 Change 002 — 청주·오산 공통 메뉴 통합

- 사용자 승인: 2026-10-08 청주 왼쪽·상단·모바일 메뉴의 기존 디자인을 제거하고 오산 공통 틀 사용. G2 확정 콘텐츠 구현보다 먼저 진행.
- 기준선: origin/main 9585411, branch `codex/unified-osan-shell`. 기존 checkout WIP와 runtime을 보존하기 위해 별도 worktree 사용.
- 범위: 내부 EMI 로고, 공통 헤더, 데스크톱 하위 메뉴 표현, 오산 모바일 플로팅 메뉴 실제 공용화. 청주 모바일 drawer 제거.
- 보존 계약: 청주·오산 업무 메뉴/권한/route/API/DB 경계 및 오산 QR 기능. 로그인 화면과 업무 콘텐츠 디자인은 범위 밖.
- 검증 계획: 관련 App navigation 및 모바일 공통 메뉴 상호작용 테스트, typecheck/build, 합성 데이터 desktop/390px 직접 확인, 독립 reviewer 검토.
- 상태: 구현·영향 자동 검증·합성 화면 직접 검수·독립 코드 검토 완료. 사용자 검수·원격 반영·병합·배포 미실시.
- 후속: 공통 틀 검수 후 G2 확정 PC/모바일 콘텐츠 구현 재개. 원격 반영·병합·배포는 별도 명시 승인 범위.

## 구현 및 검증 결과

- 오산 floating menu를 SharedMobileNavigation으로 추출하여 양 사업부에서 재사용. 청주 업무별 항목·권한·하위 메뉴·badge·개발 사용자 전환은 기존 목록에서 전달.
- 기존 청주 drawer 컴포넌트와 전용 CSS 74개 규칙 제거. 양 사업부 EMI 내부 로고·모바일 헤더·desktop 하위 메뉴·선택 색상을 공용화. 업무 콘텐츠의 사업부 스타일 조건은 보존.
- 5개 영향 test suite 118개 통과(App, App.navigation, SharedMobileNavigation, OsanUsability, OsanQrScanner). typecheck·제품 build·변경 TS lint 통과. build의 기존 500KB chunk 경고, jsdom scroll/navigation 미지원 안내는 남음.
- 1440×900 PC 양 사업부 직접 확인: sidebar208, header77, logo80×29 동일. 390×844 모바일: header52, logo58×21, left24, floating menu174 동일. 청주 긴 메뉴 높이714로 제한, 하위메뉴 접근·계정 팝업·페이지 가로 넘침0 확인. 부모 선택 hover 배경 rgb(252,235,237) 확인.
- 독립 reviewer 요청 gpt-6-astra/high, 실제 모델 NOT_REPORTED. 선택색 공용화·공백 메뉴 ID·옛 E2E 선택자 3건 보정 후 남은 actionable finding 없음.
- full-stack E2E는 선택자만 갱신했으며 이번에 DB 연결 실행하지 않음. 최종 병합 후보 전체 회귀는 사용자 검수 이후 별도 단계.
- 검수 자원: 이 worktree의 5193 loopback Vite(session69687), 모든 API를 메모리 합성 fixture로 대체한 local-shell-review.html/ts. proxy는 127.0.0.1:9로 지정하여 운영/공유 DB 접근 없음. preview·node_modules 연결·screenshot은 commit 제외.
- 합성 화면 증거: /Users/parksubin/.codex/visualizations/2026/10/08/01a118d1-afc8-7351-9370-0126a196e572/shared-shell/cheongju-desktop.png 및 cheongju-mobile.png. 구현자가 실제 열어 확인. Task 검수용 보관이며 실데이터 없음.


## 청주 왼쪽 메뉴 아이콘 재지정 · 2026-10-08

- 승인: 전체 상위/하위 메뉴별 후보 제안 후 사용자 “맞춰봐”. 기존 공통 틀의 배치·크기·색상 유지. 청주 desktop 메뉴만 적용하며 오산·모바일 및 메뉴 노출/순서/권한/route는 보존한다.
- 방법: 현재 프로젝트가 사용하는 Lucide 선형 SVG 원본을 고정 SHA로 가져와 청주 desktop 전용 아이콘 배정에 연결. 기본 아이콘과 공통 메뉴 컴포넌트는 재사용하며 새 dependency는 추가하지 않는다.
- 완료 조건/검증: 원형 fallback 메뉴의 전용 아이콘, 상하위 의미 구분, 관련 navigation 기존 테스트·타입/lint, 합성 PC 실제 화면 및 모바일/오산 영향 경계 확인.
- 출근 관리는 공식 도형에 사용자+시계 조합이 없어 출근 기록을 뜻하는 클립보드+시계를 사용한다. 입고는 공식 import(받는 함+하향 화살표), 기준정보는 목록 필터를 사용한다.
- 검증: 기존 App.navigation·SharedMobileNavigation 2개 suite 10/10 PASS, 타입 검사 및 변경 TSX lint PASS. 1440×900 청주 PC에서 G2·품질·생산관리·자재·물류·부스바 하위 메뉴 직접 관찰, 390×844 모바일 및 오산 PC 기존 아이콘 유지 확인. browser warn/error 없음. 기준정보/Pending 유형은 합성 계정의 노출 권한이 없어 실제 화면 검증 대상에서 제외했으며 코드 배정은 완료했다.
- 합성 화면 증거: `/Users/parksubin/.codex/visualizations/2026/10/08/01a118d1-afc8-7351-9370-0126a196e572/cheongju-menu-icons/`에 검수용 저장, 직접 열어 확인. 기존 5193 합성 preview 재사용, 실제 API/DB 연결 없음.
- 상태: 구현·영향 검증 완료, 사용자 검수 대기. 변경 범위 로컬 커밋만 수행하며 원격 push/PR/merge/배포 없음. DB 생성/수정/임시 DB/새 미사용 DB 없음.
