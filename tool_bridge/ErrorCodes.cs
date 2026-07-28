using System.Reflection;

namespace Topomatic.ToolBridge
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public static class ErrorCodes
    {
        public const string InternalError = "internal_error";
        public const string BadRequest = "bad_request";
        public const string ToolNotFound = "tool_not_found";
        public const string ToolExecutionFailed = "tool_execution_failed";
        public const string PreconditionFailed = "precondition_failed";
    }
}
