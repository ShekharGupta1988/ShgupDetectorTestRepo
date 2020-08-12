#load "globalGist1"
#load "websharedgist1"

using System;
using System.Threading;

[HostingEnvironmentFilter(HostingEnvironmentType = HostingEnvironmentType.All, PlatformType = PlatformType.Windows)]
[Definition(Id = "shgupasetest", Name = "Shgup ASE Test", Author = "shgup", Description = "")]
public async static Task<Response> Run(DataProviders dp, OperationContext<HostingEnvironment> cxt, Response res)
{
    await Task.Delay(1);
    res.AddMarkdownView(NewGlobalGist.Invoke());
    res.AddMarkdownView(WebSharedGist.Invoke());

    return res;
}