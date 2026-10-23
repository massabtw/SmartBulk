IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CafeteriaDayOverride')
BEGIN
    CREATE TABLE [CafeteriaDayOverride] (
        [Id]                 INT                IDENTITY (1, 1) NOT NULL,
        [Uuid]               UNIQUEIDENTIFIER   DEFAULT (newid()) NOT NULL,
        [Date]               DATE               NOT NULL,
        [HasCafeteria]       BIT                NOT NULL,
        [Reason]             NVARCHAR (100)     COLLATE Latin1_General_CI_AI NULL,
        [CreatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [UpdatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [Deleted]            BIT                DEFAULT ((0)) NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UQ_CafeteriaDayOverride_Date ON [CafeteriaDayOverride] ([Date]) WHERE [Deleted] = 0;
END
