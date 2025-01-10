using System;

namespace Notio.Shared.Configuration;

public class ConfigurationException(string message) : Exception(message)
{
}