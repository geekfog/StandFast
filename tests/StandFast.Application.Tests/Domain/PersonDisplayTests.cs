using StandFast.Application.Dtos;
using StandFast.Application.Mapping;
using StandFast.Domain.Entities;

namespace StandFast.Application.Tests.Domain;

public sealed class PersonDisplayTests
{
    [Fact]
    public void DisplayName_UsesFirstAndLastNameWhenNoOverrideIsSet()
    {
        Person person = new() { FirstName = "Ada", LastName = "Lovelace" };

        Assert.Equal("Ada Lovelace", person.DisplayName);
        Assert.Equal("Ada Lovelace", person.FullName);
        Assert.Equal("AL", person.Initials);
    }

    [Fact]
    public void DisplayName_PrefersTheOverrideAndInitialsFollowIt()
    {
        Person person = new() { FirstName = "Augusta", LastName = "King", DisplayAs = "Ada Lovelace" };

        Assert.Equal("Ada Lovelace", person.DisplayName);
        Assert.Equal("Augusta King", person.FullName);
        Assert.Equal("AL", person.Initials);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DisplayName_IgnoresABlankOverride(string displayAs)
    {
        Person person = new() { FirstName = "Grace", LastName = "Hopper", DisplayAs = displayAs };

        Assert.Equal("Grace Hopper", person.DisplayName);
    }

    [Fact]
    public void DisplayName_HandlesASingleWordOverride()
    {
        Person person = new() { FirstName = "Grace", LastName = "Hopper", DisplayAs = "Amazing" };

        Assert.Equal("Amazing", person.DisplayName);
        Assert.Equal("A", person.Initials);
    }

    [Fact]
    public void ApplyTo_StoresBlankOverrideAndNotesAsNull()
    {
        Person person = new();
        PersonEditDto edit = new() { FirstName = " Ada ", LastName = " Lovelace ", Email = " Ada.Lovelace@Example.COM ", DisplayAs = "   ", Notes = "  " };

        edit.ApplyTo(person);

        Assert.Equal("Ada", person.FirstName);
        Assert.Equal("ada.lovelace@example.com", person.Email);
        Assert.Null(person.DisplayAs);
        Assert.Null(person.Notes);
    }

    [Fact]
    public void ToDto_FlagsAnOverriddenNameAndNotes()
    {
        Person person = new() { FirstName = "Augusta", LastName = "King", DisplayAs = "Ada", Notes = "**Works Tuesdays.**" };

        PersonDto dto = person.ToDto();

        Assert.True(dto.HasDisplayOverride);
        Assert.True(dto.HasNotes);
        Assert.Equal("Ada", dto.DisplayName);
        Assert.Equal("Augusta King", dto.FullName);
    }
}
