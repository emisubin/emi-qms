import { fireEvent, render, screen, cleanup } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { OsanQrPrintDialog } from '../src/OsanQrPrintDialog';
import { ApiError, getOsanProject, fetchBlob } from '../src/api';
vi.mock('../src/api', async original => ({ ...await original<typeof import('../src/api')>(), getOsanProject: vi.fn(), fetchBlob: vi.fn() }));
const project = { projectId: 'a', title: '검수 장비', projectCode: 'A', customerName: '샘플 고객사', productName: 'Power Rack', quantity: 1, deliveryDate: '2026-09-22', poNumber: 'PO-A', workOrderNumber: 'WO-A', status: 'InProgress', createdAtUtc: '', completedStepCount: 1, totalStepCount: 14, targets: [1,2].map(i => ({ targetId: 't'+i, sequenceNumber:i, displayName:'패널 '+i, status:'NotStarted', steps:[] })) };
beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function() { this.setAttribute('open', ''); };
  HTMLDialogElement.prototype.close = function() { this.removeAttribute('open'); };
  vi.mocked(getOsanProject).mockImplementation(async (_key, id) => ({ ...project, projectId: id }));
  vi.mocked(fetchBlob).mockResolvedValue(new Blob(['svg']));
  URL.createObjectURL = vi.fn(()=>'blob:synthetic'); URL.revokeObjectURL = vi.fn();
});
afterEach(()=>{cleanup();vi.resetAllMocks();});
it('중복 프로젝트를 제거하고 30/50mm 라벨 내부에 3줄을 제공한다',async()=>{
  const {container}=render(<OsanQrPrintDialog projectIds={['a','b','a']} onClose={()=>{}}/>);
  expect(await screen.findByRole('button',{name:'4장 QR 인쇄'})).toBeEnabled();expect(fetchBlob).toHaveBeenCalledTimes(4);
  const labels=container.querySelectorAll<HTMLElement>('.osan-qr-label');expect(labels).toHaveLength(4);expect(labels[0].style.getPropertyValue('--label-size')).toBe('30mm');expect(labels[0].querySelectorAll('p')).toHaveLength(3);
  expect(labels[0]).toHaveTextContent('검수 장비Power RackWO-A');expect(vi.mocked(fetchBlob).mock.calls.map(call=>call[0])).toEqual(['/api/osan/projects/a/targets/t1/qr?format=svg','/api/osan/projects/a/targets/t2/qr?format=svg','/api/osan/projects/b/targets/t1/qr?format=svg','/api/osan/projects/b/targets/t2/qr?format=svg']);
  fireEvent.click(screen.getAllByLabelText('검수 장비 · 패널 1')[0]);expect(screen.getByRole('button',{name:'3장 QR 인쇄'})).toBeEnabled();
  fireEvent.click(screen.getByLabelText('50 × 50mm'));expect(labels[0].style.getPropertyValue('--label-size')).toBe('50mm');
});
it('일부 QR 조회 실패 시 누락한 채 인쇄하지 않는다',async()=>{
  vi.mocked(fetchBlob).mockResolvedValueOnce(new Blob(['svg'])).mockRejectedValueOnce(new ApiError(403,'denied'));
  render(<OsanQrPrintDialog projectIds={['a','b']} onClose={()=>{}}/>);expect(await screen.findByRole('alert')).toHaveTextContent('모두 준비하지 못했습니다');expect(screen.getByRole('button',{name:'0장 QR 인쇄'})).toBeDisabled();
});

it.each([[30,2],[50,3]] as const)('명시적인 인쇄 페이지로 %smm 라벨 잘림을 방지한다', async(size,count)=>{
  const {populateQrPrintDocument}=await import('../src/osanQrPrint');
  const doc=document.implementation.createHTMLDocument();
  const labels=Array.from({length:count},(_,i)=>{const div=document.createElement('div');div.className='osan-qr-label';div.textContent=String(i+1);return div});
  populateQrPrintDocument(doc,labels,size);
  expect([...doc.querySelectorAll('.sheet')].map(s=>s.children.length)).toEqual(Array(count).fill(1));
  expect(doc.querySelectorAll('.osan-qr-label')).toHaveLength(count);
  expect(doc.head.textContent).toContain('@page{size:'+size+'mm '+size+'mm;margin:0}');
  expect(doc.head.textContent).not.toContain('A4');
});
