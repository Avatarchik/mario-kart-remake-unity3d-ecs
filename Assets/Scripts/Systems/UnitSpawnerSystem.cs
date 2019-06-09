using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;

public class UnitSpawnerSystem : JobComponentSystem
{
    BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;

    protected override void OnCreate()
    {
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
    }

    struct SpawnJob : IJobForEachWithEntity<UnitSpawner>
    {
        public EntityCommandBuffer.Concurrent commandBuffer;
        public void Execute(Entity entity, int index, ref UnitSpawner spawner)
        {
            Entity spawnedEntity = commandBuffer.Instantiate(index, spawner.prefab);
            commandBuffer.SetComponent(index, spawnedEntity, new Translation { Value = spawner.spawnPoint });
            // what does the line below do?????
            commandBuffer.DestroyEntity(index, entity);
        }
    }
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        JobHandle job = new SpawnJob
        {
            commandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        }.ScheduleSingle(this, inputDeps);

        m_EntityCommandBufferSystem.AddJobHandleForProducer(job);

        return job;
    }
}
