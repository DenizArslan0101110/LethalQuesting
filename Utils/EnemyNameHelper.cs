using System.Collections.Generic;

namespace LethalQuesting.Utils
{
    public static class EnemyNameHelper
    {
        private static readonly Dictionary<string, string> EnemyCleanNames = new Dictionary<string, string>
        {
            { "Flowerman", "Bracken" },
            { "Crawler", "Thumper" },
            { "Spring", "Coil-Head" },
            { "MouthDog", "Eyeless Dog" },
            { "ForestGiant", "Forest Giant" },
            { "HoardingBug", "Hoarding Bug" },
            { "Jester", "Jester" },
            { "Centipede", "Snare Flea" },
            { "Puffer", "Spore Lizard" },
            { "Bunker Spider", "Bunker Spider" },
            { "Nutcracker", "Nutcracker" },
            { "Masked", "Masked" },
            { "Girl", "Ghost Girl" },
            { "Blob", "Hygrodere" },
            { "RadMech", "Old Bird"}
        };

        public static string GetCleanName(string internalName)
        {
            return EnemyCleanNames.TryGetValue(internalName, out string cleanName) ? cleanName : internalName;
        }
    }
}