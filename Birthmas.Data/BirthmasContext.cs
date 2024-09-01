using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

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
        if (!optionsBuilder.IsConfigured)
        {
            var connection = new MySqlConnectionStringBuilder
            {
                Server = "pinas",
                Database = "BirthdayBot",
                UserID = "BirthmasBot",
                Password = Environment.GetEnvironmentVariable("BirthmasBotDbPassword")
            };

            optionsBuilder.UseMySql(connection.ConnectionString,
                ServerVersion.AutoDetect(connection.ConnectionString),
                options => { options.EnableRetryOnFailure(20, TimeSpan.FromSeconds(10), new List<int>()); });
        }
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