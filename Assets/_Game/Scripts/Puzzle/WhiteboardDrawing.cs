using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace SubSurface.Puzzle
{
    /// <summary>
    /// Logique de dessin locale sur tableau blanc via RenderTexture.
    /// Click gauche maintenu = dessine, relâché = arrête.
    /// Les traits sont envoyés à WhiteboardSync pour la synchronisation réseau.
    ///
    /// SETUP ÉDITEUR :
    /// → Attacher sur le GO du quad (BoardQuad)
    /// → Le quad DOIT avoir un MeshCollider (requis pour hit.textureCoord)
    /// → Inspector : _boardRenderer = MeshRenderer du quad
    /// → Inspector : _boardCollider = MeshCollider du quad
    /// → Inspector : _sync = WhiteboardSync sur le parent
    /// </summary>
    public class WhiteboardDrawing : MonoBehaviour
    {
        [Header("Board")]
        [SerializeField] private MeshRenderer _boardRenderer;
        [SerializeField] private MeshCollider _boardCollider;

        [Header("Texture")]
        [SerializeField] private int _textureWidth = 1024;
        [SerializeField] private int _textureHeight = 1024;
        [SerializeField] private Color _clearColor = new Color(0.05f, 0.15f, 0.05f, 1f);

        [Header("Brush")]
        [SerializeField] private float _brushRadius = 0.005f;
        [SerializeField] private Color _brushColor = Color.white;
        [SerializeField] private int _brushTextureSize = 32;

        [Header("Network")]
        [SerializeField] private WhiteboardSync _sync;

        private RenderTexture _renderTexture;
        private Texture2D _brushTexture;
        private Material _brushMaterial;
        private Camera _mainCamera;

        private bool _isActive;
        private bool _isDrawing;
        private Vector2 _lastUv;
        private int _nextStrokeId;
        private List<Vector2> _pendingPoints;

        // Throttle : n'envoie un point que si l'UV a bougé suffisamment
        private Vector2 _lastSentUv;
        private float _sendThreshold;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            CreateRenderTexture();
            CreateBrushTexture();
            _sendThreshold = _brushRadius * 0.3f;
            _pendingPoints = new List<Vector2>();
        }

        private void OnDestroy()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }

            if (_brushTexture != null)
                Destroy(_brushTexture);

            if (_brushMaterial != null)
                Destroy(_brushMaterial);
        }

        private void Update()
        {
            if (!_isActive) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            if (_mainCamera == null)
                _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            bool leftDown = mouse.leftButton.isPressed;
            Vector2 screenPos = mouse.position.ReadValue();

            Ray ray = _mainCamera.ScreenPointToRay(screenPos);

            if (Physics.Raycast(ray, out RaycastHit hit, 5f) && hit.collider == _boardCollider)
            {
                Vector2 uv = hit.textureCoord;

                if (leftDown)
                {
                    if (!_isDrawing)
                    {
                        // Pen down — début d'un trait
                        _isDrawing = true;
                        _lastUv = uv;
                        _lastSentUv = uv;
                        _pendingPoints.Clear();
                        _pendingPoints.Add(uv);

                        StampBrush(uv);

                        if (_sync != null)
                            _sync.CmdBeginStroke(_nextStrokeId, 0, _brushRadius);
                    }
                    else
                    {
                        // Pen move — continuer le trait
                        DrawLineTo(_lastUv, uv);
                        _lastUv = uv;

                        // Throttle : n'ajoute au batch que si assez de mouvement
                        if (Vector2.Distance(uv, _lastSentUv) >= _sendThreshold)
                        {
                            _pendingPoints.Add(uv);
                            _lastSentUv = uv;
                        }
                    }
                }
                else if (_isDrawing)
                {
                    EndStroke();
                }
            }
            else if (_isDrawing && !leftDown)
            {
                EndStroke();
            }

            // Envoyer le batch de points accumulés ce frame
            FlushPendingPoints();
        }

        // =====================================================================
        // Activation (appelé par FocusPointInteraction via UnityEvent)
        // =====================================================================

        /// <summary>Active/désactive le mode dessin.</summary>
        public void SetDrawingActive(bool active)
        {
            _isActive = active;

            if (!active && _isDrawing)
                EndStroke();
        }

        // =====================================================================
        // Public API (appelé par WhiteboardSync pour les traits distants)
        // =====================================================================

        /// <summary>Dessine un segment distant sur le board (temps réel).</summary>
        public void DrawRemoteSegment(Vector2 from, Vector2 to, float brushRadius)
        {
            float savedRadius = _brushRadius;
            _brushRadius = brushRadius;
            DrawLineTo(from, to);
            _brushRadius = savedRadius;
        }

        /// <summary>Rejoue un trait complet (late-joiner).</summary>
        public void ReplayStroke(Vector2[] points, float brushRadius)
        {
            if (points == null || points.Length == 0) return;

            float savedRadius = _brushRadius;
            _brushRadius = brushRadius;

            StampBrush(points[0]);
            for (int i = 1; i < points.Length; i++)
                DrawLineTo(points[i - 1], points[i]);

            _brushRadius = savedRadius;
        }

        /// <summary>Efface le tableau.</summary>
        public void ClearBoard()
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _renderTexture;
            GL.Clear(true, true, _clearColor);
            RenderTexture.active = prev;
        }

        // =====================================================================
        // Drawing internals
        // =====================================================================

        private void StampBrush(Vector2 uv)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _renderTexture;

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, _textureWidth, _textureHeight, 0);

            float px = uv.x * _textureWidth;
            float py = (1f - uv.y) * _textureHeight;
            float r = _brushRadius * _textureWidth;

            Rect brushRect = new Rect(px - r, py - r, r * 2f, r * 2f);
            Graphics.DrawTexture(brushRect, _brushTexture, _brushMaterial);

            GL.PopMatrix();
            RenderTexture.active = prev;
        }

        private void DrawLineTo(Vector2 from, Vector2 to)
        {
            float dist = Vector2.Distance(from, to);
            float step = _brushRadius * 0.5f;

            if (dist < 0.0001f)
            {
                StampBrush(to);
                return;
            }

            int steps = Mathf.CeilToInt(dist / step);
            for (int i = 0; i <= steps; i++)
            {
                float t = steps > 0 ? (float)i / steps : 1f;
                Vector2 point = Vector2.Lerp(from, to, t);
                StampBrush(point);
            }
        }

        // =====================================================================
        // Stroke lifecycle
        // =====================================================================

        private void EndStroke()
        {
            _isDrawing = false;
            FlushPendingPoints();

            if (_sync != null)
                _sync.CmdEndStroke(_nextStrokeId);

            _nextStrokeId++;
        }

        private void FlushPendingPoints()
        {
            if (_pendingPoints.Count == 0 || _sync == null) return;

            Vector2[] batch = _pendingPoints.ToArray();
            _pendingPoints.Clear();
            _sync.CmdAddPoints(_nextStrokeId, batch);
        }

        // =====================================================================
        // Texture setup
        // =====================================================================

        private void CreateRenderTexture()
        {
            _renderTexture = new RenderTexture(_textureWidth, _textureHeight, 0, RenderTextureFormat.ARGB32);
            _renderTexture.filterMode = FilterMode.Bilinear;
            _renderTexture.Create();

            // Clear to board color
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _renderTexture;
            GL.Clear(true, true, _clearColor);
            RenderTexture.active = prev;

            // Assign to material (clone pour ne pas modifier l'asset)
            if (_boardRenderer != null)
            {
                _boardRenderer.material = new Material(_boardRenderer.sharedMaterial);
                _boardRenderer.material.mainTexture = _renderTexture;
            }
        }

        private void CreateBrushTexture()
        {
            int size = _brushTextureSize;
            _brushTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            float center = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float t = Mathf.Clamp01(1f - dist / center);
                    // Smooth falloff pour un trait doux
                    t = t * t * (3f - 2f * t);
                    _brushTexture.SetPixel(x, y, new Color(1f, 1f, 1f, t));
                }
            }

            _brushTexture.Apply();

            // Material pour le stamp avec blending alpha
            _brushMaterial = new Material(Shader.Find("Hidden/Internal-GUITextureClip"));
            _brushMaterial.color = _brushColor;
        }
    }
}
