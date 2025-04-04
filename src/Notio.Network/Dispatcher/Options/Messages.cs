namespace Notio.Network.Dispatcher.Options;

internal static class Messages
{
    public static readonly string MissingControllerAttribute = "[PacketController] The controller {0} must be marked.";
    public static readonly string NoMethodsWithPacketCommand = "[WithHandler] No methods found with [PacketCommand] in {0}.";
    public static readonly string DuplicateCommandIds = "[WithHandler] Duplicate CommandIds in {0}: {1}";
    public static readonly string CommandIdAlreadyRegistered = "[WithHandler] Id {0} is already registered.";
    public static readonly string UnauthorizedCommandAccess = "[WithHandler] Unauthorized access to Id: {0} from {1}";
    public static readonly string CommandHandlerException = "[WithHandler] Exception in {0}.{1}: {2}";
    public static readonly string RegisteredCommandForHandler = "[WithHandler] Registered {0} for {1}";
}
