using UnityEngine;

public class TileChunk
{
    public Bounds ChunkBounds { get; private set; }

    private Mesh[] _spawnMesh;
    private Material _spawnMeshMaterial;
    private MaterialPropertyBlock _mpb;
    private Camera _renderCam;

    private ComputeShader _cullShader;


    private ComputeBuffer _spawnBuffer;
    private ComputeBuffer _voteBuffer;
    private ComputeBuffer _scanBuffer;
    private ComputeBuffer _groupScanInBuffer;
    private ComputeBuffer _groupScanOutBuffer;
    private ComputeBuffer _compactBuffer;
    private ComputeBuffer[] _argsBuffer;

    private int _elementCount;
    private int _groupCount;

    private Color _chunkColor;
    struct SpawnData
    {
        Vector3 positionWS;
        float hash;
        Vector4 clumpInfo;
    };

    public TileChunk(Mesh[] spawnMesh, Material spawmMeshMat,  Camera renderCam, ComputeBuffer initialBuffer,ComputeShader cullShader,Bounds chunkBounds) 
    {
        _spawnMesh = spawnMesh;
        _spawnMeshMaterial = spawmMeshMat;
        _renderCam = renderCam;
        _spawnBuffer = initialBuffer;
        _cullShader = cullShader;
        ChunkBounds = chunkBounds;
        _mpb = new MaterialPropertyBlock();
    }

    public void Setup() 
    {
        _cullShader = (ComputeShader)GameObject.Instantiate(Resources.Load("CS_GrassCulling")) ;

        _elementCount = Utility.CeilToNearestPowerOf2(_spawnBuffer.count);
        _groupCount = _elementCount / 128;

        _voteBuffer = new ComputeBuffer(_elementCount, sizeof(int));
        _scanBuffer = new ComputeBuffer(_elementCount, sizeof(int));
        _groupScanInBuffer = new ComputeBuffer(_groupCount, sizeof(int));
        _groupScanOutBuffer = new ComputeBuffer(_groupCount, sizeof(int));

        _compactBuffer = new ComputeBuffer(_elementCount, sizeof(float) * 8);
        _argsBuffer = new ComputeBuffer[_spawnMesh.Length];
        for(int i = 0; i< _spawnMesh.Length; i++)
        {
            _argsBuffer[i] = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
            uint[] _args = new uint[] {
            _spawnMesh[i].GetIndexCount(0),
            0,
            _spawnMesh[i].GetIndexStart(0),
            _spawnMesh[i].GetBaseVertex(0),
            0
        };
            _argsBuffer[i].SetData(_args);
        }
        

        _cullShader.SetInt("_InstanceCount", _spawnBuffer.count);
        _cullShader.SetFloat("_MaxRenderDist", TileGrandCluster._MaxRenderDistance);
        _cullShader.SetFloat("_DensityFalloffDist", TileGrandCluster._DensityFalloffThreshold);
        _cullShader.SetFloat("_OffsetX", -ChunkBounds.center.x);
        _cullShader.SetFloat("_OffsetY", -ChunkBounds.center.z);

        _cullShader.SetBuffer(0, "_SpawnBuffer", _spawnBuffer);
        _cullShader.SetBuffer(0, "_VoteBuffer", _voteBuffer);

        _cullShader.SetBuffer(1, "_VoteBuffer", _voteBuffer);
        _cullShader.SetBuffer(1, "_ScanBuffer", _scanBuffer);
        _cullShader.SetBuffer(1, "_GroupScanBufferIn", _groupScanInBuffer);

        _cullShader.SetBuffer(2, "_GroupScanBufferIn", _groupScanInBuffer);
        _cullShader.SetBuffer(2, "_GroupScanBufferOut", _groupScanOutBuffer);

        _cullShader.SetBuffer(3, "_CompactBuffer", _compactBuffer);
        _cullShader.SetBuffer(3, "_SpawnBuffer", _spawnBuffer);
        _cullShader.SetBuffer(3, "_VoteBuffer", _voteBuffer);
        _cullShader.SetBuffer(3, "_ScanBuffer", _scanBuffer);
        _cullShader.SetBuffer(3, "_GroupScanBufferOut", _groupScanOutBuffer);
        for (int i = 0; i < _spawnMesh.Length; i++) 
        {
            _cullShader.SetBuffer(3, $"_ArgsBuffer{i}", _argsBuffer[i]);
            _cullShader.SetBuffer(4, $"_ArgsBuffer{i}", _argsBuffer[i]);
        }

        _chunkColor = UnityEngine.Random.ColorHSV(0, 1, 0, 1, 0.5f, 1, 0.5f, 1);
        _mpb.SetBuffer("_SpawnBuffer", _compactBuffer);
        _mpb.SetColor("_ChunkColor", _chunkColor);
        _mpb.SetVector("_Offset", new Vector4(ChunkBounds.center.x, ChunkBounds.center.z, 0, 0));
    }


    public void DrawIndirect()
    {
        if (
            _spawnMesh == null
            || _spawnMeshMaterial == null
            || _argsBuffer == null
            )
            return;

        _cullShader.SetMatrix("_Camera_P", _renderCam.projectionMatrix);
        _cullShader.SetMatrix("_Camera_V",_renderCam.transform.worldToLocalMatrix);
        _cullShader.Dispatch(4, 1, 1, 1);
        _cullShader.Dispatch(0, Mathf.CeilToInt(_spawnBuffer.count / 128f), 1, 1);
        _cullShader.Dispatch(1, _groupCount, 1, 1);
        _cullShader.Dispatch(2, 1, 1, 1);
        _cullShader.Dispatch(3, Mathf.CeilToInt(_spawnBuffer.count / 128f), 1, 1);

        float dist = Vector3.Distance(_renderCam.transform.position, ChunkBounds.center);
        if (dist < TileGrandCluster._LOD_Threshold_01)
        {
            Graphics.DrawMeshInstancedIndirect(_spawnMesh[0], 0, _spawnMeshMaterial, ChunkBounds, _argsBuffer[0],
          0, _mpb, UnityEngine.Rendering.ShadowCastingMode.On, true, 0, null, UnityEngine.Rendering.LightProbeUsage.BlendProbes);
            _mpb.SetColor("_LOD_Color", Color.green);

        }
        else if (dist >= TileGrandCluster._LOD_Threshold_01 && dist <= TileGrandCluster._LOD_Threshold_12)
        {
            Graphics.DrawMeshInstancedIndirect(_spawnMesh[1], 0, _spawnMeshMaterial, ChunkBounds, _argsBuffer[1],
         0, _mpb, UnityEngine.Rendering.ShadowCastingMode.On, true, 0, null, UnityEngine.Rendering.LightProbeUsage.BlendProbes);
            _mpb.SetColor("_LOD_Color", Color.blue);

        }
        else  if (dist > TileGrandCluster._LOD_Threshold_12)
        {
            Graphics.DrawMeshInstancedIndirect(_spawnMesh[2], 0, _spawnMeshMaterial, ChunkBounds, _argsBuffer[2],
           0, _mpb, UnityEngine.Rendering.ShadowCastingMode.On, true, 0, null, UnityEngine.Rendering.LightProbeUsage.BlendProbes);
            _mpb.SetColor("_LOD_Color", Color.yellow);

        }
    }
    

    public void ReleaseBuffer() 
    {
        _spawnBuffer?.Dispose();
        _voteBuffer?.Dispose();
        _scanBuffer?.Dispose();
        _groupScanInBuffer?.Dispose();
        _groupScanOutBuffer?.Dispose();
        _compactBuffer?.Dispose();
        if (_argsBuffer != null) 
            foreach (ComputeBuffer arg in _argsBuffer) 
                arg?.Dispose();
    
        _cullShader = null;
    }
}
