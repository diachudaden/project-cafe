IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE TABLE [PaymentTransactions] (
        [SepayId] bigint NOT NULL,
        [Gateway] nvarchar(50) NOT NULL,
        [AccountNumber] nvarchar(50) NOT NULL,
        [SubAccount] nvarchar(50) NULL,
        [Code] nvarchar(50) NULL,
        [Content] nvarchar(500) NOT NULL,
        [Description] nvarchar(500) NULL,
        [ReferenceCode] nvarchar(100) NULL,
        [Amount] bigint NOT NULL,
        [TransactionAtUtc] datetime2 NOT NULL,
        [ReceivedAtUtc] datetime2 NOT NULL,
        [MatchStatus] tinyint NOT NULL,
        [BillId] int NULL,
        [Note] nvarchar(500) NULL,
        [ResolvedBy] int NULL,
        [ResolvedAtUtc] datetime2 NULL,
        [RawPayload] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_PaymentTransactions] PRIMARY KEY ([SepayId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE TABLE [ReconciliationIssues] (
        [Id] bigint NOT NULL IDENTITY,
        [BusinessDate] date NOT NULL,
        [Kind] tinyint NOT NULL,
        [PaymentTransactionId] bigint NULL,
        [LedgerTotal] bigint NOT NULL,
        [BankTotal] bigint NOT NULL,
        [Message] nvarchar(500) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ResolvedAtUtc] datetime2 NULL,
        [ResolvedBy] int NULL,
        CONSTRAINT [PK_ReconciliationIssues] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE TABLE [Seats] (
        [Id] int NOT NULL IDENTITY,
        [BranchId] int NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [SeatType] nvarchar(50) NOT NULL,
        [SortOrder] int NOT NULL,
        [Status] tinyint NOT NULL,
        [HeldForBillId] int NULL,
        [HeldUntil] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Seats] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE TABLE [Sessions] (
        [Id] int NOT NULL IDENTITY,
        [SeatId] int NOT NULL,
        [CustomerId] int NULL,
        [PricingPlanId] int NULL,
        [StartedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Sessions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE TABLE [Shifts] (
        [Id] int NOT NULL IDENTITY,
        [BranchId] int NOT NULL,
        [OpenedAt] datetime2 NOT NULL,
        [ClosedAt] datetime2 NULL,
        CONSTRAINT [PK_Shifts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE TABLE [LedgerEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [BranchId] int NOT NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        [Direction] tinyint NOT NULL,
        [Amount] bigint NOT NULL,
        [Category] tinyint NOT NULL,
        [Method] tinyint NOT NULL,
        [BillId] int NULL,
        [PaymentTransactionId] bigint NULL,
        [ShiftId] int NULL,
        [CreatedBy] int NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [Note] nvarchar(255) NULL,
        [ReversalOfId] bigint NULL,
        CONSTRAINT [PK_LedgerEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_LedgerEntries_Amount_Positive] CHECK ([Amount] > 0),
        CONSTRAINT [FK_LedgerEntries_LedgerEntries_ReversalOfId] FOREIGN KEY ([ReversalOfId]) REFERENCES [LedgerEntries] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LedgerEntries_PaymentTransactions_PaymentTransactionId] FOREIGN KEY ([PaymentTransactionId]) REFERENCES [PaymentTransactions] ([SepayId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE TABLE [Bills] (
        [Id] int NOT NULL IDENTITY,
        [BranchId] int NOT NULL,
        [CustomerId] int NULL,
        [PricingPlanId] int NULL,
        [Total] decimal(18,2) NOT NULL,
        [Status] tinyint NOT NULL,
        [IsSeatBill] bit NOT NULL,
        [RequestedSeatId] int NULL,
        [RequestedSeatType] nvarchar(max) NULL,
        [PaymentMethod] tinyint NULL,
        [AmountReceived] bigint NULL,
        [PaidAt] datetime2 NULL,
        [SessionId] int NULL,
        CONSTRAINT [PK_Bills] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Bills_Sessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [Sessions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE INDEX [IX_Bills_SessionId] ON [Bills] ([SessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE INDEX [IX_LedgerEntries_BillId] ON [LedgerEntries] ([BillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE INDEX [IX_LedgerEntries_BranchId_OccurredAtUtc] ON [LedgerEntries] ([BranchId], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE INDEX [IX_LedgerEntries_Method_Direction_OccurredAtUtc] ON [LedgerEntries] ([Method], [Direction], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE INDEX [IX_LedgerEntries_PaymentTransactionId] ON [LedgerEntries] ([PaymentTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_LedgerEntries_ReversalOfId] ON [LedgerEntries] ([ReversalOfId]) WHERE [ReversalOfId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE INDEX [IX_LedgerEntries_ShiftId] ON [LedgerEntries] ([ShiftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE INDEX [IX_PaymentTransactions_BillId] ON [PaymentTransactions] ([BillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE INDEX [IX_PaymentTransactions_MatchStatus_ResolvedAtUtc] ON [PaymentTransactions] ([MatchStatus], [ResolvedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE INDEX [IX_PaymentTransactions_TransactionAtUtc] ON [PaymentTransactions] ([TransactionAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE INDEX [IX_ReconciliationIssues_BusinessDate_ResolvedAtUtc] ON [ReconciliationIssues] ([BusinessDate], [ResolvedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE INDEX [IX_Seats_HeldForBillId] ON [Seats] ([HeldForBillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    CREATE INDEX [IX_Seats_Status_HeldUntil] ON [Seats] ([Status], [HeldUntil]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022034124_AddPayments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022034124_AddPayments', N'10.0.5');
END;

COMMIT;
GO

