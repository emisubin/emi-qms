namespace Emi.Qms.Api.Materials;

public sealed record PanelKittingQueueResponse(
    IReadOnlyList<PanelKittingProjectResponse> Projects);

public sealed record PanelKittingProjectResponse(
    Guid ProjectId,
    string ProjectCode,
    string ProjectTitle,
    int ActiveItemCount,
    int CompletedItemCount,
    bool Ready,
    int PendingPanelCount,
    int CompletedPanelCount,
    IReadOnlyList<PanelKittingPanelResponse> Panels);

public sealed record PanelKittingPanelResponse(
    Guid PanelId,
    string DisplayCode,
    string? PanelName,
    bool PanelInfoCompleted,
    bool KittingCompleted,
    DateTimeOffset? CompletedAtUtc,
    string? CompletedByDisplayName,
    bool Selectable);

public sealed record CompletePanelKittingRequest(
    Guid OperationId,
    Guid ProjectId,
    IReadOnlyList<Guid>? PanelIds);

public sealed record PanelKittingCompletionResponse(
    Guid OperationId,
    int CompletedPanelCount,
    int GeneratedWorkItemCount,
    bool ProjectKittingCompleted,
    bool Replayed);
