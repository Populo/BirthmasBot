using Birthmas.Data;

namespace Birthmas.Service;

public interface IBirthmasService
{
    public List<Person> GetBirthdays(DateTime date);
    public Person? GetBirthday(ulong userId);
    public Task<List<ServerConfig>> GetServersByUserAsync(ulong userId);
    public Task GiveUserRoleAsync(ulong userId, ulong serverId, ulong roleId);
    public Task RemoveBirthdayRoleFromUserAsync(ulong guildId, ulong userId, ulong roleId);
    public Person AddBirthday(ulong userId, DateTime date);
    public ServerConfig ConfigServer(ulong serverId, bool giveRole, ulong roleId, ulong channelId);
    public Person? RemoveBirthday(ulong userId);
    public ServerConfig? RemoveServer(ulong serverId);
    public ServerConfig? GetServer(ulong serverId);
    public void AddBirthmas(Person person, ServerConfig config);
    public List<Data.Birthmas> GetBirthmasesForServer(ulong serverId);
}