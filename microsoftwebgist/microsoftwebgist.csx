
[ArmResourceFilter(provider: "Microsoft.Web", resourceTypeName: "*")]
[Definition(Id = "microsoftwebGist", Name = "Microsoft.Web Gist", Author = "shgup", Description = "")]
public static class MicrosoftWebGist {
    public static string Invoke()
    {
        return "string from Microsoft.Web sharable gist";
    }
}
