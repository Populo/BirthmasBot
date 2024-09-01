﻿using System.Text;
using Birthmas.Bot.Modules;
using Birthmas.Service;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Humanizer;
using NLog;

namespace Birthmas.Bot;

public class Commands(IBirthmasService service, DiscordRestClient client)
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
    private IBirthmasService _service { get; set; } = service;
    private DiscordRestClient _client { get; set; } = client;

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
                Description = "Month of birthday (as number)",
                IsRequired = true,
                Type = ApplicationCommandOptionType.Integer
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
        Description = "See birthdays coming up in the next week",
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

        Logger.Info($"Updating commands");
        Logger.Trace(string.Join(" | ", commands.Select(c => c.Name)));
        await _client.BulkOverwriteGlobalCommands(
            // ReSharper disable once CoVariantArrayConversion
            commands.Select(b => b.Build()).ToArray());

    }
    
    #region command handlers

    public async Task SetBirthdayAsync(SocketSlashCommand arg)
    {
        await arg.DeferAsync(ephemeral: true);
        var server = _service.GetServer(arg.GuildId!.Value);
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
            date = new DateTime(1970, month, day);
        }
        catch (ArgumentOutOfRangeException)
        {
            await arg.FollowupAsync("Please enter a valid date");
            return;
        }
        
        var person = _service.AddBirthday(arg.User.Id, date);
        var servers = await _service.GetServersByUserAsync(arg.User.Id);
        foreach (var s in servers)
        {
            _service.AddBirthmas(person, s);
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
        
        var exists = _service.GetServer(arg.GuildId!.Value) is not null;
        _service.ConfigServer(arg.GuildId!.Value, roleBool, roleToGive, channel.Id);
        
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
        
        _service.RemoveBirthday(arg.User.Id);
        
        await arg.FollowupAsync("Your birthday has been removed.", ephemeral: true);
    }
    
    public async Task RemoveServerAsync(SocketSlashCommand arg)
    {
        await arg.DeferAsync(ephemeral: true);
        
        _service.RemoveServer(arg.GuildId!.Value);
        
        await arg.FollowupAsync("Your server has been removed.");
    }
    
    public async Task GetServerBirthdays(SocketSlashCommand arg)
    {
        await arg.DeferAsync(ephemeral: true);
        var births = _service.GetBirthmasesForServer(arg.GuildId!.Value);
        if (!births.Any())
        {
            await arg.FollowupAsync("No birthdays found.");
            return;
        }
        int longestUsername = births
            .Select(b => _client.GetUserAsync(b.Person.UserId).Result.Username)
            .OrderByDescending(p => p.Length)
            .First()
            .Length;
        int longestDate = births
            .Select(b => $"{b.Person.Date:dddd, MMMM dd, yyyy}")
            .OrderByDescending(p => p.Length)
            .First()
            .Length;

        var header = $"| {"Person".CenterString(longestUsername)} | {"Birthday".CenterString(longestDate)} |";
        
        var builder = new StringBuilder();
        builder.AppendLine(header);
        builder.AppendLine(string.Concat(Enumerable.Repeat('-', header.Length)));

        foreach (var b in births)
        {
            var username = await _client.GetUserAsync(b.Person.UserId)
                           ?? throw new Exception("User not found");
            var currentYear = DateTime.Now.Year;
            var bday = b.Person.Date;
            var birthday = bday.AddYears(currentYear - bday.Year).ToString("dddd, MMMM dd, yyyy");

            builder.AppendLine($"| {username.Username.CenterString(longestUsername)} | {birthday.CenterString(longestDate)} |");
        }

        await arg.FollowupAsync(builder.ToString());
    }
    
    public async Task MyBirthdayAsync(SocketSlashCommand arg)
    {
        await arg.DeferAsync(ephemeral: true);
        var birthday = _service.GetBirthday(arg.User.Id);
        
        if (null == birthday) await arg.FollowupAsync("No birthday found. use /set-birthday to set your birthday.");
        else await arg.FollowupAsync($"Your birthday has been recorded as {birthday.Date:M}");
    }
    
    #endregion
}