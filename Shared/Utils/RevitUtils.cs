using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GvcRevitPlugins.Shared.Utils
{
    internal static class RevitUtils
    {
        public static Material CreateMaterial(UIDocument uiDoc, Color color, int transparency = 0, int shininess = 0)
        {
            Material sampleMaterial = new FilteredElementCollector(uiDoc.Document)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .First();

            if (sampleMaterial == null) 
                return null;

            ElementId materialId = Material.Create(uiDoc.Document, Guid.NewGuid().ToString());
            Material newMaterial = uiDoc.Document.GetElement(materialId) as Material;

            if (newMaterial == null) 
                return null;

            newMaterial.Color = color;
            newMaterial.Transparency = transparency;
            newMaterial.Shininess = shininess;

            return newMaterial;
        }

        public static WallType GetOrCreateWallType(UIDocument uiDoc, string name, BuiltInCategory category = BuiltInCategory.OST_Walls, Color color = null)
        {
            if (uiDoc == null || uiDoc.Document == null || string.IsNullOrEmpty(name))
                return null;

            Document doc = uiDoc.Document;

            WallType existingType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .OfCategory(category)
                .Cast<WallType>()
                .FirstOrDefault(w => w.Name.Equals(name));

            if (existingType != null)
                return existingType;

            WallType baseType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .OfCategory(category)
                .Cast<WallType>()
                .FirstOrDefault();

            if (baseType == null)
                return null;

            WallType newType;

            newType = baseType.Duplicate(name) as WallType;

            CompoundStructure structure = newType.GetCompoundStructure();
            if (structure == null)
                return null;

            double[] widths = 
            {
                Autodesk.Revit.DB.UnitUtils.ConvertToInternalUnits(3, UnitTypeId.Inches),
                Autodesk.Revit.DB.UnitUtils.ConvertToInternalUnits(4, UnitTypeId.Inches),
                Autodesk.Revit.DB.UnitUtils.ConvertToInternalUnits(3, UnitTypeId.Inches) 
            };

            MaterialFunctionAssignment[] functions =
            {
                MaterialFunctionAssignment.Finish1,
                MaterialFunctionAssignment.Structure,
                MaterialFunctionAssignment.Finish2
            };

            Material sampleMaterial = CreateMaterial(uiDoc, color != null? color : new Color(255,255,255));
            ElementId materialId = sampleMaterial?.Id ?? ElementId.InvalidElementId;

            List<CompoundStructureLayer> layers = new();

            for (int i = 0; i < 3; i++)
                layers.Add(new CompoundStructureLayer(widths[i], functions[i], materialId));

            CompoundStructure newStructure = CompoundStructure.CreateSimpleCompoundStructure(layers);
            newType.SetCompoundStructure(newStructure);

            return newType;
        }

        internal static Element[] GetTypesSymbols(Document doc, string fileName)
        {
            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            assemblyPath = assemblyPath.Substring(0, assemblyPath.LastIndexOf('\\'));
            string absolutePath = System.IO.Path.Combine(assemblyPath, fileName);

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            ICollection<Element> foundFamilies = collector.OfClass(typeof(Family)).Where(x => x.Name == fileName.Replace(".rfa", "")).ToList();

            if (foundFamilies.Count > 0) return GetFamilyTypes(doc, foundFamilies.First());

            SubTransaction loadFamilySubTransaction = new SubTransaction(doc);
            loadFamilySubTransaction.Start();
            doc.LoadFamily(absolutePath, out Family family);
            loadFamilySubTransaction.Commit();

            return GetFamilyTypes(doc, family);
        }

        private static Element[] GetFamilyTypes(Document doc, Element family) => (family as Family).GetFamilySymbolIds().Select(x => doc.GetElement(x)).ToArray();
    }
}
