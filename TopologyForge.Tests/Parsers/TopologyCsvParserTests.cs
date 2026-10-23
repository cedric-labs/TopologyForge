using FluentAssertions;
using TopologyForge.Models;
using TopologyForge.Parsers;

namespace TopologyForge.Tests.Parsers;

public sealed class TopologyCsvParserTests
{
    [Fact]
    public void ParseLines_WhenValidNetFlowAuditCsv_ReturnsExpectedFlowRecords()
    {
        // Arrange
        var parser = new TopologyCsvParser();
        var lines = new[]
        {
            "FirstSeenUtc,LastSeenUtc,ObservationCount,ServerName,ServerPrimaryIp,Protocol,Direction,LocalIp,LocalPort,RemoteIp,RemotePort,RemoteHostName,ProcessName,TargetExecutable",
            "2026-10-23T10:00:00Z,2026-10-23T10:05:00Z,12,SRV-APP-01,10.0.1.5,Tcp,Listening,0.0.0.0,443,0.0.0.0,0,,kestrel,MyWeb.dll",
            "2026-10-23T10:00:00Z,2026-10-23T10:05:00Z,8,SRV-APP-01,10.0.1.5,Tcp,Outbound,10.0.1.5,54122,10.0.2.10,5432,db-postgres.internal,kestrel,MyWeb.dll"
        };

        // Act
        var records = parser.ParseLines(lines);

        // Assert
        records.Should().HaveCount(2);

        records[0].SourceServerName.Should().Be("SRV-APP-01");
        records[0].SourceIp.Should().Be("10.0.1.5");
        records[0].LocalPort.Should().Be(443);
        records[0].Direction.Should().Be(TopologyFlowDirection.Listening);
        records[0].ProcessName.Should().Be("kestrel");
        records[0].ServiceTarget.Should().Be("MyWeb.dll");

        records[1].RemoteServerName.Should().Be("db-postgres.internal");
        records[1].RemoteIp.Should().Be("10.0.2.10");
        records[1].RemotePort.Should().Be(5432);
        records[1].Direction.Should().Be(TopologyFlowDirection.Outbound);
        records[1].ObservationCount.Should().Be(8);
    }

    [Fact]
    public void ParseLines_WhenQuotedFieldsContainDelimiters_ParsesAccurately()
    {
        // Arrange
        var parser = new TopologyCsvParser();
        var lines = new[]
        {
            "ServerName,ServerPrimaryIp,Protocol,Direction,LocalIp,LocalPort,RemoteIp,RemotePort,RemoteHostName,ProcessName,TargetExecutable,ObservationCount",
            "\"SRV,01\",10.0.0.1,Udp,Outbound,10.0.0.1,55000,10.0.0.2,53,dns.local,\"proc\"\"name\",\"target,app\",3"
        };

        // Act
        var records = parser.ParseLines(lines);

        // Assert
        records.Should().ContainSingle();
        records[0].SourceServerName.Should().Be("SRV,01");
        records[0].Protocol.Should().Be(TopologyTransportProtocol.Udp);
        records[0].ProcessName.Should().Be("proc\"name");
        records[0].ServiceTarget.Should().Be("target,app");
    }

    [Fact]
    public void ParseLines_WhenEmptyOrHeaderOnly_ReturnsEmptyCollection()
    {
        // Arrange
        var parser = new TopologyCsvParser();

        // Act & Assert
        parser.ParseLines([]).Should().BeEmpty();
        parser.ParseLines(["ServerName,LocalPort"]).Should().BeEmpty();
    }

    [Fact]
    public async Task ParseFileAsync_WhenFileExists_ReadsAndParsesAllRecords()
    {
        // Arrange
        var parser = new TopologyCsvParser();
        var tempFile = Path.Combine(Path.GetTempPath(), $"topology_test_{Guid.NewGuid():N}.csv");
        try
        {
            await File.WriteAllLinesAsync(tempFile,
            [
                "ServerName,ServerPrimaryIp,Protocol,Direction,LocalIp,LocalPort,RemoteIp,RemotePort,RemoteHostName,ProcessName",
                "SRV-01,192.168.1.10,Tcp,Outbound,192.168.1.10,51234,192.168.1.20,3389,SRV-RDP,mstsc"
            ]);

            // Act
            var records = await parser.ParseFileAsync(tempFile);

            // Assert
            records.Should().ContainSingle();
            records[0].RemoteServerName.Should().Be("SRV-RDP");
            records[0].RemotePort.Should().Be(3389);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
