import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { BusbarInspection } from '../src/BusbarInspection';
import { busbarApi, type BusbarProduct } from '../src/interiorBusbar';
vi.mock('../src/interiorBusbar', async original => ({ ...await original<typeof import('../src/interiorBusbar')>(), busbarApi: { write: vi.fn() } }));
afterEach(() => { cleanup(); vi.clearAllMocks(); });
const product: BusbarProduct = { id: 'panel', productFamilyId: 'family', workerId: null, workerName: null, status: 'Complete', hasFront: true, hasBack: true, revision: 2 };
const run = async (action: () => Promise<unknown>) => { await action(); return true; };
it('quality can complete an inspection without entering photos or comment', async () => {
 render(<BusbarInspection user="quality" product={product} canInspect busy={false} run={run}/>);
 expect(screen.getByText('검사 미완료')).toBeTruthy();
 fireEvent.click(screen.getByRole('button', { name: '검사 완료' }));
 await waitFor(() => expect(busbarApi.write).toHaveBeenCalledWith('quality', '/products/panel/inspection', {}));
 expect(screen.queryByRole('textbox')).toBeNull();
});
it('non-quality users can read status but cannot inspect', () => {
 render(<BusbarInspection user="production" product={product} canInspect={false} busy={false} run={run}/>);
 expect(screen.getByText('검사 미완료')).toBeTruthy(); expect(screen.queryByRole('button')).toBeNull();
});
it('completed inspection displays the recorded person and time without a second action', () => {
 render(<BusbarInspection user="quality" product={{...product, inspectedAtUtc:'2026-09-21T03:00:00Z', inspectedByDisplayName:'합성 검사자'}} canInspect busy={false} run={run}/>);
 expect(screen.getByText(/합성 검사자/)).toBeTruthy(); expect(screen.queryByRole('button')).toBeNull();
});
it('draft has no inspection action and legacy shipped products do not invite inspection', () => {
 const view=render(<BusbarInspection user="quality" product={{...product,status:'Draft'}} canInspect busy={false} run={run}/>);
 expect(screen.queryByRole('region')).toBeNull();
 view.rerender(<BusbarInspection user="quality" product={{...product,isShipped:true}} canInspect busy={false} run={run}/>);
 expect(screen.getByText('검사 기록 없음')).toBeTruthy(); expect(screen.queryByRole('button')).toBeNull();
});
it('saving disables the completion action', () => {
 render(<BusbarInspection user="quality" product={product} canInspect busy run={run}/>);
 expect((screen.getByRole('button') as HTMLButtonElement).disabled).toBe(true);
});
