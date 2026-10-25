IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MealLog')
BEGIN
    CREATE TABLE [MealLog] (
        [Id]                 INT                IDENTITY (1, 1) NOT NULL,
        [Uuid]               UNIQUEIDENTIFIER   DEFAULT (newid()) NOT NULL,
        [Date]               DATE               NOT NULL,
        [LoggedAt]           DATETIMEOFFSET (7) NOT NULL,
        [Name]               NVARCHAR (100)     NOT NULL,
        [TotalCalories]      DECIMAL (10, 2)    NOT NULL DEFAULT (0),
        [TotalProtein]       DECIMAL (10, 2)    NOT NULL DEFAULT (0),
        [TotalCarbohydrates] DECIMAL (10, 2)    NOT NULL DEFAULT (0),
        [TotalFat]           DECIMAL (10, 2)    NOT NULL DEFAULT (0),
        [CreatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [UpdatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [Deleted]            BIT                DEFAULT ((0)) NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_MealLog_Uuid ON [MealLog] ([Uuid]);
    CREATE NONCLUSTERED INDEX IX_MealLog_Date_Deleted ON [MealLog] ([Date], [Deleted]);
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MealLogItem')
BEGIN
    CREATE TABLE [MealLogItem] (
        [Id]            INT                IDENTITY (1, 1) NOT NULL,
        [Uuid]          UNIQUEIDENTIFIER   DEFAULT (newid()) NOT NULL,
        [MealLogId]     INT                NOT NULL,
        [FoodId]        INT                NOT NULL,
        [ServingGrams]  DECIMAL (10, 2)    NOT NULL,
        [Calories]      DECIMAL (10, 2)    NOT NULL,
        [Protein]       DECIMAL (10, 2)    NOT NULL,
        [Carbohydrates] DECIMAL (10, 2)    NOT NULL,
        [Fat]           DECIMAL (10, 2)    NOT NULL,
        [CreatedAt]     DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [UpdatedAt]     DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [Deleted]       BIT                DEFAULT ((0)) NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT FK_MealLogItem_MealLog FOREIGN KEY ([MealLogId]) REFERENCES [MealLog] ([Id]),
        CONSTRAINT FK_MealLogItem_Food FOREIGN KEY ([FoodId]) REFERENCES [Food] ([Id])
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_MealLogItem_Uuid ON [MealLogItem] ([Uuid]);
    CREATE NONCLUSTERED INDEX IX_MealLogItem_MealLogId_Deleted ON [MealLogItem] ([MealLogId], [Deleted]);
END;
