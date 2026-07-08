using System;
using System.Collections.Generic;
using System.Xml;
using System.Windows.Forms;
using System.IO;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Diagnostics;

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
	
	static string AppPath = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)!.Parent!.Parent!.Parent!.FullName;
	static string ConfigFile = Path.Combine(AppPath, "config.ini");
	
	[STAThread]
	static void Main()
	{
		ApplicationConfiguration.Initialize();

		AppSettings settings = LoadSettings();

		string menuFile = Path.Combine(
			AppPath,
			settings.CurrentMenu
		);

		string jsonFile = Path.ChangeExtension(menuFile, ".json");

		List<MenuItem> menuItems;

		if (File.Exists(jsonFile))
		{
			menuItems = JsonSerializer.Deserialize<List<MenuItem>>(
				File.ReadAllText(jsonFile)
			) ?? new List<MenuItem>();
		}
		else
		{
			menuItems = LoadMenuCache(settings.CurrentMenu);
		}

		ContextMenuStrip menu = new ContextMenuStrip();

		BuildMenu(menu.Items, menuItems);

		NotifyIcon tray = new NotifyIcon();
		tray.Icon = System.Drawing.SystemIcons.Application;
		tray.Visible = true;
		tray.ContextMenuStrip = menu;

		Application.Run();
	}

	static List<MenuItem> LoadMenuCache(string menuName)
	{
		string xmlFile = Path.Combine(
			AppPath,
			menuName
		);

		string jsonFile = Path.ChangeExtension(
			xmlFile,
			".json"
		);

		if (!File.Exists(jsonFile))
		{
			XmlDocument doc = new XmlDocument();
			doc.Load(xmlFile);

			List<MenuItem> menuItems = LoadMenu(doc.DocumentElement);

			File.WriteAllText(
				jsonFile,
				JsonSerializer.Serialize(
					menuItems,
					new JsonSerializerOptions
					{
						WriteIndented = true,
						Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
					})
			);

			return menuItems;
		}

		string json = File.ReadAllText(jsonFile);

		return JsonSerializer.Deserialize<List<MenuItem>>(json)
			?? new List<MenuItem>();
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
			string text = item.ActionSnippet;

			AppSettings settings = LoadSettings();

			string snippetFile = Path.Combine(
				AppPath,
				settings.FolderSnippets,
				item.ActionSnippet
			);

			if (File.Exists(snippetFile))
			{
				text = File.ReadAllText(
					snippetFile,
					System.Text.Encoding.UTF8
				);
			}

			Clipboard.SetText(text);
			SendKeys.SendWait("^v");
		}
		else if (item.ActionOpenFolder != "")
		{
			
			string command = Environment.ExpandEnvironmentVariables(
				item.ActionOpenFolder
			);

			string fileName;
			string arguments = "";

			int space = command.IndexOf(' ');

			if (space > 0)
			{
				fileName = command.Substring(0, space);
				arguments = command.Substring(space + 1);
			}
			else
			{
				fileName = command;
			}

			System.Diagnostics.Process.Start(
				new System.Diagnostics.ProcessStartInfo
				{
					FileName = fileName,
					Arguments = arguments,
					UseShellExecute = true
				});

		}
		else if (item.ActionFolderScript != "")
		{
			string folder = item.ActionFolderScript;

			if (folder == "\\" || folder == "")
			{
				folder = AppPath;
			}
			else
			{
				folder = Path.Combine(AppPath, folder);
			}

			Process.Start(
				new ProcessStartInfo
				{
					FileName = folder,
					UseShellExecute = true
				});
		}
		else if (item.ActionScriptSwitch != "")
		{
			SwitchMenu(item.ActionScriptSwitch);
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


	static AppSettings LoadSettings()
	{
		AppSettings settings = new();

		if (!File.Exists(ConfigFile))
		{
			string text =
@"[Settings]
Hotkey=Ctrl+Shift+Z
HideIcon=0
CurrentMenu=MenuMain.xml
FolderIcon=icons
FolderSnippets=snippets
AlwaysOnTop=false";

			File.WriteAllText(
				ConfigFile,
				text,
				System.Text.Encoding.UTF8
			);

			return settings;
		}

		foreach (string line in File.ReadAllLines(ConfigFile))
		{
			if (line.StartsWith("Hotkey="))
				settings.Hotkey = line["Hotkey=".Length..];

			else if (line.StartsWith("HideIcon="))
				settings.HideIcon = line["HideIcon=".Length..] == "1";

			else if (line.StartsWith("CurrentMenu="))
				settings.CurrentMenu = line["CurrentMenu=".Length..];

			else if (line.StartsWith("FolderIcon="))
				settings.FolderIcon = line["FolderIcon=".Length..];

			else if (line.StartsWith("FolderSnippets="))
				settings.FolderSnippets = line["FolderSnippets=".Length..];

			else if (line.StartsWith("AlwaysOnTop="))
				settings.AlwaysOnTop =
					line["AlwaysOnTop=".Length..]
					.Equals("true", StringComparison.OrdinalIgnoreCase);
		}

		return settings;
	}


	static void SaveSettings(AppSettings settings)
	{
		File.WriteAllText(
			ConfigFile,
			$@"[Settings]
	Hotkey={settings.Hotkey}
	HideIcon={(settings.HideIcon ? "1" : "0")}
	CurrentMenu={settings.CurrentMenu}
	FolderIcon={settings.FolderIcon}
	FolderSnippets={settings.FolderSnippets}
	AlwaysOnTop={(settings.AlwaysOnTop ? "true" : "false")}
	"
		);
	}


	static void SwitchMenu(string menuName)
	{
		AppSettings settings = LoadSettings();
		settings.CurrentMenu = menuName;
		SaveSettings(settings);
		Application.Restart();
	}


}