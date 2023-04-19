using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.CodeDom;

namespace DeadCliffDivers
{
    public class PluginInfo
    {
        public const string Name = "DeadCliffDiversMod";
        public const string Guid = "deadcliffdivers" + Name;
        public const string Version = "0.0.0";
    }

    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("valheim.exe")]
    public class dcdMod : BaseUnityPlugin
    {
        void Awake()
        {
            Harmony harmony = new Harmony(PluginInfo.Guid);
            harmony.PatchAll();
        }

        // WISH LIST: infinite powers, teleporting everything, increased chest sizes, improve smelters size, shared map, no roof for workbenches, player carry weight and belt increase, skill boost

        // INFINITE FIRES
        [HarmonyPatch(typeof(Fireplace), "UpdateFireplace")]
        class Fireplace_Patch
        {
            static void Prefix(Fireplace __instance, ref ZNetView ___m_nview)
            {
                ___m_nview.GetZDO().Set("fuel", __instance.m_maxFuel);
            }
        }

        // GATHERING
        [HarmonyPatch(typeof(Pickable), "RPC_Pick")]
        class Pickable_Patch
        {
            static void Prefix(Pickable __instance, ref int ___m_amount)
            {
                List<string> modifyItems = new List<string> { "Raspberry", "Blueberries", "Mushroom", "Carrot", "CarrotSeeds", "Turnip", "TurnipSeeds", "Onion", "OnionSeeds", "Barley", "Flax", "MushroomJotunpuffs", "MushroomMagecap", "Thistle", "Dandelion", "Cloudberry" };

                String item = __instance.m_itemPrefab.name;
                Debug.Log($"Pickable: '{item}'");
                if (modifyItems.Contains(item))
                {
                    Debug.Log($"Modifying pickable: '{item}'");
                    ___m_amount = (___m_amount * 3);
                }
            }
        }

        // RESOURCE DROPS
        [HarmonyPatch(typeof(DropTable), "GetDropList", new Type[] { typeof(int) })]
        class Drops_Patch
        {
            static void Postfix(ref DropTable __instance, ref List<GameObject> __result)
            {
                Debug.Log("Hit GetDropList postfix");

                Dictionary<string, int> modifyItems = new Dictionary<string, int>();

                // Wood
                modifyItems.Add("Wood", 4);
                modifyItems.Add("RoundLog", 3);
                modifyItems.Add("FineWood", 3);
                modifyItems.Add("ElderBark", 3);
                modifyItems.Add("YggdrasilWood", 3);

                // Ores
                modifyItems.Add("CopperOre", 3);
                modifyItems.Add("TinOre", 3);
                modifyItems.Add("IronScrap", 3);
                modifyItems.Add("SilverOre", 3);

                // Misc
                modifyItems.Add("Chitin", 3);

                List<GameObject> drops = new List<GameObject>();
                foreach (GameObject toDrop in __result)
                {
                    if (modifyItems.ContainsKey(toDrop.name))
                    {
                        int multiplier = 1;
                        modifyItems.TryGetValue(toDrop.name, out multiplier);

                        for (int i = 0; i < multiplier; i++)
                        {
                            drops.Add(toDrop);
                        }
                    }
                }

                __result = drops;
            }
        }

        // FOOD DEGREDATION
        [HarmonyPatch]
        class FoodDeg_Patch
        {
            private static FieldInfo field_Food_m_health = AccessTools.Field(typeof(Player.Food), "m_health");
            private static FieldInfo field_Food_m_stamina = AccessTools.Field(typeof(Player.Food), "m_stamina");
            private static FieldInfo field_Food_m_eitr = AccessTools.Field(typeof(Player.Food), "m_eitr");

            private static FieldInfo field_Food_m_item = AccessTools.Field(typeof(Player.Food), "m_item");
            private static FieldInfo field_ItemData_m_shared = AccessTools.Field(typeof(ItemDrop.ItemData), "m_shared");
            private static FieldInfo field_SharedData_m_food = AccessTools.Field(typeof(ItemDrop.ItemData.SharedData), "m_food");
            private static FieldInfo field_SharedData_m_foodStamina = AccessTools.Field(typeof(ItemDrop.ItemData.SharedData), "m_foodStamina");
            private static FieldInfo field_SharedData_m_foodEitr = AccessTools.Field(typeof(ItemDrop.ItemData.SharedData), "m_foodEitr");

            [HarmonyTranspiler]
            [HarmonyPatch(typeof(Player), "GetTotalFoodValue")]
            public static IEnumerable<CodeInstruction> Player_GetTotalFoodValue(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> il = instructions.ToList();

                for (int i = 0; i < il.Count; ++i)
                {
                    if (il[i].LoadsField(field_Food_m_health))
                    {
                        il[i].operand = field_Food_m_item;
                        il.Insert(++i, new CodeInstruction(OpCodes.Ldfld, field_ItemData_m_shared));
                        il.Insert(++i, new CodeInstruction(OpCodes.Ldfld, field_SharedData_m_food));
                    }
                    else if (il[i].LoadsField(field_Food_m_stamina))
                    {
                        il[i].operand = field_Food_m_item;
                        il.Insert(++i, new CodeInstruction(OpCodes.Ldfld, field_ItemData_m_shared));
                        il.Insert(++i, new CodeInstruction(OpCodes.Ldfld, field_SharedData_m_foodStamina));
                    }
                    else if (il[i].LoadsField(field_Food_m_eitr))
                    {
                        il[i].operand = field_Food_m_item;
                        il.Insert(++i, new CodeInstruction(OpCodes.Ldfld, field_ItemData_m_shared));
                        il.Insert(++i, new CodeInstruction(OpCodes.Ldfld, field_SharedData_m_foodEitr));
                    }
                }

                return il.AsEnumerable();
            }
        }
    }
}