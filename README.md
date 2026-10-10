# Armor Stand Enchant

Enchanted EpicLoot gear keeps its rarity, enchants, quality, and durability when it is taken off an armor stand.

The stand rebuilds the item from the plain prefab. This mod stores a copy of the item at the moment it is placed, and writes that copy back if the stand hands back a blank or worn-down item. EpicLoot is then asked to read the enchantment again.

Gear already on a stand when this mod is added was never copied, so take that gear off before you rely on it, or put it back on once so a copy is stored.

Both the client and the server need this mod. The player who owns the stand is the one who saves and loads the item.

## Install

Copy `Cjayride.ArmorStandEnchant.dll` into `BepInEx/plugins`.
