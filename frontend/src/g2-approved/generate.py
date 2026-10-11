from pathlib import Path
import re

def split(s,sep):
 parts=[];start=0;depth=0;q=None;escape=False
 for i,c in enumerate(s):
  if escape:escape=False;continue
  if c=='\\':escape=True;continue
  if q:
   if c==q:q=None
  elif c in '\"\'':q=c
  elif c=='(':depth+=1
  elif c==')':depth-=1
  elif c==sep and depth==0:parts.append(s[start:i]);start=i+1
 parts.append(s[start:]);return parts
def scoped(css):
 css=re.sub(r'/\*.*?\*/','',css,flags=re.S)
 def process(s):
  result='';i=0
  while i<len(s):
   opening=s.find('{',i)
   if opening<0:break
   head=s[i:opening].strip();d=1;j=opening+1;q=None
   while j<len(s) and d:
    c=s[j]
    if q:
     if c==q and s[j-1]!='\\':q=None
    elif c in '\"\'':q=c
    elif c=='{':d+=1
    elif c=='}':d-=1
    j+=1
   body=s[opening+1:j-1]
   if head.startswith('@'):result+=head+'{\n'+process(body)+'}\n'
   else:
    sels=[]
    for sel in split(head,','):
     sel=sel.strip().replace('.calendar-view ','')
     if sel in ['body','body.modal-open']:sel='.g2-approved'
     elif sel.startswith('body '):sel=sel[5:]
     # Both workspace and portal dialogs are descendants of this integration scope.
     sels.append('#g2-approved-scope '+sel)
    decl=';'.join(x.strip() if not x.strip() or '!important' in x else x.strip()+' !important' for x in split(body,';'))
    result+=','.join(sels)+'{'+decl+'}\n'
   i=j
  return result
 return process(css)
src=out=Path(__file__).resolve().parent
for name,screen in [('g2-pc-approved.html','min-width:861px'),('g2-mobile-review.html','max-width:860px')]:
 html=(src/name).read_text();css='\n'.join(re.findall(r'<style>(.*?)</style>',html,re.S))
 (out/('pc.css' if name.startswith('g2-pc') else 'mobile.css')).write_text('/* Copied from '+name+'. Only selector scope and cascade priority are adapted. */\n@media('+screen+'){\n'+scoped(css)+'}\n')
