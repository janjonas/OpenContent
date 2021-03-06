﻿using DotNetNuke.Web.Client;
using DotNetNuke.Web.Client.ClientResourceManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;
using HandlebarsDotNet;
using DotNetNuke.UI.Modules;
using System.Globalization;
using Satrabel.OpenContent.Components.Manifest;
using Satrabel.OpenContent.Components.TemplateHelpers;
using Satrabel.OpenContent.Components.Dynamic;
using System.Collections;
using DotNetNuke.Entities.Portals;
using Satrabel.OpenContent.Components.Logging;
using Satrabel.OpenContent.Components.Logging;


namespace Satrabel.OpenContent.Components.Handlebars
{
    public class HandlebarsEngine
    {
        private Func<object, string> _template;
        private int _jsOrder = 100;

        public void Compile(string source)
        {
            try
            {
                var hbs = HandlebarsDotNet.Handlebars.Create();
                RegisterDivideHelper(hbs);
                RegisterMultiplyHelper(hbs);
                RegisterEqualHelper(hbs);
                RegisterFormatNumberHelper(hbs);
                RegisterFormatDateTimeHelper(hbs);
                RegisterImageUrlHelper(hbs);
                RegisterArrayIndexHelper(hbs);
                RegisterArrayTranslateHelper(hbs);
                RegisterIfAndHelper(hbs);
                _template = hbs.Compile(source);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(string.Format("Failed to render Handlebar template source:[{0}]", source), ex);
                throw new TemplateException("Failed to render Handlebar template " + source, ex, null, source);
            }
        }

        public string Execute(dynamic model)
        {
            try
            {
                var result = _template(model);
                return result;
            }
            catch (Exception ex)
            {
                Log.Logger.Error(string.Format("Failed to execute Handlebar template with model:[{1}]", "", model), ex);
                throw new TemplateException("Failed to render Handlebar template ", ex, model, "");
            }
        }

        public string Execute(string source, dynamic model)
        {
            try
            {
                var hbs = HandlebarsDotNet.Handlebars.Create();
                RegisterDivideHelper(hbs);
                RegisterMultiplyHelper(hbs);
                RegisterEqualHelper(hbs);
                RegisterFormatNumberHelper(hbs);
                RegisterFormatDateTimeHelper(hbs);
                RegisterImageUrlHelper(hbs);
                RegisterArrayIndexHelper(hbs);
                RegisterArrayTranslateHelper(hbs);
                RegisterArrayLookupHelper(hbs);
                RegisterIfAndHelper(hbs);
                RegisterEachPublishedHelper(hbs);
                return CompileTemplate(hbs, source, model);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(string.Format("Failed to render Handlebar template source:[{0}], model:[{1}]", source, model), ex);
                throw new TemplateException("Failed to render Handlebar template ", ex, model, source);
            }
        }
        public string Execute(Page page, FileUri sourceFilename, dynamic model)
        {
            try
            {
                string source = File.ReadAllText(sourceFilename.PhysicalFilePath);
                string sourceFolder = sourceFilename.UrlFolder.Replace("\\", "/") + "/";
                var hbs = HandlebarsDotNet.Handlebars.Create();
                RegisterDivideHelper(hbs);
                RegisterMultiplyHelper(hbs);
                RegisterEqualHelper(hbs);
                RegisterFormatNumberHelper(hbs);
                RegisterFormatDateTimeHelper(hbs);
                RegisterImageUrlHelper(hbs);
                RegisterScriptHelper(hbs);
                RegisterHandlebarsHelper(hbs);
                RegisterRegisterStylesheetHelper(hbs, page, sourceFolder);
                RegisterRegisterScriptHelper(hbs, page, sourceFolder);
                RegisterArrayIndexHelper(hbs);
                RegisterArrayTranslateHelper(hbs);
                RegisterArrayLookupHelper(hbs);
                RegisterIfAndHelper(hbs);
                RegisterEachPublishedHelper(hbs);
                return CompileTemplate(hbs, source, model);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(string.Format("Failed to render Handlebar template source:[{0}], model:[{1}]", sourceFilename, model), ex);
                throw new TemplateException("Failed to render Handlebar template " + sourceFilename.FilePath, ex, model, sourceFilename.FilePath);
            }
        }
        public string Execute(Page page, IModuleControl module, TemplateFiles files, string templateVirtualFolder, dynamic model)
        {
            string sourceFilename = System.Web.Hosting.HostingEnvironment.MapPath(templateVirtualFolder + "/" + files.Template);
            try
            {

                string source = File.ReadAllText(sourceFilename);
                string sourceFolder = templateVirtualFolder.Replace("\\", "/") + "/";
                var hbs = HandlebarsDotNet.Handlebars.Create();
                if (files.PartialTemplates != null)
                {
                    foreach (var part in files.PartialTemplates.Where(t => t.Value.ClientSide == false))
                    {
                        RegisterTemplate(hbs, part.Key, templateVirtualFolder + "/" + part.Value.Template);
                    }
                }
                RegisterDivideHelper(hbs);
                RegisterMultiplyHelper(hbs);
                RegisterEqualHelper(hbs);
                RegisterFormatNumberHelper(hbs);
                RegisterFormatDateTimeHelper(hbs);
                RegisterImageUrlHelper(hbs);
                RegisterScriptHelper(hbs);
                RegisterHandlebarsHelper(hbs);
                RegisterRegisterStylesheetHelper(hbs, page, sourceFolder);
                RegisterRegisterScriptHelper(hbs, page, sourceFolder);
                //RegisterEditUrlHelper(hbs, module);
                RegisterArrayIndexHelper(hbs);
                RegisterArrayTranslateHelper(hbs);
                RegisterArrayLookupHelper(hbs);
                RegisterIfAndHelper(hbs);
                RegisterEachPublishedHelper(hbs);
                return CompileTemplate(hbs, source, model);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(string.Format("Failed to render Handlebar template source:[{0}], model:[{1}]", sourceFilename, model), ex);
                throw new TemplateException("Failed to render Handlebar template " + sourceFilename, ex, model, sourceFilename);
            }
        }

        private string CompileTemplate(IHandlebars hbs, string source, dynamic model)
        {
            var compiledTemplate = hbs.Compile(source);
            return compiledTemplate(model);
        }

        private void RegisterTemplate(HandlebarsDotNet.IHandlebars hbs, string name, string sourceFilename)
        {
            string fileName = System.Web.Hosting.HostingEnvironment.MapPath(sourceFilename);
            if (File.Exists(fileName))
            {
                using (var reader = new StreamReader(fileName))
                {
                    var partialTemplate = hbs.Compile(reader);
                    hbs.RegisterTemplate(name, partialTemplate);
                }
            }
        }
        private void RegisterMultiplyHelper(HandlebarsDotNet.IHandlebars hbs)
        {
            hbs.RegisterHelper("multiply", (writer, context, parameters) =>
            {
                try
                {
                    int a = int.Parse(parameters[0].ToString());
                    int b = int.Parse(parameters[1].ToString());
                    int c = a * b;
                    HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, c.ToString());
                }
                catch (Exception)
                {
                    HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, "0");
                }
            });
        }
        private void RegisterDivideHelper(HandlebarsDotNet.IHandlebars hbs)
        {
            hbs.RegisterHelper("divide", (writer, context, parameters) =>
            {
                try
                {
                    int a = int.Parse(parameters[0].ToString());
                    int b = int.Parse(parameters[1].ToString());
                    int c = a / b;
                    HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, c.ToString());
                }
                catch (Exception)
                {
                    HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, "0");
                }
            });
        }
        /// <summary>
        /// A block helper.
        /// Returns nothing, executes the template-part if contidions are met.
        /// </summary>
        /// <param name="hbs">The HBS.</param>
        private void RegisterEqualHelper(HandlebarsDotNet.IHandlebars hbs)
        {
            hbs.RegisterHelper("equal", (writer, options, context, arguments) =>
            {
                if (arguments.Length == 2 && arguments[0].Equals(arguments[1]))
                {
                    options.Template(writer, (object)context);
                }
                else
                {
                    options.Inverse(writer, (object)context);
                }
            });
        }
        private void RegisterEachPublishedHelper(HandlebarsDotNet.IHandlebars hbs)
        {
            hbs.RegisterHelper("published", (writer, options, context, parameters) =>
            {
                bool EditMode = PortalSettings.Current.UserMode == PortalSettings.Mode.Edit;
                if (EditMode)
                {
                    options.Template(writer, parameters[0]);
                }
                else
                {
                    var lst = new List<dynamic>();
                    foreach (dynamic item in parameters[0] as IEnumerable)
                    {
                        bool show = true;
                        try
                        {
                            if (item.publishstatus != "published")
                            {
                                show = false;
                            }
                        }
                        catch (Exception)
                        {
                        }
                        try
                        {
                            DateTime publishstartdate = DateTime.Parse(item.publishstartdate, null, System.Globalization.DateTimeStyles.RoundtripKind);
                            if (publishstartdate > DateTime.Today)
                            {
                                show = false;
                            }
                        }
                        catch (Exception)
                        {
                        }
                        try
                        {
                            DateTime publishenddate = DateTime.Parse(item.publishenddate, null, System.Globalization.DateTimeStyles.RoundtripKind);
                            if (publishenddate < DateTime.Today)
                            {
                                show = false;
                            }
                        }
                        catch (Exception)
                        {
                        }
                        if (show)
                        {
                            lst.Add(item);
                        }

                    }
                    options.Template(writer, lst);
                }
            });

        }
        private void RegisterScriptHelper(HandlebarsDotNet.IHandlebars hbs)
        {
            hbs.RegisterHelper("script", (writer, options, context, arguments) =>
            {
                HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, "<script>");
                options.Template(writer, (object)context);
                HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, "</script>");
            });

        }
        private void RegisterHandlebarsHelper(HandlebarsDotNet.IHandlebars hbs)
        {
            hbs.RegisterHelper("handlebars", (writer, options, context, arguments) =>
            {
                HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, "<script id=\"jplist-templatex\" type=\"text/x-handlebars-template\">");
                HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, context);
                HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, "</script>");
            });
        }
        private void RegisterRegisterScriptHelper(HandlebarsDotNet.IHandlebars hbs, Page page, string sourceFolder)
        {
            hbs.RegisterHelper("registerscript", (writer, context, parameters) =>
            {
                if (parameters.Length == 1)
                {
                    string jsfilename = parameters[0].ToString();
                    if (!jsfilename.StartsWith("/") && !jsfilename.Contains("//"))
                    {
                        jsfilename = sourceFolder + jsfilename;
                    }
                    ClientResourceManager.RegisterScript(page, page.ResolveUrl(jsfilename), _jsOrder++ /*FileOrder.Js.DefaultPriority*/);
                }
            });
        }
        private void RegisterRegisterStylesheetHelper(HandlebarsDotNet.IHandlebars hbs, Page page, string sourceFolder)
        {
            hbs.RegisterHelper("registerstylesheet", (writer, context, parameters) =>
            {
                if (parameters.Length == 1)
                {
                    string cssfilename = parameters[0].ToString();
                    if (!cssfilename.StartsWith("/") && !cssfilename.Contains("//"))
                    {
                        cssfilename = sourceFolder + cssfilename;
                    }
                    ClientResourceManager.RegisterStyleSheet(page, page.ResolveUrl(cssfilename), FileOrder.Css.PortalCss);
                }
            });
        }
        private void RegisterEditUrlHelper(HandlebarsDotNet.IHandlebars hbs, IModuleControl module)
        {
            hbs.RegisterHelper("editurl", (writer, context, parameters) =>
            {
                if (parameters.Length == 1)
                {
                    string id = parameters[0] as string;
                    writer.WriteSafeString(module.ModuleContext.EditUrl("itemid", id));
                }
            });
        }

        private void RegisterImageUrlHelper(HandlebarsDotNet.IHandlebars hbs)
        {
            hbs.RegisterHelper("imageurl", (writer, context, parameters) =>
            {
                if (parameters.Length == 3)
                {
                    string imageId = parameters[0] as string;
                    int width = Normalize.DynamicValue(parameters[1], -1);
                    string ratiostring = parameters[2] as string;
                    bool isMobile = HttpContext.Current.Request.Browser.IsMobileDevice;

                    var imageObject = Convert.ToInt32(imageId) == 0 ? null : new ImageUri(Convert.ToInt32(imageId));
                    var imageUrl = imageObject == null ? string.Empty : imageObject.GetImageUrl(width, ratiostring, isMobile);

                    writer.WriteSafeString(imageUrl);
                }
            });
        }
        /// <summary>
        /// Retrieved an element from a list.
        /// First param is List, the second param is the int with the position to retrieve. 
        /// Zero-based retrieval
        /// </summary>
        /// <param name="hbs">The HBS.</param>
        private void RegisterArrayIndexHelper(HandlebarsDotNet.IHandlebars hbs)
        {
            hbs.RegisterHelper("arrayindex", (writer, context, parameters) =>
            {
                try
                {
                    object[] a;
                    if (parameters[0] is IEnumerable<Object>)
                    {
                        var en = parameters[0] as IEnumerable<Object>;
                        a = en.ToArray();
                    }
                    else
                    {
                        a = (object[])parameters[0];
                    }


                    int b = int.Parse(parameters[1].ToString());
                    object c = a[b];
                    HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, c.ToString());
                }
                catch (Exception)
                {
                    HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, "");
                }
            });

        }

        private void RegisterArrayTranslateHelper(HandlebarsDotNet.IHandlebars hbs)
        {
            hbs.RegisterHelper("arraytranslate", (writer, context, parameters) =>
            {
                try
                {
                    object[] a;
                    if (parameters[0] is IEnumerable<Object>)
                    {
                        var en = parameters[0] as IEnumerable<Object>;
                        a = en.ToArray();
                    }
                    else
                    {
                        a = (object[])parameters[0];
                    }
                    object[] b;
                    if (parameters[1] is IEnumerable<Object>)
                    {
                        var en = parameters[1] as IEnumerable<Object>;
                        b = en.ToArray();
                    }
                    else
                    {
                        b = (object[])parameters[1];
                    }
                    string c = parameters[2].ToString();
                    int i = Array.IndexOf(a, c);

                    object res = b[i];
                    HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, res.ToString());
                }
                catch (Exception)
                {
                    HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, "");
                }
            });

        }

        private void RegisterArrayLookupHelper(HandlebarsDotNet.IHandlebars hbs)
        {
            hbs.RegisterHelper("lookup", (writer, options, context, arguments) =>
            {
                object[] arr;
                if (arguments[0] is IEnumerable<Object>)
                {
                    var en = arguments[0] as IEnumerable<Object>;
                    arr = en.ToArray();
                }
                else
                {
                    arr = (object[])arguments[0];
                }

                var field = arguments[1].ToString();
                var value = arguments[2].ToString();
                foreach (var obj in arr)
                {
                    Object member = DynamicUtils.GetMemberValue(obj, field);
                    if (value.Equals(member))
                    {
                        options.Template(writer, (object)obj);
                    }
                }
                options.Inverse(writer, (object)context);
            });
            /*
            hbs.RegisterHelper("arraylookup", (writer, context, parameters) =>
            {
                try
                {
                    object[] arr;
                    if (parameters[0] is IEnumerable<Object>)
                    {
                        var en = parameters[0] as IEnumerable<Object>;
                        arr = en.ToArray();
                    }
                    else
                    {
                        arr = (object[])parameters[0];
                    }
                    
                    var value = parameters[1].ToString();
                    var text = parameters[2].ToString();
                    var key = parameters[3].ToString();
                    foreach (var item in collection)
                    {
                        
                    }
                    object res = b[i];
                    string c = parameters[2].ToString();
                    HandlebarsDotNet.HandlebarsExtensions.
                    //HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, res.ToString());
                }
                catch (Exception)
                {
                    HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, "");
                }
            });
            */
        }

        private void RegisterFormatNumberHelper(HandlebarsDotNet.IHandlebars hbs)
        {
            hbs.RegisterHelper("formatNumber", (writer, context, parameters) =>
            {
                try
                {
                    decimal? number = parameters[0] as decimal?;
                    string format = parameters[1].ToString();
                    string provider = parameters[2].ToString();

                    IFormatProvider formatprovider = null;
                    if (provider.ToLower() == "invariant")
                    {
                        formatprovider = CultureInfo.InvariantCulture;
                    }
                    else if (!string.IsNullOrWhiteSpace(provider))
                    {
                        formatprovider = CultureInfo.CreateSpecificCulture(provider);
                    }

                    string res = number.Value.ToString(format, formatprovider);
                    HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, res);
                }
                catch (Exception)
                {
                    HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, "");
                }
            });
        }
        private void RegisterFormatDateTimeHelper(HandlebarsDotNet.IHandlebars hbs)
        {
            hbs.RegisterHelper("formatDateTime", (writer, context, parameters) =>
            {
                try
                {
                    string res;
                    //DateTime? datetime = parameters[0] as DateTime?;

                    DateTime datetime = DateTime.Parse(parameters[0].ToString(), null, System.Globalization.DateTimeStyles.RoundtripKind);
                    string format = "dd/MM/yyyy";
                    if (parameters.Count() > 1)
                    {
                        format = parameters[1].ToString();
                    }
                    if (parameters.Count() > 2 && !string.IsNullOrWhiteSpace(parameters[2].ToString()))
                    {
                        string provider = parameters[2].ToString();
                        IFormatProvider formatprovider = null;
                        if (provider.ToLower() == "invariant")
                        {
                            formatprovider = CultureInfo.InvariantCulture;
                        }
                        else
                        {
                            formatprovider = CultureInfo.CreateSpecificCulture(provider);
                        }
                        res = datetime.ToString(format, formatprovider);
                    }
                    else
                    {
                        res = datetime.ToString(format);
                    }

                    HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, res);
                }
                catch (Exception)
                {
                    HandlebarsDotNet.HandlebarsExtensions.WriteSafeString(writer, "");
                }
            });
        }
        private void RegisterIfAndHelper(IHandlebars hbs)
        {
            hbs.RegisterHelper("ifand", (writer, options, context, arguments) =>
            {
                bool res = true;
                foreach (var arg in arguments)
                {
                    res = res && HandlebarsUtils.IsTruthyOrNonEmpty(arg);
                }
                if (res)
                {
                    options.Template(writer, (object)context);
                }
                else
                {
                    options.Inverse(writer, (object)context);
                }
            });
        }
    }
}