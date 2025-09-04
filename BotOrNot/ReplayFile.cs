using System;
using System.IO;
using FortniteReplayReader;
using FortniteReplayReader.Models;
using Unreal.Core.Models.Enums;
using Microsoft.Extensions.Logging;

namespace BotOrNot;
public class ReplayFile
{
   public readonly string path;

   public ReplayFile(string replay_path)
   {
      path = replay_path;
   }

   public void Validate()
   {
      if (string.IsNullOrWhiteSpace(path))
      {
         throw new ArgumentException("Replay file path cannot be null or empty.");
      }
      if (!File.Exists(path))
      {
         throw new FileNotFoundException("Replay file not found.");
      }

      if (!Path.GetExtension(path).Equals(".replay", StringComparison.OrdinalIgnoreCase))
      {
         throw new ArgumentException("File does not have .replay extension.");
      }
   }
}