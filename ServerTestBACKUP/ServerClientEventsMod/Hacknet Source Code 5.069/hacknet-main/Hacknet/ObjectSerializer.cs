using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using Microsoft.Xna.Framework;

namespace Hacknet;

public static class ObjectSerializer
{
	public static string SerializeObject(object o)
	{
		return SerializeObject(o, preventOuterTag: false);
	}

	private static string SerializeObject(object o, bool preventOuterTag = false)
	{
		if (o == null)
		{
			return "";
		}
		Type type = o.GetType();
		string tagNameForType = GetTagNameForType(type);
		StringBuilder stringBuilder = new StringBuilder();
		if (!preventOuterTag)
		{
			stringBuilder.Append("<" + tagNameForType + ">");
		}
		if (TypeInstanceOfInterface(type, typeof(ICollection)))
		{
			stringBuilder.Append(SerializeCollection((ICollection)o));
		}
		else
		{
			FieldInfo[] fields = type.GetFields();
			for (int i = 0; i < fields.Length; i++)
			{
				string name = fields[i].Name;
				object value = fields[i].GetValue(o);
				if (value != null)
				{
					Type fieldType = fields[i].FieldType;
					string text = null;
					if (TypeInstanceOfInterface(fieldType, typeof(IFormattable)))
					{
						IFormattable formattable = value as IFormattable;
						text = formattable.ToString("", CultureInfo.InvariantCulture);
					}
					else
					{
						text = value.ToString();
					}
					if (fieldType == typeof(Color))
					{
						text = Utils.convertColorToParseableString((Color)value);
					}
					if (!IsSimple(value.GetType()))
					{
						text = "\n" + SerializeObject(value, preventOuterTag: true);
					}
					stringBuilder.Append(GetSerializedStringForPrimative(name, text));
				}
			}
		}
		if (!preventOuterTag)
		{
			stringBuilder.Append("\n</" + tagNameForType + ">");
		}
		else
		{
			stringBuilder.Append("\n");
		}
		return stringBuilder.ToString();
	}

	public static string GetTagNameForType(Type t)
	{
		string text = t.Name;
		if (text.StartsWith("Hacknet."))
		{
			text = text.Substring("Hacknet.".Length);
		}
		return text.Replace("`", "_");
	}

	public static bool TypeInstanceOfInterface(Type objType, Type interfaceType)
	{
		return interfaceType.IsAssignableFrom(objType);
	}

	private static string GetSerializedStringForPrimative(string tagName, string tagValue)
	{
		return string.Format(CultureInfo.InvariantCulture, "\n\t<{0}>{1}</{0}>", new object[2] { tagName, tagValue });
	}

	private static string SerializeCollection(ICollection o)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("\n");
		foreach (object item in o)
		{
			string text = null;
			text = ((!IsSimple(item.GetType())) ? SerializeObject(item) : GetSerializedStringForPrimative(GetTagNameForType(item.GetType()), item.ToString()));
			stringBuilder.Append(string.Format(CultureInfo.InvariantCulture, "\n\t{0}", new object[1] { text }));
		}
		return stringBuilder.ToString();
	}

	public static bool IsSimple(Type type)
	{
		if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
		{
			return IsSimple(type.GetGenericArguments()[0]);
		}
		return type.IsPrimitive || type.IsEnum || type.Equals(typeof(string)) || type.Equals(typeof(decimal)) || type.Equals(typeof(string)) || type.Equals(typeof(DateTime)) || type.Equals(typeof(TimeSpan));
	}

	public static object DeserializeObject(Stream s, Type t)
	{
		using XmlReader rdr = XmlReader.Create(s);
		return DeserializeObject(rdr, t);
	}

	public static object DeserializeObject(XmlReader rdr, Type t)
	{
		if (IsSimple(t))
		{
			XmlNamespaceManager namespaceResolver = new XmlNamespaceManager(new NameTable());
			return rdr.ReadElementContentAs(t, namespaceResolver);
		}
		if (TypeInstanceOfInterface(t, typeof(ICollection)))
		{
			return DeserializeCollection(rdr, t);
		}
		return DeserializeXMLObject(rdr, t);
	}

	private static object DeserializeXMLObject(XmlReader rdr, Type t, string overrideExpectedEndTag = null)
	{
		object obj = CreateObjectOfType(t);
		FieldInfo[] fields = t.GetFields();
		XmlNamespaceManager resolver = new XmlNamespaceManager(new NameTable());
		string text = GetTagNameForType(t);
		if (overrideExpectedEndTag != null)
		{
			text = overrideExpectedEndTag;
		}
		while (!rdr.EOF)
		{
			if (!string.IsNullOrWhiteSpace(rdr.Name) && rdr.IsStartElement())
			{
				for (int i = 0; i < fields.Length; i++)
				{
					if (!(fields[i].Name == rdr.Name))
					{
						continue;
					}
					rdr.MoveToContent();
					object value = null;
					if (fields[i].FieldType == typeof(Color))
					{
						value = Utils.convertStringToColor(rdr.ReadElementContentAsString());
					}
					else if (TypeInstanceOfInterface(fields[i].FieldType, typeof(ICollection)))
					{
						value = DeserializeCollection(rdr, fields[i].FieldType, fields[i].Name);
					}
					else if (!IsSimple(fields[i].FieldType))
					{
						value = DeserializeXMLObject(rdr, fields[i].FieldType, fields[i].Name);
					}
					else
					{
						try
						{
							value = ReadElementContentWithType(rdr, fields[i].FieldType, resolver);
						}
						catch (FormatException)
						{
						}
					}
					fields[i].SetValue(obj, value);
					break;
				}
			}
			rdr.Read();
			if (rdr.Name == text && !rdr.IsStartElement())
			{
				break;
			}
			if (rdr.EOF)
			{
				throw new FormatException();
			}
		}
		return obj;
	}

	private static object ReadElementContentWithType(XmlReader reader, Type type, IXmlNamespaceResolver resolver)
	{
		if (type == typeof(DateTime))
		{
			return DateTime.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);
		}
		if (type == typeof(bool))
		{
			return reader.ReadElementContentAsString().ToLower() == "true";
		}
		if (type == typeof(TimeSpan))
		{
			return TimeSpan.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);
		}
		if (type.IsEnum)
		{
			string value = reader.ReadElementContentAsString();
			return Enum.Parse(type, value);
		}
		return reader.ReadElementContentAs(type, resolver);
	}

	private static object CreateObjectOfType(Type targetType)
	{
		if (Type.GetTypeCode(targetType) == TypeCode.String)
		{
			return string.Empty;
		}
		Type[] types = new Type[0];
		ConstructorInfo constructor = targetType.GetConstructor(types);
		object obj = null;
		if (constructor == null)
		{
			if (!targetType.BaseType.UnderlyingSystemType.FullName.Contains("Enum"))
			{
				throw new ArgumentException("Unable to instantiate type: " + targetType.AssemblyQualifiedName + " - Constructor not found");
			}
			obj = Activator.CreateInstance(targetType);
		}
		else
		{
			obj = constructor.Invoke(null);
		}
		if (obj == null)
		{
			throw new ArgumentException("Unable to instantiate type: " + targetType.AssemblyQualifiedName + " - Unknown Error");
		}
		return obj;
	}

	private static object DeserializeCollection(XmlReader rdr, Type t, string tagName = "list")
	{
		object obj = CreateObjectOfType(t);
		if (!(obj is IList list))
		{
			throw new NotSupportedException();
		}
		bool flag = rdr.Name.ToLower().StartsWith(tagName.ToLower()) && rdr.IsStartElement();
		while (!flag)
		{
			rdr.Read();
			flag = rdr.Name.ToLower().StartsWith(tagName.ToLower()) && rdr.IsStartElement();
			if (rdr.EOF)
			{
				throw new FormatException();
			}
		}
		bool flag2 = false;
		do
		{
			rdr.Read();
			if (rdr.IsStartElement())
			{
				string name = rdr.Name;
				Type typeForName = GetTypeForName(name);
				object value = DeserializeObject(rdr, typeForName);
				list.Add(value);
			}
			if (rdr.Name.ToLower().StartsWith(tagName.ToLower()) && !rdr.IsStartElement())
			{
				flag2 = true;
			}
			if (rdr.EOF)
			{
				throw new FormatException();
			}
		}
		while (!flag2);
		return list;
	}

	public static Type GetTypeForName(string name)
	{
		if (string.IsNullOrWhiteSpace(name) || name.ToLower() == "none")
		{
			return null;
		}
		Type type = Type.GetType(name, throwOnError: false, ignoreCase: true);
		if (type == null)
		{
			type = Type.GetType("Hacknet." + name, throwOnError: false, ignoreCase: true);
		}
		if (type == null)
		{
			type = Type.GetType("System." + name, throwOnError: false, ignoreCase: true);
		}
		return type;
	}

	public static object DeepCopy(object input)
	{
		string s = SerializeObject(input);
		return DeserializeObject(Utils.GenerateStreamFromString(s), input.GetType());
	}

	public static object GetValueFromObject(object o, string FieldName)
	{
		Type type = o.GetType();
		PropertyInfo[] properties = type.GetProperties();
		for (int i = 0; i < properties.Length; i++)
		{
			if (properties[i].Name.ToLower() == FieldName.ToLower())
			{
				return properties[i].GetValue(o, null);
			}
		}
		FieldInfo[] fields = type.GetFields();
		for (int i = 0; i < fields.Length; i++)
		{
			if (fields[i].Name.ToLower() == FieldName.ToLower())
			{
				return fields[i].GetValue(o);
			}
		}
		return null;
	}
}
