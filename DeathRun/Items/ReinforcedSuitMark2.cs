﻿/**
 * DeathRun mod - Cattlesquat "but standing on the shoulders of giants"
 * 
 * This section taken directly from Seraphim Risen's NitrogenMod
 */
namespace DeathRun.Items
{
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using SMLHelper.V2.Crafting;
    using SMLHelper.V2.Utility;

    class ReinforcedSuitMark2 : ReinforcedSuitsCore
    {
        public ReinforcedSuitMark2()
            : base(classID: "reinforcedsuit2", friendlyName: "Reinforced Dive Suit Mark 2", description: "An upgraded dive suit capable of protecting the user at depths up to 1300m and providing heat protection up to 75C.")
        {
            OnFinishedPatching += SetStaticTechType;
        }

        protected override TechType BaseType { get; } = TechType.ReinforcedDiveSuit;
        protected override EquipmentType DiveSuit { get; } = EquipmentType.Body;

        protected override Atlas.Sprite GetItemSprite()
        {
            string mainDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            return ImageUtils.LoadSpriteFromFile(Path.Combine(mainDirectory, @"Assets\reinforcedsuit2.png"));
        }

        protected override TechData GetBlueprintRecipe()
        {
            return new TechData
            {
                craftAmount = 1,
                Ingredients = new List<Ingredient>(3)
                {
                    new Ingredient(TechType.ReinforcedDiveSuit, 1),
                    new Ingredient(TechType.AramidFibers, 1),
                    new Ingredient(TechType.AluminumOxide, 2),
                    new Ingredient(DummySuitItems.RiverEelScaleID, 2),
                }
            };
        }

        private void SetStaticTechType() => ReinforcedSuit2ID = this.TechType;
    }
}
