using MasterclassQuiz.Api.HostAuth;

namespace MasterclassQuiz.Tests;

public sealed class HostPasswordTests
{
    [Fact]
    public void A_hash_accepts_its_own_password_and_refuses_any_other()
    {
        var hash = HostPassword.Hash("correct horse");

        Assert.True(HostPassword.Verify("correct horse", hash));
        Assert.False(HostPassword.Verify("Correct horse", hash));
        Assert.False(HostPassword.Verify("", hash));
    }

    [Fact]
    public void The_same_password_hashes_differently_each_time_and_never_contains_the_password()
    {
        var first = HostPassword.Hash("correct horse");
        var second = HostPassword.Hash("correct horse");

        Assert.NotEqual(first, second);
        Assert.DoesNotContain("correct horse", first);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("correct horse")]
    [InlineData("pbkdf2-sha256.600000.not-base64.not-base64")]
    [InlineData("md5.1.AAAA.AAAA")]
    [InlineData("pbkdf2-sha256.0.AAAA.AAAA")]
    public void A_missing_or_malformed_hash_refuses_every_password(string? hash)
    {
        Assert.False(HostPassword.Verify("correct horse", hash));
    }
}
