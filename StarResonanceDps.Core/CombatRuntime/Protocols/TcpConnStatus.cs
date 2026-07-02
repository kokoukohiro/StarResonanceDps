namespace StarResonanceDps.Core.CombatRuntime.Protocols;

public class TcpConnStatus
{
    public bool IsServerSyncedUp { get; set; } = false;
    public bool IsClientSyncedUp { get; set; } = false;
}
