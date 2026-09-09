using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using DaxAlgo.Sdk;
using TradingTerminal.App.Authoring;
using TradingTerminal.App.Avalonia.Execution;
using TradingTerminal.App.Avalonia.Settings;
using TradingTerminal.Backtest;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Infrastructure.Backtest;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using TradingTerminal.UI.Logging;
using TradingTerminal.UI.Strategies;

namespace TradingTerminal.App.Avalonia.Diagnostics;

/// <summary>
/// Owned Mac UI E2E: research gallery → attach compiled strategy → Validate → Paper BOOK POSITION.
/// Strategy freeze uses the buy-once contract (AI <c>GenerateCanonicalPaperStrategy</c> still separate).
/// Invoke with <c>--smoke-research-to-paper[=/path/report.txt]</c>.
/// </summary>
internal static class ResearchToPaperE2ESmoke
{
    public static async Task<int> RunAsync(IServiceProvider services, string reportPath)
    {
        var lines = new List<string>
        {
            $"Research→Validate→Paper E2E — {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            "Flow: authoring research auto-collect → compile/register strategy → historical validation → Paper BOOK POSITION",
            string.Empty,
        };

        var exitCode = 1;
        StrategyAuthoringWindow? authoringWindow = null;
        TradingTerminal.Backtest.AvaloniaUi.QuickBacktestAvaloniaWindow? backtestWindow = null;
        PaperStrategyRunnerWindow? paperWindow = null;
        PaperStrategyRunnerViewModel? paperVm = null;
        PaperExecutionBookSessionLease? bookLease = null;
        QuickBacktestViewModel? backtest = null;

        try
        {
            var authoring = services.GetRequiredService<StrategyAuthoringViewModel>();
            authoringWindow = new StrategyAuthoringWindow { DataContext = authoring };
            authoringWindow.Show();
            await PumpAsync().ConfigureAwait(true);
            lines.Add("PASS  opened Strategy Builder (authoring UI)");

            await authoring.AutoCollectLocalResearchSamplesCommand.ExecuteAsync("next-day-plus-5")
                .ConfigureAwait(true);
            await PumpAsync().ConfigureAwait(true);
            var sampleCount = authoring.ResearchEventSampleCount;
            var galleryCount = authoring.ResearchOutcomeGalleryResult?.Matches.Count ?? 0;
            if (sampleCount <= 0)
            {
                lines.Add($"FAIL  research auto-collect samples={sampleCount} galleryMatches={galleryCount}");
                return await WriteAsync(lines, reportPath, 1).ConfigureAwait(false);
            }

            lines.Add($"PASS  research auto-collect samples={sampleCount} galleryMatches={galleryCount}");

            if (authoring.CanRunResearchExperiment)
            {
                await authoring.RunResearchExperimentCommand.ExecuteAsync(null).ConfigureAwait(true);
                await PumpAsync().ConfigureAwait(true);
                lines.Add(authoring.HasResearchExperimentEvidence
                    ? "PASS  chronological research experiment evidence bound"
                    : $"WARN  experiment ran but evidence missing ({authoring.ResearchExperimentStatusText})");
            }
            else
            {
                lines.Add($"WARN  research experiment not runnable yet ({authoring.ResearchExperimentStatusText})");
            }

            var registry = services.GetRequiredService<IInstrumentRegistry>();
            var aapl = registry.All().FirstOrDefault(item =>
                string.Equals(item.CanonicalSymbol, "AAPL", StringComparison.OrdinalIgnoreCase));
            if (aapl is null)
            {
                var id = registry.ResolveOrCreate(Contract.UsStock("AAPL", "NASDAQ"), BrokerKind.Simulated);
                aapl = registry.Get(id);
            }

            if (aapl is null)
            {
                lines.Add("FAIL  AAPL instrument unavailable in registry");
                return await WriteAsync(lines, reportPath, 1).ConfigureAwait(false);
            }

            var specification = BuildSpecification(aapl);
            var source = BuildSource(specification);
            var script = new StrategyScript(
                specification.UnitId,
                specification.Name,
                [new StrategyFile("GeneratedPaperStrategy.cs", source)]);
            var compilation = new RoslynAuthoredUnitCompilerV1().Compile(specification, script);
            if (!compilation.Success || compilation.Unit is not CompiledAuthoredUnitV1 compiled)
            {
                lines.Add("FAIL  strategy compile: " +
                          (compilation.Diagnostics.FirstOrDefault()?.Message ?? "unknown"));
                return await WriteAsync(lines, reportPath, 1).ConfigureAwait(false);
            }

            if (!authoring.TryAttachCompiledStrategyForDiagnostics(specification, compiled, script, out var attachReason))
            {
                lines.Add($"FAIL  attach compiled strategy: {attachReason}");
                return await WriteAsync(lines, reportPath, 1).ConfigureAwait(false);
            }

            lines.Add("PASS  compiled strategy attached + registered (stub for AI GenerateCanonicalPaperStrategy)");

            if (!authoring.TryCreateHistoricalValidationContext(out var context, out var validationReason) ||
                context is null)
            {
                lines.Add($"FAIL  historical validation context: {validationReason}");
                return await WriteAsync(lines, reportPath, 1).ConfigureAwait(false);
            }

            var kernelRegistry = services.GetRequiredService<IStrategyKernelRegistry>();
            var registration = kernelRegistry.Find(specification.UnitId);
            if (registration is null)
            {
                lines.Add("FAIL  strategy missing from kernel registry after attach");
                return await WriteAsync(lines, reportPath, 1).ConfigureAwait(false);
            }

            backtest = services.GetRequiredService<QuickBacktestViewModel>();
            backtestWindow = services.GetRequiredService<TradingTerminal.Backtest.AvaloniaUi.QuickBacktestAvaloniaWindow>();
            backtestWindow.DataContext = backtest;
            backtestWindow.Title = $"Historical validation — {registration.DisplayName}";
            backtestWindow.Show();
            await PumpAsync().ConfigureAwait(true);

            if (!backtest.Initialize(registration, context))
            {
                lines.Add($"FAIL  Quick Backtest Initialize: {backtest.Status}");
                return await WriteAsync(lines, reportPath, 1).ConfigureAwait(false);
            }

            lines.Add("PASS  opened Historical validation (Validate UI)");

            QuickBacktestPaperLaunchRequest? launch = null;
            void OnCompleted(QuickBacktestPaperLaunchRequest request) => launch = request;
            backtest.HistoricalValidationCompleted += OnCompleted;
            try
            {
                if (backtest.RunCommand.CanExecute(null))
                    await backtest.RunCommand.ExecuteAsync(null).ConfigureAwait(true);

                var deadline = DateTime.UtcNow.AddSeconds(60);
                while (DateTime.UtcNow < deadline && launch is null)
                {
                    await PumpAsync().ConfigureAwait(true);
                    await Task.Delay(200).ConfigureAwait(true);
                }
            }
            finally
            {
                backtest.HistoricalValidationCompleted -= OnCompleted;
            }

            if (launch?.ValidationEvidence is null)
            {
                lines.Add($"FAIL  historical validation did not complete ({backtest.Status})");
                return await WriteAsync(lines, reportPath, 1).ConfigureAwait(false);
            }

            if (!authoring.AcceptHistoricalValidationEvidence(
                    launch.ValidationEvidence,
                    launch.TestedParameters,
                    out var acceptReason))
            {
                lines.Add($"FAIL  accept validation evidence: {acceptReason}");
                return await WriteAsync(lines, reportPath, 1).ConfigureAwait(false);
            }

            lines.Add(
                $"PASS  Validate bound evidence trades={launch.ValidationEvidence.TradeCount} " +
                $"{launch.ValidationEvidence.FromUtc:u}→{launch.ValidationEvidence.ToUtc:u}");

            var books = services.GetRequiredService<PaperExecutionBookManager>();
            bookLease = books.AcquireSelectedSession();
            if (!authoring.BindValidatedPaperBook(bookLease.Book.Id, bookLease.Book.AccountId, out var bindReason))
            {
                lines.Add($"FAIL  bind Paper book: {bindReason}");
                return await WriteAsync(lines, reportPath, 1).ConfigureAwait(false);
            }

            paperVm = new PaperStrategyRunnerViewModel(
                services.GetRequiredService<IBacktestStrategyRegistry>(),
                services.GetRequiredService<IMarketDataHub>(),
                services.GetRequiredService<TradingTerminal.Core.Time.IClock>(),
                services.GetRequiredService<InMemoryLogSink>(),
                bookLease.Session,
                registry,
                bookLease.Book,
                kernelRegistry,
                services.GetRequiredService<IMarketDataIngest>(),
                services.GetRequiredService<IBrokerSelector>(),
                registration,
                launch.TestedParameters);
            paperWindow = services.GetRequiredService<PaperStrategyRunnerWindow>();
            paperWindow.DataContext = paperVm;
            paperWindow.Title = $"E2E · Paper · {bookLease.Book.Name}";
            paperWindow.Show();
            await PumpAsync().ConfigureAwait(true);

            var brokerSelector = services.GetRequiredService<IBrokerSelector>();
            if (!brokerSelector.IsConnected(BrokerKind.Simulated) &&
                brokerSelector.IsAvailable(BrokerKind.Simulated))
            {
                await brokerSelector.ConnectAsync(BrokerKind.Simulated).ConfigureAwait(true);
                await PumpAsync().ConfigureAwait(true);
                lines.Add("PASS  connected Simulated for Paper start");
            }

            await paperVm.StartCommand.ExecuteAsync(null).ConfigureAwait(true);
            await PumpAsync().ConfigureAwait(true);

            var hub = services.GetRequiredService<IMarketDataHub>();
            var clock = services.GetRequiredService<TradingTerminal.Core.Time.IClock>();
            hub.PublishQuote(new Quote(
                aapl.Id, clock.UtcNow, clock.UtcNow,
                99.5, 100.5, 100, 100, BrokerKind.Simulated, 1, false));
            hub.PublishBar(new OhlcvBar(
                aapl.Id, BarSize.OneMinute, clock.UtcNow,
                100, 101, 99, 100, 1000, BrokerKind.Simulated, true));

            var paperDeadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < paperDeadline)
            {
                await PumpAsync().ConfigureAwait(true);
                if (string.Equals(paperVm.BookPosition, "2", StringComparison.Ordinal))
                    break;
                await Task.Delay(150).ConfigureAwait(true);
            }

            var qtyOk = string.Equals(paperVm.BookPosition, "2", StringComparison.Ordinal);
            lines.Add(qtyOk
                ? $"PASS  BOOK POSITION visible qty={paperVm.BookPosition}"
                : $"FAIL  BookPosition={paperVm.BookPosition}; LastMessage={paperVm.LastMessage}");
            exitCode = qtyOk ? 0 : 1;

            if (int.TryParse(Environment.GetEnvironmentVariable("DAXALGO_SMOKE_HOLD_MS"), out var holdMs) &&
                holdMs > 0)
            {
                var holdDeadline = DateTime.UtcNow.AddMilliseconds(Math.Min(holdMs, 60_000));
                while (DateTime.UtcNow < holdDeadline)
                {
                    await PumpAsync().ConfigureAwait(true);
                    await Task.Delay(200).ConfigureAwait(true);
                }
            }
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL  {exception.GetType().Name}: {exception.Message}");
            exitCode = 1;
        }
        finally
        {
            try { paperWindow?.Close(); } catch { /* smoke */ }
            try { paperVm?.Dispose(); } catch { /* smoke */ }
            try { bookLease?.Dispose(); } catch { /* smoke */ }
            try { backtestWindow?.Close(); } catch { /* smoke */ }
            try { backtest?.Dispose(); } catch { /* smoke */ }
            try { authoringWindow?.Close(); } catch { /* smoke */ }
        }

        return await WriteAsync(lines, reportPath, exitCode).ConfigureAwait(false);
    }

    private static async Task<int> WriteAsync(List<string> lines, string reportPath, int exitCode)
    {
        lines.Add(string.Empty);
        lines.Add(exitCode == 0 ? "RESULT: PASS" : "RESULT: FAIL");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllLinesAsync(reportPath, lines).ConfigureAwait(false);
        return exitCode;
    }

    private static async Task PumpAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        Dispatcher.UIThread.RunJobs();
    }

    private static AuthoredUnitSpecificationV1 BuildSpecification(Instrument instrument)
    {
        var classification = new StrategyClassificationBindingV1("buy-once", new string('a', 64));
        var intent = new ConfirmedStrategyIntentV1(
            ConfirmedStrategyIntentV1.CurrentSchemaVersion,
            "intent-1",
            "candidate-1",
            1,
            new string('1', 64),
            new string('2', 64),
            classification,
            new StrategyIntentModelV1(StrategyIntentKindV1.PositionTarget),
            "strategy-requirements/v1",
            [
                new StrategySemanticRequirementV1(
                    "decide-target",
                    StrategySemanticStageV1.DecideIntent,
                    StrategySemanticDispositionV1.Applicable,
                    "Calculate and publish the reviewed target exposure.",
                    true,
                    new StrategyRequirementProvenanceV1(
                        ["candidate-statement-1"],
                        ["research-evidence-1"],
                        "Required by this executable smoke strategy.")),
            ],
            new string('3', 64));
        return new(
            AuthoredUnitSpecificationV1.CurrentSchemaVersion,
            "e2e-buy-once",
            "E2E buy once",
            "Buy two units on the first one-minute bar after research labels.",
            AuthoredUnitSourceKindV1.Text,
            AuthoredUnitKindV1.Strategy,
            [new AuthoredInstrumentRequestV1(
                "primary",
                instrument.CanonicalSymbol,
                instrument.Id,
                ExpectedAssetClass: null,
                PreferredBroker: BrokerKind.Simulated)],
            new AuthoredUnitTimeframeV1("1 minute", TimeSpan.FromMinutes(1)),
            StrategyDataRequirement.Bars,
            [],
            new AuthoredChartCompositionV1(
                [new AuthoredChartPaneV1("price", AuthoredChartPaneRoleV1.Price, 0)],
                [new AuthoredChartLayerV1(
                    "candles",
                    "price",
                    AuthoredChartLayerKindV1.Candles,
                    "price.candles@1",
                    new Dictionary<string, string>())]),
            [],
            [],
            AuthoredUnitExecutionIntentV1.PaperTargets,
            classification,
            intent);
    }

    private static string BuildSource(AuthoredUnitSpecificationV1 specification)
    {
        var hash = AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification);
        return $$"""
            public sealed class GeneratedPaperStrategy : IStrategyKernel, IAuthoredDrawingManifest
            {
                private int _submitted;
                private readonly List<OhlcvBar> _bars = new();
                public static string SpecificationHashSha256 => "{{hash}}";
                public IReadOnlyList<string> DrawingLayerTypeIds => new[] { "price.candles@1" };
                public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
                public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;
                public Task OnStartAsync(IStrategyRuntimeContext context, CancellationToken ct) => Task.CompletedTask;
                public Task OnBarAsync(OhlcvBar bar, IStrategyRuntimeContext context, CancellationToken ct)
                {
                    _bars.Add(bar);
                    if (Interlocked.Exchange(ref _submitted, 1) == 0)
                        context.Book.SetTargetPosition(bar.InstrumentId, 2);
                    return Task.CompletedTask;
                }
                public void Draw(IRenderSurface surface)
                {
                    if (_bars.Count == 0) { surface.Text(8, 18, "Waiting for completed bars"); return; }
                    using (surface.Layer("candles", "price.candles@1")) Candles.Draw(surface, _bars);
                }
            }
            """;
    }
}
