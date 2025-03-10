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

    class ReinforcedSuitMark3 : ReinforcedSuitsCore
    {
        public ReinforcedSuitMark3()
            : base(classID: "reinforcedsuit3", friendlyName: "Reinforced Dive Suit Mark 3", description: "An upgraded dive suit capable of protecting the user at all depths and providing heat protection up to 90C.")
        {
            OnFinishedPatching += SetStaticTechType;
        }

        protected override TechType BaseType { get; } = TechType.ReinforcedDiveSuit;
        protected override EquipmentType DiveSuit { get; } = EquipmentType.Body;

        protected override Atlas.Sprite GetItemSprite()
        {
            string mainDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            return ImageUtils.LoadSpriteFromFile(Path.Combine(mainDirectory, @"Assets\reinforcedsuit3.png"));
        }

        protected override TechData GetBlueprintRecipe()
        {
            return new TechData
            {
                craftAmount = 1,
                Ingredients = new List<Ingredient>(3)
                {
                    new Ingredient(ReinforcedSuit2ID, 1),
                    new Ingredient(TechType.AramidFibers, 1),
                    new Ingredient(TechType.Kyanite, 2),
                    new Ingredient(DummySuitItems.LavaLizardScaleID, 2),
                }
            };
        }

        private void SetStaticTechType() => ReinforcedSuit3ID = this.TechType;
    }
}
