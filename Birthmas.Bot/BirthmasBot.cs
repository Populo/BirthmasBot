using Birthmas.Bot.Quartz;
using Birthmas.Data;
using Birthmas.Service;
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
    
    private readonly ILogger<BirthmasBot> _logger;
    private IScheduler _scheduler;
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
        
        var token = await File.ReadAllTextAsync("/run/secrets/botToken");
        token = token.Trim();
        switch (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"))
        {
            case "Release":
                _logger.LogInformation("Environment: Prod");
                break;
            case "Develop":
                _logger.LogInformation("Environment: Dev");
                break;
        }

        using (var db = new BirthmasContext())
        {
            var servers = db.ServerConfigs.Count();
            _logger.LogInformation($"Server count: {servers}");
        }
        
        Client.Ready += new Commands(BirthmasService, Client.Rest, _logger).InitCommands;
        Client.Log += ClientOnLog;
        Client.SlashCommandExecuted += ClientOnSlashCommandExecuted;
        
        await Client.LoginAsync(TokenType.Bot, token);
        await Client.StartAsync();

        RestClient = Client.Rest;
        
        _logger.LogInformation("Started");
        _logger.LogInformation("Registering job");
        
        _scheduler = await StdSchedulerFactory.GetDefaultScheduler();
        _scheduler.JobFactory = new MicrosoftDependencyInjectionJobFactory(Services, new OptionsWrapper<QuartzOptions>(null));
        await _scheduler.Start();
        var job = JobBuilder.Create<BirthdayJob>()
            .WithIdentity("BirthmasCheckJob", "Birthmas")
            .Build();
        var trigger = TriggerBuilder.Create()
            .WithIdentity("BirthmasCheckTrigger", "Birthmas")
            .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(2, 0))
            //.WithSimpleSchedule(x => x.WithIntervalInSeconds(60)) // 1 minute (testing)
            .Build();
        await _scheduler.ScheduleJob(job, trigger);
        Console.WriteLine($"Running in: {trigger.GetNextFireTimeUtc().Value.ToLocalTime()}");
        
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