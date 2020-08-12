using System;
using System.Threading;

[ArmResourceFilter(provider: "*", resourceTypeName: "*")]
[Definition(Id = "testgist", Name = "Sample Gist (Shared)", Author = "shgup", Description = "")]
public static class SampleGist
{
    public static string Invoke()
    {
        return "Invoked from Sample Gist";
    }

}