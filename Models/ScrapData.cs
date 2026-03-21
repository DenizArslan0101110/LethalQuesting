namespace LethalQuesting.Models
{
    // holds info for scrap pool (this one is used to issue quests)
    public struct ScrapPoolEntry
    {
        public string Name;
        public int TotalCount;
        public int AverageValue;
    }
    
    // data holding for scrap items (this one is used to check for quest completion)
    public class ScrapData
    {
        public string Name;
        public int Value;
        public ulong NetworkId;
        public bool IsCollected;

        public ScrapData(GrabbableObject obj)
        {
            Name = obj.itemProperties.itemName;
            Value = obj.scrapValue;
            NetworkId = obj.NetworkObjectId;
            IsCollected = false;
        }
    }
}