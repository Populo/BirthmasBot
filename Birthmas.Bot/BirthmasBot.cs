using System.Text;
using Birthmas.Bot.Helpers;
using Birthmas.Bot.Quartz;
using Birthmas.Data;
using Birthmas.Service;
using Birthmas.Service.Models;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Quartz;
using Quartz.Impl;
using Quartz.Simpl;
using Serilog;

namespace Birthmas.Bot;

public class BirthmasBot
{
    public static Task Main(string[] args) => new BirthmasBot().Run(args);
    
    private ILogger<BirthmasBot> _logger;
    private IScheduler _scheduler;
    private ITrigger _birthTrigger;
    
    private DiscordSocketClient Client { get; set; }
    private IBirthmasService BirthmasService { get; set; }
    private IServiceProvider Services { get; set; }
    private BirthmasContext Db { get; set; }

    private async Task Run(string[] args)
    {
        Services = CreateProvider();

        await Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        var config = Services.GetRequiredService<BotConfig>();
        
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var channelId = config.ErrorChannelId;
            _logger.LogError($"Unhandled exception occured: {e.ExceptionObject}");
            (Client.GetChannelAsync(channelId).Result as SocketTextChannel
                ?? throw new Exception("Cannot get error channel from discord")).SendMessageAsync($"{e.ExceptionObject}");
        };
        
        Client = Services.GetRequiredService<DiscordSocketClient>()
                 ?? throw new Exception("Cannot get client from factory");
        BirthmasService = Services.GetRequiredService<IBirthmasService>()
                          ?? throw new Exception("Cannot get service from factory");
        Db = Services.GetRequiredService<BirthmasContext>()
             ?? throw new Exception("Cannot get db from factory");
        _logger = Services.GetRequiredService<ILogger<BirthmasBot>>()
                  ?? throw new Exception("Cannot get logger from factory");
        
        var token = Environment.GetEnvironmentVariable("BotToken")!.Trim();
        token = token.Trim();
        
        Client.Ready += ClientOnReady;
        Client.Log += ClientOnLog;
        Client.SlashCommandExecuted += ClientOnSlashCommandExecuted;
        
        await Client.LoginAsync(TokenType.Bot, token);
        await Client.StartAsync();
        
        _logger.LogInformation("Started");
        
        await Task.Delay(-1);
    }

    private async Task ClientOnReady()
    {
        await new Commands(BirthmasService, Client, _logger, Db).InitCommands();

        await Client.SetCustomStatusAsync($"v{BirthmasService.GetBotVersion()}");
        
        _logger.LogInformation("Registering job");
        
        _scheduler = await StdSchedulerFactory.GetDefaultScheduler();
        _scheduler.JobFactory = new MicrosoftDependencyInjectionJobFactory(Services, new OptionsWrapper<QuartzOptions>(null));
        await _scheduler.Start();
        var birthJob = JobBuilder.Create<BirthdayJob>()
            .WithIdentity("BirthmasCheckJob", "Birthmas")
            .Build();
        _birthTrigger = TriggerBuilder.Create()
            .WithIdentity("BirthmasCheckTrigger", "Birthmas")
            .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(2, 0))
            //.WithSimpleSchedule(x => x.WithIntervalInSeconds(60)) // 1 minute (testing)
            .Build();

        var statusJob = JobBuilder.Create<StatusJob>()
            .WithIdentity("StatusJob", "Birthmas")
            .Build();
        var statusTrigger = TriggerBuilder.Create()
            .WithIdentity("StatusTrigger", "Birthmas")
            .WithSimpleSchedule(x =>
            {
                x.WithIntervalInMinutes(30);
                //x.WithIntervalInMinutes(1); // testing
                x.RepeatForever();
            })
            .Build();
        
        await _scheduler.ScheduleJob(birthJob, _birthTrigger);
        await _scheduler.ScheduleJob(statusJob, statusTrigger);
    }

    private async Task ClientOnSlashCommandExecuted(SocketSlashCommand arg)
    {
        var cArgs = new StringBuilder();
        foreach (var o in arg.Data.Options)
        {
            cArgs.AppendLine($"{o.Name}: {o.Value}");
        }

        _logger.LogInformation(
            "Command received: {ArgCommandName}\nin channel: {GetChannelAsync}\nin server: {Name}\nfrom: {UserUsername}\nargs:{JsonArgs}", arg.CommandName, await Client.GetChannelAsync(arg.ChannelId!.Value), Client.GetGuild(arg.GuildId!.Value).Name, arg.User.Username, cArgs);

        var commands = new Commands(BirthmasService, Client, _logger, Db);
        Task f = arg.CommandName switch
        {
            "set-birthday" => commands.SetBirthdayAsync(arg),
            "remove-birthday" => commands.RemoveBirthdayAsync(arg),
            "server-birthdays" => commands.GetServerBirthdays(arg),
            "my-birthday" => commands.MyBirthdayAsync(arg),
            "announce-birthday" => commands.AnnounceBirthdayAsync(arg),
            "force" => commands.ForceBirthdayCheckAsync(arg, _scheduler, _birthTrigger),
            _ => Task.CompletedTask
        };
    }

    private Task ClientOnLog(LogMessage arg)
    {
        _logger.LogInformation(arg.Message);
        if (null == arg.Exception) return Task.CompletedTask;
        
        _logger.LogError(arg.Exception.ToString());
        _logger.LogError(arg.Exception.InnerException?.StackTrace);

        return Task.CompletedTask;
    }
    
    private IServiceProvider CreateProvider()
    {
        var config = new DiscordSocketConfig()
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers,
            AlwaysDownloadUsers = true
        };

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("./logs/log.txt", 
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var collection = new ServiceCollection();
        
        collection.AddSingleton(new BotConfig
        {
            ServerId = ulong.Parse(Environment.GetEnvironmentVariable("DiscordServerId") ?? "0"),
            AnnouncementChannelId = ulong.Parse(Environment.GetEnvironmentVariable("AnnouncementChannelId") ?? "0"),
            ErrorChannelId = ulong.Parse(Environment.GetEnvironmentVariable("ErrorChannelId") ?? "0"),
            BirthdayRoleId = ulong.Parse(Environment.GetEnvironmentVariable("BirthdayRoleId") ?? "0"),
        });
        collection.AddDbContext<BirthmasContext>();
        
        collection
            .AddSingleton(config)
            .AddSingleton<DiscordSocketClient>();

        collection.AddScoped<IBirthmasService, BirthmasService>();
        
        collection
            .AddSingleton<DatabaseInitializer>();

        collection.AddLogging(configuration =>
        {
            configuration.ClearProviders();
            configuration.AddSerilog();
        });
        
        return collection.BuildServiceProvider();
    }
}