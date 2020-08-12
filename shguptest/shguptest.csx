
[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "shgupTest", Name = "Shgup Web App", Author = "shgup", Description = "")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    await Task.Delay(1);
    res.AddMarkdownView("Hello world!!");

    return res;
}