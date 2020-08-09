#load "globalgist"

[HostingEnvironmentFilter(HostingEnvironmentType = HostingEnvironmentType.All, PlatformType = PlatformType.Windows)]
[Definition(Id = "shguptestase", Name = "Shgup ASE Detector", Author = "shgup", Description = "")]
public async static Task<Response> Run(DataProviders dp, OperationContext<HostingEnvironment> cxt, Response res)
{
    await Task.Delay(1);
    res.AddInsight(new Insight(InsightStatus.Critical, "Shgup ASE Detector"));

    res.AddMarkdownView(GlobalGist.Invoke(cxt.Resource.SubscriptionId, "ASE Detector"));
    return res;
}