namespace DaxAlgo.Sdk;

/// <summary>
/// Runtime evidence that an AI-authored visualizer or strategy implements the exact drawing layers
/// reviewed in its authored-unit specification. The compiler compares these ordered stable type ids
/// with the specification and also executes a bounded first-frame draw probe before registration.
/// </summary>
public interface IAuthoredDrawingManifest
{
    /// <summary>Stable layer type ids in the same order as the reviewed drawing composition.</summary>
    IReadOnlyList<string> DrawingLayerTypeIds { get; }
}
