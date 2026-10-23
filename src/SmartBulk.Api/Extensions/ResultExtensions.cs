using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartBulk.Domain.Results;

namespace SmartBulk.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult Ok<T>(this Result<T> result)
    {
        if (result.Success)
            return new OkObjectResult(result.Data);

        return HandleError(result);
    }

    public static IActionResult Created<T>(this Result<T> result, string? uri = null)
    {
        if (result.Success)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return new ObjectResult(result.Data) { StatusCode = StatusCodes.Status201Created };

            return new CreatedResult(uri, result.Data);
        }

        return HandleError(result);
    }

    public static IActionResult NoContent<T>(this Result<T> result)
    {
        if (result.Success)
            return new NoContentResult();

        return HandleError(result);
    }

    public static IActionResult Ok(this Result result)
    {
        if (result.Success)
            return new OkObjectResult(new { message = result.Message ?? "Success" });

        return HandleError(result);
    }

    private static IActionResult HandleError(Result result)
    {
        var primaryError = result.Errors.FirstOrDefault() ?? result.Message ?? ErrorMessages.UnknownError;

        if (primaryError == ErrorMessages.EntityNotFound)
        {
            return new NotFoundObjectResult(new
            {
                success = false,
                message = primaryError,
                errors = result.Errors
            });
        }

        return new BadRequestObjectResult(new
        {
            success = false,
            message = primaryError,
            errors = result.Errors
        });
    }
}

