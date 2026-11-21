using EcoKoin.Models;

namespace EcoKoin.Core.Tests.Models;

public class EcoPetTests
{
    [Theory]
    [InlineData(0, PetState.Lemas)]
    [InlineData(9, PetState.Lemas)]
    [InlineData(10, PetState.Normal)]
    [InlineData(29, PetState.Normal)]
    [InlineData(30, PetState.Segar)]
    [InlineData(500, PetState.Segar)]
    [InlineData(-5, PetState.Lemas)]
    public void UpdateState_FollowsThresholds(int koin, PetState expected)
    {
        var pet = new EcoPet("p1", "u1");
        pet.UpdateState(koin);
        Assert.Equal(expected, pet.CurrentState);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(10, 30)]
    [InlineData(33, 99)]
    [InlineData(34, 100)]
    [InlineData(10_000, 100)]
    [InlineData(int.MaxValue, 100)]
    public void UpdateState_HappinessIsClampedTo0To100(int koin, int expected)
    {
        var pet = new EcoPet("p1", "u1", 50, PetState.Normal);
        pet.UpdateState(koin);
        Assert.Equal(expected, pet.HappinessLevel);
    }

    [Theory]
    [InlineData(PetState.Segar, "happy.png")]
    [InlineData(PetState.Normal, "netral.png")]
    [InlineData(PetState.Lemas, "sad.png")]
    public void Animate_ReturnsImageForState(PetState state, string image)
    {
        var pet = new EcoPet("p1", "u1", 0, state);
        Assert.Equal(image, pet.Animate());
    }

    [Fact]
    public void Animate_FollowsUpdateState()
    {
        var pet = new EcoPet("p1", "u1");
        Assert.Equal("sad.png", pet.Animate());
        pet.UpdateState(12);
        Assert.Equal("netral.png", pet.Animate());
        pet.UpdateState(40);
        Assert.Equal("happy.png", pet.Animate());
    }

    [Fact]
    public void Constructor_DefaultsToLemasWithZeroHappiness()
    {
        var pet = new EcoPet("p1", "u1");
        Assert.Equal(PetState.Lemas, pet.CurrentState);
        Assert.Equal(0, pet.HappinessLevel);
        Assert.Equal("u1", pet.UserId);
    }
}
