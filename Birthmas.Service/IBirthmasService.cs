using Birthmas.Data;
using Birthmas.Service.Models;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Birthmas.Service;

public interface IBirthmasService
{
    public string GetBotVersion();
    public List<Person> GetBirthdaysForDate(DateOnly date);
    public Task<List<BirthdayObject>> GetServerBirthdays();
    public Person? GetBirthday(ulong userId);
    public Task GiveUserRoleAsync(ulong userId);
    public Task RemoveBirthdayRoleFromUserAsync(ulong userId);
    public Person AddBirthday(ulong userId, DateOnly date);
    public Person? RemoveBirthday(ulong userId);
    public Task<List<IGuildUser>> GetPeopleWithBirthdayRole();
    public Task PostBirthdayAnnouncementAsync(Person person);
    public Task PostBirthdayAnnouncementAsync(ulong userId);
    public Task<string> GetUserDisplayName(Person person);
    public Task<BirthdayObject> GetNextBirthday();
}

public class BirthmasService(
    ILogger<BirthmasService> logger,
    DiscordSocketClient client,
    BotConfig config,
    BirthmasContext db) : IBirthmasService
{
    private const string UnknownUser = "UNKNOWN_USER";
    
    public string GetBotVersion()
    {
        return db.Configs.FirstOrDefault(c => c.Name == "Version")?.Value ?? "0.0.0";
    }

    public List<Person> GetBirthdaysForDate(DateOnly date)
    {
        return db.People.Where(b => b.Date.Day == date.Day && b.Date.Month == date.Month).ToList();
    }
    
    public async Task<List<BirthdayObject>> GetServerBirthdays()
    {
        var people = db.People;
        List<BirthdayObject> bdays = [];
        foreach (var person in people)
        {
            var u = GetGuild().GetUser(person.UserId)
                                ?? await CheckUser(person.UserId);

            if (null == u) RemoveBirthday(person.UserId);

            else bdays.Add(new BirthdayObject(u.DisplayName ?? u.Username, person.Date));
        }

        return bdays;
    }

    public Person? GetBirthday(ulong userId)
    {
        return db.People.FirstOrDefault(b => b.UserId == userId);
    }

    public async Task GiveUserRoleAsync(ulong userId)
    {
        if (config.BirthdayRoleId != 0)
        {
            var guild = GetGuild();
            await guild.GetUser(userId).AddRoleAsync(config.BirthdayRoleId);
        }
    }

    public async Task RemoveBirthdayRoleFromUserAsync(ulong userId)
    {
        if (config.BirthdayRoleId != 0)
        {
            var guild = GetGuild();
            await guild.GetUser(userId).RemoveRoleAsync(config.BirthdayRoleId);
        }
    }

    public Person AddBirthday(ulong userId, DateOnly date)
    {
        var p = db.People.FirstOrDefault(b => b.UserId == userId);

        if (null == p)
        {
            p = new Person {UserId = userId, Date = date};
            db.People.Add(p);
        }
        else
        {
            p.Date = date;
        }
        
        db.SaveChanges();
        return p;
    }

    public Person? RemoveBirthday(ulong userId)
    {
        var p = db.People.FirstOrDefault(b => b.UserId == userId);
        if (p == null) return p;
        
        db.People.Remove(p);
        db.SaveChanges();
        return p;
    }

    public async Task<List<IGuildUser>> GetPeopleWithBirthdayRole()
    {
        var guild = GetGuild();
        var role = await guild.GetRoleAsync(config.BirthdayRoleId);
        var users = await guild.GetUsersAsync().FlattenAsync();
        return users.Where(u => u.RoleIds.Contains(role.Id)).ToList();
    }
    
    public async Task PostBirthdayAnnouncementAsync(Person person) => await PostBirthdayAnnouncementAsync(person.UserId);

    public async Task PostBirthdayAnnouncementAsync(ulong userId)
    {
        if (await client.GetChannelAsync(config.AnnouncementChannelId) is not ITextChannel channel) throw new Exception("Announcement channel is not a text channel");
        
        var user = await client.GetUserAsync(userId)
            ?? throw new Exception("Cannot get user");
        
        await GiveUserRoleAsync(userId);
        await channel.SendMessageAsync($"Happy birthday, {user.Mention}!");
    }

    public async Task<string> GetUserDisplayName(Person person)
    {
        var guild = GetGuild();
        var user = guild.GetUser(person.UserId)
                   ?? await CheckUser(person.UserId);
        
        if (user == null) return UnknownUser;
        
        return user.DisplayName ?? user.Username;
    }

    public async Task<BirthdayObject> GetNextBirthday()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Pull everyone once (Month/Day comparisons for "next occurrence" are done in-memory)
        var people = await db.People
            .AsNoTracking()
            .ToListAsync();

        if (people.Count == 0)
            throw new InvalidOperationException("No birthdays are recorded.");

        static DateOnly NextOccurrence(DateOnly birthday, DateOnly today)
        {
            // Convert the stored month/day into "this year's occurrence"
            DateOnly occurrence;
            try
            {
                occurrence = new DateOnly(today.Year, birthday.Month, birthday.Day);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Handles Feb 29 on non-leap years. celebrate on Feb 28
                occurrence = new DateOnly(today.Year, 2, 28);
            }

            return occurrence < today ? occurrence.AddYears(1) : occurrence;
        }

        // Order once, then walk forward until we find a member that still exists.
        foreach (var candidate in people.OrderBy(p => NextOccurrence(p.Date, today)))
        {
            if (!await UserIsInGuild(candidate.UserId))
            {
                RemoveBirthday(candidate.UserId);
                continue;
            }

            var displayName = await GetUserDisplayName(candidate);
            return new BirthdayObject(displayName, candidate.Date);
        }

        // If we got here, everyone in the DB was an outcast and got removed.
        throw new InvalidOperationException("No valid birthdays found (all users were removed from the server).");
    }

    public async Task<bool> UserIsInGuild(ulong userId)
    {
        var user = GetGuild().GetUser(userId);
        return user != null;
    }
    
    private async Task<SocketGuildUser?> CheckUser(ulong userId)
    {
        if (!await UserIsInGuild(userId))
        {
            RemoveBirthday(userId);
        }

        return null;
    }
    
    private SocketGuild GetGuild() => client.GetGuild(config.ServerId) ?? throw new Exception("Cannot get guild");
}