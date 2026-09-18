-- Adds the Interior Busbar menu already exposed by the API to the fixed site-access code set.
create or replace function qms_site_access_menu_codes_valid(p_menu_codes text[])
returns boolean
language sql
immutable
as $$
    select coalesce(cardinality(p_menu_codes), 0) > 0
       and cardinality(p_menu_codes) = (
            select count(distinct menu_code)
            from unnest(p_menu_codes) menu_code)
       and not exists (
            select 1
            from unnest(p_menu_codes) menu_code
            where menu_code not in (
                'Home', 'PrivacyNotice', 'NoticeBoard', 'MyWork', 'TeamsActivity',
                'Projects', 'Sales', 'G2', 'InteriorBusbar', 'FormTemplates',
                'ProductionPlanning', 'Procurement', 'Materials', 'Manufacturing',
                'Quality', 'Logistics', 'Notifications', 'NotificationSettings',
                'Pending', 'Administration'
            )
       );
$$;
