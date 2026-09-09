namespace Emi.Qms.Api.OsanProjects;

public static class OsanCompletionModes
{
    public const string Individual = "individual";
    public const string Batch = "batch";
}

public sealed record OsanProgressTargetRequest(Guid TargetId, int ExpectedVersion);

public sealed record CompleteOsanProgressInput(
    Guid OperationId,
    string CompletionMode,
    int StageSequence,
    IReadOnlyList<OsanProgressTargetRequest?> Targets,
    IReadOnlyList<OsanProgressPhotoInput> Photos);

public sealed record OsanProgressPhotoInput(
    string FileName,
    string NormalizedMime,
    byte[] Content,
    string Sha256);

public sealed record OsanProgressResponse(
    Guid ProjectId,
    string ProjectCode,
    string Title,
    string Status,
    int CompletedStepCount,
    int TotalStepCount,
    IReadOnlyList<OsanProgressTargetResponse> Targets);

public sealed record OsanProgressTargetResponse(
    Guid TargetId,
    int SequenceNumber,
    string DisplayName,
    string Status,
    int Version,
    DateTimeOffset? StartedAtUtc,
    Guid? StartedByUserId,
    string? StartedByDisplayName,
    IReadOnlyList<OsanProgressStepResponse> Steps);

public sealed record OsanProgressStepResponse(
    Guid StepId,
    int SequenceNumber,
    string StepCode,
    string StepName,
    string Status,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    Guid? CompletedByUserId,
    string? CompletedByDisplayName,
    bool CanCompleteIndividual,
    bool CanCompleteBatch,
    string? GuidanceDescription,
    IReadOnlyList<OsanGuidancePhotoResponse> GuidancePhotos,
    IReadOnlyList<OsanProgressPhotoResponse> Photos);

public sealed record OsanGuidancePhotoResponse(Guid PhotoId, string AltText);

public sealed record OsanProgressPhotoResponse(
    Guid PhotoId,
    int DisplayOrder,
    string FileName,
    string ContentType,
    int SizeBytes,
    string Sha256,
    DateTimeOffset UploadedAtUtc,
    Guid UploadedByUserId,
    string UploadedByDisplayName);

public sealed record OsanProgressMutationResponse(
    Guid OperationId,
    bool Replayed,
    OsanProgressResponse Project);

public enum OsanProgressMutationStatus
{
    Success,
    Validation,
    NotFound,
    Conflict
}

public sealed record OsanProgressMutationResult(
    OsanProgressMutationStatus Status,
    OsanProgressMutationResponse? Value = null,
    string? ErrorCode = null,
    string? Message = null,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static OsanProgressMutationResult Success(OsanProgressMutationResponse value) =>
        new(OsanProgressMutationStatus.Success, value);

    public static OsanProgressMutationResult Validation(
        IReadOnlyDictionary<string, string[]> errors) =>
        new(OsanProgressMutationStatus.Validation, Errors: errors);

    public static OsanProgressMutationResult NotFound() =>
        new(OsanProgressMutationStatus.NotFound);

    public static OsanProgressMutationResult Conflict(string code, string message) =>
        new(OsanProgressMutationStatus.Conflict, ErrorCode: code, Message: message);
}

public sealed record OsanProgressPhotoDownload(
    string FileName,
    string ContentType,
    byte[] Content);
