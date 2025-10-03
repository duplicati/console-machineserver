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
using System.Buffers.Text;
using System.Text;

namespace MachineService.Common.Util;

/// <summary>
/// Helper class to read PEM encoded strings that are Base64 encoded
/// </summary>
public static class Base64PemReader
{
    /// <summary>
    /// Read a PEM string that is Base64 encoded and return the decoded string
    /// </summary>
    /// <param name="pem">The Base64 encoded PEM string</param>
    /// <returns>The decoded string</returns>
    public static string Read(string pem)
        => Base64.IsValid(pem)
            ? Encoding.UTF8.GetString(Convert.FromBase64String(pem))
            : throw new FormatException("PEM must be supplied encoded as a Base64 string");

    /// <summary>
    /// Write a PEM string as a Base64 encoded string
    /// </summary>
    /// <param name="pem">The PEM string to encode</param>
    /// <returns>The Base64 encoded string</returns>
    public static string Write(string pem)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(pem));
}