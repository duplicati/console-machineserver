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
using MachineService.Common.Model;
using MachineService.State.Services;

namespace MachineService.State.UnitTests;

public class InMemoryStateManagerServiceTests
{
    private InMemoryStateManagerService _service = null!;

    [SetUp]
    public void Setup()
    {
        _service = new InMemoryStateManagerService();
    }

    [Test]
    public async Task RegisterClient_NewClient_ReturnsTrue()
    {
        var result = await _service.RegisterClient(ConnectionType.Agent, Guid.NewGuid(), "client1", "org1", "agent1", "1.0", "uri", "ip");

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task RegisterClient_ExistingClient_UpdatesRegistration()
    {
        await _service.RegisterClient(ConnectionType.Agent, Guid.NewGuid(), "client1", "org1", "agent1", "1.0", "uri1", "ip1");
        var initialClients = await _service.GetAgents("org1");
        var initialLastUpdated = initialClients.First().LastUpdatedOn;

        await Task.Delay(10); // Ensure time difference
        await _service.RegisterClient(ConnectionType.Agent, Guid.NewGuid(), "client1", "org1", "agent2", "2.0", "uri2", "ip2");

        var updatedClients = await _service.GetAgents("org1");
        Assert.That(updatedClients.Count, Is.EqualTo(1));
        Assert.That(updatedClients.First().MachineRegistrationId, Is.EqualTo("agent2"));
        Assert.That(updatedClients.First().ClientVersion, Is.EqualTo("2.0"));
        Assert.That(updatedClients.First().GatewayId, Is.EqualTo("uri2"));
        Assert.That(updatedClients.First().LastUpdatedOn, Is.GreaterThan(initialLastUpdated));
    }

    [Test]
    public async Task UpdateClientActivity_ExistingClient_ReturnsTrueAndUpdatesTimestamp()
    {
        await _service.RegisterClient(ConnectionType.Agent, Guid.NewGuid(), "client1", "org1", "agent1", "1.0", "uri", "ip");
        var initialClients = await _service.GetAgents("org1");
        var initialLastUpdated = initialClients.First().LastUpdatedOn;

        await Task.Delay(10);
        var result = await _service.UpdateClientActivity("client1", "org1");

        Assert.That(result, Is.True);
        var updatedClients = await _service.GetAgents("org1");
        Assert.That(updatedClients.First().LastUpdatedOn, Is.GreaterThan(initialLastUpdated));
    }

    [Test]
    public async Task UpdateClientActivity_NonExistingClient_ReturnsFalse()
    {
        var result = await _service.UpdateClientActivity("client1", "org1");

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task DeRegisterClient_ExistingClient_RemovesClient()
    {
        await _service.RegisterClient(ConnectionType.Agent, Guid.NewGuid(), "client1", "org1", "agent1", "1.0", "uri", "ip");
        var initialClients = await _service.GetAgents("org1");
        Assert.That(initialClients.Count, Is.EqualTo(1));

        var result = await _service.DeRegisterClient(Guid.NewGuid(), "client1", "org1", 0, 0);

        Assert.That(result, Is.True);
        var remainingClients = await _service.GetAgents("org1");
        Assert.That(remainingClients.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task DeRegisterClient_NonExistingClient_ReturnsTrue()
    {
        var result = await _service.DeRegisterClient(Guid.NewGuid(), "client1", "org1", 0, 0);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task GetClients_NoClients_ReturnsEmptyList()
    {
        var clients = await _service.GetAgents("org1");

        Assert.That(clients, Is.Empty);
    }

    [Test]
    public async Task GetClients_WithClients_ReturnsClientsExcludingPortalClients()
    {
        await _service.RegisterClient(ConnectionType.Agent, Guid.NewGuid(), "client1", "org1", "agent1", "1.0", "uri", "ip");
        await _service.RegisterClient(ConnectionType.Agent, Guid.NewGuid(), "portal-client1", "org1", "agent2", "1.0", "uri", "ip");

        var clients = await _service.GetAgents("org1");

        Assert.That(clients.Count, Is.EqualTo(1));
        Assert.That(clients.First().ClientId, Is.EqualTo("client1"));
    }

    [Test]
    public async Task GetClients_DifferentOrganizations_Isolated()
    {
        await _service.RegisterClient(ConnectionType.Agent, Guid.NewGuid(), "client1", "org1", "agent1", "1.0", "uri", "ip");
        await _service.RegisterClient(ConnectionType.Agent, Guid.NewGuid(), "client2", "org2", "agent2", "1.0", "uri", "ip");

        var org1Clients = await _service.GetAgents("org1");
        var org2Clients = await _service.GetAgents("org2");

        Assert.That(org1Clients.Count, Is.EqualTo(1));
        Assert.That(org1Clients.First().ClientId, Is.EqualTo("client1"));
        Assert.That(org2Clients.Count, Is.EqualTo(1));
        Assert.That(org2Clients.First().ClientId, Is.EqualTo("client2"));
    }

    [Category("Concurrency")]
    [Test]
    public async Task ConcurrentOperations_ThreadSafe()
    {
        const int numTasks = 10;
        var tasks = new List<Task>();

        for (int i = 0; i < numTasks; i++)
        {
            var clientId = $"client{i}";
            tasks.Add(Task.Run(async () =>
            {
                await _service.RegisterClient(ConnectionType.Agent, Guid.NewGuid(), clientId, "org1", $"agent{clientId}", "1.0", "uri", "ip");
                for (int j = 0; j < 5; j++)
                {
                    await _service.UpdateClientActivity(clientId, "org1");
                }
                await _service.DeRegisterClient(Guid.NewGuid(), clientId, "org1", 0, 0);
            }));
        }

        await Task.WhenAll(tasks);

        var finalClients = await _service.GetAgents("org1");
        Assert.That(finalClients, Is.Empty);
    }
}