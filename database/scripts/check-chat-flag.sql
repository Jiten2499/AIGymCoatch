SET NOCOUNT ON;
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = N'Tenants' AND COLUMN_NAME IN (N'Name', N'TenantType', N'Type', N'Kind');
SELECT t.Name, p.Code AS PlanCode, s.Status, pf.Enabled AS ChatEnabled
FROM app.Subscriptions s
JOIN app.Tenants t ON t.Id = s.TenantId
JOIN app.Plans p ON p.Id = s.PlanId
LEFT JOIN app.PlanFeatures pf ON pf.PlanId = p.Id AND pf.FeatureCode = N'GYM_CHAT'
WHERE s.DeletedAt IS NULL;
