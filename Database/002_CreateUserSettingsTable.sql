IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserSettings')
BEGIN
    CREATE TABLE [UserSettings] (
        [Id]                  INT                IDENTITY (1, 1) NOT NULL,
        [Uuid]                UNIQUEIDENTIFIER   DEFAULT (newid()) NOT NULL,
        [CalorieTargetKcal]   INT                NOT NULL DEFAULT (3000),
        [ProteinTargetGPerKg] DECIMAL (4, 2)     NOT NULL DEFAULT (2.0),
        [TimeZoneId]          VARCHAR (100)      NOT NULL DEFAULT ('America/Sao_Paulo'),
        [LunchReminderTime]   TIME (0)           NOT NULL DEFAULT ('12:30:00'),
        [DinnerReminderTime]  TIME (0)           NOT NULL DEFAULT ('19:30:00'),
        [CreatedAt]           DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [UpdatedAt]           DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [Deleted]             BIT                DEFAULT ((0)) NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    INSERT INTO [UserSettings] ([CalorieTargetKcal], [ProteinTargetGPerKg], [TimeZoneId], [LunchReminderTime], [DinnerReminderTime])
    VALUES (3000, 2.0, 'America/Sao_Paulo', '12:30:00', '19:30:00');
END
