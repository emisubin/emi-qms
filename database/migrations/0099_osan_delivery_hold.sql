-- Preserve original due dates and work records; HOLD only changes home visibility.
alter table projects add column osan_delivery_hold boolean not null default false;
