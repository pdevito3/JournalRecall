using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Mvc;
using JournalRecall.AI.Core.Persistence;
using JournalRecall.Api.Exceptions;
// Disambiguate from the framework's Microsoft.AspNetCore.Http.ProblemDetailsOptions (implicit using).
using ProblemDetailsOptions = Hellang.Middleware.ProblemDetails.ProblemDetailsOptions;

namespace JournalRecall.Api.Extensions;

/// <summary>
/// Maps domain and framework exceptions to RFC 7807 problem+json responses (Hellang ProblemDetails).
/// Exception detail (stack traces) is included only in the Development environment by default.
/// </summary>
public static class ProblemDetailsConfigurationExtension
{
    public static void ConfigureProblemDetails(ProblemDetailsOptions options)
    {
        // Domain validation → 422 Unprocessable Entity: the request is well-formed but semantically
        // invalid (a business-rule failure), as opposed to a malformed/unbindable 400. A
        // ValidationException carries a per-field error map; an InvalidSmartEnumPropertyName is a single
        // "not a valid X" message listing the allowed values.
        options.MapValidationException();
        options.MapToStatusCode<InvalidSmartEnumPropertyName>(StatusCodes.Status422UnprocessableEntity);

        options.MapToStatusCode<ForbiddenAccessException>(StatusCodes.Status401Unauthorized);
        options.MapToStatusCode<NoRolesAssignedException>(StatusCodes.Status403Forbidden);
        options.MapToStatusCode<NotFoundException>(StatusCodes.Status404NotFound);

        // A version clash in the AI conversation store (raised during agent runs that API endpoints
        // trigger) is a write conflict, not a server fault.
        options.MapToStatusCode<ConversationConcurrencyException>(StatusCodes.Status409Conflict);

        options.MapToStatusCode<NotImplementedException>(StatusCodes.Status501NotImplemented);
        options.MapToStatusCode<HttpRequestException>(StatusCodes.Status503ServiceUnavailable);

        // Exceptions are matched polymorphically, so this is the catch-all and MUST stay last:
        // anything not mapped above becomes a 500.
        options.MapToStatusCode<Exception>(StatusCodes.Status500InternalServerError);
    }

    private static void MapValidationException(this ProblemDetailsOptions options) =>
        options.Map<ValidationException>((_, ex) =>
            new ValidationProblemDetails(ex.Errors) { Status = StatusCodes.Status422UnprocessableEntity });
}
