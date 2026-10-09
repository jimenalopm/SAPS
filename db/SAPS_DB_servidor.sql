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
    WHERE [MigrationId] = N'20261009032146_EstadoContrasena'
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
    WHERE [MigrationId] = N'20261009032146_EstadoContrasena'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009032146_EstadoContrasena', N'10.0.11');
END;

COMMIT;
GO

