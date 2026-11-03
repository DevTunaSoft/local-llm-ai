using System.Xml.Linq;

namespace WorkIntelligence.Services;

public static class ScheduledTaskDefinition
{
    public static string Build(string executable,string? entryAssembly,string dataRoot,string userSid,DateTime firstRun)
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(userSid,@"^S-1-[0-9-]+$"))throw new ArgumentException("A Windows user SID is required.");
        var command=StartupOptions.ScheduledCommand(executable,entryAssembly,dataRoot);
        var arguments=command[(executable.Length+3)..];
        XNamespace ns="http://schemas.microsoft.com/windows/2004/02/mit/task";
        XElement E(string name,params object[] content)=>new(ns+name,content);
        var doc=new XDocument(new XDeclaration("1.0","utf-16",null),E("Task",new XAttribute("version","1.4"),
            E("Triggers",E("CalendarTrigger",E("StartBoundary",firstRun.ToString("yyyy-MM-ddTHH:mm:ss",System.Globalization.CultureInfo.InvariantCulture)),E("Enabled","true"),E("ScheduleByDay",E("DaysInterval",1)))),
            E("Principals",E("Principal",new XAttribute("id","User"),E("UserId",userSid),E("LogonType","InteractiveToken"),E("RunLevel","LeastPrivilege"))),
            E("Settings",E("MultipleInstancesPolicy","IgnoreNew"),E("DisallowStartIfOnBatteries","false"),E("StopIfGoingOnBatteries","false"),
                E("StartWhenAvailable","true"),E("ExecutionTimeLimit","PT4H"),E("RestartOnFailure",E("Interval","PT15M"),E("Count",3))),
            E("Actions",new XAttribute("Context","User"),E("Exec",E("Command",executable),E("Arguments",arguments)))));
        return doc.ToString();
    }
}
