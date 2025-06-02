using Birthmas.Service;
using Discord.WebSocket;
using Quartz;

namespace Birthmas.Bot.Quartz;

public class StatusJob(DiscordSocketClient client,  IBirthmasService birthmasService)
    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        // dont change if it is someones birthday
        if (birthmasService.GetBirthdays(DateTime.Today).Count != 0) return;
        
        string[] statuses =
        [
            $"v{birthmasService.GetBotVersion()} | {DateTime.Today:d}",
            "/set-birthday to set your birthday",
            "nextbirthday"
        ];
        
        var newStatus = statuses[new Random().Next(0, statuses.Length)];

        if (newStatus == "nextbirthday")
        {
            var nextBirthday = birthmasService.GetNextBirthday();
            newStatus = $"Next: {nextBirthday[0]} on {nextBirthday[1]}";
        }
        
        await client.SetCustomStatusAsync(newStatus);
    }
}