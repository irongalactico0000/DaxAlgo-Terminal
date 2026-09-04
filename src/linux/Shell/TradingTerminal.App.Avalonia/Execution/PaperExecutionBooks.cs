using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Time;
using TradingTerminal.Infrastructure.Execution;
using TradingTerminal.UI.Execution;

namespace TradingTerminal.App.Avalonia.Execution;

/// <summary>
/// Durable operator intent for one local Paper execution book. Orders, fills, positions, cash,
/// leases, and reconciliation evidence remain in the book's isolated SQLite ledger.
/// </summary>
public sealed record PaperExecutionBookDefinition(
    string Id,
    string Name,
    string AccountId,
    string PrimarySymbol,
    IReadOnlyList<string> Strategies,
    bool IsPaused,
    decimal OpeningBalance = 100_000m)
{
    public const string PaperAdapterId = "paper-simulator";

    public string AdapterId => PaperAdapterId;
    public string StrategySummary => Strategies.Count == 0
        ? "No strategy bound"
        : string.Join(", ", Strategies);
    public string InstrumentSummary => string.IsNullOrWhiteSpace(PrimarySymbol)
        ? "Any catalog instrument"
        : PrimarySymbol;
    public string StateText => IsPaused ? "Stopped" : "Running";
    public string OpeningBalanceDisplay => $"{OpeningBalance:N2} SIM";
}

public sealed record PaperExecutionBookCatalog(
    int SchemaVersion,
    string SelectedBookId,
    IReadOnlyList<PaperExecutionBookDefinition> Books);

public interface IPaperExecutionBookStore
{
    PaperExecutionBookCatalog? Read();
    void Save(PaperExecutionBookCatalog catalog);
}

/// <summary>Atomic, versioned per-user persistence for Paper execution-book configuration.</summary>
public sealed class JsonPaperExecutionBookStore : IPaperExecutionBookStore
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumBooks = 64;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object _gate = new();
    private readonly string _path;

    public JsonPaperExecutionBookStore()
        : this(DefaultPath)
    {
    }

    public JsonPaperExecutionBookStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public static string ApplicationSupportRoot
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
                root = Path.Combine(Path.GetTempPath(), "DaxAlgoTerminal");
            return Path.Combine(root, "DaxAlgoTerminal", "execution", "paper");
        }
    }

    public static string DefaultPath => Path.Combine(ApplicationSupportRoot, "books.json");
    public string FilePath => _path;

    public PaperExecutionBookCatalog? Read()
    {
        lock (_gate)
        {
            if (!File.Exists(_path)) return null;
            try
            {
                var payload = File.ReadAllText(_path);
                var catalog = JsonSerializer.Deserialize<PaperExecutionBookCatalog>(payload, SerializerOptions)
                    ?? throw new InvalidDataException("The Paper execution-book file contains JSON null.");
                return ValidateAndNormalize(catalog);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                throw new InvalidDataException(
                    $"Paper execution books could not be loaded from '{_path}'. The existing file was left untouched.",
                    exception);
            }
        }
    }

    public void Save(PaperExecutionBookCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var normalized = ValidateAndNormalize(catalog);
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var temporary = _path + ".tmp";
            var payload = JsonSerializer.Serialize(normalized, SerializerOptions);
            try
            {
                File.WriteAllText(temporary, payload);
                File.Move(temporary, _path, overwrite: true);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch { /* The previous committed file remains authoritative. */ }
            }
        }
    }

    private static PaperExecutionBookCatalog ValidateAndNormalize(PaperExecutionBookCatalog catalog)
    {
        if (catalog.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported Paper execution-book schema {catalog.SchemaVersion}.");
        if (catalog.Books is null || catalog.Books.Count is 0 or > MaximumBooks)
            throw new InvalidDataException($"A catalog must contain between 1 and {MaximumBooks} books.");

        var books = catalog.Books.Select(Normalize).ToArray();
        if (books.Select(book => book.Id).Distinct(StringComparer.Ordinal).Count() != books.Length)
            throw new InvalidDataException("Paper execution-book IDs must be unique.");
        if (books.Select(book => book.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != books.Length)
            throw new InvalidDataException("Paper execution-book names must be unique.");
        if (books.Select(book => book.AccountId).Distinct(StringComparer.Ordinal).Count() != books.Length)
            throw new InvalidDataException("Paper execution account IDs must be unique across books.");
        if (!books.Any(book => string.Equals(book.Id, catalog.SelectedBookId, StringComparison.Ordinal)))
            throw new InvalidDataException("The selected Paper execution book does not exist.");

        return new PaperExecutionBookCatalog(CurrentSchemaVersion, catalog.SelectedBookId, books);
    }

    internal static PaperExecutionBookDefinition Normalize(PaperExecutionBookDefinition book)
    {
        ArgumentNullException.ThrowIfNull(book);
        var id = RequireToken(book.Id, "Book ID", 64);
        var account = RequireToken(book.AccountId, "Account ID", 128);
        var name = book.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 48 || name.Any(char.IsControl))
            throw new InvalidDataException("Book names must contain 2–48 visible characters.");
        var symbol = book.PrimarySymbol?.Trim() ?? string.Empty;
        if (symbol.Length > 32 || symbol.Any(char.IsControl))
            throw new InvalidDataException("A primary symbol cannot exceed 32 visible characters.");
        var strategies = (book.Strategies ?? [])
            .Select(strategy => strategy?.Trim() ?? string.Empty)
            .Where(strategy => strategy.Length != 0)
            .Select(strategy => RequireToken(strategy, "Strategy ID", 128))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(strategy => strategy, StringComparer.Ordinal)
            .ToArray();
        if (book.OpeningBalance <= 0m || book.OpeningBalance > 1_000_000_000_000m ||
            !ExecutionNumericBoundary.TryMoneyFromDecimal(book.OpeningBalance, out _))
            throw new InvalidDataException("Opening balance must be an exact positive amount no greater than 1,000,000,000,000.");
        return new PaperExecutionBookDefinition(
            id, name, account, symbol, strategies, book.IsPaused, book.OpeningBalance);
    }

    private static string RequireToken(string? value, string label, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength ||
            normalized.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
        {
            throw new InvalidDataException(
                $"{label} must contain only ASCII letters, digits, '.', '_' or '-' and be at most {maximumLength} characters.");
        }
        return normalized;
    }
}

public enum PaperExecutionBookFault : byte
{
    None = 0,
    Invalid = 1,
    DuplicateName = 2,
    DuplicateAccount = 3,
    NotFound = 4,
    InUse = 5,
    HasExposure = 6,
    PersistenceFailed = 7,
    RuntimeUnavailable = 8,
}

public sealed record PaperExecutionBookResult(PaperExecutionBookFault Fault, string Message)
{
    public bool IsSuccess => Fault == PaperExecutionBookFault.None;
    public static PaperExecutionBookResult Success(string message) => new(PaperExecutionBookFault.None, message);
    public static PaperExecutionBookResult Failure(PaperExecutionBookFault fault, string message) => new(fault, message);
}

/// <summary>
/// App-lifetime owner for persistent Paper book intent and each lazily-created book runtime. Closing
/// a Console does not stop its book; the session and writer lease remain owned by this manager until
/// application shutdown or a safe book deletion.
/// </summary>
public sealed class PaperExecutionBookManager : IDisposable
{
    private readonly object _gate = new();
    private readonly IPaperExecutionBookStore _store;
    private readonly IClock _clock;
    private readonly IInstrumentRegistry _registry;
    private readonly IExecutionServiceSecretStore _secretStore;
    private readonly string _ledgerRoot;
    private readonly Dictionary<string, SessionEntry> _sessions = new(StringComparer.Ordinal);
    private PaperExecutionBookCatalog _catalog;
    private bool _disposed;

    public PaperExecutionBookManager(IClock clock, IInstrumentRegistry registry)
        : this(new JsonPaperExecutionBookStore(), clock, registry, new MacExecutionServiceSecretStore(),
            JsonPaperExecutionBookStore.ApplicationSupportRoot)
    {
    }

    public PaperExecutionBookManager(
        IPaperExecutionBookStore store,
        IClock clock,
        IInstrumentRegistry registry)
        : this(store, clock, registry, new MacExecutionServiceSecretStore(),
            JsonPaperExecutionBookStore.ApplicationSupportRoot)
    {
    }

    public PaperExecutionBookManager(
        IPaperExecutionBookStore store,
        IClock clock,
        IInstrumentRegistry registry,
        IExecutionServiceSecretStore secretStore,
        string ledgerRoot)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        ArgumentException.ThrowIfNullOrWhiteSpace(ledgerRoot);
        _ledgerRoot = Path.GetFullPath(ledgerRoot);
        var restored = _store.Read();
        _catalog = restored ?? DefaultCatalog();
        if (restored is null) _store.Save(_catalog);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<PaperExecutionBookDefinition> Books
    {
        get { lock (_gate) return _catalog.Books.ToArray(); }
    }

    public PaperExecutionBookDefinition SelectedBook
    {
        get
        {
            lock (_gate)
            {
                return _catalog.Books.Single(book =>
                    string.Equals(book.Id, _catalog.SelectedBookId, StringComparison.Ordinal));
            }
        }
    }

    public int RunningCount
    {
        get { lock (_gate) return _catalog.Books.Count(book => !book.IsPaused); }
    }

    public PaperExecutionBookResult CreateBook(
        string name,
        string accountId,
        string primarySymbol,
        IReadOnlyList<string>? strategies = null,
        decimal openingBalance = 100_000m)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_catalog.Books.Count >= JsonPaperExecutionBookStore.MaximumBooks)
                return PaperExecutionBookResult.Failure(PaperExecutionBookFault.Invalid, "The Paper engine is capped at 64 books.");
            var id = $"paper-{Guid.NewGuid():N}"[..18];
            PaperExecutionBookDefinition candidate;
            try
            {
                candidate = JsonPaperExecutionBookStore.Normalize(new PaperExecutionBookDefinition(
                    id, name, accountId, primarySymbol, strategies ?? [], IsPaused: false, openingBalance));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidDataException)
            {
                return PaperExecutionBookResult.Failure(PaperExecutionBookFault.Invalid, exception.Message);
            }
            if (_catalog.Books.Any(book => string.Equals(book.Name, candidate.Name, StringComparison.OrdinalIgnoreCase)))
                return PaperExecutionBookResult.Failure(PaperExecutionBookFault.DuplicateName, $"A book named '{candidate.Name}' already exists.");
            if (_catalog.Books.Any(book => string.Equals(book.AccountId, candidate.AccountId, StringComparison.Ordinal)))
                return PaperExecutionBookResult.Failure(PaperExecutionBookFault.DuplicateAccount, $"Paper account '{candidate.AccountId}' already belongs to another book.");
            if (candidate.PrimarySymbol.Length != 0 && !_registry.All().Any(instrument =>
                    string.Equals(instrument.CanonicalSymbol, candidate.PrimarySymbol, StringComparison.OrdinalIgnoreCase)))
                return PaperExecutionBookResult.Failure(PaperExecutionBookFault.Invalid, "Select a primary symbol from the canonical instrument catalog.");

            var next = _catalog.Books.Append(candidate).ToArray();
            return CommitLocked(new PaperExecutionBookCatalog(
                JsonPaperExecutionBookStore.CurrentSchemaVersion, candidate.Id, next),
                $"Created and selected Paper book '{candidate.Name}'.");
        }
    }

    public PaperExecutionBookResult SelectBook(string bookId)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var book = FindLocked(bookId);
            if (book is null) return Missing(bookId);
            if (string.Equals(book.Id, _catalog.SelectedBookId, StringComparison.Ordinal))
                return PaperExecutionBookResult.Success($"Paper book '{book.Name}' is already selected.");
            return CommitLocked(_catalog with { SelectedBookId = book.Id }, $"Selected Paper book '{book.Name}'.");
        }
    }

    public PaperExecutionBookResult RenameBook(string bookId, string name)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var book = FindLocked(bookId);
            if (book is null) return Missing(bookId);
            PaperExecutionBookDefinition renamed;
            try { renamed = JsonPaperExecutionBookStore.Normalize(book with { Name = name }); }
            catch (InvalidDataException exception)
            {
                return PaperExecutionBookResult.Failure(PaperExecutionBookFault.Invalid, exception.Message);
            }
            if (_catalog.Books.Any(item => item.Id != book.Id &&
                    string.Equals(item.Name, renamed.Name, StringComparison.OrdinalIgnoreCase)))
                return PaperExecutionBookResult.Failure(PaperExecutionBookFault.DuplicateName, $"A book named '{renamed.Name}' already exists.");
            return ReplaceLocked(renamed, $"Renamed Paper book to '{renamed.Name}'.");
        }
    }

    public async ValueTask<PaperExecutionBookResult> SetPausedAsync(string bookId, bool paused)
    {
        SessionEntry entry;
        PaperExecutionBookDefinition book;
        lock (_gate)
        {
            ThrowIfDisposed();
            var found = FindLocked(bookId);
            if (found is null) return Missing(bookId);
            book = found;
            try { entry = GetOrCreateSessionLocked(book); }
            catch (Exception exception)
            {
                return PaperExecutionBookResult.Failure(PaperExecutionBookFault.RuntimeUnavailable,
                    $"Paper book '{book.Name}' could not open: {exception.Message}");
            }
        }

        var result = await entry.Session.Client.SetIntakePausedAsync(paused).ConfigureAwait(false);
        if (!result.IsSuccess)
            return PaperExecutionBookResult.Failure(PaperExecutionBookFault.RuntimeUnavailable, result.Message);
        lock (_gate)
        {
            var current = FindLocked(bookId);
            if (current is null) return Missing(bookId);
            return ReplaceLocked(current with { IsPaused = paused },
                paused ? $"Stopped new-order intake for '{current.Name}'." : $"Resumed new-order intake for '{current.Name}'.");
        }
    }

    public PaperExecutionBookResult DeleteBook(string bookId)
    {
        SessionEntry? entry;
        lock (_gate)
        {
            ThrowIfDisposed();
            var book = FindLocked(bookId);
            if (book is null) return Missing(bookId);
            if (_catalog.Books.Count == 1)
                return PaperExecutionBookResult.Failure(PaperExecutionBookFault.Invalid, "At least one Paper execution book must remain.");
            _sessions.TryGetValue(book.Id, out entry);
            if (entry is { ActiveUsers: > 0 })
                return PaperExecutionBookResult.Failure(PaperExecutionBookFault.InUse, "Close every Console and Strategy Runner using this book before deleting it.");
            if (entry is not null)
            {
                var snapshot = entry.Session.Client.GetSnapshot();
                if (snapshot.Orders.Any(order => !OrderLifecycle.IsTerminal(order.State)) ||
                    snapshot.Economics.Positions.Any(position => position.Quantity.Coefficient != 0))
                    return PaperExecutionBookResult.Failure(PaperExecutionBookFault.HasExposure, "Cancel or finish working orders and flatten positions before deleting this book.");
            }

            var remaining = _catalog.Books.Where(item => item.Id != book.Id).ToArray();
            var selected = _catalog.SelectedBookId == book.Id ? remaining[0].Id : _catalog.SelectedBookId;
            var result = CommitLocked(new PaperExecutionBookCatalog(
                JsonPaperExecutionBookStore.CurrentSchemaVersion, selected, remaining),
                $"Removed Paper book '{book.Name}'. Its ledger was retained for recovery.");
            if (!result.IsSuccess) return result;
            if (entry is not null)
            {
                entry.Session.Client.SnapshotInvalidated -= entry.SnapshotHandler;
                entry.Session.Dispose();
                _sessions.Remove(book.Id);
            }
            return result;
        }
    }

    public PaperExecutionBookSessionLease AcquireSelectedSession()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var book = FindLocked(_catalog.SelectedBookId)!;
            var entry = GetOrCreateSessionLocked(book);
            entry.ActiveUsers++;
            return new PaperExecutionBookSessionLease(this, book, entry.Session);
        }
    }

    internal void Release(string bookId)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(bookId, out var entry) && entry.ActiveUsers > 0)
                entry.ActiveUsers--;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool IsBookOpen(string bookId)
    {
        lock (_gate) return _sessions.TryGetValue(bookId, out var entry) && entry.ActiveUsers > 0;
    }

    public string LedgerPathFor(string bookId)
    {
        var book = Books.SingleOrDefault(item => item.Id == bookId)
            ?? throw new KeyNotFoundException($"Paper execution book '{bookId}' does not exist.");
        return LedgerPathFor(book);
    }

    private SessionEntry GetOrCreateSessionLocked(PaperExecutionBookDefinition book)
    {
        if (_sessions.TryGetValue(book.Id, out var existing)) return existing;
        var resource = new ExecutionResource(
            new VenueId(PaperExecutionBookDefinition.PaperAdapterId),
            new TradingAccountId(book.AccountId),
            ExecutionEnvironment.SimulatedPaper);
        var session = PaperExecutionDesktopSession.CreateForBook(
            book.Id,
            book.Name,
            resource,
            LedgerPathFor(book),
            _clock,
            _registry,
            _secretStore,
            ExecutionNumericBoundary.MoneyFromDecimal(book.OpeningBalance));
        EventHandler handler = (_, _) => CapturePausedState(book.Id, session);
        session.Client.SnapshotInvalidated += handler;
        var entry = new SessionEntry(session, handler);
        _sessions.Add(book.Id, entry);
        if (book.IsPaused)
        {
            var pause = session.Client.SetIntakePausedAsync(true).AsTask().GetAwaiter().GetResult();
            if (!pause.IsSuccess)
            {
                session.Client.SnapshotInvalidated -= handler;
                session.Dispose();
                _sessions.Remove(book.Id);
                throw new InvalidOperationException(pause.Message);
            }
        }
        return entry;
    }

    private void CapturePausedState(string bookId, PaperExecutionDesktopSession session)
    {
        var paused = session.Client.GetSnapshot().IntakePaused;
        var changed = false;
        lock (_gate)
        {
            if (_disposed) return;
            var book = FindLocked(bookId);
            if (book is null || book.IsPaused == paused) return;
            var updated = book with { IsPaused = paused };
            var books = _catalog.Books.Select(item => item.Id == bookId ? updated : item).ToArray();
            _catalog = _catalog with { Books = books };
            _store.Save(_catalog);
            changed = true;
        }
        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }

    private string LedgerPathFor(PaperExecutionBookDefinition book)
    {
        var directory = book.Id == "local-paper"
            ? Path.Combine(_ledgerRoot, "local-paper")
            : Path.Combine(_ledgerRoot, "books", book.Id);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "orders.db");
    }

    private PaperExecutionBookResult ReplaceLocked(PaperExecutionBookDefinition replacement, string message)
    {
        var books = _catalog.Books.Select(book => book.Id == replacement.Id ? replacement : book).ToArray();
        return CommitLocked(_catalog with { Books = books }, message);
    }

    private PaperExecutionBookResult CommitLocked(PaperExecutionBookCatalog catalog, string message)
    {
        try
        {
            _store.Save(catalog);
            _catalog = catalog;
        }
        catch (Exception exception)
        {
            return PaperExecutionBookResult.Failure(PaperExecutionBookFault.PersistenceFailed,
                $"Paper execution books were not changed because persistence failed: {exception.Message}");
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return PaperExecutionBookResult.Success(message);
    }

    private PaperExecutionBookDefinition? FindLocked(string? bookId) =>
        _catalog.Books.FirstOrDefault(book => string.Equals(book.Id, bookId, StringComparison.Ordinal));

    private static PaperExecutionBookResult Missing(string? bookId) =>
        PaperExecutionBookResult.Failure(PaperExecutionBookFault.NotFound, $"Paper execution book '{bookId}' does not exist.");

    private static PaperExecutionBookCatalog DefaultCatalog()
    {
        var book = new PaperExecutionBookDefinition(
            "local-paper", "Local Paper", "local-paper", string.Empty, [], IsPaused: false);
        return new PaperExecutionBookCatalog(JsonPaperExecutionBookStore.CurrentSchemaVersion, book.Id, [book]);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        SessionEntry[] sessions;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            sessions = _sessions.Values.ToArray();
            _sessions.Clear();
        }
        foreach (var entry in sessions)
        {
            entry.Session.Client.SnapshotInvalidated -= entry.SnapshotHandler;
            entry.Session.Dispose();
        }
    }

    private sealed class SessionEntry(PaperExecutionDesktopSession session, EventHandler snapshotHandler)
    {
        public PaperExecutionDesktopSession Session { get; } = session;
        public EventHandler SnapshotHandler { get; } = snapshotHandler;
        public int ActiveUsers { get; set; }
    }
}

public sealed class PaperExecutionBookSessionLease : IDisposable
{
    private PaperExecutionBookManager? _owner;

    internal PaperExecutionBookSessionLease(
        PaperExecutionBookManager owner,
        PaperExecutionBookDefinition book,
        PaperExecutionDesktopSession session)
    {
        _owner = owner;
        Book = book;
        Session = session;
    }

    public PaperExecutionBookDefinition Book { get; }
    public PaperExecutionDesktopSession Session { get; }

    public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release(Book.Id);
}

/// <summary>Native book-management projection shared by the shell chip and manager window.</summary>
public sealed partial class PaperExecutionBooksViewModel : ObservableObject, IDisposable
{
    private readonly PaperExecutionBookManager? _manager;
    private readonly IInstrumentRegistry? _instrumentRegistry;
    private bool _disposed;

    public PaperExecutionBooksViewModel(
        PaperExecutionBookManager manager,
        IInstrumentRegistry instrumentRegistry,
        TradingTerminal.Infrastructure.Backtest.IBacktestStrategyRegistry strategyRegistry)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _instrumentRegistry = instrumentRegistry ?? throw new ArgumentNullException(nameof(instrumentRegistry));
        ArgumentNullException.ThrowIfNull(strategyRegistry);
        Strategies = strategyRegistry.All.Select(strategy => strategy.Id)
            .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (instrumentRegistry.All().Count == 0)
        {
            instrumentRegistry.ResolveOrCreate(Contract.UsStock("SPY", "ARCA"), BrokerKind.Simulated);
            instrumentRegistry.ResolveOrCreate(Contract.UsStock("QQQ", "NASDAQ"), BrokerKind.Simulated);
        }
        Instruments = instrumentRegistry.All()
            .Where(instrument => !instrument.Id.IsNone)
            .OrderBy(instrument => instrument.CanonicalSymbol, StringComparer.OrdinalIgnoreCase)
            .Take(256)
            .Select(instrument => new PaperExecutionInstrumentChoice(
                instrument.Id,
                instrument.CanonicalSymbol,
                instrument.AssetClass.ToString(),
                instrument.Exchange,
                instrument.Currency))
            .ToArray();
        _manager.Changed += OnManagerChanged;
        Refresh();
    }

    public PaperExecutionBooksViewModel()
    {
        Instruments = [];
        Strategies = [];
    }

    public ObservableCollection<PaperExecutionBookDefinition> Books { get; } = [];
    public IReadOnlyList<PaperExecutionInstrumentChoice> Instruments { get; }
    public IReadOnlyList<string> Strategies { get; }

    [ObservableProperty] private PaperExecutionBookDefinition? _selectedBook;
    [ObservableProperty] private PaperExecutionInstrumentChoice? _newPrimaryInstrument;
    [ObservableProperty] private string? _newStrategyId;
    [ObservableProperty] private string _newBookName = "Paper Book";
    [ObservableProperty] private string _newAccountId = "paper-account";
    [ObservableProperty] private string _newOpeningBalance = "100000";
    [ObservableProperty] private string _renameText = string.Empty;
    [ObservableProperty] private string _summary = "No Paper execution books";
    [ObservableProperty] private string _lastMessage = "Book definitions are stored separately from order ledgers.";
    [ObservableProperty] private bool _lastOperationSucceeded = true;

    public string SelectedStatus => SelectedBook is null
        ? "No book selected"
        : $"{SelectedBook.Name} · {SelectedBook.AccountId} · {SelectedBook.StateText}";

    partial void OnSelectedBookChanged(PaperExecutionBookDefinition? value)
    {
        RenameText = value?.Name ?? string.Empty;
        OnPropertyChanged(nameof(SelectedStatus));
        SelectCommand.NotifyCanExecuteChanged();
        RenameCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        ToggleCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void Create()
    {
        if (_manager is null) return;
        if (!decimal.TryParse(NewOpeningBalance, NumberStyles.Number, CultureInfo.InvariantCulture, out var openingBalance))
        {
            Apply(PaperExecutionBookResult.Failure(
                PaperExecutionBookFault.Invalid,
                "Opening balance must be a positive decimal written with '.' as the decimal separator."));
            return;
        }
        var result = _manager.CreateBook(
            NewBookName,
            NewAccountId,
            NewPrimaryInstrument?.Symbol ?? string.Empty,
            string.IsNullOrWhiteSpace(NewStrategyId) ? [] : [NewStrategyId],
            openingBalance);
        Apply(result);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Select()
    {
        if (_manager is null || SelectedBook is null) return;
        Apply(_manager.SelectBook(SelectedBook.Id));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Rename()
    {
        if (_manager is null || SelectedBook is null) return;
        Apply(_manager.RenameBook(SelectedBook.Id, RenameText));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ToggleAsync()
    {
        if (_manager is null || SelectedBook is null) return;
        Apply(await _manager.SetPausedAsync(SelectedBook.Id, !SelectedBook.IsPaused));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        if (_manager is null || SelectedBook is null) return;
        Apply(_manager.DeleteBook(SelectedBook.Id));
    }

    private bool HasSelection() => SelectedBook is not null;

    private void Apply(PaperExecutionBookResult result)
    {
        LastOperationSucceeded = result.IsSuccess;
        LastMessage = result.Message;
        Refresh();
    }

    private void OnManagerChanged(object? sender, EventArgs e) =>
        global::Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);

    private void Refresh()
    {
        if (_manager is null || _disposed) return;
        var selectedId = _manager.SelectedBook.Id;
        Books.Clear();
        foreach (var book in _manager.Books) Books.Add(book);
        SelectedBook = Books.FirstOrDefault(book => book.Id == selectedId) ?? Books.FirstOrDefault();
        Summary = $"{_manager.RunningCount}/{Books.Count} running · selected {_manager.SelectedBook.Name}";
        OnPropertyChanged(nameof(SelectedStatus));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_manager is not null) _manager.Changed -= OnManagerChanged;
    }
}
