namespace EdsDcfNet;

using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using EdsDcfNet.Validation;

/// <summary>
/// Shared pre-write validation for format-specific operations entry points.
/// </summary>
internal static class CanOpenWriteGuard
{
    internal static void EnsureValidForWrite<T>(T model, CanOpenWriteOptions? options)
        => EnsureValidForWrite(model, options, formatRules: null);

    internal static void EnsureValidForWrite<T>(
        T model,
        CanOpenWriteOptions? options,
        Action<object, List<ValidationIssue>>? formatRules)
    {
        if (!ShouldValidateBeforeWrite(options))
            return;

        ValidateKnownModel(model, formatRules);
    }

    internal static Task EnsureValidForWriteAsync<T>(
        T model,
        CanOpenWriteOptions? options,
        CancellationToken cancellationToken = default)
        => EnsureValidForWriteAsync(model, options, formatRules: null, cancellationToken);

    internal static Task EnsureValidForWriteAsync<T>(
        T model,
        CanOpenWriteOptions? options,
        Action<object, List<ValidationIssue>>? formatRules,
        CancellationToken cancellationToken)
    {
        if (!ShouldValidateBeforeWrite(options))
            return Task.CompletedTask;

        return ValidateKnownModelAsync(model, formatRules, cancellationToken);
    }

    internal static bool ShouldValidateBeforeWrite(CanOpenWriteOptions? options) =>
        options?.ValidateBeforeWrite == true;

    private static void ValidateKnownModel<T>(
        T model,
        Action<object, List<ValidationIssue>>? formatRules)
    {
        ThrowIfNull(model, nameof(model));

        var shared = ValidateShared(model!);
        ApplyFormatRules(model!, shared, formatRules);
    }

    private static Task ValidateKnownModelAsync<T>(
        T model,
        Action<object, List<ValidationIssue>>? formatRules,
        CancellationToken cancellationToken)
    {
        ThrowIfNull(model, nameof(model));
        EnsureSupportedModel(model);

        return ValidateAndApplyAsync(model, formatRules, cancellationToken);
    }

    private static async Task ValidateAndApplyAsync<T>(
        T model,
        Action<object, List<ValidationIssue>>? formatRules,
        CancellationToken cancellationToken)
    {
        var shared = await ValidateSharedAsync(model!, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        ApplyFormatRules(model!, shared, formatRules);
    }

    private static IReadOnlyList<ValidationIssue> ValidateShared<T>(T model)
    {
        return model switch
        {
            ElectronicDataSheet eds => CanOpenFile.Validate(eds),
            DeviceConfigurationFile dcf => CanOpenFile.Validate(dcf),
            NodelistProject cpj => CanOpenFile.Validate(cpj),
            _ => throw new ArgumentException(
                "Unsupported model type: " + model!.GetType().Name,
                nameof(model))
        };
    }

    private static Task<IReadOnlyList<ValidationIssue>> ValidateSharedAsync(
        object model,
        CancellationToken cancellationToken)
    {
        return model switch
        {
            ElectronicDataSheet eds => CanOpenFile.ValidateAsync(eds, cancellationToken),
            DeviceConfigurationFile dcf => CanOpenFile.ValidateAsync(dcf, cancellationToken),
            NodelistProject cpj => CanOpenFile.ValidateAsync(cpj, cancellationToken),
            _ => throw new ArgumentException(
                "Unsupported model type: " + model.GetType().Name,
                nameof(model))
        };
    }

    private static void ApplyFormatRules(
        object model,
        IReadOnlyList<ValidationIssue> shared,
        Action<object, List<ValidationIssue>>? formatRules)
    {
        var issues = new List<ValidationIssue>(shared);
        formatRules?.Invoke(model, issues);
        if (issues.Count > 0)
            throw new ModelValidationException(issues);
    }

    private static void EnsureSupportedModel(object? model)
    {
        if (model is ElectronicDataSheet or DeviceConfigurationFile or NodelistProject)
            return;

        throw new ArgumentException(
            "Unsupported model type: " + model!.GetType().Name,
            nameof(model));
    }

    private static void ThrowIfNull(object? value, string paramName)
    {
        if (value is null)
            throw new ArgumentNullException(paramName);
    }
}
