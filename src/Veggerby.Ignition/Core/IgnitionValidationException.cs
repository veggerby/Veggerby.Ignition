using System;
using System.Collections.Generic;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Exception thrown when one or more <see cref="IIgnitionValidator"/> implementations report errors during pre-flight validation.
/// </summary>
/// <remarks>
/// This exception is thrown before any ignition signals execute. Inspect <see cref="ValidationErrors"/>
/// for the full list of messages produced by the failing validators.
/// </remarks>
public sealed class IgnitionValidationException : Exception
{
    /// <summary>
    /// Gets the validation error messages produced by the failed validators.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; }

    /// <summary>
    /// Initializes a new instance with the provided validation error messages.
    /// </summary>
    /// <param name="errors">Non-empty list of validation error messages.</param>
    public IgnitionValidationException(IReadOnlyList<string> errors)
        : base($"Ignition pre-flight validation failed with {errors.Count} error(s): {string.Join("; ", errors)}")
    {
        ArgumentNullException.ThrowIfNull(errors, nameof(errors));

        if (errors.Count == 0)
        {
            throw new ArgumentException("At least one validation error is required.", nameof(errors));
        }

        ValidationErrors = errors;
    }
}
