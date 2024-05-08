using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;

public class MPF
{ 

    private const string expectedEvent = "slide_slide4_created";
    private const string mpfWindowTitle = "Mission Pinball Framework";
    private const int pause = 5;
    private TcpClient? client = null;
    private bool quit = false;
    private Mutex quitMutex = new Mutex(false);
    private bool startGame = false;
    private Mutex startGameMutex = new Mutex(false);
    Thread thread;
    bool firstLoop = true;

    /* ********************************************************************************* */
    [DllImport("user32.dll", SetLastError = true)]
    static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);
    public const int KEYEVENTF_EXTENDEDKEY = 0x0001; //Key down flag
    public const int KEYEVENTF_KEYUP = 0x0002; //Key up flag
    public const int ALT = 0xA4; //Alt key code
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    /* ********************************************************************************* */

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
        Thread.Sleep(pause * 1000);
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
                writer.WriteLine(str);
                writer.WriteLine("monitor_start?category=events");
                writer.Flush();
                ReadLoop(reader);

            }
            catch (Exception)
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
            try
            {
                string? str = reader.ReadLine(); // generates an exception when disconnected
                if (str == null)
                    return;
                string prefix = str.Substring(0, expectedPrefix.Length);
                if (prefix == expectedPrefix)
                {
                    string json = str.Substring(expectedPrefix.Length);
                    JsonNode? ev = JsonNode.Parse(json);
                    if (ev == null) continue;
                    string? ev_name = (string?)ev!["event_name"];
                    if (ev_name == null)
                        continue;
                    if (ev_name == expectedEvent)
                    {
                        startGameMutex.WaitOne();
                        startGame = true;
                        startGameMutex.ReleaseMutex();
                    }
                }
            } catch (Exception)
            {
                // probably disconnected
                return;
            }
        }
    }

    private void Foreground(IntPtr hWnd)
    {
        // https://www.roelvanlisdonk.nl/2014/09/05/reliable-bring-external-process-window-to-foreground-without-c/
        keybd_event(ALT, 0x45, KEYEVENTF_EXTENDEDKEY, 0);
        keybd_event(ALT, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
        SetForegroundWindow(hWnd);
    }

    public bool CheckStart()
    {
        startGameMutex.WaitOne();
        bool start = startGame;
        startGameMutex.ReleaseMutex();
        if (start)
        {
            start = false;
            Foreground(Process.GetCurrentProcess().MainWindowHandle);
            return true;
        }
        return false;
    }

    public void ShowMPF()
    {
        IntPtr hWnd = IntPtr.Zero;
        foreach (Process pList in Process.GetProcesses())
        {
            if (pList.MainWindowTitle.Contains(mpfWindowTitle))
                hWnd = pList.MainWindowHandle;
        }
        
        if (hWnd != IntPtr.Zero) 
            Foreground(hWnd);
    }

    public void Close()
    {
        quitMutex.WaitOne();
        quit = true;
        quitMutex.ReleaseMutex();
        client?.Close();
        thread.Join();
    }

}

public class MPFTest
{
    public static void Main()
    {
        MPF mpf = new MPF();
        while(!mpf.CheckStart())
        {
            Thread.Sleep(10);
        }
        Thread.Sleep(5000);
        mpf.ShowMPF();
        mpf.Close();
    }
}