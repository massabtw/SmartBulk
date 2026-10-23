IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CafeteriaMenu')
BEGIN
    CREATE TABLE [CafeteriaMenu] (
        [Id]                 INT                IDENTITY (1, 1) NOT NULL,
        [Uuid]               UNIQUEIDENTIFIER   DEFAULT (newid()) NOT NULL,
        [WeekStartDate]      DATE               NOT NULL,
        [Status]             VARCHAR (20)       NOT NULL DEFAULT ('Draft'),
        [CreatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [UpdatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [Deleted]            BIT                DEFAULT ((0)) NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE NONCLUSTERED INDEX IX_CafeteriaMenu_WeekStartDate ON [CafeteriaMenu] ([WeekStartDate], [Deleted]);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CafeteriaMeal')
BEGIN
    CREATE TABLE [CafeteriaMeal] (
        [Id]                 INT                IDENTITY (1, 1) NOT NULL,
        [Uuid]               UNIQUEIDENTIFIER   DEFAULT (newid()) NOT NULL,
        [CafeteriaMenuId]    INT                NOT NULL,
        [Date]               DATE               NOT NULL,
        [MealType]           VARCHAR (20)       NOT NULL,
        [PlateProfile]       VARCHAR (20)       NOT NULL DEFAULT ('Standard'),
        [CreatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [UpdatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [Deleted]            BIT                DEFAULT ((0)) NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT FK_CafeteriaMeal_Menu FOREIGN KEY ([CafeteriaMenuId]) REFERENCES [CafeteriaMenu] ([Id])
    );

    CREATE NONCLUSTERED INDEX IX_CafeteriaMeal_MenuId ON [CafeteriaMeal] ([CafeteriaMenuId], [Deleted]);
    CREATE NONCLUSTERED INDEX IX_CafeteriaMeal_Date ON [CafeteriaMeal] ([Date], [Deleted]);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CafeteriaMealItem')
BEGIN
    CREATE TABLE [CafeteriaMealItem] (
        [Id]                 INT                IDENTITY (1, 1) NOT NULL,
        [Uuid]               UNIQUEIDENTIFIER   DEFAULT (newid()) NOT NULL,
        [CafeteriaMealId]    INT                NOT NULL,
        [FoodId]             INT                NULL,
        [RawName]            NVARCHAR (200)     COLLATE Latin1_General_CI_AI NOT NULL,
        [ServingGrams]       DECIMAL (6, 2)     NOT NULL,
        [IsPending]          BIT                NOT NULL DEFAULT 0,
        [CreatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [UpdatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [Deleted]            BIT                DEFAULT ((0)) NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT FK_CafeteriaMealItem_Meal FOREIGN KEY ([CafeteriaMealId]) REFERENCES [CafeteriaMeal] ([Id]),
        CONSTRAINT FK_CafeteriaMealItem_Food FOREIGN KEY ([FoodId]) REFERENCES [Food] ([Id])
    );

    CREATE NONCLUSTERED INDEX IX_CafeteriaMealItem_MealId ON [CafeteriaMealItem] ([CafeteriaMealId], [Deleted]);
END
