# 청주·오산 테이블 소유 확정안

- 상태: **표 소유 범위 사용자 확정 / 운영 미적용.** 사용자는 전체 표 소유안과 다음 열·연결 설계 안내 뒤 2026-09-29 “좋아.”로 동의했다. 청주182개·오산56개 유지/보존 범위를 확정하며, 삭제 실행·구현·배포 승인으로 확대하지 않는다.
- 소유 Task: [기존 조사와 근거](azure-deploy-001-change-031.md). 같은 목적의 표별 상세 명세이며 별도 구현 Task를 시작하지 않는다.
- 최신 실행 조건: 사용자가 “업무 처리와 DB 구조를 분리하면 됨 — 기존 백엔드 1개 유지”를 선택했다. 표/열 분리는 유지하며 [실행 설계 §10 A](azure-deploy-001-change-031-runtime-boundary.md#10-기존-앱-수용량을-기준으로-한-재검토--2026-09-29)의 같은 프로세스 내 업무 모듈 분리를 따른다. 프로그램 전체는 양쪽 접속 정보를 보유한다는 차이를 설명받고 선택한 조건이다. 기존 청주182개/오산56개를 유지하고 183/57 추가표 제안은 이번 범위에서 제외한다.
- 기준: 2026-09-29 운영 `backend--0000068`, 배포 source `02028f2739af3da197a047008f448b3f3e95d230`, 업무 migration0001~0130. 이번에 Azure revision/image 불변을 재확인했다. 행 수는 같은 날 앞선 읽기 전용 전수 조회의 스냅샷이며 실시간 수치가 아니다. 실제 업무 원문/사용자 식별값은 수록하지 않는다.
- 후속 기준정보 승인(2026-09-30): 사용자는 오산의 불필요 권한28개·해당 역할 연결·미사용 부스바 역할1개 제거에 대한 **로컬 파일 작성과 합성 검증**을 명시 승인했다. 아래 원래 행 수는 조사 당시 기록으로 보존하며 새 목표와 구분한다. 운영 적용 승인은 별도다. 상세 승인/결과는 [같은 Task 최신 절](azure-deploy-001-change-031.md#오산-기준정보-정리-승인과-추가-전체-검증--2026-09-30)을 따른다.

## 1. 분리 원칙

- 청주 화면 → 공유 backend 안의 청주 업무 모듈/worker → `emi_qms`만 접근한다. 오산도 오산 업무 모듈/worker → `emi_qms_osan`만 접근한다. 브라우저는 DB에 직접 연결하지 않는다.
- 일반 업무 모듈에는 자기 DB에 고정된 데이터 접근 객체만 전달한다. 사업부 헤더를 바꿔 같은 업무 endpoint가 상대 DB를 선택하거나 상대 업무 모듈/API를 대신 호출하는 경로는 두지 않는다. 공유 프로세스 전체는 양쪽 접속 정보를 보유하며 프로세스별 credential 격리를 보장하는 구조는 아니다.
- 조회·입력·수정·삭제뿐 아니라 파일/사진·내보내기·알림·배경 작업·점검 잠금·migration까지 사업부별 실행 범위를 고정한다. 공통 health는 각 사업부 상태를 구분하며 공유 프로세스의 장애·배포·재시작·자원 경합은 남는다.
- 같은 이름의 공통 표는 **각 DB 안의 독립된 표**다. 서로 조회하거나 동기화하는 공유 업무 DB를 뜻하지 않는다. 같은 코드 일부를 재사용할 수 있어도 모든 테이블/열/번호가 같을 필요는 없다.
- 기존 Directory는 로그인 identity·사업부 소속·총괄 지정과 그 관리 기록에 한정해 유지한다. 현재 총괄 관리자의 양 사업부 권한과 전환 기능을 임의 폐지하지 않는다. 공통 로그인/총괄 관리 모듈은 업무 모듈과 책임을 구분하고 각 사업부의 제한된 사용자관리 인터페이스만 명시 호출한다. 청주 업무 모듈이 오산 DB를 처리하는 예외나 일반 업무 중계로 확대하지 않는다. 이 모듈들은 기존 backend 하나에서 실행한다.

## 2. 표 소유 요약

| 분류 | 서로 다른 표 이름 수 | 청주 DB | 오산 DB |
| --- | ---: | --- | --- |
| 각 사업부에서 독립 사용 | 29 | 유지 | 유지 |
| 청주 소유 | 153 | 유지 | 의존 보정 후 제거 |
| 오산 소유 | 27 | 의존 보정 후 제거 | 25개 사용 + 개인 설정 2개 보존 |
| 합계 | **209** | **182개 유지 / 27개 제거 대상** | **56개 유지·보존 / 153개 제거 대상** |

이 수치는 현재 기능을 보존하면서 상대 사업부 표를 걷어내는 1차 목표다. 182개/56개 모두가 현재 데이터 입력 중이라는 뜻도, 영구적인 최소 표 수라는 뜻도 아니다. 청주의 빈 업무 표도 현재 제공하는 기능에 필요하면 보존한다. 동일 사업부 내부의 미사용 기능 폐지나 큰 테이블 분할/통합은 이번 소유 확정에 섞지 않는다.

오산 소유 27개에는 `osan_` 접두사 표25개 외에 `notice_popup_receipts`, `notice_setting_events`가 포함된다. 이 두 표는 오산 공지 전용 경로에서만 사용된다. 접두사만 보고 공통 표로 분류하지 않는다.

## 3. 지금 제거하면 깨지는 연결과 처리 조건

| 현재 연결 | 표 소유 판단 | 제거 전에 필요한 보정 |
| --- | --- | --- |
| 오산 알림 조회/발송이 work_items·workflow_stages를 조회 | 두 표는 청주 소유 | 오산용 알림 SQL에서 청주 작업/단계 조회 제거. notifications/notification_deliveries의 불필요한 FK/열도 정리 |
| 오산 projects361건 모두 LQC 양식 기본 번호를 가짐 | 검사 양식·생산 제품 유형·청주 단계는 청주 소유 | 오산 projects의 LQC 기본값·NOT NULL·FK·보호 trigger/전용 열 보정. projects 자체와 프로젝트 데이터는 유지 |
| 오산 사용자 수정이 청주 양식 관리자 연결/이력을 조작 | form_template_manager_bindings/form_template_audit_events는 청주 소유 | 사용자 활성·부서·역할·부서장 수정은 유지하면서 청주 양식 관리자 동기화만 제거 |
| 오산에도 generic 개인 알림 설정 API가 열려 있음 | user_notification_preferences 계열3개는 청주 소유 | 오산 UI가 사용하는 오산 전용 설정은 유지. generic API는 오산 전용 처리로 연결하거나 오산에서는 닫고 청주 SQL 참조 제거 |
| 청주 WebPush SQL이 오산 알림/설정 표를 참조 | 해당 표는 오산 소유 | 청주 발송 SQL에서 오산 표 참조 제거. CASE 조건이 거짓이어도 없는 표 이름을 SQL에 남겨두지 않음 |
| 청주 qms_users 신규 등록 trigger가 오산 고객 배정을 생성 | 오산 고객·배정 표는 오산 소유 | 청주의 해당 trigger/함수 제거, 청주 사용자 등록과 감사는 유지 |
| 공통 권한 재설정/AuditStore가 site_access 표를 전제 | site_access2개는 청주 소유 | 오산 grant/revoke·감사 조회 정의에서 해당 의존 제거. 오산 mutation/권한 감사 표는 유지 |
| 공통 migration/ledger/health가 양 DB의 동일 구조/목록 또는 접근을 전제 | identity·migration ledger는 각 DB 소유 | 기존130개 이력 보존, 이후 사업부별 적용/검사. 사업부별 점검/갱신 경로는 고정된 자기 DB만 사용 |

오산에서 제거 대상으로 분류한153개 중19개에는 총159행, 청주에서 제거 대상으로 분류한27개 중4개에는 총41행이 있었다. 기준정보·초기값·자동 생성 상태가 포함되며, **전부 빈 표라는 전제로 삭제하면 안 된다.** 실제 정리 단계에서는 잔존 행의 의미와 기대값, 연결 해제·보존 조건을 다시 확인한다. 이번에는 내용을 삭제하거나 이관하지 않았다.

특별 보존:

- `osan_notification_preference_profiles` 2행과 `osan_notification_preferences` 9행은 현재 전용 코드에서 읽지 않는다. migration0104는 개인 제어 복귀를 위해 원래 설정을 보존한다고 명시한다. 현재 사용 중인 global 설정 2개와 구분해 오산에 보존하고, 과거 개인 설정을 버리는 결정을 이번 구조 분리로 대신하지 않는다.
- `busbar_ecount_employees`는 양쪽0행이고 배포 C#에서 직접 사용처를 찾지 못했다. 청주 부스바 정의이므로 청주에 보존하되 현재 사용 표로 설명하지 않는다. 삭제 여부는 청주 내부 기능 정리에서 별도 판단한다.

## 4. 전체209개 표별 소유 목록

`유지`는 해당 DB에 자체 데이터와 구조를 보존한다는 뜻이다. `제거 대상`은 위 선행 보정과 검증 후 제거할 목표이며 지금 삭제하라는 뜻이 아니다. 각 그룹의 설명은 저장되는 데이터 종류이고, 개별 표에 특이 사항이 있으면 별도 표기했다.

### 양쪽 독립 사용: 계정·권한·프로필 · 9개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `departments` | 해당 사업부의 부서 기준 | 유지 | 유지 | 10 | 10 |
| `permissions` | 해당 사업부의 권한 정의 | 유지 | 유지 | 35 | 35 |
| `qms_users` | 해당 사업부의 사용자 프로필·활성 상태 | 유지 | 유지 | 25 | 39 |
| `role_permissions` | 역할과 권한 연결 | 유지 | 유지 | 111 | 111 |
| `roles` | 해당 사업부의 역할 정의 | 유지 | 유지 | 11 | 11 |
| `user_profile_photo_audit_events` | 프로필 사진 변경 이력 | 유지 | 유지 | 0 | 2 |
| `user_profile_photos` | 사용자 프로필 사진 | 유지 | 유지 | 0 | 0 |
| `user_project_access` | 사용자별 프로젝트 접근 연결. 자기 DB의 사용자와 프로젝트만 연결 | 유지 | 유지 | 0 | 361 |
| `user_roles` | 사용자 역할 및 부여 출처 | 유지 | 유지 | 29 | 43 |

오산 권한 기준정보의 후속 목표는 `permissions` **7개**, `roles` **기존 기본역할10개**, `departments` **기존10개**다. 유지 권한은 `projects.read`, `Project.Read.All`, `Project.Create`, `Project.Update`, `Project.Delete`, `manufacturing.update`, `users.manage`이며 이7개의 기존 `role_permissions` 연결과 모든 `user_roles`를 보존한다. 수정 없는 common130 합성 기준에서는 유지 연결이29개지만 운영 연결 개수를29개로 강제 재설정하지 않는다. 청주의35권한/11역할과 기존 연결은 그대로 둔다. 알 수 없는 권한 정의 또는 삭제할 부스바 역할의 사용자/권한 연결이 발견되면 전환 전체를 중단한다.

### 양쪽 독립 사용: 프로젝트 기본정보 · 1개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `projects` | 프로젝트 기본정보. 양쪽 유지하되 열·기본값·연결 규칙은 사업부별 독립 설계 | 유지 | 유지 | 3 | 361 |

### 양쪽 독립 사용: 공지 · 4개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `notice_attachments` | 공지 첨부 | 유지 | 유지 | 0 | 0 |
| `notice_post_revisions` | 공지 수정 전 버전 | 유지 | 유지 | 6 | 4 |
| `notice_posts` | 공지 본문 | 유지 | 유지 | 5 | 2 |
| `notice_reads` | 공지 읽음 기록 | 유지 | 유지 | 0 | 6 |

### 양쪽 독립 사용: 알림·발송·기기 · 6개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `notification_deliveries` | 메일·푸시 등 발송 상태 | 유지 | 유지 | 157 | 35,650 |
| `notification_delivery_attempts` | 발송 시도·결과 | 유지 | 유지 | 177 | 65,542 |
| `notification_recipients` | 알림 수신 대상·읽음 상태 | 유지 | 유지 | 90 | 25,427 |
| `notifications` | 사업부별 알림 내용 | 유지 | 유지 | 36 | 1,007 |
| `web_push_subscription_events` | 푸시 기기 등록·상태 변경 기록 | 유지 | 유지 | 37 | 8,837 |
| `web_push_subscriptions` | 사업부별 사용자 푸시 기기 등록 | 유지 | 유지 | 7 | 19 |

### 양쪽 독립 사용: 변경·보안·출력 이력 · 5개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `audit_coverage_state` | 변경 이력 기록 시작 기준 | 유지 | 유지 | 1 | 1 |
| `audit_event_changes` | 감사 사건의 변경 항목 | 유지 | 유지 | 5,518 | 67,163 |
| `audit_events` | 업무 변경·인증 등의 감사 사건 | 유지 | 유지 | 436 | 1,216 |
| `authorization_audit_events` | 권한 거부 기록 | 유지 | 유지 | 0 | 0 |
| `data_export_events` | 파일 출력·내보내기 기록 | 유지 | 유지 | 1 | 1 |

### 양쪽 독립 사용: DB 식별·업데이트·점검 · 4개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `deployment_maintenance` | 해당 사업부 점검·배포 상태 | 유지 | 유지 | 1 | 1 |
| `deployment_maintenance_popup_receipts` | 점검 안내 팝업 확인 기록 | 유지 | 유지 | 5 | 15 |
| `qms_database_identity` | 이 DB가 어떤 사업부/종류인지 식별 | 유지 | 유지 | 1 | 1 |
| `schema_migrations` | 이 DB에 적용한 구조 변경 이력 | 유지 | 유지 | 130 | 130 |

### 오산 소유: 고객·고객 담당·승인 게이트 · 5개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `osan_customer_assignment_versions` | 사용자별 고객 배정 설정의 변경 버전 | 제거 대상 | 유지 | 25 | 39 |
| `osan_customer_assignments` | 사용자와 담당 고객 연결 | 제거 대상 | 유지 | 0 | 389 |
| `osan_customers` | 오산 거래처·고객 기준 | 제거 대상 | 유지 | 0 | 16 |
| `osan_gate_configuration` | 오산 승인 게이트 설정과 버전 | 제거 대상 | 유지 | 1 | 1 |
| `osan_gate_departments` | 오산 진행 단계별 승인 담당 부서 | 제거 대상 | 유지 | 14 | 19 |

### 오산 소유: 프로젝트 등록·관리·진행 대상 · 5개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `osan_project_create_operations` | 프로젝트 생성 요청 결과·중복 처리 방지 | 제거 대상 | 유지 | 0 | 361 |
| `osan_project_events` | 프로젝트 생성 등 업무 이벤트 | 제거 대상 | 유지 | 0 | 361 |
| `osan_project_management_history` | 프로젝트 정보 수정·삭제 등 관리 이력 | 제거 대상 | 유지 | 0 | 41 |
| `osan_project_target_steps` | 프로젝트 대상별 7개 진행 단계와 상태 | 제거 대상 | 유지 | 0 | 2,527 |
| `osan_project_targets` | 프로젝트 수량에 따라 생성한 개별 진행 대상 | 제거 대상 | 유지 | 0 | 361 |

### 오산 소유: 진행 기록·문제·요청·사진 · 9개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `osan_photo_edit_requests` | 사진 수정 신청·승인 상태 | 제거 대상 | 유지 | 0 | 45 |
| `osan_photo_revision_files` | 사진 수정본 파일과 관련 기록 | 제거 대상 | 유지 | 0 | 21 |
| `osan_progress_operations` | 진행 처리 요청 결과·중복 처리 방지 | 제거 대상 | 유지 | 0 | 706 |
| `osan_progress_photos` | 진행 사진 파일·등록 정보 | 제거 대상 | 유지 | 0 | 781 |
| `osan_progress_step_photos` | 진행 단계와 사진 연결 | 제거 대상 | 유지 | 0 | 781 |
| `osan_stage_issues` | 공정 이상·조치 상태 | 제거 대상 | 유지 | 0 | 15 |
| `osan_stage_records` | 단계 처리·반려·수정 기록 | 제거 대상 | 유지 | 0 | 814 |
| `osan_stage_work_request_recipients` | 공정 진행 요청의 수신 대상 | 제거 대상 | 유지 | 0 | 13 |
| `osan_stage_work_requests` | 공정 진행 요청 | 제거 대상 | 유지 | 0 | 10 |

### 오산 소유: 오산 알림 이벤트·공통 설정·완료 알림 · 4개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `osan_notification_events` | 오산 알림의 사건 종류·진행 단계 | 제거 대상 | 유지 | 0 | 1,007 |
| `osan_notification_global_preference_profiles` | 오산 전체 알림 설정 버전 | 제거 대상 | 유지 | 1 | 1 |
| `osan_notification_global_preferences` | 오산 사건·단계·메일/푸시별 알림 설정 | 제거 대상 | 유지 | 0 | 2 |
| `osan_project_completion_notifications` | 프로젝트 최초 완료 알림 기록·중복 방지 | 제거 대상 | 유지 | 0 | 56 |

### 오산 소유: 오산 공지 팝업·설정 이력 · 2개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `notice_popup_receipts` | 오산 공지 팝업 표시/확인 기록. 이름에 osan이 없어도 오산 전용 | 제거 대상 | 유지 | 0 | 0 |
| `notice_setting_events` | 오산 공지 고정·팝업 설정 변경 기록 | 제거 대상 | 유지 | 0 | 0 |

### 오산 소유: 현재 비활성인 개인 알림 설정 보존 · 2개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `osan_notification_preference_profiles` | 과거 개인 알림 설정 버전. 현재 비활성, 기존 2행 보존 | 제거 대상 | 보존(현재 비활성) | 0 | 2 |
| `osan_notification_preferences` | 과거 개인별 알림 설정. 현재 비활성, 기존 9행 보존 | 제거 대상 | 보존(현재 비활성) | 0 | 9 |

### 청주 소유: 청주 패널 기본정보·QR · 4개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `panel_information_excel_import_batches` | 청주 패널 기본정보·QR | 유지 | 제거 대상 | 0 | 0 |
| `panel_placeholders` | 청주 패널 기본정보·QR | 유지 | 제거 대상 | 4 | 0 |
| `panel_qr_codes` | 청주 패널 기본정보·QR | 유지 | 제거 대상 | 2 | 0 |
| `panel_qr_events` | 청주 패널 기본정보·QR | 유지 | 제거 대상 | 32 | 0 |

### 청주 소유: 청주 담당자·18단계 업무·업무 알림 · 6개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `project_assignees` | 청주 담당자·18단계 업무·업무 알림 | 유지 | 제거 대상 | 6 | 0 |
| `project_audit_events` | 청주 담당자·18단계 업무·업무 알림 | 유지 | 제거 대상 | 109 | 0 |
| `project_workflow_events` | 청주 업무 단계 변화. 오산 notifications의 잔여 FK 제거 필요 | 유지 | 제거 대상 | 14 | 0 |
| `work_item_escalations` | 청주 담당자·18단계 업무·업무 알림 | 유지 | 제거 대상 | 0 | 0 |
| `work_items` | 청주 작업 할당·요청. 오산 알림 SQL의 잔여 참조 제거 필요 | 유지 | 제거 대상 | 20 | 0 |
| `workflow_stages` | 청주 18단계 기준. 오산 진행 단계와 별개 | 유지 | 제거 대상 | 18 | 18 |

### 청주 소유: 구매·조달·엑셀 반입 · 5개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `procurement_excel_import_batch_projects` | 구매·조달·엑셀 반입 | 유지 | 제거 대상 | 0 | 0 |
| `procurement_excel_import_batches` | 구매·조달·엑셀 반입 | 유지 | 제거 대상 | 0 | 0 |
| `procurement_required_item_template_rows` | 구매·조달·엑셀 반입 | 유지 | 제거 대상 | 0 | 0 |
| `procurement_required_item_templates` | 구매·조달·엑셀 반입 | 유지 | 제거 대상 | 0 | 0 |
| `project_procurement_items` | 구매·조달·엑셀 반입 | 유지 | 제거 대상 | 9 | 0 |

### 청주 소유: 생산 계획·제조 양식·프로젝트별 구성 · 23개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `manufacturing_step_template_items` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 4 | 4 |
| `manufacturing_step_template_versions` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 1 | 1 |
| `manufacturing_step_templates` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 1 | 1 |
| `production_control_manufacturing_items` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 11 | 0 |
| `production_control_manufacturing_templates` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 2 | 0 |
| `production_control_manufacturing_versions` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 2 | 0 |
| `production_control_plan_connections` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 8 | 0 |
| `production_control_plan_items` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 12 | 0 |
| `production_control_plan_templates` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 2 | 0 |
| `production_control_plan_versions` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 2 | 0 |
| `production_plan_template_audit_events` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 0 | 0 |
| `production_plan_template_steps` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 24 | 24 |
| `production_plan_templates` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 6 | 6 |
| `production_planning_excel_import_batches` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 0 | 0 |
| `production_product_types` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 6 | 6 |
| `project_manufacturing_step_snapshots` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 14 | 0 |
| `project_production_plan_connections` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 14 | 0 |
| `project_production_plan_items` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 18 | 0 |
| `project_production_plan_set_default_values` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 0 | 0 |
| `project_production_plan_set_defaults` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 0 | 0 |
| `project_production_plan_set_item_values` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 0 | 0 |
| `project_production_plan_set_scopes` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 0 | 0 |
| `project_production_plans` | 생산 계획·제조 양식·프로젝트별 구성 | 유지 | 제거 대상 | 3 | 0 |

### 청주 소유: 자재 기준·입고·수입검사 · 9개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `material_categories` | 자재 기준·입고·수입검사 | 유지 | 제거 대상 | 6 | 5 |
| `material_category_audit_events` | 자재 기준·입고·수입검사 | 유지 | 제거 대상 | 4 | 0 |
| `material_category_iqc_setting_audit_events` | 자재 기준·입고·수입검사 | 유지 | 제거 대상 | 0 | 0 |
| `material_category_iqc_settings` | 자재 기준·입고·수입검사 | 유지 | 제거 대상 | 6 | 5 |
| `material_iqc_attempts` | 자재 기준·입고·수입검사 | 유지 | 제거 대상 | 1 | 0 |
| `material_iqc_scan_attachments` | 자재 기준·입고·수입검사 | 유지 | 제거 대상 | 0 | 0 |
| `material_iqc_scan_reports` | 자재 기준·입고·수입검사 | 유지 | 제거 대상 | 0 | 0 |
| `material_receipt_events` | 자재 기준·입고·수입검사 | 유지 | 제거 대상 | 7 | 0 |
| `material_receipts` | 자재 기준·입고·수입검사 | 유지 | 제거 대상 | 2 | 0 |

### 청주 소유: 제조 실행·배재·완료 처리 · 9개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `panel_kitting_batches` | 제조 실행·배재·완료 처리 | 유지 | 제거 대상 | 0 | 0 |
| `panel_kitting_completions` | 제조 실행·배재·완료 처리 | 유지 | 제거 대상 | 0 | 0 |
| `panel_manufacturing_assembly_batch_operations` | 제조 실행·배재·완료 처리 | 유지 | 제거 대상 | 0 | 0 |
| `panel_manufacturing_completion_confirmations` | 제조 실행·배재·완료 처리 | 유지 | 제거 대상 | 0 | 0 |
| `panel_manufacturing_events` | 제조 실행·배재·완료 처리 | 유지 | 제거 대상 | 7 | 0 |
| `panel_manufacturing_execution_steps` | 제조 실행·배재·완료 처리 | 유지 | 제거 대상 | 7 | 0 |
| `panel_manufacturing_executions` | 제조 실행·배재·완료 처리 | 유지 | 제거 대상 | 1 | 0 |
| `panel_manufacturing_operations` | 제조 실행·배재·완료 처리 | 유지 | 제거 대상 | 7 | 0 |
| `panel_manufacturing_release_operations` | 제조 실행·배재·완료 처리 | 유지 | 제거 대상 | 1 | 0 |

### 청주 소유: 검사 양식·검사 결과·검사 운영 설정 · 17개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `iqc_report_pdf_artifacts` | 검사 양식·검사 결과·검사 운영 설정 | 유지 | 제거 대상 | 0 | 0 |
| `iqc_report_photos` | 검사 양식·검사 결과·검사 운영 설정 | 유지 | 제거 대상 | 0 | 0 |
| `iqc_report_responses` | 검사 양식·검사 결과·검사 운영 설정 | 유지 | 제거 대상 | 0 | 0 |
| `iqc_report_template_items` | 검사 양식·검사 결과·검사 운영 설정 | 유지 | 제거 대상 | 6 | 6 |
| `iqc_report_template_versions` | 검사 양식·검사 결과·검사 운영 설정 | 유지 | 제거 대상 | 7 | 6 |
| `iqc_report_templates` | 검사 양식·검사 결과·검사 운영 설정 | 유지 | 제거 대상 | 7 | 6 |
| `iqc_reports` | 검사 양식·검사 결과·검사 운영 설정 | 유지 | 제거 대상 | 0 | 0 |
| `lqc_item_setting_audit_events` | 검사 양식·검사 결과·검사 운영 설정 | 유지 | 제거 대상 | 2 | 0 |
| `lqc_item_settings` | 검사 양식·검사 결과·검사 운영 설정 | 유지 | 제거 대상 | 6 | 6 |
| `panel_quality_inspection_attempts` | 검사 양식·검사 결과·검사 운영 설정 | 유지 | 제거 대상 | 0 | 0 |
| `panel_quality_operations` | 검사 양식·검사 결과·검사 운영 설정 | 유지 | 제거 대상 | 0 | 0 |
| `panel_quality_report_pdf_artifacts` | 검사 양식·검사 결과·검사 운영 설정 | 유지 | 제거 대상 | 0 | 0 |
| `panel_quality_report_photos` | 검사 양식·검사 결과·검사 운영 설정 | 유지 | 제거 대상 | 0 | 0 |
| `panel_quality_report_responses` | 검사 양식·검사 결과·검사 운영 설정 | 유지 | 제거 대상 | 0 | 0 |
| `panel_quality_reports` | 검사 양식·검사 결과·검사 운영 설정 | 유지 | 제거 대상 | 0 | 0 |
| `panel_quality_template_items` | 검사 양식·검사 결과·검사 운영 설정 | 유지 | 제거 대상 | 48 | 48 |
| `panel_quality_template_versions` | 청주 검사 양식 버전. 오산 projects의 기본값·필수값·FK 제거 필요 | 유지 | 제거 대상 | 10 | 10 |

### 청주 소유: 펜딩·문제 조치·사진·이력 · 7개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `pending_action_photos` | 펜딩·문제 조치·사진·이력 | 유지 | 제거 대상 | 0 | 0 |
| `pending_comments` | 펜딩·문제 조치·사진·이력 | 유지 | 제거 대상 | 0 | 0 |
| `pending_history` | 펜딩·문제 조치·사진·이력 | 유지 | 제거 대상 | 0 | 0 |
| `pending_issue_type_audit_events` | 펜딩·문제 조치·사진·이력 | 유지 | 제거 대상 | 0 | 0 |
| `pending_issue_type_catalog` | 펜딩·문제 조치·사진·이력 | 유지 | 제거 대상 | 4 | 4 |
| `pending_issues` | 펜딩·문제 조치·사진·이력 | 유지 | 제거 대상 | 0 | 0 |
| `pending_photo_operations` | 펜딩·문제 조치·사진·이력 | 유지 | 제거 대상 | 0 | 0 |

### 청주 소유: 물류·포장·출하·증빙 · 8개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `logistics_batch_panels` | 물류·포장·출하·증빙 | 유지 | 제거 대상 | 0 | 0 |
| `logistics_batch_units` | 물류·포장·출하·증빙 | 유지 | 제거 대상 | 0 | 0 |
| `logistics_batches` | 물류·포장·출하·증빙 | 유지 | 제거 대상 | 0 | 0 |
| `logistics_delivery_results` | 물류·포장·출하·증빙 | 유지 | 제거 대상 | 0 | 0 |
| `logistics_evidence` | 물류·포장·출하·증빙 | 유지 | 제거 대상 | 0 | 0 |
| `logistics_operations` | 물류·포장·출하·증빙 | 유지 | 제거 대상 | 0 | 0 |
| `logistics_packing_unit_panels` | 물류·포장·출하·증빙 | 유지 | 제거 대상 | 0 | 0 |
| `logistics_packing_units` | 물류·포장·출하·증빙 | 유지 | 제거 대상 | 0 | 0 |

### 청주 소유: 매출·정산·청구·월별 관리 · 14개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `sales_billing_request_batches` | 매출·정산·청구·월별 관리 | 유지 | 제거 대상 | 0 | 0 |
| `sales_billing_request_download_events` | 매출·정산·청구·월별 관리 | 유지 | 제거 대상 | 0 | 0 |
| `sales_billing_request_items` | 매출·정산·청구·월별 관리 | 유지 | 제거 대상 | 0 | 0 |
| `sales_billing_request_operations` | 매출·정산·청구·월별 관리 | 유지 | 제거 대상 | 0 | 0 |
| `sales_monthly_billing_confirmations` | 매출·정산·청구·월별 관리 | 유지 | 제거 대상 | 0 | 0 |
| `sales_monthly_billing_ledgers` | 매출·정산·청구·월별 관리 | 유지 | 제거 대상 | 0 | 0 |
| `sales_monthly_billing_operations` | 매출·정산·청구·월별 관리 | 유지 | 제거 대상 | 0 | 0 |
| `sales_monthly_billing_revision_cases` | 매출·정산·청구·월별 관리 | 유지 | 제거 대상 | 0 | 0 |
| `sales_monthly_billing_revision_panels` | 매출·정산·청구·월별 관리 | 유지 | 제거 대상 | 0 | 0 |
| `sales_monthly_billing_revisions` | 매출·정산·청구·월별 관리 | 유지 | 제거 대상 | 0 | 0 |
| `sales_monthly_target_audit_events` | 매출·정산·청구·월별 관리 | 유지 | 제거 대상 | 0 | 0 |
| `sales_monthly_targets` | 매출·정산·청구·월별 관리 | 유지 | 제거 대상 | 0 | 0 |
| `sales_settlement_operations` | 매출·정산·청구·월별 관리 | 유지 | 제거 대상 | 0 | 0 |
| `sales_settlements` | 매출·정산·청구·월별 관리 | 유지 | 제거 대상 | 0 | 0 |

### 청주 소유: UL891 세트 구성·버전·복구 · 8개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `ul891_recovery_case_events` | UL891 세트 구성·버전·복구 | 유지 | 제거 대상 | 0 | 0 |
| `ul891_recovery_cases` | UL891 세트 구성·버전·복구 | 유지 | 제거 대상 | 0 | 0 |
| `ul891_set_design_slots` | UL891 세트 구성·버전·복구 | 유지 | 제거 대상 | 0 | 0 |
| `ul891_set_instances` | UL891 세트 구성·버전·복구 | 유지 | 제거 대상 | 0 | 0 |
| `ul891_set_operations` | UL891 세트 구성·버전·복구 | 유지 | 제거 대상 | 0 | 0 |
| `ul891_set_spec_components` | UL891 세트 구성·버전·복구 | 유지 | 제거 대상 | 0 | 0 |
| `ul891_set_spec_versions` | UL891 세트 구성·버전·복구 | 유지 | 제거 대상 | 0 | 0 |
| `ul891_set_specs` | UL891 세트 구성·버전·복구 | 유지 | 제거 대상 | 0 | 0 |

### 청주 소유: G2 실적·재고·목표 · 4개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `g2_daily_metrics` | G2 실적·재고·목표 | 유지 | 제거 대상 | 246 | 0 |
| `g2_defect_inventory_counts` | G2 실적·재고·목표 | 유지 | 제거 대상 | 0 | 0 |
| `g2_inventory_counts` | G2 실적·재고·목표 | 유지 | 제거 대상 | 4 | 0 |
| `g2_targets` | G2 실적·재고·목표 | 유지 | 제거 대상 | 17 | 0 |

### 청주 소유: 인테리어 부스바·재고·출하·이카운트 · 29개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `busbar_audit` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 107 | 0 |
| `busbar_bom_lines` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 3 | 0 |
| `busbar_boms` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 3 | 0 |
| `busbar_detached_pages` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 1 | 0 |
| `busbar_ecount_attempts` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 12 | 0 |
| `busbar_ecount_employees` | 부스바 사용자↔이카운트 사원 연결용 정의. 0행·현재 앱 사용처 미발견, 청주 소유로 보존/별도 정리 판단 | 유지 | 제거 대상 | 0 | 0 |
| `busbar_ecount_jobs` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 10 | 0 |
| `busbar_ecount_runtime` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 1 | 1 |
| `busbar_label_events` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 0 | 0 |
| `busbar_label_requests` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 0 | 0 |
| `busbar_ledger` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 27 | 0 |
| `busbar_master_access` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 0 | 0 |
| `busbar_materials` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 2 | 0 |
| `busbar_operations` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 17 | 0 |
| `busbar_photo_history` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 15 | 0 |
| `busbar_photos` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 14 | 0 |
| `busbar_plans` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 5 | 0 |
| `busbar_product_families` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 4 | 0 |
| `busbar_product_qr` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 13 | 0 |
| `busbar_products` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 52 | 0 |
| `busbar_projects` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 6 | 0 |
| `busbar_publication_recovery` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 0 | 0 |
| `busbar_purchases` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 0 | 0 |
| `busbar_receipts` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 0 | 0 |
| `busbar_settings` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 1 | 1 |
| `busbar_shipment_products` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 4 | 0 |
| `busbar_shipments` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 4 | 0 |
| `busbar_stock` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 4 | 0 |
| `busbar_workers` | 인테리어 부스바·재고·출하·이카운트 | 유지 | 제거 대상 | 2 | 0 |

### 청주 소유: 청주 양식 관리자·변경 이력 · 2개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `form_template_audit_events` | 청주 양식·관리자 변경 이력. 오산 사용자 수정의 잔여 참조 제거 필요 | 유지 | 제거 대상 | 17 | 0 |
| `form_template_manager_bindings` | 청주 양식 관리자 연결. 오산 사용자 수정의 잔여 참조 제거 필요 | 유지 | 제거 대상 | 5 | 0 |

### 청주 소유: 청주 개인 알림 설정·관리자 재발송 · 4개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `notification_delivery_reprocess_events` | 청주 관리자 수동 재발송 이력. 자동 재시도는 별도 attempts에 유지 | 유지 | 제거 대상 | 2 | 0 |
| `user_notification_preference_audit_events` | 청주 개인 알림 설정·관리자 재발송 | 유지 | 제거 대상 | 0 | 0 |
| `user_notification_preference_profiles` | 청주 개인 알림 설정·관리자 재발송 | 유지 | 제거 대상 | 0 | 0 |
| `user_notification_preferences` | 청주 개인 알림 설정·관리자 재발송 | 유지 | 제거 대상 | 0 | 0 |

### 청주 소유: 청주 사이트 접속 기록 · 2개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `site_access_coverage_state` | 청주 사이트 접속 기록 시작 기준. 오산 권한/감사 설정의 잔여 참조 제거 필요 | 유지 | 제거 대상 | 1 | 1 |
| `site_access_sessions` | 청주 사이트 접속 세션. 오산 화면은 수집하지 않음 | 유지 | 제거 대상 | 536 | 0 |

### 청주 소유: 청주 관리 기준 변경·업무일 달력 · 2개

| 표 | 용도/주의점 | 청주 | 오산 | 청주 행 수 | 오산 행 수 |
| --- | --- | --- | --- | ---: | ---: |
| `admin_master_change_logs` | 청주 관리자 기준정보·사용자 삭제/복구 기록 | 유지 | 제거 대상 | 0 | 0 |
| `system_holidays` | 청주 업무일·휴일 달력 | 유지 | 제거 대상 | 0 | 0 |

## 5. Directory와 실행 경계

현재 배포 migration 정의의 Directory 표8개는 업무 DB209개 집계와 별도다. 이번 단계에서 이 DB의 업무 범위를 확대하거나 테이블을 없애지 않는다. 아래는 배포 코드 기준 목록이며 이번 요청에서 Directory 행 수를 새로 조회하지 않았다.

| 표 | 소유 데이터 |
| --- | --- |
| `directory_identities` | 로그인 identity와 공통 식별 정보 |
| `directory_business_units` | 사업부 목록 |
| `directory_business_unit_memberships` | 사용자의 사업부 소속 |
| `directory_overall_administrators` | 총괄 지정 |
| `directory_membership_audit_events` | 소속·총괄 변경 감사 |
| `directory_user_access_operations` | 소속/프로필 변경 작업의 진행·복구 상태 |
| `qms_database_identity` | Directory DB 식별 |
| `schema_migrations` | Directory 자체 구조 변경 이력 |

현재 구조는 하나의 운영 backend에 Directory/청주/오산 runtime connection이 모두 설정되어 있다. 최신 사용자 선택에 따라 이 실행 자원과 프로세스는 유지한다. 그 안에서 청주/오산 업무 모듈과 고정 데이터 접근 객체를 나눠 다음 경로를 보정한다. 별도 프로그램의 자격증명 격리로 표현하지 않는다.

- 통합 사용자 관리: BusinessUnitAccessAdministrationStore.GetSnapshotAsync가 양쪽 local profile을 읽고 UpdateAccessAsync가 영향받은 사업부 local profile을 처리한다. 같은 프로세스의 공통 관리 모듈이 각 사업부의 제한된 사용자관리 인터페이스를 호출하게 한다. 청주 업무 모듈이 오산 프로필을 직접 관리하거나 오산 업무 모듈을 호출하는 경로는 두지 않는다.
- health: DatabaseHealthChecker가 AllTargets를 순회한다. 사업부별 DB 상태를 따로 검사하고 공통 상태 점검에서 구분해 표시한다. 한쪽 DB 실패 때문에 공유 앱 전체를 준비 실패로 처리해 다른 업무까지 차단하지 않도록 보정한다. 공통 로그인 의존성과 프로세스 자체의 장애는 여전히 공유한다.
- 알림/유지보수: worker의 전체 사업부 순회와 DeploymentMaintenanceLease의 양 DB 잠금을 자기 사업부 한 곳으로 제한한다. 한쪽 점검이 다른 업무 DB를 잠그게 하지 않는다.
- migration/bootstrap/운영 관리: 사업부별 credential과 대상·실행을 분리한다. 공통 운영 도구가 명시적 대상으로 두 작업을 각각 실행할 수는 있어도, 청주 대상 작업이 오산 DB를 열어서는 안 된다. 공유 backend 시작 시 자동으로 구조를 변경하지 않고 운영 도구에서 대상 DB 식별과 계정을 확인한 뒤 해당 schema만 적용한다.
- 프론트엔드: 기존 frontend/backend 배포 구성을 유지하며 청주/오산 API 경로와 화면 실행 범위를 고정한다. 기존 총괄의 명시적 사업부 전환 시 이전 데이터·대기 요청을 폐기하고 해당 사업부 화면으로 이동하는 권한 계약은 유지한다. 화면·소스 코드를 모두 복사할 필요는 없다.

이것은 현재 일반 업무 요청에서 상대 사업부 데이터가 유출되었다는 조사 결과가 아니다. 현재 코드에서 확인한 다중 DB 접근 경로와 모듈별 분리 원칙 사이의 차이를 명시한 것이다. 계정별 상대 DB 접속 거부·헤더 변경 거부·동시 요청·worker·migration의 대상 고정 시험은 아직 실행하지 않았다.

## 6. 근거와 남은 검증

배포 commit 기준 주요 근거 경로:

- `DatabaseConnectionStringProvider.cs:33,51` 및 `BusinessUnits/BusinessUnitAccessAdministrationStore.cs:89,195,237`: 요청/명시 target 연결과 통합 사용자 관리.
- `DatabaseHealthChecker.cs:19`, `DatabaseMigrationRunner.cs:75,113`, `ReviewSafe/DatabaseMigrationCatalog.cs:58`: 전체 DB 점검 및 공통 migration.
- `Workflow/WorkflowStore.cs:904,933`, `Notifications/NotificationDeliveryStore.cs:541,2113,2143,2185,2226`: 양쪽 알림의 잔여 표 의존.
- `Identity/UserAdministrationStore.cs:207,544`: 청주 양식 관리자 동기화 잔존. `DatabaseRuntimePrivilegeManager.cs:160`: site_access 권한 설정 의존.
- `Notices/OsanNoticeExtensions.cs:39,70,84,97`: 이름이 공통처럼 보여도 오산 전용인 공지 표2개.
- `frontend/src/App.tsx:1979,2067,3237`: 오산 사이트 접속 수집/청주 개인 설정 화면 제외. `BusinessUnitCapabilityMiddleware.cs:81`: generic 개인 설정 API는 아직 오산에서도 열려 있으므로 별도 보정 필요.
- `database/migrations/0070_lqc_operating_suspension.sql:183`, `OsanProjects/OsanProjectStore.cs:1001`: 오산 프로젝트의 청주 LQC 기본값 상속.
- `database/migrations/0104_osan_global_notification_preferences.sql:34`: 개인 설정 보존 의도. `database/migrations/0112_interior_busbar_project_creator.sql:4`: 사원 연결 표 정의.

표 집계는209개가 정확히 한 소유 분류에 들어가는지, 현재 양 DB의 표 집합과 같은지 검산했다. 실제 제거·열 정리·함수/뷰/trigger 정리와 API 검증을 수행한 것은 아니다. 사용자 표 확정에 따라 이 명세를 기준으로 열·연결·실행 경계를 설계한다. DB 삭제나 배포 승인은 이 문서 검토에 포함되지 않는다.

독립 검토: `review_table_ownership`(요청 GPT-6-astra/high, 관측 모델 NOT_REPORTED)이 배포 소스·제공된 metadata를 읽어 목표 소유안을 검토했다. 공통 초안31개 중 오산 공지 표2개를 오산 전용으로 보정했고, generic 개인 설정 API의 현행 활성 상태·양방향 알림 SQL·사용자/양식 결합·LQC361건 연결 조건을 반영했다. 보정된29/153/27 분류와182/56 유지 수는 목표안으로 타당하다는 검토 결과다. 검토자는 DB에 접속하거나 삭제 시험을 수행하지 않았으며 실행 검증 완료를 뜻하지 않는다.

## 7. 열 정리 검토안 — 2026-09-29

이 절부터는 표 소유 범위 확정 이후 작성한 세부 설계안이다. 최초 표 확정의 “좋아.”와 구분하여, 사용자는 이 열 정리안을 설명받은 뒤 2026-09-29 다시 “좋아.”로 동의했고 다음 실행 경계 설계를 진행했다. 운영 열 삭제·배포 승인은 아니다. 표 수 목표는 청주182개·오산56개이며, 후속 실행 분리 설계의 관리 기록표1개 추가 제안은 backend 하나를 유지하기로 한 최신 선택에 따라 이번 범위에서 제외한다. 기존 기능과 데이터 의미를 보존하면서 상대 사업부 구조 의존과 중복 저장을 줄인다. 모든 유지 표의 열을 최소화하는 전면 재설계는 아니다.

| 표 | 현재 각 DB의 열 수 | 청주 목표 | 오산 목표 | 판단 |
| --- | ---: | ---: | ---: | --- |
| `projects` | 43 | 36 | 19 | 사업부 전용 정보와 중복 저장 분리 |
| `notifications` | 14 | 14 | 12 | 오산에서 청주 작업/업무 이벤트 연결 2개 제거 |
| `notification_deliveries` | 46 | 46 | 45 | 오산에서 청주 작업 연결 1개 제거 |
| `notice_posts` | 17 | 17 | 17 | 공통 공지·점검 공지 코드에 필요한 열 유지 |
| `qms_users` | 15 | 15 | 15 | 열은 유지하고 반대 사업부 업무를 수행하는 부수 동작 분리 |

### 7.1 projects 전체 43개 열의 소유

아래의 제거는 호환 코드·연결 보정 후 목표이며 지금 운영 DB에서 실행할 명령 목록이 아니다. `osan_` 접두사를 없애는 명칭 변경은 포함하지 않는다.

| 열 | 역할/판단 | 청주 | 오산 |
| --- | --- | --- | --- |
| `id` | 다른 표가 참조하는 프로젝트 고유 번호. 기존 값 보존 | 유지 | 유지 |
| `project_key` | 프로젝트 권한 조회에 사용하는 키 | 유지 | 유지 |
| `project_number` | 오산에서는 project_code와 중복. 권한 조회 호환 보정 필요 | 유지 | 제거 |
| `name` | 오산에서는 project_title과 중복. 권한 조회 호환 보정 필요 | 유지 | 제거 |
| `created_at_utc` | 생성 시각 | 유지 | 유지 |
| `customer_name` | 현재 화면·검색에 사용하는 고객명 | 유지 | 유지 |
| `item` | 청주 품목. 오산 품명은 osan_product_name 사용 | 유지 | 제거 |
| `project_code` | 업무 프로젝트 코드. 오산 중복 허용 유지 | 유지 | 유지 |
| `project_title` | 프로젝트 제목 | 유지 | 유지 |
| `project_title_normalized` | 청주 제목 중복 방지용 정규화 값 | 유지 | 제거 |
| `delivery_date` | 납기일 | 유지 | 유지 |
| `sales_owner_user_id` | 청주 영업 담당자 | 유지 | 제거 |
| `sales_amount` | 청주 매출 금액 | 유지 | 제거 |
| `currency_code` | 청주 금액 통화 | 유지 | 제거 |
| `delivery_location` | 청주 납품 장소 | 유지 | 제거 |
| `status` | 프로젝트 상태. 사업부별 현행 상태 규칙 유지 | 유지 | 유지 |
| `status_reason` | 청주 상태 변경 사유. 오산 HOLD 사유 저장처와 다름 | 유지 | 제거 |
| `held_by_user_id` | 청주 보류 처리자 | 유지 | 제거 |
| `held_at_utc` | 청주 보류 시각 | 유지 | 제거 |
| `cancelled_by_user_id` | 청주 취소 처리자 | 유지 | 제거 |
| `cancelled_at_utc` | 청주 취소 시각 | 유지 | 제거 |
| `created_by_user_id` | 생성자 | 유지 | 유지 |
| `updated_at_utc` | 최종 수정 시각 | 유지 | 유지 |
| `packaging_method` | 청주 포장 방식 | 유지 | 제거 |
| `deleted_at_utc` | 논리삭제 시각. 삭제된 행도 보존 | 유지 | 유지 |
| `deleted_by_user_id` | 논리삭제 처리자 | 유지 | 유지 |
| `delete_reason` | 논리삭제 사유 | 유지 | 유지 |
| `deleted_correlation_id` | 청주 삭제 요청 추적값. 오산에서는 미사용 | 유지 | 제거 |
| `fat_required` | 청주 FAT 검사 여부. 오산은 상속된 기본값 | 유지 | 제거 |
| `completed_by_user_id` | 청주 프로젝트 완료 처리자. 오산 단계 완료 기록과 별개 | 유지 | 제거 |
| `completed_at_utc` | 청주 프로젝트 완료 시각. 오산 단계 완료 기록과 별개 | 유지 | 제거 |
| `structure_mode` | 청주 프로젝트 구성 방식 | 유지 | 제거 |
| `iqc_routing_policy` | 청주 입고 검사 정책. 오산은 상속된 기본값 | 유지 | 제거 |
| `lqc_operational_snapshot` | 청주 LQC 운영 여부. 오산은 상속된 기본값 | 유지 | 제거 |
| `lqc_template_version_id` | 청주 검사 양식 연결. 오산도 현재 기본 양식에 연결됨 | 유지 | 제거 |
| `lse_task_number` | 청주 LSE 작업 번호 | 유지 | 제거 |
| `project_profile` | DB 안에서 사업부 구분. 전용 실행 경계 확립 후 불필요 | 제거 | 제거 |
| `osan_po_number` | 오산 PO 번호 | 제거 | 유지 |
| `osan_work_order_number` | 오산 작업 지시 번호 | 제거 | 유지 |
| `osan_product_name` | 오산 품명 | 제거 | 유지 |
| `osan_quantity` | 오산 수량 | 제거 | 유지 |
| `osan_delivery_hold` | 오산 납기 HOLD 여부 | 제거 | 유지 |
| `osan_customer_id` | 오산 고객 기준정보 연결 | 제거 | 유지 |

### 7.2 실제 값과 보존 조건

이번 추가 조회는 기존 통로를 재사용한 읽기 전용 집계다. 청주3행·오산361행에 대해 NULL/서로 다른 값 개수·중복 열 불일치 개수·제약/인덱스/trigger 정의를 확인했다. 아래는 조회 시점의 스냅샷이며 실제 변경 직전에 다시 검사한다.

- 오산361행의 profile은 모두 Osan이고 상태는 Active305행·Completed56행이다. 논리삭제7행도 포함한다. 행을 다시 만들거나 id를 바꾸거나 논리삭제 행을 물리삭제하지 않는다.
- 오산의 `name`/`project_title`, `project_number`/`project_code`는 각각 불일치0행이었다. 현재 수정 코드는 대표 열만 갱신하므로 미래에도 일치한다고 가정하지 않는다. 제거 직전에 불일치가 생겼다면 값 보존 방식을 확인한 뒤 처리한다.
- 오산 `project_code`는361행에 서로 다른 값337개다. migration0090도 중복 허용을 명시한다. 유일 번호는 `id`/`project_key`로 유지하며 코드에 새 UNIQUE 조건을 붙이지 않는다.
- 오산 영업 담당·금액·통화·납품 장소·포장·청주 보류/취소·완료 처리자/시각·LSE 등은 현재 전부 NULL이다. 제거 근거는 NULL 자체가 아니라 오산 업무 코드가 별도 구조를 사용한다는 점이다. 청주는 금액·통화·납품 장소·LSE에 실제 값이 있어 해당 열을 유지한다.
- 오산361행 모두 같은 LQC 기본 양식 번호를 갖는다. FAT/IQC/LQC 운영값도 기본값을 상속한다. 이 연결을 보정해야 검사 양식 표를 오산에서 제거할 수 있다.
- 오산 Completed56행에도 `completed_by_user_id`/`completed_at_utc`는 NULL이다. 실제 단계 완료자·시각은 `osan_project_target_steps`와 `osan_stage_records` 등 오산 진행 기록에서 보존한다. 마지막 수정자로 완료자를 추정해 새 값을 만들지 않는다. 상태 재계산과 최초 완료 알림도 유지한다.
- 오산 HOLD는 `osan_delivery_hold`로, HOLD 변경 사유는 기존 관리 변경 기록으로 유지한다. 청주의 보류/취소 열을 제거해도 이 업무 기록을 잃지 않게 한다.
- `customer_name`과 `osan_customer_id`는 함께 유지한다. 현재 표시·검색 및 고객 연결 계약을 보존하며, 이름 저장 방식까지 이번에 재설계하지 않는다.

### 7.3 projects를 읽고 쓰는 연결 보정

| 연결 | 청주 설계 | 오산 설계 |
| --- | --- | --- |
| 사업부 판별 | 청주 업무 모듈의 DB 접근 객체와 qms_database_identity를 CHEONGJU에 고정 | 오산 업무 모듈의 DB 접근 객체와 qms_database_identity를 OSAN에 고정 |
| project_profile 조건 | 조회·제약·부분 인덱스에서 사업부 필터 제거 | 조회·등록·수정·알림 보호 trigger에서 사업부 필터 제거 |
| 번호·제목 권한 조회 | 현행 계약 유지 | DbIdentityStore의 단건 조회와 사용자별 프로젝트 목록 모두 project_code/project_title을 기존 응답 이름으로 제공 |
| 품목 표시 | item 유지 | 공통 알림 응답의 기존 item 빈값 호환을 유지. 임의로 품명으로 바꾸지 않음 |
| 제목 유일성 | 정규화 제목 인덱스의 profile 조건만 제거. NULL·논리삭제 제외 조건 유지 | 청주 정규화 제목 열/인덱스 제거. 제목 유일성 신설 없음 |
| 코드·고객 인덱스 | 청주 코드 인덱스 유지, 오산 고객 인덱스 제거 | 코드 중복 허용 유지. 고객/코드 인덱스의 profile 조건을 제거하고 중복 인덱스 정리 |
| 필수값 | 청주 현행 입력 규칙 유지 | profile에 종속된 검사를 오산 전용 검사로 전환. 제목·코드·고객·납기·품명·양의 수량 등 현행 API 규칙 보존 |
| 상태 | 현행 청주 상태·완료 메타데이터 규칙 유지 | Active/Completed와 별도 HOLD 규칙 유지. 청주 보류/취소 상태 신설 없음 |
| 사용자/고객 FK | 생성·삭제 등 사용자 FK 유지. osan_customer_id FK 제거 | 생성·삭제 사용자 및 osan_customer_id FK 유지. 제거 열의 사용자/LQC 양식 FK 제거 |
| 보호 trigger | IQC 정책·LQC snapshot 변경 제한 유지 | 해당 두 전용 trigger와 더는 참조되지 않는 함수 제거 |
| 감사/삭제 | 현행 프로젝트 감사·논리삭제 유지 | 현행 프로젝트 감사·논리삭제 유지. delete_reason 길이 제한500도 유지 |

업무 모듈의 고정된 DB 접근 객체가 기대한 사업부의 DB에 연결되는지 identity로 검증하고, 기존 profile 참조를 보정한 다음 `project_profile`을 제거한다. DB가 분리되어 있다는 사실만으로 현재의 profile 참조를 곧바로 지워도 된다는 뜻은 아니다. 오산 DB의 현재 profile 기본값조차 Cheongju이므로 이를 오산 등록 코드가 명시값으로 덮어쓰는 의존도 함께 정리한다.

## 8. 알림·공지·사용자 관리의 연결 설계안

### 8.1 알림

- 오산 `notifications.work_item_id`, `notifications.generated_by_event_id`, `notification_deliveries.work_item_id`를 제거 대상으로 한다. 현재 세 열의 비NULL 값은0개다. 이 세 FK가 청주 작업·이벤트 표를 참조한다. 프로젝트의 LQC FK까지 합치면 이번에 확인한 유지 표→청주 제거 표의 주요 연결4개다.
- 알림 목록·상세·발송·WebPush 생성 SQL을 사업부별로 분리한다. 오산 SQL은 work_items/workflow_stages를 참조하지 않고, 청주 SQL은 osan_notification_events/osan_notification_global_preferences를 참조하지 않는다. 실행 조건이 거짓인 JOIN/CASE 분기에도 상대 소유 표 이름을 남기지 않는다.
- 기존 공통 응답의 선택값 필드는 필요하면 NULL/기존 빈값으로 제공한다. 실제 SQL은 제거된 열을 조회하지 않게 한다. 응답 이름 변경이나 새로운 표시 정책은 포함하지 않는다.
- `manual_payload_json`은 **오산 메일 내용을 저장하므로 유지한다.** 이름이 manual이라고 청주 전용으로 판단하지 않는다. 나머지 발송 상태·점유/수명·재시도·중복 방지·구독 세대·스냅샷·발송 시도 기록도 보존한다.
- 오산 `prevent_osan_external_notification_delivery` 보호 함수의 project_profile 참조는 고정 DB identity 기준으로 바꾼다. 알림/프로젝트/수신자 일치, 허용 이벤트, 활성 사용자·권한·수신 범위·WebPush 기기 및 구독 세대/활성 시각 검사는 유지한다. BEFORE INSERT 시점도 보존한다. profile 의존 하나를 없애려고 보호 trigger 전체를 삭제하거나 발송 상태 UPDATE까지 새로 제한하지 않는다.

### 8.2 공지와 사용자

- `notice_posts`의17개 열은 양쪽에서 유지한다. `pinned`, `popup_enabled`, `popup_version`, `version`도 포함한다. 오산 전용 popup receipt/settings 표가 있다는 이유로 청주의 공지 열을 지우면 공통 공지 조회·버전 검사·배포 점검 공지 생성에 영향을 준다.
- `qms_users`의15개 열은 양쪽에서 유지한다. 청주 신규 사용자 등록에 붙은 오산 고객 자동 배정 trigger/전용 함수는 제거한다. 오산 사용자 수정의 청주 양식 관리자 연결/감사 동기화는 제거한다. 자기 사업부의 사용자 활성·부서·역할·부서장 변경과 감사는 보존한다.
- 기존 권한과 총괄 관리 절차는 §5의 공통 관리 책임 및 각 사업부 전용 처리 원칙을 따른다. 같은 프로세스의 공통 관리 모듈이 제한된 사용자관리 인터페이스로 프로필 수정의 요청·진행·복구를 조정한다. 양쪽 접속 정보를 가진 공유 프로세스 안에서도 일반 업무 모듈에 상대 DB를 선택하는 기능은 제공하지 않는다. 이를 위한 신규 내부 HTTP 호출이나 별도 앱은 만들지 않는다.
- 나머지 공통 표의 열과 기존 감사 JSON은 이번 단계에서 유지한다. 과거 JSON에 제거할 열 이름이 등장해도 변경 이력을 삭제하거나 덮어쓰지 않는다.

## 9. 열 설계 근거·검증과 다음 경계

추가 근거는 동일 배포 source 기준이다. 경로는 `backend/src/Emi.Qms.Api/` 기준이며 migration은 repository root 기준이다.

- `OsanProjects/OsanProjectStore.cs:1001`: 오산 등록 열과 중복 번호/제목 저장. `OsanProjects/OsanProjectManagement.cs:29,60,77`: profile 확인·논리삭제·대표 열 수정.
- `Identity/DbIdentityStore.cs:290,497`: 권한 확인용 프로젝트 단건/목록의 중복 열 참조. `Workflow/WorkflowStore.cs:915,971,2438`: 알림의 item 참조.
- `OsanProjects/OsanProgressStore.cs:694`, `OsanProjects/OsanStageRecords.cs:56`: 오산 상태 재계산. 완료 기록은 오산 단계 표가 소유한다.
- `Notifications/OsanNotificationWriter.cs:142`, `Notifications/NotificationDeliveryStore.cs:1604`: 오산 메일 snapshot 저장/복원.
- `Notices/NoticeStore.cs:20,169,212,230,465,553`, `DeploymentMaintenance/DeploymentMaintenanceStore.cs:112`: 공지 열 공통 사용.
- `database/migrations/0090_osan_project_code_duplicates.sql`: 오산 프로젝트 코드 중복 허용. 실제 DB의 제약·인덱스·trigger 정의도 별도로 대조했다.

독립 검토는 작성과 분리된 기존 `review_table_ownership` 맥락을 재사용해 이번 열 목록·배포 source·집계 기준으로 수행했다(요청 GPT-6-astra/high, 관측 NOT_REPORTED). 43→청주36/오산19 및 알림14→12·46→45는 위 조건을 반영한 설계로 타당하다고 판단했다. 단건/목록 양쪽 권한 조회, 알림 item 호환, 코드 중복, 청주 제목 유일성, 공지 열, 메일 snapshot, BEFORE INSERT 보호 조건 및 단계 완료 기록 보존 지적을 반영했다. DB 변경이나 삭제 시험을 수행한 검토는 아니다.

이번 완료 범위는 표 확정 기록과 열·연결 설계다. 구현 시에는 정확한 열/제약/인덱스/함수/뷰/trigger 의존 목록을 만들고 기존130개 migration 이력을 보존한 추가 변경으로 진행한다. 업무 모듈의 데이터 접근·계정별 권한·migration/worker/공통 관리의 대상 고정을 함께 구현해야 사용자가 선택한 업무 처리와 DB 구조 분리가 성립한다.

운영 적용 전에는 합성 데이터의 disposable DB에서 기존 구조→목표 구조와 새 설치, 사용자/프로젝트/진행/사진/알림/감사 및 계정별 반대 DB 접속 거부를 검증한다. 일반 업무 요청의 잘못된 DB 선택·헤더 변경·동시 요청과 worker/migration 대상 고정도 검증한다. 실제 잔존 행/열 값 재확인·복구 준비·호환 배포 순서·구체적 운영 변경 범위를 별도로 정한다. 이번에 DB 열/표 삭제, 앱 구현·배포, 유료 자원 생성은 실행하지 않았다.
