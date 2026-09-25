alter table osan_photo_edit_requests
    add column if not exists reason text;

alter table osan_photo_edit_requests
    drop constraint if exists ck_osan_photo_edit_request_reason;

alter table osan_photo_edit_requests
    add constraint ck_osan_photo_edit_request_reason
    check (reason is null or (reason = btrim(reason) and char_length(reason) between 1 and 1000));
