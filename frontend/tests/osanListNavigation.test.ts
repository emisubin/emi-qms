import { beforeEach, expect, it } from 'vitest';
import { clearOsanListSnapshots, createOsanListNavigation, emptyOsanListFilters, readOsanListSnapshot, saveOsanListSnapshot } from '../src/osanListState';
beforeEach(clearOsanListSnapshots);
function saved() { saveOsanListSnapshot('test', { filters: { ...emptyOsanListFilters(), customers: ['customer'], page: 3 }, draft: '장비', scrollY: 200 }); }
it.each(['home','list','osan-progress'])('%s 내부 상세와 복귀는 유지하고 다른 메뉴 방문은 초기화한다', kind => {
 const navigate = createOsanListNavigation({kind}); saved();
 navigate(kind === 'osan-progress' ? {kind, projectId:'project'} : {kind:'detail',projectId:'project'});
 navigate({kind}); expect(readOsanListSnapshot('test')?.filters.page).toBe(3);
 navigate({kind:'notice-board'}); expect(readOsanListSnapshot('test')).toBeUndefined();
 navigate({kind}); expect(readOsanListSnapshot('test')).toBeUndefined();
});
it('홈에서 프로젝트 또는 진행현황 메뉴로 이동해도 초기화한다',()=>{
 const navigate=createOsanListNavigation({kind:'home'}); saved(); navigate({kind:'list'}); expect(readOsanListSnapshot('test')).toBeUndefined();
 saved();navigate({kind:'osan-progress'});expect(readOsanListSnapshot('test')).toBeUndefined();
});
