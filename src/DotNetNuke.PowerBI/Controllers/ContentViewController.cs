﻿using DotNetNuke.Framework;
using DotNetNuke.Framework.JavaScriptLibraries;
using DotNetNuke.Instrumentation;
using DotNetNuke.PowerBI.Components;
using DotNetNuke.PowerBI.Data;
using DotNetNuke.PowerBI.Data.SharedSettings;
using DotNetNuke.PowerBI.Models;
using DotNetNuke.PowerBI.Services;
using DotNetNuke.Services.Localization;
using DotNetNuke.UI.Utilities;
using DotNetNuke.Web.Mvc.Framework.ActionFilters;
using DotNetNuke.Web.Mvc.Framework.Controllers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using DotNetNuke.Framework.JavaScriptLibraries;

namespace DotNetNuke.PowerBI.Controllers
{
    public class ContentViewController : DnnController
    {
        private static readonly ILog Logger = LoggerSource.Instance.GetLogger(typeof(ContentViewController));

        // GET: ListView
        [DnnHandleError]
        public ActionResult Index()
        {
            var model = new EmbedConfig();
            try
            {
                // Remove the objects without permissions
                var settingsGroupId = Request.QueryString["sid"];
                if (string.IsNullOrEmpty(settingsGroupId))
                {
                    var defaultPbiSettingsGroupId = (string)ModuleContext.Settings["PowerBIEmbedded_SettingsGroupId"];
                    var pbiSettings = SharedSettingsRepository.Instance.GetSettings(ModuleContext.PortalId).RemoveUnauthorizedItems(User);
                    if (!string.IsNullOrEmpty(defaultPbiSettingsGroupId) && pbiSettings.Any(x => x.SettingsGroupId == defaultPbiSettingsGroupId))
                    {
                        settingsGroupId = defaultPbiSettingsGroupId;
                    }
                    else
                    {
                        settingsGroupId = pbiSettings.FirstOrDefault(x => !string.IsNullOrEmpty(x.SettingsGroupId))?.SettingsGroupId;
                    }
                }
                var embedService = new EmbedService(ModuleContext.PortalId, ModuleContext.TabModuleId, settingsGroupId);


                var user = ModuleContext.PortalSettings.UserInfo.Username;
                var userPropertySetting = (string)ModuleContext.Settings["PowerBIEmbedded_UserProperty"];
                if (userPropertySetting == "PowerBiGroup")
                {
                    var userProperty = PortalSettings.UserInfo.Profile.GetProperty("PowerBiGroup");
                    if (userProperty?.PropertyValue != null)
                    {
                        user = userProperty.PropertyValue;
                    }
                }
                else if (userPropertySetting == "Custom")
                {
                    var customProperties = (string) ModuleContext.Settings["PowerBIEmbedded_CustomUserProperty"];
                    var matches = Regex.Matches(customProperties, @"\[PROFILE:(?<PROPERTY>[A-z]*)]");
        
                    foreach (Match match in matches)
                    {
                        var userProperty = PortalSettings.UserInfo.Profile.GetProperty(match.Groups["PROPERTY"].Value);
                        if (userProperty?.PropertyValue != null)
                        {
                            customProperties = customProperties.Replace(match.Value, userProperty.PropertyValue);
                        }
                    }

                    user = customProperties;
                }

                if (!string.IsNullOrEmpty(Request["dashboardId"]))
                {
                    var roles = string.Join(",", ModuleContext.PortalSettings.UserInfo.Roles);
                    model = embedService.GetDashboardEmbedConfigAsync(ModuleContext.PortalSettings.UserId, user,roles, Request["dashboardId"]).Result;
                }
                else if (!string.IsNullOrEmpty(Request["reportId"]))
                {
                    var roles = string.Join(",", ModuleContext.PortalSettings.UserInfo.Roles);
                    model = embedService.GetReportEmbedConfigAsync(ModuleContext.PortalSettings.UserId, user, roles, Request["reportId"]).Result;
                }
                else if (!string.IsNullOrEmpty(GetSetting("PowerBIEmbedded_ContentItemId")))
                {
                    var roles = string.Join(",", ModuleContext.PortalSettings.UserInfo.Roles);
                    var contentItemId = GetSetting("PowerBIEmbedded_ContentItemId");
                    if (contentItemId.Substring(0, 2) == "D_")
                    {
                        model = embedService.GetDashboardEmbedConfigAsync(ModuleContext.PortalSettings.UserId, user, roles, contentItemId.Substring(2)).Result;
                    }
                    else
                    {
                        model = embedService.GetReportEmbedConfigAsync(ModuleContext.PortalSettings.UserId, user, roles, contentItemId.Substring(2)).Result;
                    }
                }

                var permissionsRepo = ObjectPermissionsRepository.Instance;
                if (!string.IsNullOrEmpty(model.Id) && !permissionsRepo.HasPermissions(embedService.Settings.InheritPermissions ?
                    embedService.Settings.SettingsGroupId : model.Id, ModuleContext.PortalId, 1, User))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "User doesn't have permissions for this resource");
                }

                ViewBag.Locale = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.Substring(0, 2);

                ViewBag.FilterPaneVisible = bool.Parse(GetSetting("PowerBIEmbedded_FilterPaneVisible", "true"));
                ViewBag.NavPaneVisible = bool.Parse(GetSetting("PowerBIEmbedded_NavPaneVisible", "true"));
                ViewBag.OverrideVisualHeaderVisibility = bool.Parse(GetSetting("PowerBIEmbedded_OverrideVisualHeaderVisibility", "false"));
                ViewBag.OverrideFilterPaneVisibility = bool.Parse(GetSetting("PowerBIEmbedded_OverrideFilterPaneVisibility", "false"));
                ViewBag.VisualHeaderVisible = bool.Parse(GetSetting("PowerBIEmbedded_VisualHeaderVisible", "false"));
                ViewBag.PrintVisible = bool.Parse(GetSetting("PowerBIEmbedded_PrintVisible", "false"));
                ViewBag.ToolbarVisible = bool.Parse(GetSetting("PowerBIEmbedded_ToolbarVisible", "false"));
                ViewBag.FullScreenVisible = bool.Parse(GetSetting("PowerBIEmbedded_FullScreenVisible", "false"));
                ViewBag.BookmarksVisible = bool.Parse(GetSetting("PowerBIEmbedded_BookmarksVisible", "false"));
                ViewBag.ApplicationInsightsEnabled = bool.Parse(GetSetting("PowerBIEmbedded_ApplicationInsightsEnabled", "false"));
                ViewBag.Height = GetSetting("PowerBIEmbedded_Height");
                ViewBag.PageName = GetSetting("PowerBIEmbedded_PageName");

                // Sets the reports page on the viewbag
                if (embedService != null)
                {
                    var reportsPage = embedService.Settings.ContentPageUrl;
                    if (reportsPage != null && !reportsPage.StartsWith("http"))
                    {
                        reportsPage = Common.Globals.AddHTTP(PortalSettings.PortalAlias.HTTPAlias) + reportsPage;
                    }
                    ViewBag.ReportsPage = reportsPage;
                }

                var currentLocale = LocaleController.Instance.GetLocale(ModuleContext.PortalId, CultureInfo.CurrentCulture.Name);

                var context = new
                {
                    ModuleContext.PortalId,
                    ModuleContext.TabId,
                    ModuleContext.ModuleId,
                    CurrentLocale = new Culture()
                    {
                        LanguageId = currentLocale.LanguageId,
                        Code = currentLocale.Code,
                        Name = currentLocale.NativeName.Split('(')[0].Trim()
                    },
                    ViewBag.OverrideVisualHeaderVisibility,
                    ViewBag.OverrideFilterPaneVisibility,
                    ViewBag.ApplicationInsightsEnabled,
                    ViewBag.NavPaneVisible,
                    ViewBag.VisualHeaderVisible,
                    ViewBag.Locale,
                    ViewBag.FilterPaneVisible,
                    ViewBag.ReportsPage,
                    ViewBag.Height,
                    ViewBag.PageName,
                    model.ContentType,
                    Token = model.EmbedToken?.Token,
                    model.EmbedUrl,
                    model.Id
                };
                ServicesFramework.Instance.RequestAjaxScriptSupport();
                ServicesFramework.Instance.RequestAjaxAntiForgerySupport();
                DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.DnnPlugins);
                ClientAPI.RegisterClientVariable(DnnPage, $"ViewContext_{ModuleContext.ModuleId}", 
                    JsonConvert.SerializeObject(context, Formatting.None), true);
              

                return View(model);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                model.ErrorMessage = LocalizeString("Error");
                return View(model);
            }
        }

        private string GetSetting(string key, string defaultValue = "")
        {
            return ModuleContext.Settings.ContainsKey(key) ?
                (string)ModuleContext.Settings[key]
                : defaultValue;
        }
    }
}