using Notio.Common.Models;
using System;
using System.Reflection;

namespace Notio.Network.Handlers.Base;

internal record PacketHandlerInfo(
    PacketControllerAttribute Controller,
    MethodInfo Method,
    Authoritys RequiredAuthority,
    Type ControllerType,
    bool IsAsync
);