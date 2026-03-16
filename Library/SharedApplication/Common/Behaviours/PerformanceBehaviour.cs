using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Behaviours;

[ExcludeFromCodeCoverage]
public class PerformanceBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    private readonly ILogger<TRequest> _logger;

    public PerformanceBehaviour(
        ILogger<TRequest> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Use local Stopwatch to avoid shared state issues and ensure proper cleanup
        var timer = Stopwatch.StartNew();

        try
        {
            var response = await next();
            return response;
        }
        finally
        {
            timer.Stop();

            var elapsedMilliseconds = timer.ElapsedMilliseconds;
            var requestName = typeof(TRequest).Name;

            if (elapsedMilliseconds > 500)
            {
                _logger.LogWarning("QTC - Long Running Request: {Name} ({ElapsedMilliseconds} milliseconds) {@Request}",
                    requestName, elapsedMilliseconds, request);
            }
            else
            {
                _logger.LogInformation("QTC - Normal Running Request: {Name} ({ElapsedMilliseconds} milliseconds) {@Request}",
                    requestName, elapsedMilliseconds, request);
            }
        }
    }
}
