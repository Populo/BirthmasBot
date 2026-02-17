namespace Birthmas.Service.Models;

public sealed class BotConfig
{
    public ulong ServerId { get; init; }
    public ulong AnnouncementChannelId { get; init; }
    public ulong ErrorChannelId { get; init; }
    public ulong BirthdayRoleId { get; init; }
}