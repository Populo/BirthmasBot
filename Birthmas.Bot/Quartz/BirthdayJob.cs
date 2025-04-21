using Birthmas.Service;
using Discord;
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
            if (roleHavers.Any())
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
            if (todaysBirthdays.Any())
            {
                await Parallel.ForEachAsync(todaysBirthdays, async (birthday, cancel) =>
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
    }
}