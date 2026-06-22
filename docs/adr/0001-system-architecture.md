# ADR-0001: 기본 시스템 아키텍처

- 상태: 제안 승인 전
- 작성일: 2026-06-22

## 배경

PC 전체관리와 모바일 QR 현장입력이 모두 필요하며, Microsoft 365·Teams를 사용하는 사내 환경과 연동해야 합니다.

## 결정

- 반응형 웹 프런트엔드: React + TypeScript
- 업무 API: ASP.NET Core Web API
- 관계형 데이터: PostgreSQL
- 인증: Microsoft Entra ID
- 사진·PDF·증빙: Azure Blob Storage
- 웹 호스팅: Azure App Service
- 비밀정보: Azure Key Vault
- 모니터링: Application Insights

초기 구현은 단일 배포 가능한 모듈형 애플리케이션으로 시작하며, 마이크로서비스로 분할하지 않습니다.

## 이유

- 별도 모바일 앱 없이 PC·스마트폰에서 동일 시스템 사용 가능
- 회사 Microsoft 계정 기반 로그인과 권한 연동에 적합
- 프로젝트·제품·검사·출하 관계를 관계형 DB로 명확히 통제 가능
- 사진과 문서를 DB에서 분리해 확장 가능
- 초기 운영·배포 복잡도를 낮춤

## 결과

개발환경, 시험환경, 운영환경을 분리해야 하며 Azure 비용·운영 담당자·데이터 보관정책을 별도로 확정해야 합니다.
