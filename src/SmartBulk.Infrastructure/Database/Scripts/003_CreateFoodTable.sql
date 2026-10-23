IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Food')
BEGIN
    CREATE TABLE [Food] (
        [Id]                 INT                IDENTITY (1, 1) NOT NULL,
        [Uuid]               UNIQUEIDENTIFIER   DEFAULT (newid()) NOT NULL,
        [Name]               NVARCHAR (200)     COLLATE Latin1_General_CI_AI NOT NULL,
        [Calories]           DECIMAL (8, 2)     NOT NULL,
        [Protein]            DECIMAL (8, 2)     NOT NULL,
        [Carbohydrates]      DECIMAL (8, 2)     NOT NULL,
        [Fat]                DECIMAL (8, 2)     NOT NULL,
        [IsCustom]           BIT                NOT NULL DEFAULT 0,
        [RawYieldFactor]     DECIMAL (5, 2)     NULL,
        [CreatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [UpdatedAt]          DATETIMEOFFSET (7) DEFAULT (getutcdate()) NULL,
        [Deleted]            BIT                DEFAULT ((0)) NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE NONCLUSTERED INDEX IX_Food_Name ON [Food] ([Name]) INCLUDE ([Calories], [Protein], [Carbohydrates], [Fat], [Deleted]);

    -- Seed TACO foods with raw yield factors for common cooking ingredients
    INSERT INTO [Food] ([Name], [Calories], [Protein], [Carbohydrates], [Fat], [IsCustom], [RawYieldFactor])
    VALUES
        (N'Arroz branco cozido', 128.00, 2.50, 28.10, 0.20, 0, 2.50),
        (N'Arroz integral cozido', 124.00, 2.60, 25.80, 1.00, 0, 2.50),
        (N'Feijão carioca cozido', 76.00, 4.80, 13.60, 0.50, 0, 2.40),
        (N'Feijão preto cozido', 77.00, 4.50, 14.00, 0.50, 0, 2.40),
        (N'Peito de frango grelhado', 159.00, 32.00, 0.00, 2.50, 0, 0.75),
        (N'Peito de frango cozido', 163.00, 31.50, 0.00, 3.20, 0, 0.80),
        (N'Peito de frango cru', 119.00, 21.50, 0.00, 3.00, 0, NULL),
        (N'Carne bovina patinho grelhado', 219.00, 35.90, 0.00, 7.30, 0, 0.70),
        (N'Carne bovina moída cozida', 212.00, 26.70, 0.00, 10.90, 0, 0.75),
        (N'Carne bovina alcatra grelhada', 241.00, 31.90, 0.00, 11.60, 0, 0.70),
        (N'Ovo de galinha cozido', 146.00, 13.30, 0.60, 9.50, 0, 1.00),
        (N'Clara de ovo cozida', 54.00, 13.00, 0.00, 0.10, 0, 1.00),
        (N'Batata inglesa cozida', 52.00, 1.20, 11.90, 0.00, 0, 1.00),
        (N'Batata doce cozida', 77.00, 0.60, 18.40, 0.10, 0, 1.00),
        (N'Macarrão espaguete cozido', 102.00, 3.30, 21.20, 0.20, 0, 2.20),
        (N'Macarrão cru', 371.00, 10.00, 77.90, 1.30, 0, NULL),
        (N'Aveia em flocos', 394.00, 13.90, 66.60, 8.50, 0, 1.00),
        (N'Banana nanica crua', 92.00, 1.40, 23.80, 0.10, 0, 1.00),
        (N'Banana prata crua', 98.00, 1.30, 26.00, 0.10, 0, 1.00),
        (N'Maçã fuji com casca', 56.00, 0.30, 15.20, 0.00, 0, 1.00),
        (N'Leite de vaca desnatado', 35.00, 3.50, 4.90, 0.10, 0, 1.00),
        (N'Leite de vaca integral', 60.00, 3.10, 4.80, 3.20, 0, 1.00),
        (N'Iogurte natural desnatado', 41.00, 3.80, 5.80, 0.30, 0, 1.00),
        (N'Queijo muçarela', 330.00, 22.60, 3.00, 25.20, 0, 1.00),
        (N'Queijo cottage', 98.00, 11.10, 3.40, 4.30, 0, 1.00),
        (N'Queijo minas frescal', 264.00, 17.40, 3.20, 20.20, 0, 1.00),
        (N'Pão francês', 300.00, 8.00, 58.60, 3.10, 0, 1.00),
        (N'Pão de forma integral', 253.00, 9.40, 49.90, 3.70, 0, 1.00),
        (N'Azeite de oliva', 884.00, 0.00, 0.00, 100.00, 0, 1.00),
        (N'Pasta de amendoim integral', 588.00, 25.00, 20.00, 50.00, 0, 1.00),
        (N'Atum em conserva em óleo', 166.00, 26.20, 0.00, 6.00, 0, 1.00),
        (N'Whey protein concentrado 80%', 400.00, 80.00, 8.00, 6.00, 0, 1.00),
        (N'Creatina monohidratada', 0.00, 0.00, 0.00, 0.00, 0, 1.00),
        (N'Tilápia filé grelhado', 128.00, 26.10, 0.00, 2.70, 0, 0.80),
        (N'Castanha de caju torrada', 570.00, 18.50, 29.10, 46.30, 0, 1.00),
        (N'Castanha do Pará', 643.00, 14.50, 15.10, 63.50, 0, 1.00),
        (N'Cenoura cozida', 30.00, 0.80, 6.70, 0.20, 0, 1.00),
        (N'Brócolis cozido', 25.00, 2.10, 4.40, 0.50, 0, 1.00),
        (N'Tomate cru', 15.00, 1.10, 3.10, 0.20, 0, 1.00),
        (N'Alface crespa crua', 11.00, 1.30, 1.70, 0.20, 0, 1.00);
END
