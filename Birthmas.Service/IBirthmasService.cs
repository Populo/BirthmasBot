﻿using Birthmas.Data;
using Discord.WebSocket;

namespace Birthmas.Service;

public interface IBirthmasService
{
    public string GetBotVersion();
    public List<Person> GetBirthdays(DateTime date);
    public Person? GetBirthday(ulong userId);
    public Task<List<ServerConfig>> GetServersByUserAsync(ulong userId);
    public Task GiveUserRoleAsync(ulong userId, ulong serverId, ulong roleId);
    public Task RemoveBirthdayRoleFromUserAsync(ulong guildId, ulong userId, ulong roleId);
    public Person AddBirthday(ulong userId, DateTime date, ulong serverId);
    public ServerConfig ConfigServer(ulong serverId, bool giveRole, ulong roleId, ulong channelId);
    public Person? RemoveBirthday(ulong userId, ulong serverId);
    public ServerConfig? RemoveServer(ulong serverId);
    public ServerConfig? GetServer(ulong serverId);
    public List<SocketGuildUser> GetPeopleWithBirthdayRole();
    public List<Person> PurgeTheOutcasts();
    public Task DownloadUsers();
    public Task PostBirthdayAnnouncementAsync(Person person);
    public Task PostBirthdayAnnouncementAsync(ulong userId, ulong serverId);
}