SET QUOTED_IDENTIFIER ON;

            SET QUOTED_IDENTIFIER ON;

            IF OBJECT_ID(N'app.StaffCompensation', N'U') IS NULL
            BEGIN
                CREATE TABLE app.StaffCompensation
                (
                    Id UNIQUEIDENTIFIER NOT NULL,
                    TenantId UNIQUEIDENTIFIER NOT NULL,
                    StaffProfileId UNIQUEIDENTIFIER NOT NULL,
                    BaseSalary DECIMAL(19, 4) NOT NULL,
                    Allowances DECIMAL(19, 4) NOT NULL,
                    Currency NVARCHAR(3) NOT NULL,
                    EffectiveFrom DATE NOT NULL,
                    EffectiveTo DATE NULL,
                    Notes NVARCHAR(500) NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    UpdatedAt DATETIME2 NOT NULL,
                    DeletedAt DATETIME2 NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    RowVersion ROWVERSION NOT NULL,
                    CONSTRAINT PK_StaffCompensation PRIMARY KEY CLUSTERED (Id),
                    CONSTRAINT FK_StaffCompensation_Tenants FOREIGN KEY (TenantId) REFERENCES app.Tenants (Id),
                    CONSTRAINT FK_StaffCompensation_StaffProfiles FOREIGN KEY (StaffProfileId) REFERENCES app.StaffProfiles (Id),
                    CONSTRAINT CK_StaffCompensation_BaseSalary CHECK (BaseSalary >= 0),
                    CONSTRAINT CK_StaffCompensation_Allowances CHECK (Allowances >= 0),
                    CONSTRAINT CK_StaffCompensation_Dates CHECK (EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom)
                );
                CREATE UNIQUE INDEX UX_StaffCompensation_Staff_Effective ON app.StaffCompensation (TenantId, StaffProfileId, EffectiveFrom) WHERE DeletedAt IS NULL;
                CREATE INDEX IX_StaffCompensation_Staff ON app.StaffCompensation (TenantId, StaffProfileId) WHERE DeletedAt IS NULL;
            END;

            IF OBJECT_ID(N'app.PayrollRuns', N'U') IS NULL
            BEGIN
                CREATE TABLE app.PayrollRuns
                (
                    Id UNIQUEIDENTIFIER NOT NULL,
                    TenantId UNIQUEIDENTIFIER NOT NULL,
                    PeriodYear INT NOT NULL,
                    PeriodMonth INT NOT NULL,
                    PeriodStart DATE NOT NULL,
                    PeriodEnd DATE NOT NULL,
                    Currency NVARCHAR(3) NOT NULL,
                    Status NVARCHAR(32) NOT NULL,
                    PreparedByUserId UNIQUEIDENTIFIER NULL,
                    PreparedAt DATETIME2 NULL,
                    SubmittedAt DATETIME2 NULL,
                    ApprovedByUserId UNIQUEIDENTIFIER NULL,
                    ApprovedAt DATETIME2 NULL,
                    Notes NVARCHAR(2000) NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    UpdatedAt DATETIME2 NOT NULL,
                    DeletedAt DATETIME2 NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    RowVersion ROWVERSION NOT NULL,
                    CONSTRAINT PK_PayrollRuns PRIMARY KEY CLUSTERED (Id),
                    CONSTRAINT FK_PayrollRuns_Tenants FOREIGN KEY (TenantId) REFERENCES app.Tenants (Id),
                    CONSTRAINT FK_PayrollRuns_PreparedByUsers FOREIGN KEY (PreparedByUserId) REFERENCES app.Users (Id),
                    CONSTRAINT FK_PayrollRuns_ApprovedByUsers FOREIGN KEY (ApprovedByUserId) REFERENCES app.Users (Id),
                    CONSTRAINT CK_PayrollRuns_Month CHECK (PeriodMonth BETWEEN 1 AND 12),
                    CONSTRAINT CK_PayrollRuns_Status CHECK (Status IN (N'Draft', N'InReview', N'Approved', N'PartiallyPaid', N'Paid'))
                );
                CREATE UNIQUE INDEX UX_PayrollRuns_Tenant_Period ON app.PayrollRuns (TenantId, PeriodYear, PeriodMonth) WHERE DeletedAt IS NULL;
                CREATE INDEX IX_PayrollRuns_Tenant_Status ON app.PayrollRuns (TenantId, Status) WHERE DeletedAt IS NULL;
            END;

            IF OBJECT_ID(N'app.PayrollLines', N'U') IS NULL
            BEGIN
                CREATE TABLE app.PayrollLines
                (
                    Id UNIQUEIDENTIFIER NOT NULL,
                    TenantId UNIQUEIDENTIFIER NOT NULL,
                    PayrollRunId UNIQUEIDENTIFIER NOT NULL,
                    StaffProfileId UNIQUEIDENTIFIER NOT NULL,
                    UserId UNIQUEIDENTIFIER NOT NULL,
                    EmployeeName NVARCHAR(200) NOT NULL,
                    EmployeeCode NVARCHAR(32) NOT NULL,
                    StaffType NVARCHAR(32) NOT NULL,
                    BaseSalary DECIMAL(19, 4) NOT NULL,
                    Allowances DECIMAL(19, 4) NOT NULL,
                    Commissions DECIMAL(19, 4) NOT NULL,
                    Bonuses DECIMAL(19, 4) NOT NULL,
                    Deductions DECIMAL(19, 4) NOT NULL,
                    GrossPay DECIMAL(19, 4) NOT NULL,
                    NetPay DECIMAL(19, 4) NOT NULL,
                    AmountPaid DECIMAL(19, 4) NOT NULL,
                    PaymentStatus NVARCHAR(32) NOT NULL,
                    MissingSalary BIT NOT NULL,
                    UnapprovedCommission BIT NOT NULL,
                    UnconfirmedAttendance BIT NOT NULL,
                    Locked BIT NOT NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    UpdatedAt DATETIME2 NOT NULL,
                    DeletedAt DATETIME2 NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    RowVersion ROWVERSION NOT NULL,
                    CONSTRAINT PK_PayrollLines PRIMARY KEY CLUSTERED (Id),
                    CONSTRAINT FK_PayrollLines_Tenants FOREIGN KEY (TenantId) REFERENCES app.Tenants (Id),
                    CONSTRAINT FK_PayrollLines_PayrollRuns FOREIGN KEY (PayrollRunId) REFERENCES app.PayrollRuns (Id),
                    CONSTRAINT FK_PayrollLines_StaffProfiles FOREIGN KEY (StaffProfileId) REFERENCES app.StaffProfiles (Id),
                    CONSTRAINT FK_PayrollLines_Users FOREIGN KEY (UserId) REFERENCES app.Users (Id),
                    CONSTRAINT CK_PayrollLines_Amounts CHECK (BaseSalary >= 0 AND Allowances >= 0 AND Commissions >= 0 AND Bonuses >= 0 AND Deductions >= 0 AND GrossPay >= 0 AND NetPay >= 0 AND AmountPaid >= 0),
                    CONSTRAINT CK_PayrollLines_PaymentStatus CHECK (PaymentStatus IN (N'Pending', N'PartiallyPaid', N'Paid')),
                    CONSTRAINT UX_PayrollLines_Run_Staff UNIQUE (PayrollRunId, StaffProfileId)
                );
                CREATE INDEX IX_PayrollLines_Run ON app.PayrollLines (TenantId, PayrollRunId) WHERE DeletedAt IS NULL;
            END;

            IF OBJECT_ID(N'app.PayrollAdjustments', N'U') IS NULL
            BEGIN
                CREATE TABLE app.PayrollAdjustments
                (
                    Id UNIQUEIDENTIFIER NOT NULL,
                    TenantId UNIQUEIDENTIFIER NOT NULL,
                    PayrollLineId UNIQUEIDENTIFIER NOT NULL,
                    Kind NVARCHAR(32) NOT NULL,
                    Label NVARCHAR(100) NOT NULL,
                    Amount DECIMAL(19, 4) NOT NULL,
                    Reason NVARCHAR(500) NOT NULL,
                    CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    UpdatedAt DATETIME2 NOT NULL,
                    DeletedAt DATETIME2 NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    RowVersion ROWVERSION NOT NULL,
                    CONSTRAINT PK_PayrollAdjustments PRIMARY KEY CLUSTERED (Id),
                    CONSTRAINT FK_PayrollAdjustments_Tenants FOREIGN KEY (TenantId) REFERENCES app.Tenants (Id),
                    CONSTRAINT FK_PayrollAdjustments_PayrollLines FOREIGN KEY (PayrollLineId) REFERENCES app.PayrollLines (Id),
                    CONSTRAINT FK_PayrollAdjustments_Users FOREIGN KEY (CreatedByUserId) REFERENCES app.Users (Id),
                    CONSTRAINT CK_PayrollAdjustments_Kind CHECK (Kind IN (N'Bonus', N'Deduction')),
                    CONSTRAINT CK_PayrollAdjustments_Amount CHECK (Amount > 0)
                );
                CREATE INDEX IX_PayrollAdjustments_Line ON app.PayrollAdjustments (TenantId, PayrollLineId) WHERE DeletedAt IS NULL;
            END;

            IF OBJECT_ID(N'app.PayrollPayments', N'U') IS NULL
            BEGIN
                CREATE TABLE app.PayrollPayments
                (
                    Id UNIQUEIDENTIFIER NOT NULL,
                    TenantId UNIQUEIDENTIFIER NOT NULL,
                    PayrollRunId UNIQUEIDENTIFIER NOT NULL,
                    PayrollLineId UNIQUEIDENTIFIER NOT NULL,
                    Amount DECIMAL(19, 4) NOT NULL,
                    PaymentDate DATE NOT NULL,
                    Method NVARCHAR(32) NOT NULL,
                    Reference NVARCHAR(100) NULL,
                    RecordedByUserId UNIQUEIDENTIFIER NOT NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    UpdatedAt DATETIME2 NOT NULL,
                    DeletedAt DATETIME2 NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    RowVersion ROWVERSION NOT NULL,
                    CONSTRAINT PK_PayrollPayments PRIMARY KEY CLUSTERED (Id),
                    CONSTRAINT FK_PayrollPayments_Tenants FOREIGN KEY (TenantId) REFERENCES app.Tenants (Id),
                    CONSTRAINT FK_PayrollPayments_PayrollRuns FOREIGN KEY (PayrollRunId) REFERENCES app.PayrollRuns (Id),
                    CONSTRAINT FK_PayrollPayments_PayrollLines FOREIGN KEY (PayrollLineId) REFERENCES app.PayrollLines (Id),
                    CONSTRAINT FK_PayrollPayments_Users FOREIGN KEY (RecordedByUserId) REFERENCES app.Users (Id),
                    CONSTRAINT CK_PayrollPayments_Amount CHECK (Amount > 0),
                    CONSTRAINT CK_PayrollPayments_Method CHECK (Method IN (N'Cash', N'Upi', N'BankTransfer', N'Card', N'Other'))
                );
                CREATE INDEX IX_PayrollPayments_Line ON app.PayrollPayments (TenantId, PayrollLineId) WHERE DeletedAt IS NULL;
                CREATE INDEX IX_PayrollPayments_Run ON app.PayrollPayments (TenantId, PayrollRunId) WHERE DeletedAt IS NULL;
                CREATE UNIQUE INDEX UX_PayrollPayments_Line_Reference ON app.PayrollPayments (TenantId, PayrollLineId, Reference) WHERE DeletedAt IS NULL AND Reference IS NOT NULL;
            END;
        
