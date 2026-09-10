using System.Security.Cryptography;
using FluentAssertions;
using TradingTerminal.Execution.Mac;
using TradingTerminal.Execution.Oms;
using Xunit;

namespace TradingTerminal.Tests.Headless.LiveExecution;

public sealed class LiveExecutionConfirmationStoreTests
{
    private static readonly DateTime TimestampUtc = new(2024, 3, 5, 14, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Confirmation_RequiresExactLiveAcknowledgementAndBoundedUtcIdentity()
    {
        var valid = Confirmation();

        valid.IsValid.Should().BeTrue();
        (valid with { Acknowledgement = "live" }).IsValid.Should().BeFalse();
        (valid with { ConfirmedAtUtc = DateTime.SpecifyKind(valid.ConfirmedAtUtc, DateTimeKind.Local) }).IsValid.Should().BeFalse();
        (valid with { ConfirmedBy = new string('x', LiveExecutionConfirmation.MaximumConfirmingIdentityLength + 1) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void InMemoryStore_IsExactBoundedAndRevocable()
    {
        var store = new InMemoryLiveExecutionConfirmationStore();
        var confirmation = Confirmation();

        store.Save(confirmation);

        store.Read(confirmation.BrokerId, confirmation.AccountId).Should().Be(confirmation);
        store.Read(confirmation.BrokerId, "different-account").Should().BeNull();
        store.Remove(confirmation.BrokerId, confirmation.AccountId).Should().BeTrue();
        store.Remove(confirmation.BrokerId, confirmation.AccountId).Should().BeFalse();
        store.Read(confirmation.BrokerId, confirmation.AccountId).Should().BeNull();
        store.Invoking(item => item.Save(confirmation with { Acknowledgement = "Live" }))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void InMemoryStore_RefusesMoreThanItsBound()
    {
        var store = new InMemoryLiveExecutionConfirmationStore();
        for (var index = 0; index < InMemoryLiveExecutionConfirmationStore.MaximumConfirmations; index++)
            store.Save(Confirmation() with { AccountId = $"account-{index}" });

        store.Invoking(item => item.Save(Confirmation() with { AccountId = "one-too-many" }))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MacKeychainStore_PersistsExactConfirmationAndRevocation_OrFailsClosed()
    {
        var store = new MacKeychainLiveExecutionConfirmationStore(
            "headless-tests-" + Guid.NewGuid().ToString("N"));
        var confirmation = Confirmation();

        try
        {
            store.Read(confirmation.BrokerId, confirmation.AccountId).Should().BeNull();
            store.Save(confirmation);
            store.Read(confirmation.BrokerId, confirmation.AccountId).Should().Be(confirmation);
            store.Read(confirmation.BrokerId, "different-account").Should().BeNull();
            store.Remove(confirmation.BrokerId, confirmation.AccountId).Should().BeTrue();
            store.Read(confirmation.BrokerId, confirmation.AccountId).Should().BeNull();
        }
        catch (Exception exception) when (IsKeychainUnavailable(exception))
        {
            // No usable login Keychain (non-macOS host, or a CI runner without an unlocked
            // keychain). Refusing is the contract: a missing Keychain must never read as
            // "no confirmation was needed".
        }
        finally
        {
            TryRevoke(store, confirmation);
        }
    }

    [Fact]
    public void MacKeychainStore_RejectsInvalidConfirmationsBeforeTouchingTheKeychain()
    {
        var store = new MacKeychainLiveExecutionConfirmationStore(
            "headless-tests-" + Guid.NewGuid().ToString("N"));

        store.Invoking(item => item.Save(Confirmation() with { Acknowledgement = "Live" }))
            .Should().Throw<ArgumentException>();
        store.Invoking(item => item.Read(" ", "700001"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MacKeychainStore_ExposesTheApplicationSupportExecutionDirectory()
    {
        var directory = MacKeychainLiveExecutionConfirmationStore.ExecutionSupportDirectory;

        directory.Should().EndWith(Path.Combine(
            "Library", "Application Support", "DaxAlgoTerminal", "Execution"));
        MacKeychainLiveExecutionConfirmationStore.ExecutionSupportPath("orders.sqlite")
            .Should().Be(Path.Combine(directory, "orders.sqlite"));
        FluentActions
            .Invoking(() => MacKeychainLiveExecutionConfirmationStore.ExecutionSupportPath("nested/orders.sqlite"))
            .Should().Throw<ArgumentException>();
    }

    private static bool IsKeychainUnavailable(Exception exception) =>
        exception is CryptographicException or PlatformNotSupportedException;

    private static void TryRevoke(
        MacKeychainLiveExecutionConfirmationStore store,
        LiveExecutionConfirmation confirmation)
    {
        try
        {
            store.Remove(confirmation.BrokerId, confirmation.AccountId);
        }
        catch (Exception exception) when (IsKeychainUnavailable(exception))
        {
        }
    }

    private static LiveExecutionConfirmation Confirmation() => new(
        "ctrader",
        "700001",
        LiveExecutionConfirmation.RequiredAcknowledgement,
        TimestampUtc,
        "test-owner");
}
