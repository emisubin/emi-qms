export type NoticeListResponse = {
  items: NoticeListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export type NoticeListItem = {
  noticeId: string;
  title: string;
  preview: string;
  authorDisplayName: string;
  authorDepartmentName: string | null;
  createdAtUtc: string;
  canDelete: boolean;
};

export type NoticeDetail = {
  noticeId: string;
  title: string;
  body: string;
  authorDisplayName: string;
  authorDepartmentName: string | null;
  createdAtUtc: string;
  canDelete: boolean;
};

export type CreateNoticeRequest = {
  requestId: string;
  title: string;
  body: string;
};

export type NoticeDeleteResponse = {
  noticeId: string;
  deleted: boolean;
};
