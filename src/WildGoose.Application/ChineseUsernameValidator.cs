// using Microsoft.AspNetCore.Identity;
// using WildGoose.Domain;
//
// namespace WildGoose.Application;
//
// public class ChineseUsernameValidator<TUser> : IUserValidator<TUser>
//     where TUser : class
// {
//     public async Task<IdentityResult> ValidateAsync(UserManager<TUser> manager, TUser user)
//     {
//         var errors = new List<IdentityError>();
//         var userName = await manager.GetUserNameAsync(user);
//
//         if (string.IsNullOrWhiteSpace(userName))
//         {
//             errors.Add(new IdentityError
//             {
//                 Code = "UserNameNullOrEmpty",
//                 Description = "用户名不能为空"
//             });
//         }
//         else if (!Defaults.AllowedUserNameRegex.IsMatch(userName))
//         {
//             errors.Add(new IdentityError
//             {
//                 Code = "InvalidUserName",
//                 Description = "用户名只能包含中文、字母、数字、@符号和下划线"
//             });
//         }
//
//         return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed(errors.ToArray());
//     }
// }