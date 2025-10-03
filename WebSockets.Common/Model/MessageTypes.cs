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
namespace WebSockets.Common.Model;

/// <summary>
/// The type of message being sent
/// </summary>
public enum MessageTypes
{
    /// <summary>
    /// The ping message, used to keep the connection alive
    /// </summary>
    Ping,
    /// <summary>
    /// The pong message, used to respond to a ping
    /// </summary>
    Pong,
    /// <summary>
    /// A message requesting authentication
    /// </summary>
    Auth,
    /// <summary>
    /// A message requesting authentication from the portal
    /// </summary>
    AuthPortal,
    /// <summary>
    /// An initial welcome message from the server
    /// </summary>
    Welcome,
    /// <summary>
    /// A warning message
    /// </summary>
    Warning,
    /// <summary>
    /// A command message for proxying commands to the client
    /// </summary>
    Command,
    /// <summary>
    /// A control message for controlling the client
    /// </summary>
    Control,
    /// <summary>
    /// A list message from the console to get a list of connected clients
    /// </summary>
    List
}