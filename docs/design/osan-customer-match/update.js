// Static visual demonstration only; no API, deployment or persistent account state.
(() => {
  const states = {
    active: {title:'PMS 업데이트 중입니다',banner:'업데이트 중 · 18:00~18:20 저장이 잠시 제한됩니다.',intro:'더 나은 서비스 제공을 위해 업데이트를 진행하고 있습니다.',time:'9월 24일(목) 18:00 ~ 18:20 (예정)',note:'조회는 가능합니다. 작성 중인 내용은 유지되며, 업데이트가 끝난 뒤 직접 저장해 주세요.',locked:true},
    scheduled: {title:'PMS 업데이트 예정 안내',banner:'업데이트 예정 · 오늘 18:00~18:20 저장이 잠시 제한됩니다.',intro:'아래 시간 동안 업데이트가 예정되어 있습니다. 진행 중인 작업은 시작 전에 저장해 주세요.',time:'9월 24일(목) 18:00 ~ 18:20 (예정)',note:'업데이트 중에는 등록·수정·삭제가 제한됩니다. 완료되면 저장을 다시 이용할 수 있습니다.',locked:false},
    delayed: {title:'PMS 업데이트가 지연되고 있습니다',banner:'업데이트 지연 · 완료 확인 전까지 저장 제한이 유지됩니다.',intro:'예정된 종료 시간이 지났지만 업데이트 확인이 진행 중입니다.',time:'9월 24일(목) 18:00부터 · 완료 시까지',note:'조회와 작성 중인 내용은 유지됩니다. 정상화가 확인되면 안내드리겠습니다.',locked:true},
    complete: {title:'PMS 업데이트가 완료되었습니다',banner:'업데이트 완료 · 이제 저장할 수 있습니다.',intro:'업데이트 및 정상 동작 확인이 완료되어 저장 제한이 해제되었습니다.',time:'9월 24일(목) 18:00 ~ 18:20',note:'작성 중이던 내용을 확인한 후 직접 저장해 주세요. 자동으로 저장하거나 새로고침하지 않습니다.',locked:false}
  };
  const notice={id:13,title:'PMS 업데이트 및 저장 제한 안내',author:'시스템',date:'2026.09.24',pin:true,popup:false,read:false,files:[],body:''};
  posts.unshift(notice);render();
  const popup=$('update-popup');let state='active',returnFocus=null;
  const mutations=['write','write-bottom','edit','delete','save-settings','repeat'];
  function show(){returnFocus=document.activeElement;popup.hidden=false;}
  function close(){const wasInside=popup.contains(document.activeElement);popup.hidden=true;if(wasInside)(returnFocus?.isConnected&&returnFocus!==document.body?returnFocus:$('update-banner-open')).focus();}
  function apply(){
    const s=states[state];$('update-title').textContent=s.title;$('update-banner-copy').textContent=s.banner;$('update-intro').textContent=s.intro;$('update-time').textContent=s.time;$('update-note').textContent=s.note;
    $('update-time-label').textContent=state==='complete'?'업데이트 시간':'저장 제한 시간';
    notice.body=`${s.intro}\n\n${$('update-time-label').textContent}\n${s.time}\n\n이번 업데이트\n1. 고객사별 알림 담당자 설정\n2. Gate별 완료 가능 부서 설정\n3. 조회 필터 개선 및 공지사항 추가\n\n${s.note}`;
    mutations.forEach(id=>{$(id).disabled=s.locked;});
    const save=$('editor').querySelector('button.brand');save.disabled=s.locked;save.textContent=s.locked?'업데이트 중 · 저장 제한':'등록';
    $('feedback').textContent=s.locked?'업데이트 중에는 등록·수정·삭제할 수 없습니다. 입력한 내용은 그대로 유지됩니다.':'';
    if(!$('detail').hidden&&selected===notice) detail(notice);
    show();
  }
  $('update-state').onchange=e=>{state=e.target.value;apply();};
  $('update-close').onclick=close;
  popup.addEventListener('keydown',e=>{if(e.key==='Escape'){e.preventDefault();close();}});
  $('update-banner-open').onclick=()=>{show();$('update-close').focus();};
  $('update-detail').onclick=()=>{close();detail(notice);$('detail-title').tabIndex=-1;$('detail-title').focus();};
  $('update-draft').onclick=()=>{editor();$('title-input').value='작업 내용 확인 요청';$('body-input').value='작성 중인 내용을 업데이트가 끝난 뒤 이어서 저장합니다.';apply();close();$('body-input').focus();};
  $('editor').addEventListener('submit',e=>{if(states[state].locked){e.preventDefault();e.stopImmediatePropagation();$('feedback').textContent='업데이트 중입니다. 완료 후 저장해 주세요.';}},true);
  apply();
})();
