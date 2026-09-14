-- Individual photos share the 40MiB total budget; do not resize existing originals.
alter table osan_progress_photos drop constraint ck_osan_progress_photos_size;
alter table osan_progress_photos add constraint ck_osan_progress_photos_size
    check (byte_size between 1 and 41943040 and octet_length(content) = byte_size);
alter table osan_photo_revision_files drop constraint osan_photo_revision_files_content_check;
alter table osan_photo_revision_files add constraint osan_photo_revision_files_content_check
    check (octet_length(content) between 1 and 41943040);
