using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
//using SheetsSync.Utils;

namespace GvcRevitPlugins.SimpleDebugs.Tests
{
    //internal static class PlaceImagesTest
    //{
    //    public static void PlaceImages(UIApplication uiApp, Document doc)
    //    {
    //        var images = new KeyValuePair<string, string>[5]
    //        {
    //            new KeyValuePair<string, string>("Observações", @"D:\Work\GVC\Emccamp\Transformação Digital\output\Observações.png"),
    //            new KeyValuePair<string, string>("Revisões", @"D:\Work\GVC\Emccamp\Transformação Digital\output\Revisões.png"),
    //            new KeyValuePair<string, string>("Zoneamento Subcategoria De Uso", @"D:\Work\GVC\Emccamp\Transformação Digital\output\Zoneamento Subcategoria De Uso.png"),
    //            new KeyValuePair<string, string>("Quadros de Áreas Construídas", @"D:\Work\GVC\Emccamp\Transformação Digital\output\Quadros de Áreas Construídas.png"),
    //            new KeyValuePair<string, string>("Tipologias Torres Quadro De Areas", @"D:\Work\GVC\Emccamp\Transformação Digital\output\Tipologias Torres Quadro De Areas.png"),
    //        };
    //        SheetSyncMethods.CreateDraftingViews(doc, images);
    //    }
    //    public static void CreateProjectParameter(UIApplication uiApp, Document doc)
    //    {
    //        var projectParameter = SheetSyncMethods.GetProjectParameter(uiApp, doc);
    //        var value = projectParameter.AsString();
    //    }
    //    public static void QueryParameters(UIApplication uiApp, Document doc)
    //    {
    //        SheetSyncMethods.QueryElementType queryElementType = SheetSyncMethods.QueryElementType.Object;
    //        string queryParameterName = "NOME BLOCO";
    //        string queryValue = "BLOCO 01";
    //        string lookupParameterName = "ALTURA EDIFICAÇÃO";

    //        string lookupProjectParameter = "QUANTIDADE DE TORRES";

    //        var projectParam = SheetSyncMethods.SearchProjectParameter(doc, lookupProjectParameter).AsString();
    //        var objectParam = SheetSyncMethods.SearchObjectParameterFromObject(doc, lookupParameterName, queryParameterName, queryValue);
    //    }
    //    public static void TestExcelUtils(string spreadsheetPath)
    //    {
    //        var tst = ExcelUtils.GetParametersList(spreadsheetPath, ExcelUtils.defaultConfig);
    //        return;
    //    }
    //    public static void FullPipeline(UIApplication uiApp, Document doc)
    //    {
    //        // 0. Config
    //        string spreadsheetPath = @"D:\Work\GVC\Emccamp\Transformação Digital\Planilhas\SP.SAN.018-SAN-AQA-EMA-01_02-INCPLA-R05.xlsx";
    //        string outputPath = @"D:\Work\GVC\Emccamp\Transformação Digital\output";

    //        // 1. Get Parameters
    //        ExcelUtils.ParameterLookup[] lookupParameters = ExcelUtils.GetParametersList(spreadsheetPath, ExcelUtils.defaultConfig);
    //        //for (int i = 0; i < lookupParameters.Length; i++)
    //        //{
    //        //    try
    //        //    {

    //        //    ExcelUtils.ParameterLookup actual = lookupParameters[i];
    //        //    string lookupValue;
    //        //    if (actual.ColumnLookupElementType == "Projeto")
    //        //        lookupValue = SheetSyncMethods.SearchProjectParameter(doc, actual.ColumnParameterName).AsString();
    //        //    else
    //        //        lookupValue = SheetSyncMethods.SearchObjectParameterFromObject(doc, actual.ColumnParameterName, actual.ColumnLookupParameterValue, actual.ColumnLookupParameterName);
    //        //    actual.ColumnParameterValue = lookupValue;
    //        //    }
    //        //    catch
    //        //    {

    //        //    }
    //        //}



    //        // 3. Get Tables
    //        ExcelUtils.PrintTableInfo[] printTables = ExcelUtils.GetImagesTables(spreadsheetPath, outputPath, ExcelUtils.defaultConfig);
    //        ExcelUtils.PrintTables(spreadsheetPath, outputPath, printTables);

    //        // 4. Tables to Drawing View
    //        PlaceImages(uiApp, doc);

    //        string row1Param = SheetSyncMethods.SearchProjectParameter(doc, "QUANTIDADE DE TORRES").AsString();
    //        string row2Param = SheetSyncMethods.SearchObjectParameterFromObject(doc, "ALTURA EDIFICAÇÃO", "NOME BLOCO", "BLOCO 01");
    //        string row3Param = SheetSyncMethods.SearchObjectParameterFromObject(doc, "ALTURA EDIFICAÇÃO", "NOME BLOCO", "BLOCO 09");

    //        return;
    //    }
    //}
}
