
[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "webappdetector", Name = "Shgup Web App Detector", Author = "shgup", Description = "")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    await Task.Delay(1);
    res.AddMarkdownView("In Web App Detector");

    return res;
}