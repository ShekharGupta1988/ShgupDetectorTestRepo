#load "functionversion"
#load "changeanalysisgist"
#load "DetectorUtils"

using System;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Diagnostics.DataProviders;
using Diagnostics.ModelsAndUtils;
using Diagnostics.ModelsAndUtils.Attributes;
using Diagnostics.ModelsAndUtils.Models;
using Diagnostics.ModelsAndUtils.Models.ResponseExtensions;
using Diagnostics.ModelsAndUtils.ScriptUtilities;
using Newtonsoft.Json;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Text;

private static List<RuntimeSitenameTimeRange> slotRuntimeRange;

[AppFilter(AppType = AppType.All, PlatformType = PlatformType.Windows, StackType = StackType.All,  InternalOnly = false)]
[Definition(Id = "appcrashes", Name = "Application Crashes",AnalysisType="appDownAnalysis,perfAnalysis", Category = Categories.AvailabilityAndPerformance, Author = "shgup,puneetg", Description = "Detects Application crashes and related events for your application.")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{   
    // TEMPORARY: Testing change analysis for function app. 

    if(cxt.Resource.AppType == AppType.FunctionApp && (cxt.Resource.Name.Equals("change-analysis-test") || cxt.Resource.Name.Equals("change-analysis-test-con"))) {   
        await ChangeAnalysis.AddChangeAnalysis(dp, cxt, res);
        return res;
    }
    // This is a workaround to exclude V2 function app, since there might be false positive in this detector.
    if(cxt.Resource.AppType == AppType.FunctionApp && !cxt.Resource.Name.EndsWith("-ak-change"))
    {
        bool isV1FunctionApp = await GetFunctionAppVersion(dp, cxt);
        if (!isV1FunctionApp)
        {
            AddNotSupportedInsightToResponse(res);
            return res;
        }
    }

    var aspnetCrashesTask = dp.Kusto.ExecuteQuery(GetAspNetCrashesQuery(cxt), cxt.Resource.Stamp.InternalName, null, "GetAspNetCrashesQuery");
    var crashInfo = await GetCrashInformation(dp, cxt, res);

    DataTable crashTimelineTable = crashInfo.Item1;
    var filteredEvents = crashInfo.Item2;
    
    bool crashDetected = crashTimelineTable != null && crashTimelineTable.Rows != null && crashTimelineTable.Rows.Count > 0;

    if (crashDetected)
    {
         List<EventLog> eventLogs = await GetEventLogs(dp, cxt, res);
        var aspnetCrashes = await aspnetCrashesTask;
        AddCrashInsightToResponse(res, cxt, filteredEvents, aspnetCrashes.Rows.Count > 0);
        AddCrashTimeLineToResponse(crashTimelineTable, res);
        AddUnhandledExceptionsToResponse(aspnetCrashes, cxt, res);
        AddEventLogsToResponse(eventLogs, cxt.Resource, res);
    }
    else
    {
        AddNoCrashInsightToResponse(res);
    }

    AddUsefulLinksToResponse(res);

    return res;
}

static string GetEventLogsHtmlTable(DataTable dt)
{

    var htmlTableBuilder = new StringBuilder(@"<table style='width:100%'>
                        <tr>
                            <th>
                                SiteName
                            </th>
                            <th>
                                Exceptions
                            </th>
                            <th>
                                Count
                            </th>
                        </tr>");

    foreach (DataRow row in dt.Rows)
    {
        htmlTableBuilder.AppendLine($@"<tr>
                                        <td>
                                           {row["SiteName"]}
                                        </td>
                                        <td> An unhandled exception occurred and the process was terminated
                                           <pre>{CleanupStackTraceFunctions(row["Exception"].ToString())}</pre>
                                        </td>
                                        <td>
                                            {row["Errors"]}
                                        </td>
                                    </tr>");
    }

    htmlTableBuilder.AppendLine("</table>");

    return htmlTableBuilder.ToString();
}

private static void AddUnhandledExceptionsToResponse(DataTable tbl, OperationContext<App> cxt, Response res)
{
    if (tbl.Rows.Count > 0)
    {
        var markdownEventLogs = new StringBuilder();
        markdownEventLogs.AppendLine("The below table shows the un-handled exceptions that caused the process to crash.");
        markdownEventLogs.AppendLine("");

        var exceptionsMarkdown = GetEventLogsHtmlTable(tbl);
        markdownEventLogs.AppendLine(exceptionsMarkdown);
        markdownEventLogs.AppendLine("");

        res.AddMarkdownView(markdownEventLogs.ToString(), "Unhandled ASP.NET exceptions detected");
    }
}

private static async Task<bool> GetFunctionAppVersion(DataProviders dp, OperationContext<App> cxt)
{
    string version = await FunctionVersion.GetFunctionVersion(dp, cxt);
    return version.StartsWith("1");
}

private static string GetAspNetCrashesQuery(OperationContext<App> cxt)
{
    return
    $@"set query_results_cache_max_age = time(1d);AntaresWebWorkerEventLogs
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | where EventSource startswith 'ASP.NET' and RawValue contains '<EventID>1325</EventID>'
        | where SiteName =~ '{cxt.Resource.Name}' or SiteName startswith '{cxt.Resource.Name}__'
        | parse RawValue with * "">IIS APPPOOL\\"" siteRunTimeName ""<"" *
        | extend SiteName = iif(siteRunTimeName =='',SiteName,siteRunTimeName)
        | extend EventXml = parse_xml(RawValue)
        | extend EventIdAspNet = tolong(EventXml.Event.System.EventID)
        | extend Exception = tostring(EventXml.Event.EventData.Data)
        | summarize Errors=count() by SiteName, Exception
        | order by Errors desc
        | take 10";
}

static string CleanupStackTraceFunctions(string callStack)
{
    var stacks = callStack.Split("at ");

    for(int i = 0; i < stacks.Length; i++)
    {
        var startIndex = stacks[i].IndexOf("(");

        if (startIndex > 0 && startIndex < stacks[i].Length)
        {
            stacks[i] = stacks[i].Substring(0,startIndex);
        }
    }
    var callstack = string.Join(Environment.NewLine + "at ",stacks);

    return System.Net.WebUtility.HtmlEncode(callstack); 
}


private static async Task<Tuple<DataTable, List<SiteCrashEvent>>> GetCrashInformation(DataProviders dp, OperationContext<App> cxt, Response res)
{
    string slotName = cxt.Resource.Slot;
    var slotInfoTask = dp.Observer.GetRuntimeSiteSlotMap(cxt.Resource.Stamp.InternalName, cxt.Resource.Name);
    DataTable kustoTable = await dp.Kusto.ExecuteQuery(GetCrashTimeLineQuery(cxt), cxt.Resource.Stamp.InternalName, null, "GetCrashTimeLineQuery");
    if (kustoTable == null || kustoTable.Rows == null || kustoTable.Rows.Count <= 0)
    {
        return Tuple.Create<DataTable,List<SiteCrashEvent>>(kustoTable, null);
    }

    var slotInfo = await slotInfoTask;
    if (!slotInfo.ContainsKey(slotName))
    {
        throw new Exception($"RuntimeSlotMap Dictionary doesnt have key for slot name : {slotName}");
    }

    slotRuntimeRange = slotInfo[slotName];
    List<SiteCrashEvent> crashEvents = new List<SiteCrashEvent>();
    foreach (DataRow row in kustoTable.Rows)
    {
        string siteName = row["SiteName"].ToString();
        if (!string.IsNullOrWhiteSpace(row["ApplicationPool"].ToString()))
        {
            siteName = row["ApplicationPool"].ToString();
        }

        crashEvents.Add(new SiteCrashEvent()
        {
            TimeStamp = GetDateTimeInUtcFormat(DateTime.Parse(row["PreciseTimeStamp"].ToString())),
            SiteName = siteName,
            ExitCode = row["ExitCode"].ToString()
        });
    }

    List<SiteCrashEvent> filteredEvents = MergetSlotTimeAndSiteEvents<SiteCrashEvent>(crashEvents, slotRuntimeRange);

    var table = ToDataTable(filteredEvents, TimeSpan.FromMinutes(5));

    return Tuple.Create(table, filteredEvents);

}


private static void AddNotSupportedInsightToResponse(Response res)
{
    res.AddInsight(new Insight(InsightStatus.Info, "Currently this detector is not suppported for V2 Function Application"));
}

private static void AddNoCrashInsightToResponse(Response res)
{
    res.AddInsight(new Insight(InsightStatus.Success, "No Application crashes detected during this timeframe"));
}

private static void AddCrashInsightToResponse(Response res, OperationContext<App> cxt, List<SiteCrashEvent> filteredEvents, bool unhandledExceptionsAspnet = false)
{
    var insightDetails = new Dictionary<string, string>
    {
        { "Description", $"<markdown>We detected crashes in your application `{cxt.Resource.Name}`.</markdown>" },        
        { "Next Steps", $"<markdown>Please check the event logs table below to see if there are any uncaught exceptions might be causing this.<br />Also, you can check out useful Links section to explore some of the other options that can help diagnose crashes in your application.</markdown>"}
    };

    string exitCodes = string.Empty;
    if (filteredEvents !=null)
    {
        var crashesByExitCode = from c in filteredEvents
              group c.ExitCode by c.ExitCode into g
              select new { g.Key, Count = g.ToList().Count() };

        insightDetails.Add ("Exception Code" , $"<markdown>`{string.Join(", " , crashesByExitCode.Select(x=>GetExceptionCodeInfo(x.Key, x.Count)))}`</markdown>");
        if (unhandledExceptionsAspnet)
        {
            insightDetails["Exception Code"] = $"<markdown>`{string.Join(", " , crashesByExitCode.Select(x=>GetExceptionCodeInfo(x.Key, x.Count)))}`  . Check the **Un-handled ASP.NET Exceptions table** below for more details.</markdown>";
        }

        exitCodes = string.Join(",", crashesByExitCode.Select(x=> x.Key));

        if (!string.IsNullOrWhiteSpace(exitCodes))
        {
            exitCodes = "Exception Code " + exitCodes;
        }
    }
    
    Insight crashInsight = new Insight(InsightStatus.Critical, $"We detected Application crashes during this timeframe. {exitCodes}", insightDetails, true);
    res.AddInsight(crashInsight);
}

private static string GetExceptionCodeInfo(string exceptionCodeInHex, int count)
{
    
    var exceptionCodes = new Dictionary<string, string>()
    {
        {"0xE0434F4D","CLR Exception"},
        {"0xE0434352", "CLR Exception"},
        {"0x80000002","Datatype Misalignment"},
        {"0x80000003","Breakpoint Exception"},
        {"0xC0000005","Native Access Violation"},
        {"0xC0000006","In Page Error"},
        {"0xC0000017","Not Enough Quota"},
        {"0xC000001D","Illegal Instruction"},
        {"0xC000008C","Array bounds Exceeded"},
        {"0xC000008D","Floating Point Denormal Operand"},
        {"0xC000008E","Floating Point Division by Zero"},
        {"0xC000008F","Visual Basic / Floating Point Inexact Result"},
        {"0xC0000090","Floating Point-Invalid Operation"},
        {"0xC0000091","Floating-Point Overflow"},
        {"0xC0000092","Floating-Point Stack-Check"},
        {"0xC0000093","Floating-Point Underflow"},
        {"0xC0000094","Integer Division By Zero"},
        {"0xC0000095","Integer Overflow"},
        {"0xC0000096","Priviliged Instruction"},
        {"0xC00000FD","Stack Overflow"},
        {"0xC0000135","Unable to locate DLL"},
        {"0xC0000138","Ordinal Not Found"},
        {"0xC0000139","Entry Point Not Found"},
        {"0xC0000142","DLL Initialization Failed"},
        {"0xC06D007E","Module Not Found"},
        {"0xC06D007F","Procedure Not Found"},
        {"0xC0020001","The string binding is invalid"},
    };
    if (exceptionCodes.ContainsKey(exceptionCodeInHex))
    {
        exceptionCodeInHex += $" - {exceptionCodes[exceptionCodeInHex]}";
    }
    else
    {
        int errorCodeInt = Convert.ToInt32(exceptionCodeInHex , 16);
        System.ComponentModel.Win32Exception win32Ex = new System.ComponentModel.Win32Exception(errorCodeInt);
        exceptionCodeInHex +=  $" - {win32Ex.Message}"; 
    }
    if (count == 1)
    {
        exceptionCodeInHex = $"{count} crash due to ({exceptionCodeInHex})";
    }
    else
    {
        exceptionCodeInHex = $"{count} crashes due to ({exceptionCodeInHex})";
    }
    
    return exceptionCodeInHex;
}

private static void AddCrashTimeLineToResponse(DataTable crashTimeLineTable, Response res)
{
    res.Dataset.Add(new DiagnosticData()
    {
        Table = crashTimeLineTable,
        RenderingProperties = new TimeSeriesRendering()
        {
            GraphType = TimeSeriesType.BarGraph,
            Title = "Application Crashes Timeline",
            GraphOptions = new
            {
                color = new string[] { "#b20505" }
            }
        }
    });
}

private static string GetCrashTimeLineQuery(OperationContext<App> cxt)
{
    string runtimeSiteName = GetMainSiteName(cxt.Resource.Name);

    return
    $@"AntaresRuntimeWorkerEvents
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}
        | where ApplicationPool =~ '{runtimeSiteName}' or ApplicationPool startswith '{runtimeSiteName}__' or SiteName =~ '{runtimeSiteName}' or SiteName startswith '{runtimeSiteName}__'
        | where EventId == 15013 and ExitCode != -3 and ExitCode != -2 and ExitCode != -1
        | project PreciseTimeStamp, ApplicationPool, SiteName, ExitCode = strcat('0x',toupper(tohex(toint(ExitCode), 8)))
    ";
}

private static async Task<List<EventLog>> GetEventLogs(DataProviders dp, OperationContext<App> cxt, Response res)
{
    try
    {
        List<EventLog> eventLogs = new List<EventLog>();
        DataTable kustoTable = await dp.Kusto.ExecuteQuery(GetEventLogsQuery(cxt), cxt.Resource.Stamp.InternalName, null, "GetEventLogsQuery");
        if (kustoTable == null || kustoTable.Rows == null || kustoTable.Rows.Count <= 0)
        {
            return eventLogs;
        }

        foreach (DataRow row in kustoTable.Rows)
        {
            eventLogs.Add(CreateEventLogRecord(row["RawValue"].ToString(),
                GetDateTimeInUtcFormat(DateTime.Parse(row["PreciseTimeStamp"].ToString())),
                row["SiteName"].ToString()));
        }

        return MergetSlotTimeAndSiteEvents<EventLog>(eventLogs, slotRuntimeRange);
    }
    catch(XmlException)
    {
        string eventLogLink = $"https://{cxt.Resource.Name}.scm.azurewebsites.net/api/vfs/LogFiles/eventlog.xml";

        res.AddMarkdownView($@"
        ### Unable to Parse Event Log File :(

        - It looks like Event Log file is malformed and we are not able to parse it.

        - However, you can still view the EventLog.Xml file <a href=""{eventLogLink}"" target=""_blank"">here.</a>
        ",
        "Application Event Logs");

        return null;
    }
    
}

private static string GetEventLogsQuery(OperationContext<App> cxt)
{
    string runtimeSiteName = GetMainSiteName(cxt.Resource.Name);

    return
    $@"AntaresWebWorkerEventLogs
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}
        | where SiteName =~ '{runtimeSiteName}' or SiteName startswith '{runtimeSiteName}__'
        | project PreciseTimeStamp, RawValue, SiteName
        | where RawValue !contains '<Provider Name="".NET Runtime""/>'
        | top 100 by PreciseTimeStamp desc
    ";
}

private static EventLog CreateEventLogRecord(string eventLogXmlEntry, DateTime timeStamp, string siteName)
{
    EventLog eventLog = new EventLog()
    {
        TimeStamp = timeStamp,
        SiteName = siteName
    };

    XElement logXml = XElement.Parse(eventLogXmlEntry);
    XElement system = logXml.Element("System");
    XElement computer = system.Element("Computer");
    XElement level = system.Element("Level");

    eventLog.Instance = computer.Value;
    eventLog.Level = Convert.ToInt32(level.Value);

    XElement eventData = logXml.Element("EventData");

    List<XElement> dataList = eventData.Elements("Data").ToList();
    if (dataList.Count() == 1)
    {
        eventLog.Data = $@"<div style=""overflow-wrap: break-word;width:75%"">{Regex.Replace(dataList.First().Value, @"\n|\r", "<br />")}</div>";
    }
    else if (dataList.Count > 1)
    {
        eventLog.Data = $@"<div style=""overflow-wrap: break-word;width:75%"">{dataList[1].Value}<br />";

        for(int iter = 2; iter < dataList.Count; iter++)
        {
            if (isValidEventEntry(dataList[iter].Value))
            {
                eventLog.Data += $"{Regex.Replace(dataList[iter].Value, @"\n|\r", "<br />")}<br />";
            }
        }
        
    }

    return eventLog;
}

private static void AddEventLogsToResponse(List<EventLog> eventLogs, App resource, Response res)
{
    string markdown = "";
    int thresholdLimit = 5;

    if (eventLogs == null || eventLogs.Count == 0)
    {
        markdown = @"
        #### No Event Logs found for this application.
        ";
    }
    else
    {
        string eventLogLink = $"https://{resource.Name}.scm.azurewebsites.net/api/vfs/LogFiles/eventlog.xml";
        markdown = $@"
            <div>
                <a style=""text-decoration:underline"" href=""{eventLogLink}"" target=""_blank"">View Full EventLog</a>
                <span style=""float:right"">
                    {EventLogIcons.Critical} Critical,
                    &nbsp;&nbsp;{EventLogIcons.Error} Error,
                    &nbsp;&nbsp;{EventLogIcons.Warning} Warning,
                    &nbsp;&nbsp;{EventLogIcons.Info} Info,
                    &nbsp;&nbsp;{EventLogIcons.Verbose} Verbose
                </span>
                
            </div>
            
            
            | Level | Timestamp (UTC) | Instance | EventData |
            | --- | --- | --- | --- |";
        bool isLimitedExceeded = false;
        int recordLimit = eventLogs.Count;
        eventLogs = eventLogs.OrderByDescending(p => p.TimeStamp).ToList();
        if (eventLogs.Count > thresholdLimit)
        {
            recordLimit = thresholdLimit;
            isLimitedExceeded = true;
        }

        for (int iter = 0; iter < recordLimit; iter++)
        {
            string timeStamp = $"{eventLogs[iter].TimeStamp.ToShortDateString()} {eventLogs[iter].TimeStamp.ToShortTimeString()}";
            string icon = GetLevelIcon(eventLogs[iter].Level);
            string eventData = eventLogs[iter].Data;
            string instance = eventLogs[iter].Instance;

            markdown += $@"
            | {icon} | {timeStamp} | {instance} | {eventData} |";
        }

        if (isLimitedExceeded)
        {
            markdown += $@"
            <pre>+ {eventLogs.Count - recordLimit} more events. <a style=""text-decoration:underline"" href=""{eventLogLink}"" target=""_blank"">View Full EventLog</a></pre>
            ";
        }

        res.AddMarkdownView(markdown, "Application Event Logs");
    }
}

private static string GetLevelIcon(int level)
{
    switch (level)
    {
        case 1:                             // Error
            return EventLogIcons.Error;
        case 2:                             // Warning
            return EventLogIcons.Warning;
        case 4:                             // Info
            return EventLogIcons.Info;
        default:                            // Verbose, Undefined, etc..
            return EventLogIcons.Verbose;
    }
}

private static bool isValidEventEntry(string entry)
{
    if (string.IsNullOrWhiteSpace(entry) || Int32.TryParse(entry, out int tempInt) || Guid.TryParse(entry, out Guid tempGuid) || DateTime.TryParse(entry, out DateTime temp))
    {
        return false;
    }
    else if(entry.Contains(@"/LM/") || entry.Equals("w3wp.exe") || entry.Equals("/") || entry.Equals("Full") || entry.StartsWith("RD") || entry.StartsWith("IIS APPPOOL") || entry.Equals("False") || entry.Equals("True") || entry.Contains("|"))
    {
        return false;
    }

    return true;
}

private static void AddUsefulLinksToResponse(Response res)
{
    res.AddMarkdownView(@"
        The following links can help you diagnose crashes in your application:-

        - <a href=""https://blogs.msdn.microsoft.com/asiatech/2015/12/28/use-crash-diagnoser-site-extension-to-capture-dump-for-intermittent-exception-issues-or-performance-issues-on-azure-web-app/"" target=""_blank"">How to capture intermittent exceptions on Azure Web App.</a>

        - <a href=""https://blogs.msdn.microsoft.com/asiatech/2016/01/12/how-to-use-crash-diagnoser-to-capture-stack-overflow-exception-dump-in-mvc-web-app-on-microsoft-azure/"" target=""_blank"">Troubleshoot Stack Overflow Exceptions on Azure Web App.</a>

        - <a href=""https://blogs.msdn.microsoft.com/asiatech/2016/01/14/tips-of-using-crash-diagnoser-on-azure-web-app/"" target=""_blank"">Tips of using Crash Diagnoser on Azure Web App.</a>

        - <a href =""https://blogs.msdn.microsoft.com/asiatech/2016/01/20/how-to-capture-dump-when-intermittent-high-cpu-happens-on-azure-web-app/"" target=""_blank"">How to capture High CPU dump on Azure Web App.</a>
        ",
        "Some Useful Links");
}

/*
    * =================================================================================
    * Below Region consists of Generic Utilities
    * =================================================================================
    */

#region Utilities
private static string GetMainSiteName(string resourceName)
{
    string mainSiteName = resourceName;
    int index = mainSiteName.IndexOf("(");
    if (index > 0)
        mainSiteName = mainSiteName.Substring(0, index);

    return mainSiteName;
}

private static List<T> MergetSlotTimeAndSiteEvents<T>(List<T> siteEvents, List<RuntimeSitenameTimeRange> slotTimeRange) where T : ISiteEvent
{
    List<T> slotSiteEvents = new List<T>();

    foreach (var siteEvent in siteEvents)
    {
        foreach (var timeRange in slotTimeRange)
        {
            if (timeRange.RuntimeSitename != null && timeRange.StartTime < siteEvent.TimeStamp && siteEvent.TimeStamp < timeRange.EndTime
                && siteEvent.SiteName.ToLower() == timeRange.RuntimeSitename.ToLower())
                slotSiteEvents.Add(siteEvent);
        }
    }

    return slotSiteEvents.OrderBy(p => p.TimeStamp).ToList();
}

private static DateTime GetDateTimeInUtcFormat(DateTime dateTime)
{
    if (dateTime.Kind == DateTimeKind.Unspecified)
    {
        return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond, DateTimeKind.Utc);
    }

    return dateTime.ToUniversalTime();
}

private static DataTable ToDataTable(List<SiteCrashEvent> events, TimeSpan binTimeSpan)
{
    DataTable dt = new DataTable("Table_0");
    dt.Columns.Add(new DataColumn("TimeStamp", Type.GetType("System.DateTime")));
    dt.Columns.Add(new DataColumn("Crashes", Type.GetType("System.Int32")));

    if (events == null || events.Count == 0)
    {
        return dt;
    }

    var groupedEvents = events.GroupBy(p => p.TimeStamp.Ticks / binTimeSpan.Ticks);
    foreach (var entry in groupedEvents)
    {
        DataRow newRow = dt.NewRow();
        newRow[0] = GetDateTimeInUtcFormat(new DateTime(entry.Key * binTimeSpan.Ticks));
        newRow[1] = entry.ToList().Count();

        dt.Rows.Add(newRow);
    }

    dt.AcceptChanges();
    return dt;
}

#endregion

/*
    * =================================================================================
    * Below Region consists of Models used in this detector
    * =================================================================================
    */

#region Models

private interface ISiteEvent
{
    string SiteName { get; set; }
    DateTime TimeStamp { get; set; }
}

private class SiteCrashEvent : ISiteEvent
{
    public string SiteName { get; set; }

    public DateTime TimeStamp { get; set; }

    public string ExitCode {get; set;}
}

private class EventLog : ISiteEvent
{
    public string SiteName { get; set; }

    public DateTime TimeStamp { get; set; }

    public int Level { get; set; }

    public string Instance { get; set; }

    public string Data { get; set; }
}

private static class EventLogIcons
{
    public static string Critical = @"<i class=""fa fa-times-circle"" style=""color:#ce4242"" aria-hidden=""true""></i>";

    public static string Error = @"<i class=""fa fa-exclamation-circle"" style=""color:red"" aria-hidden=""true""></i>";

    public static string Warning = @"<i class=""fa fa-exclamation-triangle"" style=""color:#ff9104"" aria-hidden=""true""></i>";

    public static string Info = @"<i class=""fa fa-info-circle"" style=""color:#3a9bc7"" aria-hidden=""true""></i>";

    public static string Verbose = @"<i class=""fa fa-exclamation-circle"" style=""color:#a9abad"" aria-hidden=""true""></i>";
}

#endregion


