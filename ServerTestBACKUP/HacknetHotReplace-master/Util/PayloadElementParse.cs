using HarmonyLib;
using Pathfinder.Util.XML;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Pathfinder.Util;

namespace HacknetHotReplace.Util
{
    public static class PayloadElementParse
    {
        public static XElement ToXElement(this object obj)
        {
            return ToXElement(obj, "Payload");
        }

        public static XElement ToXElement(object obj, string tagName)
        {
            var xElement = new XElement(tagName);
            if (obj.GetType().IsValueType || obj is string)
            {
                xElement.Value = obj.ToString();
                return xElement;
            }

            if (obj.GetType() == typeof(ElementInfo))
            {
                xElement.Add(XElement.Parse(obj.ToString()));
                return xElement;
            }

            var properties = obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (var property in properties)
            {
                if (property.PropertyType.IsValueType || property.PropertyType == typeof(string))
                {
                    var childEl = new XElement(property.Name)
                    {
                        Value = property.GetValue(obj).ToString()
                    };
                    xElement.Add(childEl);
                    continue;
                }

                if (property.PropertyType.IsClass)
                {
                    xElement.Add(ToXElement(property.GetValue(obj), property.Name));
                    continue;
                }
            }

            return xElement;
        }

        public static T ToObject<T>(this ElementInfo info) where T : class, new()
        {
            return (T)ToObject(info, 0, typeof(T));
        }


        private static object ToObject(ElementInfo info, int level, Type objType)
        {
            if (level == 0 && info.Name != "Payload")
            {
                throw new InvalidDataException("Command Arg Must in Payload Tag");
            }

            if (objType == typeof(ElementInfo))
            {
                return info;
            }

            object obj = null;
            try
            {
                var constructorInfo = AccessTools.Constructor(objType);
                obj = constructorInfo.Invoke(Array.Empty<object>());
            }
            catch (Exception e)
            {
                throw new InvalidCastException($"Instantiation type:{objType} failed in parse command payload", e);
            }

            var properties = objType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (var property in properties)
            {
                var propertyFullname = $"{objType.FullName}.{property.Name}";
                var curEl = info.Children.FirstOrDefault(el => el.Name == property.Name);
                if (curEl == null)
                {
                    continue;
                }

                if (property.PropertyType.IsValueType || property.PropertyType == typeof(string))
                {
                    try
                    {
                        property.SetValue(obj, Convert.ChangeType(curEl.Content, property.PropertyType));
                    }
                    catch (Exception e)
                    {
                        throw new InvalidCastException($"Parse Property:{propertyFullname} Failed, PropertyType:{property.PropertyType}, data:{curEl.Content}", e);
                    }
                    continue;
                }

                if (property.PropertyType.IsClass)
                {
                    property.SetValue(obj, ToObject(curEl, level + 1, property.PropertyType));
                    continue;
                }

                throw new InvalidCastException($"Public Property:{propertyFullname} not value type and class, can't parse");
            }

            return obj;
        }
    }
}
