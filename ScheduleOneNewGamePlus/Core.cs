using MelonLoader;
using ScheduleOne.Economy;
using ScheduleOne.NPCs;
using ScheduleOne.AvatarFramework;
using HarmonyLib;
using ScheduleOne.Persistence;
using ScheduleOne.Police;
using ScheduleOne.Product;
using ScheduleOne.PlayerScripts;
using static ScheduleOne.PlayerScripts.PlayerCrimeData;
using ScheduleOne.Map;
using ScheduleOne.DevUtilities;
using ScheduleOne.Quests;
using ScheduleOne.Storage;
using ScheduleOne.ItemFramework;
using ScheduleOne.GameTime;
using Pathfinding;
using ScheduleOne.Money;
using ScheduleOne.Dialogue;
using ScheduleOne.UI;
using UnityEngine;
using ScheduleOne.Law;
using ScheduleOne.Growing;
using FishNet.Object;
using System.Reflection.Emit;
using ScheduleOne.Levelling;
using ScheduleOne.UI.Phone;
using ScheduleOne.ScriptableObjects;
using UnityEngine.SocialPlatforms;
using ScheduleOne.NPCs.CharacterClasses;
using System.Text.RegularExpressions;
using System;
using ScheduleOne.Property;
using ScheduleOne.UI.Phone.Messages;

[assembly: MelonInfo(typeof(ScheduleCustomDealer.Core), "ScheduleCustomDealer", "1.0.0", "Sean", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ScheduleCustomDealer
{
    public class Core : MelonMod
    {
        private bool registered = false;
        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (buildIndex == 1)
            {
                if (LoadManager.Instance != null && !registered)
                {
                    LoadManager.Instance.onLoadComplete.AddListener(OnLoadCompleteCb);
                }
            }
            else
            {
                if (LoadManager.Instance != null && registered)
                {
                    LoadManager.Instance.onLoadComplete.RemoveListener(OnLoadCompleteCb);
                }
                registered = false;

            }
        }

        public void OnLoadCompleteCb()
        {
            if (registered) return;
            registered = true;

            LevelManager levelManager = NetworkSingleton<LevelManager>.Instance;
            

            // Initialize all NPCs with cash on Day1 for pickpocketing purposes
            TimeManager timeManager = NetworkSingleton<TimeManager>.Instance;
            if (timeManager.ElapsedDays == 0)
            {
                foreach (NPC npc in NPCManager.NPCRegistry)
                {
                    if (npc.Inventory.RandomCash)
                    {
                        int num = UnityEngine.Random.Range(0, 50);
                        if (num > 0)
                        {
                            CashInstance cashInstance = NetworkSingleton<MoneyManager>.Instance.GetCashInstance(num);
                            npc.Inventory.InsertItem(cashInstance);
                        }
                    }
                }
            }

            // Change Uncle Nelson's Initial Message
            UncleNelson uncle = GameObject.FindObjectsOfType<UncleNelson>().FirstOrDefault();
            if (uncle != null)
            {
                uncle.InitialMessage = "You get out alright? I've passed your info to my contact Albert Hoover. Best not to talk over your mobile phone. Go find a payphone.";
                
                if (levelManager.XP == 0)
                {
                    uncle.SendInitialMessage();
                    uncle.MSGConversation.SetIsKnown(known: false);
                    uncle.MSGConversation.SendMessageChain(new MessageChain
                    {
                        Messages = new List<string> { 
                            "**Developer Note**",
                            "Welcome to NewGamePlus mode! There are a few important differences to keep in mind.",
                            "Customers will expect you to be high when you complete a deal.",
                            "Customers only mess with drugs that have the affects they like.",
                            "You start off at the Hoodlum rank. Good luck, have fun!",
                        }
                    }, 0f, notify: true);
                    uncle.MSGConversation.SetRead(r: false);

                    // Unlock Albert the supplier
                    //Albert al = GameObject.FindObjectsOfType<Albert>().FirstOrDefault();
                    //if (al != null)
                    //{
                    //    al.SetUnlockMessage();
                    //}
                }
            }

            // Boost to Hoodlum
            if (levelManager.XP == 0)
            {
                levelManager.AddXP(1000);
            }

            // Double the call police chance for customer samples
            // Disable guaranteed first successful sample
            Customer[] customers = GameObject.FindObjectsOfType<Customer>();
            foreach (Customer customer in customers)
            {
                customer.CustomerData.CallPoliceChance *= 2;
                customer.CustomerData.GuaranteeFirstSampleSuccess = false;
            }

            // Increase Property prices x2
            Property.UnownedProperties.ForEach(property => property.Price *= 1.5f);
            Business.UnownedBusinesses.ForEach(business => business.Price *= 1.5f);

            // find all worldstorageentities for initial cash drops
            // Central Canal: b77c6f13-4f79-41a3-87fe-591957b299a8
            // Town Hall Fountain: 11043234-65f2-4861-aafa-a695ee9775ad
            // Behind Supermarket: 4a1d3e02-562b-457a-a56d-5c2513a17905
            //Dictionary<String, float> initialDrops = new Dictionary<String, float>
            //{
            //    { "b77c6f13-4f79-41a3-87fe-591957b299a8", 0f },
            //    { "11043234-65f2-4861-aafa-a695ee9775ad", 0f },
            //    { "4a1d3e02-562b-457a-a56d-5c2513a17905", 0f },
            //};

            //MelonLogger.Msg("Looking for initial cash drops...");
            //foreach (WorldStorageEntity entity in WorldStorageEntity.All)
            //{
            //    string guidStr = entity.GUID.ToString();
            //    if (initialDrops.ContainsKey(guidStr))
            //    {
            //        CashInstance cash = entity.ItemSlots[0].ItemInstance as CashInstance;
            //        if (cash != null)
            //        {
            //            MelonLogger.Msg($"Setting Dead Drop: {guidStr} to balance {initialDrops[guidStr]}");
            //            cash.SetBalance(initialDrops[guidStr]);
            //        }
            //    }
            //}
        }

        // Cops will immediately escalate to deadly force, no more tasers
        [HarmonyPatch(typeof(PlayerCrimeData), "Escalate")]
        public static class PlayerCrimeData_Escalate_Patch
        {
            public static bool Prefix(PlayerCrimeData __instance)
            {
                MelonLogger.Msg("escalate to the MAX");
                __instance.SetPursuitLevel(EPursuitLevel.Lethal);
                PoliceStation.GetClosestPoliceStation(__instance.Player.Avatar.MiddleSpineRB.position);
                PoliceStation.GetClosestPoliceStation(__instance.Player.Avatar.MiddleSpineRB.position).Dispatch(1, __instance.Player, PoliceStation.EDispatchType.Auto, beginAsSighted: true);
                return true;
            }
        }

        // Guid of 'Haha' response is 49a5e66d-5100-4193-9461-92f0e4a95330
        // Change response to yell at the player instead of laughing
        [HarmonyPatch(typeof(DialogueHandler), "InitializeDialogue", new Type[] { typeof(DialogueContainer) })]
        public static class DialogueHandler_InitializeDialogue_Patch
        {
            public static bool Prefix(DialogueHandler __instance, DialogueContainer container)
            {
                DialogueNodeData node = container.DialogueNodeData.FirstOrDefault(node => node.Guid == "49a5e66d-5100-4193-9461-92f0e4a95330");
                if (node != null)
                {
                    MelonLogger.Msg("Found the drug dealer dialogue node!");

                    node.DialogueText = "That's not funny.";
                    node.DialogueNodeLabel = "";
                }
                return true;
            }
        }

        // Have Dan from the hardware store call the cops if you tell him you're a drug dealer
        [HarmonyPatch(typeof(DialogueController_Dan), "ModifyDialogueText")]
        public static class DialogueController_Dan_ModifyDialogueText_Patch
        {
            public static bool Prefix(DialogueController_Dan __instance, string dialogueLabel, string dialogueText)
            {
                MelonLogger.Msg($"dumping info: {dialogueLabel} - {dialogueText}");
                if (dialogueText.Contains("not funny"))
                {
                    __instance.GetComponent<DialogueHandler>().NPC.actions.SetCallPoliceBehaviourCrime(new AttemptingToSell());
                    __instance.GetComponent<DialogueHandler>().NPC.actions.CallPolice_Networked(Player.Local);
                }
                return true;
            }
        }

        // Have plants start at Poor quality
        [HarmonyPatch(typeof(Plant), "Initialize")]
        public static class Plant_Initialize_Patch
        {
            public static bool Prefix(Plant __instance, NetworkObject pot, float growthProgress, float yieldLevel, float qualityLevel)
            {
                __instance.BaseQualityLevel = 0.3f;
                return true;
            }
        }

        // Edit first phone call, don't trigger questline
        //[HarmonyPatch(typeof(CallInterface), nameof(CallInterface.StartCall))]
        //public static class CallInterface_StartCall_Patch
        //{
        //    public static bool Prefix(CallInterface __instance, PhoneCallData data, CallerID caller, int startStage)

        //    {
        //        if (data.Stages[0].Text.Contains("good to hear your voice"))
        //        {
        //            data.Stages[2].Text = "Listen, the guards are saying they'll let me go if you can get them what they need. They want <h1>20 bricks of legendary coke</h> and <h1>10 gold bars</h>.";

        //            PhoneCallData.Stage stage = new PhoneCallData.Stage();
        //            stage.Text = "That shouldn't be too hard for my favorite nephew. I'll get back to you with more details. Love ya bud.";
        //            data.Stages[3] = stage;

        //            Quest_WelcomeToHylandPoint quest = GameObject.FindObjectsOfType<Quest_WelcomeToHylandPoint>().FirstOrDefault();
        //            if (quest != null)
        //            {
        //                quest.Complete();
        //            }
        //        }
        //        return true;
        //    }
        //}

        [HarmonyPatch(typeof(PenaltyHandler), nameof(PenaltyHandler.ProcessCrimeList))]
        public static class PenaltyHandler_ProcessCrimeList_Patch
        {
            static void Postfix(ref List<string> __result)
            {
                foreach (string item in __result)
                {
                    MelonLogger.Msg($"Penalty String: {item}");
                }
                string last = __result[^1];
                MelonLogger.Msg($"last string: {last}");
                var match = Regex.Match(last, @"[-+]?\d*\.?\d+");
                if (match.Success && float.TryParse(match.Value, out float value))
                {
                    MelonLogger.Msg($"Parsed float: {value}");
                    float increasedPenalty = value * 10;
                    string penaltyText = MoneyManager.FormatAmount(increasedPenalty, showDecimals: true) + " fine";
                    __result[^1] = penaltyText;
                    NetworkSingleton<MoneyManager>.Instance.ChangeCashBalance(0f - value * 9);
                }
                else
                {
                    MelonLogger.Msg("Failed to parse float.");
                }
            }
        }

        static bool highOnWeed = false;

        [HarmonyPatch(typeof(WeedInstance), nameof(WeedInstance.ApplyEffectsToPlayer))]
        public static class WeedInstance_ApplyEffectsToPlayer_Patch
        {
            static void Postfix()
            {
                highOnWeed = true;
                MelonLogger.Msg("Player is high on weed");
            }
        }

        [HarmonyPatch(typeof(WeedInstance), nameof(WeedInstance.ClearEffectsFromPlayer))]
        public static class WeedInstance_ClearEffectsFromPlayer_Patch
        {
            static void Postfix()
            {
                highOnWeed = false;
                MelonLogger.Msg("Player is no longer high on weed");
            }
        }

        static bool highOnMeth = false;
        [HarmonyPatch(typeof(MethInstance), nameof(MethInstance.ApplyEffectsToPlayer))]
        public static class MethInstance_ApplyEffectsToPlayer_Patch
        {
            static void Postfix()
            {
                highOnMeth = true;
                MelonLogger.Msg("Player is high on meth");
            }
        }

        [HarmonyPatch(typeof(MethInstance), nameof(MethInstance.ClearEffectsFromPlayer))]
        public static class MethInstance_ClearEffectsFromPlayer_Patch
        {
            static void Postfix()
            {
                highOnMeth = false;
                MelonLogger.Msg("Player is no longer high on meth");
            }
        }

        static bool highOnCocaine = false;
        [HarmonyPatch(typeof(CocaineInstance), nameof(CocaineInstance.ApplyEffectsToPlayer))]
        public static class CocaineInstance_ApplyEffectsToPlayer_Patch
        {
            static void Postfix()
            {
                highOnCocaine = true;
                MelonLogger.Msg("Player is high on cocaine");
            }
        }

        [HarmonyPatch(typeof(CocaineInstance), nameof(CocaineInstance.ClearEffectsFromPlayer))]
        public static class CocaineInstance_ClearEffectsFromPlayer_Patch
        {
            static void Postfix()
            {
                highOnCocaine = false;
                MelonLogger.Msg("Player is no longer high on cocaine");
            }
        }

        [HarmonyPatch(typeof(Contract), nameof(Contract.GetProductListMatch))]
        public static class Contract_GetProductListMatch_Patch
        {
            static void Postfix(ref float __result)
            {
                if (!highOnWeed && !highOnMeth && !highOnCocaine)
                {
                    MelonLogger.Msg("30% contract penalty for not being high");
                    __result = __result * 0.7f;
                }
            }
        }

        [HarmonyPatch(typeof(Customer), nameof(Customer.GetProductEnjoyment))]
        public static class GetProductEnjoyment_Postfix
        {
            static void Postfix(Customer __instance, ProductDefinition product, EQuality quality, ref float __result)
            {
                var productProps = product.Properties;

                var foundMatch = false;
                foreach (var preferred in __instance.CustomerData.PreferredProperties)
                {
                    if (productProps.Find(x => x == preferred) != null)
                        foundMatch = true;
                }
                if (!foundMatch)
                {
                    MelonLogger.Msg("Couldn't find a single property the customer likes, failing the check");
                    __result = -1000f;
                } else
                {
                    MelonLogger.Msg("Found at least one property the customer likes, passing the check...");
                }
            }
        }

        [HarmonyPatch(typeof(DialogueController_Ming), "CanBuyRoom")]
        public static class DialogueController_Ming_CanBuyRoom_Postfix
        {
            static void Postfix(DialogueController_Ming __instance, bool enabled, ref bool __result)
            {
                if (!__result && !__instance.Property.IsOwned)
                {
                    __result = true;
                }
            }
        }
    }
}