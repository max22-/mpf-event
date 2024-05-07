using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

public class MPF
{
    private TcpClient? client = null;

    public bool Connect()
    {
        try
        {
            client = new TcpClient();
            client.Connect("127.0.0.1", 5051);
            Stream strm = client.GetStream();
            StreamReader reader = new(strm, Encoding.ASCII);
            StreamWriter writer = new(strm, Encoding.ASCII);

            string? str = reader.ReadLine();
            if(str == null)
            {
                reader.Close();
                writer.Close();
                client.Close();
            }
            str = str?.Split('&')[0];
            Console.WriteLine(str);
            writer.WriteLine(str);
            writer.WriteLine("monitor_start?category=events");
            writer.Flush();
            return true;

        } catch (Exception e)
        {
            client?.Close();
            client = null;
            return false;
        }
    }

    public void Read()
    {
        Stream strm = client?.GetStream();
        StreamReader reader = new(strm, Encoding.ASCII);
        string? str = reader.ReadLine();
        string expectedPrefix = "monitored_event?json=";
        string prefix = str.Substring(0, expectedPrefix.Length);
        if(prefix == expectedPrefix)
        {
            string json = str.Substring(expectedPrefix.Length);
            JsonNode? ev = JsonNode.Parse(json);
            string ev_name = (string)ev!["event_name"];
            Console.WriteLine($"event: {ev_name}");
        }
    }

    public void Close()
    {
        client?.Close();
    }
}

public class MPFTest
{
    public static void Main()
    {
        MPF mpf = new MPF();
        mpf.Connect();
        while(true) {  mpf.Read(); }
        mpf.Close();
    }
}