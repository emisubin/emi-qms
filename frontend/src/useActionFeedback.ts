import { useCallback, useRef, useState } from 'react';
import { ApiError } from './api';

export type ActionFeedbackTone = 'neutral' | 'loading' | 'success' | 'error' | 'partial';

export type ActionFeedbackState = {
  tone: ActionFeedbackTone;
  message: string;
};

export type ScopedActionFeedback = ActionFeedbackState & {
  scope: string;
};

type ActionConflicts = {
  scopes?: readonly string[];
  prefixes?: readonly string[];
};

type RunActionOptions = {
  loadingMessage: string;
  successMessage: string;
  partialMessage?: string;
  errorFallback: string;
  subject?: string;
  refresh?: () => Promise<boolean>;
  conflicts?: ActionConflicts;
};

export type RunActionResult = 'success' | 'error' | 'partial' | 'blocked';

function hasConflict(busyScopes: ReadonlySet<string>, scope: string, conflicts?: ActionConflicts) {
  if (busyScopes.has(scope)) {
    return true;
  }

  if (conflicts?.scopes?.some((candidate) => busyScopes.has(candidate))) {
    return true;
  }

  return conflicts?.prefixes?.some((prefix) => [...busyScopes].some((candidate) => candidate.startsWith(prefix))) ?? false;
}

function withSubject(subject: string | undefined, message: string) {
  return subject ? `${subject}: ${message}` : message;
}

export function actionErrorMessage(error: unknown, fallback: string, subject?: string) {
  if (error instanceof ApiError) {
    const reason = error.message.trim() || fallback;
    if (error.status === 403) {
      return withSubject(subject, `${reason} 담당자 또는 관리자에게 권한을 확인해 주세요.`);
    }

    if (error.status === 404) {
      return withSubject(subject, `${reason} 목록을 새로고침해 최신 상태를 확인해 주세요.`);
    }

    if (error.status === 409) {
      return withSubject(subject, `${reason} 목록을 새로고침한 뒤 다시 확인해 주세요.`);
    }

    return withSubject(subject, `${reason} 잠시 후 다시 시도해 주세요.`);
  }

  return withSubject(subject, `${fallback} 연결 상태를 확인하고 다시 시도해 주세요.`);
}

export function useActionFeedback() {
  const busyRef = useRef<Set<string>>(new Set());
  const [busyScopes, setBusyScopes] = useState<Set<string>>(() => new Set());
  const [feedbackByScope, setFeedbackByScope] = useState<Map<string, ActionFeedbackState>>(() => new Map());
  const [latestFeedback, setLatestFeedback] = useState<ScopedActionFeedback | null>(null);

  const publish = useCallback((scope: string, feedback: ActionFeedbackState) => {
    setFeedbackByScope((current) => {
      const next = new Map(current);
      next.set(scope, feedback);
      return next;
    });
    setLatestFeedback({ scope, ...feedback });
  }, []);

  const isBusy = useCallback((scope: string) => busyScopes.has(scope), [busyScopes]);
  const hasBusyPrefix = useCallback(
    (prefix: string) => [...busyScopes].some((scope) => scope.startsWith(prefix)),
    [busyScopes]
  );
  const feedbackFor = useCallback((scope: string) => feedbackByScope.get(scope) ?? null, [feedbackByScope]);

  const reset = useCallback((scope?: string) => {
    if (!scope) {
      setFeedbackByScope(new Map());
      setLatestFeedback(null);
      return;
    }

    setFeedbackByScope((current) => {
      const next = new Map(current);
      next.delete(scope);
      return next;
    });
    setLatestFeedback((current) => current?.scope === scope ? null : current);
  }, []);

  const run = useCallback(async (
    scope: string,
    mutation: () => Promise<unknown>,
    options: RunActionOptions
  ): Promise<RunActionResult> => {
    if (hasConflict(busyRef.current, scope, options.conflicts)) {
      return 'blocked';
    }

    busyRef.current.add(scope);
    setBusyScopes(new Set(busyRef.current));
    publish(scope, { tone: 'loading', message: options.loadingMessage });

    try {
      await mutation();
      if (options.refresh) {
        const refreshed = await options.refresh();
        if (!refreshed) {
          publish(scope, {
            tone: 'partial',
            message: options.partialMessage
              ?? `${options.successMessage} 최신 목록을 불러오지 못했습니다. 새로고침해 주세요.`
          });
          return 'partial';
        }
      }

      publish(scope, { tone: 'success', message: options.successMessage });
      return 'success';
    } catch (error) {
      publish(scope, {
        tone: 'error',
        message: actionErrorMessage(error, options.errorFallback, options.subject)
      });
      return 'error';
    } finally {
      busyRef.current.delete(scope);
      setBusyScopes(new Set(busyRef.current));
    }
  }, [publish]);

  return {
    busyScopes,
    latestFeedback,
    feedbackFor,
    hasBusyPrefix,
    isBusy,
    reset,
    setFeedback: publish,
    run
  };
}
