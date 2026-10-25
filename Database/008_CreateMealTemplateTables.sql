IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Food') AND name = 'IsFavorite')
BEGIN
    ALTER TABLE [Food] ADD [IsFavorite] BIT NOT NULL DEFAULT (0);
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MealTemplate')
BEGIN
    CREATE TABLE [MealTemplate] (
        [Id]        INT                IDENTITY (1, 1) NOT NULL,
        [Uuid]      UNIQUEIDENTIFIER   DEFAULT (newid()) NOT NULL,
        [Name]      NVARCHAR (100)     NOT NULL,
        [MealType]  NVARCHAR (50)      NOT NULL,
        [CreatedAt] DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [UpdatedAt] DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [Deleted]   BIT                DEFAULT ((0)) NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_MealTemplate_Uuid ON [MealTemplate] ([Uuid]);
    CREATE NONCLUSTERED INDEX IX_MealTemplate_MealType_Deleted ON [MealTemplate] ([MealType], [Deleted]);
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MealTemplateItem')
BEGIN
    CREATE TABLE [MealTemplateItem] (
        [Id]             INT                IDENTITY (1, 1) NOT NULL,
        [Uuid]           UNIQUEIDENTIFIER   DEFAULT (newid()) NOT NULL,
        [MealTemplateId] INT                NOT NULL,
        [FoodId]         INT                NOT NULL,
        [ServingGrams]   DECIMAL (10, 2)    NOT NULL,
        [CreatedAt]      DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [UpdatedAt]      DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [Deleted]        BIT                DEFAULT ((0)) NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT FK_MealTemplateItem_MealTemplate FOREIGN KEY ([MealTemplateId]) REFERENCES [MealTemplate] ([Id]),
        CONSTRAINT FK_MealTemplateItem_Food FOREIGN KEY ([FoodId]) REFERENCES [Food] ([Id])
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_MealTemplateItem_Uuid ON [MealTemplateItem] ([Uuid]);
    CREATE NONCLUSTERED INDEX IX_MealTemplateItem_MealTemplateId_Deleted ON [MealTemplateItem] ([MealTemplateId], [Deleted]);
END;
