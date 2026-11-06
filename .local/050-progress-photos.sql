SET QUOTED_IDENTIFIER ON;
IF OBJECT_ID(N'CK_MemberProgressAssessments_Height', N'C') IS NULL
    ALTER TABLE app.MemberProgressAssessments ADD CONSTRAINT CK_MemberProgressAssessments_Height CHECK (Height IS NULL OR (Height > 0 AND Height <= 500));
IF OBJECT_ID(N'CK_MemberProgressAssessments_RightArm', N'C') IS NULL
    ALTER TABLE app.MemberProgressAssessments ADD CONSTRAINT CK_MemberProgressAssessments_RightArm CHECK (RightArm IS NULL OR (RightArm > 0 AND RightArm <= 500));
IF OBJECT_ID(N'CK_MemberProgressAssessments_RightThigh', N'C') IS NULL
    ALTER TABLE app.MemberProgressAssessments ADD CONSTRAINT CK_MemberProgressAssessments_RightThigh CHECK (RightThigh IS NULL OR (RightThigh > 0 AND RightThigh <= 500));
IF OBJECT_ID(N'CK_MemberProgressAssessments_Visceral', N'C') IS NULL
    ALTER TABLE app.MemberProgressAssessments ADD CONSTRAINT CK_MemberProgressAssessments_Visceral CHECK (VisceralFat IS NULL OR (VisceralFat > 0 AND VisceralFat <= 60));
IF OBJECT_ID(N'CK_MemberProgressAssessments_Strength', N'C') IS NULL
    ALTER TABLE app.MemberProgressAssessments ADD CONSTRAINT CK_MemberProgressAssessments_Strength CHECK (StrengthScore IS NULL OR StrengthScore BETWEEN 1 AND 10);
IF OBJECT_ID(N'CK_MemberProgressAssessments_Endurance', N'C') IS NULL
    ALTER TABLE app.MemberProgressAssessments ADD CONSTRAINT CK_MemberProgressAssessments_Endurance CHECK (EnduranceScore IS NULL OR EnduranceScore BETWEEN 1 AND 10);
IF OBJECT_ID(N'CK_MemberProgressAssessments_Flexibility', N'C') IS NULL
    ALTER TABLE app.MemberProgressAssessments ADD CONSTRAINT CK_MemberProgressAssessments_Flexibility CHECK (FlexibilityScore IS NULL OR FlexibilityScore BETWEEN 1 AND 10);
IF OBJECT_ID(N'CK_MemberProgressAssessments_Core', N'C') IS NULL
    ALTER TABLE app.MemberProgressAssessments ADD CONSTRAINT CK_MemberProgressAssessments_Core CHECK (CoreScore IS NULL OR CoreScore BETWEEN 1 AND 10);
IF OBJECT_ID(N'app.MemberProgressPhotos', N'U') IS NULL
BEGIN
    CREATE TABLE app.MemberProgressPhotos
    (
        Id UNIQUEIDENTIFIER NOT NULL,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        AssessmentId UNIQUEIDENTIFIER NOT NULL,
        StorageKey NVARCHAR(200) NOT NULL,
        ContentType NVARCHAR(80) NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NOT NULL,
        DeletedAt DATETIME2 NULL,
        CreatedBy UNIQUEIDENTIFIER NULL,
        UpdatedBy UNIQUEIDENTIFIER NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT PK_MemberProgressPhotos PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_MemberProgressPhotos_Tenants FOREIGN KEY (TenantId) REFERENCES app.Tenants (Id),
        CONSTRAINT FK_MemberProgressPhotos_MemberProgressAssessments FOREIGN KEY (AssessmentId) REFERENCES app.MemberProgressAssessments (Id),
        CONSTRAINT CK_MemberProgressPhotos_Type CHECK (ContentType IN (N'image/jpeg', N'image/png', N'image/webp'))
    );
    CREATE INDEX IX_MemberProgressPhotos_Assessment
        ON app.MemberProgressPhotos (TenantId, AssessmentId)
        WHERE DeletedAt IS NULL;
END
SELECT OBJECT_ID(N'app.MemberProgressPhotos', N'U') AS PhotoTable;
