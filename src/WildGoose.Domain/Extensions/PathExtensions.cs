using WildGoose.Domain.Entity;

namespace WildGoose.Domain.Extensions;

public static class PathExtensions
{
    /// <param name="list"></param>
    /// <typeparam name="T"></typeparam>
    extension<T>(List<T> list) where T : IPath
    {
        /// <summary>
        /// 排除不在 Parents 列表管理范围的内容
        /// </summary>
        /// <param name="parents"></param>
        /// <returns></returns>
        public IEnumerable<T> GetAvailable(List<string> parents)
        {
            return list.Where(x => parents.Any(y => x.Path.StartsWith(y)));
        }

        public IEnumerable<T> GetAvailable(IEnumerable<string> parents)
        {
            return list.Where(x => parents.Any(y => x.Path.StartsWith(y)));
        }
    }
}