using System;
using System.Threading;

[ArmResourceFilter(provider: "Microsoft.Web", resourceTypeName: "*")]
[Definition(Id = "websharedgist", Name = "Microsoft.Web Shared Gist 2", Author = "shgup", Description = "")]
public static class NewWebGist
{
    public static string Invoke(){
        return "Called from New Microsoft.Web shared Gist";
    }
}