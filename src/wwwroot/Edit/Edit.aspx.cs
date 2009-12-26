using System;
using System.Collections.Generic;
using System.Web.UI.WebControls;
using N2.Definitions;
using N2.Edit.Web;
using N2.Security;
using N2.Web;
using N2.Web.UI.WebControls;
using N2.Workflow;
using N2.Persistence;

namespace N2.Edit
{
    [NavigationLinkPlugin("Edit", "edit", "../edit.aspx?selected={selected}", Targets.Preview, "~/edit/img/ico/png/page_edit.png", 20, 
		GlobalResourceClassName = "Navigation")]
    [ToolbarPlugin("EDIT", "edit", "edit.aspx?selected={selected}", ToolbarArea.Preview, Targets.Preview, "~/Edit/Img/Ico/png/page_edit.png", 50, ToolTip = "edit", 
		GlobalResourceClassName = "Toolbar")]
    [ControlPanelLink("cpEdit", "~/edit/img/ico/png/page_edit.png", "~/edit/edit.aspx?selected={Selected.Path}", "Edit page", 50, ControlPanelState.Visible)]
    [ControlPanelLink("cpEditPreview", "~/edit/img/ico/png/page_edit.png", "~/edit/edit.aspx?selectedUrl={Selected.Url}", "Back to edit", 10, ControlPanelState.Previewing)]
	[ControlPanelPreviewPublish("Publish the currently displayed page version.", 20, 
		AuthorizedRoles = new string[] { "Administrators", "Editors", "admin" })]
	[ControlPanelPreviewDiscard("Irrecoverably delete the currently displayed version.", 30, 
		AuthorizedRoles = new string[] { "Administrators", "Editors", "admin" })]
	[ControlPanelEditingSave("Save changes", 10)]
    [ControlPanelLink("cpEditingCancel", "~/edit/img/ico/png/cancel.png", "{Selected.Url}", "Cancel changes", 20, ControlPanelState.Editing, 
		UrlEncode = false)]
	public partial class Edit : EditPage
	{
		protected PlaceHolder phPluginArea;

		protected bool CreatingNew
		{
			get { return Request["discriminator"] != null; }
		}

		protected override void OnInit(EventArgs e)
		{
			if(Request["refresh"] == "true")
                Refresh(Selection.SelectedItem, ToolbarArea.Navigation);

			InitPlugins();
			InitItemEditor();
			InitTitle();
			InitButtons();
			base.OnInit(e);
		}

		private void InitButtons()
		{
            if (Request["cancel"] == "reloadTop")
                hlCancel.NavigateUrl = "javascript:window.top.location.reload();";
            else
                hlCancel.NavigateUrl = CancelUrl();

            bool isPublicableByUser = Engine.SecurityManager.IsAuthorized(User, ie.CurrentItem, Permission.Publish);
            bool isVersionable = ie.CurrentItem.GetType().GetCustomAttributes(typeof(NotVersionableAttribute), true).Length == 0;
            bool isWritableByUser = Engine.SecurityManager.IsAuthorized(User, Selection.SelectedItem, Permission.Write);
            bool isExisting = ie.CurrentItem.ID != 0;

            btnSavePublish.Enabled = isPublicableByUser;
            btnPreview.Enabled = isVersionable && isWritableByUser;
            btnSaveUnpublished.Enabled = isVersionable && isWritableByUser;
            btnSavePublishInFuture.Enabled = isWritableByUser && isExisting;
		}

		protected override void OnLoad(EventArgs e)
		{
			LoadZones();
			LoadInfo();

			if (!IsPostBack)
                RegisterSetupToolbarScript(Selection.SelectedItem);

			base.OnLoad(e);
		}

		protected override void OnPreRender(EventArgs e)
		{
			CheckRelatedVersions(ie.CurrentItem);
			
			base.OnPreRender(e);
		}



        protected void OnPublishCommand(object sender, CommandEventArgs e)
		{
            var ctx = new CommandContext(ie.CurrentItem, Interfaces.Editing, User, ie, new PageValidator<ContentItem>(Page));
            ctx.RedirectTo = Request["returnUrl"];
            Engine.Resolve<CommandDispatcher>().Publish(ctx);

            HandleResult(ctx);

            //Validate();
            //if (IsValid)
            //{
            //    if (!Engine.SecurityManager.IsAuthorized(User, ie.CurrentItem, N2.Security.Permission.Publish))
            //    {
            //        FailValidation("Not authorized to publish.");
            //    }

            //    try
            //    {
            //        SaveChanges();
            //    }
            //    catch (Exception ex)
            //    {
            //        Engine.Resolve<IErrorHandler>().Notify(ex);
            //        FailValidation(ex.Message);
            //    }
            //}
		}

    	protected void OnPreviewCommand(object sender, CommandEventArgs e)
		{
            var ctx = new CommandContext(ie.CurrentItem, Interfaces.Editing, User, ie, new PageValidator<ContentItem>(Page));
            ctx.RedirectTo = Request["returnUrl"];
            Engine.Resolve<CommandDispatcher>().Publish(ctx);

            HandleResult(ctx);


            //Validate();
            //if (IsValid)
            //{
            //    ContentItem savedVersion = SaveVersion();

            //    Url redirectTo = Engine.EditManager.GetPreviewUrl(savedVersion);

            //    redirectTo = redirectTo.AppendQuery("preview", savedVersion.ID);
            //    if (savedVersion.VersionOf != null)
            //        redirectTo = redirectTo.AppendQuery("original", savedVersion.VersionOf.ID);
            //    if (!string.IsNullOrEmpty(Request["returnUrl"]))
            //        redirectTo = redirectTo.AppendQuery("returnUrl", Request["returnUrl"]);

            //    Response.Redirect(redirectTo);
            //}
		}

		protected void OnSaveUnpublishedCommand(object sender, CommandEventArgs e)
		{
            var ctx = new CommandContext(ie.CurrentItem, Interfaces.Editing, User, ie, new PageValidator<ContentItem>(Page));
            ctx.RedirectTo = Request["returnUrl"];
            Engine.Resolve<CommandDispatcher>().Publish(ctx);

            HandleResult(ctx);

            //Validate();
            //if (IsValid)
            //{
            //    ContentItem savedVersion = SaveVersion();
            //    Url redirectUrl = Engine.EditManager.GetEditExistingItemUrl(savedVersion);
            //    Response.Redirect(redirectUrl.AppendQuery("refresh=true"));
            //}
        }

        protected void OnSaveFuturePublishCommand(object sender, CommandEventArgs e)
        {
            Validate();
            if (IsValid)
            {
                ContentItem savedVersion = SaveVersionForFuturePublishing();
                Url redirectUrl = Engine.EditManager.GetEditExistingItemUrl(savedVersion);
				Response.Redirect(redirectUrl.AppendQuery("refresh=true"));
			}
        }

        private void HandleResult(CommandContext ctx)
        {
            if (ctx.Errors.Count > 0)
            {
                string message = string.Empty;
                foreach (var ex in ctx.Errors)
                {
                    Engine.Resolve<IErrorHandler>().Notify(ex);
                    message += ex.Message + "<br/>";
                }
                FailValidation(message);
            }
            else if (!string.IsNullOrEmpty(ctx.RedirectTo))
            {
                Refresh(ctx.Data, ctx.RedirectTo);
            }
        }

        void FailValidation(string message)
        {
            cvException.IsValid = false;
            cvException.ErrorMessage = message;
        }
		
		
		
		protected override string GetToolbarSelectScript(ToolbarPluginAttribute toolbarPlugin)
		{
			if (CreatingNew)
				return "n2ctx.toolbarSelect('new');";

			return base.GetToolbarSelectScript(toolbarPlugin);
		}

		private void CheckRelatedVersions(ContentItem item)
		{
			hlNewerVersion.Visible = false;
			hlOlderVersion.Visible = false;

			if (item.VersionOf != null)
			{
				DisplayThisIsVersionInfo(item.VersionOf);
			}
			else
			{
				IList<ContentItem> unpublishedVersions = Find.Items
					.Where.VersionOf.Eq(item)
					.And.Updated.Gt(item.Updated)
					.OrderBy.Updated.Desc.MaxResults(1).Select();

				if(unpublishedVersions.Count > 0 && unpublishedVersions[0].Updated > item.Updated)
				{
					DisplayThisHasNewerVersionInfo(unpublishedVersions[0]);
				}
			}
		}

		private void DisplayThisHasNewerVersionInfo(ContentItem itemToLink)
		{
            string url = Url.ToAbsolute(Engine.EditManager.GetEditExistingItemUrl(itemToLink));
			hlNewerVersion.NavigateUrl = url;
			hlNewerVersion.Visible = true;
		}

		private void DisplayThisIsVersionInfo(ContentItem itemToLink)
		{
            string url = Url.ToAbsolute(Engine.EditManager.GetEditExistingItemUrl(itemToLink));
			hlOlderVersion.NavigateUrl = url;
			hlOlderVersion.Visible = true;
		}

		private void InitPlugins()
		{
			foreach(EditToolbarPluginAttribute plugin in Engine.EditManager.GetPlugins<EditToolbarPluginAttribute>(Page.User))
			{
                plugin.AddTo(phPluginArea, new PluginContext(Selection.SelectedItem, MemorizedItem, ControlPanelState.Visible));
			}
		}

		private void InitTitle()
		{
			if (ie.CurrentItem.ID == 0)
			{
				ItemDefinition definition = Engine.Definitions.GetDefinition(ie.CurrentItemType);
				string definitionTitle = GetGlobalResourceString("Definitions", definition.Discriminator + ".Title") ?? definition.Title;
				string format = GetLocalResourceString("EditPage.TitleFormat.New");
				Title = string.Format(format, definitionTitle);
			}
			else
			{
				string format = GetLocalResourceString("EditPage.TitleFormat.Update");
				Title = string.Format(format, ie.CurrentItem.Title);
			}
		}

		private void InitItemEditor()
		{
			string dataType = Request["dataType"];
			string discriminator = Request["discriminator"];
			if(!string.IsNullOrEmpty(discriminator))
			{
                ie.Discriminator = Engine.Definitions.GetDefinition(discriminator).Discriminator;
                ie.ParentPath = Selection.SelectedItem.Path;
			}
			else if (!string.IsNullOrEmpty(dataType))
			{
                Type t = Type.GetType(dataType);
                if (t == null)
                    throw new ArgumentException("Couldn't load a type of the given parameter '" + dataType + "'", "dataType");
                ItemDefinition d = Engine.Definitions.GetDefinition(discriminator);
                if(d == null)
                    throw new N2Exception("Couldn't find any definition for type '" + t + "'");
                ie.Discriminator = d.Discriminator;
                ie.ParentPath = Selection.SelectedItem.Path;
			}
			else
			{
                ie.CurrentItem = Selection.SelectedItem;
			}
			ie.ZoneName = base.Page.Request["zoneName"];
		}

		private void LoadZones()
		{
			Type itemType = ie.CurrentItemType;
			ucZones.CurrentItem = ie.CurrentItem;
			ItemDefinition definition = N2.Context.Definitions.GetDefinition(itemType);
			ucZones.DataSource = definition.AvailableZones;
			ucZones.DataBind();
		}

		private void LoadInfo()
		{
			ucInfo.CurrentItem = ie.CurrentItem;
			ucInfo.DataBind();
		}

        private void SaveChanges()
        {
            //Engine.Resolve<Workflow.CommandProvider>().PublishCommand()

			ItemEditorVersioningMode mode = (ie.CurrentItem.VersionOf == null) ? ItemEditorVersioningMode.VersionAndSave : ItemEditorVersioningMode.SaveAsMaster;
        	ContentItem currentItem = ie.Save(ie.CurrentItem, mode);

            if (Request["before"] != null)
            {
                ContentItem before = Engine.Resolve<N2.Edit.Navigator>().Navigate(Request["before"]);
                Engine.Resolve<ITreeSorter>().MoveTo(currentItem, NodePosition.Before, before);
            }
            else if (Request["after"] != null)
            {
                ContentItem after = Engine.Resolve<N2.Edit.Navigator>().Navigate(Request["after"]);
                Engine.Resolve<ITreeSorter>().MoveTo(currentItem, NodePosition.After, after);
            }

            Refresh(currentItem.VersionOf ?? currentItem, ToolbarArea.Both);
            Title = string.Format(GetLocalResourceString("SavedFormat"), currentItem.Title);
            ie.Visible = false;
        }

		private ContentItem SaveVersion()
		{
			ItemEditorVersioningMode mode = (ie.CurrentItem.VersionOf == null) ? ItemEditorVersioningMode.VersionOnly : ItemEditorVersioningMode.SaveOnly;
			return ie.Save(ie.CurrentItem, mode);
		}

        private ContentItem SaveVersionForFuturePublishing()
        {
            // Explicitly setting the current versions FuturePublishDate.
            // The database will end up with two new rows in the detail table.
            // On row pointing to the master and one to the latest/new version.
            System.Web.UI.Control item = null;
            if(ie.AddedEditors.TryGetValue("FuturePublishDate", out item))
            {
                DatePicker futurePublishDate = item as DatePicker;
                if(futurePublishDate != null)
                    ie.CurrentItem["FuturePublishDate"] = futurePublishDate.Text;
            }
            return SaveVersion();
        }
    }
}