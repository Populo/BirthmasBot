using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Birthmas.Data;

public class BirthmasContext : DbContext
{
    public DbSet<Person> People { get; set; }
    public DbSet<ServerConfig> ServerConfigs { get; set; }
    public DbSet<Birthmas> Birthmas { get; set; }
    public DbSet<Config> Configs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>()
            .HasMany(p => p.Birthmas)
            .WithOne(b => b.Person)
            .IsRequired();
        
        modelBuilder.Entity<ServerConfig>()
            .HasMany(s => s.Birthmas)
            .WithOne(b => b.ServerConfig)
            .IsRequired(false);
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
    [Key]
    public ulong UserId { get; set; }
    public DateTime Date { get; set; }
    
    public ICollection<Birthmas> Birthmas { get; set; }
}

public class ServerConfig
{
    [Key]
    public ulong ServerId { get; set; }
    public ulong AnnouncementChannelId { get; set; }
    public bool GiveRole { get; set; }
    public ulong RoleId { get; set; }
    
    public ICollection<Birthmas> Birthmas { get; set; }
}

public class Birthmas
{
    public Guid Id { get; set; }
    public Person Person { get; set; }
    public ServerConfig ServerConfig { get; set; }
}

public class Config
{
    [Key]
    public string Name { get; set; }
    public string Value { get; set; }
}