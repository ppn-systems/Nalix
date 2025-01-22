using Notio.Common.Exceptions;
using Notio.Logging;
using Notio.Network.Http.Core;
using Notio.Network.Http.Exceptions;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Notio.Network.Http.Middleware;

public sealed class ExceptionHandlingMiddleware(NotioLog logger) : MiddlewareBase
{
    private readonly NotioLog _logger = logger;

    protected override async Task HandleAsync(HttpContext context)
    {
        try
        {
            // Cho phép request tiếp tục pipeline
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.Error("Unhandled exception", ex);

            var statusCode = ex switch
            {
                ValidationException => HttpStatusCode.BadRequest,
                UnauthorizedException => HttpStatusCode.Unauthorized,
                NotFoundException => HttpStatusCode.NotFound,
                _ => HttpStatusCode.InternalServerError
            };

            object error = new
            {
                StatusCode = (int)statusCode,
                Details = ex is BaseException baseEx ? baseEx.Details : null,
                ex.Message
            };

            await context.Response.WriteErrorResponseAsync(statusCode, error);

            // Ngăn không cho request tiếp tục pipeline
            throw;
        }
    }
}