using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WildGoose.Application.Permission.Internal.V10;
using WildGoose.Application.Permission.Internal.V10.Queries;
using WildGoose.Domain;

namespace WildGoose.Controllers.V10;

[Route("api/v1.0/permissions")]
[ApiController]
[Authorize]
public class PermissionController(PermissionService permissionService, ILogger<PermissionController> logger)
    : ControllerBase
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    [HttpPost("enforce")]
    public async Task<IActionResult> EnforceAsync([FromBody] List<EnforceQuery> query)
    {
        HttpContext.Request.EnableBuffering();
        var stream = HttpContext.Request.Body;
        stream.Seek(0, System.IO.SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        var body = await reader.ReadToEndAsync();
        logger.LogDebug("Enforce query start: {EnforceQuery}", body);

        if (query.Count == 0)
        {
            logger.LogDebug("Enforce {EnforceQuery}", "[]");
            return Content("[]", "text/plain");
        }

        var builder = new StringBuilder("[");
        for (var i = 0; i < query.Count; ++i)
        {
            var v = await permissionService.EnforceAsync(query[i]);
            builder.Append(v ? "true" : "false");
            if (i != query.Count - 1)
            {
                builder.Append(',');
            }
        }

        builder.Append(']');
        var result = builder.ToString();
        logger.LogDebug("Enforce {EnforceQuery}, {Result}", JsonSerializer.Serialize(query), result);
        return Content(result, "text/plain");
    }
}