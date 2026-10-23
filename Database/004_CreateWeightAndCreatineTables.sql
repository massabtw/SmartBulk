IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WeightLog')
BEGIN
    CREATE TABLE [WeightLog] (
        [Id]                 INT                IDENTITY (1, 1) NOT NULL,
        [Uuid]               UNIQUEIDENTIFIER   DEFAULT (newid()) NOT NULL,
        [Date]               DATE               NOT NULL,
        [WeightKg]           DECIMAL (5, 2)     NOT NULL,
        [CreatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [UpdatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [Deleted]            BIT                DEFAULT ((0)) NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UQ_WeightLog_Date_Active ON [WeightLog] ([Date]) WHERE [Deleted] = 0;
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CreatineLog')
BEGIN
    CREATE TABLE [CreatineLog] (
        [Id]                 INT                IDENTITY (1, 1) NOT NULL,
        [Uuid]               UNIQUEIDENTIFIER   DEFAULT (newid()) NOT NULL,
        [Date]               DATE               NOT NULL,
        [Taken]              BIT                NOT NULL DEFAULT 1,
        [CreatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [UpdatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [Deleted]            BIT                DEFAULT ((0)) NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UQ_CreatineLog_Date_Active ON [CreatineLog] ([Date]) WHERE [Deleted] = 0;
END
