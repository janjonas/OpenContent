#region Copyright

// 
// Copyright (c) 2015
// by Satrabel
// 

#endregion

#region Using Statements

using System;
using System.Linq;
using System.Collections.Generic;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Modules.Actions;
using DotNetNuke.Services.Localization;
using DotNetNuke.Security;
using DotNetNuke.Web.Razor;
using System.IO;
using DotNetNuke.Services.Exceptions;
using System.Web.UI;
using System.Web.Hosting;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using System.Dynamic;
using Newtonsoft.Json;
using DotNetNuke.Framework.JavaScriptLibraries;
using DotNetNuke.Web.Client.ClientResourceManagement;
using DotNetNuke.Web.Client;
using Satrabel.OpenContent.Components;
using Satrabel.OpenContent.Components.Json;
using System.Web.WebPages;
using System.Web;
using Satrabel.OpenContent.Components.Handlebars;
using DotNetNuke.Framework;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Common;
using DotNetNuke.UI;
using DotNetNuke.Entities.Host;
using DotNetNuke.Entities.Controllers;
using Satrabel.OpenContent.Components.Rss;
using System.Web.UI.WebControls;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Tabs;
using Satrabel.OpenContent.Components.Dynamic;
using DotNetNuke.Security.Permissions;
using DotNetNuke.Security.Roles;
using DotNetNuke.Entities.Users;
using DotNetNuke.Instrumentation;
using DotNetNuke.Services.Installer.Log;
using Satrabel.OpenContent.Components.Lucene;
using Satrabel.OpenContent.Components.Manifest;

#endregion

namespace Satrabel.OpenContent
{
    public partial class View : RazorModuleBase, IActionable
    {
        private static readonly ILog Logger = LoggerSource.Instance.GetLogger(typeof(View));

        private int _itemId = Null.NullInteger;
        private readonly TemplateInfo _info = new TemplateInfo();
        private OpenContentSettings _settings;

        protected override void OnPreRender(EventArgs e)
        {
            //base.OnPreRender(e);
            pHelp.Visible = false;

            _info.Template = _settings.Template;
            _info.ItemId = _itemId;
            if (_settings.TabId > 0 && _settings.ModuleId > 0) // other module
            {
                _info.TabId = _settings.TabId;
                _info.ModuleId = _settings.ModuleId;
                ModuleController mc = new ModuleController();
                _info.Module = mc.GetModule(_info.ModuleId, _info.TabId, false);
            }
            else // this module
            {
                _info.ModuleId = ModuleContext.ModuleId;
                _info.Module = ModuleContext.Configuration;
            }
            InitTemplateInfo();
            if (!_info.DataExist)
            {
                // no data exist and ... -> show initialization
                if (ModuleContext.EditMode)
                {
                    // edit mode
                    if (_settings.Template == null || ModuleContext.IsEditable)
                    {
                        RenderInitForm();
                    }
                    else if (_info.Template != null)
                    {
                        RenderDemoData();
                    }
                }
                else if (_info.Template != null)
                {
                    RenderDemoData();
                }
            }
            if (!string.IsNullOrEmpty(_info.OutputString))
            {
                var lit = new LiteralControl(Server.HtmlDecode(_info.OutputString));
                Controls.Add(lit);
                //bool EditWitoutPostback = HostController.Instance.GetBoolean("EditWitoutPostback", false);
                var mst = OpenContentUtils.GetManifest(_info.Template);
                bool EditWitoutPostback = mst != null && mst.EditWitoutPostback;
                if (ModuleContext.PortalSettings.EnablePopUps && ModuleContext.IsEditable && EditWitoutPostback)
                {
                    AJAX.WrapUpdatePanelControl(lit, true);
                }
                IncludeResourses(_info.Template);
                //if (DemoData) pDemo.Visible = true;
                if (_info.TemplateManifest != null && _info.TemplateManifest.ClientSideData)
                {
                    DotNetNuke.Framework.ServicesFramework.Instance.RequestAjaxScriptSupport();
                    DotNetNuke.Framework.ServicesFramework.Instance.RequestAjaxAntiForgerySupport();
                }
                if (_info.Files != null && _info.Files.PartialTemplates != null)
                {
                    foreach (var item in _info.Files.PartialTemplates.Where(p=> p.Value.ClientSide))
                    {
                        try
                        {
                            var f = new FileUri(_info.Template.Path, item.Value.Template);
                            string s = File.ReadAllText(f.PhysicalFilePath);
                            var litPartial = new LiteralControl(s);
                            Controls.Add(litPartial);
                        }
                        catch (Exception ex)
                        {
                            DotNetNuke.UI.Skins.Skin.AddModuleMessage(this, ex.Message, DotNetNuke.UI.Skins.Controls.ModuleMessage.ModuleMessageType.RedError);
                        }
                        
                    }
                }

            }
        }

        private void RenderDemoData()
        {
            bool demoExist = GetDemoData(_info, _settings);
            if (demoExist)
            {
                TemplateManifest manifest = OpenContentUtils.GetTemplateManifest(_info.Template);
                TemplateFiles files = null;
                if (manifest != null && manifest.Main != null)
                {
                    _info.Template = new FileUri(_info.Template.UrlFolder, manifest.Main.Template);
                    files = manifest.Main;
                }
                _info.OutputString = GenerateOutput(_info.Template, _info.DataJson, _info.SettingsJson, files);
            }
        }

        private void RenderInitForm()
        {
            pHelp.Visible = true;
            if (!Page.IsPostBack)
            {
                rblDataSource.SelectedIndex = (_settings.TabId > 0 && _settings.ModuleId > 0 ? 1 : 0);
                BindOtherModules(_settings.TabId, _settings.ModuleId);
                BindTemplates(_settings.Template, (_info.IsOtherModule ? _info.Template : null));
            }
            if (rblDataSource.SelectedIndex == 1) // other module
            {
                ModuleController mc = new ModuleController();
                var dsModule = mc.GetTabModule(int.Parse(ddlDataSource.SelectedValue));
                var dsSettings = new OpenContentSettings(dsModule.ModuleSettings);
                _info.OtherModuleSettingsJson = dsSettings.Data;
                _info.OtherModuleTemplate = dsSettings.Template;
                _info.TabId = dsModule.TabID;
                _info.ModuleId = dsModule.ModuleID;
            }
            BindButtons(_settings, _info);
            if (rblUseTemplate.SelectedIndex == 0) // existing template
            {
                _info.Template = new FileUri(ddlTemplate.SelectedValue);
                if (rblDataSource.SelectedIndex == 0) // this module
                {
                    RenderDemoData();
                }
                else // other module
                {
                    RenderOtherModuleDemoData();
                }
            }
            else // new template
            {
                _info.Template = new FileUri(ddlTemplate.SelectedValue);
                if (rblFrom.SelectedIndex == 0) // site
                {
                    RenderDemoData();
                }
            }
        }

        private static bool SettingsNeeded(FileUri template)
        {
            var schemaFileUri = new FileUri(template.Path + "schema.json");
            return schemaFileUri.FileExists;
        }

        private void RenderOtherModuleDemoData()
        {

            TemplateManifest TemplateManifest = OpenContentUtils.GetTemplateManifest(_info.Template);
            if (TemplateManifest != null && TemplateManifest.IsListTemplate)
            {
                // Multi items Template
                if (_info.ItemId == Null.NullInteger)
                {
                    // List template
                    if (TemplateManifest.Main != null)
                    {
                        _info.Files = TemplateManifest.Main;
                        // for list templates a main template need to be defined
                        GetDataList(_info, _settings, TemplateManifest.ClientSideData);
                        if (_info.DataExist && !(SettingsNeeded(_info.Template) && _info.SettingsJson == null))
                        {
                            _info.OutputString = GenerateListOutput(_info.Template.UrlFolder, TemplateManifest.Main, _info.DataList, _info.SettingsJson);
                        }
                    }
                }
            }
            else
            {
                if (TemplateManifest != null && TemplateManifest.Main != null)
                {
                    _info.Template = new FileUri(_info.Template.UrlFolder, TemplateManifest.Main.Template);
                }
                bool dsDataExist = GetModuleDemoData(_info, _settings);
                if (dsDataExist)
                {
                    if (_info.OtherModuleTemplate.FilePath == _info.Template.FilePath && !string.IsNullOrEmpty(_info.OtherModuleSettingsJson))
                    {
                        _info.SettingsJson = _info.OtherModuleSettingsJson;
                    }
                    _info.OutputString = GenerateOutput(_info.Template, _info.DataJson, _info.SettingsJson, null);
                }
            }
        }

        private void BindButtons(OpenContentSettings settings, TemplateInfo info)
        {
            bool templateDefined = info.Template != null;
            bool settingsDefined = !string.IsNullOrEmpty(settings.Data);
            bool settingsNeeded = false;
            if (rblUseTemplate.SelectedIndex == 0) // existing template
            {
                string templateFilename = HostingEnvironment.MapPath("~/" + ddlTemplate.SelectedValue);
                string prefix = Path.GetFileNameWithoutExtension(templateFilename) + "-";
                string schemaFilename = Path.GetDirectoryName(templateFilename) + "\\" + prefix + "schema.json";
                settingsNeeded = File.Exists(schemaFilename);
                templateDefined = templateDefined &&
                    (!ddlTemplate.Visible || (settings.Template.FilePath == ddlTemplate.SelectedValue));
                settingsDefined = settingsDefined || !settingsNeeded;
            }
            else // new template
            {
                templateDefined = false;
            }

            bSave.CssClass = "dnnPrimaryAction";
            bSave.Enabled = true;
            hlEditSettings.CssClass = "dnnSecondaryAction";
            hlEditContent.CssClass = "dnnSecondaryAction";
            //if (ModuleContext.PortalSettings.UserInfo.IsSuperUser)
            hlEditSettings.Enabled = false;
            hlEditSettings.Visible = settingsNeeded;

            if (templateDefined && ModuleContext.EditMode && settingsNeeded)
            {
                //hlTempleteExchange.NavigateUrl = ModuleContext.EditUrl("ShareTemplate");
                hlEditSettings.NavigateUrl = ModuleContext.EditUrl("EditSettings");
                //hlTempleteExchange.Visible = true;
                hlEditSettings.Enabled = true;

                bSave.CssClass = "dnnSecondaryAction";
                bSave.Enabled = false;
                hlEditSettings.CssClass = "dnnPrimaryAction";
                hlEditContent.CssClass = "dnnSecondaryAction";

            }
            hlEditContent.Enabled = false;
            hlEditContent2.Enabled = false;
            if (templateDefined && settingsDefined && ModuleContext.EditMode)
            {
                hlEditContent.NavigateUrl = ModuleContext.EditUrl("Edit");
                hlEditContent.Enabled = true;
                hlEditContent2.NavigateUrl = ModuleContext.EditUrl("Edit");
                hlEditContent2.Enabled = true;
                bSave.CssClass = "dnnSecondaryAction";
                bSave.Enabled = false;
                hlEditSettings.CssClass = "dnnSecondaryAction";
                hlEditContent.CssClass = "dnnPrimaryAction";
            }
        }
        private void BindOtherModules(int TabId, int ModuleId)
        {
            ModuleController mc = new ModuleController();
            var modules = mc.GetModules(ModuleContext.PortalId).Cast<ModuleInfo>();
            modules = modules.Where(m => m.ModuleDefinition.DefinitionName == "OpenContent" && m.IsDeleted == false);
            rblDataSource.Items[1].Enabled = modules.Any();
            phDataSource.Visible = rblDataSource.SelectedIndex == 1; // other module
            if (rblDataSource.SelectedIndex == 1) // other module
            {
                rblUseTemplate.SelectedIndex = 0; // existing template
                phFrom.Visible = false;
                phTemplateName.Visible = false;
            }
            rblUseTemplate.Items[1].Enabled = rblDataSource.SelectedIndex == 0; // this module
            ddlDataSource.Items.Clear();
            foreach (var item in modules)
            {
                if (item.TabModuleID != ModuleContext.TabModuleId)
                {
                    var tc = new TabController();
                    var Tab = tc.GetTab(item.TabID, ModuleContext.PortalId, false);
                    var li = new ListItem(Tab.TabName + " - " + item.ModuleTitle, item.TabModuleID.ToString());
                    ddlDataSource.Items.Add(li);
                    if (item.TabID == TabId && item.ModuleID == ModuleId)
                    {
                        li.Selected = true;
                    }
                }
            }
        }
        private void InitTemplateInfo()
        {
            if (_settings.Template != null)
            {
                // if there is a manifest and Main section exist , use it as template
                _info.Manifest = OpenContentUtils.GetManifest(_settings.Template);
                if (_info.Manifest != null)
                {
                    _info.TemplateManifest = _info.Manifest.GetTemplateManifest(_settings.Template);
                }
                if (_info.TemplateManifest != null && _info.TemplateManifest.Main != null)
                {
                    _info.Template = new FileUri(_settings.Template.UrlFolder, _info.TemplateManifest.Main.Template);
                }

                if (_info.TemplateManifest != null && _info.TemplateManifest.IsListTemplate)
                {
                    // Multi items Template
                    if (_itemId == Null.NullInteger)
                    {
                        // List template
                        if (_info.TemplateManifest.Main != null)
                        {
                            _info.Files = _info.TemplateManifest.Main;
                            // for list templates a main template need to be defined
                            GetDataList(_info, _settings, _info.TemplateManifest.ClientSideData);
                            if (_info.DataExist)
                            {
                                _info.OutputString = GenerateListOutput(_settings.Template.UrlFolder, _info.TemplateManifest.Main, _info.DataList, _info.SettingsJson);
                            }
                        }
                    }
                    else
                    {
                        // detail template
                        if (_info.TemplateManifest.Detail != null)
                        {
                            _info.Files = _info.TemplateManifest.Detail;
                            GetDetailData(_info, _settings);
                            if (_info.DataExist)
                            {
                                _info.OutputString = GenerateOutput(_settings.Template.UrlFolder, _info.TemplateManifest.Detail, _info.DataJson, _info.SettingsJson);
                            }
                        }
                    }
                }
                else
                {
                    TemplateFiles files = null;
                    if (_info.TemplateManifest != null)
                    {
                        files = _info.TemplateManifest.Main;
                        _info.Template = new FileUri(_settings.Template.UrlFolder, files.Template);
                    }
                    // single item template
                    GetData();
                    if (_info.DataExist)
                    {
                        _info.OutputString = GenerateOutput(_info.Template, _info.DataJson, _info.SettingsJson, files);
                    }
                }
            }
        }

        #region Event Handlers
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            var modSettings = ModuleContext.Settings;
            // auto attach module 
            string OpenContent_AutoAttach = PortalController.GetPortalSetting("OpenContent_AutoAttach", ModuleContext.PortalId, "False");
            bool AutoAttach = bool.Parse(OpenContent_AutoAttach);
            if (AutoAttach)
            {
                //var module = ModuleController.Instance.GetModule(ModuleContext.ModuleId, ModuleContext.TabId, false);
                var module = ModuleContext.Configuration;
                var defaultModule = module.DefaultLanguageModule;
                if (defaultModule != null)
                {
                    if (ModuleContext.ModuleId != defaultModule.ModuleID)
                    {
                        ModuleController mc = new ModuleController();
                        
                        mc.DeLocalizeModule(module);

                        mc.ClearCache(defaultModule.TabID);
                        mc.ClearCache(module.TabID);
                        
                        const string ModuleSettingsCacheKey = "ModuleSettings{0}"; // to be compatible with dnn 7.2
                        DataCache.RemoveCache(string.Format(ModuleSettingsCacheKey, defaultModule.TabID));
                        DataCache.RemoveCache(string.Format(ModuleSettingsCacheKey, module.TabID));

                        //DataCache.ClearCache();

                        module = mc.GetModule(defaultModule.ModuleID, ModuleContext.TabId, true);
                        modSettings = module.ModuleSettings;
                    }
                }
            }
            _settings = new OpenContentSettings(modSettings);

        }
        private string GenerateOutput(FileUri template, string dataJson, string settingsJson, TemplateFiles files)
        {
            try
            {
                if (template != null)
                {
                    if (!template.FileExists)
                        Exceptions.ProcessModuleLoadException(this, new Exception(template.FilePath + " don't exist"));

                    string TemplateVirtualFolder = template.UrlFolder;
                    string PhysicalTemplateFolder = Server.MapPath(TemplateVirtualFolder);
                    if (!string.IsNullOrEmpty(dataJson))
                    {

                        ModelFactory mf = new ModelFactory(dataJson, settingsJson, PhysicalTemplateFolder, _info.Manifest, files, ModuleContext.Configuration, ModuleContext.PortalSettings);
                        dynamic model = mf.GetModelAsDynamic();

                        //if (LocaleController.Instance.GetLocales(ModuleContext.PortalId).Count > 1)
                        //{
                        //    dataJson = JsonUtils.SimplifyJson(dataJson, LocaleController.Instance.GetCurrentLocale(ModuleContext.PortalId).Code);
                        //}
                        //dynamic model = JsonUtils.JsonToDynamic(dataJson);

                        //CompleteModel(settingsJson, TemplateFolder, model, files);
                        if (template.Extension != ".hbs")
                        {
                            return ExecuteRazor(template, model);
                        }
                        else
                        {
                            HandlebarsEngine hbEngine = new HandlebarsEngine();
                            return hbEngine.Execute(Page, template, model);
                        }
                    }
                    else
                    {
                        return "";
                    }
                }
                else
                {
                    return "";
                }
            }
            catch (Exception ex)
            {
                Exceptions.ProcessModuleLoadException(this, ex);

            }
            return "";
        }

        private string ExecuteRazor(FileUri template, dynamic model)
        {
            string webConfig = template.PhysicalFullDirectory; // Path.GetDirectoryName(template.PhysicalFilePath);
            webConfig = webConfig.Remove(webConfig.LastIndexOf("\\")) + "\\web.config";
            if (!File.Exists(webConfig))
            {
                string filename = HostingEnvironment.MapPath("~/DesktopModules/OpenContent/Templates/web.config");
                File.Copy(filename, webConfig);
            }
            try
            {
                var razorEngine = new RazorEngine("~/" + template.FilePath, ModuleContext, LocalResourceFile);
                var writer = new StringWriter();
                RazorRender(razorEngine.Webpage, writer, model);
                return writer.ToString();
            }
            catch (Exception ex)
            {
                Exceptions.ProcessModuleLoadException(string.Format("Error while loading template {0}", template.FilePath), this, ex);
                return "";
            }
        }
        private string GenerateListOutput(string TemplateVirtualFolder, TemplateFiles files, IEnumerable<OpenContentInfo> dataList, string settingsJson)
        {
            try
            {
                if (!(string.IsNullOrEmpty(files.Template)))
                {
                    string PhysicalTemplateFolder = Server.MapPath(TemplateVirtualFolder);
                    FileUri Template = CheckFiles(TemplateVirtualFolder, files, PhysicalTemplateFolder);
                    if (dataList != null /*&& dataList.Any()*/)
                    {
                        ModelFactory mf = new ModelFactory(dataList, settingsJson, PhysicalTemplateFolder, _info.Manifest, files, ModuleContext.Configuration, ModuleContext.PortalSettings);
                        dynamic model = mf.GetModelAsDynamic();

                        //string editRole = _info.Manifest == null ? "" : _info.Manifest.EditRole;
                        //dynamic model = new ExpandoObject();
                        //model.Items = new List<dynamic>();
                        //foreach (var item in dataList)
                        //{                            
                        //    string dataJson = item.Json;
                        //    if (LocaleController.Instance.GetLocales(ModuleContext.PortalId).Count > 1)
                        //    {
                        //        dataJson = JsonUtils.SimplifyJson(dataJson, LocaleController.Instance.GetCurrentLocale(ModuleContext.PortalId).Code);
                        //    }
                        //    dynamic dyn = JsonUtils.JsonToDynamic(dataJson);


                        //    dyn.Context = new ExpandoObject();
                        //    dyn.Context.Id = item.ContentId;
                        //    dyn.Context.EditUrl = ModuleContext.EditUrl("id", item.ContentId.ToString());
                        //    dyn.Context.IsEditable = ModuleContext.IsEditable ||
                        //        (!string.IsNullOrEmpty(editRole) &&
                        //        OpenContentUtils.HasEditPermissions(ModuleContext.PortalSettings, _info.Module, editRole, item.CreatedByUserId));
                        //    dyn.Context.DetailUrl = Globals.NavigateURL(ModuleContext.TabId, false, ModuleContext.PortalSettings, "", DnnUtils.GetCurrentCultureCode(), /*OpenContentUtils.CleanupUrl(dyn.Title)*/"", "id=" + item.ContentId.ToString());
                        //    dyn.Context.MainUrl = Globals.NavigateURL(ModuleContext.TabId, false, ModuleContext.PortalSettings, "", DnnUtils.GetCurrentCultureCode(), /*OpenContentUtils.CleanupUrl(dyn.Title)*/"");


                        //    model.Items.Add(dyn);
                        //}
                        //CompleteModel(settingsJson, PhysicalTemplateFolder, model, files);
                        return ExecuteTemplate(TemplateVirtualFolder, files, Template, model);
                    }
                }
            }
            catch (Exception ex)
            {
                Exceptions.ProcessModuleLoadException(this, ex);
            }
            return "";
        }
        /*
        private void CompleteModel(string settingsJson, string PhysicalTemplateFolder, dynamic model, TemplateFiles manifest)
        {
            if (manifest != null && manifest.SchemaInTemplate)
            {
                // schema
                string schemaFilename = PhysicalTemplateFolder + "\\" + "schema.json";
                try
                {
                    dynamic schema = JsonUtils.JsonToDynamic(File.ReadAllText(schemaFilename));
                    model.Schema = schema;
                }
                catch (Exception ex)
                {
                    Exceptions.ProcessModuleLoadException(string.Format("Invalid json-schema. Please verify file {0}.", schemaFilename), this, ex, true);
                }
            }
            if (manifest != null && manifest.OptionsInTemplate)
            {
                // options
                JToken optionsJson = null;
                // default options
                string optionsFilename = PhysicalTemplateFolder + "\\" + "options.json";
                if (File.Exists(optionsFilename))
                {
                    string fileContent = File.ReadAllText(optionsFilename);
                    if (!string.IsNullOrWhiteSpace(fileContent))
                    {
                        optionsJson = fileContent.ToJObject("Options");
                    }
                }
                // language options
                optionsFilename = PhysicalTemplateFolder + "\\" + "options." + DnnUtils.GetCurrentCultureCode() + ".json";
                if (File.Exists(optionsFilename))
                {
                    string fileContent = File.ReadAllText(optionsFilename);
                    if (!string.IsNullOrWhiteSpace(fileContent))
                    {
                        var extraJson = fileContent.ToJObject("Options cultureSpecific");
                        if (optionsJson == null)
                            optionsJson = extraJson;
                        else
                            optionsJson = optionsJson.JsonMerge(extraJson);
                    }
                }
                if (optionsJson != null)
                {
                    dynamic Options = JsonUtils.JsonToDynamic(optionsJson.ToString());
                    model.Options = Options;
                }
            }
            // settings
            if (settingsJson != null)
            {
                model.Settings = JsonUtils.JsonToDynamic(settingsJson);
            }
            string editRole = _info.Manifest == null ? "" : _info.Manifest.EditRole;
            // context
            model.Context = new ExpandoObject();
            model.Context.ModuleId = ModuleContext.ModuleId;
            model.Context.ModuleTitle = ModuleContext.Configuration.ModuleTitle;
            
            model.Context.AddUrl = ModuleContext.EditUrl();
            model.Context.IsEditable = ModuleContext.IsEditable ||
                                      (!string.IsNullOrEmpty(editRole) &&
                                        OpenContentUtils.HasEditPermissions(ModuleContext.PortalSettings, _info.Module, editRole, -1));
            model.Context.PortalId = ModuleContext.PortalId;
            model.Context.MainUrl = Globals.NavigateURL(ModuleContext.TabId, false, ModuleContext.PortalSettings, "", DnnUtils.GetCurrentCultureCode());

        }
         */
        private string GenerateOutput(string TemplateVirtualFolder, TemplateFiles files, string dataJson, string settingsJson)
        {
            try
            {
                if (!(string.IsNullOrEmpty(files.Template)))
                {
                    string PhysicalTemplateFolder = Server.MapPath(TemplateVirtualFolder);
                    FileUri template = CheckFiles(TemplateVirtualFolder, files, PhysicalTemplateFolder);

                    if (!string.IsNullOrEmpty(dataJson))
                    {

                        //if (LocaleController.Instance.GetLocales(ModuleContext.PortalId).Count > 1)
                        //{
                        //    dataJson = JsonUtils.SimplifyJson(dataJson, LocaleController.Instance.GetCurrentLocale(ModuleContext.PortalId).Code);
                        //}
                        //dynamic model = JsonUtils.JsonToDynamic(dataJson);
                        //CompleteModel(settingsJson, PhysicalTemplateFolder, model, files);

                        ModelFactory mf = new ModelFactory(dataJson, settingsJson, PhysicalTemplateFolder, _info.Manifest, files, ModuleContext.Configuration, ModuleContext.PortalSettings);
                        dynamic model = mf.GetModelAsDynamic();

                        Page.Title = model.Title + " | " + ModuleContext.PortalSettings.PortalName;
                        /*
                        var container = Globals.FindControlRecursive(this, "ctr" + ModuleContext.ModuleId);
                        Control ctl = DotNetNuke.Common.Globals.FindControlRecursiveDown(container, "titleLabel");
                        if (ctl != null && ctl is Label)
                        {
                            ((Label)ctl).Text = model.Title;
                        }
                        */

                        return ExecuteTemplate(TemplateVirtualFolder, files, template, model);
                    }
                    else
                    {
                        return "";
                    }
                }
                else
                {
                    return "";
                }
            }
            catch (Exception ex)
            {
                Exceptions.ProcessModuleLoadException(this, ex);
            }
            return "";
        }

        private string ExecuteTemplate(string TemplateVirtualFolder, TemplateFiles files, FileUri template, dynamic model)
        {
            if (template.Extension != ".hbs")
            {
                return ExecuteRazor(template, model);
            }
            else
            {
                HandlebarsEngine hbEngine = new HandlebarsEngine();
                return hbEngine.Execute(Page, this, files, TemplateVirtualFolder, model);
            }
        }
        private FileUri CheckFiles(string templateVirtualFolder, TemplateFiles files, string templateFolder)
        {
            if (files == null)
            {
                Exceptions.ProcessModuleLoadException(this, new Exception("Manifest.json missing or incomplete"));
            }
            string templateFile = templateFolder + "\\" + files.Template;
            string template = templateVirtualFolder + "/" + files.Template;
            if (!File.Exists(templateFile))
                Exceptions.ProcessModuleLoadException(this, new Exception(template + " don't exist"));
            if (files.PartialTemplates != null)
            {
                foreach (var partial in files.PartialTemplates)
                {
                    templateFile = templateFolder + "\\" + partial.Value.Template;
                    string partialTemplate = templateVirtualFolder + "/" + partial.Value.Template;
                    if (!File.Exists(templateFile))
                        Exceptions.ProcessModuleLoadException(this, new Exception(partialTemplate + " don't exist"));
                }
            }
            return new FileUri(template);
        }

        private void GetData()
        {
            _info.DataExist = false;
            _info.DataJson = "";
            _info.SettingsJson = "";
            OpenContentController ctrl = new OpenContentController();
            var struc = ctrl.GetFirstContent(_info.ModuleId);
            if (struc != null)
            {
                _info.DataJson = struc.Json;
                _info.SettingsJson = _settings.Data;
                if (string.IsNullOrEmpty(_info.SettingsJson))
                {
                    string schemaFilename = _info.Template.PhysicalFullDirectory + "\\" + _info.Template.FileNameWithoutExtension + "-schema.json";
                    bool settingsNeeded = File.Exists(schemaFilename);
                    _info.DataExist = !settingsNeeded;
                }
                else
                {
                    _info.DataExist = true;
                }
            }
        }
        private bool GetModuleDemoData(TemplateInfo info, OpenContentSettings settings)
        {
            info.DataJson = "";
            info.SettingsJson = "";
            OpenContentController ctrl = new OpenContentController();
            var struc = ctrl.GetFirstContent(info.ModuleId);
            if (struc != null)
            {
                info.DataJson = struc.Json;
                if (settings.Template != null && info.Template.FilePath == settings.Template.FilePath)
                {
                    info.SettingsJson = settings.Data;
                }
                if (string.IsNullOrEmpty(info.SettingsJson))
                {
                    var settingsFilename = info.Template.PhysicalFullDirectory + "\\" + info.Template.FileNameWithoutExtension + "-data.json";
                    if (File.Exists(settingsFilename))
                    {
                        string fileContent = File.ReadAllText(settingsFilename);
                        if (!string.IsNullOrWhiteSpace(fileContent))
                        {
                            info.SettingsJson = fileContent;
                        }
                    }
                }
                return true;
            }
            return false;
        }
        private void GetDetailData(TemplateInfo info, OpenContentSettings settings)
        {
            info.DataExist = false;
            info.DataJson = "";
            info.SettingsJson = "";
            OpenContentController ctrl = new OpenContentController();
            var struc = ctrl.GetContent(info.ItemId);
            if (struc != null)
            {
                info.DataJson = struc.Json;
                info.SettingsJson = settings.Data;
                info.DataExist = true;
            }

        }
        private void GetDataList(TemplateInfo info, OpenContentSettings settings, bool ClientSide)
        {
            info.DataExist = false;
            info.SettingsJson = "";
            OpenContentController ctrl = new OpenContentController();
            if (ClientSide)
            {
                // check if data is present, but dont return data
                var data = ctrl.GetFirstContent(info.ModuleId);
                if (data != null)
                {
                    info.DataList = new List<OpenContentInfo>();
                    info.SettingsJson = settings.Data;
                    info.DataExist = true;
                }
            }
            else
            {
                bool useLucene = info.Manifest.Index;
                if (useLucene)
                {
                    var indexConfig = OpenContentUtils.GetIndexConfig(settings.Template);
                    string luceneFilter = settings.LuceneFilter;
                    string luceneSort = settings.LuceneSort;
                    int? luceneMaxResults = settings.LuceneMaxResults;
                    SearchResults docs = LuceneController.Instance.Search(_info.ModuleId.ToString(), "Title", "", luceneFilter, luceneSort, (luceneMaxResults.HasValue ? luceneMaxResults.Value : 100), 0, indexConfig);
                    int total = docs.ToalResults;
                    var dataList = new List<OpenContentInfo>();
                    foreach (var item in docs.ids)
                    {
                        var content = ctrl.GetContent(int.Parse(item));
                        if (content != null)
                        {
                            dataList.Add(content);
                        }
                    }
                    info.DataList = dataList;
                }
                else
                {
                    info.DataList = ctrl.GetContents(info.ModuleId);
                }
                if (info.DataList != null && info.DataList.Any())
                {
                    info.SettingsJson = settings.Data;
                    info.DataExist = true;
                }
            }
        }

        private bool Filter(string json, string key, string value)
        {
            bool accept = true;
            JObject obj = json.ToJObject("query string filter");
            JToken member = obj.SelectToken(key, false);
            if (member is JArray)
            {
                accept = member.Any(c => c.ToString() == value);
            }
            else if (member is JValue)
            {
                accept = member.ToString() == value;
            }
            return accept;
        }

        private bool Filter(dynamic obj, string key, string value)
        {
            bool accept = true;
            Object member = DynamicUtils.GetMemberValue(obj, key);
            if (member is IEnumerable<Object>)
            {
                accept = ((IEnumerable<Object>)member).Any(c => c.ToString() == value);
            }
            else if (member is string)
            {
                accept = (string)member == value;
            }
            return accept;
        }

        private bool GetDemoData(TemplateInfo info, OpenContentSettings settings)
        {
            info.DataJson = "";
            info.SettingsJson = "";
            bool settingsNeeded = false;
            OpenContentController ctrl = new OpenContentController();
            var dataFilename = info.Template.PhysicalFullDirectory + "\\" + "data.json";
            if (File.Exists(dataFilename))
            {
                string fileContent = File.ReadAllText(dataFilename);
                if (!string.IsNullOrWhiteSpace(fileContent))
                {
                    info.DataJson = fileContent;
                }
            }
            if (settings.Template != null && info.Template.FilePath == settings.Template.FilePath)
            {
                info.SettingsJson = settings.Data;
            }
            if (string.IsNullOrEmpty(info.SettingsJson))
            {
                var settingsFilename = info.Template.PhysicalFullDirectory + "\\" + info.Template.FileNameWithoutExtension + "-data.json";
                if (File.Exists(settingsFilename))
                {
                    string fileContent = File.ReadAllText(settingsFilename);
                    if (!string.IsNullOrWhiteSpace(fileContent))
                    {
                        info.SettingsJson = fileContent;
                    }
                }
                else
                {
                    string schemaFilename = info.Template.PhysicalFullDirectory + "\\" + info.Template.FileNameWithoutExtension + "-schema.json";
                    settingsNeeded = File.Exists(schemaFilename);
                }
            }
            return !string.IsNullOrWhiteSpace(info.DataJson) && (!string.IsNullOrWhiteSpace(info.SettingsJson) || !settingsNeeded);
        }
        #endregion
        public DotNetNuke.Entities.Modules.Actions.ModuleActionCollection ModuleActions
        {
            get
            {
                var Actions = new ModuleActionCollection();

                TemplateManifest templateManifest = null;
                Manifest manifest;
                FileUri template = OpenContentUtils.GetTemplate(ModuleContext.Settings, out manifest, out templateManifest);
                bool templateDefined = template != null;

                bool listMode = templateManifest != null && templateManifest.IsListTemplate;
                if (Page.Request.QueryString["id"] != null)
                {
                    int.TryParse(Page.Request.QueryString["id"], out _itemId);
                }

                if (templateDefined)
                {
                    Actions.Add(ModuleContext.GetNextActionID(),
                        Localization.GetString((listMode && _itemId == Null.NullInteger ? ModuleActionType.AddContent : ModuleActionType.EditContent), LocalResourceFile),
                                ModuleActionType.AddContent,
                                "",
                                "",
                                (listMode && _itemId != Null.NullInteger ? ModuleContext.EditUrl("id", _itemId.ToString()) : ModuleContext.EditUrl()),
                                false,
                                SecurityAccessLevel.Edit,
                                true,
                                false);
                }
                /*
                string AddEditControl = PortalController.GetPortalSetting("OpenContent_AddEditControl", ModuleContext.PortalId, "");
                if (TemplateDefined && !string.IsNullOrEmpty(AddEditControl))
                {
                    Actions.Add(ModuleContext.GetNextActionID(),
                                Localization.GetString("AddEntity.Action", LocalResourceFile),
                                ModuleActionType.EditContent,
                                "",
                                "",
                                ModuleContext.EditUrl("AddEdit"),
                                false,
                                SecurityAccessLevel.Edit,
                                true,
                                false);
                }
                */
                Actions.Add(ModuleContext.GetNextActionID(),
                         Localization.GetString("EditSettings.Action", LocalResourceFile),
                         ModuleActionType.ContentOptions,
                         "",
                         "~/DesktopModules/OpenContent/images/settings.gif",
                         ModuleContext.EditUrl("EditSettings"),
                         false,
                         SecurityAccessLevel.Admin,
                         true,
                         false);

                if (templateDefined)
                    Actions.Add(ModuleContext.GetNextActionID(),
                               Localization.GetString("EditTemplate.Action", LocalResourceFile),
                               ModuleActionType.ContentOptions,
                               "",
                               "~/DesktopModules/OpenContent/images/edittemplate.png",
                               ModuleContext.EditUrl("EditTemplate"),
                               false,
                               SecurityAccessLevel.Host,
                               true,
                               false);
                if (templateDefined || manifest != null)
                    Actions.Add(ModuleContext.GetNextActionID(),
                               Localization.GetString("EditData.Action", LocalResourceFile),
                               ModuleActionType.EditContent,
                               "",
                               "~/DesktopModules/OpenContent/images/edit.png",
                               ModuleContext.EditUrl("EditData"),
                               false,
                               SecurityAccessLevel.Host,
                               true,
                               false);

                Actions.Add(ModuleContext.GetNextActionID(),
                           Localization.GetString("ShareTemplate.Action", LocalResourceFile),
                           ModuleActionType.ContentOptions,
                           "",
                           "~/DesktopModules/OpenContent/images/exchange.png",
                           ModuleContext.EditUrl("ShareTemplate"),
                           false,
                           SecurityAccessLevel.Host,
                           true,
                           false);

                Actions.Add(ModuleContext.GetNextActionID(),
                           Localization.GetString("EditGlobalSettings.Action", LocalResourceFile),
                           ModuleActionType.ContentOptions,
                           "",
                           "~/DesktopModules/OpenContent/images/exchange.png",
                           ModuleContext.EditUrl("EditGlobalSettings"),
                           false,
                           SecurityAccessLevel.Host,
                           true,
                           false);
                /*
                Actions.Add(ModuleContext.GetNextActionID(),
                           Localization.GetString("EditGlobalSettings.Action", LocalResourceFile),
                           ModuleActionType.ContentOptions,
                           "",
                           "~/DesktopModules/OpenContent/images/settings.png",
                           ModuleContext.EditUrl("EditGlobalSettings"),
                           false,
                           SecurityAccessLevel.Host,
                           true,
                           false);
                */
                Actions.Add(ModuleContext.GetNextActionID(),
                          Localization.GetString("Help.Action", LocalResourceFile),
                          ModuleActionType.ContentOptions,
                          "",
                          "~/DesktopModules/OpenContent/images/help.png",
                          "https://opencontent.readme.io",
                          false,
                          SecurityAccessLevel.Host,
                          true,
                          true);


                return Actions;
            }
        }

        private void RazorRender(WebPageBase Webpage, TextWriter writer, dynamic model)
        {
            var HttpContext = new HttpContextWrapper(System.Web.HttpContext.Current);
            if ((Webpage) is DotNetNukeWebPage<dynamic>)
            {
                var mv = (DotNetNukeWebPage<dynamic>)Webpage;
                mv.Model = model;
            }
            if (Webpage != null)
                Webpage.ExecutePageHierarchy(new WebPageContext(HttpContext, Webpage, null), writer, Webpage);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (Page.Request.QueryString["id"] != null)
            {
                int.TryParse(Page.Request.QueryString["id"], out _itemId);
            }

            //string Template = OpenContentUtils.GetTemplateFolder(ModuleContext.Settings);
            //string settingsJson = ModuleContext.Settings["data"] as string;
            if (!Page.IsPostBack)
            {
                //if (ModuleContext.EditMode && !ModuleContext.IsEditable)
                if (ModuleContext.PortalSettings.UserId > 0)
                {
                    string OpenContent_EditorsRoleId = PortalController.GetPortalSetting("OpenContent_EditorsRoleId", ModuleContext.PortalId, "");
                    if (!string.IsNullOrEmpty(OpenContent_EditorsRoleId))
                    {
                        int roleId = int.Parse(OpenContent_EditorsRoleId);
                        var objModule = ModuleContext.Configuration;
                        var permExist = objModule.ModulePermissions.Where(tp => tp.RoleID == roleId).Any();
                        if (!permExist)
                        {
                            //todo sacha: add two permissions, read and write; Or better still add all permissions that are available. eg if you installed extra permissions

                            var permissionController = new PermissionController();
                            // view permission
                            var arrSystemModuleViewPermissions = permissionController.GetPermissionByCodeAndKey("SYSTEM_MODULE_DEFINITION", "VIEW");
                            var permission = (PermissionInfo)arrSystemModuleViewPermissions[0];
                            var objModulePermission = new ModulePermissionInfo
                            {
                                ModuleID = ModuleContext.Configuration.ModuleID,
                                //ModuleDefID = permission.ModuleDefID,
                                //PermissionCode = permission.PermissionCode,
                                PermissionID = permission.PermissionID,
                                PermissionKey = permission.PermissionKey,
                                RoleID = roleId,
                                //UserID = userId,
                                AllowAccess = true
                            };
                            objModule.ModulePermissions.Add(objModulePermission);
                            // edit permission
                            arrSystemModuleViewPermissions = permissionController.GetPermissionByCodeAndKey("SYSTEM_MODULE_DEFINITION", "EDIT");
                            permission = (PermissionInfo)arrSystemModuleViewPermissions[0];
                            objModulePermission = new ModulePermissionInfo
                            {
                                ModuleID = ModuleContext.Configuration.ModuleID,
                                //ModuleDefID = permission.ModuleDefID,
                                //PermissionCode = permission.PermissionCode,
                                PermissionID = permission.PermissionID,
                                PermissionKey = permission.PermissionKey,
                                RoleID = roleId,
                                //UserID = userId,
                                AllowAccess = true
                            };
                            objModule.ModulePermissions.Add(objModulePermission);
                            ModulePermissionController.SaveModulePermissions(objModule);
                        }
                    }


                }
            }
        }
        private void IncludeResourses(FileUri template)
        {
            if (template != null)
            {
                //JavaScript.RequestRegistration() 
                //string templateBase = template.FilePath.Replace("$.hbs", ".hbs");
                var cssfilename = new FileUri(Path.ChangeExtension(template.FilePath, "css"));
                if (cssfilename.FileExists)
                {
                    ClientResourceManager.RegisterStyleSheet(Page, Page.ResolveUrl(cssfilename.UrlFilePath), FileOrder.Css.PortalCss);
                }
                var jsfilename = new FileUri(Path.ChangeExtension(template.FilePath, "js"));
                if (jsfilename.FileExists)
                {
                    ClientResourceManager.RegisterScript(Page, Page.ResolveUrl(jsfilename.UrlFilePath), FileOrder.Js.DefaultPriority+100);
                }
                ClientResourceManager.RegisterScript(Page, Page.ResolveUrl("~/DesktopModules/OpenContent/js/opencontent.js"), FileOrder.Js.DefaultPriority);


            }
        }

        protected void rblUseTemplate_SelectedIndexChanged(object sender, EventArgs e)
        {
            phFrom.Visible = rblUseTemplate.SelectedIndex == 1;
            phTemplateName.Visible = rblUseTemplate.SelectedIndex == 1;
            rblFrom.SelectedIndex = 0;
            var scriptFileSetting = OpenContentUtils.GetTemplate(ModuleContext.Settings);
            ddlTemplate.Items.Clear();
            if (rblUseTemplate.SelectedIndex == 0) // existing
            {
                ddlTemplate.Items.AddRange(OpenContentUtils.GetTemplatesFiles(ModuleContext.PortalSettings, ModuleContext.ModuleId, scriptFileSetting, "OpenContent").ToArray());
            }
            else if (rblUseTemplate.SelectedIndex == 1) // new
            {
                ddlTemplate.Items.AddRange(OpenContentUtils.GetTemplates(ModuleContext.PortalSettings, ModuleContext.ModuleId, scriptFileSetting, "OpenContent").ToArray());
            }
        }

        protected void rblFrom_SelectedIndexChanged(object sender, EventArgs e)
        {
            ddlTemplate.Items.Clear();
            if (rblFrom.SelectedIndex == 0) // site
            {
                var scriptFileSetting = OpenContentUtils.GetTemplate(ModuleContext.Settings);
                ddlTemplate.Items.AddRange(OpenContentUtils.GetTemplates(ModuleContext.PortalSettings, ModuleContext.ModuleId, scriptFileSetting, "OpenContent").ToArray());

                //ddlTemplate.Items.AddRange(OpenContentUtils.GetTemplatesFiles(ModuleContext.PortalSettings, ModuleContext.ModuleId, scriptFileSetting, "OpenContent").ToArray());
            }
            else if (rblFrom.SelectedIndex == 1) // web
            {
                FeedParser parser = new FeedParser();
                var items = parser.Parse("http://www.openextensions.net/templates?agentType=rss&PropertyTypeID=9", FeedType.RSS);
                foreach (var item in items.OrderBy(t => t.Title))
                {
                    ddlTemplate.Items.Add(new ListItem(item.Title, item.ZipEnclosure));
                }
                if (ddlTemplate.Items.Count > 0)
                {
                    tbTemplateName.Text = Path.GetFileNameWithoutExtension(ddlTemplate.Items[0].Value);
                }
            }
        }
        protected void bSave_Click(object sender, EventArgs e)
        {
            try
            {
                ModuleController mc = new ModuleController();

                if (rblDataSource.SelectedIndex == 0) // this module
                {
                    mc.DeleteModuleSetting(ModuleContext.ModuleId, "tabid");
                    mc.DeleteModuleSetting(ModuleContext.ModuleId, "moduleid");
                }
                else // other module
                {
                    var dsModule = mc.GetTabModule(int.Parse(ddlDataSource.SelectedValue));
                    mc.UpdateModuleSetting(ModuleContext.ModuleId, "tabid", dsModule.TabID.ToString());
                    mc.UpdateModuleSetting(ModuleContext.ModuleId, "moduleid", dsModule.ModuleID.ToString());
                }

                if (rblUseTemplate.SelectedIndex == 0) // existing
                {
                    mc.UpdateModuleSetting(ModuleContext.ModuleId, "template", ddlTemplate.SelectedValue);
                    //mc.UpdateModuleSetting(ModuleId, "data", HiddenField.Value);
                }
                else if (rblUseTemplate.SelectedIndex == 1) // new
                {
                    if (rblFrom.SelectedIndex == 0) // site
                    {
                        string oldFolder = Server.MapPath(ddlTemplate.SelectedValue);
                        string template = OpenContentUtils.CopyTemplate(ModuleContext.PortalId, oldFolder, tbTemplateName.Text);
                        mc.UpdateModuleSetting(ModuleContext.ModuleId, "template", template);
                    }
                    else if (rblFrom.SelectedIndex == 1) // web
                    {
                        string fileName = ddlTemplate.SelectedValue;
                        string template = OpenContentUtils.ImportFromWeb(ModuleContext.PortalId, fileName, tbTemplateName.Text);
                        mc.UpdateModuleSetting(ModuleContext.ModuleId, "template", template);
                    }
                }
                mc.DeleteModuleSetting(ModuleContext.ModuleId, "data");
                Response.Redirect(Globals.NavigateURL(), true);
            }
            catch (Exception exc)
            {
                //Module failed to load
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }
        protected void ddlTemplate_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (rblUseTemplate.SelectedIndex == 0) // existing
            {

            }
            else if (rblUseTemplate.SelectedIndex == 1) // new template
            {
                if (rblFrom.SelectedIndex == 1) // web
                {
                    tbTemplateName.Text = Path.GetFileNameWithoutExtension(ddlTemplate.SelectedValue);
                }
            }
        }

        protected void ddlDataSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            ModuleController mc = new ModuleController();
            var dsModule = mc.GetTabModule(int.Parse(ddlDataSource.SelectedValue));
            var dsSettings = new OpenContentSettings(dsModule.ModuleSettings);
            BindTemplates(dsSettings.Template, dsSettings.Template);
        }

        private void BindTemplates(FileUri Template, FileUri OtherModuleTemplate)
        {
            ddlTemplate.Items.Clear();
            ddlTemplate.Items.AddRange(OpenContentUtils.GetTemplatesFiles(ModuleContext.PortalSettings, ModuleContext.ModuleId, Template, "OpenContent", OtherModuleTemplate).ToArray());
            if (ddlTemplate.Items.Count == 0)
            {
                rblUseTemplate.Items[0].Enabled = false;
                rblUseTemplate.SelectedIndex = 1;
                rblUseTemplate_SelectedIndexChanged(null, null);
                rblFrom.Items[0].Enabled = false;
                rblFrom.SelectedIndex = 1;
                rblFrom_SelectedIndexChanged(null, null);

            }
        }

        protected void rblDataSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (rblDataSource.SelectedIndex == 1) // other module
            {
                //BindOtherModules(dsModule.TabID, dsModule.ModuleID);
                BindOtherModules(-1, -1);
                ModuleController mc = new ModuleController();
                var dsModule = mc.GetTabModule(int.Parse(ddlDataSource.SelectedValue));
                var dsSettings = new OpenContentSettings(dsModule.ModuleSettings);
                BindTemplates(dsSettings.Template, dsSettings.Template);
            }
            else // this module
            {
                BindOtherModules(-1, -1);
                BindTemplates(null, null);
            }
        }
    }
}