import { act, renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../src/api';
import { actionErrorMessage, useActionFeedback, type RunActionResult } from '../src/useActionFeedback';

describe('useActionFeedback', () => {
  it('publishes success only after the mutation and refresh both finish', async () => {
    const mutation = vi.fn(async () => undefined);
    const refresh = vi.fn(async () => true);
    const { result } = renderHook(() => useActionFeedback());

    let outcome: RunActionResult | undefined;
    await act(async () => {
      outcome = await result.current.run('work:1', mutation, {
        loadingMessage: '처리 중',
        successMessage: '처리 완료',
        errorFallback: '처리 실패',
        refresh
      });
    });

    expect(outcome).toBe('success');
    expect(mutation).toHaveBeenCalledOnce();
    expect(refresh).toHaveBeenCalledOnce();
    expect(result.current.feedbackFor('work:1')).toEqual({ tone: 'success', message: '처리 완료' });
    expect(result.current.isBusy('work:1')).toBe(false);
    act(() => result.current.reset('work:1'));
    expect(result.current.feedbackFor('work:1')).toBeNull();
  });

  it('keeps the mutation result and reports partial success when refresh fails', async () => {
    const { result } = renderHook(() => useActionFeedback());

    let outcome: RunActionResult | undefined;
    await act(async () => {
      outcome = await result.current.run('notification:1', async () => undefined, {
        loadingMessage: '읽음 처리 중',
        successMessage: '읽음 처리 완료',
        partialMessage: '읽음 처리는 완료했지만 새 목록을 불러오지 못했습니다.',
        errorFallback: '읽음 처리 실패',
        refresh: async () => false
      });
    });

    expect(outcome).toBe('partial');
    expect(result.current.feedbackFor('notification:1')).toEqual({
      tone: 'partial',
      message: '읽음 처리는 완료했지만 새 목록을 불러오지 못했습니다.'
    });
  });

  it('blocks a duplicate scope while the first mutation is still running', async () => {
    let release: (() => void) | undefined;
    const mutation = vi.fn(() => new Promise<void>((resolve) => { release = resolve; }));
    const { result } = renderHook(() => useActionFeedback());
    let first: Promise<RunActionResult> | undefined;

    act(() => {
      first = result.current.run('work:1', mutation, {
        loadingMessage: '처리 중',
        successMessage: '처리 완료',
        errorFallback: '처리 실패'
      });
    });

    let duplicate: RunActionResult | undefined;
    await act(async () => {
      duplicate = await result.current.run('work:1', mutation, {
        loadingMessage: '처리 중',
        successMessage: '처리 완료',
        errorFallback: '처리 실패'
      });
    });

    expect(duplicate).toBe('blocked');
    expect(mutation).toHaveBeenCalledOnce();

    await act(async () => {
      release?.();
      await first;
    });
    expect(result.current.isBusy('work:1')).toBe(false);
  });

  it('enforces notification row and bulk conflicts in both directions', async () => {
    let releaseRow: (() => void) | undefined;
    let releaseAll: (() => void) | undefined;
    const { result } = renderHook(() => useActionFeedback());
    let rowRun: Promise<RunActionResult> | undefined;
    let allRun: Promise<RunActionResult> | undefined;

    act(() => {
      rowRun = result.current.run('notification:1', () => new Promise<void>((resolve) => { releaseRow = resolve; }), {
        loadingMessage: '개별 처리 중',
        successMessage: '개별 처리 완료',
        errorFallback: '개별 처리 실패',
        conflicts: { scopes: ['notifications:all'] }
      });
    });
    let bulkWhileRowBusy: RunActionResult | undefined;
    await act(async () => {
      bulkWhileRowBusy = await result.current.run('notifications:all', async () => undefined, {
        loadingMessage: '전체 처리 중',
        successMessage: '전체 처리 완료',
        errorFallback: '전체 처리 실패',
        conflicts: { prefixes: ['notification:'] }
      });
    });
    expect(bulkWhileRowBusy).toBe('blocked');
    await act(async () => {
      releaseRow?.();
      await rowRun;
    });

    act(() => {
      allRun = result.current.run('notifications:all', () => new Promise<void>((resolve) => { releaseAll = resolve; }), {
        loadingMessage: '전체 처리 중',
        successMessage: '전체 처리 완료',
        errorFallback: '전체 처리 실패',
        conflicts: { prefixes: ['notification:'] }
      });
    });
    let rowWhileBulkBusy: RunActionResult | undefined;
    await act(async () => {
      rowWhileBulkBusy = await result.current.run('notification:2', async () => undefined, {
        loadingMessage: '개별 처리 중',
        successMessage: '개별 처리 완료',
        errorFallback: '개별 처리 실패',
        conflicts: { scopes: ['notifications:all'] }
      });
    });
    expect(rowWhileBulkBusy).toBe('blocked');
    await act(async () => {
      releaseAll?.();
      await allRun;
    });
  });

  it('uses structured API status guidance instead of parsing message text', () => {
    expect(actionErrorMessage(new ApiError(403, '처리할 권한이 없습니다.'), '처리 실패', '업무 A'))
      .toBe('업무 A: 처리할 권한이 없습니다. 담당자 또는 관리자에게 권한을 확인해 주세요.');
    expect(actionErrorMessage(new ApiError(409, '다른 사용자가 먼저 처리했습니다.'), '처리 실패'))
      .toBe('다른 사용자가 먼저 처리했습니다. 목록을 새로고침한 뒤 다시 확인해 주세요.');
  });
});
