using System.Text;
using Birthmas.Bot.Modules;
using Birthmas.Data;
using Birthmas.Service;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Birthmas.Bot;

public class Commands(IBirthmasService service, DiscordSocketClient client, ILogger<BirthmasBot> logger, BirthmasContext db)
{
    private ILogger<BirthmasBot> Logger { get; set; } = logger;
    private IBirthmasService Service { get; set; } = service;
    private DiscordSocketClient Client { get; set; } = client;

    #region command object

    private static readonly SlashCommandBuilder SetBirthday = new()
    {
        Name = "set-birthday",
        Description = "Set your birthday",
        Options =
        [
            new SlashCommandOptionBuilder
            {
                Name = "month",
                Description = "Month of birthday",
                IsRequired = true,
                Type = ApplicationCommandOptionType.Integer,
                Choices = 
                    [
                        new ApplicationCommandOptionChoiceProperties()
                        {
                            Name = "January",
                            Value = 1
                        },
                        new ApplicationCommandOptionChoiceProperties()
                        {
                            Name = "February",
                            Value = 2
                        },
                        new ApplicationCommandOptionChoiceProperties()
                        {
                            Name = "March",
                            Value = 3
                        },
                        new ApplicationCommandOptionChoiceProperties()
                        {
                            Name = "April",
                            Value = 4
                        },
                        new ApplicationCommandOptionChoiceProperties()
                        {
                            Name = "May",
                            Value = 5
                        },
                        new ApplicationCommandOptionChoiceProperties()
                        {
                            Name = "June",
                            Value = 6
                        },
                        new ApplicationCommandOptionChoiceProperties()
                        {
                            Name = "July",
                            Value = 7
                        },
                        new ApplicationCommandOptionChoiceProperties()
                        {
                            Name = "August",
                            Value = 8
                        },
                        new ApplicationCommandOptionChoiceProperties()
                        {
                            Name = "September",
                            Value = 9
                        },
                        new ApplicationCommandOptionChoiceProperties()
                        {
                            Name = "October",
                            Value = 10
                        },
                        new ApplicationCommandOptionChoiceProperties()
                        {
                            Name = "November",
                            Value = 11
                        },
                        new ApplicationCommandOptionChoiceProperties()
                        {
                            Name = "December",
                            Value = 12
                        }
                    ]
            },

            new SlashCommandOptionBuilder
            {
                Name = "day",
                Description = "Day of birthday",
                IsRequired = true,
                Type = ApplicationCommandOptionType.Integer
            }
        ]
    };
    
    private static readonly SlashCommandBuilder RemoveBirthday = new()
    {
        Name = "remove-birthday",
        Description = "Remove your birthday from the list"
    };

    private static readonly SlashCommandBuilder ServerBirthdays = new()
    {
        Name = "server-birthdays",
        Description = "See all birthdays for the year",
    };

    private static readonly SlashCommandBuilder MyBirthday = new()
    {
        Name = "my-birthday",
        Description = "See my birthday"
    };

    private static readonly SlashCommandBuilder AnnounceBirthday = new()
    {
        Name = "announce-birthday",
        Description = "Manually trigger birthday announcement",
        DefaultMemberPermissions = GuildPermission.ManageGuild,
        Options =
        [
            new SlashCommandOptionBuilder()
            {
                Name = "person",
                Description = "Person to celebrate",
                IsRequired = true,
                Type = ApplicationCommandOptionType.User
            },

        ]
    };
    
    private static readonly SlashCommandBuilder ForceBirthdayCheck = new()
    {
        Name = "force",
        Description = "force check for birthdays",
        DefaultMemberPermissions = GuildPermission.Administrator
    };
    
    #endregion
    
    public async Task InitCommands()
    {
        List<SlashCommandBuilder> commands = new()
        {
            SetBirthday,
            RemoveBirthday,
            ServerBirthdays,
            MyBirthday,
            AnnounceBirthday,
            ForceBirthdayCheck
        };

        Logger.LogInformation($"Updating commands");
        Logger.LogTrace(string.Join(" | ", commands.Select(c => c.Name)));
        await Client.BulkOverwriteGlobalApplicationCommandsAsync(
            // ReSharper disable once CoVariantArrayConversion
            commands.Select(b => b.Build()).ToArray());

    }
    
    #region command handlers

    public async Task SetBirthdayAsync(SocketSlashCommand arg)
    {
        await arg.DeferAsync(ephemeral: true);

        var monthL = (long)arg.Data.Options.First(o => o.Name == "month").Value;
        var dayL = (long)arg.Data.Options.First(o => o.Name == "day").Value;

        var month = Convert.ToInt32(monthL);
        var day = Convert.ToInt32(dayL);

        DateOnly date;
        try
        {
            date = new DateOnly(1972, month, day);
        }
        catch (ArgumentOutOfRangeException)
        {
            await arg.FollowupAsync("Please enter a valid date");
            return;
        }

        Service.AddBirthday(arg.User.Id, date);
        
        _ = arg.FollowupAsync($"Your birthday has been recorded as {date:M}");
    }
    
    public async Task RemoveBirthdayAsync(SocketSlashCommand arg)
    {   
        await arg.DeferAsync(ephemeral: true);
        
        Service.RemoveBirthday(arg.User.Id);
        
        await arg.FollowupAsync("Your birthday has been removed.", ephemeral: true);
    }
    
    public async Task GetServerBirthdays(SocketSlashCommand arg)
    {
        await arg.DeferAsync();
        var bdays = await Service.GetServerBirthdays();
        
        if (!bdays.Any())
        {
            await arg.FollowupAsync("No birthdays found.");
            return;
        }
        int longestUsername = bdays
            .Select(b => b.Name)
            .OrderByDescending(p => p.Length)
            .First()
            .Length;
        int longestDate = bdays
            .Select(b => b.TableString)
            .OrderByDescending(p => p.Length)
            .First()
            .Length;

        var personTitle = "Person";
        var birthdayTitle = "Birthday";
        
        longestUsername = longestUsername < birthdayTitle.Length ? birthdayTitle.Length : longestUsername;
        longestDate = longestDate < birthdayTitle.Length ? birthdayTitle.Length : longestDate;

        var userHeader = personTitle.CenterString(longestUsername);
        var birthdayHeader = birthdayTitle.CenterString(longestDate);
        
        var header = $"| {userHeader} | {birthdayHeader} |";
        
        var builder = new StringBuilder();
        
        builder.AppendLine("```");
        builder.AppendLine($"\u256d{string.Concat(Enumerable.Repeat('\u2015', longestUsername + 2))}\u252c{string.Concat(Enumerable.Repeat('\u2015', longestDate + 2))}\u256e");
        builder.AppendLine(header);

        builder.AppendLine($"\u251c{string.Concat(Enumerable.Repeat('\u2015', longestUsername + 2))}\u007c{string.Concat(Enumerable.Repeat('\u2015', longestDate + 2))}\u2524");
        var peopleSorted = bdays.OrderBy(p => p.Birthday);

        foreach (var b in peopleSorted)
        {
            var leap = b.Birthday is { Month: 2, Day: 29 };
            var birthday = b.TableString;
            birthday += leap ? "*" : string.Empty;
            
            builder.AppendLine($"\u007c {b.Name.CenterString(longestUsername)} | {birthday.CenterString(longestDate)} \u007c");
        }

        builder.AppendLine($"\u2570{string.Concat(Enumerable.Repeat('\u2015', longestUsername + 2))}\u2534{string.Concat(Enumerable.Repeat('\u2015', longestDate + 2))}\u256f");
        builder.Append("```");

        await arg.FollowupAsync(builder.ToString());
    }
    
    public async Task MyBirthdayAsync(SocketSlashCommand arg)
    {
        await arg.DeferAsync(ephemeral: true);
        var birthday = Service.GetBirthday(arg.User.Id);
        
        if (null == birthday) await arg.FollowupAsync("No birthday found. use /set-birthday to set your birthday.");
        else await arg.FollowupAsync($"Your birthday has been recorded as {birthday.Date:M}");
    }
    
    public async Task AnnounceBirthdayAsync(SocketSlashCommand arg)
    {
        try
        {
            await arg.DeferAsync(ephemeral: true);

            var person = (SocketGuildUser)arg.Data.Options.First(o => o.Name == "person").Value;

            await Service.PostBirthdayAnnouncementAsync(person.Id);

            await arg.DeleteOriginalResponseAsync();
        }
        catch (Exception ex)
        {
            await arg.FollowupAsync(ex.Message);
            Logger.LogError(ex, "Error occured while announcing birthday");
        }
    }

    public async Task ForceBirthdayCheckAsync(SocketSlashCommand arg, IScheduler scheduler, ITrigger trigger)
    {
        if (arg.User.Id != 191051620430249984)
        {
            await arg.RespondAsync("Only Dale can use this command.", ephemeral: true);
            return;
        }

        await scheduler.TriggerJob(trigger.JobKey);
        await arg.RespondAsync("Birthday check triggered.", ephemeral: true);
    }
    
    #endregion
}