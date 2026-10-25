IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ShoppingList')
BEGIN
    CREATE TABLE ShoppingList (
        Id INT IDENTITY(1, 1) NOT NULL,
        Uuid UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        WeekStartDate DATE NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        Deleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT PK_ShoppingList PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT UQ_ShoppingList_Uuid UNIQUE NONCLUSTERED (Uuid ASC)
    );

    CREATE NONCLUSTERED INDEX IX_ShoppingList_WeekStartDate ON ShoppingList (WeekStartDate ASC) WHERE Deleted = 0;
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ShoppingListItem')
BEGIN
    CREATE TABLE ShoppingListItem (
        Id INT IDENTITY(1, 1) NOT NULL,
        Uuid UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        ShoppingListId INT NOT NULL,
        FoodId INT NOT NULL,
        PlannedGrams DECIMAL(10, 2) NOT NULL,
        RawGrams DECIMAL(10, 2) NOT NULL,
        IsPurchased BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        Deleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT PK_ShoppingListItem PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT UQ_ShoppingListItem_Uuid UNIQUE NONCLUSTERED (Uuid ASC),
        CONSTRAINT FK_ShoppingListItem_ShoppingList FOREIGN KEY (ShoppingListId) REFERENCES ShoppingList (Id),
        CONSTRAINT FK_ShoppingListItem_Food FOREIGN KEY (FoodId) REFERENCES Food (Id)
    );

    CREATE NONCLUSTERED INDEX IX_ShoppingListItem_ShoppingListId ON ShoppingListItem (ShoppingListId ASC) WHERE Deleted = 0;
    CREATE NONCLUSTERED INDEX IX_ShoppingListItem_FoodId ON ShoppingListItem (FoodId ASC) WHERE Deleted = 0;
END;

