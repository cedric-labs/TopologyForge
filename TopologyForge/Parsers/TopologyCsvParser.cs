using System.Globalization;
using System.Text;
using TopologyForge.Models;

namespace TopologyForge.Parsers;

/// <summary>
/// Analyseur de fichiers CSV d'audit de flux réseau prenant en charge les champs quotés RFC 4180 et l'ordre dynamique des colonnes.
/// </summary>
public sealed class TopologyCsvParser : ITopologyCsvParser
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<TopologyFlowRecord>> ParseFileAsync(
        string filePath,
        char delimiter = ',',
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            return [];
        }

        var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        return ParseLines(lines, delimiter);
    }

    /// <inheritdoc />
    public IReadOnlyList<TopologyFlowRecord> ParseLines(IEnumerable<string> lines, char delimiter = ',')
    {
        ArgumentNullException.ThrowIfNull(lines);

        using var enumerator = lines.GetEnumerator();
        string? headerLine = null;

        while (enumerator.MoveNext())
        {
            if (!string.IsNullOrWhiteSpace(enumerator.Current))
            {
                headerLine = enumerator.Current;
                break;
            }
        }

        if (headerLine is null)
        {
            return [];
        }

        var headers = SplitCsvLine(headerLine, delimiter);
        var columnMap = BuildColumnIndexMap(headers);
        var records = new List<TopologyFlowRecord>();

        while (enumerator.MoveNext())
        {
            var rawLine = enumerator.Current;
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var fields = SplitCsvLine(rawLine, delimiter);
            var record = ConvertFieldsToFlowRecord(fields, columnMap);
            if (record is not null)
            {
                records.Add(record);
            }
        }

        return records;
    }

    private static Dictionary<string, int> BuildColumnIndexMap(IReadOnlyList<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            var cleanHeader = headers[i].Trim();
            if (!string.IsNullOrEmpty(cleanHeader) && !map.ContainsKey(cleanHeader))
            {
                map[cleanHeader] = i;
            }
        }

        return map;
    }

    private static TopologyFlowRecord? ConvertFieldsToFlowRecord(
        IReadOnlyList<string> fields,
        Dictionary<string, int> columnMap)
    {
        var serverName = GetField(fields, columnMap, "ServerName");
        var serverPrimaryIp = GetField(fields, columnMap, "ServerPrimaryIp");
        var localIp = GetField(fields, columnMap, "LocalIp");
        var localPortStr = GetField(fields, columnMap, "LocalPort");
        var remoteIp = GetField(fields, columnMap, "RemoteIp");
        var remotePortStr = GetField(fields, columnMap, "RemotePort");
        var remoteHostName = GetField(fields, columnMap, "RemoteHostName");
        var protocolStr = GetField(fields, columnMap, "Protocol");
        var directionStr = GetField(fields, columnMap, "Direction");
        var processName = GetField(fields, columnMap, "ProcessName");
        var targetExecutable = GetField(fields, columnMap, "TargetExecutable");
        var observationCountStr = GetField(fields, columnMap, "ObservationCount");

        _ = int.TryParse(localPortStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var localPort);
        _ = int.TryParse(remotePortStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var remotePort);

        if (!long.TryParse(observationCountStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var observationCount) || observationCount <= 0)
        {
            observationCount = 1;
        }

        var protocol = Enum.TryParse<TopologyTransportProtocol>(protocolStr, ignoreCase: true, out var parsedProtocol)
            ? parsedProtocol
            : TopologyTransportProtocol.Tcp;

        var direction = Enum.TryParse<TopologyFlowDirection>(directionStr, ignoreCase: true, out var parsedDirection)
            ? parsedDirection
            : (remotePort == 0 ? TopologyFlowDirection.Listening : TopologyFlowDirection.Outbound);

        var effectiveSourceIp = !string.IsNullOrWhiteSpace(serverPrimaryIp) && serverPrimaryIp is not "0.0.0.0" and not "::"
            ? serverPrimaryIp
            : localIp;

        var effectiveServiceTarget = !string.IsNullOrWhiteSpace(targetExecutable)
            ? targetExecutable
            : processName;

        return new TopologyFlowRecord(
            SourceServerName: serverName,
            SourceIp: effectiveSourceIp,
            LocalPort: localPort,
            RemoteServerName: remoteHostName,
            RemoteIp: remoteIp,
            RemotePort: remotePort,
            Protocol: protocol,
            Direction: direction,
            ProcessName: processName,
            ServiceTarget: effectiveServiceTarget,
            ObservationCount: observationCount);
    }

    private static string GetField(IReadOnlyList<string> fields, Dictionary<string, int> columnMap, string columnName) =>
        columnMap.TryGetValue(columnName, out var index) && index >= 0 && index < fields.Count
            ? fields[index]
            : string.Empty;

    private static List<string> SplitCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == delimiter)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        result.Add(current.ToString());
        return result;
    }
}
