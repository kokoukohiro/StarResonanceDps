using System.Diagnostics;

namespace StarResonanceDps.Core.CombatRuntime.Protocols;

public class Utils
{
    public static List<TcpHelper.TcpRow> GetTCPConnectionsForExe(string[] filenames)
    {
        List<TcpHelper.TcpRow> tcpConns = [];

        var procs = GetProcessesFromList(filenames);
        List<int> pids = [];
        foreach (var filename in filenames)
        {
            if (procs.TryGetValue(filename, out var process))
            {
                pids.Add(process.Id);
            }
        }

        var tcpConnections = TcpHelper.GetExtendedTcpTable();
        foreach (var conn in tcpConnections) {
            if (pids.Contains(conn.owningPid)) {
                tcpConns.Add(conn);
            }
        }

        return tcpConns;
    }

    public static Dictionary<string, Process> GetProcessesFromList(string[] filenames)
    {
        var processesDict = new Dictionary<string, Process>();
        var processes = Process.GetProcesses();
        foreach (var process in processes)
        {
            if (filenames.Contains(process.ProcessName))
            {
                processesDict.TryAdd(process.ProcessName, process);
            }
        }

        return processesDict;
    }
}
