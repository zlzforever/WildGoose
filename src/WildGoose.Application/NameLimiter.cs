namespace WildGoose.Application;

public static class NameLimiter
{
    public const string Pattern = "[0-9-_a-zA-Z\\u4e00-\\u9fa5]+";
    public const string Message = "只能使用汉字、英文、中划线、下划线，不能有空格、@、￥等特殊字符";
}