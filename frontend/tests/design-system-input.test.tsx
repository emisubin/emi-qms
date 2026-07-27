import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { DsActionBar, DsChoiceGroup, DsInputFlow, DsInputSection } from '../src/design-system';

describe('department input design system', () => {
  it('exposes the same three-step input order and numbered sections', () => {
    render(
      <DsInputFlow title="구매정보 입력">
        <DsInputSection number={1} title="대상 확인">
          <input aria-label="품목" />
        </DsInputSection>
        <DsActionBar description="확인 후 저장">
          <button type="button">저장</button>
        </DsActionBar>
      </DsInputFlow>
    );

    expect(screen.getByRole('heading', { name: '구매정보 입력' })).toBeInTheDocument();
    expect(screen.getByRole('list', { name: '입력 순서' })).toHaveTextContent('1대상 확인2값 입력3저장');
    expect(screen.getByRole('heading', { name: '대상 확인' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '저장' })).toBeEnabled();
  });

  it('uses one-click accessible choices without changing the stored value contract', () => {
    const onChange = vi.fn();
    render(
      <DsChoiceGroup
        label="FAT 필요 여부"
        value="false"
        options={[
          { value: 'false', label: '필요 없음' },
          { value: 'true', label: '필요' }
        ]}
        onChange={onChange}
      />
    );

    expect(screen.getByRole('button', { name: '필요 없음' })).toHaveAttribute('aria-pressed', 'true');
    fireEvent.click(screen.getByRole('button', { name: '필요' }));
    expect(onChange).toHaveBeenCalledWith('true');
  });
});
