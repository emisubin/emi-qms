-- HEIC image item payloads are stored without resizing/re-encoding, after metadata sanitization.
alter table osan_progress_photos drop constraint ck_osan_progress_photos_mime;
alter table osan_progress_photos add constraint ck_osan_progress_photos_mime
    check (normalized_mime in ('image/jpeg', 'image/png', 'image/heic'));
alter table osan_photo_revision_files drop constraint osan_photo_revision_files_normalized_mime_check;
alter table osan_photo_revision_files add constraint osan_photo_revision_files_normalized_mime_check
    check (normalized_mime in ('image/jpeg', 'image/png', 'image/heic'));
