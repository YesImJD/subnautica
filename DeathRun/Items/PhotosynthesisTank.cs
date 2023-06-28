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

    class PhotosynthesisTank : O2TanksCore
    {
        public PhotosynthesisTank()
            : base(classID: "photosynthesistank", friendlyName: "Photosynthesis Tank", description: "A lightweight air tank housing microorganisms which produce oxygen when exposed to sunlight.")
        {
            OnFinishedPatching += SetStaticTechType;
        }

        protected override TechType BaseType { get; } = TechType.PlasteelTank;
        protected override EquipmentType SpecialtyO2Tank { get; } = EquipmentType.Tank;

        protected override Atlas.Sprite GetItemSprite()
        {
            string mainDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            return ImageUtils.LoadSpriteFromFile(Path.Combine(mainDirectory, @"Assets\photosynthesistank.png"));
        }

        protected override TechData GetBlueprintRecipe()
        {
            return new TechData
            {
                craftAmount = 1,
                Ingredients = new List<Ingredient>(3)
                {
                    new Ingredient(TechType.PlasteelTank, 1),
                    new Ingredient(TechType.PurpleBrainCoralPiece, 2),
                    new Ingredient(TechType.EnameledGlass, 1),
                }
            };
        }

        private void SetStaticTechType() => PhotosynthesisTankID = this.TechType;

        public override TechType RequiredForUnlock { get; } = TechType.PlasteelTank;
    }
}
