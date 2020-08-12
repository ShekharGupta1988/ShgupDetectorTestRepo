using System;
using System.Threading;

[ArmResourceFilter(provider: "*", resourceTypeName: "*")]
[Definition(Id = "globalgist", Name = "Global Gist", Author = "shgup", Description = "")]
public static class GlobalGist
{
    public static string Invoke()
    {
        return "Invoked from Global Shared Gist";
    }
}