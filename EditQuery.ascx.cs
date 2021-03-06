#region Copyright

// 
// Copyright (c) 2015
// by Satrabel
// 

#endregion

#region Using Statements

using System;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Common;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Newtonsoft.Json.Linq;
using Satrabel.OpenContent.Components;
using Satrabel.OpenContent.Components.Alpaca;
using Satrabel.OpenContent.Components.Lucene;
using Satrabel.OpenContent.Components.Lucene.Config;
using Satrabel.OpenContent.Components.Lucene.Index;
using Satrabel.OpenContent.Components.Manifest;

#endregion

namespace Satrabel.OpenContent
{
    public partial class EditQuery : PortalModuleBase
    {
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            hlCancel.NavigateUrl = Globals.NavigateURL();
            cmdSave.NavigateUrl = Globals.NavigateURL();
            //OpenContentSettings settings = this.OpenContentSettings();
            //AlpacaEngine alpaca = new AlpacaEngine(Page, ModuleContext, settings.Template.Uri().FolderPath, "query");
            AlpacaEngine alpaca = new AlpacaEngine(Page, ModuleContext, "", "");
            alpaca.RegisterAll();
            string itemId = null;//Request.QueryString["id"] == null ? -1 : int.Parse(Request.QueryString["id"]);
            AlpacaContext = new AlpacaContext(PortalId, ModuleId, itemId, ScopeWrapper.ClientID, hlCancel.ClientID, cmdSave.ClientID, null, null);
        }
        protected void bIndex_Click(object sender, EventArgs e)
        {
            OpenContentSettings settings = new OpenContentSettings(Settings);
            bool index = false;
            if (settings.TemplateAvailable)
            {
                index = settings.Manifest.Index;
            }
            FieldConfig indexConfig = null;
            if (index)
            {
                indexConfig = OpenContentUtils.GetIndexConfig(settings.Template.Key.TemplateDir);
            }

            int moduleid = ModuleId;
            if (settings.IsOtherModule)
            {
                moduleid = settings.ModuleId;
            }

            LuceneController.Instance.ReIndexModuleData(moduleid, indexConfig);
        }
        protected void bGenerate_Click(object sender, EventArgs e)
        {
            /*
            OpenContentController occ = new OpenContentController();
            var oc = occ.GetFirstContent(ModuleId);
            if (oc != null)
            {
                var data = JObject.Parse(oc.Json);
                for (int i = 0; i < 10000; i++)
                {
                    data["Title"] = "Title " + i;
                    var newoc = new OpenContentInfo()
                    {
                        Title = "check" + i,
                        ModuleId = ModuleId,
                        Html = "tst",
                        Json = data.ToString(),
                        CreatedByUserId = UserId,
                        CreatedOnDate = DateTime.Now,
                        LastModifiedByUserId = UserId,
                        LastModifiedOnDate = DateTime.Now

                    };
                    occ.AddContent(newoc, true, null);
                }
            }
            */
        }

        public AlpacaContext AlpacaContext { get; private set; }
    }
}

