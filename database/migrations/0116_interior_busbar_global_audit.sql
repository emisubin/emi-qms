-- Add central change capture without altering existing business or evidence rows.
create trigger trg_qms_global_audit_busbar_settings
after insert or update or delete on busbar_settings
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_busbar_product_families
after insert or update or delete on busbar_product_families
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_busbar_materials
after insert or update or delete on busbar_materials
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_busbar_workers
after insert or update or delete on busbar_workers
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_busbar_boms
after insert or update or delete on busbar_boms
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_busbar_bom_lines
after insert or update or delete on busbar_bom_lines
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_busbar_projects
after insert or update or delete on busbar_projects
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_busbar_plans
after insert or update or delete on busbar_plans
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_busbar_purchases
after insert or update or delete on busbar_purchases
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_busbar_products
after insert or update or delete on busbar_products
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_busbar_photos
after insert or update or delete on busbar_photos
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_busbar_stock
after insert or update or delete on busbar_stock
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_busbar_ecount_employees
after insert or update or delete on busbar_ecount_employees
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_busbar_master_access
after insert or update or delete on busbar_master_access
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_busbar_shipment_products
after insert or update or delete on busbar_shipment_products
for each row execute function qms_audit_capture_row_change();
