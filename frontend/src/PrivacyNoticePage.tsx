import type { MouseEvent } from 'react';

const privacyContactEmail = 'subin.park@emiinc.co.kr';
const privacyContactPhone = '010-8236-1431';

const privacySections = [
  { id: 'privacy-processing', label: '개인정보 처리' },
  { id: 'privacy-services', label: '외부 서비스' },
  { id: 'privacy-rights', label: '문의와 권리' },
  { id: 'service-rules', label: '사내 이용 기준' }
] as const;

type PrivacySectionId = typeof privacySections[number]['id'];

type PrivacyNoticePageProps = {
  onBack: () => void;
};

export function PrivacyNoticePage({ onBack }: PrivacyNoticePageProps) {
  function moveToSection(event: MouseEvent<HTMLAnchorElement>, targetId: PrivacySectionId) {
    event.preventDefault();
    const target = document.getElementById(targetId);
    if (!target) return;

    const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches === true;
    window.history.replaceState(window.history.state, '', `${window.location.pathname}${window.location.search}#${targetId}`);
    target.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'start' });
    target.focus({ preventScroll: true });
  }

  return (
    <article className="privacy-notice-page">
      <header className="privacy-notice-hero">
        <div>
          <p className="eyebrow">SERVICE &amp; PRIVACY</p>
          <h2>개인정보·이용 안내</h2>
          <p>EMI PMS에서 처리하는 정보와 사내 이용 기준을 한곳에서 확인할 수 있습니다.</p>
        </div>
        <button type="button" onClick={onBack}>홈으로</button>
      </header>

      <aside className="privacy-pilot-note" aria-label="시범 운영 안내">
        <strong>시범 운영 기간</strong>
        <span>2026년 8월 11일(화) ~ 8월 31일(월)</span>
        <p>시범 운영 중 생성된 계정과 업무 자료는 종료일에 자동 삭제하지 않으며, 이후 운영으로 그대로 이어집니다.</p>
      </aside>

      <nav className="privacy-notice-index" aria-label="개인정보·이용 안내 목차">
        {privacySections.map((section) => (
          <a
            key={section.id}
            href={`#${section.id}`}
            onClick={(event) => moveToSection(event, section.id)}
          >
            {section.label}
          </a>
        ))}
      </nav>

      <section id="privacy-processing" className="privacy-notice-section" tabIndex={-1}>
        <div className="privacy-section-heading">
          <span>01</span>
          <div>
            <p className="eyebrow">PRIVACY POLICY</p>
            <h3>개인정보 처리방침</h3>
          </div>
        </div>
        <p>
          주식회사 이엠아이는 EMI PMS의 계정 관리, 권한 부여, 프로젝트 업무 수행, 업무 알림과 보안 운영을 위해
          필요한 범위에서 임직원 정보를 처리합니다.
        </p>

        <div className="privacy-data-list" aria-label="처리 정보와 보유 기간">
          <section>
            <h4>계정·조직 정보</h4>
            <p>이름, 업무용 이메일, Microsoft Entra 식별정보, 부서와 역할</p>
            <dl>
              <div><dt>이용 목적</dt><dd>로그인, 계정 식별, 권한 부여와 업무 담당 확인</dd></div>
              <div><dt>보유 기간</dt><dd>사내 규정에 따름</dd></div>
            </dl>
          </section>
          <section>
            <h4>프로필 사진 <span>선택</span></h4>
            <p>사용자가 직접 등록한 JPEG 또는 PNG 사진</p>
            <dl>
              <div><dt>이용 목적</dt><dd>계정 식별과 화면 표시</dd></div>
              <div><dt>보유 기간</dt><dd>사진 삭제·교체 또는 퇴사 시까지</dd></div>
            </dl>
            <p className="privacy-choice-note">등록 전 선택 동의를 받으며, 동의하지 않아도 기본 이니셜로 모든 업무 기능을 이용할 수 있습니다.</p>
          </section>
          <section>
            <h4>업무 관련 파일·기록</h4>
            <p>프로젝트, 담당 업무, 검사·조치 결과, 의견, 사진·첨부파일과 변경 이력</p>
            <dl>
              <div><dt>이용 목적</dt><dd>프로젝트 수행, 부서 간 인계, 품질 증빙과 업무 감사</dd></div>
              <div><dt>보유 기간</dt><dd>사내 규정에 따름</dd></div>
            </dl>
          </section>
          <section>
            <h4>알림·접속·보안 정보</h4>
            <p>알림 설정과 발송 이력, PWA 푸시 기기 구독 정보, 접속·보안 기록, 인증 및 브라우저 세션 정보</p>
            <dl>
              <div><dt>이용 목적</dt><dd>업무 알림 전달, 로그인 유지, 장애 대응과 보안 감사</dd></div>
              <div><dt>보유 기간</dt><dd>사내 규정에 따름</dd></div>
            </dl>
          </section>
        </div>
      </section>

      <section id="privacy-services" className="privacy-notice-section" tabIndex={-1}>
        <div className="privacy-section-heading">
          <span>02</span>
          <div>
            <p className="eyebrow">CONNECTED SERVICES</p>
            <h3>외부 서비스와 알림</h3>
          </div>
        </div>
        <p>
          로그인과 서비스 운영에는 회사가 검토해 사용 중인 Microsoft Entra, Azure, Microsoft 365, Teams와 Graph,
          회사 메일 서비스를 이용합니다. 구체적인 처리 범위와 보호 조치는 회사와 각 서비스 제공자의 계약 및 사내 정책을 따릅니다.
        </p>
        <ul>
          <li>업무 알림은 인앱 알림, 사용자가 켠 기기의 PWA 푸시, Teams Activity와 메일로 전달될 수 있습니다.</li>
          <li>PWA 푸시 구독 주소와 암호화 키는 기기 알림 전달에만 사용하며 화면과 일반 로그에는 표시하지 않습니다.</li>
        </ul>
      </section>

      <section id="privacy-rights" className="privacy-notice-section" tabIndex={-1}>
        <div className="privacy-section-heading">
          <span>03</span>
          <div>
            <p className="eyebrow">CONTACT &amp; RIGHTS</p>
            <h3>확인·정정·삭제 요청과 문의</h3>
          </div>
        </div>
        <p>
          본인의 정보 확인, 정정, 삭제 또는 처리 중지를 요청할 수 있습니다. 다만 법령, 감사 또는 사내 기록 보존 의무가 있는
          업무 기록은 요청 즉시 삭제되지 않을 수 있으며 처리 결과와 사유를 안내합니다.
        </p>
        <address className="privacy-contact-card">
          <div><span>담당 부서</span><strong>영업CS팀</strong></div>
          <div><span>이메일</span><a href={`mailto:${privacyContactEmail}`}>{privacyContactEmail}</a></div>
          <div><span>전화</span><a href={`tel:${privacyContactPhone.replaceAll('-', '')}`}>{privacyContactPhone}</a></div>
        </address>
      </section>

      <section id="service-rules" className="privacy-notice-section" tabIndex={-1}>
        <div className="privacy-section-heading">
          <span>04</span>
          <div>
            <p className="eyebrow">INTERNAL USE</p>
            <h3>사내 서비스 이용 기준</h3>
          </div>
        </div>
        <ul className="privacy-rule-list">
          <li><strong>업무 목적으로만 이용</strong><span>개인 용도나 승인받지 않은 외부 업무에 사용하지 않습니다.</span></li>
          <li><strong>계정 공유 금지</strong><span>본인 계정만 사용하고 다른 사람에게 로그인 정보나 인증 수단을 전달하지 않습니다.</span></li>
          <li><strong>최소한의 조회·다운로드</strong><span>담당 업무에 필요한 범위만 확인하고 자료를 승인 없이 외부로 반출하지 않습니다.</span></li>
          <li><strong>업무 증빙 주의</strong><span>사진과 첨부파일에 업무와 무관한 개인정보, 얼굴, 사적 문서가 포함되지 않게 확인합니다.</span></li>
          <li><strong>문제 즉시 신고</strong><span>잘못된 공유, 계정 도용, 분실 또는 정보 노출이 의심되면 담당 부서에 바로 알립니다.</span></li>
        </ul>
      </section>

      <footer className="privacy-notice-revision">
        <span>시행일 2026년 8월 11일</span>
        <span>버전 1.0 · 시범 운영 검수본</span>
      </footer>
    </article>
  );
}
