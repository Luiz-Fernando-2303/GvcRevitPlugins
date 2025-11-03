using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using GvcRevitPlugins.Shared.App;
using GvcRevitPlugins.Shared.Commands;
using Revit.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GvcRevitPlugins.TerrainCheck.Commands
{
    public class SetTerrainBoundaryIdCommand : AsyncCommandBase, IGvcCommand
    {
        public override async Task ExecuteAsync(object parameter)
        {
            bool dummy = await RevitTask.RunAsync(async app => { return await RevitTask.RaiseGlobal<GvcExternalEventHandler, IGvcCommand, bool>(this); });
        }

        public void MakeAction(object uiAppObj)
        {
            try
            {
                var uiApp = uiAppObj as UIApplication;
                if (uiApp == null)
                {
                    TaskDialog.Show("Erro", "Aplicação inválida.");
                    return;
                }

                var uiDoc = uiApp.ActiveUIDocument;
                var doc = uiDoc?.Document;
                if (doc == null)
                {
                    TaskDialog.Show("Erro", "Nenhum documento ativo encontrado.");
                    return;
                }

                var selection = uiDoc.Selection;
                List<Reference> pickedRef;

                try
                {
                    pickedRef = selection.PickObjects(ObjectType.Element, "Selecione os objetos de divisa").ToList();
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    TaskDialog.Show("Cancelado", "Seleção cancelada pelo usuário.");
                    return;
                }

                if (pickedRef == null || pickedRef.Count == 0)
                {
                    TaskDialog.Show("Aviso", "Nenhum objeto foi selecionado.");
                    return;
                }

                List<Element> elements = pickedRef
                    .Select(r => doc.GetElement(r.ElementId))
                    .Where(e => e != null)
                    .ToList();

                if (elements.Count == 0)
                {
                    TaskDialog.Show("Aviso", "Nenhum elemento válido foi encontrado na seleção.");
                    return;
                }

                string selectionType = TerrainCheckApp._thisApp.Store.BoundarySelectionType;
                switch (selectionType)
                {
                    case "Linha de Divisa":
                        elements = elements
                            .Where(e => e.Category?.BuiltInCategory == BuiltInCategory.OST_SitePropertyLineSegment)
                            .ToList();
                        break;

                    case "Parede":
                        elements = elements
                            .Where(e => e.Category?.BuiltInCategory == BuiltInCategory.OST_Walls)
                            .ToList();
                        break;

                    case "Guarda Corpo":
                        elements = elements
                            .Where(e => e.Category?.BuiltInCategory == BuiltInCategory.OST_StairsRailing)
                            .ToList();
                        break;

                    case "Arrimo":
                        elements = elements
                            .Where(e =>
                            {
                                try
                                {
                                    var type = doc.GetElement(e.GetTypeId()) as ElementType;
                                    if (type == null) return false;

                                    var heightParam = type.LookupParameter("Altura Arrimo");
                                    return heightParam != null && heightParam.HasValue;
                                }
                                catch
                                {
                                    return false;
                                }
                            })
                            .ToList();
                        break;

                    default:
                        TaskDialog.Show("Erro", $"Tipo de elemento não reconhecido: {selectionType}");
                        return;
                }

                if (elements.Count == 0)
                {
                    TaskDialog.Show("Erro", $"Nenhum objeto do tipo \"{selectionType}\" foi encontrado.");
                    return;
                }

                TerrainCheckApp._thisApp.Store.TerrainBoundaryIds = elements.Select(e => e.Id).ToList();
                TerrainCheckApp._thisApp.Store.selection = new SelectionToLines(
                    TerrainCheckApp._thisApp.Store.TerrainBoundaryIds,
                    doc
                );

                TaskDialog.Show("Sucesso", $"{elements.Count} objeto(s) do tipo \"{selectionType}\" foram selecionados.");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Erro Crítico", $"Ocorreu um erro inesperado: {ex.Message}");
            }
        }
    }
}
