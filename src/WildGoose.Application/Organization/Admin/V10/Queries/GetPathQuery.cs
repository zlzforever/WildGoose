using System.ComponentModel.DataAnnotations;

namespace WildGoose.Application.Organization.Admin.V10.Queries
{
    public class GetPathQuery
    {
        [StringLength(maximumLength: 20, MinimumLength = 2, ErrorMessage = "查询关键字在 2 到 20 字符之间")]
        public string Keyword { get; set; }
    }
}
