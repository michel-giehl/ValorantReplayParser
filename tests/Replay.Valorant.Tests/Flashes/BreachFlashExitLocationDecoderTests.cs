using Replay.Encoding.Archives;
using Replay.Unreal.Parsing;
using Replay.Valorant.Flashes.Descriptors;

namespace Replay.Valorant.Tests.Flashes;

public class BreachFlashExitLocationDecoderTests
{
    // ExitResults captured from the six flashes in 5ffbb140-05bc-4244-8f80-d1b8ee0d4c8d (13.06).
    [TestCase("78B0BA633DCFE4827457380000F8FF0F00F84C2E48778503CE80C22E440D9ED585CDEE700060C401E0FFFFFF7FC6210000", 390, 1481, -8750, 450)]
    [TestCase("788C21243FCE38034A1C0F0000FFFF01009D710694381E38DF0BD1913C78661DE6BFE00180290780FFFFFFFF31870000", 384, 1649, -7600, 483)]
    [TestCase("786AECA73E4E77612794140200000001009DEEC24E282938CD0172715E70B20F6CE09800F0E600F0FFFFFF3FE7100000", 381, 750, -7877, 658)]
    [TestCase("78171E243FCE9683ECE4180000FFFF01009D2D07D9C93138410F5B146B7082190765AC0070E700F0FFFFFFBFE7100000", 381, 1837, -6300, 796)]
    [TestCase("787C6D573FCE25055D2D1C0000FFFF01009D4B0ABA5A3838AF1599157470A21D5C29BE00B0E800F0FFFFFFFFE8100000", 381, 2635, -5400, 901)]
    [TestCase("78F27FFC3ECEC29FB4CD230000FFFF01009D853F699B47389BFB1997AA70C604192DE900F0EA00F0FFFFFF3FEB100000", 381, -123, -4700, 1145)]
    public void Decode_RecordedExitResult_ReturnsWorldCentimetersAndConsumesHitResult(
        string hex, int bitCount, double x, double y, double z)
    {
        var bytes = Convert.FromHexString(hex);
        using var archive = new BitArchiveReader(bytes, bitCount);
        var context = new FieldDecodeContext();
        var decoder = new BreachFlashExitLocationDecoder();
        var location = decoder.Decode(ref context, archive).VectorValue;
        Assert.Multiple(() =>
        {
            Assert.That(location.X, Is.EqualTo(x));
            Assert.That(location.Y, Is.EqualTo(y));
            Assert.That(location.Z, Is.EqualTo(z));
            Assert.That(location.ScaleFactor, Is.EqualTo(1));
            Assert.That(archive.AtEnd, Is.True);
        });

        using var truncated = new BitArchiveReader(bytes, bitCount - 1);
        Assert.Throws<ArchiveReadException>(() => decoder.Decode(ref context, truncated));
    }
}
