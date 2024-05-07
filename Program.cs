using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

public class MPF
{
    private const string expectedEvent = "slide_slide4_created";
    private TcpClient? client = null;
    private bool quit = false;
    private Mutex quitMutex = new Mutex(false);
    private bool startGame = false;
    private Mutex startGameMutex = new Mutex(false);
    Thread thread;
    bool firstLoop = true;

    public MPF()
    {
        thread = new Thread(new ThreadStart(ThreadProc));
        thread.Start();
    }
    ~MPF()
    {
        quitMutex.Dispose();
        startGameMutex.Dispose();
    }

    public void Stop()
    {
        quitMutex.WaitOne();
        quit = true;
        quitMutex.ReleaseMutex();
    }

    private void Pause()
    {
        int seconds = 10;
        Console.WriteLine($"Waiting for {seconds} seconds...");
        Thread.Sleep(seconds * 1000);
    }

    private void ThreadProc()
    {
        while (true)
        {
            quitMutex.WaitOne();
            bool q = quit;
            quitMutex.ReleaseMutex();
            if (q)
            {
                break;
            }

            if(firstLoop) firstLoop = false; else Pause();

            try
            {
                client = new TcpClient();
                client.Connect("127.0.0.1", 5051);
                Stream strm = client.GetStream();
                StreamReader reader = new(strm, Encoding.ASCII);
                StreamWriter writer = new(strm, Encoding.ASCII);
                string? str = reader.ReadLine();
                if (str == null)
                {
                    reader.Close();
                    writer.Close();
                    client.Close();
                    continue;
                }
                str = str?.Split('&')[0];
                Console.WriteLine(str);
                writer.WriteLine(str);
                writer.WriteLine("monitor_start?category=events");
                writer.Flush();
                //writer.Dispose();
                ReadLoop(reader);

            }
            catch (Exception e)
            {
                client?.Close();
                client = null;
            }
        }
        client?.Close();
    }

    public void ReadLoop(StreamReader reader)
    {
        string expectedPrefix = "monitored_event?json=";
        while (true)
        {
            string? str = reader.ReadLine();
            if (str == null)
                return;
            string prefix = str.Substring(0, expectedPrefix.Length);
            if (prefix == expectedPrefix)
            {
                string json = str.Substring(expectedPrefix.Length);
                JsonNode? ev = JsonNode.Parse(json);
                string? ev_name = (string)ev!["event_name"];
                if (ev_name == null)
                    continue;
                Console.WriteLine($"event: {ev_name}");
                if(ev_name == expectedEvent)
                {
                    startGameMutex.WaitOne();
                    startGame = true;
                    startGameMutex.ReleaseMutex();
                }
            }
        }
    }

    public void WaitForEvent()
    {
        while (true)
        {
            startGameMutex.WaitOne();
            bool start = startGame;
            startGameMutex.ReleaseMutex();
            if (start) return; else Thread.Sleep(10);
        }
    }

    public void Close()
    {
        client?.Close();
        quitMutex.WaitOne();
        quit = true;
        quitMutex.ReleaseMutex();
    }
}

public class MPFTest
{
    public static void Main()
    {
        MPF mpf = new MPF();
        mpf.WaitForEvent();
        Console.WriteLine("Starting game");
        mpf.Close();
    }
}