using StandFast.Domain.Common;

namespace StandFast.Application.Tests.Domain;

public sealed class SupervisorChainTests
{
    private static readonly Guid Ada = Guid.CreateVersion7();
    private static readonly Guid Ben = Guid.CreateVersion7();
    private static readonly Guid Cy = Guid.CreateVersion7();
    private static readonly Guid Dee = Guid.CreateVersion7();

    [Fact]
    public void AChainEndingAtSomeoneWithNoSupervisor_HasNoLoop()
    {
        Dictionary<Guid, Guid?> supervisorOf = new() { [Ben] = Cy, [Cy] = null };

        Assert.Empty(SupervisorChain.FindLoop(Ada, Ben, supervisorOf));
    }

    [Fact]
    public void ASupervisorMissingFromPeople_EndsTheChain()
    {
        Assert.Empty(SupervisorChain.FindLoop(Ada, Ben, new Dictionary<Guid, Guid?>()));
    }

    [Fact]
    public void BeingYourOwnSupervisor_IsALoop()
    {
        Assert.Equal([Ada, Ada], SupervisorChain.FindLoop(Ada, Ada, new Dictionary<Guid, Guid?>()));
    }

    [Fact]
    public void AChainLeadingBackToThePerson_IsTheLoopInReportingOrder()
    {
        Dictionary<Guid, Guid?> supervisorOf = new() { [Ben] = Cy, [Cy] = Ada };

        Assert.Equal([Ada, Ben, Cy, Ada], SupervisorChain.FindLoop(Ada, Ben, supervisorOf));
    }

    [Fact]
    public void ALoopHigherUpTheChain_IsFoundWithoutThePerson()
    {
        Dictionary<Guid, Guid?> supervisorOf = new() { [Ben] = Cy, [Cy] = Dee, [Dee] = Cy };

        Assert.Equal([Cy, Dee, Cy], SupervisorChain.FindLoop(Ada, Ben, supervisorOf));
    }
}
