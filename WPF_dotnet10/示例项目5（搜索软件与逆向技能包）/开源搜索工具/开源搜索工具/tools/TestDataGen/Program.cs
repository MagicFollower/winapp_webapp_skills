using System.Diagnostics;

string root = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "IndexApp1M");
int dirs = args.Length > 1 && int.TryParse(args[1], out int parsedDirs) ? parsedDirs : 1000;
int perDir = args.Length > 2 && int.TryParse(args[2], out int n) ? n : 1000;

Directory.CreateDirectory(root);
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("generating " + (dirs * perDir).ToString("N0") + " files under " + root);

byte[] payload = new byte[16];
var watch = Stopwatch.StartNew();
long created = 0;

for (int d = 0; d < dirs; d++)
{
    string dir = Path.Combine(root, "d" + d.ToString("D4"));
    Directory.CreateDirectory(dir);

    for (int f = 0; f < perDir; f++)
    {
        string name = (f & 3) switch
        {
            0 => "report-" + d.ToString("D4") + "-" + f.ToString("D4") + ".pdf",
            1 => "photo-" + d.ToString("D4") + "-" + f.ToString("D4") + ".jpg",
            2 => "notes-" + d.ToString("D4") + "-" + f.ToString("D4") + ".txt",
            _ => "video-" + d.ToString("D4") + "-" + f.ToString("D4") + ".mp4"
        };

        using var stream = new FileStream(Path.Combine(dir, name), FileMode.Create, FileAccess.Write,
            FileShare.None, 64, FileOptions.None);
        stream.Write(payload, 0, payload.Length);
        created++;
    }

    if ((d + 1) % 50 == 0)
    {
        Console.WriteLine("dirs=" + (d + 1) + " files=" + created.ToString("N0")
            + " elapsed=" + watch.Elapsed.TotalSeconds.ToString("F1") + "s");
    }
}

Console.WriteLine("DONE files=" + created.ToString("N0") + " elapsed=" + watch.Elapsed.TotalSeconds.ToString("F1") + "s");
