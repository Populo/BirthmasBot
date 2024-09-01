using Birthmas.Data;
using Discord;
using Discord.Rest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace Birthmas.Service;

public class BirthmasService : IBirthmasService
{
    private DiscordRestClient Client { get; set; }
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

    public BirthmasService()
    {
        Client = new DiscordRestClient();
        Client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("BIRTHMAS_TOKEN")).Wait();
    }
 
    public List<Person> GetBirthdays(DateTime date)
    {
        using var db = new BirthmasContext();
        
        _logger.Info($"Getting Birthdays for {date:d}");
        return db.People.Where(b => b.Date.Day == date.Day && b.Date.Month == date.Month).ToList();
    }

    public Person? GetBirthday(ulong userId)
    {
        using var db = new BirthmasContext();
        return db.People.FirstOrDefault(b => b.UserId == userId);
    }

    // this is disgusting but i dont expect this bot to be used by more than 2 servers
    public async Task<List<ServerConfig>> GetServersByUserAsync(ulong userId)
    {
        var user = await Client.GetUserAsync(userId);
        _logger.Info($"Getting servers for {user.Username}");
        
        var db = new BirthmasContext();
        
        var result = new List<ServerConfig>();
        foreach (var server in db.ServerConfigs)
        {
            var g = await Client.GetGuildAsync(server.ServerId);
            if (g == null) continue;
            
            var u = await g.GetUserAsync(userId);
            if (null != u) result.Add(server);
        }
        
        return result;
    }
    
    public async Task GiveUserRoleAsync(ulong userId, ulong serverId, ulong roleId)
    {
        var guild = await Client.GetGuildAsync(serverId) 
                    ?? throw new Exception("Cannot get guild");
        var user = await guild.GetUserAsync(userId) 
                   ?? throw new Exception("Cannot get user");
        var role = guild.GetRole(roleId) 
                   ?? throw new Exception("Cannot get role");
        
        _logger.Info($"Gave user {user.Username} the role {role.Name} in server {guild.Name}");
        await user.AddRoleAsync(role);
    }

    public Person AddBirthday(ulong userId, DateTime date, ulong serverId)
    {
        using var db = new BirthmasContext();

        var birthday = db.People
            .Include(b => b.Server)
            .FirstOrDefault(b => b.UserId == userId
            && b.Server.ServerId == serverId);
        
        if (null == birthday) {
            var server = db.ServerConfigs.FirstOrDefault(s => s.ServerId == serverId);
            if (null == server) throw new Exception("Cannot find server");
            
            birthday = new Person()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Server = server
            };
            
            db.People.Add(birthday);
        }

        birthday.Date = date;
        db.SaveChanges();
        
        _logger.Info($"Added birthday {date:d} for {userId}");
        return birthday;
    }

    public ServerConfig ConfigServer(ulong serverId, bool giveRole, ulong roleId, ulong channelId)
    {
        using var db = new BirthmasContext();

        var server = db.ServerConfigs.FirstOrDefault(s => s.ServerId == serverId);
        if (null == server)
        {
            server = new ServerConfig()
            {
                ServerId = serverId,
            };
            db.ServerConfigs.Add(server);
        }

        server.GiveRole = giveRole;
        server.RoleId = roleId;
        server.AnnouncementChannelId = channelId;
        
        db.SaveChanges();

        _logger.Info($"Added server {serverId}");
        return server;
    }
    public async Task RemoveBirthdayRoleFromUserAsync(ulong guildId, ulong userId, ulong roleId)
    {
        var guild = await Client.GetGuildAsync(guildId)
                    ?? throw new Exception("Cannot get guild");
        var user = await guild.GetUserAsync(userId)
                    ?? throw new Exception("Cannot get user");
        var role = guild.GetRole(roleId)
                    ?? throw new Exception("Cannot get role");
        
        _ = user.RemoveRoleAsync(role);
        _logger.Info($"Removed birthday role {role.Name} from {user.Username} in {guild.Name}");
    }

    public Person? RemoveBirthday(ulong userId, ulong serverId)
    {
        using var db = new BirthmasContext();
        var birthday = db.People
            .Include(b => b.Server)
            .FirstOrDefault(b => b.UserId == userId
                && b.Server.ServerId == serverId);
        if (null == birthday) return null;
        
        db.People.Remove(birthday);
        db.SaveChanges();
        
        _logger.Info($"Removed birthday for user {userId}");
        return birthday;
    }

    public ServerConfig? RemoveServer(ulong serverId)
    {
        using var db = new BirthmasContext();
        var server = db.ServerConfigs
            .Include(s => s.People)
            .FirstOrDefault(s => s.ServerId == serverId);
        
        if (null == server) return null;
        
        db.ServerConfigs.Remove(server);
        db.People.RemoveRange(server.People);
        db.SaveChanges();
        
        _logger.Info($"Removed server {serverId}");
        return server;
    }

    public ServerConfig? GetServer(ulong serverId)
    {
        using var db = new BirthmasContext();
        return db.ServerConfigs
            .Include(sc => sc.People)
            .FirstOrDefault(s => s.ServerId == serverId);
    }
}