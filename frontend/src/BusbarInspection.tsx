import { busbarApi, busbarDateTime, type BusbarProduct } from './interiorBusbar';

export function BusbarInspection({ user, product, canInspect, busy, run }: {
  user: string; product: BusbarProduct; canInspect: boolean; busy: boolean;
  run: (action: () => Promise<unknown>, message?: string) => Promise<boolean>;
}) {
  if (product.status !== 'Complete') return null;
  return <section className="busbar-inspection" aria-label="품질 검사">
    <div><strong>{product.inspectedAtUtc ? '검사 완료' : product.isShipped ? '검사 기록 없음' : '검사 미완료'}</strong>
      {product.inspectedAtUtc
        ? <p>검사자: {product.inspectedByDisplayName || '기록 없음'} · {busbarDateTime(product.inspectedAtUtc)}</p>
        : <p>{product.isShipped ? '검사 기능 도입 전 출하된 패널입니다.' : '품질팀 검사 완료 후 출하할 수 있습니다.'}</p>}
    </div>
    {!product.inspectedAtUtc && !product.isShipped && canInspect && <button type="button" disabled={busy} onClick={() => void run(
      () => busbarApi.write(user, `/products/${product.id}/inspection`, {}),
      '검사 완료를 등록했습니다. 검사자와 시간이 기록되었습니다.',
    )}>검사 완료</button>}
  </section>;
}
