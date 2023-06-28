/**
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

    class ChemosynthesisTank : O2TanksCore
    {
        public ChemosynthesisTank()
            : base(classID: "chemosynthesistank", friendlyName: "Chemosynthesis Tank", description: "A lightweight O2 tank that houses microorganisms that produce oxygen under high temperatures.")
        {
            OnFinishedPatching += SetStaticTechType;
        }

        protected override TechType BaseType { get; } = TechType.PlasteelTank;
        protected override EquipmentType SpecialtyO2Tank { get; } = EquipmentType.Tank;

        protected override Atlas.Sprite GetItemSprite()
        {
            string mainDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            return ImageUtils.LoadSpriteFromFile(Path.Combine(mainDirectory, @"Assets\chemosynthesistank.png"));
        }

        protected override TechData GetBlueprintRecipe()
        {
            return new TechData
            {
                craftAmount = 1,
                Ingredients = new List<Ingredient>(3)
                {
                    new Ingredient(TechType.PlasteelTank, 1),
                    new Ingredient(DummySuitItems.ThermoBacteriaID, 4),
                    new Ingredient(TechType.Kyanite, 1),
                }
            };
        }

        private void SetStaticTechType() => ChemosynthesisTankID = this.TechType;
    }
}
