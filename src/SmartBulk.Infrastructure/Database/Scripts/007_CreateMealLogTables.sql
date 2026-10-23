IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MealLog')
BEGIN
    CREATE TABLE MealLog (
        Id                 INT IDENTITY(1,1) PRIMARY KEY,
        Uuid               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        Date               DATE NOT NULL,
        LoggedAt           DATETIMEOFFSET NOT NULL,
        Name               NVARCHAR(100) NOT NULL,
        TotalCalories      DECIMAL(10,2) NOT NULL DEFAULT 0,
        TotalProtein       DECIMAL(10,2) NOT NULL DEFAULT 0,
        TotalCarbohydrates DECIMAL(10,2) NOT NULL DEFAULT 0,
        TotalFat           DECIMAL(10,2) NOT NULL DEFAULT 0,
        CreatedAt          DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt          DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        Deleted            BIT NOT NULL DEFAULT 0
    );

    CREATE UNIQUE INDEX UX_MealLog_Uuid ON MealLog(Uuid);
    CREATE INDEX IX_MealLog_Date_Deleted ON MealLog(Date, Deleted);
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MealLogItem')
BEGIN
    CREATE TABLE MealLogItem (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        Uuid          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        MealLogId     INT NOT NULL FOREIGN KEY REFERENCES MealLog(Id),
        FoodId        INT NOT NULL FOREIGN KEY REFERENCES Food(Id),
        ServingGrams  DECIMAL(10,2) NOT NULL,
        Calories      DECIMAL(10,2) NOT NULL,
        Protein       DECIMAL(10,2) NOT NULL,
        Carbohydrates DECIMAL(10,2) NOT NULL,
        Fat           DECIMAL(10,2) NOT NULL,
        CreatedAt     DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt     DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        Deleted       BIT NOT NULL DEFAULT 0
    );

    CREATE UNIQUE INDEX UX_MealLogItem_Uuid ON MealLogItem(Uuid);
    CREATE INDEX IX_MealLogItem_MealLogId_Deleted ON MealLogItem(MealLogId, Deleted);
END;
