// Copyright (c) 2025 Duplicati Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
namespace MachineService.Common;

/// <summary>
/// Constants for the machine service error messages
/// </summary>
public static class ErrorMessages
{

    /// <summary>
    /// The message was not parsable and will be considered as malformed.
    ///
    /// No further detail is provided on the error so as to not reveal any information about the parser.
    /// </summary>
    public const string MalformedEnvelope = "Malformed message";

    /// <summary>
    /// This error is used when a message of type command is received, to be forwarded to to a destination
    /// and it is not found in the connection list
    /// </summary>
    public const string DestinationNotAvailableForRelay = "Destination not available for relay";

    /// <summary>
    /// This error is sent to a agent/portal when the token is expired.
    /// </summary>
    public const string TokenExpired = "Token expired";

    /// <summary>
    /// Authportal message without payload
    /// </summary>
    public const string AuthMessageWithoutPayload = "Auth message without payload";

    /// <summary>
    /// Invalid connection state for authentication
    /// </summary>
    public const string InvalidConnectionStateForAuthentication = "Invalid connection state for authentication";

    /// <summary>
    /// Authportal message without token
    /// </summary>
    public const string AuthMessageWithoutToken = "Auth message without token";

    /// <summary>
    /// Invalid connection state for list
    /// </summary>
    public const string InvalidConnectionStateForList = "Invalid connection state for list";

    /// <summary>
    /// Invalid protocol version
    /// </summary>
    public const string InvalidProtocolVersion = "Invalid protocol version";

    /// <summary>
    /// Invalid payload for authentication
    /// </summary>
    public const string InvalidAuthPayload = "Invalid payload for authentication";

    /// <summary>
    /// Too much data sent on websocket, will close connection
    /// </summary>
    public const string TooMuchDataWithoutAuthentication = "Too much data sent on websocket before authentication, will close connection";
}