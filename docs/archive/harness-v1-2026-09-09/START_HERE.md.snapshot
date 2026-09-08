# 시작 안내

## 이번 단계의 목표

이 폴더를 회사가 소유하는 **비공개 Git 저장소**로 만들고 Codex가 작업할 프로젝트 폴더로 등록합니다.

## 권장 방법: GitHub Desktop

1. 이 압축파일을 회사 PC의 적절한 위치에 풉니다.
   - 권장 예: `C:\Work\emi-qms`
   - OneDrive 동기화 폴더와 공용 네트워크 드라이브는 초기 개발 폴더로 사용하지 않습니다.
2. GitHub Desktop을 실행합니다.
3. `File → Add local repository`를 선택하고 압축을 푼 폴더를 지정합니다.
4. Git 저장소가 아니라는 안내가 나오면 `create a repository`를 선택합니다.
5. 저장소 이름은 `emi-qms`로 지정합니다.
6. 첫 커밋 메시지는 아래와 같이 입력합니다.

```text
chore: initialize EMI QMS project documentation
```

7. `Publish repository`를 누릅니다.
8. **Keep this code private** 또는 **Private**가 반드시 선택됐는지 확인합니다.
9. 가능하면 개인 계정보다 회사 GitHub Organization 소유로 게시합니다.

## Codex에서 열기

1. Codex 앱을 실행합니다.
2. `Open project` 또는 프로젝트 폴더 선택 메뉴에서 `emi-qms` 폴더를 엽니다.
3. 권한은 우선 작업 폴더 내부만 수정 가능한 기본 샌드박스를 유지합니다.
4. 첫 요청으로 아래 문장을 입력합니다.

```text
AGENTS.md와 docs 전체를 먼저 읽어라.
tasks/001-bootstrap-development-environment.md를 수행하되,
요구사항이 충돌하면 임의로 결정하지 말고 충돌 지점을 보고하라.
작업이 끝나면 변경 파일, 실행 방법, 테스트 결과, 남은 위험을 요약하라.
```

## 게시 전 보안 확인

다음 정보가 저장소에 들어가면 안 됩니다.

- 운영 DB 비밀번호
- Microsoft 365·Azure 관리자 비밀번호
- ECOUNT 운영 API 키
- 고객사 실도면과 실제 프로젝트 데이터
- 기사 연락처 등 실제 개인정보
- 운영 서버 인증서·SSH 키

개발 중에는 `.env.example`에 있는 가짜 값만 사용합니다.
