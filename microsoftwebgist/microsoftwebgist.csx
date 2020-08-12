using System;
using System.Threading;

[ArmResourceFilter(provider: "Microsoft.Web", resourceTypeName: "*")]
[Definition(Id = "microsoftwebgist", Name = "Microsoft Web Gist", Author = "shgup", Description = "")]
public static class MicrosoftWebGist
{
    public static string Invoke()
    {
        return "Invoked from Microsoft.Web Shared Gist";
    }
}