using GestionCarp.Core.Common;

namespace GestionCarp.Core.Tests;

public class NaturalStringComparerTests
{
    [Fact]
    public void SortsNumbersByValue()
    {
        var names = new[] { "cap10.mkv", "cap2.mkv", "cap1.mkv", "Cap3.mkv", "cap01b.mkv" };
        var sorted = names.OrderBy(n => n, NaturalStringComparer.Instance).ToArray();
        Assert.Equal(new[] { "cap1.mkv", "cap01b.mkv", "cap2.mkv", "Cap3.mkv", "cap10.mkv" }, sorted);
    }

    [Theory]
    [InlineData("a2", "a10", -1)]
    [InlineData("a10", "a2", 1)]
    [InlineData("A1", "a1", -1)]
    [InlineData("x", "x1", -1)]
    public void ComparesPairs(string a, string b, int expectedSign)
        => Assert.Equal(expectedSign, Math.Sign(NaturalStringComparer.Instance.Compare(a, b)));
}
