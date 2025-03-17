using System.Timers;
using Birthmas.Data;
using Birthmas.Service;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Serilog;
using Timer = System.Timers.Timer;

namespace Birthmas.Bot;

public class BirthmasBot
{
    public static Task Main(string[] args) => new BirthmasBot().Run(args);
    
    private readonly ILogger<BirthmasBot> _logger;
    private readonly Timer _timer;
    private DiscordSocketClient Client { get; set; }
    private DiscordRestClient RestClient { get; set; }
    private IBirthmasService BirthmasService { get; set; }
    private IServiceProvider Services { get; set; }

    private BirthmasBot()
    {
        Services = CreateProvider();

        Client = Services.GetRequiredService<DiscordSocketClient>()
            ?? throw new Exception("Cannot get client from factory");
        BirthmasService = Services.GetRequiredService<IBirthmasService>()
            ?? throw new Exception("Cannot get service from factory");
        _logger = Services.GetRequiredService<ILogger<BirthmasBot>>()
            ?? throw new Exception("Cannot get logger from factory");
        
        var now = DateTime.Now;
        var today1Am = DateTime.Today.AddMinutes(30);
        if (now > today1Am)
        {
            today1Am = today1Am.AddDays(1);
        }

        var duration = today1Am - now;
#if DEBUG
        duration = TimeSpan.FromMinutes(1);
#endif
        
        
        _timer = new Timer()
        {
            AutoReset = true,
            Enabled = true,
            Interval = duration.TotalMilliseconds
        };
        _timer.Elapsed += TimerOnElapsed;
    }

    private async Task Run(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            using var db = new BirthmasContext();
            var channelId = db.Configs.First(c => c.Name == "ErrorChannel").Value
                ?? throw new Exception("Cannot get error channel from config");
            
            _logger.LogError($"Unhandled exception occured: {e.ExceptionObject}");
            (Client.GetChannelAsync(ulong.Parse(channelId)).Result as SocketTextChannel
                ?? throw new Exception("Cannot get error channel from discord")).SendMessageAsync($"{e.ExceptionObject}");
        };
        
        if (args.Length == 0) throw new Exception("Include token in args");
        switch (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"))
        {
            case "Release":
                _logger.LogInformation("Environment: Prod");
                break;
            case "Develop":
                _logger.LogInformation("Environment: Dev");
                break;
        }
        
        Client.Ready += new Commands(BirthmasService, Client.Rest, _logger).InitCommands;
        Client.Log += ClientOnLog;
        Client.SlashCommandExecuted += ClientOnSlashCommandExecuted;
        
        await Client.LoginAsync(TokenType.Bot, args[0]);
        await Client.StartAsync();
        _timer.Start();

        RestClient = Client.Rest;
        _logger.LogInformation("Started");
        await Task.Delay(-1);
    }

    private async Task ClientOnSlashCommandExecuted(SocketSlashCommand arg)
    {
        var jsonArgs = new JObject();
        foreach (var o in arg.Data.Options)
        {
            jsonArgs.Add(null != o.Value ? new JProperty(o.Name, o.Value.ToString()) : new JProperty(o.Name, "null"));
        }

        _logger.LogInformation(
            $"Command received: {arg.CommandName}\nin channel: {await Client.GetChannelAsync(arg.ChannelId!.Value)}\nin server: {Client.GetGuild(arg.GuildId!.Value).Name}\nfrom: {arg.User.Username}\n```json\nargs:{jsonArgs}\n```");

        var commands = new Commands(BirthmasService, RestClient, _logger);
        switch (arg.CommandName)
        {
            case "set-birthday":
                _ = commands.SetBirthdayAsync(arg);
                break;
            case "config-server":
                _ = commands.SetServerAsync(arg);
                break;
            case "remove-birthday":
                _ = commands.RemoveBirthdayAsync(arg);
                break;
            case "remove-server":
                _ = commands.RemoveServerAsync(arg);
                break;
            case "server-birthdays":
                _ = commands.GetServerBirthdays(arg);
                break;
            case "my-birthday":
                _ = commands.MyBirthdayAsync(arg);
                break;
        }
    }

    private Task ClientOnLog(LogMessage arg)
    {
        _logger.LogInformation(arg.Message);
        if (null != arg.Exception)
        {
            _logger.LogError(arg.Exception.ToString());
            _logger.LogError(arg.Exception.InnerException?.StackTrace);
        }

        return Task.CompletedTask;
    }

    private void TimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            // remove people who are no longer in any servers
            BirthmasService.DownloadUsers();
            var outcasts = BirthmasService.PurgeTheOutcasts();
            if (outcasts.Any())
            {
                _logger.LogInformation($"Purged {outcasts.Count} outcasts.");
            }
            
            // only do the real work in the morning morning
            if (DateTime.Now.Hour != 0)
            {
                _logger.LogInformation("Not 1am.");
                return;
            }
            
            var roleHavers = BirthmasService.GetPeopleWithBirthdayRole();
            if (roleHavers.Any())
            {
                Parallel.ForEachAsync(roleHavers, async (person, _) =>
                {
                    try
                    {
                        var server = BirthmasService.GetServer(person.Guild.Id)
                                     ?? throw new Exception("Cannot get guild");
                        await person.RemoveRoleAsync(server.RoleId);
                        _logger.LogInformation($"Removed role from {person.Nickname} in {person.Guild.Name}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error while removing old role from {person.Username} in {person.Guild.Name}");
                    }
                });
            }
            // add today's birthday roles
            var todaysBirthdays = BirthmasService.GetBirthdays(DateTime.Today);
            if (todaysBirthdays.Any())
            {
                Parallel.ForEachAsync(todaysBirthdays, async (birthday, cancel) =>
                {
                    try
                    {
                        var servers = await BirthmasService.GetServersByUserAsync(birthday.UserId);
                        var user = await Client.GetUserAsync(birthday.UserId)
                                   ?? throw new Exception("Cannot get user");
                        if (!servers.Any()) return;

                        _ = Parallel.ForEachAsync(servers, cancel, async (server, cancel2) =>
                        {
                            var channel = await Client.GetChannelAsync(server.AnnouncementChannelId) as ITextChannel
                                          ?? throw new Exception("Cannot get channel from server");

                            _ = channel.SendMessageAsync($"Happy birthday {user.Mention}!");
                            if (server.GiveRole)
                            {
                                _ = BirthmasService.GiveUserRoleAsync(user.Id, server.ServerId, server.RoleId);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occured while adding roles");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occured while running.");
        }


        _timer.Interval = TimeSpan.FromHours(1).TotalMilliseconds;
    }
    
    private IServiceProvider CreateProvider()
    {
        var config = new DiscordSocketConfig()
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers
        };

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("logs/log.txt", 
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var collection = new ServiceCollection();

        collection.AddTransient<IBirthmasService, BirthmasService>();
        collection
            .AddSingleton(config)
            .AddSingleton<DiscordSocketClient>();
        collection
            .AddSingleton(config)
            .AddSingleton<DiscordRestClient>();

        collection.AddLogging(configuration =>
        {
            configuration.ClearProviders();
            configuration.AddSerilog();
        });

        return collection.BuildServiceProvider();
    }
}