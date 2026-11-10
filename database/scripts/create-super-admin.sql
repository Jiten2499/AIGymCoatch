/*
    =============================================================================
    Create (or repair) a platform SuperAdmin in the LifeFit AI production database.
    =============================================================================

    WHAT THIS DOES
      Everything required for a working SuperAdmin, and nothing else:
        1. app.Users            - the login account, home tenant = the platform tenant.
        2. app.TenantMembers    - active membership of the platform tenant.
        3. app.UserRoles        - the SuperAdmin role on the platform tenant.

      The SuperAdmin role already owns every permission, including PLATFORM_ADMIN,
      which is what the API's RequireSuperAdminAsync check requires (SuperAdmin role
      name + PLATFORM_ADMIN permission). This script does NOT touch the role or
      permission catalog; those are owned by Liquibase migrations.

    BEFORE YOU RUN
      1. Generate the password hash and salt with:
             database/scripts/generate-super-admin-hash.ps1
         The iteration count must match the deployment's Password:Iterations
         (default 210000). Never store a plaintext password here.
      2. Fill in the @Email, @FirstName, @LastName, @PasswordHash and @PasswordSalt
         variables below.
      3. Take a database backup. Run against the production database, e.g.:
             sqlcmd -S <server> -d <database> -G -I -i create-super-admin.sql
         (-I = QUOTED_IDENTIFIER ON, required by the filtered unique indexes.)

    SAFE TO RE-RUN
      Idempotent. If the e-mail already exists it is repaired in place (password reset,
      re-activated, re-linked, SuperAdmin re-granted) instead of duplicated. No physical
      deletes. The whole operation runs in a single transaction and rolls back on any
      validation failure.
    =============================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

------------------------------------------------------------------------------
-- 1. INPUTS - edit these.
------------------------------------------------------------------------------
DECLARE @Email        NVARCHAR(256) = N'hello@mindgridtech.com';
DECLARE @FirstName    NVARCHAR(100) = N'Jitender';
DECLARE @LastName     NVARCHAR(100) = N'Khatri';
DECLARE @Phone        NVARCHAR(32)  = NULL;                 -- optional, must be unique if set
DECLARE @PasswordHash NVARCHAR(512) = N'cU+mlZApRs0oPlonHve/ek/yyy0nznXE0uIXqSaYh0M=';  -- from the PowerShell helper
DECLARE @PasswordSalt NVARCHAR(128) = N'tDPREa/D+sPpTkmkQ6mlVA==';  -- from the PowerShell helper

------------------------------------------------------------------------------
-- 2. CONSTANTS - do not edit.
------------------------------------------------------------------------------
DECLARE @PlatformTenantId UNIQUEIDENTIFIER = '33333333-3333-4333-8333-333333333001';
DECLARE @Now              DATETIME2 = SYSUTCDATETIME();
DECLARE @SuperAdminRoleId UNIQUEIDENTIFIER;
DECLARE @UserId           UNIQUEIDENTIFIER;

BEGIN TRY
    BEGIN TRANSACTION;

    --------------------------------------------------------------------------
    -- 3. Validate inputs.
    --------------------------------------------------------------------------
    IF @Email LIKE N'REPLACE[_]%' OR @FirstName LIKE N'REPLACE[_]%' OR @LastName LIKE N'REPLACE[_]%'
        OR @PasswordHash LIKE N'REPLACE[_]%' OR @PasswordSalt LIKE N'REPLACE[_]%'
        THROW 60001, N'Fill in the @Email, @FirstName, @LastName, @PasswordHash and @PasswordSalt variables before running.', 1;

    IF @Email IS NULL OR LEN(LTRIM(RTRIM(@Email))) = 0 OR @Email NOT LIKE N'%_@_%_._%'
        THROW 60002, N'@Email is not a valid e-mail address.', 1;

    -- A 32-byte PBKDF2 hash is 44 Base64 chars; a 16-byte salt is 24 Base64 chars.
    -- Guards against pasting the wrong value or swapping hash and salt.
    IF LEN(@PasswordHash) <> 44
        THROW 60003, N'@PasswordHash does not look like a 32-byte Base64 value (expected length 44). Regenerate it with generate-super-admin-hash.ps1.', 1;
    IF LEN(@PasswordSalt) <> 24
        THROW 60004, N'@PasswordSalt does not look like a 16-byte Base64 value (expected length 24). Regenerate it with generate-super-admin-hash.ps1.', 1;

    --------------------------------------------------------------------------
    -- 4. Verify the platform prerequisites exist (seeded by Liquibase).
    --------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM app.Tenants WHERE Id = @PlatformTenantId AND Status = N'Active' AND DeletedAt IS NULL)
        THROW 60010, N'The platform tenant is missing or not active. Run the Liquibase migrations first.', 1;

    SELECT @SuperAdminRoleId = Id FROM app.Roles WHERE Name = N'SuperAdmin' AND DeletedAt IS NULL;
    IF @SuperAdminRoleId IS NULL
        THROW 60011, N'The SuperAdmin role is missing. Run the Liquibase migrations first.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM app.RolePermissions rp
        JOIN app.Permissions p ON p.Id = rp.PermissionId
        WHERE rp.RoleId = @SuperAdminRoleId AND p.Code = N'PLATFORM_ADMIN' AND p.DeletedAt IS NULL)
        THROW 60012, N'The SuperAdmin role does not have PLATFORM_ADMIN. Run the Liquibase migrations first.', 1;

    -- Guard against a phone clash with another active user.
    IF @Phone IS NOT NULL AND EXISTS (
        SELECT 1 FROM app.Users WHERE Phone = @Phone AND Email <> @Email AND DeletedAt IS NULL)
        THROW 60013, N'That phone number already belongs to another active user.', 1;

    --------------------------------------------------------------------------
    -- 5. Upsert the user account (home tenant = platform tenant).
    --------------------------------------------------------------------------
    SELECT @UserId = Id FROM app.Users WHERE Email = @Email AND DeletedAt IS NULL;

    IF @UserId IS NULL
    BEGIN
        SET @UserId = NEWID();
        INSERT INTO app.Users
            (Id, TenantId, Email, Phone, PasswordHash, PasswordSalt, FirstName, LastName, Status, CreatedAt, UpdatedAt)
        VALUES
            (@UserId, @PlatformTenantId, @Email, @Phone, @PasswordHash, @PasswordSalt, @FirstName, @LastName, N'Active', @Now, @Now);
    END
    ELSE
    BEGIN
        -- Repair an existing account: reset password, name, contact and re-activate.
        UPDATE app.Users
        SET TenantId     = @PlatformTenantId,
            Phone        = @Phone,
            PasswordHash = @PasswordHash,
            PasswordSalt = @PasswordSalt,
            FirstName    = @FirstName,
            LastName     = @LastName,
            Status       = N'Active',
            UpdatedAt    = @Now
        WHERE Id = @UserId;
    END

    --------------------------------------------------------------------------
    -- 6. Ensure active membership of the platform tenant.
    --------------------------------------------------------------------------
    IF EXISTS (SELECT 1 FROM app.TenantMembers WHERE TenantId = @PlatformTenantId AND UserId = @UserId AND DeletedAt IS NULL)
        UPDATE app.TenantMembers
        SET Status = N'Active', UpdatedAt = @Now
        WHERE TenantId = @PlatformTenantId AND UserId = @UserId AND DeletedAt IS NULL;
    ELSE
        INSERT INTO app.TenantMembers (Id, TenantId, UserId, Status, JoinedAt, CreatedAt, UpdatedAt)
        VALUES (NEWID(), @PlatformTenantId, @UserId, N'Active', @Now, @Now, @Now);

    --------------------------------------------------------------------------
    -- 7. Ensure the SuperAdmin role on the platform tenant.
    --------------------------------------------------------------------------
    IF NOT EXISTS (
        SELECT 1 FROM app.UserRoles
        WHERE UserId = @UserId AND RoleId = @SuperAdminRoleId AND TenantId = @PlatformTenantId AND DeletedAt IS NULL)
        INSERT INTO app.UserRoles (Id, UserId, RoleId, TenantId, CreatedAt, UpdatedAt)
        VALUES (NEWID(), @UserId, @SuperAdminRoleId, @PlatformTenantId, @Now, @Now);

    COMMIT TRANSACTION;

    --------------------------------------------------------------------------
    -- 8. Report.
    --------------------------------------------------------------------------
    SELECT
        u.Id            AS UserId,
        u.Email,
        u.FirstName,
        u.LastName,
        u.Status        AS UserStatus,
        tm.Status       AS PlatformMembership,
        r.Name          AS RoleName
    FROM app.Users u
    JOIN app.TenantMembers tm ON tm.UserId = u.Id AND tm.TenantId = @PlatformTenantId AND tm.DeletedAt IS NULL
    JOIN app.UserRoles ur ON ur.UserId = u.Id AND ur.TenantId = @PlatformTenantId AND ur.DeletedAt IS NULL
    JOIN app.Roles r ON r.Id = ur.RoleId
    WHERE u.Id = @UserId;

    PRINT 'SuperAdmin is ready. Sign in with the e-mail and the password used to generate the hash.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    DECLARE @Message NVARCHAR(2048) = ERROR_MESSAGE();
    DECLARE @ErrNum  INT = ERROR_NUMBER();
    PRINT CONCAT('SuperAdmin creation failed (error ', @ErrNum, '): ', @Message);
    THROW;
END CATCH;
