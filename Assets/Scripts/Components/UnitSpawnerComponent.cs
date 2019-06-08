using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct UnitSpawnerComponent : IComponentData
{
    public Entity prefab;
    public float3 spawnPoint;
}
