using Autodesk.Revit.UI;
using GvcRevitPlugins.Shared.Commands;
using Revit.Async.ExternalEvents;

namespace GvcRevitPlugins.Shared.App
{
    internal class GvcExternalEventHandler : SyncGenericExternalEventHandler<IGvcCommand, bool>
    {
        public override string GetName() => "GvcExternalEventHandler";

        protected override bool Handle(UIApplication app, IGvcCommand method)
        {
            method.MakeAction(app);
            return true;
        }
        public override object Clone()
        {
            throw new System.NotImplementedException();
        }
    }
}
