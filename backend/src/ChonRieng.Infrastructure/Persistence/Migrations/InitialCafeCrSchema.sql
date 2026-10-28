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
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [Branches] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Address] nvarchar(300) NOT NULL,
        [Lat] decimal(9,6) NULL,
        [Lng] decimal(9,6) NULL,
        [Is24h] bit NOT NULL,
        [OpenTime] time NOT NULL,
        [CloseTime] time NOT NULL,
        [Phone] nvarchar(32) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Branches] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(32) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [PricePlans] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NULL,
        [Name] nvarchar(120) NOT NULL,
        [DurationMinutes] int NOT NULL,
        [Price] decimal(12,0) NOT NULL,
        [ExtendPrice] decimal(12,0) NOT NULL,
        [ValidFrom] datetimeoffset NULL,
        [ValidTo] datetimeoffset NULL,
        CONSTRAINT [PK_PricePlans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PricePlans_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [Shifts] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        [Type] tinyint NOT NULL,
        [StartAt] datetimeoffset NOT NULL,
        [EndAt] datetimeoffset NOT NULL,
        [RequiredStaff] int NOT NULL,
        [IsOpen] bit NOT NULL,
        [Status] tinyint NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Shifts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Shifts_TimeRange] CHECK ([EndAt] > [StartAt]),
        CONSTRAINT [FK_Shifts_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [Zones] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Zones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Zones_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [Customers] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Points] int NOT NULL,
        [Tier] nvarchar(40) NOT NULL,
        [TotalSpent] decimal(12,0) NOT NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Customers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [Employees] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [HourlyRate] decimal(12,0) NOT NULL,
        [NightAllowance] decimal(12,0) NOT NULL,
        [PinHash] nvarchar(256) NOT NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Employees_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Type] nvarchar(64) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Body] nvarchar(2000) NOT NULL,
        [Link] nvarchar(500) NULL,
        [ReadAt] datetimeoffset NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [UserSessions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [DeviceId] nvarchar(200) NOT NULL,
        [IsPos] bit NOT NULL,
        [BranchId] uniqueidentifier NULL,
        [RefreshTokenHash] nvarchar(256) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [RevokedAt] datetimeoffset NULL,
        CONSTRAINT [PK_UserSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserSessions_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserSessions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [Seats] (
        [Id] uniqueidentifier NOT NULL,
        [ZoneId] uniqueidentifier NOT NULL,
        [Code] nvarchar(32) NOT NULL,
        [Type] tinyint NOT NULL,
        [Status] tinyint NOT NULL,
        CONSTRAINT [PK_Seats] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Seats_Zones_ZoneId] FOREIGN KEY ([ZoneId]) REFERENCES [Zones] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [Attendances] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [ShiftId] uniqueidentifier NOT NULL,
        [CheckInAt] datetimeoffset NULL,
        [CheckOutAt] datetimeoffset NULL,
        [Method] nvarchar(32) NOT NULL,
        [Approved] bit NOT NULL,
        CONSTRAINT [PK_Attendances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Attendances_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Attendances_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [Availabilities] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [IsRecurring] bit NOT NULL,
        [DayOfWeek] tinyint NULL,
        [SpecificDate] date NULL,
        [From] time NOT NULL,
        [To] time NOT NULL,
        CONSTRAINT [PK_Availabilities] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Availabilities_Recurrence] CHECK (([IsRecurring] = 1 AND [DayOfWeek] IS NOT NULL AND [SpecificDate] IS NULL) OR ([IsRecurring] = 0 AND [DayOfWeek] IS NULL AND [SpecificDate] IS NOT NULL)),
        CONSTRAINT [CK_Availabilities_TimeRange] CHECK ([From] < [To]),
        CONSTRAINT [FK_Availabilities_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [EmployeeBranches] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [IsHome] bit NOT NULL,
        CONSTRAINT [PK_EmployeeBranches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeBranches_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_EmployeeBranches_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [Expenses] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [Category] nvarchar(80) NOT NULL,
        [Amount] decimal(12,0) NOT NULL,
        [Date] datetimeoffset NOT NULL,
        [Note] nvarchar(1000) NULL,
        [CreatedByEmployeeId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Expenses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Expenses_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Expenses_Employees_CreatedByEmployeeId] FOREIGN KEY ([CreatedByEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [Payrolls] (
        [Id] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Month] date NOT NULL,
        [TotalHours] decimal(8,2) NOT NULL,
        [BaseAmount] decimal(12,0) NOT NULL,
        [Allowance] decimal(12,0) NOT NULL,
        [Bonus] decimal(12,0) NOT NULL,
        [Deduction] decimal(12,0) NOT NULL,
        [NetAmount] decimal(12,0) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_Payrolls] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payrolls_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [ShiftAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [ShiftId] uniqueidentifier NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Status] tinyint NOT NULL,
        [RespondedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ShiftAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShiftAssignments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShiftAssignments_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [ShiftClosings] (
        [Id] uniqueidentifier NOT NULL,
        [ShiftId] uniqueidentifier NOT NULL,
        [ClosedByEmployeeId] uniqueidentifier NOT NULL,
        [ExpectedCash] decimal(12,0) NOT NULL,
        [CountedCash] decimal(12,0) NOT NULL,
        [Difference] decimal(12,0) NOT NULL,
        [Note] nvarchar(1000) NULL,
        [Denominations] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ShiftClosings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShiftClosings_Employees_ClosedByEmployeeId] FOREIGN KEY ([ClosedByEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShiftClosings_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [ShiftRequests] (
        [Id] uniqueidentifier NOT NULL,
        [Type] tinyint NOT NULL,
        [ShiftId] uniqueidentifier NOT NULL,
        [RequesterEmployeeId] uniqueidentifier NOT NULL,
        [TargetEmployeeId] uniqueidentifier NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [ReviewedByEmployeeId] uniqueidentifier NULL,
        [ReviewedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ShiftRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShiftRequests_Employees_RequesterEmployeeId] FOREIGN KEY ([RequesterEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShiftRequests_Employees_ReviewedByEmployeeId] FOREIGN KEY ([ReviewedByEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShiftRequests_Employees_TargetEmployeeId] FOREIGN KEY ([TargetEmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShiftRequests_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [Sessions] (
        [Id] uniqueidentifier NOT NULL,
        [SeatId] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NULL,
        [PricePlanId] uniqueidentifier NOT NULL,
        [StartAt] datetimeoffset NOT NULL,
        [EndAt] datetimeoffset NOT NULL,
        [Status] tinyint NOT NULL,
        CONSTRAINT [PK_Sessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Sessions_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Sessions_PricePlans_PricePlanId] FOREIGN KEY ([PricePlanId]) REFERENCES [PricePlans] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Sessions_Seats_SeatId] FOREIGN KEY ([SeatId]) REFERENCES [Seats] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [Bills] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(8) NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [ShiftId] uniqueidentifier NOT NULL,
        [SessionId] uniqueidentifier NOT NULL,
        [Total] decimal(12,0) NOT NULL,
        [PaymentMethod] tinyint NOT NULL,
        [ClaimedByCustomerId] uniqueidentifier NULL,
        [ClaimedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Bills] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Bills_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Bills_Customers_ClaimedByCustomerId] FOREIGN KEY ([ClaimedByCustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Bills_Sessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [Sessions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Bills_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [SessionExtensions] (
        [Id] uniqueidentifier NOT NULL,
        [SessionId] uniqueidentifier NOT NULL,
        [AddedMinutes] int NOT NULL,
        [Price] decimal(12,0) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SessionExtensions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SessionExtensions_Sessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [Sessions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE TABLE [PointTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [BillId] uniqueidentifier NULL,
        [Points] int NOT NULL,
        [Reason] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_PointTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PointTransactions_Bills_BillId] FOREIGN KEY ([BillId]) REFERENCES [Bills] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PointTransactions_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Attendances_EmployeeId_ShiftId] ON [Attendances] ([EmployeeId], [ShiftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_Attendances_ShiftId] ON [Attendances] ([ShiftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_Availabilities_EmployeeId_DayOfWeek_SpecificDate] ON [Availabilities] ([EmployeeId], [DayOfWeek], [SpecificDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_Bills_BranchId] ON [Bills] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_Bills_ClaimedByCustomerId] ON [Bills] ([ClaimedByCustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Bills_Code] ON [Bills] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Bills_SessionId] ON [Bills] ([SessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_Bills_ShiftId] ON [Bills] ([ShiftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Customers_UserId] ON [Customers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_EmployeeBranches_BranchId] ON [EmployeeBranches] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EmployeeBranches_EmployeeId] ON [EmployeeBranches] ([EmployeeId]) WHERE [IsHome] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EmployeeBranches_EmployeeId_BranchId] ON [EmployeeBranches] ([EmployeeId], [BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Employees_UserId] ON [Employees] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_Expenses_BranchId_Date] ON [Expenses] ([BranchId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_Expenses_CreatedByEmployeeId] ON [Expenses] ([CreatedByEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_ReadAt] ON [Notifications] ([UserId], [ReadAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payrolls_EmployeeId_Month] ON [Payrolls] ([EmployeeId], [Month]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_PointTransactions_BillId] ON [PointTransactions] ([BillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_PointTransactions_CustomerId] ON [PointTransactions] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_PricePlans_BranchId] ON [PricePlans] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Seats_ZoneId_Code] ON [Seats] ([ZoneId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_SessionExtensions_SessionId] ON [SessionExtensions] ([SessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_Sessions_CustomerId] ON [Sessions] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_Sessions_PricePlanId] ON [Sessions] ([PricePlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_Sessions_SeatId_Status] ON [Sessions] ([SeatId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_ShiftAssignments_EmployeeId] ON [ShiftAssignments] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ShiftAssignments_ShiftId_EmployeeId] ON [ShiftAssignments] ([ShiftId], [EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_ShiftClosings_ClosedByEmployeeId] ON [ShiftClosings] ([ClosedByEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ShiftClosings_ShiftId] ON [ShiftClosings] ([ShiftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_ShiftRequests_RequesterEmployeeId] ON [ShiftRequests] ([RequesterEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_ShiftRequests_ReviewedByEmployeeId] ON [ShiftRequests] ([ReviewedByEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_ShiftRequests_ShiftId] ON [ShiftRequests] ([ShiftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_ShiftRequests_TargetEmployeeId] ON [ShiftRequests] ([TargetEmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_Shifts_BranchId_StartAt_EndAt] ON [Shifts] ([BranchId], [StartAt], [EndAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [EmailIndex] ON [Users] ([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_PhoneNumber] ON [Users] ([PhoneNumber]) WHERE [PhoneNumber] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [Users] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_UserSessions_BranchId] ON [UserSessions] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE INDEX [IX_UserSessions_UserId] ON [UserSessions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_UserSessions_UserId_IsPos] ON [UserSessions] ([UserId], [IsPos]) WHERE [RevokedAt] IS NULL AND [IsPos] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Zones_BranchId_Name] ON [Zones] ([BranchId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028050512_InitialCafeCrSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261028050512_InitialCafeCrSchema', N'10.0.5');
END;

COMMIT;
GO

