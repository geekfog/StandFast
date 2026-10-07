using StandFast.Domain.Common;

namespace StandFast.Application.Tests.Domain;

public sealed class OptionalTextTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" \n ", null)]
    [InlineData("  done\n\n", "done")]
    [InlineData("1. one\n   - [ ] task", "1. one\n   - [ ] task")]
    public void Normalize_TrimsAndTreatsBlankAsAbsent(string? value, string? expected) => Assert.Equal(expected, OptionalText.Normalize(value));

    [Theory]
    [InlineData("done\n", "done", true)] // Typed text ending in a new line matches what saving it stored.
    [InlineData("", null, true)]
    [InlineData("done ", "done", true)]
    [InlineData("done", "done.", false)]
    [InlineData("done", null, false)]
    public void Matches_ComparesStoredForms(string? typed, string? stored, bool expected) => Assert.Equal(expected, OptionalText.Matches(typed, stored));
}
