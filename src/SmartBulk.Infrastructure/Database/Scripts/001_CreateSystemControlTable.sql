IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SystemControl')
BEGIN
    CREATE TABLE [SystemControl] (
        [Id]                 INT                IDENTITY (1, 1) NOT NULL,
        [Uuid]               UNIQUEIDENTIFIER   DEFAULT (newid()) NOT NULL,
        [SystemKey]          VARCHAR (50)       NOT NULL,
        [SystemValue]        VARCHAR (200)      NOT NULL,
        [CreatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [UpdatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [Deleted]            BIT                DEFAULT ((0)) NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    INSERT INTO [SystemControl] ([SystemKey], [SystemValue])
    VALUES ('SystemStatus', 'Initialized');
END

