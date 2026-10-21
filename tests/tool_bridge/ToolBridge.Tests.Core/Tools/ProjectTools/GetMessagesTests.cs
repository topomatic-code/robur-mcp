using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Topomatic.FoundationClasses.Diagnostics;
using Topomatic.ToolBridge.DependencyInjection;
using Topomatic.ToolBridge.Exceptions;
using Topomatic.ToolBridge.Services;
using Topomatic.ToolBridge.Services.Models;
using ToolsUnderTest = Topomatic.ToolBridge.Tools.ProjectTools;

namespace ToolBridge.Tests.Core.Tools.ProjectTools
{
    internal sealed class GetMessagesTests
    {
        [Test]
        internal static void CheckOptionalAndCombinedFilters()
        {
            var logger = new TestLogger();
            logger.Entries.Add(new LogEntry { Source = "Road", Level = TaskLevel.Warning, Message = "First" });
            logger.Entries.Add(new LogEntry { Source = "Road", Level = TaskLevel.Error, Message = "Second" });
            logger.Entries.Add(new LogEntry { Source = "Drawing", Level = TaskLevel.Warning, Message = "Third" });
            var tools = CreateTools(logger);
            var all = Messages(tools, new Dictionary<string, object>());
            Test.Require(all.Count == 3 && (string)all[0]["source"] == "Road" &&
                (string)all[0]["level"] == "Warning" && (string)all[0]["message"] == "First", "All messages and their fields must be returned in provider order.");
            Test.Require(Filtered(tools, new JObject()).Count == 3, "Empty filter must return all messages.");
            Test.Require(Filtered(tools, new JObject { ["source"] = "Road" }).Count == 2, "Source is optional and filters independently.");
            Test.Require(Filtered(tools, new JObject { ["level"] = "Warning" }).Count == 2, "Level is optional and filters independently.");
            var combined = Filtered(tools, new JObject { ["source"] = "Road", ["level"] = "Error" });
            Test.Require(combined.Count == 1 && (string)combined[0]["message"] == "Second", "Both filters must match the same message.");
            Test.Require(Filtered(tools, new JObject { ["source"] = "road" }).Count == 0, "Filters must use exact matches.");
            Test.Require(Filtered(tools, new JObject { ["level"] = "Information" }).Count == 0, "A valid level without matches must return an empty array.");
            Test.Require(logger.Entries.Count == 3, "Filtering must not modify provider messages.");
            logger.Entries.Clear();
            Test.Require(Messages(tools, new Dictionary<string, object>()).Count == 0, "Each call must read current active messages.");
            logger.Entries.Add(new LogEntry { Source = "Road", Level = TaskLevel.Information, Message = "Info" });
            var information = Filtered(tools, new JObject { ["level"] = "Information" });
            Test.Require(information.Count == 1 && (string)information[0]["level"] == "Information", "Information must be supported and returned by its enum name.");
        }

        [Test]
        internal static void CheckInvalidFiltersAndProviderErrors()
        {
            var logger = new TestLogger();
            var tools = CreateTools(logger);
            Test.Throws<BadRequestException>(() => Messages(tools, new Dictionary<string, object> { ["filter"] = "invalid" }));
            foreach (var key in new[] { "source", "level" })
                Test.Throws<BadRequestException>(() => Filtered(tools, new JObject { [key] = 42 }));
            foreach (var level in new[] { "Unknown", "warning", "0", "" })
                Test.Throws<BadRequestException>(() => Filtered(tools, new JObject { ["level"] = level }));
            Test.Require(logger.Calls == 0, "Invalid filters must not reach the provider.");
            logger.Error = new PreconditionFailedException("Log is unavailable");
            Test.Require(ReferenceEquals(logger.Error, Test.Throws<PreconditionFailedException>(() => Messages(tools, new Dictionary<string, object>()))), "Provider errors must propagate unchanged.");
        }

        private static ToolsUnderTest CreateTools(TestLogger logger)
        {
            var container = new Container();
            container.RegisterSingleton<IRoburLogger>(() => logger);
            return new ToolsUnderTest { Container = container };
        }

        private static JArray Messages(ToolsUnderTest tools, Dictionary<string, object> args) =>
            (JArray)JObject.FromObject(tools.GetMessages(args))["result"]["messages"];

        private static JArray Filtered(ToolsUnderTest tools, JObject filter) =>
            Messages(tools, new Dictionary<string, object> { ["filter"] = filter });

        private sealed class TestLogger : IRoburLogger
        {
            internal readonly List<LogEntry> Entries = new List<LogEntry>();
            internal int Calls;
            internal Exception Error;

            public List<LogEntry> GetLog()
            {
                Calls++;
                if (Error != null) throw Error;
                return Entries;
            }
        }
    }
}
