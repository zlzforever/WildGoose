// using System.ComponentModel.DataAnnotations;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using WildGoose.Application.Domain.V10;
// using WildGoose.Application.Domain.V10.Command;
// using WildGoose.Application.Domain.V10.Dto;
//
// namespace WildGoose.Controllers.V10;
//
// [ApiController]
// [Route("api/v1.0/domains")]
// [Authorize(Roles = "admin")]
// public class DomainController : ControllerBase
// {
//     private readonly ILogger<DomainController> _logger;
//     private readonly DomainService _domainService;
//
//     public DomainController(ILogger<DomainController> logger, DomainService domainService)
//     {
//         _logger = logger;
//         _domainService = domainService;
//     }
//
//     [HttpPost]
//     public async Task<string> AddAsync([FromBody] CreateDomainCommand command)
//     {
//         return await _domainService.AddAsync(command);
//     }
//
//     [HttpPatch("{id}")]
//     public async Task<string> UpdateAsync([FromRoute, Required, StringLength(36)] string id,
//         [FromBody] UpdateDomainCommand command)
//     {
//         command.Id = id;
//         await _domainService.UpdateAsync(command);
//         return id;
//     }
//
//     [HttpDelete("{id}")]
//     public async Task<string> DeleteAsync([FromRoute, Required, StringLength(36)] string id)
//     {
//         await _domainService.DeleteAsync(id);
//         return id;
//     }
//
//     [HttpGet]
//     public async Task<IEnumerable<DomainDto>> GetListAsync()
//     {
//         return await _domainService.GetListAsync();
//     }
//
//     [HttpGet("{domainId}/roles")]
//     public async Task<IEnumerable<RoleDto>> GetListAsync([FromRoute, Required, StringLength(36)] string id)
//     {
//         return await _domainService.GetRoleListAsync(id);
//     }
// }