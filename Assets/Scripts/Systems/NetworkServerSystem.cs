using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;

public class NetworkServerSystem : JobComponentSystem
{
    private BeginSimulationEntityCommandBufferSystem m_Barrier;
    private EntityQuery m_ServerGroup;
    private EntityQuery m_ConnectionGroup;

    public UdpNetworkDriver m_Driver;
    public NativeList<NetworkConnection> m_Connections;

    public NetworkPipeline m_Unreliable_Pipeline;
    public NetworkPipeline m_Reliable_Pipeline;

    const int connectionCapacity = 16;

    const int k_PacketSize = 256;

    private int serverListening = 0;

    protected override void OnCreateManager()
    {
        m_Barrier = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
        m_ServerGroup = GetEntityQuery(ComponentType.ReadWrite<NetworkServer>());
        m_ConnectionGroup = GetEntityQuery(ComponentType.ReadWrite<NetworkServerConnection>());

        /*
        ReliableUtility.Parameters reliabilityParams = new ReliableUtility.Parameters { WindowSize = 32 };
        SimulatorUtility.Parameters simulatorParams = new SimulatorUtility.Parameters { MaxPacketSize = k_PacketSize, MaxPacketCount = 30, PacketDelayMs = 100 };

        m_Driver = new UdpNetworkDriver(simulatorParams, reliabilityParams);

        m_Unreliable_Pipeline = m_Driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage), typeof(SimulatorPipelineStage));
        m_Reliable_Pipeline = m_Driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage), typeof(SimulatorPipelineStage));

        m_Connections = new NativeList<NetworkConnection>(connectionCapacity, Allocator.Persistent); // first parameter is number of connections to accept
        */
        Debug.Log("network server on create");
    }

    protected override void OnDestroyManager()
    {
        // Destroy NetworkDrivers if the manager is destroyed with live entities
        Debug.Log("Server system on destroy");
        if (m_Driver.IsCreated)
            m_Driver.Dispose();
        
    }

    struct ListenForConnectionsJob : IJob
    {
        public EntityCommandBuffer commandBuffer;
        public UdpNetworkDriver driver;
        public void Execute()
        {
            //Debug.Log("listen for connections job");
            NetworkConnection connection;
            while ((connection = driver.Accept()) != default(NetworkConnection))
            {
                Entity serverConnectionEntity = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(serverConnectionEntity, new NetworkServerConnection { connection = connection });
                Debug.Log("connection accepted");
            }
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        inputDeps.Complete();

        JobHandle serverJobHandle;
        //Debug.Log("server system on update");
        if (!m_Driver.IsCreated)
        {
            ReliableUtility.Parameters reliabilityParams = new ReliableUtility.Parameters { WindowSize = 32 };
            SimulatorUtility.Parameters simulatorParams = new SimulatorUtility.Parameters { MaxPacketSize = k_PacketSize, MaxPacketCount = 30, PacketDelayMs = 100 };

            m_Driver = new UdpNetworkDriver(simulatorParams, reliabilityParams);
            NetworkEndPoint endpoint = NetworkEndPoint.AnyIpv4;
            endpoint.Port = 9000;
            if (m_Driver.Bind(endpoint) != 0)
                Debug.Log("Failed to bind to port 9000");
            else
            {
                m_Driver.Listen();
                Debug.Log("driver listening on port 9000");
            }  
        }
        else
        {
            serverJobHandle = m_Driver.ScheduleUpdate(inputDeps);
            serverJobHandle = new ListenForConnectionsJob
            {
                commandBuffer = m_Barrier.CreateCommandBuffer(),
                driver = m_Driver,
            }.Schedule(serverJobHandle);
            m_Barrier.AddJobHandleForProducer(serverJobHandle);
            //Debug.Log("about to listen for connections");
            return serverJobHandle;
        }

        return inputDeps;
    }
}
