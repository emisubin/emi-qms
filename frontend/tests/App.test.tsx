import { StrictMode } from 'react';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from '../src/App';

const salesOwnerId = '50000000-0000-0000-0000-000000000002';
const projectId = '71000000-0000-0000-0000-000000000010';
const onHoldProjectId = '71000000-0000-0000-0000-000000000011';
const cancelledProjectId = '71000000-0000-0000-0000-000000000012';
const panelIds = [
  '72000000-0000-0000-0000-000000000001',
  '72000000-0000-0000-0000-000000000002',
  '72000000-0000-0000-0000-000000000003',
  '72000000-0000-0000-0000-000000000004'
];

describe('App', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(mockFetch));
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:panel-template');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
  });

  afterEach(() => {
    window.history.pushState(null, '', '/');
    Object.defineProperty(window, 'matchMedia', { writable: true, value: undefined });
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('shows project registration actions for Sales users', async () => {
    render(<App />);

    expect(await screen.findByRole('button', { name: '신규 프로젝트' })).toBeInTheDocument();
    expect(screen.getAllByText('KRW 1,250,000.5').length).toBeGreaterThan(0);
  });

  it('shows all project tabs by default with a sticky desktop header and workflow progress', async () => {
    render(<App />);

    const tabs = await screen.findAllByRole('tab');
    expect(tabs.slice(0, 5).map((tab) => tab.textContent)).toEqual(['전체', '진행', '보류', '완료', '취소']);
    expect(screen.getByRole('tab', { name: '전체' })).toHaveAttribute('aria-selected', 'true');

    const table = await screen.findByRole('table', { name: '프로젝트 목록' });
    const header = table.querySelector('.project-list-head');
    expect(header).not.toBeNull();
    expect(header).toHaveTextContent('프로젝트명고객사CodeItem면수납기일상태진행률');
    expect(header).toHaveClass('project-list-head');
    expect(within(table).getByText('TASK-003A Demo')).toBeInTheDocument();
    expect(within(table).getByText('OnHold Project')).toBeInTheDocument();
    expect(within(table).getByText('Completed Project')).toBeInTheDocument();
    expect(within(table).getByText('Cancelled Project')).toBeInTheDocument();
    expect(within(table).getByText('제조 전')).toBeInTheDocument();
    expect(within(table).getByText('0%')).toBeInTheDocument();
    expect(table).not.toHaveTextContent('BeforeManufacturing');
    expect(table).not.toHaveTextContent('0/4');

    fireEvent.click(screen.getByRole('tab', { name: '진행' }));
    await waitFor(() => expect(screen.queryByText('OnHold Project')).not.toBeInTheDocument());
    expect(screen.getByText('TASK-003A Demo')).toBeInTheDocument();
  });

  it('renders project list cards for mobile layout without raw enum values', async () => {
    mockMobileViewport(true);
    render(<App />);

    const mobileList = await screen.findByTestId('project-list-mobile');
    const firstCard = within(mobileList).getAllByTestId('project-list-card')[0];
    expect(firstCard).toHaveTextContent('TASK-003A Demo');
    expect(firstCard).toHaveTextContent('고객사EMI Test Customer');
    expect(firstCard).toHaveTextContent('CodePJT-003A');
    expect(firstCard).toHaveTextContent('ItemControl Panel');
    expect(firstCard).toHaveTextContent('면수4면');
    expect(firstCard).toHaveTextContent('납기일2026-10-10');
    expect(firstCard).toHaveTextContent('상태제조 전');
    expect(firstCard).toHaveTextContent('진행률0%');
    expect(firstCard).not.toHaveTextContent('BeforeManufacturing');
  });

  it('hides business action buttons from System Administrator while showing sales amount', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-admin' } });

    await waitFor(() => expect(screen.queryByRole('button', { name: '신규 프로젝트' })).not.toBeInTheDocument());
    expect(screen.getAllByText('KRW 1,250,000.5').length).toBeGreaterThan(0);
  });

  it('hides sales amount and project write buttons from Manufacturing users', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-manufacturing' } });

    await waitFor(() => expect(screen.queryByRole('button', { name: '신규 프로젝트' })).not.toBeInTheDocument());
    expect(screen.queryByText('KRW 1,250,000.5')).not.toBeInTheDocument();

    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    await screen.findByText('제품·패널 목록');

    expect(screen.queryByRole('button', { name: '수정' })).not.toBeInTheDocument();
  });

  it('validates required fields on the create form', async () => {
    render(<App />);

    fireEvent.click(await screen.findByRole('button', { name: '신규 프로젝트' }));
    fireEvent.click(await screen.findByRole('button', { name: '등록' }));

    expect((await screen.findAllByText('필수 입력값입니다.')).length).toBeGreaterThanOrEqual(5);
    expect(screen.getByText('포장방식은 필수 선택값입니다.')).toBeInTheDocument();
  });

  it('shows a friendly duplicate title conflict message', async () => {
    render(<App />);

    fireEvent.click(await screen.findByRole('button', { name: '신규 프로젝트' }));
    await screen.findByRole('option', { name: 'Dev Sales User' });
    fillCreateForm('DUP-001', 'Duplicate Project');
    fireEvent.click(screen.getByRole('button', { name: '등록' }));

    expect(await screen.findByText('동일한 PJT Title이 이미 존재합니다.')).toBeInTheDocument();
  });

  it('disables the submit button while saving and navigates to detail after create', async () => {
    render(<App />);

    fireEvent.click(await screen.findByRole('button', { name: '신규 프로젝트' }));
    await screen.findByRole('option', { name: 'Dev Sales User' });
    fillCreateForm('NEW-001', 'New Project');
    fireEvent.click(screen.getByRole('button', { name: '등록' }));

    expect(await screen.findByRole('button', { name: '저장 중' })).toBeDisabled();
    const productPanelTable = await screen.findByRole('table', { name: '제품·패널 목록' });
    expect(within(productPanelTable).getByText('No')).toBeInTheDocument();
    expect(within(productPanelTable).getByText('패널명')).toBeInTheDocument();
    expect(within(productPanelTable).getAllByText('미입력').length).toBeGreaterThanOrEqual(4);
  });

  it('requires panel selections when decreasing panel count', async () => {
    render(<App />);

    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('button', { name: '수정' }));
    fireEvent.change(await screen.findByLabelText('면수*'), { target: { value: '3' } });
    fireEvent.change(screen.getByLabelText('수정사유*'), { target: { value: '면수 감소' } });
    fireEvent.click(screen.getByRole('button', { name: '저장' }));

    expect(await screen.findByText('감소 면수만큼 취소할 패널을 선택하세요.')).toBeInTheDocument();
  });

  it('keeps project edit form hidden until all initial data is loaded', async () => {
    const salesOwners = createDeferred<Response>();
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname;
      if (path === '/api/sales-owners') {
        return salesOwners.promise;
      }

      return mockFetch(input, init);
    }));

    render(<App />);

    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('button', { name: '수정' }));

    expect(await screen.findByText('프로젝트 정보를 불러오는 중입니다.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '저장' })).not.toBeInTheDocument();

    salesOwners.resolve(json([{ userId: salesOwnerId, displayName: 'Dev Sales User' }]));

    expect(await screen.findByLabelText('면수*')).toHaveValue(4);
    expect(screen.getByRole('button', { name: '저장' })).toBeEnabled();
  });

  it('does not overwrite user edits when a stale project edit load resolves later', async () => {
    const staleProject = createDeferred<Response>();
    let delayNextProjectDetail = false;
    let delayedProjectDetail = false;

    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname;
      if (delayNextProjectDetail
          && !delayedProjectDetail
          && path === `/api/projects/${projectId}`
          && init?.method === undefined) {
        delayedProjectDetail = true;
        return staleProject.promise;
      }

      return mockFetch(input, init);
    }));

    render(<StrictMode><App /></StrictMode>);

    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    delayNextProjectDetail = true;
    fireEvent.click(await screen.findByRole('button', { name: '수정' }));

    const panelCount = await screen.findByLabelText('면수*');
    const customerName = screen.getByLabelText('고객사*');
    expect(panelCount).toHaveValue(4);

    fireEvent.change(panelCount, { target: { value: '6' } });
    fireEvent.change(customerName, { target: { value: 'Changed Customer' } });
    expect(panelCount).toHaveValue(6);
    expect(customerName).toHaveValue('Changed Customer');

    staleProject.resolve(json(projectDetail(true, 'Active', 'TASK-003A Demo')));

    await waitFor(() => {
      expect(panelCount).toHaveValue(6);
      expect(customerName).toHaveValue('Changed Customer');
    });
  });

  it('reinitializes the edit form when navigating to a different project', async () => {
    render(<App />);

    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('button', { name: '수정' }));
    fireEvent.change(await screen.findByLabelText('PJT Title*'), { target: { value: 'Unsaved Title' } });
    expect(screen.getByLabelText('PJT Title*')).toHaveValue('Unsaved Title');

    fireEvent.click(screen.getByRole('button', { name: '상세' }));
    fireEvent.click(await screen.findByRole('button', { name: '목록' }));
    fireEvent.click(await screen.findByText('OnHold Project'));
    fireEvent.click(await screen.findByRole('button', { name: '수정' }));

    expect(await screen.findByLabelText('PJT Title*')).toHaveValue('OnHold Project');
  });

  it('shows stale panel count conflicts without overwriting the edited value', async () => {
    let changePanelCountBody: { panelCount: number; expectedActivePanelCount: number } | undefined;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname;
      if (path === `/api/projects/${projectId}/change-panel-count`) {
        changePanelCountBody = JSON.parse(String(init?.body));
        return json({ title: '다른 사용자가 프로젝트 면수를 변경했습니다. 화면을 새로고침한 후 다시 시도해 주세요.' }, 409);
      }

      return mockFetch(input, init);
    }));

    render(<App />);

    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('button', { name: '수정' }));
    fireEvent.change(await screen.findByLabelText('면수*'), { target: { value: '6' } });
    fireEvent.change(screen.getByLabelText('고객사*'), { target: { value: 'Changed Customer' } });
    fireEvent.change(screen.getByLabelText('수정사유*'), { target: { value: '면수 증가' } });
    fireEvent.click(screen.getByRole('button', { name: '저장' }));

    expect(await screen.findByText('다른 사용자가 프로젝트 면수를 변경했습니다. 화면을 새로고침한 후 다시 시도해 주세요.')).toBeInTheDocument();
    expect(screen.getByLabelText('면수*')).toHaveValue(6);
    expect(screen.getByLabelText('고객사*')).toHaveValue('Changed Customer');
    expect(changePanelCountBody).toEqual(expect.objectContaining({
      panelCount: 6,
      expectedActivePanelCount: 4
    }));
  });

  it('requires reasons for hold and cancel dialogs', async () => {
    render(<App />);

    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    await screen.findByText('제품·패널 목록');
    fireEvent.click(screen.getByRole('button', { name: '보류' }));
    fireEvent.click(within(screen.getByRole('dialog', { name: '프로젝트 보류' })).getByRole('button', { name: '확인' }));

    expect(await screen.findByText('사유는 필수입니다.')).toBeInTheDocument();
  });

  it('renders OnHold and Cancelled status badges', async () => {
    render(<App />);

    expect((await screen.findAllByText('보류')).length).toBeGreaterThan(0);
    expect(screen.getAllByText('취소').length).toBeGreaterThan(0);
  });

  it('ignores a stale active tab response after the cancelled tab loads', async () => {
    const active = createDeferred<Response>();
    const cancelled = createDeferred<Response>();
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/projects' && url.searchParams.get('status') === 'Active') {
        return active.promise;
      }

      if (url.pathname === '/api/projects' && url.searchParams.get('status') === 'Cancelled') {
        return cancelled.promise;
      }

      return mockFetch(input, init);
    }));

    render(<App />);
    fireEvent.click(await screen.findByRole('tab', { name: '취소' }));
    cancelled.resolve(projectListResponse([projectListItem('dev-sales', 'Cancelled', 'Race Cancelled', cancelledProjectId)]));

    expect(await screen.findByText('Race Cancelled')).toBeInTheDocument();
    active.resolve(projectListResponse([projectListItem('dev-sales', 'Active', 'Race Active', projectId)]));

    await waitFor(() => expect(screen.queryByText('Race Active')).not.toBeInTheDocument());
    expect(screen.getByRole('tab', { name: '취소' })).toHaveAttribute('aria-selected', 'true');
  });

  it('ignores stale cancelled responses after the deleted archive tab loads', async () => {
    const cancelled = createDeferred<Response>();
    const deleted = createDeferred<Response>();
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/projects' && url.searchParams.get('status') === 'Cancelled') {
        return cancelled.promise;
      }

      if (url.pathname === '/api/deleted-projects') {
        return deleted.promise;
      }

      return mockFetch(input, init);
    }));

    render(<App />);
    fireEvent.click(await screen.findByRole('tab', { name: '취소' }));
    fireEvent.click(await screen.findByRole('tab', { name: '삭제 보관함' }));
    deleted.resolve(projectListResponse([deletedProjectListItem('dev-sales')]));

    expect(await screen.findByText('Deleted Project')).toBeInTheDocument();
    cancelled.resolve(projectListResponse([projectListItem('dev-sales', 'Cancelled', 'Late Cancelled', cancelledProjectId)]));

    await waitFor(() => expect(screen.queryByText('Late Cancelled')).not.toBeInTheDocument());
    expect(screen.getByRole('tab', { name: '삭제 보관함' })).toHaveAttribute('aria-selected', 'true');
  });

  it('keeps the latest search result when an earlier search fails later', async () => {
    const alpha = createDeferred<Response>();
    const beta = createDeferred<Response>();
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/projects' && url.searchParams.get('search') === 'Alpha') {
        return alpha.promise;
      }

      if (url.pathname === '/api/projects' && url.searchParams.get('search') === 'Beta') {
        return beta.promise;
      }

      return mockFetch(input, init);
    }));

    render(<App />);
    const search = await screen.findByPlaceholderText('고객사, Item, PJT Code, PJT Title 검색');
    fireEvent.change(search, { target: { value: 'Alpha' } });
    fireEvent.change(search, { target: { value: 'Beta' } });
    beta.resolve(projectListResponse([projectListItem('dev-sales', 'Active', 'Beta Result', projectId)]));

    expect(await screen.findByText('Beta Result')).toBeInTheDocument();
    alpha.resolve(json({ title: 'stale failure' }, 500));

    await waitFor(() => expect(screen.queryByText('stale failure')).not.toBeInTheDocument());
    expect(screen.queryByText('Alpha Result')).not.toBeInTheDocument();
  });

  it('does not render an error banner for an aborted stale request', async () => {
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/projects' && url.searchParams.get('status') === 'Active') {
        return abortableResponse(init?.signal ?? undefined);
      }

      if (url.pathname === '/api/projects' && url.searchParams.get('status') === 'Cancelled') {
        return Promise.resolve(projectListResponse([projectListItem('dev-sales', 'Cancelled', 'Abort Cancelled', cancelledProjectId)]));
      }

      return mockFetch(input, init);
    }));

    render(<App />);
    fireEvent.click(await screen.findByRole('tab', { name: '취소' }));

    expect(await screen.findByText('Abort Cancelled')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('downloads the xlsx panel template with the selected unit and server filename', async () => {
    let requestedSearch = '';
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === `/api/projects/${projectId}/panel-information/import/template`) {
        requestedSearch = url.search;
        return Promise.resolve(new Response(new Blob(['xlsx']), {
          status: 200,
          headers: {
            'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
            'Content-Disposition': "attachment; filename*=UTF-8''TASK-003A_Demo_Panel_Information_inch.xlsx"
          }
        }));
      }

      return mockFetch(input, init);
    }));

    let downloadedFileName = '';
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function click(this: HTMLAnchorElement) {
      downloadedFileName = this.download;
    });
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-design' } });
    await screen.findByRole('button', { name: '신규 프로젝트' });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('button', { name: '패널정보 수정' }));
    fireEvent.change(await screen.findByLabelText('입력 단위'), { target: { value: 'Inch' } });
    fireEvent.click(screen.getByRole('button', { name: 'Excel 양식 다운로드' }));

    expect(await screen.findByText('Excel 양식을 다운로드했습니다.')).toBeInTheDocument();
    expect(requestedSearch).toBe('?unit=inch');
    expect(clickSpy).toHaveBeenCalled();
    expect(downloadedFileName).toBe('TASK-003A_Demo_Panel_Information_inch.xlsx');
    expect(screen.queryByText(/CSV/i)).not.toBeInTheDocument();
  });

  it('shows panel template download only to panel information editors', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-design' } });
    await screen.findByRole('button', { name: '신규 프로젝트' });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    expect(await screen.findByRole('button', { name: '패널정보 수정' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Excel 양식 다운로드' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '패널정보 수정' }));
    expect(await screen.findByRole('button', { name: 'Excel 양식 다운로드' })).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('개발 사용자'), { target: { value: 'dev-manufacturing' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    await screen.findByText('제품·패널 목록');
    expect(screen.queryByRole('button', { name: '패널정보 수정' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Excel 양식 다운로드' })).not.toBeInTheDocument();
  });

  it('shows a friendly template download server error', async () => {
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === `/api/projects/${projectId}/panel-information/import/template`) {
        return Promise.resolve(json({ title: '양식을 다운로드할 수 없습니다.' }, 500));
      }

      return mockFetch(input, init);
    }));

    render(<App />);
    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-design' } });
    await screen.findByRole('button', { name: '신규 프로젝트' });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('button', { name: '패널정보 수정' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Excel 양식 다운로드' }));

    expect(await screen.findByText('양식을 다운로드할 수 없습니다.')).toBeInTheDocument();
  });

  it('does not send size updates when only the panel name changes after switching the edit unit', async () => {
    const savedRequests: Array<{
      panels: Array<{
        panelNameUpdate?: { isChanged: boolean; value: string | null };
        sizeUpdate?: unknown;
      }>;
    }> = [];
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === `/api/projects/${projectId}/panel-information` && init?.method === 'PATCH') {
        savedRequests.push(JSON.parse(String(init.body)));
        return Promise.resolve(json(panelInformationWithSize(projectId, 'DRIFT-B')));
      }

      if (url.pathname === `/api/projects/${projectId}/panel-information`) {
        return Promise.resolve(json(panelInformationWithSize(projectId, 'DRIFT-A')));
      }

      return mockFetch(input, init);
    }));

    render(<App />);
    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-design' } });
    await screen.findByRole('button', { name: '신규 프로젝트' });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('button', { name: '패널정보 수정' }));
    fireEvent.change(await screen.findByLabelText('입력 단위'), { target: { value: 'Inch' } });
    fireEvent.change(await screen.findByLabelText('No.1 패널명'), { target: { value: 'DRIFT-B' } });
    fireEvent.change(await screen.findByLabelText('수정사유*'), { target: { value: '패널명만 변경' } });
    fireEvent.click(screen.getByRole('button', { name: '직접 입력 저장' }));

    await screen.findByText('패널정보를 저장했습니다.');
    const savedBody = savedRequests[0];
    expect(savedBody.panels).toHaveLength(1);
    expect(savedBody.panels[0].panelNameUpdate).toEqual({ isChanged: true, value: 'DRIFT-B' });
    expect(savedBody.panels[0].sizeUpdate).toBeUndefined();
  });

  it('shows direct, excel, canonical, original input, and legacy panel audit metadata', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-admin' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));

    expect(await screen.findByText('전체 이력')).toBeInTheDocument();
    expect(await screen.findByText('직접 입력 · 대상 패널 1면')).toBeInTheDocument();
    expect(await screen.findByText('Excel 입력 · 대상 패널 1면')).toBeInTheDocument();
    expect(await screen.findByText('기존 이력 · 대상 패널 1면')).toBeInTheDocument();
    expect(screen.getByText('입력 파일: panel_information_01.xlsx')).toBeInTheDocument();
    expect(screen.getAllByText('변경항목 1건').length).toBeGreaterThanOrEqual(3);
    fireEvent.click(screen.getAllByText('변경 상세')[0]);
    expect(screen.getByText('원본 입력값: 31.5 inch')).toBeInTheDocument();
    expect(screen.getByText('입력단위: inch')).toBeInTheDocument();
    expect(screen.getByText('WidthMm: 700 → 800.1')).toBeInTheDocument();
  });
});

function fillCreateForm(projectCode: string, projectTitle: string) {
  fireEvent.change(screen.getByLabelText('고객사*'), { target: { value: 'EMI Test Customer' } });
  fireEvent.change(screen.getByLabelText('Item*'), { target: { value: 'Control Panel' } });
  fireEvent.change(screen.getByLabelText('PJT Code*'), { target: { value: projectCode } });
  fireEvent.change(screen.getByLabelText('PJT Title*'), { target: { value: projectTitle } });
  fireEvent.change(screen.getByLabelText('면수*'), { target: { value: '4' } });
  fireEvent.change(screen.getByLabelText('납기일*'), { target: { value: '2026-10-10' } });
  fireEvent.change(screen.getByLabelText('영업담당자*'), { target: { value: salesOwnerId } });
  fireEvent.change(screen.getByLabelText('포장방식*'), { target: { value: 'WoodenCrate' } });
  fireEvent.change(screen.getByLabelText('판매금액'), { target: { value: '1250000.5' } });
}

async function mockFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const url = new URL(String(input));
  const userKey = readDevUser(init);
  const path = url.pathname;

  if (path === '/health/ready') {
    return json({
      name: 'ready',
      status: 'ok',
      database: { isReady: true, reason: 'reachable' },
      checkedAtUtc: '2026-06-25T00:00:00Z'
    });
  }

  if (path === '/api/me') {
    return json(currentUser(userKey));
  }

  if (path === '/api/sales-owners') {
    return json([{ userId: salesOwnerId, displayName: 'Dev Sales User' }]);
  }

  if (path === '/api/projects' && init?.method === 'POST') {
    const body = JSON.parse(String(init.body)) as { projectTitle: string };
    if (body.projectTitle.toLowerCase().includes('duplicate')) {
      return json({ title: '동일한 PJT Title이 이미 존재합니다.' }, 409);
    }

    await new Promise((resolve) => setTimeout(resolve, 50));
    return json(projectDetail(true, 'Active', body.projectTitle), 201);
  }

  if (path === '/api/projects') {
    const status = url.searchParams.get('status');
    const items = [
      projectListItem(userKey, 'Active', 'TASK-003A Demo', projectId),
      projectListItem(userKey, 'OnHold', 'OnHold Project', onHoldProjectId),
      projectListItem(userKey, 'Completed', 'Completed Project', '71000000-0000-0000-0000-000000000013'),
      projectListItem(userKey, 'Cancelled', 'Cancelled Project', cancelledProjectId)
    ].filter((item) => !status || item.status === status);

    return json({
      items,
      page: 1,
      pageSize: 20,
      totalCount: items.length
    });
  }

  if (path === '/api/deleted-projects') {
    return json({
      items: [deletedProjectListItem(userKey)],
      page: 1,
      pageSize: 20,
      totalCount: 1
    }, canReadDeletedProjects(userKey) ? 200 : 403);
  }

  if (path === `/api/deleted-projects/${projectId}`) {
    return json({
      ...deletedProjectListItem(userKey),
      statusReason: '삭제 전 상태 사유',
      panels: panels(),
      auditHistory: []
    }, canReadDeletedProjects(userKey) ? 200 : 403);
  }

  if (path === `/api/projects/${projectId}` && init?.method === 'PATCH') {
    return json(projectDetail(canReadSalesAmount(userKey), 'Active', 'TASK-003A Demo'));
  }

  if (path === `/api/projects/${projectId}`) {
    return json(projectDetail(canReadSalesAmount(userKey), 'Active', 'TASK-003A Demo'));
  }

  if (path === `/api/projects/${onHoldProjectId}`) {
    return json(projectDetail(canReadSalesAmount(userKey), 'OnHold', 'OnHold Project', onHoldProjectId));
  }

  if (path === `/api/projects/${projectId}/panels`) {
    return json(panels());
  }

  if (path === `/api/projects/${onHoldProjectId}/panels`) {
    return json(panels(onHoldProjectId));
  }

  if (path.startsWith(`/api/projects/${projectId}/panels/`)) {
    return json(panels()[0]);
  }

  if (path === `/api/projects/${projectId}/audit-history`) {
    if (userKey !== 'dev-admin') {
      return json({ title: 'Forbidden' }, 403);
    }

    return json({
      items: [
        {
          auditEventId: '73000000-0000-0000-0000-000000000001',
          entityType: 'Project',
          entityId: projectId,
          projectId,
          action: 'ProjectCreated',
          changedByUserId: salesOwnerId,
          changedByUserName: 'Dev Sales User',
          changedAtUtc: '2026-06-25T00:00:00Z',
          correlationId: 'test'
        }
      ]
    });
  }

  if (path === `/api/projects/${onHoldProjectId}/audit-history`) {
    return json({ items: [] });
  }

  if (path === `/api/projects/${projectId}/panel-information`) {
    return json(panelInformation(projectId));
  }

  if (path === `/api/projects/${onHoldProjectId}/panel-information`) {
    return json(panelInformation(onHoldProjectId));
  }

  if (path === `/api/projects/${projectId}/panel-information/history`
      || path === `/api/projects/${onHoldProjectId}/panel-information/history`) {
    return json(panelInformationHistory(), userKey === 'dev-admin' ? 200 : 403);
  }

  if (path.endsWith('/delete')) {
    return json({
      ...deletedProjectListItem(userKey),
      statusReason: null,
      panels: panels(),
      auditHistory: []
    });
  }

  if (path.includes('/change-panel-count') || path.endsWith('/hold') || path.endsWith('/resume') || path.endsWith('/cancel') || path.endsWith('/reactivate')) {
    return json(projectDetail(canReadSalesAmount(userKey), 'OnHold', 'TASK-003A Demo'));
  }

  return json({ title: 'not found' }, 404);
}

function currentUser(userKey: string) {
  const permissions = ['projects.read', 'Project.Read.All'];
  if (userKey === 'dev-sales') {
    permissions.push('Project.Create', 'Project.Update', 'Project.Hold', 'Project.Cancel', 'Project.Delete', 'Project.Deleted.Read', 'Project.SalesAmount.Read', 'PanelInfo.Update');
  }

  if (userKey === 'dev-design' || userKey === 'dev-production') {
    permissions.push('PanelInfo.Update');
  }

  if (userKey === 'dev-admin') {
    permissions.push('Project.Deleted.Read', 'Project.SalesAmount.Read', 'Audit.Read.All', 'users.manage');
  }

  return {
    developmentUserKey: userKey,
    displayName: userKey,
    department: 'test',
    roles: [userKey.replace('dev-', '')],
    permissions,
    projectAccess: []
  };
}

function projectListItem(userKey: string, status: 'Active' | 'OnHold' | 'Cancelled' | 'Completed', title: string, id = projectId) {
  const item: Record<string, unknown> = {
    projectId: id,
    customerName: 'EMI Test Customer',
    item: 'Control Panel',
    projectCode: 'PJT-003A',
    projectTitle: title,
    activePanelCount: 4,
    deliveryDate: '2026-10-10',
    salesOwnerUserId: salesOwnerId,
    salesOwnerName: 'Dev Sales User',
    packagingMethod: 'WoodenCrate',
    deliveryLocation: 'Dock A',
    status,
    projectWorkStatus: status === 'Active' ? 'BeforeManufacturing' : status,
    projectProgressPercent: status === 'Active' ? 0 : null,
    createdAt: '2026-06-25T00:00:00Z',
    updatedAt: '2026-06-25T00:00:00Z'
  };

  if (canReadSalesAmount(userKey)) {
    item.salesAmount = 1250000.5;
    item.currencyCode = 'KRW';
  }

  return item;
}

function deletedProjectListItem(userKey: string) {
  return {
    ...projectListItem(userKey, 'Cancelled', 'Deleted Project', projectId),
    deletedAtUtc: '2026-06-25T01:00:00Z',
    deletedByUserId: salesOwnerId,
    deletedByUserName: 'Dev Sales User',
    deleteReason: '오등록 정리'
  };
}

function projectDetail(
  includeSalesAmount: boolean,
  status: 'Active' | 'OnHold' | 'Cancelled',
  title: string,
  id = projectId
) {
  return {
    ...projectListItem(includeSalesAmount ? 'dev-sales' : 'dev-manufacturing', status, title, id),
    qrEligibleCount: 0,
    manufacturingCompletedCount: 0,
    inspectionCompletedCount: 0,
    statusReason: status === 'Active' ? null : '상태 사유'
  };
}

function panels(id = projectId) {
  return panelIds.map((panelId, index) => ({
    panelId,
    projectId: id,
    sequenceNumber: index + 1,
    displayCode: `P0${index + 1}`,
    panelName: null,
    width: null,
    height: null,
    depth: null,
    panelStatus: 'Active',
    workflowStage: 'BeforeManufacturing',
    panelInfoCompleted: false,
    qrEligible: false,
    createdAt: '2026-06-25T00:00:00Z',
    updatedAt: '2026-06-25T00:00:00Z'
  }));
}

function panelInformation(id = projectId) {
  return {
    projectId: id,
    projectStatus: id === onHoldProjectId ? 'OnHold' : 'Active',
    packagingMethod: 'WoodenCrate',
    activePanelCount: 4,
    panelInfoCompletedCount: 0,
    panelInfoPendingCount: 4,
    qrEligibleCount: 0,
    manufacturingCompletedCount: 0,
    inspectionCompletedCount: 0,
    duplicatePanelNameGroupCount: 0,
    projectPanelInformationCompleted: false,
    panelInformationStatusMessage: null,
    panels: panelIds.map((panelId, index) => ({
      panelId,
      projectId: id,
      sequenceNumber: index + 1,
      panelNumber: `No.${index + 1}`,
      displayCode: `P0${index + 1}`,
      panelName: null,
      displayName: `No.${index + 1} · 패널명 미입력`,
      widthMm: null,
      heightMm: null,
      depthMm: null,
      panelStatus: 'Active',
      workflowStage: 'BeforeManufacturing',
      panelInfoCompleted: false,
      qrEligible: false,
      hasDuplicateName: false,
      duplicateNameCount: 0,
      panelInfoVersion: 0,
      createdAt: '2026-06-25T00:00:00Z',
      updatedAt: '2026-06-25T00:00:00Z',
      panelInfoUpdatedAtUtc: null,
      panelInfoUpdatedByUserId: null,
      panelInfoUpdatedByUserName: null
    }))
  };
}

function panelInformationWithSize(id = projectId, panelName = 'DRIFT-A') {
  const response = panelInformation(id);
  Object.assign(response.panels[0] as unknown as Record<string, unknown>, {
    panelName,
    displayName: `No.1 · ${panelName}`,
    widthMm: 800,
    heightMm: 1800,
    depthMm: 400,
    panelInfoVersion: 2,
    panelInfoCompleted: true,
    qrEligible: true
  });
  return response;
}

function panelInformationHistory() {
  return {
    groups: [
      {
        groupId: 'import:91000000000000000000000000000001',
        actionType: 'PanelInfoUpdated',
        inputSource: 'Excel',
        changedByUserId: salesOwnerId,
        changedByName: 'Dev Design User',
        changedAtUtc: '2026-06-26T01:30:00Z',
        reason: 'Excel inch 변경',
        importBatchId: '91000000-0000-0000-0000-000000000001',
        importFileName: 'panel_information_01.xlsx',
        importUploadedAtUtc: '2026-06-26T01:29:00Z',
        affectedPanelCount: 1,
        changeCount: 1,
        changes: [
          {
            entityType: 'Panel',
            entityId: panelIds[1],
            panelNumber: 'No.2',
            panelDisplayName: 'No.2 · PNL-2',
            displayCode: 'P02',
            fieldName: 'WidthMm',
            oldValue: '700',
            newValue: '800.1',
            inputUnit: 'Inch',
            originalInputValue: '31.5'
          }
        ]
      },
      {
        groupId: 'correlation:corr-direct',
        actionType: 'PanelInfoUpdated',
        inputSource: 'Direct',
        changedByUserId: salesOwnerId,
        changedByName: 'Dev Design User',
        changedAtUtc: '2026-06-26T01:00:00Z',
        reason: '직접 입력',
        importBatchId: null,
        importFileName: null,
        importUploadedAtUtc: null,
        affectedPanelCount: 1,
        changeCount: 1,
        changes: [
          {
            entityType: 'Panel',
            entityId: panelIds[0],
            panelNumber: 'No.1',
            panelDisplayName: 'No.1 · PNL-1',
            displayCode: 'P01',
            fieldName: 'PanelName',
            oldValue: '',
            newValue: 'PNL-1',
            inputUnit: 'Mm',
            originalInputValue: null
          }
        ]
      },
      {
        groupId: 'correlation:corr-legacy',
        actionType: 'PanelInfoUpdated',
        inputSource: null,
        changedByUserId: null,
        changedByName: null,
        changedAtUtc: '2026-06-26T00:30:00Z',
        reason: null,
        importBatchId: null,
        importFileName: null,
        importUploadedAtUtc: null,
        affectedPanelCount: 1,
        changeCount: 1,
        changes: [
          {
            entityType: 'Panel',
            entityId: panelIds[2],
            panelNumber: 'No.3',
            panelDisplayName: 'No.3 · PNL-LEGACY',
            displayCode: 'P03',
            fieldName: 'PanelName',
            oldValue: '',
            newValue: 'PNL-LEGACY',
            inputUnit: null,
            originalInputValue: null
          }
        ]
      }
    ],
    auditEvents: [
      {
        auditEventId: '90000000-0000-0000-0000-000000000001',
        entityType: 'Panel',
        entityId: panelIds[1],
        projectId,
        action: 'PanelInfoUpdated',
        panelNumber: 'No.2',
        panelDisplayName: 'No.2 · PNL-2',
        displayCode: 'P02',
        fieldName: 'WidthMm',
        oldValue: '700',
        newValue: '800.1',
        reason: 'Excel inch 변경',
        changedByUserId: salesOwnerId,
        changedByUserName: 'Dev Design User',
        changedAtUtc: '2026-06-26T01:30:00Z',
        correlationId: 'corr-excel',
        inputSource: 'Excel',
        importBatchId: '91000000-0000-0000-0000-000000000001',
        inputUnit: 'Inch',
        originalInputValue: '31.5',
        importFileName: 'panel_information_01.xlsx',
        importUploadedAtUtc: '2026-06-26T01:29:00Z'
      },
      {
        auditEventId: '90000000-0000-0000-0000-000000000002',
        entityType: 'Panel',
        entityId: panelIds[0],
        projectId,
        action: 'PanelInfoUpdated',
        panelNumber: 'No.1',
        panelDisplayName: 'No.1 · PNL-1',
        displayCode: 'P01',
        fieldName: 'PanelName',
        oldValue: '',
        newValue: 'PNL-1',
        changedByUserId: salesOwnerId,
        changedByUserName: 'Dev Design User',
        changedAtUtc: '2026-06-26T01:00:00Z',
        correlationId: 'corr-direct',
        inputSource: 'Direct',
        inputUnit: 'Mm'
      },
      {
        auditEventId: '90000000-0000-0000-0000-000000000003',
        entityType: 'Panel',
        entityId: panelIds[2],
        projectId,
        action: 'PanelInfoUpdated',
        panelNumber: 'No.3',
        panelDisplayName: 'No.3 · PNL-LEGACY',
        displayCode: 'P03',
        fieldName: 'PanelName',
        oldValue: '',
        newValue: 'PNL-LEGACY',
        changedByUserId: null,
        changedByUserName: null,
        changedAtUtc: '2026-06-26T00:30:00Z',
        correlationId: 'corr-legacy'
      }
    ],
    excelImportBatches: [
      {
        importBatchId: '91000000-0000-0000-0000-000000000001',
        projectId,
        originalFileName: 'panel_information_01.xlsx',
        fileSizeBytes: 1234,
        fileSha256: 'a'.repeat(64),
        inputUnit: 'Inch',
        totalRowCount: 1,
        newPanelCount: 0,
        changedPanelCount: 1,
        unchangedPanelCount: 0,
        skippedPanelCount: 0,
        uploadedByUserId: salesOwnerId,
        uploadedByUserName: 'Dev Design User',
        uploadedAtUtc: '2026-06-26T01:29:00Z',
        reason: 'Excel inch 변경'
      }
    ]
  };
}

function canReadSalesAmount(userKey: string) {
  return userKey === 'dev-sales' || userKey === 'dev-admin';
}

function canReadDeletedProjects(userKey: string) {
  return userKey === 'dev-sales' || userKey === 'dev-admin';
}

function mockMobileViewport(matches: boolean) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches,
      media: query,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn()
    }))
  });
}

function readDevUser(init?: RequestInit) {
  const headers = init?.headers;
  if (headers instanceof Headers) {
    return headers.get('X-Dev-User') ?? 'dev-sales';
  }

  return 'dev-sales';
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}

function projectListResponse(items: unknown[]) {
  return json({
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length
  });
}

function abortableResponse(signal?: AbortSignal) {
  return new Promise<Response>((resolve, reject) => {
    if (signal?.aborted) {
      reject(new DOMException('The operation was aborted.', 'AbortError'));
      return;
    }

    signal?.addEventListener('abort', () => {
      reject(new DOMException('The operation was aborted.', 'AbortError'));
    }, { once: true });

    setTimeout(() => {
      resolve(projectListResponse([projectListItem('dev-sales', 'Active', 'Should Not Render', projectId)]));
    }, 100);
  });
}

function createDeferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((innerResolve, innerReject) => {
    resolve = innerResolve;
    reject = innerReject;
  });

  return { promise, resolve, reject };
}
