using System.Reflection;

namespace Topomatic.ToolBridge
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public static class ToolDomains
    {
        public const string CadView = "ВИДОВОЙ ЭКРАН";
        public const string Project = "ПРОЕКТ";
        public const string Culverts = "ТРУБЫ";
        public const string Drawing = "ЧЕРТЕЖ";
        public const string Landscaping = "ОЗЕЛЕНЕНИЕ";
        public const string Tlc = "TLC";
    }
}
