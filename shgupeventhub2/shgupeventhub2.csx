#load "globalGist1"

using System;
using System.Threading;

[ArmResourceFilter(provider: "Microsoft.EventHub", resourceTypeName: "namespaces")]
[Definition(Id = "shgupeventHub2", Name = "Shgup Event Hub Detector 2", Author = "shgup", Description = "")]
public async static Task<Response> Run(DataProviders dp, OperationContext<ArmResource> cxt, Response res)
{
    res.AddMarkdownView(NewGlobalGist.Invoke());
    return res;
}