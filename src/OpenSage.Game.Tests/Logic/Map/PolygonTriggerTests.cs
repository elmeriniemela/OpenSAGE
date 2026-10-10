using System.IO;
using System.Text;
using OpenSage.FileFormats;
using OpenSage.Logic.Map;
using Xunit;

namespace OpenSage.Tests.Logic.Map;

public sealed class PolygonTriggerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void IgnoringDegenerateTriggerConsumesAllItsPoints(int pointCount)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        WriteTrigger("Ignored", pointCount);
        WriteTrigger("Valid", 3);
        stream.Position = 0;
        using var reader = new BinaryReader(stream);

        Assert.Null(PolygonTrigger.Parse(reader, 3));
        var valid = PolygonTrigger.Parse(reader, 3);
        Assert.NotNull(valid);
        Assert.Equal("Valid", valid.Name);
        Assert.Equal(3, valid.Points.Length);
        Assert.Equal(stream.Length, stream.Position);

        void WriteTrigger(string name, int points)
        {
            writer.WriteUInt16PrefixedAsciiString(name);
            writer.Write(1u); // ID
            writer.Write(false); // Water
            writer.Write(false); // River
            writer.Write(0u); // River start
            writer.Write((uint)points);
            for (var i = 0; i < points; i++)
            {
                writer.Write(i);
                writer.Write(i + 10);
                writer.Write(0);
            }
        }
    }
}
