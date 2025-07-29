using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Linq;

// Substituir modelo generico por painel aovelar pre fabricado (cassol)

namespace GvcRevitPlugins.TerrainCheck
{
    public static class TerrainCheckCommand
    {
        internal static void Execute(UIApplication uiApp, bool draw = false)
        {
            TerrainCheckCommand_ wallCommand = new();
            wallCommand.Execute(uiApp);
        }
    }

    public class TerrainCheckCommand_
    {
        public virtual void Execute(UIApplication uiApp)
        {
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            var store = TerrainCheckApp._thisApp?.Store;

            if (store == null)
            {
                TaskDialog.Show("Erro", "Objeto 'Store' não está inicializado.");
                return;
            }

            if (store.IntersectionElementId == null || store.IntersectionElementId == ElementId.InvalidElementId)
            {
                TaskDialog.Show("Erro", "Elemento de interseção não foi definido.");
                return;
            }

            if (store.selection == null || store.selection.Lines == null || store.selection.Lines.Count() == 0)
            {
                TaskDialog.Show("Erro", "Seleção de linhas de contorno está vazia ou não definida.");
                return;
            }

            if (store.SubdivisionLevel <= 0)
            {
                TaskDialog.Show("Erro", "Nível de subdivisão inválido. Deve ser maior que zero.");
                return;
            }

            if (double.IsNaN(store.PlatformElevation) || double.IsInfinity(store.PlatformElevation))
            {
                TaskDialog.Show("Erro", "Elevação da plataforma não foi definida corretamente.");
                return;
            }

            ProjectFaces projectFaces = new ProjectFaces(
                uiDoc,
                store.IntersectionElementId,
                store.selection.Lines,
                store.SubdivisionLevel,
                store.PlatformElevation
            );
        }
    }
}
