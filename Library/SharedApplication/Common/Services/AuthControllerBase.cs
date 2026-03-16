using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Lascodia.Trading.Engine.SharedApplication.Common.Interfaces;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Services;

[Route("api/[controller]")]
[ApiController]
public abstract class AuthControllerBase<T> : ControllerBase where T : AuthControllerBase<T>
{
    protected readonly ILogger<T> Logger;
    protected readonly IConfiguration Config;
    private ISender _mediator;
    protected readonly ICurrentUserService UserService;

    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetService<ISender>();

    public AuthControllerBase(ILogger<T> logger, IConfiguration config, ICurrentUserService userService)
    {
        Logger = logger;
        Config = config;
        UserService = userService;
    }
}
