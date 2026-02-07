using BotOrNot.Core.Services;

public static class CompareElims
{
    public static async Task RunAsync(string demosDir)
    {
        var service = new ReplayService();
        var files = Directory.GetFiles(demosDir, "*.replay", SearchOption.TopDirectoryOnly);
        Console.WriteLine($"Found {files.Length} replay files\n");
        Console.WriteLine($"{"File",-60} {"OwnerKills",10} {"OwnerElims",10} {"Match?",6}");
        Console.WriteLine(new string('-', 90));

        int matched = 0, mismatched = 0, errors = 0;

        foreach (var file in files.OrderBy(f => f))
        {
            try
            {
                var data = await service.LoadReplayAsync(file);
                var ownerKills = data.OwnerKills;
                var ownerElimCount = data.OwnerEliminations.Count(p => !p.IsNpc);
                var match = ownerKills == ownerElimCount;
                var fileName = Path.GetFileName(file);
                if (fileName.Length > 58) fileName = fileName[..58];

                Console.WriteLine($"{fileName,-60} {ownerKills,10} {ownerElimCount,10} {(match ? "YES" : "NO"),6}");

                if (match) matched++;
                else mismatched++;
            }
            catch (Exception ex)
            {
                var fileName = Path.GetFileName(file);
                if (fileName.Length > 58) fileName = fileName[..58];
                Console.WriteLine($"{fileName,-60} {"ERR",10} {"ERR",10} {"ERR",6}  {ex.Message[..Math.Min(60, ex.Message.Length)]}");
                errors++;
            }
        }

        Console.WriteLine(new string('-', 90));
        Console.WriteLine($"Total: {files.Length} | Matched: {matched} | Mismatched: {mismatched} | Errors: {errors}");
    }
}
