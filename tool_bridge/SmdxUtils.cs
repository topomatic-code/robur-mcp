using System;
using System.Linq;
using System.Reflection;
using Topomatic.Cad.Foundation;
using Topomatic.Visualization;

namespace Topomatic.ToolBridge
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public static class SmdxUtils
    {
        public static object CreateImElementObj(ImElement element)
        {
            var bounds = element.GetModel()?.GetBounds() ?? BoundingBox3D.Empty;
            return new
            {
                name = element.Name,
                properties = CreatePropsArray(element.GetAllProperties()),
                bounds = new
                {
                    left = bounds.Left,
                    right = bounds.Right,
                    top = bounds.Top,
                    bottom = bounds.Bottom,
                    near = bounds.Near,
                    far = bounds.Far
                }
            };
        }

        public static object CreatePropsArray(ImProperties props)
        {
            var propsArray = new object[props.Count];
            for (int i = 0; i < props.Count; i++)
            {
                var prop = props[i];
                var nameParts = prop.Name.Split('|');
                string group;
                if (nameParts.Length > 1)
                {
                    var groupParts = new string[nameParts.Length - 1];
                    Array.Copy(nameParts, groupParts, groupParts.Length);
                    group = string.Join("|", groupParts);
                }
                else
                {
                    group = "";
                }
                propsArray[i] = new
                {
                    name = nameParts.Last(),
                    group,
                    tag = prop.Tag,
                    value = prop.StringValue,
                    units = prop.Info?.Units?.Title ?? ""
                };
            }
            return propsArray;
        }
    }
}
