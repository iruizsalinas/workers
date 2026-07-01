using Xunit;

namespace Workers.Tests;

public sealed class R2ChecksumsTests
{
    [Fact]
    public void ConstructorCopiesChecksumBytes()
    {
        var sha256 = new byte[] { 1, 2, 3 };

        var checksums = new R2Checksums(
            Md5: null,
            Sha1: null,
            Sha256: sha256,
            Sha384: null,
            Sha512: null);
        sha256[0] = 9;

        Assert.Equal([1, 2, 3], checksums.Sha256);
    }

    [Fact]
    public void ChecksumPropertiesReturnSnapshots()
    {
        var checksums = new R2Checksums(
            Md5: [1, 2, 3],
            Sha1: null,
            Sha256: null,
            Sha384: null,
            Sha512: null);

        var md5 = checksums.Md5;
        Assert.NotNull(md5);
        md5[0] = 9;

        Assert.Equal([1, 2, 3], checksums.Md5);
    }
}
