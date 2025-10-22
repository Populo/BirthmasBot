using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Birthmas.Data;

public class BirthmasContext : DbContext
{
    public DbSet<Person> People { get; set; }
    public DbSet<ServerConfig> ServerConfigs { get; set; }
    public DbSet<Config> Configs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServerConfig>()
            .HasMany(sc => sc.People)
            .WithOne(p => p.Server)
            .IsRequired();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder = null)
    {
        if (optionsBuilder.IsConfigured) return;
        
        var connectionString = new SqlConnectionStringBuilder
        {
            DataSource = "dale-server",  // or "dale-server\\INSTANCENAME" if using named instance
            InitialCatalog = "db_birthmasbot_prod",
            UserID = "user_birthmasbot_prod",
            Password = File.ReadAllText("/run/secrets/dbPass"),
            TrustServerCertificate = true,  // Add this if you have certificate issues
            Encrypt = true  // SQL Server defaults to encrypted connections
        }.ConnectionString;

        optionsBuilder.UseSqlServer(connectionString,
            options => { 
                options.EnableRetryOnFailure(
                    maxRetryCount: 20, 
                    maxRetryDelay: TimeSpan.FromSeconds(10), 
                    errorNumbersToAdd: null
                ); 
            });
    }
}

public class Person
{
    public Guid Id { get; set; }
    public ulong UserId { get; set; }
    public DateTime Date { get; set; }
    
    public ServerConfig Server { get; set; }
}

public class ServerConfig
{
    [Key]
    public ulong ServerId { get; set; }
    public ulong AnnouncementChannelId { get; set; }
    public bool GiveRole { get; set; }
    public ulong RoleId { get; set; }
    
    public ICollection<Person> People { get; set; }
}

public class Config
{
    [Key]
    public string Name { get; set; }
    public string Value { get; set; }
}