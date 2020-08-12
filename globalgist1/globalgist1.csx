using System;
using System.Threading;

[ArmResourceFilter(provider: "*", resourceTypeName: "*")]
[Definition(Id = "globalGist1", Name = "Global Gist 1", Author = "shgup", Description = "")]
public static class NewGlobalGist
{
    public static string Invoke(){
        return "Called from New Global Gist";
    }
}