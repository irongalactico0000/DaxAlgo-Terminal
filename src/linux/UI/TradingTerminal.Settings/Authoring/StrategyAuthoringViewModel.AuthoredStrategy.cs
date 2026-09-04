using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.App.Authoring;

public sealed partial class StrategyAuthoringViewModel
{
    /// <summary>
    /// True when the reviewed intent can be represented by the current SDK virtual-target surface.
    /// Quote sets and execution schedules need richer command capabilities; signal-only work has no
    /// position target and therefore must not be mislabeled as a Paper strategy.
    /// </summary>
    public bool CanonicalPaperStrategyIntentSupported => ConfirmedStrategyIntent?.IntentModel.Kind is
        StrategyIntentKindV1.PositionTarget or
        StrategyIntentKindV1.MultiLegTarget or
        StrategyIntentKindV1.PortfolioTarget;

    public string CanonicalPaperStrategyAvailabilityText => ConfirmedStrategyIntent?.IntentModel.Kind switch
    {
        null => "Confirm the complete strategy request first.",
        StrategyIntentKindV1.PositionTarget => "Build one runnable SDK strategy whose targets can be backtested and sent to a Paper book.",
        StrategyIntentKindV1.MultiLegTarget => "Build one runnable multi-instrument SDK strategy; each reviewed leg remains independently identified.",
        StrategyIntentKindV1.PortfolioTarget => "Build one runnable SDK strategy that emits reviewed per-instrument portfolio targets.",
        StrategyIntentKindV1.SignalOnly => "Signal-only intent has no position target. Build it as a display/alert unit instead of pretending it trades.",
        StrategyIntentKindV1.QuoteSet => "Two-sided quote sets are not representable by the current virtual-target SDK.",
        StrategyIntentKindV1.ExecutionSchedule => "Parent-order schedules are not representable by the current virtual-target SDK.",
        StrategyIntentKindV1.Extension => "This governed extension has no canonical SDK lowering registered.",
        _ => "This intent cannot be lowered to the current Paper target contract.",
    };

    public bool CanGenerateCanonicalPaperStrategy =>
        CanEnterFourLaneConformance &&
        CanonicalPaperStrategyIntentSupported &&
        ChartEvidenceReadyForSpecification &&
        !IsGenerating &&
        _authoredUnitSpecificationGenerator is not null &&
        _authoredUnitSourceGenerator is not null &&
        _instrumentRegistry is not null &&
        SelectedAiProvider is { IsAvailable: true };

    private bool ChartEvidenceReadyForSpecification =>
        !HasUninspectedChartReferences &&
        ChartReferences
            .Where(item => item.Reference.Similarity.Any(static similarity => similarity is
                ChartReferenceSimilarityV1.MarketPattern or ChartReferenceSimilarityV1.RelatedInstruments))
            .All(item => ChartPatternSelections.Any(selection =>
                string.Equals(selection.ReferenceId, item.Reference.ReferenceId, StringComparison.Ordinal) &&
                string.Equals(selection.ReferenceContentHashSha256, item.Reference.ContentHashSha256, StringComparison.Ordinal)));

    [RelayCommand(CanExecute = nameof(CanGenerateCanonicalPaperStrategyAction))]
    private async Task GenerateCanonicalPaperStrategyAsync()
    {
        if (!CanGenerateCanonicalPaperStrategy ||
            ConfirmedStrategyIntent is not { } confirmedIntent ||
            ConfirmedStrategyIntentHash is not { Length: 64 } confirmedIntentHash ||
            CurrentCandidate is not { } candidate ||
            SelectedAiProvider is not { } choice)
        {
            Status = CanonicalPaperStrategyAvailabilityText;
            return;
        }

        var provider = ResolveClient(choice) ?? choice.Client;
        var candidateHash = StrategyCandidateCanonicalJsonV1.Hash(candidate);
        var rawRequest = candidate.RawIntent;
        var instruments = BuildAuthoredUnitInstrumentCandidates(rawRequest);
        if (instruments.Count == 0)
        {
            Status = "No canonical instrument with a usable broker alias matches this confirmed strategy request.";
            return;
        }

        var generationEpoch = Interlocked.Increment(ref _generationContextEpoch);
        var strategyId = StrategyId.Trim();
        _generateCts?.Cancel();
        _generateCts?.Dispose();
        var turnCts = new CancellationTokenSource();
        _generateCts = turnCts;
        IsGenerating = true;
        AiStatus = "Freezing the confirmed strategy into one runnable SDK contract…";
        Append(AuthoringMessage.Tool(
            "Run",
            "Canonical Paper strategy generation",
            $"Confirmed intent {confirmedIntentHash[..12]}… · creating the reviewed instruments, data, parameters, drawing, and target contract."));

        try
        {
            var result = await _authoredUnitSpecificationGenerator!.GenerateAsync(
                provider,
                new AuthoredUnitSpecificationGenerationRequestV1(
                    strategyId,
                    rawRequest,
                    instruments,
                    confirmedIntent,
                    ChartReferences.Select(static item => item.Reference).ToArray(),
                    ChartReferenceInspections.ToArray(),
                    ChartPatternSelections.ToArray()),
                turnCts.Token);
            InputTokens += result.Usage.InputTokens;
            OutputTokens += result.Usage.OutputTokens;
            CachedTokens += result.Usage.CachedInputTokens;

            if (!IsGenerationContextCurrent(generationEpoch, strategyId) ||
                ConfirmedStrategyIntent is null ||
                !string.Equals(confirmedIntentHash, ConfirmedStrategyIntentHash, StringComparison.Ordinal) ||
                !string.Equals(candidateHash, CandidateContentHash, StringComparison.Ordinal))
            {
                AiStatus = "The confirmed strategy changed while its runnable contract was being generated. The returned artifact was discarded.";
                return;
            }

            if (!result.Success || result.Specification is null)
            {
                var detail = string.Join(Environment.NewLine, result.Issues.Select(issue =>
                    $"{issue.Code} · {issue.Path}: {issue.Message}"));
                AiStatus = "The runnable Strategy specification failed closed. No source was generated.";
                Append(AuthoringMessage.Tool("Fail", "Runnable strategy contract rejected", detail));
                return;
            }

            AuthoredUnitSpecification = result.Specification;
            var specificationHash = AuthoredUnitSpecificationCanonicalJsonV1.Hash(result.Specification);
            Append(AuthoringMessage.Tool(
                "Ok",
                "Runnable strategy contract frozen",
                $"{result.Specification.Instruments.Count} instrument(s) · {result.Specification.DataRequirement} · " +
                $"{result.Specification.Drawing.Layers.Count} layer(s) · exact intent {confirmedIntentHash[..12]}… · specification {specificationHash[..12]}…."));

            if (!await GenerateAuthoredUnitSourceAsync(provider, result.Specification, turnCts.Token))
                return;

            ActiveScreen = StrategyAuthoringScreen.Build;
            WorkbenchTab = 0;
        }
        catch (OperationCanceledException) when (turnCts.IsCancellationRequested)
        {
            AiStatus = "Runnable strategy generation stopped; nothing was registered.";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Canonical authored strategy generation failed for {Id}", strategyId);
            AiStatus = $"Runnable strategy generation failed: {exception.Message}";
            Append(AuthoringMessage.Tool("Fail", "Runnable strategy generation failed", exception.Message));
        }
        finally
        {
            if (IsGenerationContextCurrent(generationEpoch, strategyId))
            {
                IsGenerating = false;
                Save();
            }
            if (ReferenceEquals(_generateCts, turnCts)) _generateCts = null;
            turnCts.Dispose();
        }
    }

    private bool CanGenerateCanonicalPaperStrategyAction() => CanGenerateCanonicalPaperStrategy;
}
