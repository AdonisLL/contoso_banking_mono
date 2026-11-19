-- Contoso Legacy Bank - database bootstrap
-- Run against (localdb)\MSSQLLocalDB. Creates the ContosoLegacyBank database if missing.
IF DB_ID(N'ContosoLegacyBank') IS NULL
BEGIN
    CREATE DATABASE ContosoLegacyBank;
END
GO
