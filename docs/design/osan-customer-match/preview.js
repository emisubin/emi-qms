// Local-only interaction prototype. No API, browser storage or production mutations.
const customers = ['한빛전자', '미래산업', '미래테크', '다온시스템'];
const input = document.querySelector('#customer');
const status = document.querySelector('#match-status');
const dialog = document.querySelector('#customer-dialog');
const choose = document.querySelector('#choose');
const confirm = document.querySelector('#confirm');
const save = document.querySelector('#save');
const clear = document.querySelector('#clear');
const result = document.querySelector('#result');
let linked = null, matches = [], timer, composing = false, pending = null, dismissedQuery = null;
const normalize = value => value.trim().replace(/\s+/g, '').toLocaleLowerCase();
function paint(icon, title, subtitle, state) {
  status.replaceChildren(); status.dataset.state = state;
  const circle = document.createElement('span'); circle.className = 'status-icon'; circle.textContent = icon; circle.setAttribute('aria-hidden','true');
  const text = document.createElement('div'); const strong = document.createElement('strong'); strong.textContent = title;
  const small = document.createElement('small'); small.textContent = subtitle; text.append(strong,small); status.append(circle,text);
}
function updateSave() {
  save.disabled = !linked;
  document.querySelector('#save-hint').textContent = linked ? `연결된 고객사: ${linked}` : '고객사를 연결한 뒤 등록할 수 있습니다.';
}
function evaluate(openPopup = true) {
  clearTimeout(timer); timer = null;
  const value = normalize(input.value);
  matches = value ? customers.filter(name => normalize(name).includes(value)) : [];
  clear.hidden = !input.value; choose.hidden = false; input.setAttribute('aria-invalid','false');
  if (!value) { linked = null; status.replaceChildren(); }
  else if (matches.length === 1) {
    linked = matches[0]; paint('✓', `연결된 고객사 · ${linked}`, '등록된 고객사와 연결되었습니다. 이름을 확인해 주세요.', 'linked');
  } else if (matches.length > 1) {
    linked = null; paint('!', `일치하는 고객사 ${matches.length}개`, matches.join(' · '), 'multiple'); choose.hidden = false;
    if (openPopup && dismissedQuery !== value && !dialog.open) openDialog();
  } else { linked = null; input.setAttribute('aria-invalid','true'); paint('!', '등록된 고객사를 찾을 수 없습니다', '이름을 다시 확인하거나 관리자에게 고객사 등록을 요청해 주세요.', 'none'); }
  updateSave();
}
function typing() {
  clearTimeout(timer); linked = null; result.textContent = ''; choose.hidden = false; updateSave(); clear.hidden = !input.value; input.setAttribute('aria-invalid','false');
  dismissedQuery = null;
  if (!input.value.trim()) { status.replaceChildren(); return; }
  paint('…', '고객사 확인 중', '등록된 고객사에서 입력한 이름을 찾고 있습니다.', 'searching');
  if (!composing) timer = setTimeout(() => evaluate(true), 650);
}
function openDialog() {
  clearTimeout(timer); timer = null; pending = null; confirm.disabled = true;
  document.querySelector('#selection-count').textContent = '고객사 1개를 선택해 주세요.';
  document.querySelector('#dialog-description').textContent = '고객사명을 검색한 뒤 연결할 고객사 1개를 선택해 주세요.';
  document.querySelector('#dialog-search').value = input.value.trim();
  renderCandidates();
  input.setAttribute('aria-expanded','true'); dialog.showModal();
  document.querySelector('#dialog-search').focus();
}
function renderCandidates() {
  const query = normalize(document.querySelector('#dialog-search').value);
  const candidates = customers.filter(name => normalize(name).includes(query));
  pending = null; confirm.disabled = true;
  document.querySelector('#selection-count').textContent = `검색 결과 ${candidates.length}개 · 1개를 선택해 주세요.`;
  document.querySelector('#search-empty').hidden = candidates.length > 0;
  const fieldset = document.querySelector('#candidates');
  fieldset.replaceChildren();
  const legend = document.createElement('legend'); legend.className = 'sr-only'; legend.textContent='연결할 고객사'; fieldset.append(legend);
  candidates.forEach(name => {
    const label = document.createElement('label'); const radio = document.createElement('input'); radio.type='radio';radio.name='candidate';radio.value=name;
    const strong = document.createElement('strong');strong.textContent=name;
    const tag=document.createElement('small');tag.textContent='등록 고객사';
    radio.addEventListener('change',()=>{pending=name;confirm.disabled=false;document.querySelector('#selection-count').textContent=`${name} 선택`;});
    label.append(radio,strong,tag);fieldset.append(label);
  });
}
document.querySelector('#dialog-search').addEventListener('input',renderCandidates);
function closeDialog(){dismissedQuery=normalize(input.value);dialog.close();input.setAttribute('aria-expanded','false'); input.focus();}
input.addEventListener('input',typing);
input.addEventListener('compositionstart',()=>{composing=true;clearTimeout(timer);});
input.addEventListener('compositionend',()=>{composing=false;typing();});
input.addEventListener('blur',()=>{if (!composing && !dialog.open && timer) evaluate(true);});
input.addEventListener('keydown',event=>{
 if (event.isComposing || composing) return;
 if (event.key==='Enter' || event.key==='ArrowDown') {event.preventDefault(); if (!composing) evaluate(true); if(matches.length>1&&!dialog.open)openDialog();}
 if(event.key==='Escape'){clearTimeout(timer); dismissedQuery=normalize(input.value);evaluate(false);}
});
choose.addEventListener('click',openDialog);
['close-dialog','cancel-dialog'].forEach(id=>document.querySelector('#'+id).addEventListener('click',closeDialog));
dialog.addEventListener('cancel',event=>{event.preventDefault();closeDialog();});
dialog.addEventListener('click',event=>{if(event.target===dialog){const r=dialog.getBoundingClientRect();if(event.clientX<r.left||event.clientX>r.right||event.clientY<r.top||event.clientY>r.bottom)closeDialog();}});
confirm.addEventListener('click',()=>{
 if(!pending)return;linked=pending;input.value=pending;closeDialog();choose.hidden=false;clear.hidden=false;input.setAttribute('aria-invalid','false');
 paint('✓',`연결된 고객사 · ${linked}`,'선택한 고객사로 연결되었습니다. 이름을 확인해 주세요.','linked');updateSave();
});
function reset(){clearTimeout(timer);input.value='';linked=null;dismissedQuery=null;result.textContent='';evaluate(false);input.focus();}
clear.addEventListener('click',reset);document.querySelector('#reset').addEventListener('click',reset);
document.querySelectorAll('[data-example]').forEach(button=>button.addEventListener('click',()=>{reset();input.value=button.dataset.example;evaluate(true);input.scrollIntoView({block:'center',behavior:'instant'});}));
document.querySelector('#project-form').addEventListener('submit',event=>{event.preventDefault();if(!linked)return;result.textContent=`시안 확인 완료 · ${linked}에 연결됩니다. 실제 프로젝트는 등록되지 않았습니다.`;});
evaluate(false);
