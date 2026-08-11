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
  updatedAtUtc: string | null;
};

export type NoticeDetail = {
  noticeId: string;
  title: string;
  body: string;
  bodyFormat: NoticeBodyFormat;
  version: number;
  authorDisplayName: string;
  authorDepartmentName: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  canEdit: boolean;
  canDelete: boolean;
  attachments: NoticeAttachment[];
};

export type NoticeBodyFormat = 'PlainTextV1' | 'BoldMarkupV1';

export type NoticeAttachment = {
  attachmentId: string;
  fileName: string;
  contentType: string;
  byteSize: number;
  createdAtUtc: string;
  canDelete: boolean;
};

export type CreateNoticeRequest = {
  requestId: string;
  title: string;
  body: string;
  bodyFormat: NoticeBodyFormat;
};

export type UpdateNoticeRequest = {
  expectedVersion: number;
  title: string;
  body: string;
  bodyFormat: NoticeBodyFormat;
};

export type NoticeDeleteResponse = {
  noticeId: string;
  deleted: boolean;
};

export type NoticeAttachmentDeleteResponse = {
  noticeId: string;
  attachmentId: string;
  deleted: boolean;
};

export type NoticeAttachmentDownload = {
  blob: Blob;
  fileName: string;
};
