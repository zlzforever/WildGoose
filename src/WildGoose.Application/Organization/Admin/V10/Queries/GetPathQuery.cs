using System.ComponentModel.DataAnnotations;

namespace WildGoose.Application.Organization.Admin.V10.Queries
{
    public class GetPathQuery
    {
        [StringLength(maximumLength: 20, ErrorMessage = "查询关键字不能超过 20 字符")]
        public string Keyword { get; set; }
    }
}
