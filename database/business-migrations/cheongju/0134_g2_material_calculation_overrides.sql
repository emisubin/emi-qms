-- Imported numeric literals remain part of the existing daily inventory snapshot.
alter table g2_material_counts
 add column calculation_overrides jsonb
 check (calculation_overrides is null or jsonb_typeof(calculation_overrides) = 'object');
