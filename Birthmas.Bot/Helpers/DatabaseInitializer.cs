using Birthmas.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Birthmas.Bot.Helpers;

public class DatabaseInitializer(ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync()
    {
        try
        {
            // Step 1: Create database and user if they don't exist
            await EnsureDatabaseAndUserExistAsync();
            
            // Step 2: Apply migrations
            await ApplyMigrationsAsync();
            
            logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during database initialization");
            throw;
        }
    }
    
    private async Task EnsureDatabaseAndUserExistAsync()
    {
        using (var connection = new SqlConnection(BirthmasContext.GetMasterConnectionString()))
        {
            await connection.OpenAsync();
            
            using (var command = connection.CreateCommand())
            {
                command.CommandTimeout = 60;
                
                // Create database if it doesn't exist
                command.CommandText = $@"
                    IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{Environment.GetEnvironmentVariable("DbName")}')
                    BEGIN
                        CREATE DATABASE [{Environment.GetEnvironmentVariable("DbName")}];
                    END";
                
                await command.ExecuteNonQueryAsync();
                logger.LogInformation("Database ensured to exist");
                
                // Create login if it doesn't exist
                var dbUser = Environment.GetEnvironmentVariable("DbUser");
                var dbPassword = Environment.GetEnvironmentVariable("DbPassword");
                command.CommandText = $@"
                    IF NOT EXISTS (SELECT * FROM sys.syslogins WHERE name = '{dbUser}')
                    BEGIN
                        CREATE LOGIN [{dbUser}] WITH PASSWORD = '{dbPassword}';
                    END";
                
                await command.ExecuteNonQueryAsync();
                logger.LogInformation($"Login '{dbUser}' ensured to exist");
            }
            
            // Create user in the database if it doesn't exist
            using (var command = connection.CreateCommand())
            {
                command.CommandTimeout = 60;
                command.CommandText = $@"
                    USE [{Environment.GetEnvironmentVariable("DbName")}];
                    IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = '{Environment.GetEnvironmentVariable("DbUser")}')
                    BEGIN
                        CREATE USER [{Environment.GetEnvironmentVariable("DbUser")}] FOR LOGIN [{Environment.GetEnvironmentVariable("DbUser")}];
                        ALTER ROLE db_owner ADD MEMBER [{Environment.GetEnvironmentVariable("DbUser")}];
                    END";
                
                await command.ExecuteNonQueryAsync();
                logger.LogInformation($"User '{Environment.GetEnvironmentVariable("DbUser")}' ensured to exist with db_owner role");
            }
        }
    }
    
    private async Task ApplyMigrationsAsync()
    {
        using (var context = new BirthmasContext())
        {
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            
            if (pendingMigrations.Any())
            {
                logger.LogInformation($"Applying {pendingMigrations.Count()} pending migrations");
                await context.Database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully");
            }
            else
            {
                logger.LogInformation("No pending migrations");
            }
        }
    }
}
