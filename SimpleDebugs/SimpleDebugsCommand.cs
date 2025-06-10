using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GvcRevitPlugins.SimpleDebugs.Tests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using Parameter = Autodesk.Revit.DB.Parameter;

namespace GvcRevitPlugins.SimpleDebugs
{
    [Transaction(TransactionMode.Manual)]
    public class SimpleDebugsCommand : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;
            UIApplication uiApp = commandData.Application;

            //Int64 id = 21088;
            //var parameterElementId = new ElementId(id);
            //Element tst = doc.GetElement(parameterElementId);
            //ParameterElement parameterElement = null;
            //ParameterValueProvider provider = new ParameterValueProvider(parameterElementId);
            //var evaluator = new FilterStringEquals();
            //var rule = new FilterStringRule(provider, evaluator, string.Empty);
            //var filter = new ElementParameterFilter(rule);

            //string paramName = "sadasd";

            //// Collect elements with the parameter
            //var elementsWithParameter = new FilteredElementCollector(doc)
            //    .WhereElementIsNotElementType()
            //    //.WherePasses(filter)
            //    //.Where<Element>(x => x is FamilyInstance || x is HostObject)
            //    .Where(x => x.LookupParameter(paramName) != null)
            //    .ToArray();


            //SearchParameter(doc, "Sólido topográfico", "Parametro Teste Query 01", "Sólido Topográfico Exemplo 01", "Parametro Teste", "row 25");
            //PlaceImagesTest.FullPipeline(uiApp, doc);
            //return Result.Succeeded;

            //PlaceImagesTest.QueryParameters(uiApp, doc);
            //return Result.Succeeded;
            //PlaceImagesTest.PlaceImages(uiApp, doc); //Working!!
            //PlaceImagesTest.CreateProjectParameter(uiApp, doc);
            //return Result.Succeeded;

            return Result.Succeeded;

        }
        public static List<string> CategoryRowsNotFound = new List<string>();
        
        private void SearchParameter(Document doc, string ElementCategory, string queryParameterName, string queryParameterValue, string lookupParameterName, string rowInfo)
        {
            Category category = GetCategoryFromString(doc, ElementCategory);
            Categories tst = doc.Settings.Categories;

            if (!SearchCategory(ElementCategory, out long builtInCategoryId))
            {
                CategoryRowsNotFound.Add(rowInfo);
                return;
            }

            Element[] element = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfCategory((BuiltInCategory)builtInCategoryId)
                .ToElements()
                .ToArray();

            IEnumerable<Element> test = element.Where(x => ElementQueryPredicate(x, queryParameterName, queryParameterValue));
            if (test != null && test.Count() > 0)
                Console.WriteLine(test);

            return;
        }
        public static void SearchProjectParameter(Document doc, string parameterName, int parameterId, string parameterGuid)
        {
            if (parameterGuid is null)
                return;


            ElementId id = new ElementId(parameterId);
            Element element = doc.GetElement(id);

            Parameter param = element.LookupParameter(parameterName);
            if (param != null)
            {
                string value = param.AsString();
                Console.WriteLine(value);
            }

        }
        public static bool ElementQueryPredicate(Element element, string parameterName, string parameterValue)
        {
            Parameter param = element.GetParameters(parameterName).First();
            if (param is null)
                return false;

            string paramValue = param.AsValueString(formatOptions: new FormatOptions());
            //string paramValue = param.AsString(); //TODO: Works only when is string
            return paramValue == parameterValue;
        }
        
        public static Category GetCategoryFromString(Document doc, string categoryName)
        {
            foreach (Category cat in doc.Settings.Categories)
                if (cat.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                    return cat;
            return null;
        }
        public static BuiltInCategory? GetBuiltInCategoryFromString(string categoryName)
        {
            foreach (BuiltInCategory bic in Enum.GetValues(typeof(BuiltInCategory)))
            {
                string bicName = LabelUtils.GetLabelFor(bic);

                if (bicName.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                    return bic;
            }
            return null;
        }
        public static string ReadEmbeddedResource(string resourceName)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new System.IO.StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
        public static Tuple<string, long, string, string, string>[] CategoriesTable;
        public static Tuple<string, long, string, string, string>[] ReadCategoriesTable()
        {
            string tsv = ReadEmbeddedResource("GvcRevitPlugins.Resources.RevitCategoriesList.tsv");
            string[] rows = tsv.Replace("\"", "").Split("\r\n");
            Tuple<string, long, string, string, string>[] categoriesDict = new Tuple<string, long, string, string, string>[rows.Length]; // BuiltInCategory, id, Type, Name (ENU), Name (PTB)

            for (int i = 1; i < rows.Length; i++)
            {
                string[] cells = rows[i].Split("\t");
                long id = long.Parse(cells[0]);
                string builtInCategory = cells[1];
                string type = cells[2];
                string nameENU = cells[3];
                string namePTB = cells[4];
                categoriesDict[i - 1] = new Tuple<string, long, string, string, string>(builtInCategory, id, type, nameENU, namePTB);
            }

            return categoriesDict;
        }
        public static bool SearchCategory(string category, out long builtInCategoryId)
        {
            if (CategoriesTable.Length < 1)
                CategoriesTable = ReadCategoriesTable();

            Tuple<string, long, string, string, string> catFound = CategoriesTable.First(x => x.Item5 == category);

            if (catFound != null)
            {
                builtInCategoryId = catFound.Item2;
                return true;
            }

            builtInCategoryId = 0;
            return false;
        }
    }
    public static class SheetSyncMethods
    {
        public static string FilePathParameterGroupName = "GVC";
        public static string FilePathParameterName = "SheetSync - Caminho da Planilha";
        
        public static string SearchObjectParameterFromObject(Document doc, string lookupParameterName, string queryParameterName, string queryParameterValue)
        {
            Element[] elements = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElements()
                .ToArray();

            Element foundObject = elements.FirstOrDefault(x => ElementQueryPredicate(x, queryParameterName, queryParameterValue));

            var lookupParameter = foundObject.LookupParameter(lookupParameterName);
            string lookupParameterValue = lookupParameter.AsValueString();

            return lookupParameterValue;
        }
        public static bool ElementQueryPredicate(Element element, string queryParameterName, string queryParameterValue)
        {
            Parameter param = element.LookupParameter(queryParameterName);
            if (param is null)
                return false;
            
            string paramValue = param.AsValueString(formatOptions: new FormatOptions());
            return paramValue == queryParameterValue;
        }
        public static Parameter SearchProjectParameter(Document doc, string parameterName)
        {
            ProjectInfo projectInfo = doc.ProjectInformation;
            Parameter existingParam = projectInfo.LookupParameter(parameterName);
            return existingParam;
        }
        public static Parameter GetProjectParameter(UIApplication uiApp, Document doc)
        {
            Application app = uiApp.Application;

            ProjectInfo projectInfo = doc.ProjectInformation;
            Parameter existingParam = projectInfo.LookupParameter(FilePathParameterName);

            if(existingParam != null)
                return existingParam;
            

            else
            {
                TaskDialog.Show("Erro", "O parâmetro \"SheetSync - Caminho da Planilha\" não existe. Por favor criá-lo");
                throw new Exception("Parameter doesn't exist");
                
            }

            return null;

            ForgeTypeId paramType = SpecTypeId.String.Text;
            ForgeTypeId groupTypeId = GroupTypeId.IdentityData;
            ExternalDefinitionCreationOptions options = new ExternalDefinitionCreationOptions(FilePathParameterName, paramType)
            {
                Visible = true
            };

            // Create the new definition
            //Definition def = new ExternalDefinitionCreationOptions(FilePathParameterName, paramType).CreateExternalDefinition();
            return null;

            Definition def = null;

            // Bind to Project Information category
            CategorySet catSet = new CategorySet();
            catSet.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_ProjectInformation));

            Transaction transaction = new Transaction(doc, "SheetSync - Criar parâmetro de projeto");
            transaction.Start();

            InstanceBinding binding = app.Create.NewInstanceBinding(catSet);
            //doc.ParameterBindings.Insert(def, binding, BuiltInParameterGroup.PG_PROJECT_INFORMATION);
            doc.ParameterBindings.Insert(def, binding, groupTypeId);

            transaction.Commit();
            TaskDialog.Show("Success", $"Parameter '{FilePathParameterName}' created.");
            Parameter createdParam = projectInfo.LookupParameter(FilePathParameterName);
            return createdParam;
        }
        public static Result CreateDraftingViews(Document doc, KeyValuePair<string, string>[] viewsInfos)
        {
            Transaction transaction = new Transaction(doc, "SheetSync - Criar Vistas de Desenho");
            transaction.Start();

            try
            {
                for (int i = 0; i < viewsInfos.Length; i++)
                    CreateDrawingView(doc, viewsInfos[i].Key, viewsInfos[i].Value);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Erro", $"Não foi possível criar uma vista de desenho. Mais detalhes:\n{ex}");
                return Result.Failed;
            }


            TaskDialog.Show("Sucesso", "Vistas de Desenho criadas!");
            transaction.Commit();
            return Result.Succeeded;
        }
        public static Result CreateDrawingView(Document doc, string viewName, string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                TaskDialog.Show("Error", "PNG file not found.");
                return Result.Failed;
            }

            ImageType imageType = LoadImageIntoRevit(doc, imagePath);

            if (imageType == null)
            {
                TaskDialog.Show("Error", "Failed to load PNG.");
                return Result.Failed;
            }

            // Create a new Drafting View
            ViewFamilyType draftingViewType = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Drafting);

            if (draftingViewType == null)
            {
                TaskDialog.Show("Error", "No Drafting View Type found.");
                return Result.Failed;
            }

            ViewDrafting draftingView = ViewDrafting.Create(doc, draftingViewType.Id);
            draftingView.Name = viewName;

            // Place the image in the Drafting View
            XYZ imagePosition = new XYZ(0, 0, 0); // Adjust the position as needed
            ImageInstance imageInstance = ImageInstance.Create(doc, draftingView, imageType.Id, new ImagePlacementOptions(imagePosition, BoxPlacement.Center));
            return Result.Succeeded;
        }
        private static ImageType LoadImageIntoRevit(Document doc, string imagePath)
        {
            // Check if the image is already loaded
            ImageType existingImage = new FilteredElementCollector(doc)
                .OfClass(typeof(ImageType))
                .Cast<ImageType>()
                .FirstOrDefault(img => img.Path == imagePath);

            if (existingImage != null)
                return existingImage;

            // Load new ImageType
            ImageTypeOptions options = new ImageTypeOptions(imagePath, false, ImageTypeSource.Import);
            return ImageType.Create(doc, options);
        }
        public enum QueryElementType
        {
            Project,
            Object
        }
    }
}
