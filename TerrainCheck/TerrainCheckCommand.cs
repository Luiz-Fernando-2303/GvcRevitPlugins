using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Linq;

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
            try
            {
                if (uiApp == null)
                {
                    TaskDialog.Show("Erro", "Aplicação inválida.");
                    return;
                }

                UIDocument uiDoc = uiApp.ActiveUIDocument;
                Document doc = uiDoc?.Document;
                if (doc == null)
                {
                    TaskDialog.Show("Erro", "Nenhum documento ativo encontrado.");
                    return;
                }

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

                if (store.selection == null || store.selection.Lines == null || !store.selection.Lines.Any())
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

                try
                {
                    ProjectFaces projectFaces = new ProjectFaces(
                        uiDoc,
                        store.IntersectionElementId,
                        store.selection.Lines,
                        store.selection.LineResults,
                        store.SubdivisionLevel, 
                        store.PlatformElevation
                    );

                    TaskDialog.Show("Sucesso", "Verificação de terreno concluída com sucesso.");
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Erro", $"Falha ao processar faces do projeto: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Erro Crítico", $"Ocorreu um erro inesperado: {ex.Message}");
            }
        }
    }
}
