using System;
using System.Collections.Generic;
using System.Xml;
using System.Windows.Forms;
using System.IO;

class MenuItem
{
	public string ItemName { get; set; } = "";
	public string TypeMenu { get; set; } = "";
	public string Icon { get; set; } = "";

	public string ActionNameMenu { get; set; } = "";
	public string ActionSnippet { get; set; } = "";
	public string ActionOpenFolder { get; set; } = "";
	public string ActionFolderScript { get; set; } = "";
	public string ActionScriptSwitch { get; set; } = "";
	public string ActionSettingsAhk { get; set; } = "";

	public List<MenuItem> Children { get; set; } = new();
}

class AppSettings
{
	public string Hotkey { get; set; } = "Ctrl+Shift+Z";
	public bool HideIcon { get; set; } = false;
	public string CurrentMenu { get; set; } = "MenuMain.xml";
	public string FolderIcon { get; set; } = "icons";
	public string FolderSnippets { get; set; } = "snippets";
	public bool AlwaysOnTop { get; set; } = false;
}

class Program
{
	
	static string AppPath = AppDomain.CurrentDomain.BaseDirectory;
	static string ConfigFile = Path.Combine(AppPath, "config.ini");
	
	static void Main()
	{
		ApplicationConfiguration.Initialize();

		AppSettings settings = LoadSettings();

		string menuFile = Path.Combine(
			AppPath,
			settings.CurrentMenu
		);

		XmlDocument doc = new XmlDocument();
		doc.Load(menuFile);

		List<MenuItem> menuItems = LoadMenu(doc.DocumentElement);

		ContextMenuStrip menu = new ContextMenuStrip();

		BuildMenu(menu.Items, menuItems);

		NotifyIcon tray = new NotifyIcon();
		tray.Icon = System.Drawing.SystemIcons.Application;
		tray.Visible = true;
		tray.ContextMenuStrip = menu;

		Application.Run();
	}

	static List<MenuItem> LoadMenu(XmlNode node)
	{
		List<MenuItem> items = new();

		foreach (XmlNode child in node.ChildNodes)
		{
			if (child.NodeType != XmlNodeType.Element)
				continue;

			MenuItem item = new();

			item.ItemName = GetAttribute(child, "itemName");
			item.TypeMenu = GetAttribute(child, "typeMenu");
			item.Icon = GetAttribute(child, "icon");

			item.ActionNameMenu = GetAttribute(child, "action_name_menu");
			item.ActionSnippet = GetAttribute(child, "action_snippet");
			item.ActionOpenFolder = GetAttribute(child, "action_open_folder");
			item.ActionFolderScript = GetAttribute(child, "action_folder_script");
			item.ActionScriptSwitch = GetAttribute(child, "action_script_switch");
			item.ActionSettingsAhk = GetAttribute(child, "action_settings_ahk");

			if (item.TypeMenu == "subMenu")
				item.Children = LoadMenu(child);

			items.Add(item);
		}

		return items;
	}

	static void BuildMenu(
		ToolStripItemCollection collection,
		List<MenuItem> items)
	{
		foreach (MenuItem item in items)
		{
			if (item.TypeMenu == "separator")
			{
				collection.Add(new ToolStripSeparator());
				continue;
			}

			ToolStripMenuItem menuItem =
				new ToolStripMenuItem(item.ItemName);

			if (item.TypeMenu == "subMenu")
			{
				BuildMenu(menuItem.DropDownItems, item.Children);
			}
			else if (item.TypeMenu == "item")
			{
				if (item.ActionNameMenu != "")
				{
					menuItem.Enabled = false;
				}
				else
				{
					menuItem.Click += (s, e) =>
					{
						ExecuteAction(item);
					};
				}
			}

			collection.Add(menuItem);
		}
	}

	static void ExecuteAction(MenuItem item)
	{
		if (item.ActionSnippet != "")
		{
			Clipboard.SetText(item.ActionSnippet);
			SendKeys.SendWait("^v");
		}
		else if (item.ActionOpenFolder != "")
		{
			System.Diagnostics.Process.Start(
				new System.Diagnostics.ProcessStartInfo
				{
					FileName = item.ActionOpenFolder,
					UseShellExecute = true
				});
		}
		else if (item.ActionFolderScript != "")
		{
			System.Diagnostics.Process.Start(
				new System.Diagnostics.ProcessStartInfo
				{
					FileName = item.ActionFolderScript,
					UseShellExecute = true
				});
		}
		else if (item.ActionScriptSwitch != "")
		{
			MessageBox.Show(
				"Switch menu: " + item.ActionScriptSwitch);
		}
		else if (item.ActionSettingsAhk != "")
		{
			MessageBox.Show(
				"Setting: " + item.ActionSettingsAhk);
		}
	}

	static string GetAttribute(XmlNode node, string name)
	{
		return node.Attributes?[name]?.Value ?? "";
	}
}