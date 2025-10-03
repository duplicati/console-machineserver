# Duplicati Machine Services

The Duplicati Machine Services is a service that allows secure proxying of commands between two endpoints, allowing secure and seamless access to a limited API. By using only standard outgoing HTTP WebSocket connections the service has a minimum of load on each client and is compatible with most firewalls.

This project can be run as stand-alone in Debug mode, but requires a controller that implements the authentication of requests via the MassTransit message bus.

The MachineServices are composed of two active services that are used to orchestrate the conmunication between the Portal and the Agents for the Remote Management feature.

## MachineService.Server

This is the webserver/websocket server that listens for incoming requests from the Portal and Agents.

It has an adaptive protocol that can be used to communicate with the Portal and Agents using both plain text/Json messages and JWT/JWE encoded and encrypted payloads

The service keeps track of connected clients in a database so it is possible to support multiple instances of the server.

## Protocol

Connections are always initiated by the client/console to the MachineServer via websockets.

The message envelope is as follows:

```json
{
  "From": "source",
  "To": "destination",
  "Type": "pong",
  "MessageId": "428411b1-e8a0-4e5b-91b2-52cdd3b10fc3",
  "Payload": null
}
```

Valid types for Console to MachineService are:

- **welcome** - sent by the MachineService when the connection is established, asking for authentication to start
- **authportal** - used to authenticate the console to MachineService
- **list** - list all the available agents belonging to the same organization as the authenticated portal user
- **command** - sends a (proxy) command to an agent, the payload is a json object with the http command, headers and body.
- **control** - sends a control message to an agent, the payload is a json object with the control details.

The state machine is as follows:

1. Console client connects to MachineService's websocket.
2. MachineService sends a welcome message to the Console client.
3. Console sends an authportal message to MachineService including the access token.
4. MachineService responds with an authportal message with the authresult as payload

If authentication is successfull, the Portal can send a `list` message to MachineService to get a list of available agents, or directly send a command to an agent.

### `authportal`

Request:

Payload model:

```csharp
public record AuthMessage(string? JwToken, string? PublicKey, string ClientVersion, int ProtocolVersion, Dictionary<string, string?>? Metadata);
```

```json
{
  "from": "Console",
  "to": "MachineService",
  "type": "authportal",
  "messageId": "428411b1-e8a0-4e5b-91b2-52cdd3b10fc3",
  "payload": {
    "jwToken": "Access token",
    "clientVersion": "",
    "protocolVersion": 1
  }
}
```

Response:

Payload model:

```csharp
public record AuthResultMessage(bool? Accepted, bool? WillReplaceToken, string? NewToken);
```

```json
{
  "from": "Console",
  "to": "MachineService",
  "type": "authportal",
  "messageId": "428411b1-e8a0-4e5b-91b2-52cdd3b10fc3",
  "payload": {
    "accepted": true
  }
}
```

For Console authentication WillReplaceToken and NewToken are always null, they only apply for agent connections, where they indicate that the agent should take NewToken as the token is about to expire.

If Accepted is false, the websocket connection is closed by the MachineServer.

### list

Request:

Payload model: none, send null.

```json
{
  "from": "Console",
  "to": "MachineService",
  "type": "list",
  "messageId": "428411b1-e8a0-4e5b-91b2-52cdd3b10fc3",
  "payload": null
}
```

Response:

Payload model: **\_An Array of ClientRegistration**

```csharp
public record ClientRegistration
{
    public string ClientId;
    public string ClientVersion;
    public string OrganizationId;
    public string MachineRegistrationId;
    public string MachineServerUri;
    public ClientType Type;
    public DateTime LastUpdated;
}
```

```json
{
  "from": "Console",
  "to": "MachineService",
  "type": "list",
  "messageId": "428411b1-e8a0-4e5b-91b2-52cdd3b10fc3",
  "payload": [
    {
      "clientId": "428411b1-e8a0-4e5b-91b2-52cdd3b10fc3",
      "clientVersion": "2.0.0",
      "organizationId": "11111111-1111-1111-1111-111111111111",
      "machineRegistrationId": "yyyyyy"
    },
    {
      "clientId": "328411b1-e8a0-4e5b-91b2-52cdd3b10fc3",
      "clientVersion": "2.0.0",
      "organizationId": "11111111-1111-1111-1111-111111111111",
      "machineRegistrationId": "yyyyyy"
    }
  ]
}
```
