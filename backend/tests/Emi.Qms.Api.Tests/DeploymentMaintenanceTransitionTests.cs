using Emi.Qms.Api.DeploymentMaintenance;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class DeploymentMaintenanceTransitionTests
{
    [Theory]
    [InlineData("Announced",false)]
    [InlineData("Active",false)]
    [InlineData("Delayed",false)]
    [InlineData("Completed",false)]
    [InlineData("Failed",true)]
    public void FailClosesEveryPreparedReleaseStateAndIsIdempotent(string currentState,bool isNoOp)
    {
        var decision=DeploymentMaintenanceStore.ResolveTransition(
            currentState,"fail",hasRevisedEnd:false,verified:false);

        Assert.NotNull(decision);
        Assert.Equal("Failed",decision.Value.NextState);
        Assert.Equal(isNoOp,decision.Value.IsNoOp);
    }

    [Theory]
    [InlineData("Idle")]
    [InlineData("Unknown")]
    public void FailRejectsStatesThatDoNotBelongToARelease(string currentState)
    {
        Assert.Null(DeploymentMaintenanceStore.ResolveTransition(
            currentState,"fail",hasRevisedEnd:false,verified:false));
    }
}
