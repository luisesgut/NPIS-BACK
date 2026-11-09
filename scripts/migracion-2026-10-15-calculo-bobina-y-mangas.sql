BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015225428_AddCalculoBobina'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_Repeticion] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015225428_AddCalculoBobina'
)
BEGIN
    CREATE TABLE [CoresBobina] (
        [Id] uniqueidentifier NOT NULL,
        [Nombre] nvarchar(150) NOT NULL,
        [DiametroExteriorM] decimal(10,5) NOT NULL,
        [KgPorCm] decimal(10,5) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_CoresBobina] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015225428_AddCalculoBobina'
)
BEGIN
    CREATE TABLE [MaterialesBobina] (
        [Id] uniqueidentifier NOT NULL,
        [Codigo] nvarchar(60) NOT NULL,
        [DensidadKgM3] decimal(12,4) NOT NULL,
        [TipoPelicula] nvarchar(150) NULL,
        [Terminado] nvarchar(150) NULL,
        [FormulaLdpe] nvarchar(150) NULL,
        [EsEstimado] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [UpdatedByName] nvarchar(200) NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_MaterialesBobina] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015225428_AddCalculoBobina'
)
BEGIN
    CREATE TABLE [ParametrosBobina] (
        [Id] int NOT NULL,
        [AdhesivoGm2] decimal(10,4) NOT NULL,
        [TintaGm2] decimal(10,4) NOT NULL,
        [TarimaAnchoCm] decimal(10,2) NOT NULL,
        [TarimaLargoCm] decimal(10,2) NOT NULL,
        [TarimaAltoMaxCm] decimal(10,2) NOT NULL,
        [ToleranciaAlto] decimal(6,4) NOT NULL,
        CONSTRAINT [PK_ParametrosBobina] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015225428_AddCalculoBobina'
)
BEGIN
    CREATE TABLE [NpiMaterialesExactos] (
        [NpiId] uniqueidentifier NOT NULL,
        [Capa] nvarchar(10) NOT NULL,
        [MaterialBobinaId] uniqueidentifier NOT NULL,
        [SetByUserId] uniqueidentifier NOT NULL,
        [SetByName] nvarchar(200) NOT NULL,
        [SetAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_NpiMaterialesExactos] PRIMARY KEY ([NpiId], [Capa]),
        CONSTRAINT [FK_NpiMaterialesExactos_MaterialesBobina_MaterialBobinaId] FOREIGN KEY ([MaterialBobinaId]) REFERENCES [MaterialesBobina] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_NpiMaterialesExactos_Npis_NpiId] FOREIGN KEY ([NpiId]) REFERENCES [Npis] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015225428_AddCalculoBobina'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CoresBobina_Nombre] ON [CoresBobina] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015225428_AddCalculoBobina'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MaterialesBobina_Codigo] ON [MaterialesBobina] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015225428_AddCalculoBobina'
)
BEGIN
    CREATE INDEX [IX_NpiMaterialesExactos_MaterialBobinaId] ON [NpiMaterialesExactos] ([MaterialBobinaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015225428_AddCalculoBobina'
)
BEGIN

    INSERT INTO MaterialesBobina (Id, Codigo, DensidadKgM3, TipoPelicula, Terminado, FormulaLdpe, EsEstimado, IsActive) VALUES
    (NEWID(), N'PET', 1400, N'PET', NULL, NULL, 0, 1),
    (NEWID(), N'PET MET', 1400, N'PET', N'METALIZADO', NULL, 0, 1),
    (NEWID(), N'PET ALOX', 1400, N'PET', N'ALOX', NULL, 0, 1),
    (NEWID(), N'PET MATE', 1400, N'PET', N'MATE', NULL, 0, 1),
    (NEWID(), N'FOIL', 2700, N'FOIL', NULL, NULL, 0, 1),
    (NEWID(), N'BOPA', 2700, N'BOPA', NULL, NULL, 0, 0),
    (NEWID(), N'BOPP', 910, N'BOPP', NULL, NULL, 0, 1),
    (NEWID(), N'BOPP ANTIFOG', 910, N'BOPP', N'ANTIFOG', NULL, 0, 1),
    (NEWID(), N'BOPP CAVITADO', 680, N'BOPP', N'CAVITADO', NULL, 0, 1),
    (NEWID(), N'BOPP MATE', 910, N'BOPP', N'MATE', NULL, 0, 1),
    (NEWID(), N'BOPP MET', 910, N'BOPP', N'METALIZADO', NULL, 0, 1),
    (NEWID(), N'BOPP OXO', 910, N'BOPP', N'OXODEGRADABLE', NULL, 0, 1),
    (NEWID(), N'CPP', 900, N'CPP', NULL, NULL, 0, 1),
    (NEWID(), N'CPP OXO', 910, N'CPP', N'OXODEGRADABLE', NULL, 0, 1),
    (NEWID(), N'C-02', 990, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'C-03', 900, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'C-04', 920, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-01', 920, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-02', 961.367, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-03', 918.1325, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-04', 932.7974999999999, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-05', 919.9359999999999, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-06', 1082.319, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-07', 930, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-08', 920.176, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-09', 930, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-10', 920, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-11', 930.329, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-12', 919.065, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-13', 927.2504, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-15', 1068.372, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-14', 920, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-16', 1036.53, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-17', 919.651, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-18', 990.51, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-20', 978.564, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-21', 919.314, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-22', 931.562, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-23', 1049.865, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-24', 927, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-25', 919.32, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-26', 930, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-27', 971.785, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'M-01', 921.025, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'M-02', 938.8199999999999, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'M-03', 921.5550000000001, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'M-04', 972.09, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'M-05', 921.31, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'M-06', 918.9000000000001, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'M-08', 971.82, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'M-09', 966.855, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'M-11', 1237.7800000000002, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'M-12', 1242.4859999999999, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'M-13', 1189.5, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'ME-01', 920.2, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'ME-02', 922.356, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'ME-03', 926.886, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'ME-04', 927.466, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'ME-07', 1265, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'ME-08', 923.345, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'ME-09', 919.6, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'ME-10', 0, NULL, NULL, NULL, 0, 0),
    (NEWID(), N'ME-14', 985.12, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'ME-15', 925.925, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'ME-16', 920.75, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'ME-17', 926.3000000000001, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'ME-18', 971.785, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'ME-19', 992.2800000000001, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'ME-20', 926.175, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'ME-21', 923.67, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'ME-22', 0, NULL, NULL, NULL, 0, 0),
    (NEWID(), N'SIGLO XX', 920, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-32', 920, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'MCE-18', 930, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'ME-24', 920, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'MCE-14', 930, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'MCE-12', 1610, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'PCR', 930, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'MCE-19', 920, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'MDO PE', 940, N'MDO', NULL, NULL, 0, 1),
    (NEWID(), N'CE-31', 930, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-36', 950, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CE-35', 920, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'MCE-24', 920, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'Shannon w/eva', 920, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'MCE-27', 950, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'MCE-25', 920, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'MCE-26', 950, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'MCE-21', 990, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'MCE-08', 920, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'MCE-28', 950, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'MCE-29', 950, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'MCE-11', 930, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'CPP MET', 900, N'CPP', N'METALIZADO', NULL, 0, 1),
    (NEWID(), N'MCE-17', 920, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'MCE-30', 930, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'MCE-32', 930, NULL, NULL, NULL, 0, 1),
    (NEWID(), N'MCE-31', 900, NULL, NULL, NULL, 0, 1);

    INSERT INTO MaterialesBobina (Id, Codigo, DensidadKgM3, TipoPelicula, Terminado, FormulaLdpe, EsEstimado, IsActive)
    VALUES (NEWID(), N'PE ESTIMADO', 920, N'PE', NULL, NULL, 1, 1);

    -- Un estimado por cada fórmula LDPE que exista en el catálogo, para poder ajustarlos por separado.
    INSERT INTO MaterialesBobina (Id, Codigo, DensidadKgM3, TipoPelicula, Terminado, FormulaLdpe, EsEstimado, IsActive)
    SELECT NEWID(), LEFT(N'PE ESTIMADO ' + o.Value, 60), 920, N'PE', NULL, o.Value, 1, 1
    FROM CatalogOptions o JOIN Catalogs c ON c.Id = o.CatalogId
    WHERE c.[Key] = N'FORMULA LDPE' AND o.Value NOT IN (N'N/A', N'NA')
      AND NOT EXISTS (SELECT 1 FROM MaterialesBobina m WHERE m.Codigo = LEFT(N'PE ESTIMADO ' + o.Value, 60));

    INSERT INTO CoresBobina (Id, Nombre, DiametroExteriorM, KgPorCm, IsActive) VALUES
    (NEWID(), N'3"', 0.095, 0.0188, 1),
    (NEWID(), N'6"', 0.1712, 0.02, 1);

    INSERT INTO ParametrosBobina (Id, AdhesivoGm2, TintaGm2, TarimaAnchoCm, TarimaLargoCm, TarimaAltoMaxCm, ToleranciaAlto)
    VALUES (1, 1.7, 2.0, 100, 120, 150, 0);

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015225428_AddCalculoBobina'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015225428_AddCalculoBobina', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015231618_AddDesarrolloManga'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_DesarrolloManga] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015231618_AddDesarrolloManga'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM Catalogs WHERE [Key] = N'DESARROLLO MANGA')
    BEGIN
        DECLARE @Id UNIQUEIDENTIFIER = NEWID();
        INSERT INTO Catalogs (Id, [Key], DisplayName) VALUES (@Id, N'DESARROLLO MANGA', N'Desarrollo de manga (mm)');
        INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
        (NEWID(), @Id, N'370', 0, 1),
        (NEWID(), @Id, N'390', 1, 1),
        (NEWID(), @Id, N'400', 2, 1),
        (NEWID(), @Id, N'450', 3, 1),
        (NEWID(), @Id, N'460', 4, 1),
        (NEWID(), @Id, N'470', 5, 1),
        (NEWID(), @Id, N'480', 6, 1),
        (NEWID(), @Id, N'490', 7, 1),
        (NEWID(), @Id, N'500', 8, 1),
        (NEWID(), @Id, N'520', 9, 1),
        (NEWID(), @Id, N'540', 10, 1),
        (NEWID(), @Id, N'550', 11, 1),
        (NEWID(), @Id, N'560', 12, 1),
        (NEWID(), @Id, N'570', 13, 1),
        (NEWID(), @Id, N'580', 14, 1),
        (NEWID(), @Id, N'590', 15, 1),
        (NEWID(), @Id, N'600', 16, 1),
        (NEWID(), @Id, N'610', 17, 1),
        (NEWID(), @Id, N'620', 18, 1),
        (NEWID(), @Id, N'630', 19, 1),
        (NEWID(), @Id, N'650', 20, 1),
        (NEWID(), @Id, N'660', 21, 1),
        (NEWID(), @Id, N'670', 22, 1),
        (NEWID(), @Id, N'680', 23, 1),
        (NEWID(), @Id, N'700', 24, 1),
        (NEWID(), @Id, N'720', 25, 1),
        (NEWID(), @Id, N'732', 26, 1),
        (NEWID(), @Id, N'740', 27, 1),
        (NEWID(), @Id, N'750', 28, 1),
        (NEWID(), @Id, N'800', 29, 1),
        (NEWID(), @Id, N'N/A', 30, 1);
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015231618_AddDesarrolloManga'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015231618_AddDesarrolloManga', N'10.0.11');
END;

COMMIT;
GO

