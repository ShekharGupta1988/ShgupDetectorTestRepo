[SupportTopic(Id = "32635057", PesId = "15614")]

private static string GetQuery(OperationContext<App> cxt)
{
    return
    $@"<YOUR_TABLE_NAME>
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | <YOUR_QUERY>";
}
 
[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "shgup_id3", Name = "Shgup Test 3", Author = "shgup", Description = "")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{
    await Task.Delay(1);
    res.AddInsight(new Insight(InsightStatus.Critical, "Detector Invoked - Shgup Test 3, Id: shgup_id3, No Support Topic tagged to Run Method"));


    return res;

}
