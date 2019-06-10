using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Networking.Transport;
using UnityEngine;

public struct NetworkClientConnection: IComponentData
{
    public NetworkConnection connection;
}

public struct NetworkServerConnection : IComponentData
{
    public NetworkConnection connection;
}
