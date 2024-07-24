using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    /// <returns></returns>
    [HttpPost("enforce")]
    public async Task<IActionResult> EnforceAsync()
    {
        using var streamReader = new StreamReader(HttpContext.Request.Body);
        var body = await streamReader.ReadToEndAsync();
        logger.LogDebug("Enforce query start: {EnforceQuery}", body);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var query = JsonSerializer.Deserialize<List<EnforceQuery>>(body, options);
        if (query == null || query.Count == 0)
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