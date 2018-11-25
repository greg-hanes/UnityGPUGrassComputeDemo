using UnityEngine;

public class ComputeInt2Parameter
{
    private ComputeShader m_shader;
    private string m_name;
    private int m_valueX;
    private int m_valueY;

    private bool m_dirty = true;

    public int ValueX
    {
        get { return m_valueX; }
        set
        {
            if (value != m_valueX)
            {
                m_valueX = value;
                m_dirty = true;
            }
        }
    }

    public int ValueY
    {
        get { return m_valueY; }
        set
        {
            if (value != m_valueY)
            {
                m_valueY = value;
                m_dirty = true;
            }
        }
    }

    public ComputeInt2Parameter(ComputeShader shader, string name, int valueX, int valueY)
    {
        m_shader = shader;
        m_name = name;
        m_valueX = valueX;
        m_valueY = valueY;
    }

    public void Apply()
    {
        if (m_dirty)
        {
            m_shader.SetInts(m_name, m_valueX, m_valueY);
            m_dirty = false;
        }
    }
}

public class ComputeIntParameter
{
    private ComputeShader m_shader;
    private string m_name;
    private int m_value;

    private bool m_dirty = true;

    public int Value
    {
        get { return m_value; }
        set
        {
            if (value != m_value)
            {
                m_value = value;
                m_dirty = true;
            }
        }
    }

    public ComputeIntParameter(ComputeShader shader, string name, int value)
    {
        m_shader = shader;
        m_name = name;
        m_value = value;
    }

    public void Apply()
    {
        if (m_dirty)
        {
            m_shader.SetInt(m_name, m_value);
            m_dirty = false;
        }
    }
}

public class ComputeFloat2Parameter
{
    private ComputeShader m_shader;
    private string m_name;
    private Vector2 m_value;

    private bool m_dirty = true;

    public Vector2 Value
    {
        get { return m_value; }
        set
        {
            if (value != m_value)
            {
                m_value = value;
                m_dirty = true;
            }
        }
    }

    public ComputeFloat2Parameter(ComputeShader shader, string name, Vector2 value)
    {
        m_shader = shader;
        m_name = name;
        m_value = value;
    }

    public void Apply()
    {
        if (m_dirty)
        {
            m_shader.SetFloats(m_name, m_value.x, m_value.y);
            m_dirty = false;
        }
    }
}

public class ComputeFloatParameter
{
    private ComputeShader m_shader;
    private string m_name;
    private float m_value;

    private bool m_dirty = true;

    public float Value
    {
        get { return m_value; }
        set
        {
            if (value != m_value)
            {
                m_value = value;
                m_dirty = true;
            }
        }
    }

    public ComputeFloatParameter(ComputeShader shader, string name, float value)
    {
        m_shader = shader;
        m_name = name;
        m_value = value;
    }

    public void Apply()
    {
        if (m_dirty)
        {
            m_shader.SetFloat(m_name, m_value);
            m_dirty = false;
        }
    }
}

public class GrassRenderer : MonoBehaviour
{
    [SerializeField]
    private ComputeShader _computeShader;

    [SerializeField]
    private float _grassLength = 3;

    // Grass Geometry Generation:
    private int _gridSamplerKernel;
    private int _gridSizeX = 1024;
    private int _gridSizeY = 1024;
    private ComputeBuffer _grassPointBuffer;
    private ComputeBuffer _countBuffer;

    private int _grassPointsCount;

    private ComputeFloat2Parameter param_WorldPos;
    private ComputeFloatParameter param_Offset;
    private ComputeInt2Parameter param_GridDimensions;
    private ComputeFloat2Parameter param_GridWorldDimensions;
    private ComputeFloat2Parameter param_CenterPoint;


    // Physics Simulation:
    [SerializeField]
    private bool _windEnabled = true;

    [SerializeField]
    private Renderer _debugDisplacementRenderer;

    const int kTextureSizeX = 1024;
    const int kTextureSizeY = 1024;

    private uint _threadGroupSizeX;
    private uint _threadGroupSizeY;

    private ComputeFloatParameter param_dt;
    private ComputeFloatParameter param_K;
    private ComputeFloatParameter param_Damping;
    private ComputeFloat2Parameter param_WindDirection;
    private ComputeFloatParameter param_WindTime;
    private ComputeFloatParameter param_WindForce;
    private ComputeFloatParameter param_ImpulseForce;
    private ComputeFloatParameter param_Density;
    private ComputeFloat2Parameter param_movemenDelta;

    private int _updatePhysicsKernel;
    private RenderTexture _texDisplacement;
    private RenderTexture _texVelocity;
    private RenderTexture _externalForces;

    private RenderTexture _texDisplacementOut;
    private RenderTexture _texVelocityOut;
    private Texture2D _texImpulse;
    private Vector2 _lastPosition;


    // Draw Grass Geometry:
    [SerializeField]
    private Shader _grassGeometryShader;

    private Material _grassGeoMaterial;

    void Start()
    {
        InitParameters();
        InitGeometry();
        InitPhysics();
        InitGrassDrawing();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }

        param_dt.Value = Time.deltaTime;
        param_dt.Apply();
        UpdateGeometry();
        UpdatePhysics();
    }

    void OnRenderObject()
    {
        Debug.Log(Time.frameCount);

        Matrix4x4 mvp = Camera.current.projectionMatrix * Camera.current.worldToCameraMatrix;

        _grassGeoMaterial.SetTexture("displacementTex", _texDisplacement);
        _grassGeoMaterial.SetMatrix("mvp", mvp);
        _grassGeoMaterial.SetVector("worldPositionOffset", new Vector2(transform.position.x, transform.position.z));
        _grassGeoMaterial.SetVector("worldDimensions", new Vector2(transform.localScale.x * 10, transform.localScale.z * 10));
        _grassGeoMaterial.SetFloat("grassLength", _grassLength);
        _grassGeoMaterial.SetPass(0);

        ComputeBuffer.CopyCount(_grassPointBuffer, _countBuffer, 0);
        Graphics.DrawProceduralIndirect(MeshTopology.Points, _countBuffer);
    }

    void OnGUI()
    {
        GUISlider("K", param_K, 0.0f, 150.0f);
        GUISlider("Damping", param_Damping, 0.0f, 100.0f);
        GUISlider("WindForce", param_WindForce, 0.0f, 50.0f);
        GUISlider("Impulse Force", param_ImpulseForce, 0.0f, 25.0f);
        GUISlider("Density", param_Density, 0.0f, 1.0f);
        GUISlider("Random Offset", param_Offset, 0.0f, 1.0f);

        GUILayout.Label("Count: " + _grassPointsCount);
    }

    void OnDestroy()
    {
        Release(ref _texDisplacement);
        Release(ref _texVelocity);
        Release(ref _externalForces);
        Release(ref _texDisplacementOut);
        Release(ref _texVelocityOut);
        Release(ref _countBuffer);
        Release(ref _grassPointBuffer);
    }


    private void InitParameters()
    {
        param_WorldPos = new ComputeFloat2Parameter(_computeShader, "worldPos", new Vector2(transform.position.x, transform.position.z));
        param_dt = new ComputeFloatParameter(_computeShader, "dt", Time.deltaTime);
        param_K = new ComputeFloatParameter(_computeShader, "k", 144);
        param_Damping = new ComputeFloatParameter(_computeShader, "damping", 16);
        param_WindDirection = new ComputeFloat2Parameter(_computeShader, "windDirection", Vector2.one.normalized);
        param_WindTime = new ComputeFloatParameter(_computeShader, "windTime", 0);
        param_WindForce = new ComputeFloatParameter(_computeShader, "windForce", 25);
        param_ImpulseForce = new ComputeFloatParameter(_computeShader, "impulseForce", 10f);
        param_Density = new ComputeFloatParameter(_computeShader, "density", 0.15f);
        param_Offset = new ComputeFloatParameter(_computeShader, "offsetMultiplier", 0.33f);
        param_GridDimensions = new ComputeInt2Parameter(_computeShader, "gridDimensions", _gridSizeX, _gridSizeY);
        param_GridWorldDimensions = new ComputeFloat2Parameter(_computeShader, "gridWorldSize", new Vector2(transform.localScale.x * 10, transform.localScale.z * 10));
        param_CenterPoint = new ComputeFloat2Parameter(_computeShader, "centerPoint", new Vector2(transform.position.x, transform.position.z));
        param_movemenDelta = new ComputeFloat2Parameter(_computeShader, "movementOffset", new Vector2(0, 0));
    }

    private void InitGeometry()
    {
        _grassPointBuffer = new ComputeBuffer(_gridSizeX * _gridSizeY, 8, ComputeBufferType.Counter);
        _countBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
        int[] args = new int[] { 0, 1, 0, 0 };
        _countBuffer.SetData(args);

        _gridSamplerKernel = _computeShader.FindKernel("UniformGridSampler");
        _computeShader.SetBuffer(_gridSamplerKernel, "grassPoints", _grassPointBuffer);
    }

    private void CreateImpulseTexture()
    {
        _texImpulse = new Texture2D(256, 256, TextureFormat.RGBAHalf, false, true);

        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                // pos = [-1,1]
                Vector3 pos = new Vector3((x - 128) / 128.0f, 0, -(y - 128) / 128.0f);

                float distance = pos.magnitude;

                if (distance >= 1.0f)
                {
                    _texImpulse.SetPixel(x, y, new Color(0, 0, 0, 1.0f));
                    continue;
                }

                float a = 500;
                float b = 0.0f;
                float c = 0.2f;

                float distanceMinusB = distance - b;
                float f = a * Mathf.Exp(-(distanceMinusB * distanceMinusB) / (2 * c * c));
                pos.Normalize();
                Vector3 impulse = f * pos;
                // f(x) = a*e^(  -(x-b)^2 / 2c^2   )
                //  x = sample location
                //  a = height
                //  b = center
                //  c = width

                _texImpulse.SetPixel(x, y, new Color(impulse.x, -impulse.y, impulse.z, 1.0f));
            }
        }

        _texImpulse.Apply();
    }

    private void InitPhysics()
    {
        _updatePhysicsKernel = _computeShader.FindKernel("UpdatePhysicalModel");
        uint threadGroupSizeZ;
        _computeShader.GetKernelThreadGroupSizes(_updatePhysicsKernel, out _threadGroupSizeX, out _threadGroupSizeY, out threadGroupSizeZ);
        _texDisplacement = new RenderTexture(kTextureSizeX, kTextureSizeY, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        _texDisplacement.enableRandomWrite = true;
        _texDisplacement.Create();

        _texVelocity = new RenderTexture(kTextureSizeX, kTextureSizeY, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        _texVelocity.enableRandomWrite = true;
        _texVelocity.Create();

        _externalForces = new RenderTexture(kTextureSizeX, kTextureSizeY, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        _externalForces.enableRandomWrite = true;
        _externalForces.Create();

        _texDisplacementOut = new RenderTexture(kTextureSizeX, kTextureSizeY, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        _texDisplacementOut.enableRandomWrite = true;
        _texDisplacementOut.Create();

        _texVelocityOut = new RenderTexture(kTextureSizeX, kTextureSizeY, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        _texVelocityOut.enableRandomWrite = true;
        _texVelocityOut.Create();

        _computeShader.SetFloats("texelsPerMeter", kTextureSizeX / (10 * transform.localScale.x), kTextureSizeY / (10 * transform.localScale.z));

        _debugDisplacementRenderer.sharedMaterial.SetTexture("_MainTex", _texDisplacement);

        _lastPosition = new Vector2(transform.position.x, transform.position.z);

        CreateImpulseTexture();
    }

    private void InitGrassDrawing()
    {
        _grassGeoMaterial = new Material(_grassGeometryShader);
        _grassGeoMaterial.SetBuffer("grassVerts", _grassPointBuffer);
    }


    private void UpdateGeometry()
    {
        param_CenterPoint.Value = new Vector2(transform.position.x, transform.position.z);
        param_GridWorldDimensions.Value = new Vector2(transform.localScale.x * 10, transform.localScale.z * 10);

        param_Density.Apply();
        param_Offset.Apply();
        param_GridWorldDimensions.Apply();
        param_GridDimensions.Apply();
        param_CenterPoint.Apply();

        _grassPointBuffer.SetCounterValue(0);
        _computeShader.Dispatch(_gridSamplerKernel, _gridSizeX / 32, _gridSizeY / 32, 1);

        using (var countBuffer = new ComputeBuffer(1, 16, ComputeBufferType.IndirectArguments))
        {
            ComputeBuffer.CopyCount(_grassPointBuffer, countBuffer, 0);
            var count = new int[4];
            countBuffer.GetData(count);
            _grassPointsCount = count[0];
        }
    }

    private void UpdatePhysics()
    {
        if (Input.GetMouseButton(0))
            OnClick();

        param_WindTime.Value += Time.deltaTime;

        Vector2 curPos = new Vector2(transform.position.x, transform.position.z);
        Vector2 deltaPos = curPos - _lastPosition;
        _lastPosition = curPos;

        param_WorldPos.Value = curPos;
        param_WorldPos.Apply();

        _computeShader.SetInt("windEnabled", _windEnabled ? 1 : 0);
        param_movemenDelta.Value = deltaPos;
        param_movemenDelta.Apply();
        param_WindTime.Apply();
        param_Damping.Apply();
        param_WindForce.Apply();
        param_ImpulseForce.Apply();
        param_K.Apply();
        param_WindDirection.Apply();

        _computeShader.SetFloats("texelsPerMeter", kTextureSizeX / (10 * transform.localScale.x), kTextureSizeY / (10 * transform.localScale.z));
        _computeShader.SetTexture(_updatePhysicsKernel, "texDisplacementIn", _texDisplacement);
        _computeShader.SetTexture(_updatePhysicsKernel, "texVelocityIn", _texVelocity);
        _computeShader.SetTexture(_updatePhysicsKernel, "texExternalForces", _externalForces);
        _computeShader.SetTexture(_updatePhysicsKernel, "texDisplacement", _texDisplacementOut);
        _computeShader.SetTexture(_updatePhysicsKernel, "texVelocity", _texVelocityOut);

        _computeShader.Dispatch(_updatePhysicsKernel, kTextureSizeX / (int)_threadGroupSizeX, kTextureSizeY / (int)_threadGroupSizeY, 1);

        RenderTexture tmp = _texDisplacement;
        _texDisplacement = _texDisplacementOut;
        _texDisplacementOut = tmp;

        tmp = _texVelocity;
        _texVelocity = _texVelocityOut;
        _texVelocityOut = tmp;

        var active = RenderTexture.active;
        RenderTexture.active = _externalForces;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = active;
    }


    private void OnClick()
    {
        RaycastHit info;

        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out info))
        {
            Vector2 uv = info.textureCoord;
            var activeTarget = RenderTexture.active;
            RenderTexture.active = _externalForces;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, _externalForces.width, 0, _externalForces.height);

            Vector2 rPos = new Vector2(uv.x * _externalForces.width, uv.y * _externalForces.height) - new Vector2(_texImpulse.width / 2, _texImpulse.height / 2);
            Vector2 rSize = new Vector2(_texImpulse.width, _texImpulse.height);
            Rect r = new Rect(rPos, rSize);
            Graphics.DrawTexture(r, _texImpulse);
            GL.PopMatrix();
            RenderTexture.active = activeTarget;
        }
    }


    private static void GUISlider(string label, ComputeFloatParameter param, float min, float max)
    {
        GUILayout.Label(label + ": " + param.Value);
        param.Value = GUILayout.HorizontalSlider(param.Value, min, max, GUILayout.Width(400));
    }

    private static void Release(ref RenderTexture rt)
    {
        rt.Release();
        rt = null;
    }

    private static void Release(ref ComputeBuffer cb)
    {
        cb.Release();
        cb = null;
    }
}

