using MachineService.Common.Services;

namespace MachineService.Server.Utility;

/// <summary>
/// Helper methods for forwarding proxy messages
/// </summary>
public static class ProxyForwardHelper
{
    /// <summary>
    /// Forwards a proxy response message to all relevant gateway servers
    /// </summary>
    /// <param name="gatewayConnectionList">The gateway connection list</param>
    /// <param name="instanceId">The instance id</param>
    /// <param name="message">The message to forward</param>
    /// <param name="organizationId">The organization id</param>
    /// <param name="clientId">The client id</param>
    /// <returns>>True if any gateway was found and the message was forwarded to at least one, otherwise false</returns>
    public static async Task<bool> ForwardToRelevantGateways(this GatewayConnectionList gatewayConnectionList, string instanceId, EnvelopedMessage message, string organizationId, string clientId)
    {
        var any = false;
        foreach (var gatewayServer in gatewayConnectionList.IsRelevantTo(organizationId, clientId))
        {
            any = true;
            try
            {
                await ForwardProxyResponse(gatewayServer, instanceId, message, organizationId);
            }
            catch (Exception e)
            {
                Log.Warning(e, "Failed to forward proxy response to gateway {GatewayId}", gatewayServer.ClientId);
            }
        }

        return any;
    }

    /// <summary>
    /// Forwards a control response message to a gateway server
    /// </summary>
    /// <param name="from">The source client id</param>
    /// <param name="target">The target gateway server</param>
    /// <param name="sourceMessage">The source message from the agent</param>
    /// <param name="organizationId">The organization id</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public static async Task ForwardProxyResponse(this SocketState target, string from, EnvelopedMessage sourceMessage, string organizationId)
    {
        var message = new EnvelopedMessage
        {
            Type = MessageTypes.Proxy.ToString().ToLowerInvariant(),
            From = from,
            To = target.ClientId,
            MessageId = sourceMessage.MessageId,
            Payload = EnvelopedMessage.SerializePayload(new ProxyMessage
            {
                Type = sourceMessage.Type!,
                From = sourceMessage.From!,
                To = sourceMessage.To!,
                OrganizationId = organizationId,
                InnerMessage = sourceMessage.Payload
            }),
        };

        await target.WriteMessage(message, WrappingType.PlainText);
    }
}
