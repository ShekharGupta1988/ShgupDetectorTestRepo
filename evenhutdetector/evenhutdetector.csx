#load "globalgist"

using System;
using System.Threading;


[ArmResourceFilter(provider: "Microsoft.EventHub", resourceTypeName: "namespaces")]
[Definition(Id = "evenhutdetector", Name = "Shgup Event Hub Detector", Author = "shgup", Description = "")]
public async static Task<Response> Run(DataProviders dp, OperationContext<ArmResource> cxt, Response res)
{
    await Task.Delay(1);
    res.AddMarkdownView("In Event Hub Detector");

    res.AddMarkdownView(GlobalGist.Invoke());

    return res;
}