using System;
using System.Threading;

[ArmResourceFilter(provider: "*", resourceTypeName: "*")]
[Definition(Id = "globalgist", Name = "Global Gist", Author = "shgup", Description = "")]
public static class GlobalGist
{
    public static string Invoke(string subscriptionId, string caller)
    {
        return $"Global Gist Invoke() called for Subscription : {subscriptionId} by caller : {caller}";
    }
}