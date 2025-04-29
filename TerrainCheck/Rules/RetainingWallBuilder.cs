using System;

namespace GvcRevitPlugins.TerrainCheck.Rules
{
    internal class RetainingWallBuilder : TerrainCheckRuleBuilder
    {
        public override void SetColor()
        {
            throw new NotImplementedException();
        }

        public override void SetDescription()
        {
            GetRule().Description = "Arrimo - ";
        }

        public override void SetIsActive()
        {
            //bool isActive = LupaRevitUI.Commands.RevitCommands.RetainingWallIsActive;
            bool isActive = true;
            GetRule().IsActive = isActive;
        }

        public override void SetName()
        {
            //string name = LupaRevitUI.Commands.RevitCommands.RetainingWallName;
            string name = "Retaining Wall";
            GetRule().Name = name;
        }

        public override void SetWallTypeId()
        {
            //int wallTypeId = LupaRevitUI.Commands.RevitCommands.WallTypeId;
            int wallTypeId = 0;
            GetRule().WallTypeId = wallTypeId;
        }

        public override void SetWallTypeName()
        {
            //string wallTypeName = LupaRevitUI.Commands.RevitCommands.WallTypeName;
            string wallTypeName = "Muro de Arrimo";
            GetRule().WallTypeName = wallTypeName;
        }
    }
}
