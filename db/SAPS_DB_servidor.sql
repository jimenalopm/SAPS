IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    IF SCHEMA_ID(N'soda') IS NULL EXEC(N'CREATE SCHEMA [soda];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE TABLE [soda].[tb_Categoria] (
        [idCategoria] int NOT NULL IDENTITY,
        [Nombre] varchar(50) NOT NULL,
        [EstaActivo] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_Categoria] PRIMARY KEY ([idCategoria])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE TABLE [soda].[tb_Tamano] (
        [idTamano] int NOT NULL IDENTITY,
        [Nombre] varchar(30) NOT NULL,
        [EstaActivo] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_Tamano] PRIMARY KEY ([idTamano])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(128) NOT NULL,
        [ProviderKey] nvarchar(128) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(128) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE TABLE [soda].[tb_Pedido] (
        [idPedido] int NOT NULL IDENTITY,
        [TokenRegistro] uniqueidentifier NOT NULL,
        [CodigoColaborador] varchar(50) NOT NULL,
        [NombreColaborador] varchar(150) NOT NULL,
        [idUsuario] nvarchar(450) NOT NULL,
        [FechaRegistroUtc] datetime2 NOT NULL,
        [TipoComida] varchar(20) NOT NULL,
        [Observaciones] varchar(500) NULL,
        [Total] bigint NOT NULL,
        [EsPrueba] bit NOT NULL,
        CONSTRAINT [PK_Pedido] PRIMARY KEY ([idPedido]),
        CONSTRAINT [CK_Pedido_TipoComida] CHECK ([TipoComida] IN ('Desayuno','Almuerzo','Merienda')),
        CONSTRAINT [CK_Pedido_Total] CHECK ([Total] > 0),
        CONSTRAINT [FK_Pedido_Usuario] FOREIGN KEY ([idUsuario]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE TABLE [soda].[tb_Producto] (
        [idProducto] int NOT NULL IDENTITY,
        [Nombre] varchar(100) NOT NULL,
        [idCategoria] int NOT NULL,
        [EsEspecial] bit NOT NULL DEFAULT CAST(0 AS bit),
        [TieneTamano] bit NOT NULL DEFAULT CAST(0 AS bit),
        [EstaActivo] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_Producto] PRIMARY KEY ([idProducto]),
        CONSTRAINT [FK_Producto_Categoria] FOREIGN KEY ([idCategoria]) REFERENCES [soda].[tb_Categoria] ([idCategoria]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE TABLE [soda].[tb_Bebida] (
        [idBebida] int NOT NULL IDENTITY,
        [Nombre] varchar(100) NOT NULL,
        [Tipo] varchar(20) NOT NULL,
        [idTamano] int NULL,
        [Precio] int NOT NULL,
        [EstaActivo] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_Bebida] PRIMARY KEY ([idBebida]),
        CONSTRAINT [CK_Bebida_Precio] CHECK ([Precio] > 0),
        CONSTRAINT [CK_Bebida_Tipo] CHECK ([Tipo] IN ('Gaseosa','Embotellada','Energizante','Jugo')),
        CONSTRAINT [FK_Bebida_Tamano] FOREIGN KEY ([idTamano]) REFERENCES [soda].[tb_Tamano] ([idTamano]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE TABLE [soda].[tb_Precio] (
        [idPrecio] int NOT NULL IDENTITY,
        [idProducto] int NOT NULL,
        [idTamano] int NULL,
        [Monto] int NOT NULL,
        [FechaVigenciaDesde] date NOT NULL,
        [FechaVigenciaHasta] date NULL,
        [EstaActivo] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_Precio] PRIMARY KEY ([idPrecio]),
        CONSTRAINT [CK_Precio_Monto] CHECK ([Monto] > 0),
        CONSTRAINT [CK_Precio_Vigencia] CHECK ([FechaVigenciaHasta] IS NULL OR [FechaVigenciaHasta] >= [FechaVigenciaDesde]),
        CONSTRAINT [FK_Precio_Producto] FOREIGN KEY ([idProducto]) REFERENCES [soda].[tb_Producto] ([idProducto]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Precio_Tamano] FOREIGN KEY ([idTamano]) REFERENCES [soda].[tb_Tamano] ([idTamano]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE TABLE [soda].[tb_DetallePedido] (
        [idDetallePedido] int NOT NULL IDENTITY,
        [idPedido] int NOT NULL,
        [idPrecio] int NULL,
        [idBebida] int NULL,
        [NombreArticulo] varchar(100) NOT NULL,
        [NombreTamano] varchar(30) NULL,
        [Cantidad] int NOT NULL,
        [PrecioUnitario] int NOT NULL,
        [Subtotal] bigint NOT NULL,
        CONSTRAINT [PK_DetallePedido] PRIMARY KEY ([idDetallePedido]),
        CONSTRAINT [CK_DetallePedido_Articulo] CHECK (([idPrecio] IS NOT NULL AND [idBebida] IS NULL) OR ([idPrecio] IS NULL AND [idBebida] IS NOT NULL)),
        CONSTRAINT [CK_DetallePedido_Cantidad] CHECK ([Cantidad] > 0),
        CONSTRAINT [CK_DetallePedido_PrecioUnitario] CHECK ([PrecioUnitario] > 0),
        CONSTRAINT [CK_DetallePedido_Subtotal] CHECK ([Subtotal] = CAST([Cantidad] AS bigint) * [PrecioUnitario]),
        CONSTRAINT [FK_DetallePedido_Bebida] FOREIGN KEY ([idBebida]) REFERENCES [soda].[tb_Bebida] ([idBebida]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DetallePedido_Pedido] FOREIGN KEY ([idPedido]) REFERENCES [soda].[tb_Pedido] ([idPedido]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DetallePedido_Precio] FOREIGN KEY ([idPrecio]) REFERENCES [soda].[tb_Precio] ([idPrecio]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE INDEX [IX_Bebida_idTamano] ON [soda].[tb_Bebida] ([idTamano]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Categoria_Nombre] ON [soda].[tb_Categoria] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE INDEX [IX_DetallePedido_idBebida] ON [soda].[tb_DetallePedido] ([idBebida]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE INDEX [IX_DetallePedido_idPedido] ON [soda].[tb_DetallePedido] ([idPedido]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE INDEX [IX_DetallePedido_idPrecio] ON [soda].[tb_DetallePedido] ([idPrecio]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE INDEX [IX_Pedido_CodigoColaborador_FechaRegistroUtc] ON [soda].[tb_Pedido] ([CodigoColaborador], [FechaRegistroUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE INDEX [IX_Pedido_idUsuario] ON [soda].[tb_Pedido] ([idUsuario]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Pedido_TokenRegistro] ON [soda].[tb_Pedido] ([TokenRegistro]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Precio_idProducto_idTamano] ON [soda].[tb_Precio] ([idProducto], [idTamano]) WHERE [EstaActivo] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE INDEX [IX_Precio_idTamano] ON [soda].[tb_Precio] ([idTamano]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE INDEX [IX_Producto_idCategoria] ON [soda].[tb_Producto] ([idCategoria]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Tamano_Nombre] ON [soda].[tb_Tamano] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001162307_InicialEstandares'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001162307_InicialEstandares', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006032751_ColaboradoresRrhh'
)
BEGIN
    IF SCHEMA_ID(N'rrhh') IS NULL EXEC(N'CREATE SCHEMA [rrhh];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006032751_ColaboradoresRrhh'
)
BEGIN
    CREATE TABLE [rrhh].[tb_Colaborador] (
        [idColaborador] int NOT NULL IDENTITY,
        [Codigo] varchar(10) NOT NULL,
        [NombreCompleto] varchar(100) NOT NULL,
        [EstaActivo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [RutaFoto] varchar(260) NULL,
        [FechaRegistro] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Colaborador] PRIMARY KEY ([idColaborador])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006032751_ColaboradoresRrhh'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Colaborador_Codigo] ON [rrhh].[tb_Colaborador] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006032751_ColaboradoresRrhh'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006032751_ColaboradoresRrhh', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007181456_AgregarOrdenTamano'
)
BEGIN
    ALTER TABLE [soda].[tb_Tamano] ADD [Orden] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007181456_AgregarOrdenTamano'
)
BEGIN

    EXEC(N'
    UPDATE soda.tb_Tamano
    SET Orden = CASE
            WHEN Nombre LIKE ''Peque%'' THEN 1
            WHEN Nombre = ''Mediano''   THEN 2
            WHEN Nombre = ''Grande''    THEN 3
            ELSE 3 + idTamano
        END
    WHERE Orden = 0;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007181456_AgregarOrdenTamano'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007181456_AgregarOrdenTamano', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007183019_TamanosPorCategoria'
)
BEGIN
    ALTER TABLE [soda].[tb_Tamano] ADD [EsParaBebida] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007183019_TamanosPorCategoria'
)
BEGIN
    CREATE TABLE [soda].[tb_CategoriaTamano] (
        [idCategoria] int NOT NULL,
        [idTamano] int NOT NULL,
        CONSTRAINT [PK_CategoriaTamano] PRIMARY KEY ([idCategoria], [idTamano]),
        CONSTRAINT [FK_CategoriaTamano_Categoria] FOREIGN KEY ([idCategoria]) REFERENCES [soda].[tb_Categoria] ([idCategoria]) ON DELETE CASCADE,
        CONSTRAINT [FK_CategoriaTamano_Tamano] FOREIGN KEY ([idTamano]) REFERENCES [soda].[tb_Tamano] ([idTamano]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007183019_TamanosPorCategoria'
)
BEGIN
    CREATE INDEX [IX_CategoriaTamano_idTamano] ON [soda].[tb_CategoriaTamano] ([idTamano]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007183019_TamanosPorCategoria'
)
BEGIN

    EXEC(N'
    UPDATE t
    SET t.EsParaBebida = 1
    FROM soda.tb_Tamano t
    WHERE t.Nombre NOT LIKE ''Peque%''
      AND t.Nombre NOT IN (''Mediano'', ''Grande'')
      AND NOT EXISTS (SELECT 1 FROM soda.tb_Precio p WHERE p.idTamano = t.idTamano);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007183019_TamanosPorCategoria'
)
BEGIN

    INSERT INTO soda.tb_CategoriaTamano (idCategoria, idTamano)
    SELECT DISTINCT pr.idCategoria, p.idTamano
    FROM soda.tb_Precio p
    JOIN soda.tb_Producto pr ON pr.idProducto = p.idProducto
    WHERE p.idTamano IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007183019_TamanosPorCategoria'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007183019_TamanosPorCategoria', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007184230_QuitarOrdenTamano'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[soda].[tb_Tamano]') AND [c].[name] = N'Orden');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [soda].[tb_Tamano] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [soda].[tb_Tamano] DROP COLUMN [Orden];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007184230_QuitarOrdenTamano'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007184230_QuitarOrdenTamano', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009120000_TiposDeBebida'
)
BEGIN
    ALTER TABLE [soda].[tb_Bebida] DROP CONSTRAINT [CK_Bebida_Tipo];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009120000_TiposDeBebida'
)
BEGIN
    CREATE TABLE [soda].[tb_TipoBebida] (
        [idTipoBebida] int NOT NULL IDENTITY,
        [Nombre] varchar(20) NOT NULL,
        [EstaActivo] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_TipoBebida] PRIMARY KEY ([idTipoBebida])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009120000_TiposDeBebida'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'idTipoBebida', N'Nombre') AND [object_id] = OBJECT_ID(N'[soda].[tb_TipoBebida]'))
        SET IDENTITY_INSERT [soda].[tb_TipoBebida] ON;
    EXEC(N'INSERT INTO [soda].[tb_TipoBebida] ([idTipoBebida], [Nombre])
    VALUES (1, ''Gaseosa''),
    (2, ''Embotellada''),
    (3, ''Energizante''),
    (4, ''Jugo'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'idTipoBebida', N'Nombre') AND [object_id] = OBJECT_ID(N'[soda].[tb_TipoBebida]'))
        SET IDENTITY_INSERT [soda].[tb_TipoBebida] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009120000_TiposDeBebida'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UQ_TipoBebida_Nombre] ON [soda].[tb_TipoBebida] ([Nombre]) WHERE [Nombre] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009120000_TiposDeBebida'
)
BEGIN

    INSERT INTO soda.tb_TipoBebida (Nombre, EstaActivo)
    SELECT DISTINCT b.Tipo, 1
    FROM soda.tb_Bebida b
    WHERE NOT EXISTS (SELECT 1 FROM soda.tb_TipoBebida t WHERE t.Nombre = b.Tipo);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009120000_TiposDeBebida'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009120000_TiposDeBebida', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009230000_TipoComidaDesdeCatalogo'
)
BEGIN
    ALTER TABLE [soda].[tb_Pedido] DROP CONSTRAINT [CK_Pedido_TipoComida];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009230000_TipoComidaDesdeCatalogo'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[soda].[tb_Pedido]') AND [c].[name] = N'TipoComida');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [soda].[tb_Pedido] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [soda].[tb_Pedido] ALTER COLUMN [TipoComida] varchar(50) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009230000_TipoComidaDesdeCatalogo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009230000_TipoComidaDesdeCatalogo', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013041602_EstadoContrasena'
)
BEGIN
    CREATE TABLE [tb_EstadoContrasena] (
        [idUsuario] nvarchar(450) NOT NULL,
        [FechaUltimoCambio] datetime2 NOT NULL,
        CONSTRAINT [PK_EstadoContrasena] PRIMARY KEY ([idUsuario]),
        CONSTRAINT [FK_EstadoContrasena_Usuario] FOREIGN KEY ([idUsuario]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013041602_EstadoContrasena'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013041602_EstadoContrasena', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013061613_GestionUsuarios'
)
BEGIN
    ALTER TABLE [tb_EstadoContrasena] ADD [RequiereCambioInicial] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013061613_GestionUsuarios'
)
BEGIN
    CREATE TABLE [tb_UsuarioColaborador] (
        [idUsuario] nvarchar(450) NOT NULL,
        [idColaborador] int NOT NULL,
        [FechaVinculacion] datetime2 NOT NULL,
        CONSTRAINT [PK_UsuarioColaborador] PRIMARY KEY ([idUsuario]),
        CONSTRAINT [FK_UsuarioColaborador_Colaborador] FOREIGN KEY ([idColaborador]) REFERENCES [rrhh].[tb_Colaborador] ([idColaborador]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UsuarioColaborador_Usuario] FOREIGN KEY ([idUsuario]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013061613_GestionUsuarios'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_UsuarioColaborador_idColaborador] ON [tb_UsuarioColaborador] ([idColaborador]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013061613_GestionUsuarios'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013061613_GestionUsuarios', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014120000_TiposBebidaYFrescos'
)
BEGIN

    -- 1) Tipos de bebida: "Frescos" (era Jugo) y "Tés y refrescos" (era Embotellada, ahora también con los Jugo).
    IF NOT EXISTS (SELECT 1 FROM soda.tb_TipoBebida WHERE Nombre = N'Frescos')
    BEGIN
        IF EXISTS (SELECT 1 FROM soda.tb_TipoBebida WHERE Nombre = N'Jugo')
            UPDATE soda.tb_TipoBebida SET Nombre = N'Frescos', EstaActivo = 1 WHERE Nombre = N'Jugo';
        ELSE
            INSERT INTO soda.tb_TipoBebida (Nombre, EstaActivo) VALUES (N'Frescos', 1);
    END;

    IF NOT EXISTS (SELECT 1 FROM soda.tb_TipoBebida WHERE Nombre = N'Tés y refrescos')
    BEGIN
        IF EXISTS (SELECT 1 FROM soda.tb_TipoBebida WHERE Nombre = N'Embotellada')
            UPDATE soda.tb_TipoBebida SET Nombre = N'Tés y refrescos', EstaActivo = 1 WHERE Nombre = N'Embotellada';
        ELSE
            INSERT INTO soda.tb_TipoBebida (Nombre, EstaActivo) VALUES (N'Tés y refrescos', 1);
    END;

    UPDATE soda.tb_Bebida SET Tipo = N'Tés y refrescos' WHERE Tipo IN (N'Embotellada', N'Jugo');
    DELETE FROM soda.tb_TipoBebida
    WHERE Nombre IN (N'Embotellada', N'Jugo')
      AND NOT EXISTS (SELECT 1 FROM soda.tb_Bebida b WHERE b.Tipo = soda.tb_TipoBebida.Nombre);

    -- 2) Tamaños de bebida para el fresco.
    INSERT INTO soda.tb_Tamano (Nombre, EsParaBebida, EstaActivo)
    SELECT v.Nombre, 1, 1
    FROM (VALUES (N'Vaso pequeño'), (N'Vaso mediano'), (N'Vaso grande')) AS v(Nombre)
    WHERE NOT EXISTS (SELECT 1 FROM soda.tb_Tamano t WHERE t.Nombre = v.Nombre);

    -- 3) El fresco que estaba como producto de la categoría "Fresco" pasa a ser la bebida "Frescos naturales".
    DECLARE @idCategoriaFresco int = (SELECT idCategoria FROM soda.tb_Categoria WHERE Nombre = N'Fresco');
    IF @idCategoriaFresco IS NOT NULL
    BEGIN
        INSERT INTO soda.tb_Bebida (Nombre, Tipo, idTamano, Precio, EstaActivo)
        SELECT N'Frescos naturales', N'Frescos', tb.idTamano, MAX(pr.Monto), 1
        FROM soda.tb_Producto p
        JOIN soda.tb_Precio pr ON pr.idProducto = p.idProducto AND pr.EstaActivo = 1 AND pr.idTamano IS NOT NULL
        JOIN soda.tb_Tamano ta ON ta.idTamano = pr.idTamano
        JOIN soda.tb_Tamano tb ON tb.Nombre = N'Vaso ' + LOWER(ta.Nombre)
        WHERE p.idCategoria = @idCategoriaFresco
          AND p.Nombre IN (N'Fresco', N'Fresco natural')
          AND NOT EXISTS (SELECT 1 FROM soda.tb_Bebida x WHERE x.Nombre = N'Frescos naturales' AND x.idTamano = tb.idTamano)
        GROUP BY tb.idTamano;

        -- 4) Se elimina el fresco de comida si ningún pedido lo usó; si alguno lo usó, solo se desactiva.
        DECLARE @frescos TABLE (idProducto int NOT NULL PRIMARY KEY);
        INSERT INTO @frescos (idProducto)
        SELECT idProducto FROM soda.tb_Producto
        WHERE idCategoria = @idCategoriaFresco AND Nombre IN (N'Fresco', N'Fresco natural');

        DELETE FROM soda.tb_Precio
        WHERE idProducto IN (SELECT idProducto FROM @frescos)
          AND NOT EXISTS (SELECT 1 FROM soda.tb_DetallePedido d WHERE d.idPrecio = soda.tb_Precio.idPrecio);

        UPDATE soda.tb_Precio SET EstaActivo = 0 WHERE idProducto IN (SELECT idProducto FROM @frescos);

        DELETE FROM soda.tb_Producto
        WHERE idProducto IN (SELECT idProducto FROM @frescos)
          AND NOT EXISTS (SELECT 1 FROM soda.tb_Precio x WHERE x.idProducto = soda.tb_Producto.idProducto);

        UPDATE soda.tb_Producto SET EstaActivo = 0 WHERE idProducto IN (SELECT idProducto FROM @frescos);

        -- 5) La categoría "Fresco" se elimina si quedó vacía, o se desactiva si solo le quedan productos inactivos.
        IF NOT EXISTS (SELECT 1 FROM soda.tb_Producto WHERE idCategoria = @idCategoriaFresco)
        BEGIN
            DELETE FROM soda.tb_CategoriaTamano WHERE idCategoria = @idCategoriaFresco;
            DELETE FROM soda.tb_Categoria WHERE idCategoria = @idCategoriaFresco;
        END
        ELSE IF NOT EXISTS (SELECT 1 FROM soda.tb_Producto WHERE idCategoria = @idCategoriaFresco AND EstaActivo = 1)
            UPDATE soda.tb_Categoria SET EstaActivo = 0 WHERE idCategoria = @idCategoriaFresco;
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014120000_TiposBebidaYFrescos'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014120000_TiposBebidaYFrescos', N'10.0.11');
END;

COMMIT;
GO

