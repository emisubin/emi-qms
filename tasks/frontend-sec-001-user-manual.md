# TASK-FRONTEND-SEC-001 User Manual

## 1. Dependency란 무엇인가

Dependency는 EMI-QMS가 화면을 만들고 테스트하거나 실행할 때 사용하는 외부 software 부품이다. 직접 만든 업무 기능이 아니지만 안전성과 개발 환경에 영향을 준다.

## 2. Vite와 esbuild가 하는 일

- Vite: 개발 중 frontend 화면을 열고 변경을 빠르게 반영하며 production build를 만든다.
- esbuild: JavaScript/TypeScript를 빠르게 변환하는 Vite 내부 도구다.
- Vitest: frontend unit test를 실행한다.

## 3. 왜 업데이트가 필요한가

기존 version에는 개발 서버가 특정 조건에서 local file을 잘못 보여줄 수 있는 공개 취약점이 있었다. 이번 변경은 Vite major를 바꾸지 않고 공식 수정이 포함된 patch version으로 올린다.

## 4. 보안 경고 등급의 의미

- Critical: 즉시 차단하고 해결해야 하는 최고 위험
- High: 게시나 merge 전에 반드시 해결
- Moderate: exploit 조건과 patch를 검토해 해결 또는 risk 결정
- Low: 영향은 낮지만 가능한 경우 함께 해결

이번 Task는 모든 등급을 0으로 만들었다.

## 5. 사용자 화면에 미치는 영향

업무 기능, 화면 구성과 data 형식은 바뀌지 않는다. 개발 서버와 build/test 도구만 업데이트된다. 화면이 이전과 동일하게 열리고 작동하는지가 검수 핵심이다.

## 6. 업데이트 후 확인할 화면

- Main
- Project와 내 업무
- Notification
- Admin
- Teams Activity

일반 사용자 화면은 일반 검수 사용자 역할로, 관리자 화면은 관리자 역할로 확인한다.

## 7. HTTP/HTTPS 접속 확인

일반 개발은 HTTP, Teams 검수는 HTTPS를 사용한다. 이번 자동 검증은 별도 port 5184/5185에서 수행했다. 현재 5174 server는 아직 업데이트 전 runtime이므로 patch가 반영됐다고 판단하면 안 된다.

## 8. 화면이 열리지 않을 때

1. 주소의 HTTP/HTTPS가 맞는지 확인
2. Frontend와 backend health 확인
3. Port 충돌 메시지 확인
4. Browser console 오류 확인
5. Dependency install과 audit 결과 확인

현재 5174 server를 임의 재시작하지 말고 controlled handover Task를 따른다.

## 9. Rollback이 필요한 경우

업데이트 후 명확한 회귀가 있으면 dependency manifest와 lockfile을 함께 되돌린다. DB는 변경하지 않았으므로 DB rollback은 없다. 취약 version으로 장기 운영하지 않고 수정 version으로 forward-fix한다.

## 10. 하면 안 되는 작업

- Audit 숫자만 없애려고 force update
- Vite major 임의 upgrade
- Current UAT DB/volume 삭제
- Current 5174 server 임의 restart
- 실제 secret/certificate 내용 공유
- 승인 없는 실제 외부 알림 발송
- 사용자 검수 전 PR merge

## 11. FAQ

### 업무 data가 바뀌나요?

아니다. Migration과 DB 변경이 없다.

### 현재 5174에서 바로 patch를 확인할 수 있나요?

아니다. 현재 server는 patch 전 process다. PR merge 후 별도 controlled handover가 필요하다.

### 왜 Vite 8로 올리지 않았나요?

이번 목적은 보안 patch다. Vite 7.3.6으로 필요한 fix와 esbuild 0.28 호환성을 얻을 수 있어 major upgrade risk를 피했다.

### Audit 경고가 다시 생길 수 있나요?

가능하다. 새로운 advisory가 공개될 수 있으므로 게시 전과 정기적으로 audit을 다시 실행한다.

### 실제 secret으로 보안 테스트하나요?

아니다. `/tmp`의 가짜 canary fixture만 사용한다.

## 12. 사용자 검수 체크리스트

상태: `Checklist 작성됨`, `자동 검증 완료`, `사용자 검수 대기`.

- [ ] Main 화면이 기존과 동일하게 열림
- [ ] Project, 업무와 admin 화면이 열림
- [ ] Teams Activity와 HTTPS 접속 정상
- [ ] API/User 카드 정상
- [ ] 화면 동작/style의 눈에 띄는 회귀 없음
- [ ] SOP가 실행 가능함
- [ ] 이 문서가 비개발자에게 이해 가능함
- [ ] Audit High 0 확인
- [ ] 현재 5174는 patch 전이며 별도 handover가 필요함을 이해함
