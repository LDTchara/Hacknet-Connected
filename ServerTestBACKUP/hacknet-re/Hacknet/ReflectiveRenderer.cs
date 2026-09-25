using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hacknet.Gui;
using Hacknet.UIUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hacknet;

public static class ReflectiveRenderer
{
	private struct RenderableField
	{
		public string VariableName;

		public string RenderedValue;

		public bool IsTitle;

		public Type t;

		public int IndentLevel;

		public override string ToString()
		{
			return (IsTitle ? "TITLE" : VariableName) + ": " + RenderedValue;
		}
	}

	public static Action<Vector2, Type, string> PreRenderForObject;

	public static int GetEntryLineHeight()
	{
		return (int)(GuiData.ActiveFontConfig.tinyFontCharHeight * 2f + 2f);
	}

	public static void RenderObject(object o, Rectangle bounds, SpriteBatch spriteBatch, ScrollableSectionedPanel panel, Color TitleColor)
	{
		Type type = o.GetType();
		List<RenderableField> fields = GetRenderablesFromType(type, o);
		int entryLineHeight = GetEntryLineHeight();
		panel.PanelHeight = entryLineHeight;
		panel.NumberOfPanels = fields.Count;
		int pixelsPerIndentLevel = 20;
		panel.Draw(delegate(int i, Rectangle dest, SpriteBatch sb)
		{
			int num = fields[i].IndentLevel * pixelsPerIndentLevel;
			dest.X += num;
			dest.Width -= num;
			if (fields[i].IsTitle)
			{
				TextItem.doFontLabelToSize(dest, fields[i].RenderedValue, GuiData.font, TitleColor, doNotOversize: true, offsetToTopLeft: true);
				sb.Draw(destinationRectangle: new Rectangle(dest.X, dest.Y + dest.Height - 1, dest.Width, 1), texture: Utils.white, color: TitleColor);
			}
			else
			{
				string text = fields[i].VariableName + " :";
				Vector2 vector = GuiData.smallfont.MeasureString(text);
				sb.DrawString(GuiData.smallfont, text, new Vector2(dest.X, dest.Y), Color.Gray);
				string text2 = fields[i].RenderedValue;
				Vector2 vector2 = GuiData.smallfont.MeasureString(text2);
				if (vector2.X > (float)(dest.Width - 20) || vector2.Y > (float)dest.Height)
				{
					text2.Replace("\n", " ");
					text2 = text2.Substring(0, Math.Min(text2.Length, (int)((float)dest.Width / (GuiData.ActiveFontConfig.tinyFontCharHeight + 2f)))) + "...";
				}
				Vector2 vector3 = new Vector2((float)dest.X + vector.X + 6f, dest.Y);
				if (PreRenderForObject != null && sb.Name != "AltRTBatch")
				{
					PreRenderForObject(vector3, fields[i].t, fields[i].RenderedValue);
				}
				sb.DrawString(GuiData.smallfont, text2, vector3, Color.White);
			}
		}, spriteBatch, bounds);
		PreRenderForObject = null;
	}

	private static List<RenderableField> GetRenderablesFromType(Type type, object o, int indentLevel = 0)
	{
		List<RenderableField> list = new List<RenderableField>();
		if (ObjectSerializer.IsSimple(type))
		{
			list.Add(new RenderableField
			{
				VariableName = "Data",
				RenderedValue = o.ToString(),
				IsTitle = false,
				IndentLevel = indentLevel,
				t = type
			});
		}
		else if (ObjectSerializer.TypeInstanceOfInterface(type, typeof(ICollection)))
		{
			ICollection collection = o as ICollection;
			foreach (object item in collection)
			{
				list.AddRange(GetRenderablesFromType(item.GetType(), item, indentLevel + 1));
			}
		}
		else
		{
			list.Add(new RenderableField
			{
				IsTitle = true,
				RenderedValue = FilterTypeName(type.Name),
				IndentLevel = indentLevel
			});
			FieldInfo[] fields = type.GetFields();
			for (int i = 0; i < fields.Length; i++)
			{
				if (ObjectSerializer.IsSimple(fields[i].FieldType))
				{
					list.Add(new RenderableField
					{
						VariableName = fields[i].Name,
						RenderedValue = fields[i].GetValue(o).ToString(),
						IsTitle = false,
						IndentLevel = indentLevel,
						t = fields[i].FieldType
					});
				}
				else
				{
					list.AddRange(GetRenderablesFromType(fields[i].FieldType, fields[i].GetValue(o), indentLevel + 1));
				}
			}
			PropertyInfo[] properties = type.GetProperties();
			for (int i = 0; i < properties.Length; i++)
			{
				if (ObjectSerializer.IsSimple(properties[i].PropertyType))
				{
					list.Add(new RenderableField
					{
						VariableName = properties[i].Name,
						RenderedValue = properties[i].GetValue(o, null).ToString(),
						IsTitle = false,
						IndentLevel = indentLevel,
						t = properties[i].PropertyType
					});
				}
				else
				{
					list.AddRange(GetRenderablesFromType(properties[i].PropertyType, properties[i].GetValue(o, null), indentLevel + 1));
				}
			}
		}
		return list;
	}

	private static string FilterTypeName(string name)
	{
		return name.Replace("Hacknet.", "");
	}
}
