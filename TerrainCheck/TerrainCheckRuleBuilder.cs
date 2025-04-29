namespace GvcRevitPlugins.TerrainCheck
{
    internal abstract class TerrainCheckRuleBuilder
    {
        protected TerrainCheckRule terrainCheckRule;
        public void CreateRule()
        {
            terrainCheckRule = new TerrainCheckRule();
        }
        public TerrainCheckRule GetRule()
        {
            return terrainCheckRule;
        }
        public abstract void SetName();
        public abstract void SetDescription();
        public abstract void SetIsActive();
        public abstract void SetColor();
        public abstract void SetWallTypeId();
        public abstract void SetWallTypeName();
    }
}
