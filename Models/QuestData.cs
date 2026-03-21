using Unity.Netcode;

namespace LethalQuesting.Models
{
    public enum QuestType
    {
        None,
        Scrap,
        Kill,
        Survive,
        Betray,
        JumpLimit
    }
    public struct QuestData : INetworkSerializable
    {
        public QuestType Type;
        public string Title;
        public string TargetName;
        public int TargetCount;
        public int CurrentCount;
        public int Reward;
        public bool IsRewardGiven;
   
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Type);
            serializer.SerializeValue(ref Title);
            serializer.SerializeValue(ref TargetName);
            serializer.SerializeValue(ref TargetCount);
            serializer.SerializeValue(ref CurrentCount);
            serializer.SerializeValue(ref Reward);
            serializer.SerializeValue(ref IsRewardGiven);
        }
    }
}