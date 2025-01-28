using Notio.Common.Exceptions;
using Notio.Lite;
using Notio.Web.Utilities;
using Notio.Web.WebModule;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Notio.Web.Internal;

internal sealed class DummyWebModuleContainer : IWebModuleContainer
{
    public static readonly IWebModuleContainer Instance = new DummyWebModuleContainer();

    private DummyWebModuleContainer()
    {
    }

    public IComponentCollection<IWebModule> Modules => throw UnexpectedCall();

    public ConcurrentDictionary<object, object> SharedItems => throw UnexpectedCall();

    public void Dispose()
    {
    }

    private InternalErrorException UnexpectedCall([CallerMemberName] string member = "")
    {
        return SelfCheck.Failure($"Unexpected call to {nameof(DummyWebModuleContainer)}.{member}.");
    }
}