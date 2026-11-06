using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;

var pipeName = args[0];
var port = args.Length > 1 ? int.Parse(args[1]) : 1433;
var listener = new TcpListener(IPAddress.Loopback, port);
listener.Start();
Console.WriteLine($"listening {port}");

while (true)
{
    var tcp = await listener.AcceptTcpClientAsync();
    _ = RelayAsync(tcp, pipeName);
}

static async Task RelayAsync(TcpClient tcp, string pipeName)
{
    using var client = tcp;
    try
    {
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(5000);
        Console.WriteLine("piped");
        using var stream = client.GetStream();
        var outbound = stream.CopyToAsync(pipe);
        var inbound = pipe.CopyToAsync(stream);
        await Task.WhenAny(outbound, inbound);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.GetType().Name);
    }
}
