﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using DotNetNuke.Web.Api;
using System.Net.Http.Headers;
using Satrabel.OpenContent.Components.Handlebars;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Portals;
using Newtonsoft.Json.Linq;
using Satrabel.OpenContent.Components.Lucene;
using Satrabel.OpenContent.Components.Lucene.Config;
using Satrabel.OpenContent.Components.Datasource;

namespace Satrabel.OpenContent.Components.Rss
{
    public class RssApiController : DnnApiController
    {
        [AllowAnonymous]
        [HttpGet]
        public HttpResponseMessage GetFeed(int moduleId, int tabId)
        {
            return GetFeed(moduleId, tabId, "rss", "application/rss+xml");
        }
        [AllowAnonymous]
        [HttpGet]
        public HttpResponseMessage GetFeed(int moduleId, int tabId, string template, string mediaType)
        {
            ModuleController mc = new ModuleController();
            List<IDataItem> dataList = new List<IDataItem>(); ;
            var module = mc.GetModule(moduleId, tabId, false);
            OpenContentSettings settings = module.OpenContentSettings();
            var rssTemplate = new FileUri(settings.TemplateDir, template + ".hbs");
            string source = File.ReadAllText(rssTemplate.PhysicalFilePath);
            var ds = DataSourceManager.GetDataSource("OpenContent");
            var dsContext = new DataSourceContext()
            {
                ModuleId = moduleId,
                TemplateFolder = settings.TemplateDir.FolderPath
            };
            bool useLucene = settings.Template.Manifest.Index;
            if (useLucene)
            {
                var indexConfig = OpenContentUtils.GetIndexConfig(settings.Template.Key.TemplateDir);
                var queryDef = new QueryDefinition(indexConfig);
                queryDef.BuildFilter(true);
                queryDef.BuildSort("");
                SearchResults docs = LuceneController.Instance.Search(moduleId.ToString(), queryDef);
                if (docs != null)
                {
                    int total = docs.TotalResults;
                    foreach (var item in docs.ids)
                    {
                        var dsItem = ds.Get(dsContext, item);
                        //var content = ctrl.GetContent(int.Parse(item));
                        if (dsItem != null)
                        {
                            dataList.Add(dsItem);
                        }
                    }
                }
            }

            ModelFactory mf = new ModelFactory(dataList, null, settings.TemplateDir.PhysicalFullDirectory, null, null, null, module, PortalSettings, tabId, moduleId);
            dynamic model = mf.GetModelAsDynamic();
            HandlebarsEngine hbEngine = new HandlebarsEngine();
            string res = hbEngine.Execute(source, model);
            var response = new HttpResponseMessage();
            response.Content = new StringContent(res);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            return response;

        }
    }
}