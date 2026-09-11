-- Preserve the workflow mail contract and permit only the matching recipient's current
-- device for Osan workflow push. The existing trigger runs BEFORE INSERT, so later
-- delivery status updates remain possible after logout, reset, or provider expiry.
create or replace function prevent_osan_external_notification_delivery()
returns trigger language plpgsql as $$
begin
    if exists(select 1 from qms_database_identity where singleton=true and business_unit_code='OSAN')
       and not (
           (new.channel='Mail' and new.delivery_type='OsanWorkflow' and exists (
               select 1 from notifications n
               join projects p on p.id=n.project_id and p.project_profile='Osan'
               join notification_recipients r on r.notification_id=n.id
               where n.id=new.notification_id and n.source_kind='OsanWorkflow'
                 and n.project_id=new.project_id and r.id=new.notification_recipient_id
                 and r.user_id=new.recipient_user_id
           ))
           or (new.channel='WebPush' and new.delivery_type='WebPushNotification' and exists (
               select 1 from notifications n
               join projects p on p.id=n.project_id and p.project_profile='Osan'
               join notification_recipients r on r.notification_id=n.id
               join qms_users u on u.id=r.user_id and u.is_active=true
               join web_push_subscriptions s on s.user_id=u.id and s.is_active=true
               where n.id=new.notification_id and n.source_kind='OsanWorkflow'
                 and n.visibility_scope='RecipientOnly'
                 and n.project_id=new.project_id and r.id=new.notification_recipient_id
                 and r.user_id=new.recipient_user_id
                 and s.id=new.web_push_subscription_id
                 and s.generation=new.web_push_subscription_generation
                 and s.activated_at_utc<=n.created_at_utc
                 and (u.auth_provider<>'EntraId' or exists (
                     select 1 from user_roles approved where approved.user_id=u.id
                 ))
           ))
       ) then
        raise exception using errcode='42501',message='external_notification_delivery_disabled_for_business_unit';
    end if;
    return new;
end;
$$;
