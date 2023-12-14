using Telegram.Bot.Types;

namespace Helpers;

public static class UserHelpers
{
    public static string GetUserDisplayName(this User user)
    {
        var userName = string.IsNullOrEmpty(user.FirstName) && string.IsNullOrEmpty(user.LastName) 
            ? "Інкогніто" 
            : $"{user.FirstName} {user.LastName}".Trim();
        if (!string.IsNullOrEmpty(user.Username))
            userName += $" (@{user.Username})";
        return userName;
    }
}