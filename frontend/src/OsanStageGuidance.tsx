const guidance = [
  {
    description: '1. 외관 / 구조 / 도장 / 색차 검사를 시행한다.\n-. 반드시 2D / 3D 도면을 토대로 검사할 것',
    evidence: '-> IQC 시행 완료 후 외함 사진 1장 촬영하여 등록할 것',
    specification: '도장 및 도금 : 도면 기준\n도장 Spec : 60~150㎛\n색차 Spec : △e 1.5 이하'
  },
  {
    description: '1. 체결 및 조립 검사를 시행한다.\n-. Label 부착 확인\n-. Part Spec 확인\n-. 반드시 승인도를 통해 배치 검사할 것 (배치 오류 부적합 사례 있음)\n-. ESN을 통한 제작 전 사양 반드시 확인할 것',
    evidence: '-> 배치검사 완료 후 사진 1장 촬영하여 등록할 것 (Rack의 경우 Door Open 상태로 촬영)'
  },
  {
    description: '1. Line 배선, 포설, 체결 검사를 시행한다.\n-. Harness표가 아닌 2D 도면으로 검사할 것\n-. DVM을 통한 Pin Check 방식으로 시행할 것\n-. Cable 포설 후 완 체결 시행할 것 (당김검사)\n-. ESN을 통한 Eye Cap 색상 점검할 것',
    evidence: '-> 배선검사 완료 후 ESN과 함께 배선 완료된 사진 1장 촬영하여 등록할 것 (ESN과 설비 동시 촬영)'
  },
  {
    description: '1. 체결부 토크값 확인 및 I-Marking 등 8계통 작업 시행한다.\n-. Safety Label 부착 상태 확인\n-. On/Off Label 부착 상태 확인\n-. Dip Switch Setting\n-. 아크릴 커버 확인',
    evidence: '-> 8계통 작업 완료 후 사진 1장 촬영하여 등록할 것 (Rack의 경우 Door Open 상태로 촬영)'
  },
  {
    description: '1. 설비의 동작 Test를 시행한다.\n-. Connector 출력 이상 유/무 확인\n-. Power Supply 출력 확인\n-. EMO 동작 상태 확인\n-. FAN 동작 확인\n-. GPS/UPS 확인\n-. Door Close 시 Limit Switch 확인',
    evidence: '-> 동작검사 완료 후 Relay On 상태 사진 1장 촬영하여 등록할 것'
  },
  {
    description: '1. 설비 외관 / 8계통 / CTQ 확인\n-. 판금류 가공품 외관 이상 없을 것\n-. 고객사 라벨 부착 기준에 이상 없을 것\n-. I-Marking 표기 기준에 이상 없을 것\n-. Safety Label Map과 일치 확인\n-. Dip Switch Setting 일치 확인',
    evidence: '-> 출하검사 완료 후 외관 전체 상태 사진 1장 촬영하여 등록할 것'
  },
  {
    description: '1. 제품 포장 및 Cleaning\n-. Cleaning 상태 이상 없을 것\n-. 포장상태 이상 없을 것\n-. 포장 이후 제품 식별라벨 확인',
    evidence: '-> 포장 완료 후 식별라벨 부착된 상태 사진 1장 촬영하여 등록할 것'
  }
];

export function OsanStageGuidance({ stage }: { stage: number }) {
  const content = guidance[stage - 1];
  if (!content) return null;
  return <section className="osan-progress-guidance" aria-label="단계 설명">
    <p>{content.description}</p>
    <p><strong>{content.evidence}</strong></p>
    {content.specification && <p>{content.specification}</p>}
  </section>;
}
