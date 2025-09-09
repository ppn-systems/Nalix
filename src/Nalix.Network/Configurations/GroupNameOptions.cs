using Nalix.Shared.Configuration;
using Nalix.Shared.Configuration.Binding;
using System;

namespace Nalix.Network.Configurations;

internal sealed class GroupNameOptions : ConfigurationLoader
{
    public String Accept => $"net/accept/{ConfigurationManager.Instance.Get<NetworkSocketOptions>().Port}";
}
