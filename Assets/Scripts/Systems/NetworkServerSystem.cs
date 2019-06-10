using System;
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

    private NativeList<int> currentId;

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
        if (currentId.IsCreated)
            currentId.Dispose();
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

    struct UpdateServerJob : IJobForEachWithEntity<NetworkServerConnection>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;
        public UdpNetworkDriver.Concurrent driver;
        public NativeArray<int> id;

        public void Execute(Entity entity, int index, ref NetworkServerConnection serverConnection)
        {
            DataStreamReader stream;
            NetworkEvent.Type command;
            while ((command = driver.PopEventForConnection(serverConnection.connection, out stream)) != NetworkEvent.Type.Empty)
            {
                if (command == NetworkEvent.Type.Data)
                {
                    Debug.Log("data command on server");
                    DataStreamReader.Context readerCtx = default(DataStreamReader.Context);
                    byte[] lengthOfDataAsBytes = stream.ReadBytesAsArray(ref readerCtx, sizeof(int));
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(lengthOfDataAsBytes);
                    }
                    int lengthOfData = BitConverter.ToInt32(lengthOfDataAsBytes, 0);

                    byte[] dataRead = stream.ReadBytesAsArray(ref readerCtx, lengthOfData);

   
                    byte[] cmdTypeRead = new byte[sizeof(int)];
                    Buffer.BlockCopy(dataRead, 0, cmdTypeRead, 0, sizeof(int));
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(cmdTypeRead);
                    }
                    int commandType = BitConverter.ToInt32(cmdTypeRead, 0);

                    Debug.Log("command type received " + commandType);
                    if (commandType == CommandType.SpawnNewPlayer)
                    {
                        int networkId = id[0];
                        byte[] cmdTypeAsBytes = BitConverter.GetBytes(CommandType.SpawnNewPlayer);
                        byte[] idAsBytes = BitConverter.GetBytes(networkId);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(cmdTypeAsBytes);
                            Array.Reverse(idAsBytes);
                        }

                        byte[] result = NetworkingUtils.CombineBytes(cmdTypeAsBytes, idAsBytes);
                        int lengthOfMsg = result.Length * sizeof(byte); // in bytes, one int, just for the command type and the id
                        byte[] lengthAsBytes = BitConverter.GetBytes(lengthOfMsg);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(lengthAsBytes);
                        }
                        result = NetworkingUtils.CombineBytes(lengthAsBytes, result);
                        DataStreamWriter data = new DataStreamWriter(sizeof(byte) * result.Length, Allocator.Temp);
                        data.Write(result);
   
                        id[0] += 1;
                        
                        driver.Send(NetworkPipeline.Null, serverConnection.connection, data);

                        commandBuffer.SetComponent(index, entity, new NetworkServerConnection
                        {
                            connection = serverConnection.connection,
                            networkId = networkId
                        });
                    }
                }
                else if (command == NetworkEvent.Type.Disconnect)
                {
                    commandBuffer.DestroyEntity(index, entity);
                }
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

            currentId = new NativeList<int>(1, Allocator.Persistent);
            currentId.Add(0);
        }
        else
        {
            serverJobHandle = m_Driver.ScheduleUpdate(inputDeps);
            serverJobHandle = new ListenForConnectionsJob
            {
                commandBuffer = m_Barrier.CreateCommandBuffer(),
                driver = m_Driver,
            }.Schedule(serverJobHandle);

            serverJobHandle.Complete();

            serverJobHandle = new UpdateServerJob
            {
                commandBuffer = m_Barrier.CreateCommandBuffer().ToConcurrent(),
                driver = m_Driver.ToConcurrent(),
                id = currentId.AsDeferredJobArray()
            }.Schedule(this, serverJobHandle);

            m_Barrier.AddJobHandleForProducer(serverJobHandle);
            //Debug.Log("about to listen for connections");
            return serverJobHandle;
        }

        return inputDeps;
    }
}
