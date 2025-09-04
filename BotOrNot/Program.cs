// TODO: Refactor Reader Class, Possibly add Fortnite tracker data. Examine data by time, for example X minutes, last 10 minutes, etc.
namespace BotOrNot;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}