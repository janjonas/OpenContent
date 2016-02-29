#region Copyright

// 
// Copyright (c) 2015
// by Satrabel
// 

#endregion

#region Using Statements

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using DotNetNuke.Web.Api;
using Newtonsoft.Json.Linq;
using DotNetNuke.Instrumentation;
using DotNetNuke.Security;
using Satrabel.OpenContent.Components.Json;
using DotNetNuke.Entities.Modules;
using System.Collections.Generic;
using Satrabel.OpenContent.Components.Infrastructure;
using Satrabel.OpenContent.Components.Alpaca;
using Satrabel.OpenContent.Components.Lucene;
using Satrabel.OpenContent.Components.Manifest;

#endregion

namespace Satrabel.OpenContent.Components
{
    [SupportedModules("OpenContent")]
    public class OpenContentAPIController : DnnApiController
    {
        public string BaseDir
        {
            get
            {
                return PortalSettings.HomeDirectory + "/OpenContent/Templates/";
            }
        }
        [ValidateAntiForgeryToken]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        [HttpGet]
        public HttpResponseMessage Edit()
        {
            return Edit(-1);
        }
        [ValidateAntiForgeryToken]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        [HttpGet]
        public HttpResponseMessage Edit(int id)
        {
            OpenContentSettings settings = ActiveModule.OpenContentSettings();
            ModuleInfo module = ActiveModule;
            if (settings.ModuleId > 0)
            {
                ModuleController mc = new ModuleController();
                module = mc.GetModule(settings.ModuleId, settings.TabId, false);
            }
            var manifest = settings.Manifest;
            TemplateManifest templateManifest = settings.Template;

            string editRole = manifest == null ? "" : manifest.EditRole;
            bool listMode = templateManifest != null && templateManifest.IsListTemplate;
            try
            {
                var fb = new FormBuilder(settings.TemplateDir);
                JObject json = fb.BuildForm();
                int createdByUserid = -1;
                var content = GetContent(module.ModuleID, listMode, id);
                if (content != null)
                {
                    json["data"] = content.Json.ToJObject("GetContent " + id);
                    if (json["schema"]["properties"]["ModuleTitle"] is JObject)
                    {
                        //json["data"]["ModuleTitle"] = ActiveModule.ModuleTitle;
                        if (json["data"]["ModuleTitle"] != null && json["data"]["ModuleTitle"].Type == JTokenType.String)
                        {
                            json["data"]["ModuleTitle"] = ActiveModule.ModuleTitle;
                        }
                        else if (json["data"]["ModuleTitle"] != null && json["data"]["ModuleTitle"].Type == JTokenType.Object)
                        {
                            json["data"]["ModuleTitle"][DnnUtils.GetCurrentCultureCode()] = ActiveModule.ModuleTitle;
                        }
                    }
                    AddVersions(json, content);
                    createdByUserid = content.CreatedByUserId;
                }

                if (!OpenContentUtils.HasEditPermissions(PortalSettings, module, editRole, createdByUserid))
                {
                    return Request.CreateResponse(HttpStatusCode.Unauthorized);
                }

                return Request.CreateResponse(HttpStatusCode.OK, json);
            }
            catch (Exception exc)
            {
                Log.Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }

        [ValidateAntiForgeryToken]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.Edit)]
        [HttpGet]
        public HttpResponseMessage EditData(string key)
        {
            OpenContentSettings settings = ActiveModule.OpenContentSettings();
            ModuleInfo module = ActiveModule;
            if (settings.ModuleId > 0)
            {
                ModuleController mc = new ModuleController();
                module = mc.GetModule(settings.ModuleId, settings.TabId, false);
            }
            var manifest = settings.Manifest;
            TemplateManifest templateManifest = settings.Template;
            var dataManifest = manifest.AdditionalData[key];
            string scope = AdditionalDataUtils.GetScope(dataManifest, PortalSettings, module.ModuleID, ActiveModule.TabModuleID);
            try
            {
                var templateFolder = string.IsNullOrEmpty(dataManifest.TemplateFolder) ? settings.TemplateDir : settings.TemplateDir.ParentFolder.Append(dataManifest.TemplateFolder);
                var fb = new FormBuilder(templateFolder);
                JObject json = fb.BuildForm(key);
                int createdByUserid = -1;
                var dc = new AdditionalDataController();
                var data = dc.GetData(scope, dataManifest.StorageKey ?? key);
                if (data != null)
                {
                    json["data"] = data.Json.ToJObject("GetContent " + scope + "/" + key);
                    AddVersions(json, data);
                    createdByUserid = data.CreatedByUserId;
                }
                return Request.CreateResponse(HttpStatusCode.OK, json);
            }
            catch (Exception exc)
            {
                Log.Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.Edit)]
        [ValidateAntiForgeryToken]
        [HttpPost]
        public HttpResponseMessage UpdateData(JObject json)
        {
            try
            {
                OpenContentSettings settings = ActiveModule.OpenContentSettings();
                ModuleInfo module = ActiveModule;
                if (settings.ModuleId > 0)
                {
                    ModuleController mc = new ModuleController();
                    module = mc.GetModule(settings.ModuleId, settings.TabId, false);
                }
                var manifest = settings.Template.Manifest;
                TemplateManifest templateManifest = settings.Template;
                string key = json["key"].ToString();
                var dataManifest = manifest.AdditionalData[key];
                string scope = AdditionalDataUtils.GetScope(dataManifest, PortalSettings, module.ModuleID, ActiveModule.TabModuleID);
                AdditionalDataController ctrl = new AdditionalDataController();
                AdditionalDataInfo data = ctrl.GetData(scope, dataManifest.StorageKey ?? key);
                if (data == null)
                {
                    data = new AdditionalDataInfo()
                    {
                        Scope = scope,
                        DataKey = dataManifest.StorageKey ?? key,
                        Json = json["form"].ToString(),
                        CreatedByUserId = UserInfo.UserID,
                        CreatedOnDate = DateTime.Now,
                        LastModifiedByUserId = UserInfo.UserID,
                        LastModifiedOnDate = DateTime.Now,
                    };
                    ctrl.AddData(data);
                }
                else
                {
                    data.Json = json["form"].ToString();
                    data.LastModifiedByUserId = UserInfo.UserID;
                    data.LastModifiedOnDate = DateTime.Now;
                    ctrl.UpdateData(data);
                }
                return Request.CreateResponse(HttpStatusCode.OK, "");
            }
            catch (Exception exc)
            {
                Log.Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }

        private OpenContentInfo GetContent(int moduleId, bool listMode, int id)
        {
            IDatasource ctrl = Factories.GetDatasource();
            if (listMode)
            {
                if (id > 0)
                {
                    return ctrl.GetContent(id);
                }
            }
            else
            {
                return ctrl.GetFirstContent(moduleId);

            }
            return null;
        }

        private static void AddVersions(JObject json, OpenContentInfo struc)
        {
            if (!string.IsNullOrEmpty(struc.VersionsJson))
            {
                var verLst = new JArray();
                foreach (var item in struc.Versions)
                {
                    var ver = new JObject();
                    ver["text"] = item.CreatedOnDate.ToShortDateString() + " " + item.CreatedOnDate.ToShortTimeString();
                    if (verLst.Count == 0) // first
                    {
                        ver["text"] = ver["text"] + " ( current )";
                    }
                    ver["ticks"] = item.CreatedOnDate.Ticks.ToString();
                    verLst.Add(ver);
                }
                json["versions"] = verLst;

                //json["versions"] = JArray.Parse(struc.VersionsJson);
            }
        }

        private static void AddVersions(JObject json, AdditionalDataInfo data)
        {
            if (!string.IsNullOrEmpty(data.VersionsJson))
            {
                var verLst = new JArray();
                foreach (var item in data.Versions)
                {
                    var ver = new JObject();
                    ver["text"] = item.CreatedOnDate.ToShortDateString() + " " + item.CreatedOnDate.ToShortTimeString();
                    if (verLst.Count == 0) // first
                    {
                        ver["text"] = ver["text"] + " ( current )";
                    }
                    ver["ticks"] = item.CreatedOnDate.Ticks.ToString();
                    verLst.Add(ver);
                }
                json["versions"] = verLst;

                //json["versions"] = JArray.Parse(struc.VersionsJson);
            }
        }
        [ValidateAntiForgeryToken]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        [HttpGet]
        public HttpResponseMessage Version(int id, string ticks)
        {
            //FileUri template = OpenContentUtils.GetTemplate(ActiveModule.ModuleSettings);
            OpenContentSettings settings = ActiveModule.OpenContentSettings();
            ModuleInfo module = ActiveModule;
            if (settings.ModuleId > 0)
            {
                ModuleController mc = new ModuleController();
                module = mc.GetModule(settings.ModuleId, settings.TabId, false);
            }
            var manifest = settings.Template.Manifest;
            var templateManifest = settings.Template;
            string editRole = manifest == null ? "" : manifest.EditRole;
            bool listMode = templateManifest != null && templateManifest.IsListTemplate;
            JToken json = new JObject();
            try
            {
                int CreatedByUserid = -1;
                var content = GetContent(module.ModuleID, listMode, id);
                if (content != null)
                {
                    if (!string.IsNullOrEmpty(content.VersionsJson))
                    {
                        var ver = content.Versions.Single(v => v.CreatedOnDate.Ticks.ToString() == ticks);
                        json = ver.Json;

                    }
                    CreatedByUserid = content.CreatedByUserId;
                }
                if (!OpenContentUtils.HasEditPermissions(PortalSettings, module, editRole, CreatedByUserid))
                {
                    return Request.CreateResponse(HttpStatusCode.Unauthorized);
                }
                return Request.CreateResponse(HttpStatusCode.OK, json);
            }
            catch (Exception exc)
            {
                Log.Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }

        [ValidateAntiForgeryToken]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.Edit)]
        [HttpGet]
        public HttpResponseMessage Settings()
        {
            string data = (string)ActiveModule.ModuleSettings["data"];
            string Template = (string)ActiveModule.ModuleSettings["template"];
            try
            {
                var templateUri = new FileUri(Template);
                string key = templateUri.FileNameWithoutExtension;
                var fb = new FormBuilder(templateUri);
                JObject json = fb.BuildForm(key);

                var dataJson = data.ToJObject("Raw settings json");
                if (dataJson != null)
                    json["data"] = dataJson;

                return Request.CreateResponse(HttpStatusCode.OK, json);
            }
            catch (Exception exc)
            {
                Log.Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        [ValidateAntiForgeryToken]
        [HttpPost]
        public HttpResponseMessage Update(JObject json)
        {
            try
            {
                bool index = false;
                OpenContentSettings settings = ActiveModule.OpenContentSettings();
                ModuleInfo module = ActiveModule;
                if (settings.ModuleId > 0)
                {
                    ModuleController mc = new ModuleController();
                    module = mc.GetModule(settings.ModuleId, settings.TabId, false);
                }
                var manifest = settings.Template.Manifest;
                TemplateManifest templateManifest = settings.Template;
                index = settings.Template.Manifest.Index;
                string editRole = manifest == null ? "" : manifest.EditRole;

                bool listMode = templateManifest != null && templateManifest.IsListTemplate;
                int createdByUserid = -1;
                IDatasource ctrl = Factories.GetDatasource();
                OpenContentInfo content = null;
                if (listMode)
                {
                    int itemId;
                    if (json["id"] != null && int.TryParse(json["id"].ToString(), out itemId))
                    {
                        content = ctrl.GetContent(itemId);
                        if (content != null)
                            createdByUserid = content.CreatedByUserId;
                    }
                }
                else
                {
                    content = ctrl.GetFirstContent(module.ModuleID);
                    if (content != null)
                        createdByUserid = content.CreatedByUserId;
                }

                if (!OpenContentUtils.HasEditPermissions(PortalSettings, module, editRole, createdByUserid))
                {
                    return Request.CreateResponse(HttpStatusCode.Unauthorized);
                }
                var indexConfig = OpenContentUtils.GetIndexConfig(settings.Template.Key.TemplateDir);
                if (content == null)
                {
                    content = new OpenContentInfo()
                    {
                        ModuleId = module.ModuleID,
                        Title = json["form"]["Title"] == null ? ActiveModule.ModuleTitle : json["form"]["Title"].ToString(),
                        Json = json["form"].ToString(),
                        CreatedByUserId = UserInfo.UserID,
                        CreatedOnDate = DateTime.Now,
                        LastModifiedByUserId = UserInfo.UserID,
                        LastModifiedOnDate = DateTime.Now,
                        Html = "",
                    };
                    ctrl.AddContent(content, index, indexConfig);
                }
                else
                {
                    content.Title = json["form"]["Title"] == null ? ActiveModule.ModuleTitle : json["form"]["Title"].ToString();
                    content.Json = json["form"].ToString();
                    content.LastModifiedByUserId = UserInfo.UserID;
                    content.LastModifiedOnDate = DateTime.Now;
                    ctrl.UpdateContent(content, index, indexConfig);
                }
                if (json["form"]["ModuleTitle"] != null && json["form"]["ModuleTitle"].Type == JTokenType.String)
                {
                    string ModuleTitle = json["form"]["ModuleTitle"].ToString();
                    OpenContentUtils.UpdateModuleTitle(ActiveModule, ModuleTitle);
                }
                else if (json["form"]["ModuleTitle"] != null && json["form"]["ModuleTitle"].Type == JTokenType.Object)
                {
                    string ModuleTitle = json["form"]["ModuleTitle"][DnnUtils.GetCurrentCultureCode()].ToString();
                    OpenContentUtils.UpdateModuleTitle(ActiveModule, ModuleTitle);
                }
                return Request.CreateResponse(HttpStatusCode.OK, "");
            }
            catch (Exception exc)
            {
                Log.Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }

        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        [ValidateAntiForgeryToken]
        [HttpPost]
        public HttpResponseMessage Delete(JObject json)
        {
            try
            {
                bool Index = false;
                OpenContentSettings settings = ActiveModule.OpenContentSettings();
                ModuleInfo module = ActiveModule;
                if (settings.ModuleId > 0)
                {
                    ModuleController mc = new ModuleController();
                    module = mc.GetModule(settings.ModuleId, settings.TabId, false);
                }
                var manifest = settings.Template.Manifest;
                TemplateManifest templateManifest = settings.Template;
                Index = manifest.Index;
                string editRole = manifest == null ? "" : manifest.EditRole;
                bool listMode = templateManifest != null && templateManifest.IsListTemplate;
                int CreatedByUserid = -1;
                IDatasource ctrl = Factories.GetDatasource();
                OpenContentInfo content = null;
                if (listMode)
                {
                    int ItemId;
                    if (int.TryParse(json["id"].ToString(), out ItemId))
                    {
                        content = ctrl.GetContent(ItemId);
                        if (content != null)
                        {
                            CreatedByUserid = content.CreatedByUserId;
                        }
                    }
                }
                else
                {
                    content = ctrl.GetFirstContent(module.ModuleID);
                    if (content != null)
                    {
                        CreatedByUserid = content.CreatedByUserId;
                    }
                }
                if (!OpenContentUtils.HasEditPermissions(PortalSettings, module, editRole, CreatedByUserid))
                {
                    return Request.CreateResponse(HttpStatusCode.Unauthorized);
                }
                if (content != null)
                {
                    ctrl.DeleteContent(content, Index);
                }
                return Request.CreateResponse(HttpStatusCode.OK, "");
            }
            catch (Exception exc)
            {
                Log.Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }

        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        [ValidateAntiForgeryToken]
        [HttpPost]
        public HttpResponseMessage UpdateSettings(JObject json)
        {
            try
            {
                var mc = new ModuleController();
                int moduleId = ActiveModule.ModuleID;
                if (json["data"] != null)
                {
                    var data = json["data"].ToString();
                    //string template = (string)ActiveModule.ModuleSettings["template"];
                    //if (!string.IsNullOrEmpty(template)) mc.UpdateModuleSetting(moduleId, "template", template);
                    if (!string.IsNullOrEmpty(data)) mc.UpdateModuleSetting(moduleId, "data", data);
                }
                else if (json["form"] != null)
                {
                    var form = json["form"].ToString();
                    var key = json["key"].ToString();
                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(form)) mc.UpdateModuleSetting(moduleId, key, form);
                }
                return Request.CreateResponse(HttpStatusCode.OK, "");
            }
            catch (Exception exc)
            {
                Log.Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }

        [ValidateAntiForgeryToken]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.Edit)]
        [HttpPost]
        public HttpResponseMessage LookupData(LookupDataRequestDTO req)
        {
            OpenContentSettings settings = ActiveModule.OpenContentSettings();
            ModuleInfo module = ActiveModule;
            if (settings.ModuleId > 0)
            {
                ModuleController mc = new ModuleController();
                module = mc.GetModule(settings.ModuleId, settings.TabId, false);
            }
            var manifest = settings.Template.Manifest;
            TemplateManifest templateManifest = settings.Template;
            string key = req.dataKey;
            var dataManifest = manifest.AdditionalData[key];
            string scope = AdditionalDataUtils.GetScope(dataManifest, PortalSettings, module.ModuleID, ActiveModule.TabModuleID);
            List<LookupResultDTO> res = new List<LookupResultDTO>();
            try
            {
                AdditionalDataController ctrl = new AdditionalDataController();
                AdditionalDataInfo data = ctrl.GetData(scope, dataManifest.StorageKey ?? key);
                if (data != null)
                {

                    JToken json = data.Json.ToJObject("Get data of  " + req.dataKey);
                    if (!string.IsNullOrEmpty(req.dataMember))
                    {
                        json = json[req.dataMember];
                    }
                    if (json is JArray)
                    {
                        foreach (JToken item in (JArray)json)
                        {
                            res.Add(new LookupResultDTO()
                            {
                                value = item[req.valueField] == null ? "" : item[req.valueField].ToString(),
                                text = item[req.textField] == null ? "" : item[req.textField].ToString()
                            });
                        }
                    }
                    /*
                    else if (json is JObject)
                    {
                        foreach (var item in json.Children<JProperty>())
                        {
                            res.Add(new LookupResultDTO()
                            {
                                value = dataManifest.ModelKey ?? key +"/"+item.Name,
                                text = item.Value[req.textField] == null ? "" : item.Value[req.textField].ToString()
                            });
                        }
                    }
                     */
                }
                return Request.CreateResponse(HttpStatusCode.OK, res);
            }
            catch (Exception exc)
            {
                Log.Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }

        [ValidateAntiForgeryToken]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.Edit)]
        [HttpPost]
        public HttpResponseMessage Lookup(LookupRequestDTO req)
        {
            ModuleController mc = new ModuleController();
            var module = mc.GetModule(req.moduleid, req.tabid, false);
            var settings = module.OpenContentSettings();
            Manifest.Manifest manifest = settings.Manifest;
            TemplateManifest templateManifest = settings.Template;
            bool listMode = templateManifest != null && templateManifest.IsListTemplate;
            //JToken json = new JObject();
            List<LookupResultDTO> res = new List<LookupResultDTO>();
            try
            {
                IDatasource ctrl = Factories.GetDatasource();
                if (listMode)
                {
                    var items = ctrl.GetContents(req.moduleid);
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            res.Add(new LookupResultDTO()
                            {
                                value = item.ContentId.ToString(),
                                text = item.Title
                            });
                        }
                    }
                }
                else
                {
                    var struc = ctrl.GetFirstContent(req.moduleid);
                    if (struc != null)
                    {

                        JToken json = struc.Json.ToJObject("GetFirstContent data of moduleId " + req.moduleid);
                        if (!string.IsNullOrEmpty(req.dataMember))
                        {
                            json = json[req.dataMember];
                            if (json is JArray)
                            {
                                foreach (JToken item in (JArray)json)
                                {
                                    res.Add(new LookupResultDTO()
                                    {
                                        value = item[req.valueField] == null ? "" : item[req.valueField].ToString(),
                                        text = item[req.textField] == null ? "" : item[req.textField].ToString()
                                    });
                                }
                            }
                        }
                    }
                }
                return Request.CreateResponse(HttpStatusCode.OK, res);
            }
            catch (Exception exc)
            {
                Log.Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }
        [ValidateAntiForgeryToken]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        [HttpPost]
        public HttpResponseMessage List(ListDTO req)
        {
            OpenContentSettings settings = ActiveModule.OpenContentSettings();
            ModuleInfo module = ActiveModule;
            if (settings.ModuleId > 0)
            {
                ModuleController mc = new ModuleController();
                module = mc.GetModule(settings.ModuleId, settings.TabId, false);
            }
            var manifest = settings.Template.Manifest;
            TemplateManifest templateManifest = settings.Template;
            string editRole = manifest == null ? "" : manifest.EditRole;
            bool listMode = templateManifest != null && templateManifest.IsListTemplate;
            JArray json = new JArray();
            try
            {
                if (listMode)
                {
                    var indexConfig = OpenContentUtils.GetIndexConfig(settings.Template.Key.TemplateDir);
                    var docs = LuceneController.Instance.Search(module.ModuleID.ToString(), "Title", req.query, "", "", 10, 0, indexConfig);
                    foreach (var item in docs.ids)
                    {
                        var content = GetContent(module.ModuleID, listMode, int.Parse(item));
                        if (content != null)
                        {
                            json.Add(content.Json.ToJObject("GetContent " + item));
                        }
                    }
                }
                else
                {
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "not supported because not in multi items template ");
                }
                return Request.CreateResponse(HttpStatusCode.OK, json);
            }
            catch (Exception exc)
            {
                Log.Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }

        [ValidateAntiForgeryToken]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.Edit)]
        [HttpGet]
        public HttpResponseMessage EditSettings(string key)
        {
            string data = (string)ActiveModule.ModuleSettings[key];
            try
            {
                OpenContentSettings settings = ActiveModule.OpenContentSettings();
                var fb = new FormBuilder(settings.TemplateDir);
                JObject json = fb.BuildForm(key);
                var dataJson = data.ToJObject("Raw settings json");
                if (dataJson != null)
                    json["data"] = dataJson;

                return Request.CreateResponse(HttpStatusCode.OK, json);
            }
            catch (Exception exc)
            {
                Log.Logger.Error(exc);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
        }
    }

    public class LookupRequestDTO
    {
        public int moduleid { get; set; }
        public int tabid { get; set; }
        public string dataMember { get; set; }
        public string valueField { get; set; }
        public string textField { get; set; }
    }
    public class LookupDataRequestDTO
    {
        public string dataKey { get; set; }
        public string dataMember { get; set; }
        public string valueField { get; set; }
        public string textField { get; set; }
    }

    public class LookupResultDTO
    {
        public string value { get; set; }
        public string text { get; set; }
    }

    public class ListDTO
    {
        public string query { get; set; }
    }
}

