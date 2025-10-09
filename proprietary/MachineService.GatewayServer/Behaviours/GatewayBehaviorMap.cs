// Copyright (c) 2025 Duplicati Inc. All rights reserved.

namespace MachineService.GatewayServer.Behaviours;

/// <summary>
/// Mapping of message types to their corresponding behavior classes for the gateway server.
/// Behaviors should be added here to be recognized by the gateway server.
/// </summary>
public static class GatewayBehaviorMap
{
    /// <summary>
    /// Dictionary mapping command strings to behavior types.
    /// </summary>
    public static Dictionary<string, Type> Behaviors => new Dictionary<string, Type>
    {
        {AuthGatewayBehavior.Command, typeof(AuthGatewayBehavior)},
        {AuthPortalBehavior.Command, typeof(AuthPortalBehavior)},
        {CommandBehavior.Command, typeof(CommandBehavior)},
        {ListBehavior.Command, typeof(ListBehavior)},
        {PingBehavior.Command, typeof(PingBehavior)},
        {ProxyBehavior.Command, typeof(ProxyBehavior)},
    };
}
