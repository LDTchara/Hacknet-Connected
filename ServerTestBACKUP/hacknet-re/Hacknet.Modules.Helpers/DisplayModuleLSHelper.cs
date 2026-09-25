using System;
using System.Collections.Generic;
using Hacknet.Gui;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet.Modules.Helpers;

internal class DisplayModuleLSHelper
{
	private struct LSItem
	{
		public Action Clicked;

		public int indent;

		public string DisplayName;

		public bool IsEmtyDisplay;
	}

	private const int BUTTON_HEIGHT = 20;

	private const int BUTTON_MARGIN = 2;

	private const int INDENT = 30;

	private ScrollableSectionedPanel panel;

	public DisplayModuleLSHelper()
	{
		panel = new ScrollableSectionedPanel(24, GuiData.spriteBatch.GraphicsDevice);
	}

	public void DrawUI(Rectangle dest, OS os)
	{
		int ButtonHeight = (int)(GuiData.ActiveFontConfig.tinyFontCharHeight + 10f);
		if (panel.PanelHeight != ButtonHeight + 4)
		{
			panel = new ScrollableSectionedPanel(ButtonHeight + 4, GuiData.spriteBatch.GraphicsDevice);
		}
		Folder f = ((os.connectedComp == null) ? os.thisComputer.files.root : os.connectedComp.files.root);
		List<LSItem> items = BuildDirectoryDrawList(f, 0, 0, os);
		panel.NumberOfPanels = items.Count;
		int width = dest.Width - 25;
		Action<int, Rectangle, SpriteBatch> drawSection = delegate(int index, Rectangle bounds, SpriteBatch sb)
		{
			LSItem lSItem = items[index];
			if (lSItem.IsEmtyDisplay)
			{
				TextItem.doFontLabel(new Vector2(bounds.X + 5 + lSItem.indent, bounds.Y + 2), "-" + LocaleTerms.Loc("Empty") + "-", GuiData.tinyfont, null, width, ButtonHeight);
			}
			else if (Button.doButton(300000 + index, bounds.X + 5 + lSItem.indent, bounds.Y + 2, width - lSItem.indent, ButtonHeight, lSItem.DisplayName, null))
			{
				lSItem.Clicked();
			}
		};
		Button.DisableIfAnotherIsActive = true;
		panel.Draw(drawSection, GuiData.spriteBatch, dest);
		Button.DisableIfAnotherIsActive = false;
	}

	private List<LSItem> BuildDirectoryDrawList(Folder f, int recItteration, int indentOffset, OS os)
	{
		List<LSItem> list = new List<LSItem>();
		double commandSeperationDelay = 0.019;
		for (int i = 0; i < f.folders.Count; i++)
		{
			int myIndex = i;
			LSItem lSItem = default(LSItem);
			lSItem.DisplayName = "/" + f.folders[i].name;
			lSItem.Clicked = delegate
			{
				int num2 = 0;
				for (int k = 0; k < os.navigationPath.Count - recItteration; k++)
				{
					Action action3 = delegate
					{
						os.runCommand("cd ..");
					};
					if (num2 > 0)
					{
						os.delayer.Post(ActionDelayer.Wait((double)num2 * commandSeperationDelay), action3);
					}
					else
					{
						action3();
					}
					num2++;
				}
				Action action4 = delegate
				{
					os.runCommand("cd " + f.folders[myIndex].name);
				};
				if (num2 > 0)
				{
					os.delayer.Post(ActionDelayer.Wait((double)num2 * commandSeperationDelay), action4);
				}
				else
				{
					action4();
				}
			};
			lSItem.indent = indentOffset;
			LSItem item = lSItem;
			list.Add(item);
			indentOffset += 30;
			if (os.navigationPath.Count - 1 >= recItteration && os.navigationPath[recItteration] == i)
			{
				list.AddRange(BuildDirectoryDrawList(f.folders[i], recItteration + 1, indentOffset, os));
			}
			indentOffset -= 30;
		}
		for (int i = 0; i < f.files.Count; i++)
		{
			int myIndex = i;
			LSItem lSItem2 = default(LSItem);
			lSItem2.DisplayName = f.files[i].name;
			lSItem2.Clicked = delegate
			{
				int num = 0;
				for (int j = 0; j < os.navigationPath.Count - recItteration; j++)
				{
					Action action = delegate
					{
						os.runCommand("cd ..");
					};
					if (num > 0)
					{
						os.delayer.Post(ActionDelayer.Wait((double)num * commandSeperationDelay), action);
					}
					else
					{
						action();
					}
					num++;
				}
				Action action2 = delegate
				{
					os.runCommand("cat " + f.files[myIndex].name);
				};
				if (num > 0)
				{
					os.delayer.Post(ActionDelayer.Wait((double)num * commandSeperationDelay), action2);
				}
				else
				{
					action2();
				}
			};
			lSItem2.indent = indentOffset;
			LSItem item2 = lSItem2;
			list.Add(item2);
		}
		if (f.folders.Count == 0 && f.files.Count == 0)
		{
			LSItem lSItem3 = default(LSItem);
			lSItem3.IsEmtyDisplay = true;
			lSItem3.indent = indentOffset;
			LSItem item3 = lSItem3;
			list.Add(item3);
		}
		return list;
	}
}
