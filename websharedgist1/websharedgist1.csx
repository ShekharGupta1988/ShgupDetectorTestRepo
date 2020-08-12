using System;
using System.Threading;

[ArmResourceFilter(provider: "Microsoft.Web", resourceTypeName: "*")]
[Definition(Id = "websharedgist1", Name = "Microsoft.Web shared gist 2", Author = "shgup", Description = "")]
public static class WebSharedGist
{
    public static string Invoke(){
        return "Called from New Web Shared Gist";
    }
}