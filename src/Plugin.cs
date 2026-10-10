using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Cjayride.ArmorStandEnchant
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("randyknapp.mods.epicloot", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "cjayride.ArmorStandEnchant";
        public const string PluginName = "Armor Stand Enchant";
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            new Harmony(PluginGuid).PatchAll();
            Logger.LogInfo("Armor stand items keep the enchantment, quality, and durability they had when they were placed.");
        }
    }

    internal sealed class Backup
    {
        public int Slot;
        public int Quality;
        public float Durability;
        public int Variant;
        public int WorldLevel;
        public long CrafterId;
        public string CrafterName;
        public Dictionary<string, string> Custom = new Dictionary<string, string>();

        public static Backup From(ItemDrop.ItemData item, int slot)
        {
            var backup = new Backup
            {
                Slot = slot,
                Quality = item.m_quality,
                Durability = item.m_durability,
                Variant = item.m_variant,
                WorldLevel = item.m_worldLevel,
                CrafterId = item.m_crafterID,
                CrafterName = item.m_crafterName ?? ""
            };
            foreach (var pair in item.m_customData)
                backup.Custom[pair.Key] = pair.Value;
            return backup;
        }

        public byte[] Pack()
        {
            var pkg = new ZPackage();
            pkg.Write(Quality);
            pkg.Write(Durability);
            pkg.Write(Variant);
            pkg.Write(WorldLevel);
            pkg.Write(CrafterId);
            pkg.Write(CrafterName ?? "");
            pkg.Write(Custom.Count);
            foreach (var pair in Custom)
            {
                pkg.Write(pair.Key);
                pkg.Write(pair.Value ?? "");
            }
            return pkg.GetArray();
        }

        public static Backup Unpack(int slot, byte[] bytes)
        {
            if (bytes == null || bytes.Length < 8)
                return null;
            var pkg = new ZPackage(bytes);
            var backup = new Backup { Slot = slot };
            backup.Quality = pkg.ReadInt();
            backup.Durability = pkg.ReadSingle();
            backup.Variant = pkg.ReadInt();
            backup.WorldLevel = pkg.ReadInt();
            backup.CrafterId = pkg.ReadLong();
            backup.CrafterName = pkg.ReadString();
            int count = pkg.ReadInt();
            for (int i = 0; i < count; i++)
                backup.Custom[pkg.ReadString()] = pkg.ReadString();
            return backup;
        }
    }

    internal static class StandMemory
    {
        public static int Key(int slot)
        {
            unchecked
            {
                int hash = 17;
                string text = "cjay_ase" + slot;
                for (int i = 0; i < text.Length; i++)
                    hash = hash * 31 + text[i];
                return hash;
            }
        }

        public static void Write(ZDO zdo, Backup backup)
        {
            if (zdo == null || backup == null)
                return;
            zdo.Set(Key(backup.Slot), backup.Pack());
        }

        public static Backup Read(ZDO zdo, int slot)
        {
            if (zdo == null)
                return null;
            return Backup.Unpack(slot, zdo.GetByteArray(Key(slot)));
        }

        public static void Clear(ZDO zdo, int slot)
        {
            if (zdo == null)
                return;
            zdo.Set(Key(slot), Array.Empty<byte>());
        }
    }

    internal static class MagicReload
    {
        static MethodInfo dataMethod;
        static MethodInfo loadAll;
        static MethodInfo getMagic;
        static MethodInfo loadMagic;
        static bool lookedUp;

        public static void Apply(ItemDrop.ItemData item)
        {
            if (item == null)
                return;
            Lookup();
            if (dataMethod == null)
                return;
            object info;
            try
            {
                info = dataMethod.Invoke(null, new object[] { item });
            }
            catch (Exception)
            {
                return;
            }
            if (info == null)
                return;
            try
            {
                loadAll?.Invoke(info, null);
                if (getMagic != null)
                {
                    object component = getMagic.Invoke(info, new object[] { "" });
                    loadMagic?.Invoke(component, null);
                }
            }
            catch (Exception)
            {
            }
        }

        static void Lookup()
        {
            if (lookedUp)
                return;
            lookedUp = true;
            var extensions = AccessTools.TypeByName("EpicLoot.Data.ItemExtensions");
            dataMethod = extensions == null ? null : AccessTools.Method(extensions, "Data", new[] { typeof(ItemDrop.ItemData) });
            if (dataMethod == null)
                return;
            var infoType = dataMethod.ReturnType;
            loadAll = AccessTools.Method(infoType, "LoadAll");
            var magic = AccessTools.TypeByName("EpicLoot.MagicItemComponent");
            if (magic == null)
                return;
            MethodInfo openGet = null;
            foreach (var method in infoType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name == "Get" && method.IsGenericMethodDefinition)
                {
                    openGet = method;
                    break;
                }
            }
            if (openGet == null)
                return;
            getMagic = openGet.MakeGenericMethod(magic);
            loadMagic = AccessTools.Method(magic, "Load");
        }
    }

    [HarmonyPatch(typeof(ArmorStand), "UpdateAttach")]
    internal static class RememberOnAttach
    {
        static void Prefix(ArmorStand __instance, out Backup __state)
        {
            __state = null;
            if (__instance == null || !__instance.m_nview || !__instance.m_nview.IsOwner())
                return;
            var stand = Traverse.Create(__instance);
            var item = stand.Field("m_queuedItem").GetValue<ItemDrop.ItemData>();
            if (item == null)
                return;
            int slot = stand.Field("m_queuedSlot").GetValue<int>();
            __state = Backup.From(item, slot);
        }

        static void Postfix(ArmorStand __instance, Backup __state)
        {
            if (__state == null || __instance == null || !__instance.m_nview || !__instance.m_nview.IsValid())
                return;
            StandMemory.Write(__instance.m_nview.GetZDO(), __state);
        }
    }

    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.LoadFromZDO))]
    internal static class NoteLoadedItem
    {
        public static ItemDrop.ItemData Last;

        [HarmonyPriority(Priority.Last)]
        static void Postfix(ItemDrop.ItemData itemData, int index)
        {
            if (index >= 0)
                Last = itemData;
        }
    }

    [HarmonyPatch(typeof(ArmorStand), "DropItem")]
    internal static class RestoreOnDrop
    {
        static void Prefix()
        {
            NoteLoadedItem.Last = null;
        }

        static void Postfix(ArmorStand __instance, int index)
        {
            if (__instance == null || !__instance.m_nview || !__instance.m_nview.IsOwner())
                return;

            var item = NoteLoadedItem.Last;
            NoteLoadedItem.Last = null;
            var zdo = __instance.m_nview.GetZDO();
            var backup = StandMemory.Read(zdo, index);
            if (item != null && backup != null)
            {
                if (backup.Quality > item.m_quality)
                    item.m_quality = backup.Quality;
                if (backup.Durability > item.m_durability)
                    item.m_durability = backup.Durability;
                if (item.m_crafterID == 0 && backup.CrafterId != 0)
                {
                    item.m_crafterID = backup.CrafterId;
                    item.m_crafterName = backup.CrafterName;
                }
                if (item.m_variant == 0 && backup.Variant != 0)
                    item.m_variant = backup.Variant;
                if (item.m_worldLevel == 0 && backup.WorldLevel != 0)
                    item.m_worldLevel = backup.WorldLevel;
                foreach (var pair in backup.Custom)
                {
                    if (!item.m_customData.ContainsKey(pair.Key) || string.IsNullOrEmpty(item.m_customData[pair.Key]))
                        item.m_customData[pair.Key] = pair.Value;
                }
                item.m_equipped = false;
            }

            if (item != null)
                MagicReload.Apply(item);

            if (item != null)
            {
                foreach (var drop in UnityEngine.Object.FindObjectsByType<ItemDrop>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (drop != null && drop.m_itemData == item)
                    {
                        drop.SetQuality(item.m_quality);
                        drop.Save();
                        break;
                    }
                }
            }

            StandMemory.Clear(zdo, index);
        }
    }
}
