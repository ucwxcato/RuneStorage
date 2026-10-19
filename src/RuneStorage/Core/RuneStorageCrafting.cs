using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RuneStorage
{
    internal sealed class RuneStorageCraftingStationGroup
    {
        internal string Identity { get; set; }
        internal string DisplayName { get; set; }
        internal int NearbyLevel { get; set; }
        internal bool RequiresStation { get; set; }
        internal List<RuneStorageCraftingRecipe> Recipes { get; } = new List<RuneStorageCraftingRecipe>();
    }

    internal sealed class RuneStorageCraftingIngredient
    {
        internal string Identity { get; set; }
        internal string SharedName { get; set; }
        internal string DisplayName { get; set; }
        internal int WorldLevel { get; set; }
        internal int Required { get; set; }
        internal int Available { get; set; }
    }

    internal sealed class RuneStorageCraftingRecipe
    {
        internal string Identity { get; set; }
        internal string DisplayName { get; set; }
        internal Sprite Icon { get; set; }
        internal int OutputAmount { get; set; }
        internal int RequiredStationLevel { get; set; }
        internal int NearbyStationLevel { get; set; }
        internal bool StationAvailable { get; set; }
        internal bool IngredientsAvailable { get; set; }
        internal bool OutputAvailable { get; set; }
        internal bool IsSupported { get; set; }
        internal string IngredientSummary { get; set; }
        internal string DisabledReason { get; set; }
        internal List<RuneStorageCraftingIngredient> Ingredients { get; set; }
        internal Recipe Recipe { get; set; }

        internal bool CanCraft => IsSupported && StationAvailable && IngredientsAvailable && OutputAvailable;
    }

    internal static class RuneStorageCrafting
    {
        internal const string HandCraftingStationId = "RuneStorage_HandCrafting";

        internal static List<RuneStorageCraftingStationGroup> GetStationGroups(
            Player player,
            Container core,
            bool outputToCore)
        {
            var groups = new Dictionary<string, RuneStorageCraftingStationGroup>(StringComparer.Ordinal);
            if (player == null || core == null || ObjectDB.instance == null || ObjectDB.instance.m_recipes == null)
            {
                return new List<RuneStorageCraftingStationGroup>();
            }

            Inventory coreInventory = core.GetInventory();
            if (coreInventory == null) return new List<RuneStorageCraftingStationGroup>();

            foreach (Recipe recipe in ObjectDB.instance.m_recipes)
            {
                if (!IsKnownNormalRecipe(player, recipe)) continue;

                CraftingStation requiredStation = recipe.GetRequiredStation(1);
                string stationId = GetStationIdentity(requiredStation);
                if (!groups.TryGetValue(stationId, out RuneStorageCraftingStationGroup group))
                {
                    group = new RuneStorageCraftingStationGroup
                    {
                        Identity = stationId,
                        DisplayName = GetStationDisplayName(requiredStation),
                        RequiresStation = requiredStation != null,
                        NearbyLevel = requiredStation == null ? 0 : FindBestNearbyLevel(player, requiredStation)
                    };
                    groups.Add(stationId, group);
                }

                group.Recipes.Add(BuildRecipeView(player, coreInventory, recipe, group, outputToCore));
            }

            return groups.Values
                .OrderBy(group => group.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.Identity, StringComparer.Ordinal)
                .ToList();
        }

        internal static string Craft(
            Player player,
            RuneStorageTerminalMarker terminal,
            RuneStorageCoreMarker expectedCore,
            string recipeIdentity,
            bool outputToCore)
        {
            if (!RuneStorageCoreAccess.TryResolve(player, terminal, out RuneStorageCoreMarker marker, out Container core, out string reason))
            {
                return reason;
            }

            if (marker != expectedCore)
            {
                return "The Core connected to this terminal changed. Reopen the terminal and try again.";
            }

            if (!core.IsOwner())
            {
                return "The Core owner must validate this craft. The request was not run locally.";
            }

            if (!RuneStorageTransactions.TryBeginCoreOperation(marker, out int operationId))
            {
                return "The Core is already handling another operation. Try again in a moment.";
            }

            List<ItemDrop.ItemData> removedIngredients = null;
            bool ingredientsConsumed = false;
            try
            {
                if (!RuneStorageCoreAccess.TryResolve(player, terminal, out RuneStorageCoreMarker currentMarker, out Container currentCore, out reason))
                {
                    return reason;
                }

                if (currentMarker != marker || currentCore != core)
                {
                    return "The Core connected to this terminal changed. Reopen the terminal and try again.";
                }

                if (marker.IsReadOnly)
                {
                    return marker.StatusMessage ?? "The Core is read-only. No ingredients were used.";
                }

                Recipe recipe = ResolveRecipe(player, recipeIdentity);
                if (recipe == null)
                {
                    return "That recipe is no longer available. The recipe list has been refreshed.";
                }

                CraftingStation requiredStation = recipe.GetRequiredStation(1);
                int requiredLevel = requiredStation != null ? recipe.GetRequiredStationLevel(1) : 0;
                if (requiredStation != null && !HasNearbyStationAtLevel(player, requiredStation, requiredLevel))
                {
                    return $"A nearby {GetStationDisplayName(requiredStation)} at level {requiredLevel} or higher is required.";
                }

                Inventory coreInventory = core.GetInventory();
                Inventory playerInventory = player.GetInventory();
                if (coreInventory == null || playerInventory == null)
                {
                    return "A player or Core inventory is unavailable. No ingredients were used.";
                }

                List<RuneStorageCraftingIngredient> ingredients = GetIngredients(recipe, coreInventory);
                if (!IsSupportedRecipe(recipe, ingredients, out string unsupportedReason))
                {
                    return unsupportedReason;
                }

                RuneStorageCraftingIngredient missing = ingredients.FirstOrDefault(ingredient => ingredient.Available < ingredient.Required);
                if (missing != null)
                {
                    return $"The Core needs {missing.Required - missing.Available} more {missing.DisplayName}. No ingredients were used.";
                }

                Inventory destination = outputToCore ? coreInventory : playerInventory;
                ItemDrop.ItemData output = CreateOutput(recipe, player);
                if (output == null)
                {
                    return "Valheim could not create this recipe's output. No ingredients were used.";
                }

                if (!CanAcceptOutput(destination, coreInventory, output, ingredients, outputToCore))
                {
                    return outputToCore
                        ? "The Core has no room for the complete output after this craft. No ingredients were used."
                        : "Your inventory has no room for the complete output. No ingredients were used.";
                }

                string outputIdentity = RuneStorageItemGroups.GetIdentity(output);
                if (!TryConsumeIngredients(coreInventory, ingredients, out removedIngredients))
                {
                    if (!RestoreIngredients(coreInventory, removedIngredients, marker))
                    {
                        return marker.StatusMessage ?? "Ingredient recovery failed. The Core is now read-only to protect its contents.";
                    }

                    return "The Core ingredients changed before the craft could be committed. Nothing was crafted.";
                }
                ingredientsConsumed = true;
                long outputCountBefore = CountExactIdentity(destination, outputIdentity);

                bool outputAdded;
                try
                {
                    outputAdded = destination.AddItem(output);
                }
                catch (Exception exception)
                {
                    Plugin.Log.LogError($"RuneStorage output delivery failed: {exception}");
                    outputAdded = false;
                }

                long deliveredCount = CountExactIdentity(destination, outputIdentity) - outputCountBefore;
                if (!outputAdded || deliveredCount != recipe.m_amount)
                {
                    if (deliveredCount > 0 && !RemoveExactIdentity(destination, outputIdentity, deliveredCount))
                    {
                        marker.SetReadOnly("RuneStorage could not reverse partial craft output delivery.");
                        ingredientsConsumed = false;
                        return "Output delivery was incomplete and could not be safely reversed. The Core is read-only; inspect both inventories before continuing.";
                    }

                    if (!RestoreIngredients(coreInventory, removedIngredients, marker))
                    {
                        return marker.StatusMessage ?? "Output delivery and ingredient recovery failed. The Core is now read-only.";
                    }
                    ingredientsConsumed = false;

                    return "The output could not be added. The ingredients were restored; free inventory space and try again.";
                }

                ingredientsConsumed = false;
                return $"Crafted {GetItemDisplayName(recipe.m_item.m_itemData)} x{recipe.m_amount.ToString(CultureInfo.InvariantCulture)} to {(outputToCore ? "the Core" : "your inventory")}.";
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"RuneStorage craft failed: {exception}");
                if (ingredientsConsumed && !RestoreIngredients(core.GetInventory(), removedIngredients, marker))
                {
                    return marker.StatusMessage ?? "The craft failed and ingredient recovery was incomplete. The Core is now read-only.";
                }
                return "The craft stopped after an inventory error. Check the Core and output inventory before trying again.";
            }
            finally
            {
                RuneStorageTransactions.EndCoreOperation(operationId);
            }
        }

        internal static Recipe ResolveRecipe(Player player, string recipeIdentity)
        {
            if (player == null || string.IsNullOrEmpty(recipeIdentity) || ObjectDB.instance == null || ObjectDB.instance.m_recipes == null)
            {
                return null;
            }

            return ObjectDB.instance.m_recipes.FirstOrDefault(recipe =>
                IsKnownNormalRecipe(player, recipe)
                && string.Equals(GetRecipeIdentity(recipe), recipeIdentity, StringComparison.Ordinal));
        }

        private static RuneStorageCraftingRecipe BuildRecipeView(
            Player player,
            Inventory coreInventory,
            Recipe recipe,
            RuneStorageCraftingStationGroup stationGroup,
            bool outputToCore)
        {
            List<RuneStorageCraftingIngredient> ingredients = GetIngredients(recipe, coreInventory);
            bool supported = IsSupportedRecipe(recipe, ingredients, out string unsupportedReason);
            int requiredLevel = stationGroup.RequiresStation ? recipe.GetRequiredStationLevel(1) : 0;
            bool stationAvailable = !stationGroup.RequiresStation || stationGroup.NearbyLevel >= requiredLevel;
            bool ingredientsAvailable = ingredients.All(ingredient => ingredient.Available >= ingredient.Required);
            ItemDrop.ItemData output = CreateOutput(recipe, player);
            Inventory playerInventory = player.GetInventory();
            bool outputAvailable = output != null && (outputToCore
                ? CanAcceptOutput(coreInventory, coreInventory, output, ingredients, true)
                : playerInventory != null && playerInventory.CanAddItem(output, output.m_stack));

            string stationStatus = stationGroup.RequiresStation
                ? stationAvailable
                    ? $"{stationGroup.DisplayName} Lv. {requiredLevel} required; nearby Lv. {stationGroup.NearbyLevel}"
                    : stationGroup.NearbyLevel > 0
                        ? $"Needs {stationGroup.DisplayName} Lv. {requiredLevel}; nearby station is Lv. {stationGroup.NearbyLevel}"
                        : $"No {stationGroup.DisplayName} within crafting range"
                : "No station required";

            string ingredientStatus = ingredients.Count == 0
                ? "No Core ingredients required"
                : string.Join("   ", ingredients.Select(ingredient =>
                    $"{ingredient.DisplayName} {ingredient.Available.ToString(CultureInfo.InvariantCulture)}/{ingredient.Required.ToString(CultureInfo.InvariantCulture)}"));

            string disabledReason = !supported
                ? unsupportedReason
                : !stationAvailable
                    ? stationStatus
                    : !ingredientsAvailable
                        ? "The Core does not hold all required ingredients."
                        : !outputAvailable
                            ? outputToCore ? "The Core cannot fit the full output." : "Your inventory cannot fit the full output."
                            : null;

            return new RuneStorageCraftingRecipe
            {
                Identity = GetRecipeIdentity(recipe),
                DisplayName = GetItemDisplayName(recipe.m_item.m_itemData),
                Icon = recipe.m_item.m_itemData.GetIcon(),
                OutputAmount = recipe.m_amount,
                RequiredStationLevel = requiredLevel,
                NearbyStationLevel = stationGroup.NearbyLevel,
                StationAvailable = stationAvailable,
                IngredientsAvailable = ingredientsAvailable,
                OutputAvailable = outputAvailable,
                IsSupported = supported,
                IngredientSummary = $"{stationStatus}   |   {ingredientStatus}",
                DisabledReason = disabledReason,
                Ingredients = ingredients,
                Recipe = recipe
            };
        }

        private static bool IsKnownNormalRecipe(Player player, Recipe recipe)
        {
            if (player == null || recipe == null || !recipe.m_enabled || recipe.m_item == null
                || recipe.m_item.m_itemData == null || recipe.m_item.m_itemData.m_shared == null)
            {
                return false;
            }

            if (recipe.m_noCraftOnlyUpgrade || recipe.m_repairStation != null) return false;
            string sharedName = recipe.m_item.m_itemData.m_shared.m_name;
            string prefabName = recipe.m_item.gameObject.name;
            return player.IsRecipeKnown(sharedName) || player.IsRecipeKnown(prefabName);
        }

        private static bool IsSupportedRecipe(
            Recipe recipe,
            List<RuneStorageCraftingIngredient> ingredients,
            out string reason)
        {
            if (recipe == null || recipe.m_item == null || recipe.m_item.m_itemData == null || recipe.m_amount <= 0)
            {
                reason = "Valheim marked this recipe as incomplete. It cannot be crafted from RuneStorage.";
                return false;
            }

            if (recipe.m_craftingStation != null && recipe.GetRequiredStation(1) == null)
            {
                reason = "Valheim could not resolve this recipe's required crafting station.";
                return false;
            }

            if (recipe.m_requireOnlyOneIngredient)
            {
                reason = "This recipe uses Valheim's alternative-ingredient rule, which RuneStorage does not support yet.";
                return false;
            }

            if (recipe.m_resources != null && recipe.m_resources.Any(requirement =>
                requirement == null
                || requirement.m_resItem == null
                || requirement.m_resItem.m_itemData == null
                || requirement.m_resItem.m_itemData.m_shared == null
                || string.IsNullOrEmpty(requirement.m_resItem.m_itemData.m_shared.m_name)
                || requirement.GetAmount(1) <= 0))
            {
                reason = "This recipe has an ingredient RuneStorage cannot safely identify.";
                return false;
            }

            if (ingredients == null || ingredients.Any(ingredient => ingredient.Required <= 0 || string.IsNullOrEmpty(ingredient.SharedName)))
            {
                reason = "This recipe has an ingredient RuneStorage cannot safely identify.";
                return false;
            }

            reason = null;
            return true;
        }

        private static List<RuneStorageCraftingIngredient> GetIngredients(Recipe recipe, Inventory inventory)
        {
            var requiredByIdentity = new Dictionary<string, RuneStorageCraftingIngredient>(StringComparer.Ordinal);
            if (recipe == null || recipe.m_resources == null) return new List<RuneStorageCraftingIngredient>();

            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (requirement == null || requirement.m_resItem == null || requirement.m_resItem.m_itemData == null
                    || requirement.m_resItem.m_itemData.m_shared == null)
                {
                    continue;
                }

                ItemDrop.ItemData requiredItem = requirement.m_resItem.m_itemData;
                string sharedName = requiredItem.m_shared.m_name;
                int amount = requirement.GetAmount(1);
                if (string.IsNullOrEmpty(sharedName) || amount <= 0) continue;

                string identity = GetIngredientIdentity(sharedName, requiredItem.m_worldLevel);
                if (!requiredByIdentity.TryGetValue(identity, out RuneStorageCraftingIngredient ingredient))
                {
                    ingredient = new RuneStorageCraftingIngredient
                    {
                        Identity = identity,
                        SharedName = sharedName,
                        DisplayName = GetItemDisplayName(requiredItem),
                        WorldLevel = requiredItem.m_worldLevel
                    };
                    requiredByIdentity.Add(identity, ingredient);
                }

                ingredient.Required = checked(ingredient.Required + amount);
            }

            List<RuneStorageCraftingIngredient> result = requiredByIdentity.Values
                .OrderBy(ingredient => ingredient.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(ingredient => ingredient.Identity, StringComparer.Ordinal)
                .ToList();

            if (inventory != null)
            {
                foreach (RuneStorageCraftingIngredient ingredient in result)
                {
                    ingredient.Available = CountIngredient(inventory, ingredient);
                }
            }

            return result;
        }

        private static int CountIngredient(Inventory inventory, RuneStorageCraftingIngredient ingredient)
        {
            if (inventory == null || ingredient == null) return 0;
            long count = inventory.GetAllItems()
                .Where(item => MatchesIngredient(item, ingredient))
                .Sum(item => (long)Math.Max(0, item.m_stack));
            return (int)Math.Min(int.MaxValue, count);
        }

        private static long CountExactIdentity(Inventory inventory, string identity)
        {
            if (inventory == null || string.IsNullOrEmpty(identity)) return 0;
            return inventory.GetAllItems()
                .Where(item => string.Equals(RuneStorageItemGroups.GetIdentity(item), identity, StringComparison.Ordinal))
                .Sum(item => (long)Math.Max(0, item.m_stack));
        }

        private static bool RemoveExactIdentity(Inventory inventory, string identity, long requested)
        {
            if (inventory == null || string.IsNullOrEmpty(identity) || requested < 0) return false;
            long before = CountExactIdentity(inventory, identity);
            long remaining = requested;

            foreach (ItemDrop.ItemData item in inventory.GetAllItems()
                .Where(candidate => string.Equals(RuneStorageItemGroups.GetIdentity(candidate), identity, StringComparison.Ordinal))
                .ToArray())
            {
                if (remaining <= 0) break;
                int amount = (int)Math.Min(remaining, item.m_stack);
                if (amount <= 0) continue;

                long countBefore = CountExactIdentity(inventory, identity);
                bool removed;
                try
                {
                    removed = inventory.RemoveItem(item, amount);
                }
                catch (Exception exception)
                {
                    Plugin.Log.LogError($"RuneStorage could not reverse craft output: {exception}");
                    return false;
                }

                long countAfter = CountExactIdentity(inventory, identity);
                long actual = Math.Max(0, countBefore - countAfter);
                remaining -= actual;
                if (!removed || actual != amount) break;
            }

            return remaining == 0 && before - CountExactIdentity(inventory, identity) == requested;
        }

        private static bool MatchesIngredient(ItemDrop.ItemData item, RuneStorageCraftingIngredient ingredient)
        {
            return item != null && item.m_shared != null && ingredient != null
                && string.Equals(item.m_shared.m_name, ingredient.SharedName, StringComparison.Ordinal)
                && item.m_worldLevel == ingredient.WorldLevel;
        }

        private static int FindBestNearbyLevel(Player player, CraftingStation requiredStation)
        {
            if (player == null || requiredStation == null) return 0;

            int bestLevel = 0;
            foreach (CraftingStation station in UnityEngine.Object.FindObjectsByType<CraftingStation>(FindObjectsSortMode.None))
            {
                if (station == null || !SameStationType(station, requiredStation) || !station.InUseDistance(player)) continue;
                bestLevel = Math.Max(bestLevel, station.GetLevel(true));
            }

            return bestLevel;
        }

        private static bool HasNearbyStationAtLevel(Player player, CraftingStation requiredStation, int requiredLevel)
        {
            if (player == null || requiredStation == null) return false;

            foreach (CraftingStation station in UnityEngine.Object.FindObjectsByType<CraftingStation>(FindObjectsSortMode.None))
            {
                if (station == null || !SameStationType(station, requiredStation) || !station.InUseDistance(player)) continue;
                if (station.GetLevel(true) >= requiredLevel) return true;
            }

            return false;
        }

        private static bool SameStationType(CraftingStation instance, CraftingStation required)
        {
            if (instance == null || required == null) return false;
            if (!string.IsNullOrEmpty(instance.m_name) && string.Equals(instance.m_name, required.m_name, StringComparison.Ordinal)) return true;

            return string.Equals(NormalizePrefabName(instance.gameObject.name), NormalizePrefabName(required.gameObject.name), StringComparison.Ordinal);
        }

        private static string GetStationIdentity(CraftingStation station)
        {
            return station == null ? HandCraftingStationId : NormalizePrefabName(station.gameObject.name);
        }

        private static string GetStationDisplayName(CraftingStation station)
        {
            if (station == null) return "Hand";
            return Localize(!string.IsNullOrEmpty(station.m_name) ? station.m_name : station.gameObject.name);
        }

        private static string NormalizePrefabName(string name)
        {
            return (name ?? string.Empty).Replace("(Clone)", string.Empty).Trim();
        }

        private static string GetRecipeIdentity(Recipe recipe)
        {
            CraftingStation station = recipe.GetRequiredStation(1);
            List<RuneStorageCraftingIngredient> ingredients = GetIngredients(recipe, null);
            var builder = new StringBuilder(192);
            Append(builder, recipe.name ?? string.Empty);
            Append(builder, recipe.m_item != null ? NormalizePrefabName(recipe.m_item.gameObject.name) : string.Empty);
            Append(builder, GetStationIdentity(station));
            Append(builder, (station != null ? recipe.GetRequiredStationLevel(1) : 0).ToString(CultureInfo.InvariantCulture));
            Append(builder, recipe.m_amount.ToString(CultureInfo.InvariantCulture));
            Append(builder, recipe.m_requireOnlyOneIngredient ? "alternative-ingredients" : "all-ingredients");
            Append(builder, recipe.m_qualityResultAmountMultiplier.ToString("R", CultureInfo.InvariantCulture));
            foreach (RuneStorageCraftingIngredient ingredient in ingredients.OrderBy(value => value.Identity, StringComparer.Ordinal))
            {
                Append(builder, ingredient.Identity);
                Append(builder, ingredient.Required.ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string GetIngredientIdentity(string sharedName, int worldLevel)
        {
            return sharedName.Length.ToString(CultureInfo.InvariantCulture) + ":" + sharedName + "|"
                + worldLevel.ToString(CultureInfo.InvariantCulture);
        }

        private static ItemDrop.ItemData CreateOutput(Recipe recipe, Player player)
        {
            if (player == null || recipe == null || recipe.m_item == null || recipe.m_item.m_itemData == null || recipe.m_amount <= 0) return null;
            GameObject outputPrefab = recipe.m_item.gameObject;
            if (outputPrefab == null) return null;

            ItemDrop.ItemData output = recipe.m_item.m_itemData.Clone();
            output.m_dropPrefab = outputPrefab;
            output.m_stack = recipe.m_amount;
            output.m_crafterID = player.GetPlayerID();
            output.m_crafterName = player.GetPlayerName();
            return output;
        }

        private static bool CanAcceptOutput(
            Inventory destination,
            Inventory coreInventory,
            ItemDrop.ItemData output,
            List<RuneStorageCraftingIngredient> ingredients,
            bool outputToCore)
        {
            if (destination == null || output == null || output.m_stack <= 0) return false;
            if (!outputToCore) return destination.CanAddItem(output, output.m_stack);
            if (coreInventory == null || ingredients == null) return false;

            var projected = new Inventory("RuneStorage craft capacity check", null, coreInventory.GetWidth(), coreInventory.GetHeight());
            var remainingCosts = ingredients.ToDictionary(ingredient => ingredient.Identity, ingredient => ingredient.Required, StringComparer.Ordinal);
            foreach (ItemDrop.ItemData current in coreInventory.GetAllItems())
            {
                if (current == null || current.m_stack <= 0) continue;

                ItemDrop.ItemData projectedItem = current.Clone();
                RuneStorageCraftingIngredient matchingIngredient = ingredients.FirstOrDefault(ingredient => MatchesIngredient(current, ingredient));
                if (matchingIngredient != null && remainingCosts.TryGetValue(matchingIngredient.Identity, out int remaining) && remaining > 0)
                {
                    int consumed = Math.Min(remaining, projectedItem.m_stack);
                    projectedItem.m_stack -= consumed;
                    remainingCosts[matchingIngredient.Identity] = remaining - consumed;
                }

                if (projectedItem.m_stack <= 0) continue;
                if (!projected.AddItem(projectedItem, current.m_gridPos)) return false;
            }

            if (remainingCosts.Values.Any(remaining => remaining > 0)) return false;
            return projected.CanAddItem(output, output.m_stack);
        }

        private static bool TryConsumeIngredients(
            Inventory inventory,
            List<RuneStorageCraftingIngredient> ingredients,
            out List<ItemDrop.ItemData> removed)
        {
            removed = new List<ItemDrop.ItemData>();
            try
            {
                foreach (RuneStorageCraftingIngredient ingredient in ingredients)
                {
                    int remaining = ingredient.Required;
                    foreach (ItemDrop.ItemData item in inventory.GetAllItems().Where(candidate => MatchesIngredient(candidate, ingredient)).ToArray())
                    {
                        if (remaining <= 0) break;
                        int amount = Math.Min(remaining, item.m_stack);
                        if (amount <= 0) continue;

                        int before = CountIngredient(inventory, ingredient);
                        bool removeSucceeded = inventory.RemoveItem(item, amount);
                        int after = CountIngredient(inventory, ingredient);
                        int actuallyRemoved = Math.Max(0, before - after);
                        if (actuallyRemoved > 0)
                        {
                            ItemDrop.ItemData restore = item.Clone();
                            restore.m_stack = actuallyRemoved;
                            removed.Add(restore);
                            remaining -= actuallyRemoved;
                        }

                        if (!removeSucceeded || actuallyRemoved != amount) return false;
                    }

                    if (remaining > 0) return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"RuneStorage ingredient removal failed: {exception}");
                return false;
            }
        }

        private static bool RestoreIngredients(
            Inventory inventory,
            List<ItemDrop.ItemData> removed,
            RuneStorageCoreMarker marker)
        {
            if (removed == null || removed.Count == 0) return true;

            foreach (ItemDrop.ItemData item in removed)
            {
                try
                {
                    if (inventory.AddItem(item, item.m_gridPos)) continue;
                    if (inventory.AddItem(item)) continue;
                }
                catch (Exception exception)
                {
                    Plugin.Log.LogError($"RuneStorage ingredient restoration failed: {exception}");
                }

                marker.SetReadOnly("RuneStorage could not restore ingredients after an interrupted craft transaction.");
                Plugin.Log.LogError($"RuneStorage could not restore ingredient '{item.m_shared?.m_name}' to Core '{marker.name}'.");
                return false;
            }

            return true;
        }

        private static string GetItemDisplayName(ItemDrop.ItemData item)
        {
            string rawName = item != null && item.m_shared != null ? item.m_shared.m_name : null;
            if (string.IsNullOrEmpty(rawName)) return item != null && item.m_dropPrefab != null ? item.m_dropPrefab.name : "Unknown item";
            return Localize(rawName);
        }

        private static string Localize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Unknown";
            try
            {
                if (Localization.instance != null)
                {
                    string localized = Localization.instance.Localize(value);
                    if (!string.IsNullOrEmpty(localized)) return localized;
                }
            }
            catch (Exception)
            {
                return value;
            }

            return value.TrimStart('$');
        }

        private static void Append(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(value);
            builder.Append('|');
        }
    }
}
