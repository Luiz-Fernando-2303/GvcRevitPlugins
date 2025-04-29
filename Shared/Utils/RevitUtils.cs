using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace GvcRevitPlugins.Shared.Utils
{
    internal static class RevitUtils
    {
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
        //private static Element[] GetFamilyTypes(Document doc, Element family) => (family as Family).GetFamilySymbolIds().ToList().Select(x => doc.GetElement(x)).ToArray();
        private static Element[] GetFamilyTypes(Document doc, Element family) => (family as Family).GetFamilySymbolIds().Select(x => doc.GetElement(x)).ToArray();
    }
}
