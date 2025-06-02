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
            // remove people who are no longer in any servers
            await BirthmasService.DownloadUsers();
            var outcasts = BirthmasService.PurgeTheOutcasts();
            if (outcasts.Count != 0)
            {
                _logger.LogInformation($"Purged {outcasts.Count} outcasts.");
            }
            
            var roleHavers = BirthmasService.GetPeopleWithBirthdayRole();
            if (roleHavers.Count != 0)
            {
                await Parallel.ForEachAsync(roleHavers, async (person, _) =>
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
            if (todaysBirthdays.Count != 0)
            {
                List<string> birthdays = new();
                await Parallel.ForEachAsync(todaysBirthdays, async (birthday, cancel) =>
                {
                    try
                    {
                        await BirthmasService.PostBirthdayAnnouncementAsync(birthday);
                        birthdays.Add(BirthmasService.GetUserDisplayName(birthday));
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