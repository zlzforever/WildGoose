// using System.ComponentModel.DataAnnotations;
//
// namespace WildGoose.Application.Domain.Admin.V10.Command;
//
// public record UpdateDomainCommand
// {
//     /// <summary>
//     /// 标识
//     /// </summary>
//     internal string Id { get; set; }
//
//     /// <summary>
//     /// 名称
//     /// </summary>
//     [Required, StringLength(100),
//      RegularExpression(NameLimiter.Pattern, ErrorMessage = NameLimiter.Message)]
//     public string Name { get; set; }
//
//     /// <summary>
//     /// 描述
//     /// </summary>
//     [StringLength(256)]
//     public string Description { get; set; }
// }