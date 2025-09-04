using FortniteReplayReader.Models;
using FortniteReplayReader.Models.NetFieldExports;

namespace BotOrNot;

public class PlayerLookupCache
{
    public Dictionary<string, object> players;
    
    public void Load(FortniteReplay replay)
    {
        foreach (var pd in replay.PlayerData ?? Enumerable.Empty<object>())
        {
            var t = pd.GetType();
            var id = t.GetProperty("PlayerId")?.GetValue(pd);

            players[id.ToString()] = pd;
        }
    }
    
}