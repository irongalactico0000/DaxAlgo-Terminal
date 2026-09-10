using FluentAssertions;
using TradingTerminal.Execution.Alpaca;
using TradingTerminal.Execution.Oms;
using Xunit;

namespace TradingTerminal.Tests.Headless.LiveExecution;

public sealed class LiveExecutionAuthorizationGateTests
{
    private static readonly DateTime TimestampUtc = new(2026, 3, 5, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Live_endpoint_is_refused_when_AllowLiveExecution_is_false()
    {
        var store = new InMemoryLiveExecutionConfirmationStore();
        store.Save(Confirmation());

        var act = () => LiveExecutionAuthorizationGate.Require(
            allowLiveExecution: false,
            hasRequiredCredentials: true,
            AlpacaExecutionOptions.BrokerId,
            "PA-LIVE",
            store);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AllowLiveExecution*");
    }

    [Fact]
    public void Live_endpoint_is_refused_without_persisted_LIVE_confirmation()
    {
        var act = () => LiveExecutionAuthorizationGate.Require(
            allowLiveExecution: true,
            hasRequiredCredentials: true,
            AlpacaExecutionOptions.BrokerId,
            "PA-LIVE",
            new InMemoryLiveExecutionConfirmationStore());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*LIVE confirmation*");
    }

    [Fact]
    public void Live_endpoint_is_refused_without_credentials()
    {
        var store = new InMemoryLiveExecutionConfirmationStore();
        store.Save(Confirmation());

        var act = () => LiveExecutionAuthorizationGate.Require(
            allowLiveExecution: true,
            hasRequiredCredentials: false,
            AlpacaExecutionOptions.BrokerId,
            "PA-LIVE",
            store);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*credentials*");
    }

    [Fact]
    public void Live_endpoint_passes_when_all_gates_are_exact()
    {
        var store = new InMemoryLiveExecutionConfirmationStore();
        var confirmation = Confirmation();
        store.Save(confirmation);

        var authorized = LiveExecutionAuthorizationGate.Require(
            allowLiveExecution: true,
            hasRequiredCredentials: true,
            AlpacaExecutionOptions.BrokerId,
            "PA-LIVE",
            store);

        authorized.Should().Be(confirmation);
    }

    [Fact]
    public void No_Binance_execution_adapter_type_exists_in_the_live_kernel()
    {
        var orderAdapters = typeof(AlpacaExecutionAdapter).Assembly.GetTypes()
            .Where(type => typeof(IBrokerExecutionAdapter).IsAssignableFrom(type) &&
                           type is { IsClass: true, IsAbstract: false })
            .Select(type => type.Name)
            .ToArray();

        orderAdapters.Should().Contain(["AlpacaExecutionAdapter", "CTraderExecutionAdapter", "InteractiveBrokersExecutionAdapter"]);
        orderAdapters.Should().NotContain(name => name.Contains("Binance", StringComparison.OrdinalIgnoreCase));
    }

    private static LiveExecutionConfirmation Confirmation() => new(
        AlpacaExecutionOptions.BrokerId,
        "PA-LIVE",
        LiveExecutionConfirmation.RequiredAcknowledgement,
        TimestampUtc,
        "headless-test");
}
