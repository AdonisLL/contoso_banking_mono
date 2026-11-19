-- Contoso Legacy Bank - Notifications queue business logic
USE ContosoLegacyBank;
GO

IF OBJECT_ID(N'dbo.usp_Notification_Enqueue', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Notification_Enqueue;
IF OBJECT_ID(N'dbo.usp_Notification_ClaimPending', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Notification_ClaimPending;
IF OBJECT_ID(N'dbo.usp_Notification_MarkSent', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Notification_MarkSent;
IF OBJECT_ID(N'dbo.usp_Notification_MarkFailed', N'P') IS NOT NULL DROP PROCEDURE dbo.usp_Notification_MarkFailed;
GO

CREATE PROCEDURE dbo.usp_Notification_Enqueue
    @CustomerId INT,
    @TemplateCode VARCHAR(50),
    @Payload NVARCHAR(MAX),
    @Channel VARCHAR(20) = N'Email',
    @NotificationId BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Notifications (CustomerId, TemplateCode, Payload, Channel, Status)
    VALUES (@CustomerId, @TemplateCode, @Payload, @Channel, N'Pending');
    SET @NotificationId = SCOPE_IDENTITY();
END
GO

-- Atomically claims up to @BatchSize pending notifications for this worker instance using
-- UPDATE ... OUTPUT so two concurrent worker processes never send the same notification twice.
CREATE PROCEDURE dbo.usp_Notification_ClaimPending
    @BatchSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Claimed TABLE (NotificationId BIGINT PRIMARY KEY);

    UPDATE TOP (@BatchSize) n
        SET Status = N'Processing'
        OUTPUT inserted.NotificationId INTO @Claimed
    FROM dbo.Notifications n
    WHERE Status = N'Pending';

    SELECT n.NotificationId, n.CustomerId, n.TemplateCode, n.Payload, n.Channel, n.CreatedUtc,
           t.Subject, t.BodyTemplate,
           c.FirstName, c.LastName, c.Email, c.Phone
    FROM dbo.Notifications n
    JOIN @Claimed cl ON cl.NotificationId = n.NotificationId
    JOIN dbo.NotificationTemplates t ON t.Code = n.TemplateCode
    JOIN dbo.Customers c ON c.CustomerId = n.CustomerId
    ORDER BY n.CreatedUtc;
END
GO

CREATE PROCEDURE dbo.usp_Notification_MarkSent
    @NotificationId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Notifications SET Status = N'Sent', SentUtc = SYSUTCDATETIME() WHERE NotificationId = @NotificationId;
END
GO

CREATE PROCEDURE dbo.usp_Notification_MarkFailed
    @NotificationId BIGINT,
    @FailureReason NVARCHAR(400)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Notifications SET Status = N'Failed', FailureReason = @FailureReason WHERE NotificationId = @NotificationId;
END
GO
