using System.Collections.Generic;
using LethalNetworkAPI;
using LethalQuesting.Core;
using Unity.Netcode;
// In-Project Files 
using LethalQuesting.Managers;
using LethalQuesting.Models;

namespace LethalQuesting
{
    public class NetworkHandler
    {
        // Host knows and updates these, clients receive it to know quest progression
        public static LNetworkVariable<List<QuestData>> TodaysQuestsData = LNetworkVariable<List<QuestData>>.Create("TodaysQuestsData");

        // Client sends when they drop an item, host uses information to update quests
        public static LNetworkMessage<ulong> ItemDroppedMessage = LNetworkMessage<ulong>.Create("ItemDropped");

        public static void Initialize()
        {
            TodaysQuestsData.OnValueChanged += (oldVal, newVal) => { UpdateQuests.UpdateTheQuests(newVal); };

            ItemDroppedMessage.OnServerReceived += (objId, senderId) =>
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    Managers.ScrapManager.CheckScrapCollection(objId);
                }
            };
        }
    }
}