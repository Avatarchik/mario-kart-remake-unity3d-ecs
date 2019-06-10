using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEditor.MemoryProfiler;
using UnityEngine;

public class NetworkClientSystem : JobComponentSystem
{
    private BeginSimulationEntityCommandBufferSystem m_Barrier;
    private EntityQuery m_ClientGroup;
    private EntityQuery m_ConnectionGroup;

    public UdpNetworkDriver m_Driver;
    public NativeArray<NetworkConnection> m_Connection;
    public NativeArray<byte> m_Done;

    public NetworkEndPoint m_Server_EndPoint;
    public NetworkPipeline m_Unreliable_Pipeline;
    public NetworkPipeline m_Reliable_Pipeline;

    const int k_PacketSize = 256;

    protected override void OnCreateManager()
    {
        m_Barrier = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
        m_ClientGroup = GetEntityQuery(ComponentType.ReadWrite<NetworkClient>());
        m_ConnectionGroup = GetEntityQuery(ComponentType.ReadWrite<NetworkClientConnection>());

        /*ReliableUtility.Parameters reliabilityParams = new ReliableUtility.Parameters { WindowSize = 32 };
        SimulatorUtility.Parameters simulatorParams = new SimulatorUtility.Parameters { MaxPacketSize = k_PacketSize, MaxPacketCount = 30, PacketDelayMs = 100 };

        m_Driver = new UdpNetworkDriver(simulatorParams, reliabilityParams);

        m_Server_EndPoint = NetworkEndPoint.LoopbackIpv4;
        m_Server_EndPoint.Port = 9000;

        m_Unreliable_Pipeline = m_Driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage), typeof(SimulatorPipelineStage));
        m_Reliable_Pipeline = m_Driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage), typeof(SimulatorPipelineStage));

        m_Connections = new NativeArray<NetworkConnection>(1, Allocator.Persistent);
        m_Done = new NativeArray<byte>(1, Allocator.Persistent);*/
        Debug.Log("client system oncreate");

    }

    protected override void OnDestroyManager()
    {
        Debug.Log("client system on destroy manager");
        if (m_Driver.IsCreated)
            m_Driver.Dispose();
    }

    struct SendConnectionRequestJob : IJob
    {
        public EntityCommandBuffer commandBuffer;
        public UdpNetworkDriver driver;
        public NetworkEndPoint serverEndPoint;

        public void Execute()
        {
            Entity clientEntity = commandBuffer.CreateEntity();
            Debug.Log("about to send a connection request");
            commandBuffer.AddComponent(clientEntity, new NetworkClientConnection { connection = driver.Connect(serverEndPoint) });
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        inputDeps.Complete();

        JobHandle clientJobHandle;
        if (!m_Driver.IsCreated)
        {
            ReliableUtility.Parameters reliabilityParams = new ReliableUtility.Parameters { WindowSize = 32 };
            SimulatorUtility.Parameters simulatorParams = new SimulatorUtility.Parameters { MaxPacketSize = k_PacketSize, MaxPacketCount = 30, PacketDelayMs = 100 };

            m_Driver = new UdpNetworkDriver(simulatorParams, reliabilityParams);

            m_Server_EndPoint = NetworkEndPoint.LoopbackIpv4;
            m_Server_EndPoint.Port = 9000;
        }
        if (m_Server_EndPoint.IsValid && m_ConnectionGroup.IsEmptyIgnoreFilter)
        {
            //Debug.Log("client job handle about to be created");
            clientJobHandle = new SendConnectionRequestJob
            {
                commandBuffer = m_Barrier.CreateCommandBuffer(),
                driver = m_Driver,
                serverEndPoint = m_Server_EndPoint,
            }.Schedule(inputDeps);

            m_Barrier.AddJobHandleForProducer(clientJobHandle);

            return clientJobHandle;
        }

        return inputDeps;
    }

}
