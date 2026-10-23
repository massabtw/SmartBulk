IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Food') AND name = 'IsFavorite')
BEGIN
    ALTER TABLE Food ADD IsFavorite BIT NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MealTemplate')
BEGIN
    CREATE TABLE MealTemplate (
        Id        INT IDENTITY(1,1) PRIMARY KEY,
        Uuid      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        Name      NVARCHAR(100) NOT NULL,
        MealType  NVARCHAR(50) NOT NULL, -- 'Breakfast', 'Shake', 'FullMeal'
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        Deleted   BIT NOT NULL DEFAULT 0
    );

    CREATE UNIQUE INDEX UX_MealTemplate_Uuid ON MealTemplate(Uuid);
    CREATE INDEX IX_MealTemplate_MealType_Deleted ON MealTemplate(MealType, Deleted);
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MealTemplateItem')
BEGIN
    CREATE TABLE MealTemplateItem (
        Id             INT IDENTITY(1,1) PRIMARY KEY,
        Uuid           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        MealTemplateId INT NOT NULL FOREIGN KEY REFERENCES MealTemplate(Id),
        FoodId         INT NOT NULL FOREIGN KEY REFERENCES Food(Id),
        ServingGrams   DECIMAL(10,2) NOT NULL,
        CreatedAt      DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt      DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        Deleted        BIT NOT NULL DEFAULT 0
    );

    CREATE UNIQUE INDEX UX_MealTemplateItem_Uuid ON MealTemplateItem(Uuid);
    CREATE INDEX IX_MealTemplateItem_MealTemplateId_Deleted ON MealTemplateItem(MealTemplateId, Deleted);
END;
