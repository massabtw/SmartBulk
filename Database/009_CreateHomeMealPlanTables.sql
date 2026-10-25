IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HomeMealPlan')
BEGIN
    CREATE TABLE [HomeMealPlan] (
        [Id]            INT                IDENTITY (1, 1) NOT NULL,
        [Uuid]          UNIQUEIDENTIFIER   DEFAULT (newid()) NOT NULL,
        [WeekStartDate] DATE               NOT NULL,
        [CreatedAt]     DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [UpdatedAt]     DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [Deleted]       BIT                DEFAULT ((0)) NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_HomeMealPlan_Uuid ON [HomeMealPlan] ([Uuid]);
    CREATE UNIQUE NONCLUSTERED INDEX UX_HomeMealPlan_WeekStartDate ON [HomeMealPlan] ([WeekStartDate]) WHERE [Deleted] = 0;
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HomeMealPlanItem')
BEGIN
    CREATE TABLE [HomeMealPlanItem] (
        [Id]             INT                IDENTITY (1, 1) NOT NULL,
        [Uuid]           UNIQUEIDENTIFIER   DEFAULT (newid()) NOT NULL,
        [HomeMealPlanId] INT                NOT NULL,
        [Date]           DATE               NOT NULL,
        [MealSlot]       NVARCHAR (50)      NOT NULL,
        [MealTemplateId] INT                NOT NULL,
        [CreatedAt]      DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [UpdatedAt]      DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [Deleted]        BIT                DEFAULT ((0)) NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT FK_HomeMealPlanItem_HomeMealPlan FOREIGN KEY ([HomeMealPlanId]) REFERENCES [HomeMealPlan] ([Id]),
        CONSTRAINT FK_HomeMealPlanItem_MealTemplate FOREIGN KEY ([MealTemplateId]) REFERENCES [MealTemplate] ([Id])
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_HomeMealPlanItem_Uuid ON [HomeMealPlanItem] ([Uuid]);
    CREATE NONCLUSTERED INDEX IX_HomeMealPlanItem_PlanId_Date ON [HomeMealPlanItem] ([HomeMealPlanId], [Date], [Deleted]);
END;

