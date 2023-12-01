using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WildGoose.Application.Permission.Internal.V10;
using WildGoose.Application.Permission.Internal.V10.Queries;

namespace WildGoose.Controllers.V10;

[Route("api/v1.1/enforcers")]
[ApiController]
[Authorize]
public class EnforcerController : ControllerBase
{
    private readonly PermissionService _permissionService;
    
    public EnforcerController(PermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> EnforceAsync([FromQuery] EnforceQuery query)
    {
        var result = await _permissionService.EnforceAsync(query);
        return Content(result ? "true" : "false", "text/plain");
    }
}