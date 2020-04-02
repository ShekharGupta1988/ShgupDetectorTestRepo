[SupportTopic(Id = "32635056", PesId = "15614")] 
[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "shgup_id2", Name = "Shgup Test 2", Author = "shgup", Description = "")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    await Task.Delay(1);
    res.AddInsight(new Insight(InsightStatus.Critical, "Detector Invoked - Shgup Test 2, Id: shgup_id2"));


    return res;

}
