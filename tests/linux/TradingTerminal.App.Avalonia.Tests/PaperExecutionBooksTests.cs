using System.Text.Json;
using Avalonia.Headless.XUnit;
using TradingTerminal.App.Avalonia.Execution;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;
using TradingTerminal.Infrastructure.Execution;
using TradingTerminal.UI.Execution;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class PaperExecutionBooksTests
{
    [Fact]
    public void Json_store_round_trips_selected_book_and_pause_intent_atomically()
    {
        var directory = NewDirectory();
        try
        {
            var path = Path.Combine(directory, "books.json");
            var store = new JsonPaperExecutionBookStore(path);
            var catalog = new PaperExecutionBookCatalog(
                JsonPaperExecutionBookStore.CurrentSchemaVersion,
                "paper-beta",
                [
                    new("local-paper", "Local Paper", "local-paper", "", [], false),
                    new("paper-beta", "Beta", "paper-account-beta", "QQQ", ["ema", "pairs-screen"], true, 250_000.25m),
                ]);

            store.Save(catalog);
            var restored = Assert.IsType<PaperExecutionBookCatalog>(store.Read());

            Assert.Equal("paper-beta", restored.SelectedBookId);
            var beta = Assert.Single(restored.Books, book => book.Id == "paper-beta");
            Assert.True(beta.IsPaused);
            Assert.Equal("paper-account-beta", beta.AccountId);
            Assert.Equal("QQQ", beta.PrimarySymbol);
            Assert.Equal(["ema", "pairs-screen"], beta.Strategies);
            Assert.Equal(250_000.25m, beta.OpeningBalance);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Json_store_loads_pre_analytics_book_with_the_explicit_default_opening_balance()
    {
        var directory = NewDirectory();
        try
        {
            var path = Path.Combine(directory, "books.json");
            File.WriteAllText(path,
                """
                {
                  "schemaVersion": 1,
                  "selectedBookId": "local-paper",
                  "books": [
                    {
                      "id": "local-paper",
                      "name": "Local Paper",
                      "accountId": "local-paper",
                      "primarySymbol": "",
                      "strategies": [],
                      "isPaused": false
                    }
                  ]
                }
                """);

            var restored = Assert.IsType<PaperExecutionBookCatalog>(
                new JsonPaperExecutionBookStore(path).Read());

            Assert.Equal(100_000m, Assert.Single(restored.Books).OpeningBalance);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Invalid_catalog_fails_closed_without_replacing_the_existing_file()
    {
        var directory = NewDirectory();
        try
        {
            var path = Path.Combine(directory, "books.json");
            var store = new JsonPaperExecutionBookStore(path);
            store.Save(new PaperExecutionBookCatalog(
                JsonPaperExecutionBookStore.CurrentSchemaVersion,
                "local-paper",
                [new("local-paper", "Local Paper", "local-paper", "", [], false)]));
            var before = File.ReadAllBytes(path);

            Assert.Throws<InvalidDataException>(() => store.Save(new PaperExecutionBookCatalog(
                JsonPaperExecutionBookStore.CurrentSchemaVersion,
                "missing",
                [new("local-paper", "Local Paper", "local-paper", "", [], false)])));

            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task Selected_book_restores_with_its_account_ledger_and_paused_state()
    {
        var directory = NewDirectory();
        var storePath = Path.Combine(directory, "books.json");
        var ledgerRoot = Path.Combine(directory, "ledgers");
        var clock = new MutableClock(new DateTime(2026, 8, 25, 1, 0, 0, DateTimeKind.Utc));
        var registry = new MemoryRegistry();
        var secret = new FixedSecretStore();
        try
        {
            string selectedId;
            string ledgerPath;
            using (var manager = Manager())
            {
                var created = manager.CreateBook("Momentum Paper", "paper-momentum", "TESTA", ["ema"], 275_000.50m);
                Assert.True(created.IsSuccess, created.Message);
                selectedId = manager.SelectedBook.Id;
                ledgerPath = manager.LedgerPathFor(selectedId);
                Assert.NotEqual(manager.LedgerPathFor("local-paper"), ledgerPath);

                using var lease = manager.AcquireSelectedSession();
                Assert.Equal("paper-momentum", lease.Session.Resource.TradingAccountId.Value);
                Assert.Equal(275_000.50m, lease.Session.Client.GetSnapshot().PortfolioAnalytics!.OpeningBalance);
                Assert.Equal(selectedId, lease.Session.BookId);
                Assert.True((await lease.Session.Client.RefreshAsync()).IsSuccess);
                var instrument = lease.Session.Instruments.Single(choice => choice.Symbol == "TESTA");
                await SubmitMarket(lease.Session, instrument, OrderSide.Buy, "2", "100");
                await SubmitMarket(lease.Session, instrument, OrderSide.Sell, "2", "110");
                var analytics = lease.Session.Client.GetSnapshot().PortfolioAnalytics!;
                Assert.Equal(20m, analytics.RealizedProfitAndLoss);
                Assert.Equal(275_020.50m, analytics.MarkedEquity);
                var paused = await manager.SetPausedAsync(selectedId, true);
                Assert.True(paused.IsSuccess, paused.Message);
            }

            using (var restored = Manager())
            {
                Assert.Equal(selectedId, restored.SelectedBook.Id);
                Assert.True(restored.SelectedBook.IsPaused);
                Assert.Equal(ledgerPath, restored.LedgerPathFor(selectedId));
                using var lease = restored.AcquireSelectedSession();
                Assert.True(lease.Session.Client.GetSnapshot().IntakePaused);
                Assert.True((await lease.Session.Client.RefreshAsync()).IsSuccess);
                Assert.Equal("paper-momentum", lease.Session.Client.GetSnapshot().Resource.TradingAccountId.Value);
                Assert.Equal(275_000.50m, lease.Session.Client.GetSnapshot().PortfolioAnalytics!.OpeningBalance);
                Assert.Equal(20m, lease.Session.Client.GetSnapshot().PortfolioAnalytics!.RealizedProfitAndLoss);
                Assert.Equal(275_020.50m, lease.Session.Client.GetSnapshot().PortfolioAnalytics!.MarkedEquity);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        PaperExecutionBookManager Manager() => new(
            new JsonPaperExecutionBookStore(storePath),
            clock,
            registry,
            secret,
            ledgerRoot);

        static async Task SubmitMarket(
            PaperExecutionDesktopSession session,
            PaperExecutionInstrumentChoice instrument,
            OrderSide side,
            string quantity,
            string mark)
        {
            var draft = new PaperOrderTicketDraft(
                instrument, side, OrderType.Market, TimeInForce.Day,
                quantity, mark, null, null, false);
            Assert.True(session.TryCreateSubmit(
                draft, session.Client.GetSnapshot(), out var request, out var reason), reason);
            var result = await session.Client.SubmitAsync(request!);
            Assert.True(result.IsSuccess, result.Message);
        }
    }

    [AvaloniaFact]
    public async Task Two_books_have_independent_resources_ledgers_orders_and_positions()
    {
        var directory = NewDirectory();
        var clock = new MutableClock(new DateTime(2026, 8, 25, 2, 0, 0, DateTimeKind.Utc));
        var registry = new MemoryRegistry();
        try
        {
            using var manager = new PaperExecutionBookManager(
                new JsonPaperExecutionBookStore(Path.Combine(directory, "books.json")),
                clock,
                registry,
                new FixedSecretStore(),
                Path.Combine(directory, "ledgers"));
            Assert.True(manager.CreateBook("Alpha", "paper-alpha", "TESTA").IsSuccess);
            using var alpha = manager.AcquireSelectedSession();
            Assert.True((await alpha.Session.Client.RefreshAsync()).IsSuccess);
            var instrument = alpha.Session.Instruments.Single(choice => choice.Symbol == "TESTA");
            var draft = new PaperOrderTicketDraft(
                instrument, OrderSide.Buy, OrderType.Market, TimeInForce.Day,
                "2", "100", null, null, false);
            Assert.True(alpha.Session.TryCreateSubmit(
                draft, alpha.Session.Client.GetSnapshot(), out var request, out var reason), reason);
            Assert.True((await alpha.Session.Client.SubmitAsync(request!)).IsSuccess);
            Assert.Single(alpha.Session.Client.GetSnapshot().Orders);
            Assert.Equal(2m, ExecutionNumericBoundary.ToDecimal(
                Assert.Single(alpha.Session.Client.GetSnapshot().Economics.Positions).Quantity));

            Assert.True(manager.CreateBook("Beta", "paper-beta", "TESTB").IsSuccess);
            using var beta = manager.AcquireSelectedSession();
            Assert.True((await beta.Session.Client.RefreshAsync()).IsSuccess);

            Assert.NotEqual(alpha.Book.Id, beta.Book.Id);
            Assert.NotEqual(alpha.Session.Resource, beta.Session.Resource);
            Assert.NotEqual(manager.LedgerPathFor(alpha.Book.Id), manager.LedgerPathFor(beta.Book.Id));
            Assert.Empty(beta.Session.Client.GetSnapshot().Orders);
            Assert.Empty(beta.Session.Client.GetSnapshot().Economics.Positions);
            Assert.Single(alpha.Session.Client.GetSnapshot().Orders);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task Delete_refuses_active_window_and_nonflat_exposure_but_never_deletes_ledger()
    {
        var directory = NewDirectory();
        var clock = new MutableClock(new DateTime(2026, 8, 25, 3, 0, 0, DateTimeKind.Utc));
        var registry = new MemoryRegistry();
        try
        {
            using var manager = new PaperExecutionBookManager(
                new JsonPaperExecutionBookStore(Path.Combine(directory, "books.json")),
                clock,
                registry,
                new FixedSecretStore(),
                Path.Combine(directory, "ledgers"));
            Assert.True(manager.CreateBook("Exposure", "paper-exposure", "TESTA").IsSuccess);
            var id = manager.SelectedBook.Id;
            var ledger = manager.LedgerPathFor(id);
            using (var lease = manager.AcquireSelectedSession())
            {
                Assert.Equal(PaperExecutionBookFault.InUse, manager.DeleteBook(id).Fault);
                Assert.True((await lease.Session.Client.RefreshAsync()).IsSuccess);
                var instrument = lease.Session.Instruments.Single(choice => choice.Symbol == "TESTA");
                var draft = new PaperOrderTicketDraft(
                    instrument, OrderSide.Buy, OrderType.Market, TimeInForce.Day,
                    "1", "100", null, null, false);
                Assert.True(lease.Session.TryCreateSubmit(
                    draft, lease.Session.Client.GetSnapshot(), out var request, out var reason), reason);
                Assert.True((await lease.Session.Client.SubmitAsync(request!)).IsSuccess);
            }

            Assert.Equal(PaperExecutionBookFault.HasExposure, manager.DeleteBook(id).Fault);
            Assert.True(File.Exists(ledger));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task Native_admit_handoff_catalog_schema_opens_selected_book_fill_and_position()
    {
        // Native Admit→Prepare writes this Mac-compatible books.json shape (CreateBook handoff).
        var directory = NewDirectory();
        var storePath = Path.Combine(directory, "books.json");
        var ledgerRoot = Path.Combine(directory, "ledgers");
        var clock = new MutableClock(new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc));
        try
        {
            var handoffCatalog = """
                {
                  "schemaVersion": 1,
                  "selectedBookId": "paper-handoff01",
                  "books": [
                    {
                      "id": "paper-handoff01",
                      "name": "admit-owner-test",
                      "accountId": "owner-test-account",
                      "primarySymbol": "TESTA",
                      "strategies": [ "owner-test-strategy" ],
                      "isPaused": false,
                      "openingBalance": 100000
                    }
                  ]
                }
                """;
            await File.WriteAllTextAsync(storePath, handoffCatalog);

            using var manager = new PaperExecutionBookManager(
                new JsonPaperExecutionBookStore(storePath),
                clock,
                new MemoryRegistry(),
                new FixedSecretStore(),
                ledgerRoot);
            Assert.Equal("paper-handoff01", manager.SelectedBook.Id);
            Assert.Equal("owner-test-account", manager.SelectedBook.AccountId);
            Assert.Equal("TESTA", manager.SelectedBook.PrimarySymbol);
            Assert.Contains("owner-test-strategy", manager.SelectedBook.Strategies);

            using var lease = manager.AcquireSelectedSession();
            Assert.True((await lease.Session.Client.RefreshAsync()).IsSuccess);
            var instrument = lease.Session.Instruments.Single(choice => choice.Symbol == "TESTA");
            var draft = new PaperOrderTicketDraft(
                instrument, OrderSide.Buy, OrderType.Market, TimeInForce.Day,
                "2", "100", null, null, false);
            Assert.True(lease.Session.TryCreateSubmit(
                draft, lease.Session.Client.GetSnapshot(), out var request, out var reason), reason);
            Assert.True((await lease.Session.Client.SubmitAsync(request!)).IsSuccess);

            var snapshot = lease.Session.Client.GetSnapshot();
            Assert.Contains(snapshot.Orders, order => order.State == OrderLifecycleState.Filled);
            Assert.Equal(2m, ExecutionNumericBoundary.ToDecimal(
                Assert.Single(snapshot.Economics.Positions).Quantity));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "daxalgo-paper-book-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class FixedSecretStore : IExecutionServiceSecretStore
    {
        private readonly byte[] _secret = Enumerable.Range(0, ExecutionIpcProtocol.SecretSize)
            .Select(value => checked((byte)value)).ToArray();
        public byte[] LoadOrCreate() => (byte[])_secret.Clone();
    }

    private sealed class MemoryRegistry : IInstrumentRegistry
    {
        private readonly Dictionary<int, Instrument> _instruments = [];
        private readonly Dictionary<(BrokerKind Broker, string Symbol), InstrumentId> _aliases = [];
        private int _next;

        public MemoryRegistry()
        {
            ResolveOrCreate(Contract.UsStock("TESTA", "TEST"), BrokerKind.Simulated);
            ResolveOrCreate(Contract.UsStock("TESTB", "TEST"), BrokerKind.Simulated);
        }

        public Instrument? Get(InstrumentId id) => _instruments.GetValueOrDefault(id.Value);
        public InstrumentId? Resolve(BrokerKind broker, string brokerSymbol) =>
            _aliases.TryGetValue((broker, brokerSymbol), out var value) ? value : null;
        public InstrumentId ResolveOrCreate(Contract contract, BrokerKind broker)
        {
            if (_aliases.TryGetValue((broker, contract.Symbol), out var existing)) return existing;
            var id = new InstrumentId(++_next);
            _instruments.Add(id.Value, new Instrument(
                id, contract.Symbol, AssetClass.Equity, contract.PrimaryExchange,
                contract.Currency, 0.01, 1));
            _aliases.Add((broker, contract.Symbol), id);
            return id;
        }
        public string? ToBrokerSymbol(InstrumentId id, BrokerKind broker) =>
            _aliases.FirstOrDefault(item => item.Key.Broker == broker && item.Value == id).Key.Symbol;
        public void RegisterAlias(InstrumentAlias alias) =>
            _aliases[(alias.Broker, alias.BrokerSymbol)] = alias.InstrumentId;
        public IReadOnlyList<Instrument> All() => _instruments.Values.OrderBy(item => item.Id.Value).ToArray();
    }
}
