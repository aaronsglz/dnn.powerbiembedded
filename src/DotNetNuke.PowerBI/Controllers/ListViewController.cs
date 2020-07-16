﻿using DotNetNuke.Common;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Instrumentation;
using DotNetNuke.PowerBI.Data;
using DotNetNuke.PowerBI.Data.SharedSettings;
using DotNetNuke.PowerBI.Models;
using DotNetNuke.PowerBI.Services;
using DotNetNuke.Web.Mvc.Framework.ActionFilters;
using DotNetNuke.Web.Mvc.Framework.Controllers;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;

namespace DotNetNuke.PowerBI.Controllers
{
    public class ListViewController : DnnController
    {
        private static readonly ILog Logger = LoggerSource.Instance.GetLogger(typeof(ListViewController));
        // GET: ListView
        [DnnHandleError]
        public ActionResult Index()
        {
            try
            {
                EmbedService embedService;
                if (!string.IsNullOrEmpty(Request.QueryString["sid"]))
                    embedService = new EmbedService(ModuleContext.PortalId, ModuleContext.TabModuleId, Request.QueryString["sid"]);
                else
                {
                    var defaultPbiSettingsGroupId = (string)ModuleContext.Settings["PowerBIEmbedded_SettingsGroupId"];
                    var pbiSettings = SharedSettingsRepository.Instance.GetSettings(ModuleContext.PortalId).RemoveUnauthorizedItems(User);
                    if (!string.IsNullOrEmpty(defaultPbiSettingsGroupId) && pbiSettings.Any(x => x.SettingsGroupId == defaultPbiSettingsGroupId))
                        embedService = new EmbedService(ModuleContext.PortalId, ModuleContext.TabModuleId, (string)ModuleContext.Settings["PowerBIEmbedded_SettingsGroupId"]);
                    else
                        embedService = new EmbedService(ModuleContext.PortalId, ModuleContext.TabModuleId, pbiSettings.FirstOrDefault(x => !string.IsNullOrEmpty(x.SettingsGroupId)).SettingsGroupId);
                }

                var model = embedService.GetContentListAsync(ModuleContext.PortalSettings.UserId).Result;                
                if (model != null)
                {
                    // Remove other culture contents
                    model = model.RemoveOtherCultureItems();

                    // Remove the objects without permissions
                    model = model.RemoveUnauthorizedItems(User);

                    // Sets the reports page on the viewbag
                    var reportsPage = embedService.Settings.ContentPageUrl;
                    if (!reportsPage.StartsWith("http"))
                    {
                        reportsPage = Globals.AddHTTP(PortalSettings.PortalAlias.HTTPAlias) + reportsPage;
                    }

                    ViewBag.ReportsPage = reportsPage;

                    //Get SettingsId
                    if (!String.IsNullOrEmpty(Request.QueryString["sid"]))
                    {
                        ViewBag.SettingsGroupId = Request.QueryString["sid"];
                    }
                    else
                    {
                        var tabModuleSettings = ModuleController.Instance.GetTabModule(ModuleContext.TabModuleId)
                            .TabModuleSettings;
                        if (tabModuleSettings.ContainsKey("PowerBIEmbedded_SettingsGroupId"))
                            ViewBag.SettingsGroupId = tabModuleSettings["PowerBIEmbedded_SettingsGroupId"];
                        else
                            ViewBag.SettingsGroupId = "";
                    }

                    return View(model);
                }

                return View();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return View();
            }
        }
    }
}