SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @appLockResult int;
    EXEC @appLockResult = sys.sp_getapplock
        @Resource = N'Sprint3_Task03_02_BackfillUserIdentityId',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 60000;

    IF @appLockResult < 0
        RAISERROR(N'Unable to acquire the User.IdentityId backfill lock.', 16, 1);

    IF EXISTS
    (
        SELECT 1
        FROM dbo.[用户管理] WITH (UPDLOCK, HOLDLOCK)
        WHERE IdentityId IS NOT NULL
        GROUP BY IdentityId
        HAVING COUNT_BIG(*) > 1
    )
        RAISERROR(N'Duplicate non-null IdentityId values exist; backfill aborted.', 16, 1);

    IF EXISTS
    (
        SELECT 1
        FROM dbo.[用户管理] WITH (UPDLOCK, HOLDLOCK)
        WHERE Id IS NULL OR LTRIM(RTRIM(Id)) = N''
    )
        RAISERROR(N'Null or empty User.Id values exist; deterministic backfill aborted.', 16, 1);

    IF EXISTS
    (
        SELECT 1
        FROM dbo.[用户管理] WITH (UPDLOCK, HOLDLOCK)
        GROUP BY Id
        HAVING COUNT_BIG(*) > 1
    )
        RAISERROR(N'Duplicate User.Id values exist; deterministic backfill aborted.', 16, 1);

    DECLARE @rowsToBackfill bigint =
    (
        SELECT COUNT_BIG(*)
        FROM dbo.[用户管理] WITH (UPDLOCK, HOLDLOCK)
        WHERE IdentityId IS NULL
    );

    DECLARE @baseIdentityId bigint =
    (
        SELECT ISNULL(MAX(IdentityId), CONVERT(bigint, 0))
        FROM dbo.[用户管理] WITH (UPDLOCK, HOLDLOCK)
    );

    IF @baseIdentityId < 0
        SET @baseIdentityId = 0;

    IF @rowsToBackfill > 9223372036854775807 - @baseIdentityId
        RAISERROR(N'IdentityId range overflow; backfill aborted.', 16, 1);

    ;WITH IdentityAssignments AS
    (
        SELECT
            Id,
            @baseIdentityId + ROW_NUMBER() OVER (ORDER BY Id) AS NewIdentityId
        FROM dbo.[用户管理] WITH (UPDLOCK, HOLDLOCK)
        WHERE IdentityId IS NULL
    )
    UPDATE userRecord
    SET IdentityId = assignments.NewIdentityId
    FROM dbo.[用户管理] AS userRecord
    INNER JOIN IdentityAssignments AS assignments
        ON assignments.Id = userRecord.Id
    WHERE userRecord.IdentityId IS NULL;

    IF @@ROWCOUNT <> @rowsToBackfill
        RAISERROR(N'Updated row count does not match the planned backfill count.', 16, 1);

    IF EXISTS (SELECT 1 FROM dbo.[用户管理] WHERE IdentityId IS NULL)
        RAISERROR(N'Null IdentityId values remain after backfill.', 16, 1);

    IF EXISTS
    (
        SELECT 1
        FROM dbo.[用户管理]
        GROUP BY IdentityId
        HAVING COUNT_BIG(*) > 1
    )
        RAISERROR(N'Duplicate IdentityId values detected after backfill.', 16, 1);

    COMMIT TRANSACTION;

    SELECT
        COUNT_BIG(*) AS UserRowCount,
        MIN(IdentityId) AS MinIdentityId,
        MAX(IdentityId) AS MaxIdentityId,
        SUM(CASE WHEN IdentityId IS NULL THEN 1 ELSE 0 END) AS NullIdentityIdCount
    FROM dbo.[用户管理];
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    DECLARE @errorMessage nvarchar(2048) = ERROR_MESSAGE();
    RAISERROR(@errorMessage, 16, 1);
END CATCH;
