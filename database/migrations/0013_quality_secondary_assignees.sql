alter table project_assignees drop constraint if exists ck_project_assignees_responsibility_type;

alter table project_assignees add constraint ck_project_assignees_responsibility_type
    check (responsibility_type in (
        'Procurement',
        'ProductionPlanning',
        'Manufacturing',
        'Quality',
        'Logistics',
        'SalesPrimary',
        'SalesSecondary',
        'DesignPrimary',
        'DesignSecondary',
        'ProductionPlanningPrimary',
        'ProductionPlanningSecondary',
        'ProcurementPrimary',
        'ProcurementSecondary',
        'MaterialsPrimary',
        'MaterialsSecondary',
        'ManufacturingPrimary',
        'ManufacturingSecondary',
        'LogisticsPrimary',
        'LogisticsSecondary',
        'QualityIQC',
        'QualityIQCSecondary',
        'QualityLQC',
        'QualityLQCSecondary',
        'QualityOQC',
        'QualityOQCSecondary',
        'QualityCustomerInspection',
        'QualityCustomerInspectionSecondary'
    ));
