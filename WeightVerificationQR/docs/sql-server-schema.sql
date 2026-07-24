-- Weight Verification & QR Label System — SQL Server schema
-- Use this if you're deploying against a shared SQL Server instance instead of the
-- default per-install SQLite file. Run once against a new, empty database.
--
-- After running this, in App.xaml.cs:
--   1. Change UseSqlite(...) to UseSqlServer(dbSettings.ConnectionString)
--   2. Update the connection string in appsettings.json
--   3. In DbInitializer.cs, you can drop the EnsureCreatedAsync() call (this script
--      already created the schema) and keep only the admin-password-seeding logic below.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Products')
BEGIN
    CREATE TABLE Products (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        ProductName   NVARCHAR(200)   NOT NULL,
        Quantity      NVARCHAR(50)    NOT NULL DEFAULT '',
        MinWeightKg   DECIMAL(10,3)   NOT NULL,
        MaxWeightKg   DECIMAL(10,3)   NOT NULL,
        CodePrefix    NVARCHAR(10)    NOT NULL DEFAULT 'KIT',
        IsActive      BIT             NOT NULL DEFAULT 1,
        CreatedAt     DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
        UpdatedAt     DATETIME2       NULL
    );
    CREATE INDEX IX_Products_ProductName ON Products(ProductName);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        FullName      NVARCHAR(100)   NOT NULL,
        Username      NVARCHAR(50)    NOT NULL,
        PasswordHash  NVARCHAR(MAX)   NOT NULL,
        PasswordSalt  NVARCHAR(MAX)   NOT NULL,
        Role          INT             NOT NULL,  -- 1=Admin, 2=Supervisor, 3=Operator
        IsActive      BIT             NOT NULL DEFAULT 1,
        CreatedAt     DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
        LastLoginAt   DATETIME2       NULL
    );
    CREATE UNIQUE INDEX IX_Users_Username ON Users(Username);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WeighRecords')
BEGIN
    CREATE TABLE WeighRecords (
        Id                  INT IDENTITY(1,1) PRIMARY KEY,
        KitNumber           NVARCHAR(40)    NOT NULL,
        ProductId           INT             NOT NULL,
        ProductName         NVARCHAR(200)   NOT NULL,
        Quantity            NVARCHAR(50)    NOT NULL DEFAULT '',
        WeightKg            DECIMAL(10,3)   NOT NULL,
        Result              INT             NOT NULL,  -- 0=Pending, 1=Pass, 2=Fail
        FailReason          INT             NOT NULL DEFAULT 0,
        RecordDate          DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
        OperatorName        NVARCHAR(100)   NOT NULL DEFAULT '',
        QrId                NVARCHAR(40)    NOT NULL DEFAULT '',
        QrGenerated         BIT             NOT NULL DEFAULT 0,
        PrintedSuccessfully BIT             NOT NULL DEFAULT 0,
        PrinterStatus       NVARCHAR(50)    NOT NULL DEFAULT 'N/A',
        ReprintCount        INT             NOT NULL DEFAULT 0,
        Remarks             NVARCHAR(500)   NOT NULL DEFAULT ''
    );
    CREATE UNIQUE INDEX IX_WeighRecords_KitNumber ON WeighRecords(KitNumber);
    CREATE INDEX IX_WeighRecords_RecordDate ON WeighRecords(RecordDate);
    CREATE INDEX IX_WeighRecords_QrId ON WeighRecords(QrId);
END
GO

-- Seed the two products from the spec (idempotent - skips if already present)
IF NOT EXISTS (SELECT 1 FROM Products WHERE ProductName = 'I Kit 12 mm & 6 mm EPE')
BEGIN
    INSERT INTO Products (ProductName, Quantity, MinWeightKg, MaxWeightKg, CodePrefix, IsActive)
    VALUES ('I Kit 12 mm & 6 mm EPE', '100 Nos', 1.000, 1.051, 'KIT', 1);
END

IF NOT EXISTS (SELECT 1 FROM Products WHERE ProductName = '12.7 mm & 6.35 mm EPE Gray')
BEGIN
    INSERT INTO Products (ProductName, Quantity, MinWeightKg, MaxWeightKg, CodePrefix, IsActive)
    VALUES ('12.7 mm & 6.35 mm EPE Gray', '100 Nos', 1.050, 1.080, 'KIT', 1);
END
GO

-- Seed a placeholder admin row. The app replaces PasswordHash/PasswordSalt with a real
-- PBKDF2 hash for "Admin@123" on first run (see DbInitializer.cs) - do not log in with
-- this placeholder value, it will not match any password.
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin')
BEGIN
    INSERT INTO Users (FullName, Username, PasswordHash, PasswordSalt, Role, IsActive)
    VALUES ('System Administrator', 'admin', 'REPLACE_ON_FIRST_RUN', 'REPLACE_ON_FIRST_RUN', 1, 1);
END
GO
