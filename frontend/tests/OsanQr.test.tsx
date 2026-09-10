import { fireEvent, render, screen, within, cleanup } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { OsanQrPage } from '../src/OsanQrPage';
import { OsanQrPrintDialog } from '../src/OsanQrPrintDialog';
import { ApiError, getOsanProject, fetchBlob } from '../src/api';
import { getOsanProgress, osanStageNames } from '../src/osanProgress';
vi.mock('../src/api', async original => ({ ...await original<typeof import('../src/api')>(), getOsanProject: vi.fn(), fetchBlob: vi.fn() }));
vi.mock('../src/osanProgress', async original => ({ ...await original<typeof import('../src/osanProgress')>(), getOsanProgress: vi.fn() }));
const project = { projectId: 'a', title: '검수 장비', projectCode: 'A', customerName: '샘플 고객사', productName: 'Power Rack', quantity: 1, deliveryDate: '2026-09-22', poNumber: 'PO-A', workOrderNumber: 'WO-A', status: 'InProgress', createdAtUtc: '', completedStepCount: 1, totalStepCount: 7, targets: [] };
beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function() { this.setAttribute('open', ''); };
  HTMLDialogElement.prototype.close = function() { this.removeAttribute('open'); };
  vi.mocked(getOsanProject).mockImplementation(async (_key, id) => ({ ...project, projectId: id }));
  vi.mocked(getOsanProgress).mockResolvedValue({ projectId: 'a', title: project.title, projectCode: 'A', status: 'InProgress', completedStepCount: 1, totalStepCount: 7, targets: [{ targetId: 't', sequenceNumber: 1, displayName: '패널 1', status: 'InProgress', version: 1, startedAtUtc: null, startedByUserId: null, startedByDisplayName: null, steps: osanStageNames.map((stepName,i) => ({ stepId: String(i), sequenceNumber: i+1, stepCode: String(i), stepName, status: i===0?'Completed':'NotStarted', startedAtUtc: null, completedByUserId: 'u', completedByDisplayName: '검수 작업자', completedAtUtc: i===0?'2026-09-10T01:24:00Z':null, canCompleteIndividual:false, canCompleteBatch:false, guidanceDescription:null, guidancePhotos:[], photos:[] })) }] });
  vi.mocked(fetchBlob).mockResolvedValue(new Blob(['svg']));
  URL.createObjectURL = vi.fn(()=>'blob:synthetic'); URL.revokeObjectURL = vi.fn();
});
afterEach(()=>{cleanup();vi.resetAllMocks();});
it('7단계와 요약, 완료/미완료 기록을 조회 전용으로 제공한다',async()=>{
  render(<OsanQrPage projectId="a"/>);
  await screen.findByRole('heading',{name:project.title});
  expect(screen.getByRole('progressbar')).toHaveAttribute('value','14');
  expect(screen.getAllByRole('button',{name:/패널 1 .* 기록/})).toHaveLength(7);
  fireEvent.click(screen.getByRole('button',{name:'패널 1 입고검사 완료 기록'}));
  const modal=screen.getByRole('dialog'); expect(within(modal).getByText('검수 작업자')).toBeInTheDocument();
  expect(within(modal).queryByRole('button',{name:'완료'})).not.toBeInTheDocument();
  fireEvent.click(within(modal).getByRole('button',{name:'닫기'}));
  fireEvent.click(screen.getByRole('button',{name:'패널 1 배치검사 미완료 기록'}));
  expect(screen.getByText('아직 완료 기록이 없습니다.')).toBeInTheDocument();
});
it('조회 거부 시 데이터 대신 권한 오류를 제공한다',async()=>{
  vi.mocked(getOsanProject).mockRejectedValue(new ApiError(403,'denied')); render(<OsanQrPage projectId="a"/>);
  expect(await screen.findByRole('alert')).toHaveTextContent('권한이 없습니다'); expect(screen.queryByText(project.title)).not.toBeInTheDocument();
});
it('중복 프로젝트를 제거하고 30/50mm 라벨 내부에 3줄을 제공한다',async()=>{
  const {container}=render(<OsanQrPrintDialog projectIds={['a','b','a']} onClose={()=>{}}/>);
  expect(await screen.findByRole('button',{name:'2개 QR 인쇄'})).toBeEnabled();expect(fetchBlob).toHaveBeenCalledTimes(2);
  const labels=container.querySelectorAll<HTMLElement>('.osan-qr-label');expect(labels).toHaveLength(2);expect(labels[0].style.getPropertyValue('--label-size')).toBe('30mm');expect(labels[0].querySelectorAll('p')).toHaveLength(3);
  expect(labels[0]).toHaveTextContent('검수 장비Power RackWO-A');fireEvent.click(screen.getByLabelText('50 × 50mm'));expect(labels[0].style.getPropertyValue('--label-size')).toBe('50mm');
});
it('일부 QR 조회 실패 시 누락한 채 인쇄하지 않는다',async()=>{
  vi.mocked(fetchBlob).mockResolvedValueOnce(new Blob(['svg'])).mockRejectedValueOnce(new ApiError(403,'denied'));
  render(<OsanQrPrintDialog projectIds={['a','b']} onClose={()=>{}}/>);expect(await screen.findByRole('alert')).toHaveTextContent('모두 준비하지 못했습니다');expect(screen.getByRole('button',{name:'0개 QR 인쇄'})).toBeDisabled();
});

it.each([[30,45,[40,5]],[50,18,[15,3]]] as const)('명시적인 인쇄 페이지로 %smm 라벨 잘림을 방지한다', async(size,count,expected)=>{
  const {populateQrPrintDocument}=await import('../src/osanQrPrint');
  const doc=document.implementation.createHTMLDocument();
  const labels=Array.from({length:count},(_,i)=>{const div=document.createElement('div');div.className='osan-qr-label';div.textContent=String(i+1);return div});
  populateQrPrintDocument(doc,labels,size);
  expect([...doc.querySelectorAll('.sheet')].map(s=>s.children.length)).toEqual(expected);
  expect(doc.querySelectorAll('.osan-qr-label')).toHaveLength(count);
});
