using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.CompilerServices;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: StartupChecks <plugin.dll> <Dalamud-directory> [--expect-load-failure]");
    Environment.ExitCode = 2;
    return;
}
var root = Path.GetFullPath(args[1]);
var expectFailure = args.Contains("--expect-load-failure");
ServiceProxy.ConfigDirectory = Path.Combine(Path.GetTempPath(), "MonkLayoutStartupChecks", Guid.NewGuid().ToString("N"));
AssemblyLoadContext.Default.Resolving += (_, name) => File.Exists(Path.Combine(root, name.Name + ".dll")) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(root, name.Name + ".dll")) : null;
var pluginPath=Path.GetFullPath(args[0]);
var asm=AssemblyLoadContext.Default.LoadFromAssemblyPath(pluginPath);
var pluginType=asm.GetType("MonkLayout.Plugin",true)!;
Console.WriteLine($"Testing actual plugin {asm.GetName().Version}");
var propertyNames=new List<string>();
foreach(var p in pluginType.GetProperties(BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public))
{
    if(p.GetCustomAttributesData().Any(a=>a.AttributeType.Name=="PluginServiceAttribute"))
    {
        var proxy=DispatchProxy.Create(p.PropertyType,typeof(ServiceProxy));
        ((ServiceProxy)proxy).Name=p.Name;
        p.SetValue(null,proxy);
        propertyNames.Add(p.Name);
    }
}
Console.WriteLine("Injected service interfaces: "+string.Join(", ", propertyNames));
object? plugin=null;
try
{
    await Task.Run(()=>plugin=Activator.CreateInstance(pluginType));
    Console.WriteLine("CONSTRUCTOR_OK");
}
catch(Exception e)
{
    while(e.InnerException!=null)e=e.InnerException;
    Console.WriteLine($"CONSTRUCTOR_FAILED {e.GetType().FullName}: {e.Message}");
    Console.WriteLine($"OBJECT_READS {ServiceProxy.ObjectReads}");
    var reproduced = ServiceProxy.ObjectReads > 0 && e.Message == "Not on main thread!";
    Environment.ExitCode = expectFailure && reproduced ? 0 : 1;
    if (expectFailure && reproduced) Console.WriteLine("EXPECTED_HISTORICAL_LOAD_FAILURE_REPRODUCED");
    if (Directory.Exists(ServiceProxy.ConfigDirectory)) Directory.Delete(ServiceProxy.ConfigDirectory, true);
    return;
}
Console.WriteLine($"OBJECT_READS {ServiceProxy.ObjectReads}");
if(expectFailure) throw new Exception("Expected a historical load failure, but construction succeeded");
if(ServiceProxy.ObjectReads!=0)throw new Exception("Constructor touched player objects");
if(ServiceProxy.FrameworkUpdate==null)throw new Exception("No framework callback registered");
Console.WriteLine("FRAMEWORK_CALLBACK_REGISTERED");
if(plugin!=null)
{
    // Simulate the game update thread with no logged-in character. No native game state is required.
    ServiceProxy.InFrameworkUpdate=true;
    ServiceProxy.FrameworkUpdate.DynamicInvoke(ServiceProxy.FrameworkService);
    ServiceProxy.InFrameworkUpdate=false;
    Console.WriteLine($"FIRST_FRAMEWORK_UPDATE_OK OBJECT_READS {ServiceProxy.ObjectReads}");
    if(ServiceProxy.ObjectReads!=1)throw new Exception("Framework tick did not refresh the session exactly once");
    var before=ServiceProxy.ObjectReads;
    pluginType.GetMethod("Open",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(plugin,null);
    if(ServiceProxy.ObjectReads!=before)throw new Exception("Open callback accessed player objects");
    Console.WriteLine("OPEN_CALLBACK_OK_WITHOUT_OBJECT_READS");
    ((IDisposable)plugin).Dispose();
    Console.WriteLine("DISPOSE_OK");
}

if (Directory.Exists(ServiceProxy.ConfigDirectory)) Directory.Delete(ServiceProxy.ConfigDirectory, true);

public class ServiceProxy : DispatchProxy
{
    public string Name="";
    public static string ConfigDirectory="";
    public static int ObjectReads;
    public static bool InFrameworkUpdate;
    public static Delegate? FrameworkUpdate;
    public static object? FrameworkService;
    static readonly Dictionary<Type,object> Nested=new();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        var m=method!; args??=[];
        if(Name=="Framework")FrameworkService=this;
        if(Name=="Objects"&&m.Name=="get_LocalPlayer")
        {
            ObjectReads++;
            if(!InFrameworkUpdate)
            {
                // Run the real current-host guard without touching native game pointers.
                var host=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Dalamud");
                host.GetType("Dalamud.Utility.ThreadSafety",true)!.GetMethod("AssertMainThread",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,[null]);
                throw new Exception("Expected the real Dalamud main-thread guard to reject this access");
            }
            return null;
        }
        if(m.Name=="get_IsInFrameworkUpdateThread")return InFrameworkUpdate;
        if(m.Name=="GetPluginConfigDirectory")return ConfigDirectory;
        if(Name=="Framework"&&m.Name=="add_Update"){FrameworkUpdate=(Delegate)args[0]!; return null;}
        if(Name=="Framework"&&m.Name=="remove_Update"){FrameworkUpdate=null; return null;}
        if(m.Name=="get_UiBuilder")
        {
            if(!Nested.TryGetValue(m.ReturnType,out var proxy))
            {
                Console.WriteLine("UiBuilder service type: "+m.ReturnType.FullName+" interface="+m.ReturnType.IsInterface);
                proxy=m.ReturnType.IsInterface?DispatchProxy.Create(m.ReturnType,typeof(ServiceProxy)):RuntimeHelpers.GetUninitializedObject(m.ReturnType);
                Nested[m.ReturnType]=proxy;
            }
            return proxy;
        }
        if(m.ReturnType==typeof(void))return null;
        if(m.ReturnType==typeof(bool))return false;
        if(m.ReturnType==typeof(string))return "";
        return m.ReturnType.IsValueType?Activator.CreateInstance(m.ReturnType):null;
    }
}
