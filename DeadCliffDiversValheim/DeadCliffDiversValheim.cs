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
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.Runtime.Remoting.Messaging;

namespace DeadCliffDiversValheim
{
    public class PluginInfo
    {
        public const string Name = "DeadCliffDiversValheim";
        public const string Guid = "deadcliffdivers" + Name;
        public const string Version = "1.0.0";
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

        // INFINITE FIRES
        [HarmonyPatch(typeof(Fireplace), "UpdateFireplace")]
        public static class Fireplace_Patch
        {
            static void Prefix(Fireplace __instance, ref ZNetView ___m_nview)
            {
                ___m_nview.GetZDO().Set("fuel", __instance.m_maxFuel);
            }
        }

        // GATHERING
        [HarmonyPatch(typeof(Pickable), "RPC_Pick")]
        public static class Pickable_Patch
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
        public static class Drops_Patch
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
                    else
                    {
                        drops.Add(toDrop);
                    }
                }

                __result = drops;
            }
        }

        // FOOD DEGREDATION
        [HarmonyPatch]
        public static class FoodDeg_Patch
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
        const float GP_DURATION = 1800f;
        const float GP_COOLDOWN = 60f;
        // Player buff
        [HarmonyPatch(typeof(Player), nameof(Player.SetGuardianPower))]
        public static class BossPowers_Patch
        {
            private static void Postfix(ref Player __instance)
            {
                // (thanks chatgpt):
                // Use AccessTools.Field to get the private m_guardianSE field from the Player class
                FieldInfo guardianSEField = AccessTools.Field(typeof(Player), "m_guardianSE");

                // Get the value of the m_guardianSE field for the given Player instance
                StatusEffect guardianSE = (StatusEffect)guardianSEField.GetValue(__instance);

                if (guardianSE) { 
                    // Modify the m_ttl and m_cooldown properties of the guardianSE object
                    guardianSE.m_ttl = GP_DURATION;
                    guardianSE.m_cooldown = GP_COOLDOWN;

                    Debug.Log($"(BossPowers_Patch) Setting guardianSE.m_ttl to '{GP_DURATION}' and guardianSE.m_cooldown to '{GP_COOLDOWN}'");

                    // Set the modified value of the m_guardianSE field for the given Player instance
                    guardianSEField.SetValue(__instance, guardianSE);
                }
            }
        }

        // Other players buff
        [HarmonyPatch(typeof(SEMan), "AddStatusEffect", new Type[] { typeof(StatusEffect), typeof(bool), typeof(int), typeof(float) })]
        public static class BossPowersEffects_Patch
        {
            private static void Postfix(SEMan __instance, StatusEffect statusEffect, bool resetTime = false, int itemLevel = 0, float skillLevel = 0)
            {
                // Access private m_character field off of SEMan
                FieldInfo characterField = AccessTools.Field(typeof(SEMan), "m_character");
                Character character = (Character)characterField.GetValue(__instance);

                // Every guardian power starts with GP_
                if (character.IsPlayer() && statusEffect.name.StartsWith("GP_"))
                {
                    // Access status effects
                    FieldInfo statusEffectsField = AccessTools.Field(typeof(SEMan), "m_statusEffects");
                    List<StatusEffect> statusEffects = (List<StatusEffect>)statusEffectsField.GetValue(__instance);

                    foreach (StatusEffect buff in statusEffects)
                    {
                        if (buff.m_name == __instance.GetStatusEffect(statusEffect.name).m_name)
                        {
                            __instance.GetStatusEffect(statusEffect.name).m_ttl = GP_DURATION;
                        }
                    }
                }
            }
        }

        // CHESTS
        [HarmonyPatch(typeof(Container), "Awake")]
        public static class Chests_Patch
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
        public static class Smelter_Patch
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
        public static class WorkbenchRemoveRestrictions
        {
            private static void Prefix(ref CraftingStation __instance)
            {
                __instance.m_craftRequireRoof = false;
            }
        }

        // workbench range
        [HarmonyPatch(typeof(CraftingStation), "Start")]
        public static class WorkbenchRangeIncrease
        {
            private static void Prefix(ref CraftingStation __instance, ref float ___m_rangeBuild, GameObject ___m_areaMarker)
            {
                try
                {
                    const float RANGE = 40f;

                    ___m_rangeBuild = RANGE;
                    ___m_areaMarker.GetComponent<CircleProjector>().m_radius = ___m_rangeBuild;
                    float scaleIncrease = (RANGE - 20f) / 20f * 100f;
                    ___m_areaMarker.gameObject.transform.localScale = new Vector3(scaleIncrease / 100, 1f, scaleIncrease / 100);

                    EffectArea effectArea = __instance.GetComponentInChildren<EffectArea>();
                    if (effectArea != null && (effectArea.m_type & EffectArea.Type.PlayerBase) != 0)
                    {
                        SphereCollider collider = __instance.GetComponentInChildren<SphereCollider>();
                        if (collider != null)
                        {
                            collider.transform.localScale = Vector3.one * RANGE * 2f;
                        }
                    }
                }
                catch {}
            }
        }

        // CARRY WEIGHT
        // base
        [HarmonyPatch(typeof(Player), "Awake")]
        public static class BaseCarry_Patch
        {
            private static void Postfix(ref Player __instance)
            {
                __instance.m_maxCarryWeight = 400f;
            }
        }

        // megingjord
        [HarmonyPatch(typeof(SE_Stats), "Setup")]
        public static class Megingjord_Patch
        {
            private static void Postfix(ref SE_Stats __instance)
            {
                if (__instance.m_addMaxCarryWeight > 0) { 
                    __instance.m_addMaxCarryWeight = (__instance.m_addMaxCarryWeight - 150) + 200;
                }
            }
        }

        // SKILLS
        [HarmonyPatch(typeof(Skills), "RaiseSkill")]
        public static class Skills_Patch
        {
            private static void Prefix(ref float factor)
            {
                factor = 1.75f;
            }
        }

        // TOOLS STAMINA
        [HarmonyPatch(typeof(Player), "UseStamina")]
        public static class ToolsStamina_Patch
        {
            private static void Prefix(ref Player __instance, ref float v)
            {
                string methodName = new StackTrace().GetFrame(2).GetMethod().Name;
                if (methodName.Contains("UpdatePlacement") || methodName.Contains("Repair") || methodName.Contains("RemovePiece")) { 
                    string item = __instance.GetRightItem().m_shared.m_name;
                    if (item.Equals("$item_hammer") || item.Equals("$item_hoe") || item.Equals("$item_cultivator"))
                    {
                        v = 0f;
                    }
                }
            }
        }

        // FERMENTER
        // production speed
        [HarmonyPatch(typeof(Fermenter), "Awake")]
        public static class ApplyFermenterChanges
        {
            private static void Prefix(ref float ___m_fermentationDuration)
            {
                ___m_fermentationDuration = 600f;
            }
        }

        // number of items
        [HarmonyPatch(typeof(Fermenter), "GetItemConversion")]
        public static class ApplyFermenterItemCountChanges
        {
            private static void Postfix(ref Fermenter.ItemConversion __result)
            {
                __result.m_producedItems = 18;
            }
        }

        // BEEHIVE
        [HarmonyPatch(typeof(Beehive), "Awake")]
        public static class Beehive_Awake_Patch
        {
            private static void Prefix(ref float ___m_secPerUnit, ref int ___m_maxHoney)
            {
                //___m_secPerUnit = 10f;
                ___m_maxHoney = 8;
            }
        }

        // PORTALS
        [HarmonyPatch(typeof(Inventory), "IsTeleportable")]
        public static class Portable_Patch
        {
            private static void Postfix(ref bool __result)
            {
                __result = true;
            }
        }

        // WEATHER DAMAGE
        // rain damage
        [HarmonyPatch(typeof(WearNTear), "HaveRoof")]
        public static class RainDamage_Patch
        {
            private static void Postfix(ref bool __result)
            {
                __result = true;
            }
        }

        // underwater damage
        [HarmonyPatch(typeof(WearNTear), "IsUnderWater")]
        public static class UnderwaterDamage_Patch
        {
            private static void Postfix(ref bool __result)
            {
                __result = false;
            }
        }

        // ITEMS WHILE SWIMMING
        [HarmonyPatch(typeof(Humanoid), "UpdateEquipment")]
        public static class SwimmingAndTools_Patch
        {
            private static MethodInfo method_Humanoid_HideHandItems = AccessTools.Method(typeof(Humanoid), nameof(Humanoid.HideHandItems));
            private static MethodInfo method_HideHandItems = AccessTools.Method(typeof(SwimmingAndTools_Patch), nameof(SwimmingAndTools_Patch.HideHandItems));

            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> il = instructions.ToList();

                for (int i = 0; i < il.Count; ++i)
                {
                    if (il[i].Calls(method_Humanoid_HideHandItems))
                    {
                        il[i - 1].opcode = OpCodes.Nop;
                        il[i].operand = method_HideHandItems;
                        break;
                    }
                }

                return il.AsEnumerable();
            }
            public static void HideHandItems() { }
        }
    }
}