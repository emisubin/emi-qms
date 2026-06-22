# 기여 절차

1. 작업지시서 또는 Issue를 기준으로 별도 브랜치를 만듭니다.
2. 기능과 테스트를 함께 변경합니다.
3. 변경 범위를 작게 유지합니다.
4. Pull Request에 업무 영향, DB 영향, 권한 영향, 테스트 결과를 기록합니다.
5. 관련 부서 담당자의 사용자 검증이 필요한 경우 운영 배포 전에 완료합니다.

## 브랜치 예시

```text
feature/project-registration
feature/product-qr
fix/inspection-permission
chore/bootstrap
```

## 커밋 예시

```text
feat: add project registration validation
fix: block shipment when NCR remains open
test: cover unauthorized product access
chore: initialize local development environment
```
