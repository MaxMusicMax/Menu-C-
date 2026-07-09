using System;
using System.Collections.Generic;
using System.Xml;
using System.Windows.Forms;
using System.IO;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

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
	public string ActionFontSize { get; set; } = "";
	public string ActionIconSize { get; set; } = "";
	public List<MenuItem> Children { get; set; } = new();
}

class AppSettings
{
	public string Hotkey { get; set; } = "Ctrl+Shift+X";
	public bool HideIcon { get; set; } = false;
	public string CurrentMenu { get; set; } = "MenuMain.xml";
	public string FolderIcon { get; set; } = "icons";
	public string FolderSnippets { get; set; } = "snippets";
	public bool AlwaysOnTop { get; set; } = false;
	public int FontSize { get; set; } = 12;
	public int IconSize { get; set; } = 24;
}

class Program
{
	const int HOTKEY_ID = 1;
	const int WM_HOTKEY = 0x0312;

	[DllImport("user32.dll")]
	static extern bool RegisterHotKey(
		IntPtr hWnd,
		int id,
		uint fsModifiers,
		uint vk
	);

	[DllImport("user32.dll")]
	static extern bool UnregisterHotKey(
		IntPtr hWnd,
		int id
	);

	[DllImport("user32.dll")]
	static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

	const byte VK_CONTROL = 0x11;
	const byte VK_V = 0x56;
	const uint KEYEVENTF_KEYUP = 0x0002;

	static NotifyIcon tray = null!;
	static ContextMenuStrip menu = null!;
	static Icon? trayIcon;
	static ChangeHotkeyForm? hotkeyForm;
	static List<MenuItem> menuItems = new();

	static string AppPath = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
	static string ConfigFile = Path.Combine(AppPath, "config.ini");

	class MyContextMenuStrip : ContextMenuStrip
	{
		protected override CreateParams CreateParams
		{
			get
			{
				CreateParams cp = base.CreateParams;
				cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW
				return cp;
			}
		}
	}

	class MyToolStripDropDownMenu : ToolStripDropDownMenu
	{
		protected override CreateParams CreateParams
		{
			get
			{
				CreateParams cp = base.CreateParams;
				cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW
				return cp;
			}
		}
	}

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

		menu = new MyContextMenuStrip();

		menu.ImageScalingSize = new Size(
			settings.IconSize,
			settings.IconSize
		);

		menu.Font = new Font(
			menu.Font.FontFamily,
			settings.FontSize
		);

		menu.ImageScalingSize = new Size(18, 18);

		BuildMenu(menu.Items, menuItems);

		tray = new NotifyIcon();
		
		string iconFile = Path.Combine(
			AppPath,
			"CS.ico"
		);

		if (File.Exists(iconFile))
		{
			using (Icon tempIcon = new Icon(iconFile))
			{
				trayIcon = new Icon(
					tempIcon,
					new Size(32, 32)
				);
			}

			tray.Icon = trayIcon;
		}
		else
		{
			tray.Icon = System.Drawing.SystemIcons.Application;
		}

		tray.ContextMenuStrip = menu;
		tray.Visible = !settings.HideIcon;

		RegisterHotKey(
			ProgramMessageWindow.Handle,
			HOTKEY_ID,
			ParseModifiers(settings.Hotkey),
			ParseKey(settings.Hotkey)
		);

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
			item.ActionFontSize = GetAttribute(child, "action_font_size");
			item.ActionIconSize = GetAttribute(child, "action_icon_size");

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

			ToolStripMenuItem menuItem = new ToolStripMenuItem(item.ItemName);
			
			if (item.Icon != "")
			{
				menuItem.Image = LoadIcon(item.Icon);
			}
			else if (item.ActionOpenFolder != "")
			{
				string file = Environment.ExpandEnvironmentVariables(
					item.ActionOpenFolder
				);

				if (File.Exists(file))
				{
					menuItem.Image = LoadFileIcon(file);
				}
			}

			menuItem.DropDown = new MyToolStripDropDownMenu();

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
			menuItem.Font = menu.Font;
			collection.Add(menuItem);
		}
	}

	static void SetMenuFont(
	ToolStripItemCollection items,
	Font font)
	{
		foreach (ToolStripItem item in items)
		{
			item.Font = font;

			if (item is ToolStripMenuItem menuItem)
			{
				SetMenuFont(
					menuItem.DropDownItems,
					font
				);
			}
		}
	}

	static void ReloadMenu()
	{
		AppSettings settings = LoadSettings();

		string jsonFile = Path.Combine(
			AppPath,
			Path.ChangeExtension(settings.CurrentMenu, ".json")
		);

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

		menu.Items.Clear();

		BuildMenu(
			menu.Items,
			menuItems
		);
	}

	static void SetMenuIconSize(int size)
	{
		menu.ImageScalingSize = new Size(size, size);

		SetDropDownIconSize(
			menu.Items,
			size
		);
	}

	static void SetDropDownIconSize(
	ToolStripItemCollection items,
	int size)
	{
		foreach (ToolStripItem item in items)
		{
			item.ImageScaling =
				ToolStripItemImageScaling.SizeToFit;

			if (item is ToolStripMenuItem menuItem)
			{
				menuItem.DropDown.ImageScalingSize =
					new Size(size, size);

				SetDropDownIconSize(
					menuItem.DropDownItems,
					size
				);
			}
		}
	}

	static void ExecuteAction(MenuItem item)
	{
		
		if (item.ActionIconSize != "")
		{
			AppSettings settings = LoadSettings();

			settings.IconSize =
				int.Parse(item.ActionIconSize);

			SaveSettings(settings);

			SetMenuIconSize(
				settings.IconSize
			);

			return;
		}

		if (item.ActionFontSize != "")
		{
			AppSettings settings = LoadSettings();

			settings.FontSize = int.Parse(
				item.ActionFontSize
			);

			SaveSettings(settings);

			menu.Font = new Font(
				menu.Font.FontFamily,
				settings.FontSize
			);

			SetMenuFont(
				menu.Items,
				menu.Font
			);

			return;
		}
		
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

			IntPtr hwnd = GetForegroundWindow();
			Clipboard.SetText(text);
			Thread.Sleep(100);
			SetForegroundWindow(hwnd);
			Thread.Sleep(100);
			keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
			keybd_event(VK_V, 0, 0, UIntPtr.Zero);
			keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
			keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

		}

		else if (item.ActionOpenFolder != "")
		{
			string command = Environment.ExpandEnvironmentVariables(
				item.ActionOpenFolder
			);

			if (File.Exists(command) || Directory.Exists(command))
			{
				Process.Start(
					new ProcessStartInfo
					{
						FileName = command,
						UseShellExecute = true
					});
			}
			else
			{
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

				Process.Start(
					new ProcessStartInfo
					{
						FileName = fileName,
						Arguments = arguments,
						UseShellExecute = true
					});
			}
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
			
			if (item.ActionSettingsAhk == "DeleteJson")
			{
				AppSettings settings = LoadSettings();
				string jsonFile = Path.Combine(
					AppPath,
					Path.ChangeExtension(settings.CurrentMenu, ".json")
				);

				if (File.Exists(jsonFile))
				{
					File.Delete(jsonFile);
				}

				ReloadMenu();
			}

			else if (item.ActionSettingsAhk == "IconShow")
			{
				AppSettings settings = LoadSettings();
				settings.HideIcon = !settings.HideIcon;
				SaveSettings(settings);
				tray.Visible = !settings.HideIcon;
			}

			else if (item.ActionSettingsAhk == "CloseProgram")
			{
				tray.Visible = false;
				Application.Exit();
			}

			else if (item.ActionSettingsAhk == "RebootProgram")
			{
				tray.Visible = false;
				Application.Restart();
			}

			else if (item.ActionSettingsAhk == "ChangeHotkeyGui")
			{
				ChangeHotkeyForm form = new ChangeHotkeyForm();
				form.ShowDialog();
			}

			else
			{
				MessageBox.Show(
					"Setting: " + item.ActionSettingsAhk);
			}
		}
	}

	[DllImport("shell32.dll", CharSet = CharSet.Auto)]
	static extern uint ExtractIconEx(
		string szFileName,
		int nIconIndex,
		IntPtr[] phiconLarge,
		IntPtr[] phiconSmall,
		uint nIcons
	);

	[DllImport("user32.dll")]
	static extern bool DestroyIcon(
		IntPtr hIcon
	);

	static System.Drawing.Image? LoadIcon(string icon)
	{
		try
		{
			string[] parts = icon.Split(':');

			string file = parts[0];
			int index = 0;

			if (parts.Length > 1)
				index = int.Parse(parts[1]) - 1;

			AppSettings settings = LoadSettings();

			file = Path.Combine(
				AppPath,
				settings.FolderIcon,
				file
			);

			if (!File.Exists(file))
				return null;

			IntPtr[] large = new IntPtr[1];
			IntPtr[] small = new IntPtr[1];

			uint result = ExtractIconEx(
				file,
				index,
				large,
				small,
				1
			);

			if (result == 0)
				return null;

			System.Drawing.Icon ico =
				System.Drawing.Icon.FromHandle(
					small[0] != IntPtr.Zero
					? small[0]
					: large[0]
				);

			System.Drawing.Image image =
				ico.ToBitmap();


			DestroyIcon(
				small[0] != IntPtr.Zero
				? small[0]
				: large[0]
			);

			return image;
		}
		catch
		{
			return null;
		}
	}

	static System.Drawing.Image? LoadFileIcon(string file)
	{
		try
		{
			using System.Drawing.Icon? icon =
				System.Drawing.Icon.ExtractAssociatedIcon(file);

			if (icon == null)
				return null;

			return icon.ToBitmap();
		}
		catch
		{
			return null;
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

		foreach (string line in File.ReadAllLines(ConfigFile).Select(x => x.Trim()))
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
			
			else if (line.StartsWith("FontSize="))
			settings.FontSize =
				int.Parse(line["FontSize=".Length..]);

			else if (line.StartsWith("IconSize="))
			settings.IconSize =
				int.Parse(line["IconSize=".Length..]);
			
		}

		return settings;
	}

	static uint ParseModifiers(string hotkey)
	{
		uint modifiers = 0;

		if (hotkey.Contains("Ctrl"))
			modifiers |= 0x0002;

		if (hotkey.Contains("Shift"))
			modifiers |= 0x0004;

		if (hotkey.Contains("Alt"))
			modifiers |= 0x0001;

		if (hotkey.Contains("Win"))
			modifiers |= 0x0008;

		return modifiers;
	}

	static uint ParseKey(string hotkey)
	{
		string key = hotkey
			.Split('+')
			.Last();

		if (key.Length == 1)
		{
			return (uint)char.ToUpper(key[0]);
		}

		if (key.StartsWith("F"))
		{
			return (uint)Keys.F1 +
				(uint.Parse(key.Substring(1))) - 1;
		}

		return (uint)Keys.None;
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
FontSize={settings.FontSize}
IconSize={settings.IconSize}
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

	static void CheckJson()
	{
		AppSettings settings = LoadSettings();

		string xmlFile = Path.Combine(
			AppPath,
			settings.CurrentMenu
		);

		string jsonFile = Path.ChangeExtension(
			xmlFile,
			".json"
		);

		if (!File.Exists(jsonFile))
		{
			LoadMenuCache(settings.CurrentMenu);
		}
	}

	static NativeWindow ProgramMessageWindow = new HotkeyWindow();

	class HotkeyWindow : NativeWindow
	{
		public HotkeyWindow()
		{
			CreateParams cp = new CreateParams();

			cp.ExStyle = 0x00000080; // WS_EX_TOOLWINDOW
			cp.Style = unchecked((int)0x80000000); // WS_POPUP

			CreateHandle(cp);
		}

		protected override void WndProc(ref Message m)
		{
			if (m.Msg == WM_HOTKEY)
			{
				CheckJson();

				menu.Show(Cursor.Position);
			}

			base.WndProc(ref m);
		}
	}

	class ChangeHotkeyForm : Form
	{
		TextBox box;
		Button save;

		public ChangeHotkeyForm()
		{
			Text = "Горячие клавиши";
			Width = 300;
			Height = 150;
			StartPosition = FormStartPosition.CenterScreen;

			AppSettings settings = LoadSettings();

			Label label = new Label();
			label.Text = "Новая комбинация:";
			label.Left = 10;
			label.Top = 15;
			label.Width = 200;

			box = new TextBox();
			box.Left = 10;
			box.Top = 40;
			box.Width = 250;
			box.Text = settings.Hotkey;
			box.ReadOnly = true;

			box.KeyDown += (s, e) =>
			{
				List<string> keys = new();

				if (e.Control)
					keys.Add("Ctrl");

				if (e.Shift)
					keys.Add("Shift");

				if (e.Alt)
					keys.Add("Alt");


				if (e.KeyCode >= Keys.A &&
					e.KeyCode <= Keys.Z)
				{
					keys.Add(e.KeyCode.ToString());
				}
				else if (e.KeyCode >= Keys.F1 &&
					e.KeyCode <= Keys.F12)
				{
					keys.Add(e.KeyCode.ToString());
				}


				box.Text = string.Join("+", keys);
			};

			save = new Button();
			save.Text = "Сохранить";
			save.Left = 10;
			save.Top = 75;
			save.Width = 100;

			save.Click += (s, e) =>
			{
				AppSettings settings = LoadSettings();

				settings.Hotkey = box.Text;

				SaveSettings(settings);

				UnregisterHotKey(
					ProgramMessageWindow.Handle,
					HOTKEY_ID
				);

				RegisterHotKey(
					ProgramMessageWindow.Handle,
					HOTKEY_ID,
					ParseModifiers(settings.Hotkey),
					ParseKey(settings.Hotkey)
				);


				Close();
			};

			Controls.Add(label);
			Controls.Add(box);
			Controls.Add(save);
		}
	}

}