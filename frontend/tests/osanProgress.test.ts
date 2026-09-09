import { describe, expect, it } from 'vitest';
import { completionUnavailable, validateOsanPhotos, type OsanProgressTarget } from '../src/osanProgress';
function target(completed: number[] = []): OsanProgressTarget {
  return { targetId: 'one', sequenceNumber: 1, displayName: '제품 1', status: 'InProgress', version: 1, canStart: false, startedAtUtc: null, startedByUserId: null, startedByDisplayName: null,
    steps: Array.from({ length: 7 }, (_, index) => ({ stepId: String(index), stepCode: String(index), canCompleteIndividual: true, canCompleteBatch: true, guidanceDescription: null, guidancePhotos: [], startedAtUtc: null, completedByUserId: null, sequenceNumber: index + 1, stepName: String(index + 1), status: completed.includes(index + 1) ? 'Completed' : 'NotStarted', completedAtUtc: null, completedByDisplayName: null, photos: [] })) };
}
describe('오산 완료 선택 계약', () => {
  it('개별 다음 단계만 허용하지만 일괄 한 대상의 1~6단계 임의 완료를 허용한다', () => {
    expect(completionUnavailable([target()], 3, 'individual')).toContain('다음 미완료');
    expect(completionUnavailable([target()], 3, 'batch')).toBeNull();
  });
  it('앞 단계가 하나라도 미완료인 혼합 선택 포장을 모두 막는다', () => {
    expect(completionUnavailable([target([1, 2, 3, 4, 5, 6]), target([1, 2, 3, 4, 5])], 7, 'batch')).toContain('앞 6단계');
    expect(completionUnavailable([target([1, 2, 3, 4, 5, 6])], 7, 'individual')).toBeNull();
  });
  it('이미 완료한 대상을 자동 제외하지 않는다', () => {
    expect(completionUnavailable([target(), target([1])], 1, 'batch')).toContain('이미 완료');
  });
});
describe('사진 제한', () => {
  const file = (size: number, type = 'image/jpeg') => new File([new Uint8Array(size)], 'sample.jpg', { type });
  it('선택 첨부와 정확한 크기 경계를 허용한다', () => {
    expect(validateOsanPhotos([])).toBeNull();
    expect(validateOsanPhotos(Array.from({ length: 3 }, () => file(5 * 1024 * 1024)))).toBeNull();
    expect(validateOsanPhotos([file(5 * 1024 * 1024 + 1)])).toContain('장당');
  });
  it('HEIC, 빈 파일, 개수 초과, 총량 초과를 구분한다', () => {
    expect(validateOsanPhotos([file(1, 'image/heic')])).toContain('HEIC');
    expect(validateOsanPhotos([file(0)])).toContain('빈 파일');
    expect(validateOsanPhotos(Array.from({ length: 6 }, () => file(1)))).toContain('5장');
    expect(validateOsanPhotos(Array.from({ length: 4 }, () => file(4 * 1024 * 1024)))).toContain('전체');
  });
});

describe('사진 완료 API 전송', () => {
  it('원본 파일과 mode·version을 multipart로 보내고 업무 컨텍스트를 유지한다', async () => {
    const { completeOsanProgress } = await import('../src/osanProgress');
    const { resetBusinessUnitRequestContext, selectBusinessUnit, setRuntimeMutationAllowed } = await import('../src/api');
    const { vi } = await import('vitest');
    resetBusinessUnitRequestContext(); selectBusinessUnit('OSAN'); setRuntimeMutationAllowed(true);
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ operationId: 'operation', replayed: false, project: {} }), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    try {
      const original = new File([new Uint8Array([137, 80, 78, 71])], '원본.png', { type: 'image/png' });
      await completeOsanProgress('project', { operationId: 'operation', completionMode: 'batch', stageSequence: 3, targets: [{ targetId: 'target', expectedVersion: 4 }], photos: [original] }, 'dev-user');
      const [url, options] = fetchMock.mock.calls[0];
      expect(String(url)).toContain('/api/osan/projects/project/progress/completions');
      expect(new Headers(options?.headers).get('X-Qms-Business-Unit')).toBe('OSAN');
      expect(new Headers(options?.headers).get('X-Dev-User')).toBe('dev-user');
      expect(new Headers(options?.headers).has('Content-Type')).toBe(false);
      const body = options?.body as FormData;
      expect(body.get('completionMode')).toBe('batch');
      expect(body.get('stageSequence')).toBe('3');
      expect(body.get('targets')).toBe('[{"targetId":"target","expectedVersion":4}]');
      expect((body.get('photos') as File).size).toBe(4);
      expect((body.get('photos') as File).name).toBe('원본.png');
    } finally { fetchMock.mockRestore(); resetBusinessUnitRequestContext(); }
  });
});
