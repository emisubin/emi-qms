do $$
begin
    alter table notification_deliveries
        drop constraint if exists ck_notification_deliveries_channel;

    alter table notification_deliveries
        add constraint ck_notification_deliveries_channel
        check (channel in ('TeamsChannel', 'TeamsDirectMessage', 'TeamsActivity', 'Mail'));
end $$;
