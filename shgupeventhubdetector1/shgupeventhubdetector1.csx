#load "globalgist"
#load "eventhubGist"
using System;

[ArmResourceFilter(provider: "Microsoft.EventHub", resourceTypeName: "namespaces")]
[Definition(Id = "shgupEventHubDetector1", Name = "Shgup Event Hub Detector 1", Author = "shgup", Description = "")]
public async static Task<Response> Run(DataProviders dp, OperationContext<ArmResource> cxt, Response res)
{
    await Task.Delay(1);
    res.AddMarkdownView(EventHubGist.Invoke());
    res.AddMarkdownView(GlobalGist.Invoke(cxt.Resource.SubscriptionId, "EventHubDetector"));
    return res;
}