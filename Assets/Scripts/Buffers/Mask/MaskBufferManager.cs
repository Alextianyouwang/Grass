using UnityEngine;
[ExecuteInEditMode]
[DefaultExecutionOrder(-100)]
public class MaskBufferManager : BufferPool
{
    [Range(0, 1)]
    public float Threshold = 0.5f;
    private void OnEnable()
    {
        Initialize("CS/CS_FoliageMask", "_MaskBuffer",sizeof(float)*4);
        OnBufferCreated += SetInitialParameters;

        TileGrandCluster.OnRequestMaskBuffer += CreateBuffer;
        TileGrandCluster.OnRequestDisposeMaskBuffer += DisposeBuffer;
        TileVisualizer.OnRequestMaskBuffer += CreateBuffer;
        TileVisualizer.OnRequestDisposeMaskBuffer += DisposeBuffer;
        TileStripeFX.OnRequestMaskBuffer += CreateBuffer;
        TileStripeFX.OnRequestDisposeMaskBuffer += DisposeBuffer;
    }

    private void OnDisable()
    {

        TileGrandCluster.OnRequestMaskBuffer -= CreateBuffer;
        TileGrandCluster.OnRequestDisposeMaskBuffer -= DisposeBuffer;
        TileVisualizer.OnRequestMaskBuffer -= CreateBuffer;
        TileVisualizer.OnRequestDisposeMaskBuffer -= DisposeBuffer;
        TileStripeFX.OnRequestMaskBuffer -= CreateBuffer;
        TileStripeFX.OnRequestDisposeMaskBuffer -= DisposeBuffer;
        foreach (DataPerTileCluster d in _dataTracker.Values)
            d.Buffer.Dispose();
        _dataTracker.Clear();
    }
    private void SetInitialParameters() 
    {
    
    }

    private void LateUpdate()
    {
        ComputeSetFloat("_Time", Time.time);
        ComputeSetFloat("_Step", Threshold);
        UpdateBuffer();
    }
}
