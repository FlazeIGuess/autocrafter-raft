using System.Collections.Generic;
using UnityEngine;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Read-only view model for one recipe row in the picker.
    /// </summary>
    public sealed class RecipeListItemViewModel
    {
        public Item_Base RecipeItem;
        public string RecipeName;
        public Sprite OutputSprite;
        public int OutputAmount;
        public string SearchBlob;
        public readonly List<RecipeIngredientViewModel> Ingredients = new List<RecipeIngredientViewModel>();
    }

    /// <summary>
    /// Ingredient requirement/availability line used by recipe picker rows.
    /// </summary>
    public sealed class RecipeIngredientViewModel
    {
        public string DisplayName;
        public int RequiredAmount;
        public int AvailableAmount;
    }
}