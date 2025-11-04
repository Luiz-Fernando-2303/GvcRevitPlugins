using Autodesk.Revit.ApplicationServices;
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
                .FirstOrDefault(w => w.GetCompoundStructure() != null);

            if (baseType == null)
                throw new InvalidOperationException("Nenhum WallType válido com CompoundStructure encontrado no documento.");

            WallType newType = baseType.Duplicate(name) as WallType;

            CompoundStructure structure = newType.GetCompoundStructure();
            if (structure == null)
                throw new InvalidOperationException("O WallType base não possui CompoundStructure.");

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

            Material sampleMaterial = CreateMaterial(uiDoc, color ?? new Color(255, 255, 255), 70);
            ElementId materialId = sampleMaterial?.Id ?? ElementId.InvalidElementId;

            List<CompoundStructureLayer> layers = new();
            for (int i = 0; i < 3; i++)
                layers.Add(new CompoundStructureLayer(widths[i], functions[i], materialId));

            CompoundStructure newStructure = CompoundStructure.CreateSimpleCompoundStructure(layers);
            newType.SetCompoundStructure(newStructure);

            EnsureAndSetWallTypeParameters(doc, newType, new Dictionary<string, string>
            {
                { "Source", "GVC_CEF" },
                { "ResultType", $"{name}" },
                { "CreatedOn", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                { "CreatedBy", Environment.UserName },
                { "ColorR", (color?.Red ?? 255).ToString() },
                { "ColorG", (color?.Green ?? 255).ToString() },
                { "ColorB", (color?.Blue ?? 255).ToString() }
            });

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

        public static void EnsureAndSetWallTypeParameters(Document doc, WallType wallType, Dictionary<string, string> parameters)
        {
            if (doc == null || wallType == null || parameters == null || parameters.Count == 0)
                return;

            if (!doc.IsModifiable)
            {
                using (Transaction transaction = new Transaction(doc, "____"))
                {
                    transaction.Start();
                    Execute();
                    transaction.Commit();
                }
            }
            else
            {
                Execute();
            }

            void Execute()
            {
                Application app = doc.Application;

                string sharedParamPath = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "GVB_SharedParams.txt"
                );

                if (!System.IO.File.Exists(sharedParamPath))
                    System.IO.File.WriteAllText(sharedParamPath, "");

                app.SharedParametersFilename = sharedParamPath;
                DefinitionFile defFile = app.OpenSharedParameterFile();
                if (defFile == null)
                    throw new InvalidOperationException("Não foi possível abrir/gerar shared parameter file automático.");


                CategorySet catSet = app.Create.NewCategorySet();
                catSet.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Walls));

                BindingMap bindingMap = doc.ParameterBindings;
                TypeBinding binding = app.Create.NewTypeBinding(catSet);

                DefinitionGroup group = defFile.Groups.get_Item("GVB_Params") ?? defFile.Groups.Create("GVB_Params");

                foreach (var kvp in parameters)
                {
                    string paramName = kvp.Key;

                    if (wallType.LookupParameter(paramName) != null)
                        continue;

                    Definition def = group.Definitions.get_Item(paramName);
                    if (def == null)
                    {
                        ForgeTypeId spec = SpecTypeId.String.Text;

                        var opt = new ExternalDefinitionCreationOptions(paramName, spec)
                        {
                            Visible = true
                        };
                        def = group.Definitions.Create(opt);
                    }

                    bindingMap.Insert(def, binding, GroupTypeId.Text);
                }

                foreach (var kvp in parameters)
                {
                    string paramName = kvp.Key;
                    string value = kvp.Value;

                    Parameter param = wallType.LookupParameter(paramName);
                    if (param == null || param.IsReadOnly) continue;

                    switch (param.StorageType)
                    {
                        case StorageType.String:
                            param.Set(value);
                            break;
                        case StorageType.Double:
                            if (double.TryParse(value, out double d))
                                param.Set(d);
                            break;
                        case StorageType.Integer:
                            if (int.TryParse(value, out int i))
                                param.Set(i);
                            break;
                        case StorageType.ElementId:
                            if (int.TryParse(value, out int id))
                                param.Set(new ElementId(id));
                            break;
                    }
                }
            }
        }
    }
}
