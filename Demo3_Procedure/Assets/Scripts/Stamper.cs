using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Applies pooled decal projectors to stamped objects. Every pool slot uses a
/// different rendering-layer bit, so its decal can render only on its target.
/// </summary>
[DisallowMultipleComponent]
public sealed class Stamper : MonoBehaviour
{
    [Serializable]
    public sealed class StampEvent : UnityEvent<GameObject> { }

    private const int PoolSize = 8;
    private const int FirstRenderingLayerBit = 1; // Keep bit 0 available as the Default layer.

    [Header("Stamp")]
    [SerializeField] private DecalProjector decalProjectorPrefab;
    [SerializeField] private Transform spawnPoint;
    [Min(0f)] [SerializeField] private float stampVelocity = 0.5f;

    [Header("Events")]
    [Tooltip("Invoked with the stamped object after a decal has been successfully applied.")]
    [SerializeField] private StampEvent onStamped = new StampEvent();

    public StampEvent OnStamped => onStamped;

    [Serializable]
    private sealed class DecalSlot
    {
        public DecalProjector Projector;
        public uint RenderingLayer;
        public readonly List<Renderer> TargetRenderers = new List<Renderer>();
    }

    private readonly DecalSlot[] pool = new DecalSlot[PoolSize];
    private readonly Dictionary<Renderer, uint> originalRenderingLayers = new Dictionary<Renderer, uint>();
    private int nextSlot;
    private bool poolReady;

    private void Awake()
    {
        BuildPool();
    }

    private void BuildPool()
    {
        if (decalProjectorPrefab == null)
        {
            Debug.LogError($"{nameof(Stamper)} on {name} needs a Decal Projector Prefab.", this);
            enabled = false;
            return;
        }

        Transform origin = spawnPoint != null ? spawnPoint : transform;
        for (int i = 0; i < PoolSize; i++)
        {
            DecalProjector projector = Instantiate(
                decalProjectorPrefab,
                origin.position,
                origin.rotation,
                transform);

            uint renderingLayer = 1u << (FirstRenderingLayerBit + i);
            projector.name = $"{decalProjectorPrefab.name} (Pool {i + 1})";
            projector.renderingLayerMask = renderingLayer;
            projector.gameObject.SetActive(false);

            pool[i] = new DecalSlot
            {
                Projector = projector,
                RenderingLayer = renderingLayer
            };
        }

        poolReady = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!poolReady || collision.relativeVelocity.magnitude < stampVelocity) return;

        Transform target = collision.rigidbody != null
            ? collision.rigidbody.transform
            : collision.transform;

        ApplyStamp(target);
    }

    private void ApplyStamp(Transform target)
    {
        if (target == null) return;

        DecalSlot slot = pool[nextSlot];
        nextSlot = (nextSlot + 1) % PoolSize;

        ReleaseSlot(slot);

        // Include inactive child renderers because an object can change visible parts
        // while the stamp is still attached to it.
        target.GetComponentsInChildren(true, slot.TargetRenderers);
        for (int i = slot.TargetRenderers.Count - 1; i >= 0; i--)
        {
            Renderer targetRenderer = slot.TargetRenderers[i];
            if (targetRenderer == null)
            {
                slot.TargetRenderers.RemoveAt(i);
                continue;
            }

            if (!originalRenderingLayers.ContainsKey(targetRenderer))
                originalRenderingLayers.Add(targetRenderer, targetRenderer.renderingLayerMask);

            targetRenderer.renderingLayerMask |= slot.RenderingLayer;
        }

        Transform origin = spawnPoint != null ? spawnPoint : transform;
        Transform projectorTransform = slot.Projector.transform;
        projectorTransform.SetParent(target, true);
        projectorTransform.SetPositionAndRotation(origin.position, origin.rotation);
        slot.Projector.renderingLayerMask = slot.RenderingLayer;
        slot.Projector.gameObject.SetActive(true);

        // Conveyor products are spawned at runtime, so notify the stamped instance.
        MineAssembler assembler = target.GetComponentInParent<MineAssembler>();
        if (assembler != null) assembler.NotifyStamped(target.gameObject);

        onStamped.Invoke(target.gameObject);
    }

    private void ReleaseSlot(DecalSlot slot)
    {
        if (slot == null) return;

        if (slot.Projector != null)
        {
            slot.Projector.gameObject.SetActive(false);
            // ApplyStamp reparents reused decals; OnDestroy deletes them in place.
            // Never reparent to this stamp here: it may already be destroying itself.
        }

        for (int i = 0; i < slot.TargetRenderers.Count; i++)
        {
            Renderer targetRenderer = slot.TargetRenderers[i];
            if (targetRenderer == null) continue;

            if (originalRenderingLayers.TryGetValue(targetRenderer, out uint originalMask))
            {
                uint remainingStampLayers = GetActiveStampLayers(targetRenderer, slot);
                targetRenderer.renderingLayerMask = originalMask | remainingStampLayers;

                if (remainingStampLayers == 0u)
                    originalRenderingLayers.Remove(targetRenderer);
            }
            else
            {
                targetRenderer.renderingLayerMask &= ~slot.RenderingLayer;
            }
        }

        slot.TargetRenderers.Clear();
    }

    private uint GetActiveStampLayers(Renderer targetRenderer, DecalSlot slotBeingReleased)
    {
        uint layers = 0u;
        for (int i = 0; i < pool.Length; i++)
        {
            DecalSlot slot = pool[i];
            if (slot == null || slot == slotBeingReleased) continue;
            if (slot.TargetRenderers.Contains(targetRenderer))
                layers |= slot.RenderingLayer;
        }
        return layers;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < pool.Length; i++)
        {
            DecalSlot slot = pool[i];
            if (slot == null) continue;
            ReleaseSlot(slot);
            if (slot.Projector != null) Destroy(slot.Projector.gameObject);
        }

        originalRenderingLayers.Clear();
    }
}
