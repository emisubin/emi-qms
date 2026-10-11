import { useEffect, useState } from 'react';

export function useG2Mobile() {
  const [mobile,setMobile]=useState(()=>typeof matchMedia==='function'&&matchMedia('(max-width:860px)').matches);
  useEffect(()=>{if(typeof matchMedia!=='function')return;const query=matchMedia('(max-width:860px)');const update=()=>setMobile(query.matches);query.addEventListener('change',update);return()=>query.removeEventListener('change',update);},[]);
  return mobile;
}
