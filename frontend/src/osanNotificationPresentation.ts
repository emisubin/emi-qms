import type { NotificationItem } from './projects';
export const osanNotificationKinds = ['프로젝트 생성', 'Gate 완료', '진행단계 반려', '진행단계 수정 완료', '공정 이상 발생', '이상 조치 완료', '프로젝트 완료', '공정 진행 요청'];
const stages = ['입고검사', '배치검사', '배선검사', '8계통', '동작검사', '출하검사', '포장'];
export function osanNotificationPresentation(item: NotificationItem) {
  const title = item.title;
  const kind = title.includes('공정 진행 요청') ? 7 : /공정 이상 발생|이상 등록/.test(title) ? 4 : /조치\s*완료/.test(title) ? 5 : title.includes('반려') ? 2 : title.includes('수정') ? 3 : title.includes('프로젝트') ? /등록|생성/.test(title) ? 0 : 6 : 1;
  let stage = item.workflowStageName ?? '';
  if (!stage && item.linkUrl) {
    try {
      const value = new URL(item.linkUrl, 'https://pms.local').searchParams.get('stage');
      stage = value && /^\d+$/.test(value) ? stages[Number(value) - 1] ?? '' : value ?? '';
    } catch { /* Malformed legacy links must not prevent the feed rendering. */ }
  }
  stage ||= stages.find(value => title.includes(value)) ?? '';
  const lines = item.message.split('\n').map(line => line.trim()).filter(Boolean);
  const excerpt = lines.find(line => /^(사유|공정 이상 내용|조치 내용|요청 내용):/.test(line)) ?? lines.find(line => line.includes('님이 ')) ?? lines.find(line => !/^(Code|W\/O|진행 대상):/.test(line)) ?? title;
  return { kind, stage, excerpt, label: osanNotificationKinds[kind], tone: [2, 4, 7].includes(kind) ? 'red' : [1, 5, 6].includes(kind) ? 'green' : 'neutral' };
}
