using Birthmas.Bot.Helpers;
using Birthmas.Service;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Birthmas.Bot.Quartz;

public class BirthdayJob(DiscordSocketClient Client,  IBirthmasService BirthmasService, ILogger<BirthdayJob> Logger) : IJob
{
    private ILogger<BirthdayJob> _logger = Logger;
    
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var roleHavers = await BirthmasService.GetPeopleWithBirthdayRole();
            if (roleHavers.Count != 0)
            {
                await Parallel.ForEachAsync(roleHavers, async (person, _) =>
                {
                    try
                    {
                        await BirthmasService.RemoveBirthdayRoleFromUserAsync(person.Id);
                        _logger.LogInformation($"Removed role from {person.Nickname}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error while removing old role from {person.Username}");
                    }
                });
            }
            // add today's birthday roles
            var todaysBirthdays = BirthmasService.GetBirthdaysForDate(DateOnly.FromDateTime(DateTime.Today));
            if (todaysBirthdays.Count != 0)
            {
                List<string> birthdays = new();
                await Parallel.ForEachAsync(todaysBirthdays, async (birthday, cancel) =>
                {
                    try
                    {
                        await BirthmasService.PostBirthdayAnnouncementAsync(birthday);
                        birthdays.Add(await BirthmasService.GetUserDisplayName(birthday));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occured while adding roles");
                    }
                });
                await Client.SetCustomStatusAsync($"Happy Birthday {string.Join(", ", birthdays)}!");
            }
            else
            {
                await Client.SetCustomStatusAsync($"v{BirthmasService.GetBotVersion()} | {DateTime.Today:d}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occured while running.");
        }
    }
}