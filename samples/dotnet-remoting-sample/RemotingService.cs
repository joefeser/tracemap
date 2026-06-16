using System;
using System.Collections;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;

namespace TraceMap.Samples.LegacyRemoting;

public sealed class RemotingService : MarshalByRefObject
{
    public string Ping()
    {
        return "ok";
    }
}

public static class RemotingHost
{
    public static void Start()
    {
        var properties = new Hashtable
        {
            ["name"] = "synthetic-channel",
            ["port"] = "synthetic-port"
        };
        var channel = new TcpChannel(properties, null, null);
        ChannelServices.RegisterChannel(channel, ensureSecurity: false);
        RemotingConfiguration.RegisterWellKnownServiceType(
            typeof(RemotingService),
            "synthetic-object-uri",
            WellKnownObjectMode.Singleton);
    }
}

public static class RemotingClient
{
    public static object Connect()
    {
        ChannelServices.RegisterChannel(new TcpChannel(), ensureSecurity: false);
        return Activator.GetObject(
            typeof(RemotingService),
            "tcp://synthetic.invalid/synthetic-object-uri");
    }
}
