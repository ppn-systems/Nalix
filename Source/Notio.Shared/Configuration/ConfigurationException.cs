using System;

namespace Notio.Shared.Configuration;

public class ConfiguredException(string message) : Exception(message)
{
}