using System.Text;
using Birthmas.Bot.Modules;
using Birthmas.Service;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Birthmas.Bot;

public class Commands(IBirthmasService service, DiscordRestClient client, ILogger<BirthmasBot> logger)
{
    private ILogger<BirthmasBot> Logger { get; set; } = logger;
    private IBirthmasService Service { get; set; } = service;
    private DiscordRestClient Client { get; set; } = client;

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

    private static readonly SlashCommandBuilder ConfigServer = new()
    {
        Name = "config-server",
        Description = "Configure your server",
        DefaultMemberPermissions = GuildPermission.ManageGuild,
        Options = 
            [
                new SlashCommandOptionBuilder()
                {
                    Name = "announcementchannel",
                    Description = "Channel to announce birthday in",
                    IsRequired = true,
                    Type = ApplicationCommandOptionType.Channel
                },
                new SlashCommandOptionBuilder()
                {
                    Name = "giverole",
                    Description = "Give the birthday person a special role?",
                    IsRequired = true,
                    Type = ApplicationCommandOptionType.Boolean
                },
                new SlashCommandOptionBuilder()
                {
                    Name = "roletogive",
                    Description = "Role to give the birthday person",
                    IsRequired = false,
                    Type = ApplicationCommandOptionType.Role
                }
            ]
    };

    private static readonly SlashCommandBuilder RemoveBirthday = new()
    {
        Name = "remove-birthday",
        Description = "Remove your birthday from the list"
    };

    private static readonly SlashCommandBuilder RemoveServer = new()
    {
        Name = "remove-server",
        Description = "Remove your server",
        DefaultMemberPermissions = GuildPermission.ManageGuild,
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
    
    #endregion
    
    public async Task InitCommands()
    {
        List<SlashCommandBuilder> commands = new()
        {
            SetBirthday,
            ConfigServer,
            RemoveBirthday,
            RemoveServer,
            ServerBirthdays,
            MyBirthday
        };

        Logger.LogInformation($"Updating commands");
        Logger.LogTrace(string.Join(" | ", commands.Select(c => c.Name)));
        await Client.BulkOverwriteGlobalCommands(
            // ReSharper disable once CoVariantArrayConversion
            commands.Select(b => b.Build()).ToArray());

    }
    
    #region command handlers

    public async Task SetBirthdayAsync(SocketSlashCommand arg)
    {
        await arg.DeferAsync(ephemeral: true);
        var server = Service.GetServer(arg.GuildId!.Value);
        if (null == server)
        {
            await arg.FollowupAsync("This server has not been configured yet.");
            return;
        }

        var monthL = (long)arg.Data.Options.First(o => o.Name == "month").Value;
        var dayL = (long)arg.Data.Options.First(o => o.Name == "day").Value;

        var month = Convert.ToInt32(monthL);
        var day = Convert.ToInt32(dayL);

        DateTime date;
        try
        {
            date = new DateTime(1972, month, day);
        }
        catch (ArgumentOutOfRangeException)
        {
            await arg.FollowupAsync("Please enter a valid date");
            return;
        }

        try
        {
            var person = Service.AddBirthday(arg.User.Id, date, arg.GuildId!.Value);
        }
        catch
        {
            await arg.FollowupAsync("This server has not been configured yet.");
            return;
        }
        
        _ = arg.FollowupAsync($"Your birthday has been recorded as {date:M}");
    }
    
    public async Task SetServerAsync(SocketSlashCommand arg)
    {
        await arg.DeferAsync(ephemeral: true);
        
        var channel = (SocketChannel)arg.Data.Options.First(o => o.Name == "announcementchannel").Value;
        var roleBool = (bool)arg.Data.Options.First(o => o.Name == "giverole").Value;
        var roleToGiveParam = arg.Data.Options.FirstOrDefault(o => o.Name == "roletogive");

        ulong roleToGive = 0;
        if (roleBool && null != roleToGiveParam)
        {
            roleToGive = Convert.ToUInt64(((IRole)roleToGiveParam.Value).Id);
        }
        else if (roleBool && null == roleToGiveParam)
        {
            await arg.FollowupAsync("Must give a role if GiveRole parameter is true");
            return;
        }

        if (channel is SocketVoiceChannel)
        {
            await arg.FollowupAsync("Please specify a text channel");
            return;
        }
        
        var exists = Service.GetServer(arg.GuildId!.Value) is not null;
        Service.ConfigServer(arg.GuildId!.Value, roleBool, roleToGive, channel.Id);
        
        if (exists)
        {
            await arg.FollowupAsync("Server config updated.");
        }
        else
        {
            await arg.FollowupAsync("Server added.");
        }
    }
    
    public async Task RemoveBirthdayAsync(SocketSlashCommand arg)
    {   
        await arg.DeferAsync(ephemeral: true);
        
        Service.RemoveBirthday(arg.User.Id, arg.GuildId!.Value);
        
        await arg.FollowupAsync("Your birthday has been removed.", ephemeral: true);
    }
    
    public async Task RemoveServerAsync(SocketSlashCommand arg)
    {
        await arg.DeferAsync(ephemeral: true);
        
        Service.RemoveServer(arg.GuildId!.Value);
        
        await arg.FollowupAsync("Your server has been removed.");
    }
    
    public async Task GetServerBirthdays(SocketSlashCommand arg)
    {
        await arg.DeferAsync();
        var guild = await Client.GetGuildAsync(arg.GuildId!.Value);
        var births = Service.GetServer(guild.Id);
        
        if (null == births)
        {
            await arg.FollowupAsync("This server has not been configured yet.");
            return;
        }
        if (!births.People.Any())
        {
            await arg.FollowupAsync("No birthdays found.");
            return;
        }
        Dictionary<ulong, string> people = [];
        List<string> dates = [];
        foreach (var b in births.People)
        {
            var user = await guild.GetUserAsync(b.UserId);
            if (null == user) continue;
            
            var username = user.Nickname ?? user.Username;
            people.Add(b.UserId, username);
            var newDate = b.Date.AddYears(DateTime.Now.Year - b.Date.Year);
            dates.Add($"{newDate:dddd MMMM dd, yyyy}");
        }
        int longestUsername = people
            .OrderByDescending(p => p.Value.Length)
            .First()
            .Value
            .Length;
        int longestDate = dates
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
        var peopleSorted = births.People.OrderBy(p => p.Date.DayOfYear);

        foreach (var b in peopleSorted)
        {
            var nickname = people[b.UserId];
            var currentYear = DateTime.Now.Year;
            var bday = b.Date;

            var leap = bday is { Month: 2, Day: 29 };
            bday = bday.AddYears(currentYear - bday.Year);
            
            var birthday = $"{bday:dddd MMMM dd, yyyy}";
            birthday += leap ? "*" : string.Empty;
            
            builder.AppendLine($"\u007c {nickname.CenterString(longestUsername)} | {birthday.CenterString(longestDate)} \u007c");
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
    
    #endregion
}