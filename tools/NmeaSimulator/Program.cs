using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NmeaSimulator;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("NMEA Simulator");
        Console.WriteLine("==============");
        Console.WriteLine();

        var host = "127.0.0.1";
        var port = 9001;
        var rateHz = 5.0;

        // Parse command line arguments
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--host" when i + 1 < args.Length:
                    host = args[++i];
                    break;
                case "-p" or "--port" when i + 1 < args.Length:
                    port = int.Parse(args[++i]);
                    break;
                case "-r" or "--rate" when i + 1 < args.Length:
                    rateHz = double.Parse(args[++i]);
                    break;
                case "--help":
                    ShowHelp();
                    return;
            }
        }

        Console.WriteLine($"Target: {host}:{port}");
        Console.WriteLine($"Rate: {rateHz} Hz");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to stop");
        Console.WriteLine();

        using var udpClient = new UdpClient();
        var endpoint = new IPEndPoint(IPAddress.Parse(host), port);

        var intervalMs = (int)(1000 / rateHz);
        var sentenceCount = 0;

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var baseTime = DateTime.UtcNow;
        var random = new Random();

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var sentences = GenerateNmeaSentences(baseTime, random);

                foreach (var sentence in sentences)
                {
                    var bytes = Encoding.ASCII.GetBytes(sentence + "\r\n");
                    await udpClient.SendAsync(bytes, bytes.Length, endpoint);
                    sentenceCount++;

                    Console.WriteLine(sentence);
                }

                Console.WriteLine($"--- Sent {sentences.Length} sentences (total: {sentenceCount}) ---");
                Console.WriteLine();

                baseTime = baseTime.AddSeconds(1.0 / rateHz);
                await Task.Delay(intervalMs, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine();
            Console.WriteLine($"Stopped. Total sentences sent: {sentenceCount}");
        }
    }

    static string[] GenerateNmeaSentences(DateTime time, Random random)
    {
        var timeStr = time.ToString("HHmmss.ff");
        var dateStr = time.ToString("ddMMyy");

        // Simulated position (somewhere in Europe)
        var lat = 48.0 + random.NextDouble() * 0.01;
        var lon = 11.0 + random.NextDouble() * 0.01;

        var latStr = FormatLatitude(lat);
        var lonStr = FormatLongitude(lon);

        var sentences = new List<string>();

        // GGA - Global Positioning System Fix Data
        var gga = $"$GNGGA,{timeStr},{latStr},N,{lonStr},E,1,12,0.8,520.0,M,47.0,M,,";
        sentences.Add(AddChecksum(gga));

        // RMC - Recommended Minimum Specific GNSS Data
        var rmc = $"$GNRMC,{timeStr},A,{latStr},N,{lonStr},E,0.5,45.2,{dateStr},,,A";
        sentences.Add(AddChecksum(rmc));

        // GSA - GNSS DOP and Active Satellites
        var gsa = "$GNGSA,A,3,01,02,03,04,05,06,07,08,09,10,11,12,1.2,0.8,0.9,1";
        sentences.Add(AddChecksum(gsa));

        // VTG - Course Over Ground and Ground Speed
        var vtg = "$GNVTG,45.2,T,,M,0.5,N,0.9,K,A";
        sentences.Add(AddChecksum(vtg));

        // GLL - Geographic Position - Latitude/Longitude
        var gll = $"$GNGLL,{latStr},N,{lonStr},E,{timeStr},A,A";
        sentences.Add(AddChecksum(gll));

        return sentences.ToArray();
    }

    static string FormatLatitude(double lat)
    {
        var degrees = (int)lat;
        var minutes = (lat - degrees) * 60;
        return $"{degrees:D2}{minutes:00.0000}";
    }

    static string FormatLongitude(double lon)
    {
        var degrees = (int)lon;
        var minutes = (lon - degrees) * 60;
        return $"{degrees:D3}{minutes:00.0000}";
    }

    static string AddChecksum(string sentence)
    {
        // Remove $ if present
        var data = sentence.StartsWith("$") ? sentence.Substring(1) : sentence;

        // Calculate checksum (XOR of all bytes)
        byte checksum = 0;
        foreach (char c in data)
        {
            checksum ^= (byte)c;
        }

        return $"{sentence}*{checksum:X2}";
    }

    static void ShowHelp()
    {
        Console.WriteLine("NMEA Simulator - Sends simulated NMEA sentences via UDP");
        Console.WriteLine();
        Console.WriteLine("Usage: NmeaSimulator [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --host <host>   Target host (default: 127.0.0.1)");
        Console.WriteLine("  -p, --port <port>   Target port (default: 9001)");
        Console.WriteLine("  -r, --rate <hz>     Send rate in Hz (default: 5)");
        Console.WriteLine("  --help              Show this help");
    }
}
