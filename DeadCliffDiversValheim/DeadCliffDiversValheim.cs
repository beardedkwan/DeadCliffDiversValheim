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

namespace DeadCliffDiversValheim
{
    public class PluginInfo
    {
        public const string Name = "DeadCliffDiversValheim";
        public const string Guid = "deadcliffdivers" + Name;
        public const string Version = "0.0.0";
    }

    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("valheim.exe")]
    public class DeadCliffDiversValheim : BaseUnityPlugin
    {
        void Awake()
        {
            Harmony harmony = new Harmony(PluginInfo.Guid);
            harmony.PatchAll();
        }

        // Todo: skill boost, no planting stamina usage, fermenter, beehive
        // Thinking about: Teleport anything

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
                int multiplier = 3;

                String item = __instance.m_itemPrefab.name;
                //Debug.Log($"Pickable: '{item}'");
                if (modifyItems.Contains(item))
                {
                    //Debug.Log($"Modifying pickable: '{item}'");
                    ___m_amount = (___m_amount * multiplier);
                }
            }
        }

        // RESOURCE DROPS
        [HarmonyPatch(typeof(DropTable), "GetDropList", new Type[] { typeof(int) })]
        class Drops_Patch
        {
            static void Postfix(ref DropTable __instance, ref List<GameObject> __result)
            {
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

        // BOSS POWERS
        [HarmonyPatch(typeof(Player), nameof(Player.SetGuardianPower))]
        class BossPowers_Patch
        {
            private static void Postfix(ref Player __instance)
            {
                const float duration = 1800;
                const float cooldown = 60;

                // (thanks chatgpt):
                // Use AccessTools.Field to get the private m_guardianSE field from the Player class
                FieldInfo guardianSEField = AccessTools.Field(typeof(Player), "m_guardianSE");

                // Get the value of the m_guardianSE field for the given Player instance
                StatusEffect guardianSE = (StatusEffect)guardianSEField.GetValue(__instance);

                if (guardianSE) { 
                    // Modify the m_ttl and m_cooldown properties of the guardianSE object
                    guardianSE.m_ttl = duration;
                    guardianSE.m_cooldown = cooldown;

                    // Set the modified value of the m_guardianSE field for the given Player instance
                    guardianSEField.SetValue(__instance, guardianSE);
                }
            }
        }

        // CHESTS
        [HarmonyPatch(typeof(Container), "Awake")]
        class Chests_Patch
        {
            private static void Prefix(Container __instance)
            {
                string name = __instance.name;

                // wood chest
                if (name.StartsWith("piece_chest_wood"))
                {
                    __instance.m_height = 3;
                }

                // personal chest
                if (name.StartsWith("piece_chest_private"))
                {
                    __instance.m_width = 8;
                    __instance.m_height = 4;
                }
            }
        }

        // REFINERIES
        [HarmonyPatch(typeof(Smelter), "Awake")]
        class Smelter_Patch
        {
            private static void Prefix(ref Smelter __instance)
            {
                string name = __instance.name;
                //Debug.Log($"smelter patch, name: '{name}'");

                if (name.StartsWith("charcoal_kiln"))
                {
                    __instance.m_maxOre = 60;
                    __instance.m_secPerProduct = 3f;
                }
                else if (name.StartsWith("smelter"))
                {
                    __instance.m_maxOre = 60;
                    __instance.m_maxFuel = 60;
                    __instance.m_fuelPerProduct = 1;
                    __instance.m_secPerProduct = 3f;
                }
                else if (name.StartsWith("blastfurnace"))
                {
                    __instance.m_maxOre = 60;
                    __instance.m_maxFuel = 60;
                    __instance.m_fuelPerProduct = 1;
                    __instance.m_secPerProduct = 3f;
                }
                else if (name.StartsWith("piece_spinningwheel"))
                {
                    __instance.m_maxOre = 100;
                    __instance.m_secPerProduct = 3f;
                }
                else if (name.StartsWith("windmill"))
                {
                    __instance.m_maxOre = 100;
                    __instance.m_secPerProduct = 3f;
                }
                else if (name.StartsWith("eitrrefinery"))
                {
                    __instance.m_maxOre = 60;
                    __instance.m_maxFuel = 60;
                    __instance.m_secPerProduct = 3f;
                }
            }
        }

        // CRAFTING STATIONS
        // increase upgrades distance
        [HarmonyPatch(typeof(StationExtension), "Awake")]
        public static class StationExtension_Awake_Patch
        {
            [HarmonyPrefix]
            public static void Prefix(ref float ___m_maxStationDistance)
            {
                ___m_maxStationDistance = 15f;
            }
        }

        // remove roof requirement
        [HarmonyPatch(typeof(CraftingStation), "CheckUsable")]
        class WorkbenchRemoveRestrictions
        {
            private static void Prefix(ref CraftingStation __instance)
            {
                __instance.m_craftRequireRoof = false;
            }
        }

        // CARRY WEIGHT
        // base
        [HarmonyPatch(typeof(Player), "Awake")]
        class BaseCarry_Patch
        {
            private static void Postfix(ref Player __instance)
            {
                __instance.m_maxCarryWeight = 400f;
            }
        }

        // megingjord
        [HarmonyPatch(typeof(SE_Stats), "Setup")]
        class Megingjord_Patch
        {
            private static void Postfix(ref SE_Stats __instance)
            {
                if (__instance.m_addMaxCarryWeight > 0) { 
                    __instance.m_addMaxCarryWeight = (__instance.m_addMaxCarryWeight - 150) + 200;
                }
            }
        }
    }
}