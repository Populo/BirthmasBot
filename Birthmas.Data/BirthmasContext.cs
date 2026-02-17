using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Birthmas.Data;

public class BirthmasContext : DbContext
{
    public DbSet<Person> People { get; set; }
    public DbSet<Config> Configs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>(entity =>
        {
            entity.HasKey(p => p.UserId);

            // key is the userId, we dont want to generate this
            entity.Property(p => p.UserId).ValueGeneratedNever();
        });
    }
    
    public static string GetMasterConnectionString()
    {
        return new SqlConnectionStringBuilder()
        {
            DataSource = Environment.GetEnvironmentVariable("DbHost"),
            InitialCatalog = "master",
            UserID = "sa",
            TrustServerCertificate = true,
            Encrypt = true,
            Password = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD")
        }.ConnectionString;
    }

    private static string GetAppConnectionString()
    {
        return new SqlConnectionStringBuilder()
        {
            DataSource = Environment.GetEnvironmentVariable("DbHost"),
            InitialCatalog = Environment.GetEnvironmentVariable("DbName"),
            UserID = Environment.GetEnvironmentVariable("DbUser"),
            TrustServerCertificate = true,
            Encrypt = true,
            Password = Environment.GetEnvironmentVariable("DbPassword")
        }.ConnectionString;
    }
    
    

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder = null)
    {
        if (optionsBuilder.IsConfigured) return;

        optionsBuilder.UseSqlServer(GetAppConnectionString(),
            options => { 
                options.EnableRetryOnFailure(
                    maxRetryCount: 20, 
                    maxRetryDelay: TimeSpan.FromSeconds(10), 
                    errorNumbersToAdd: null
                ); 
            });
    }
}

[PrimaryKey(nameof(UserId))]
public class Person
{
    public ulong UserId { get; set; }
    public DateOnly Date { get; set; }
}

public class Config
{
    [Key]
    public string Name { get; set; }
    public string Value { get; set; }
}