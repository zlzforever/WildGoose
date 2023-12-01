namespace WildGoose.Application;

public static class NameLimiter
{
    public const string Pattern = "[\\w]+";
    public const string Message = "只能使用汉字、英文、下划线，不能有空格、@、￥等特殊字符";
}