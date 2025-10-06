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
using MachineService.Common.Enums;
using MachineService.Common.Services;

namespace MachineService.Common.UnitTests;

public class StatisticsGathererTests
{
    [Category("Concurrency")]
    [Test]
    public async Task ThreadSafetyTest()
    {
        var gatherer = new StatisticsGatherer();
        var tasks = new List<Task>();
        const int numThreads = 10;
        const int incrementsPerThread = 10000;
        var expectedTotal = (ulong)(numThreads * incrementsPerThread);

        // Concurrent increments and exports
        for (int i = 0; i < numThreads; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < incrementsPerThread; j++)
                {
                    gatherer.Increment(StatisticsType.CommandRelaySuccess);
                    if (j % 1000 == 0) // Frequent exports during increments
                    {
                        _ = gatherer.Export();
                    }
                }
            }));
        }

        // Additional thread just doing exports
        tasks.Add(Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                _ = gatherer.Export();
                await Task.Delay(1);
            }
        }));

        await Task.WhenAll(tasks);

        var finalStats = await gatherer.ExportAndResetAsync();
        Assert.That(finalStats[StatisticsType.CommandRelaySuccess], Is.EqualTo(expectedTotal));
    }
}