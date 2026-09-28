import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OsanNotificationsPage } from '../src/OsanNotificationsPage';
import { osanNotificationPresentation } from '../src/osanNotificationPresentation';
import type { NotificationItem } from '../src/projects';
import { getNotificationSummary, listNotifications, markAllNotificationsRead, markNotificationRead } from '../src/api';
vi.mock('../src/api', () => ({ getNotificationSummary: vi.fn(), listNotifications: vi.fn(), markAllNotificationsRead: vi.fn(), markNotificationRead: vi.fn(), exportOsanNotificationsExcel: vi.fn() }));
vi.mock('../src/SelectedExcelExport', () => ({ SelectedExportTray: () => null, SelectionCheckbox: () => null }));
const item: NotificationItem = { notificationId:'n1',projectId:'p1',projectTitle:'테스트 장비',projectCode:'C1',projectItem:'Rack',workItemId:null,workItemTitle:null,workflowStageCode:null,workflowStageName:'배선검사',notificationType:'Info',notificationTypeLabel:'알림',severity:'Normal',severityLabel:'일반',visibilityScope:'User',visibilityScopeLabel:'개인',sourceKind:'Workflow',sourceKindLabel:'공정',title:'공정 진행 요청',message:'김관리님이 공정 진행을 요청했습니다.\n요청 내용: 배선검사 부탁드립니다.',linkUrl:'/progress?projectId=p1&targetId=t1&stage=3',createdAtUtc:'2026-09-28T01:00:00Z',readAtUtc:null };
let scope=0;
const props = () => ({ developmentUserKey:'user',scopeKey:`test-${++scope}`,onOpen:vi.fn(),onBadgeRefresh:vi.fn() });
beforeEach(() => { vi.resetAllMocks(); vi.stubGlobal('scrollTo',vi.fn()); vi.mocked(getNotificationSummary).mockResolvedValue({unreadCount:1,blockingCount:0}); vi.mocked(listNotifications).mockResolvedValue({items:[item]}); vi.mocked(markNotificationRead).mockResolvedValue({...item,readAtUtc:'2026-09-28T02:00:00Z'}); });
afterEach(() => {cleanup();vi.unstubAllGlobals();});
describe('Osan notification C', () => {
 it('defaults unread, keeps tab order, and directly opens the exact target after reading',async () => {
  const p=props();render(<OsanNotificationsPage {...p}/>);
  const tabs=screen.getAllByRole('tab');expect(tabs.map(tab=>tab.textContent?.replace(/\d/g,''))).toEqual(['읽지 않음','읽음','전체']);
  await screen.findByText('테스트 장비');expect(listNotifications).toHaveBeenCalledWith('user','unread');
  fireEvent.click(screen.getByRole('button',{name:/테스트 장비.*Rack/}));
  await waitFor(()=>expect(p.onOpen).toHaveBeenCalledWith('p1',item.linkUrl));
  expect(markNotificationRead).toHaveBeenCalledWith('user','n1');expect(screen.queryByRole('dialog')).toBeNull();
 });
 it('does not navigate on failed read, and allows retry',async () => {
  const p=props();vi.mocked(markNotificationRead).mockRejectedValueOnce(new Error('failed'));render(<OsanNotificationsPage {...p}/>);
  await screen.findByText('테스트 장비');fireEvent.click(screen.getByRole('button',{name:/테스트 장비.*Rack/}));
  await screen.findByRole('alert');expect(p.onOpen).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button',{name:/테스트 장비.*Rack/}));await waitFor(()=>expect(p.onOpen).toHaveBeenCalledOnce());
 });
 it('clears old rows when a different tab fails to load',async () => {
  render(<OsanNotificationsPage {...props()}/>);await screen.findByText('테스트 장비');vi.mocked(listNotifications).mockRejectedValueOnce(new Error('failed'));
  fireEvent.click(screen.getByRole('tab',{name:'읽음'}));await screen.findByRole('alert');expect(screen.queryByText('테스트 장비')).toBeNull();
 });
 it('ignores completion after leaving the page',async () => {
  let resolve!:(value:NotificationItem)=>void;vi.mocked(markNotificationRead).mockImplementation(()=>new Promise(r=>{resolve=r;}));
  const p=props();const rendered=render(<OsanNotificationsPage {...p}/>);await screen.findByText('테스트 장비');fireEvent.click(screen.getByRole('button',{name:/테스트 장비.*Rack/}));rendered.unmount();
  await act(async()=>resolve(item));expect(p.onOpen).not.toHaveBeenCalled();expect(p.onBadgeRefresh).not.toHaveBeenCalled();
 });
 it('restores filters after opening and coming back, scoped to the user',async () => {
  const p=props();let rendered=render(<OsanNotificationsPage {...p}/>);await screen.findByText('테스트 장비');
  fireEvent.click(screen.getByRole('tab',{name:'전체'}));await waitFor(()=>expect(listNotifications).toHaveBeenLastCalledWith('user',undefined));await screen.findByText('테스트 장비');
  fireEvent.change(screen.getByRole('textbox',{name:'알림 검색'}),{target:{value:'테스트'}});fireEvent.click(screen.getByRole('button',{name:'검색'}));fireEvent.click(screen.getByRole('button',{name:/^필터/}));fireEvent.click(screen.getByRole('button',{name:/알림 유형 필터/}));fireEvent.click(screen.getByRole('checkbox',{name:'공정 진행 요청'}));fireEvent.click(screen.getByRole('button',{name:'적용'}));
  fireEvent.click(screen.getByRole('button',{name:/테스트 장비.*Rack/}));await waitFor(()=>expect(p.onOpen).toHaveBeenCalled());rendered.unmount();
  rendered=render(<OsanNotificationsPage {...p}/>);expect(screen.getByRole('tab',{name:'전체'})).toHaveAttribute('aria-selected','true');expect(screen.getByRole('textbox')).toHaveValue('테스트');fireEvent.click(screen.getByRole('button',{name:/^필터/}));expect(screen.getByRole('button',{name:'알림 유형 필터: 공정 진행 요청'})).toBeInTheDocument();rendered.unmount();
  render(<OsanNotificationsPage {...props()}/>);expect(screen.getByRole('textbox')).toHaveValue('');expect(screen.getByRole('tab',{name:/읽지 않음/})).toHaveAttribute('aria-selected','true');
 });
 it('represents all eight approved types and prioritizes comments',()=>{
  const titles=['프로젝트 등록','Gate 완료','단계 반려','사진 수정 완료','공정 이상 발생','조치 완료','프로젝트 완료','공정 진행 요청'];
  expect(titles.map(title=>osanNotificationPresentation({...item,title}).kind)).toEqual([0,1,2,3,4,5,6,7]);
  expect(osanNotificationPresentation(item).excerpt).toBe('요청 내용: 배선검사 부탁드립니다.');
 });
 it('refreshes rows and badge after read all',async()=>{
  vi.mocked(markAllNotificationsRead).mockResolvedValue({unreadCount:0,blockingCount:0});const p=props();render(<OsanNotificationsPage {...p}/>);await screen.findByText('테스트 장비');fireEvent.click(screen.getByRole('button',{name:'전체 읽음'}));await waitFor(()=>expect(p.onBadgeRefresh).toHaveBeenCalledOnce());expect(markAllNotificationsRead).toHaveBeenCalledWith('user');
 });
});

it('공통 필터에서 검색·복수 선택·적용·취소·초기화를 제공한다', async () => {
 vi.mocked(listNotifications).mockResolvedValue({items:[item,{...item,notificationId:'n2',title:'Gate 완료',projectTitle:'완료 장비'},{...item,notificationId:'n3',title:'공정 이상 발생',projectTitle:'이상 장비'}]});
 render(<OsanNotificationsPage {...props()}/>);await screen.findByText('테스트 장비');
 fireEvent.click(screen.getByRole('button',{name:/^필터/}));
 fireEvent.click(screen.getByRole('button',{name:/알림 유형 필터/}));
 const dialog=screen.getByRole('dialog',{name:'알림 유형 선택'});
 fireEvent.change(within(dialog).getByRole('searchbox'),{target:{value:'공정'}});
 fireEvent.click(within(dialog).getByRole('button',{name:'검색 결과 전체 선택'}));
 expect(screen.getByText('완료 장비')).toBeInTheDocument();
 fireEvent.click(within(dialog).getByRole('button',{name:'적용'}));
 expect(screen.queryByText('완료 장비')).toBeNull();expect(screen.getByText('이상 장비')).toBeInTheDocument();
 fireEvent.click(screen.getByRole('button',{name:/알림 유형 필터/}));
 fireEvent.click(screen.getByRole('button',{name:'검색 결과 선택 해제'}));
 fireEvent.click(screen.getByRole('button',{name:'취소'}));
 expect(screen.queryByText('완료 장비')).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'초기화'}));expect(screen.getByText('완료 장비')).toBeInTheDocument();
});
