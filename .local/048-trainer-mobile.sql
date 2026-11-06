SET QUOTED_IDENTIFIER ON;
GO
IF COL_LENGTH('app.PtSessions', 'StartedAtUtc') IS NULL
BEGIN
    ALTER TABLE app.PtSessions ALTER COLUMN CompletedOn DATE NULL;
    ALTER TABLE app.PtSessions ADD StartedAtUtc DATETIME2 NULL;
    ALTER TABLE app.PtSessions ADD ElapsedSeconds INT NOT NULL CONSTRAINT DF_PtSessions_ElapsedSeconds DEFAULT 0;
    ALTER TABLE app.PtSessions ADD Location NVARCHAR(120) NULL;
    ALTER TABLE app.PtSessions ADD SessionType NVARCHAR(80) NULL;
    ALTER TABLE app.PtSessions ADD Focus NVARCHAR(500) NULL;
END
GO
IF COL_LENGTH('app.PtSessions', 'ElapsedSeconds') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PtSessions_Elapsed')
    ALTER TABLE app.PtSessions ADD CONSTRAINT CK_PtSessions_Elapsed CHECK (ElapsedSeconds >= 0 AND ElapsedSeconds <= 28800);
GO
IF OBJECT_ID('app.PtSessionExercises', 'U') IS NULL
BEGIN
    CREATE TABLE app.PtSessionExercises
    (
        Id UNIQUEIDENTIFIER NOT NULL,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        PtSessionId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(120) NOT NULL,
        MuscleGroup NVARCHAR(80) NULL,
        Sets INT NOT NULL,
        Reps NVARCHAR(40) NOT NULL,
        Completed BIT NOT NULL,
        SortOrder INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NOT NULL,
        DeletedAt DATETIME2 NULL,
        CreatedBy UNIQUEIDENTIFIER NULL,
        UpdatedBy UNIQUEIDENTIFIER NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT PK_PtSessionExercises PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_PtSessionExercises_Tenants FOREIGN KEY (TenantId) REFERENCES app.Tenants (Id),
        CONSTRAINT FK_PtSessionExercises_PtSessions FOREIGN KEY (PtSessionId) REFERENCES app.PtSessions (Id),
        CONSTRAINT CK_PtSessionExercises_Sets CHECK (Sets BETWEEN 1 AND 20)
    );
    CREATE INDEX IX_PtSessionExercises_Session ON app.PtSessionExercises (TenantId, PtSessionId, SortOrder) WHERE DeletedAt IS NULL;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PtSessions_TenantId_Trainer_Start' AND object_id = OBJECT_ID(N'app.PtSessions'))
    CREATE INDEX IX_PtSessions_TenantId_Trainer_Start ON app.PtSessions (TenantId, TrainerProfileId, ScheduledStartUtc) WHERE DeletedAt IS NULL;
GO
