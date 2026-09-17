-- Preserve the normalized original media type in both current and immutable history rows.
alter table busbar_photo_history add column content_type text not null default 'image/jpeg';
alter table busbar_photos add constraint ck_busbar_photos_content_type
 check(content_type in ('image/jpeg','image/png','image/heic'));
alter table busbar_photo_history add constraint ck_busbar_photo_history_content_type
 check(content_type in ('image/jpeg','image/png','image/heic'));
