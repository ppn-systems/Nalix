using Nalix.Common.Exceptions;
using Nalix.Network.Web.Utilities;
using Nalix.Network.Web.WebModule;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Web.Internal;

internal sealed class DummyWebModuleContainer : IWebModuleContainer
{
    public static readonly IWebModuleContainer Instance = new DummyWebModuleContainer();

    private DummyWebModuleContainer()
    {
    }

    public IComponentCollection<IWebModule> Modules => throw UnexpectedCall();

    public void Dispose()
    {
    }

    private InternalErrorException UnexpectedCall([CallerMemberName] string member = "")
        => SelfCheck.Failure($"Unexpected call to {nameof(DummyWebModuleContainer)}.{member}.");
}