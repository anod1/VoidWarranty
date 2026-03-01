using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using VoidWarranty.Core;
using VoidWarranty.Interaction;
using VoidWarranty.Player;

namespace SubSurface.Puzzle
{
    /// <summary>
    /// Logique de dessin locale sur tableau blanc via RenderTexture.
    /// Click gauche = dessine, click droit = efface.
    /// Fonctionne en permanence : si le joueur tient le marqueur et que
    /// le raycast caméra touche le board, il peut dessiner directement.
    /// Pas besoin de FocusPointInteraction.
    ///
    /// SETUP ÉDITEUR :
    /// → Attacher sur le GO du quad (BoardQuad)
    /// → Le quad DOIT avoir un BoxCollider fin (pas de MeshCollider)
    ///   UV calculé manuellement depuis la position locale du hit
    /// → Le parent doit avoir un BoxCollider pour la physique/grab
    /// → Inspector : _boardRenderer = MeshRenderer du quad
    /// → Inspector : _boardCollider = BoxCollider du quad
    /// → Inspector : _sync = WhiteboardSync sur le parent
    /// → Inspector : _requiredItemId = ID de l'item marqueur requis (ex: "marker")
    /// </summary>
    public class WhiteboardDrawing : MonoBehaviour, IInteractable
    {
        [Header("Board")]
        [SerializeField] private MeshRenderer _boardRenderer;
        [SerializeField] private Collider _boardCollider;

        [Header("Prompt")]
        [SerializeField] private string _missingItemPromptKey = "FEEDBACK_MARKER_NEEDED";

        [Header("Texture")]
        [SerializeField] private int _textureWidth = 1024;
        [SerializeField] private int _textureHeight = 1024;
        [SerializeField] private Color _clearColor = new Color(0.95f, 0.95f, 0.92f, 1f);

        [Header("Brush (draw)")]
        [SerializeField] private float _brushRadius = 0.005f;
        [SerializeField] private Color _brushColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        [SerializeField] private int _brushTextureSize = 32;

        [Header("Eraser")]
        [Tooltip("Rayon de la gomme (plus gros que le brush)")]
        [SerializeField] private float _eraserRadius = 0.02f;

        [Header("Item requis")]
        [Tooltip("ID de l'item à tenir en main pour dessiner (vide = pas de restriction)")]
        [SerializeField] private string _requiredItemId = "marker";

        [Header("Network")]
        [SerializeField] private WhiteboardSync _sync;

        private RenderTexture _renderTexture;
        private Texture2D _brushTexture;
        private Texture2D _eraserTexture;
        private Material _drawMaterial;
        private Material _eraserMaterial;
        private Camera _mainCamera;

        private bool _isDrawing;
        private bool _isErasing;
        private Vector2 _lastUv;
        private int _nextStrokeId;
        private List<Vector2> _pendingPoints;

        // Rayon actif (brush ou eraser selon le mode)
        private float _activeRadius;

        // Throttle : n'envoie un point que si l'UV a bougé suffisamment
        private Vector2 _lastSentUv;
        private float _sendThreshold;

        /// <summary>True si le joueur local possède le marqueur requis en main (visible).</summary>
        private bool HasRequiredItem
        {
            get
            {
                if (string.IsNullOrEmpty(_requiredItemId)) return true;
                var inventory = PlayerInventory.LocalInstance;
                if (inventory == null) return false;
                // Bloqué si le grab est actif (objet porté → clic gauche = brandish, pas dessin)
                if (inventory.IsGrabActive) return false;
                return inventory.EquippedItemId == _requiredItemId;
            }
        }

        // =====================================================================
        // IInteractable — prompt contextuel
        // =====================================================================

        public void Interact(GameObject interactor) { }

        public string GetInteractionPrompt()
        {
            if (HasRequiredItem)
                return string.Empty; // Marker en main → pas de prompt, dessine direct

            // Pas de marker → feedback gris
            string msg = LocalizationManager.Get(_missingItemPromptKey);
            return $"<color=#888888>{msg}</color>";
        }

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            CreateRenderTexture();
            CreateBrushTextures();
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

            if (_eraserTexture != null)
                Destroy(_eraserTexture);

            if (_drawMaterial != null)
                Destroy(_drawMaterial);

            if (_eraserMaterial != null)
                Destroy(_eraserMaterial);
        }

        private void Update()
        {
            // Pas de marker en main → pas de dessin
            if (!HasRequiredItem)
            {
                if (_isDrawing) EndStroke();
                if (_isErasing) EndStroke();
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null) return;

            if (_mainCamera == null)
                _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            bool leftDown = mouse.leftButton.isPressed;
            bool rightDown = mouse.rightButton.isPressed;

            // Raycast depuis le centre de l'écran (crosshair)
            Ray ray = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            // ~0 = tous les layers y compris Ignore Raycast (layer 2)
            if (Physics.Raycast(ray, out RaycastHit hit, 5f, ~0)
                && hit.collider == _boardCollider)
            {
                // UV calculé manuellement : Quad local space (-0.5..+0.5) → UV (0..1)
                Vector3 local = _boardCollider.transform.InverseTransformPoint(hit.point);
                Vector2 uv = new Vector2(local.x + 0.5f, local.y + 0.5f);
                uv.x = Mathf.Clamp01(uv.x);
                uv.y = Mathf.Clamp01(uv.y);

                if (leftDown && !rightDown)
                {
                    // Draw mode
                    if (_isErasing) EndStroke();
                    HandleStroke(uv, false);
                }
                else if (rightDown && !leftDown)
                {
                    // Erase mode
                    if (_isDrawing) EndStroke();
                    HandleStroke(uv, true);
                }
                else
                {
                    // Rien ou les deux → arrêter
                    if (_isDrawing || _isErasing) EndStroke();
                }
            }
            else
            {
                if (_isDrawing || _isErasing) EndStroke();
            }

            FlushPendingPoints();
        }

        // =====================================================================
        // Stroke handling (draw ou erase)
        // =====================================================================

        private void HandleStroke(Vector2 uv, bool erasing)
        {
            bool isActive = erasing ? _isErasing : _isDrawing;
            float radius = erasing ? _eraserRadius : _brushRadius;

            if (!isActive)
            {
                // Début du trait
                if (erasing)
                    _isErasing = true;
                else
                    _isDrawing = true;

                _activeRadius = radius;
                _lastUv = uv;
                _lastSentUv = uv;
                _pendingPoints.Clear();
                _pendingPoints.Add(uv);

                StampAt(uv, erasing);

                // colorIndex: 0 = draw, 1 = erase
                byte colorIndex = (byte)(erasing ? 1 : 0);
                if (_sync != null)
                    _sync.CmdBeginStroke(_nextStrokeId, colorIndex, radius);
            }
            else
            {
                // Continuer le trait
                DrawLineAt(_lastUv, uv, erasing);
                _lastUv = uv;

                float threshold = erasing ? _eraserRadius * 0.3f : _sendThreshold;
                if (Vector2.Distance(uv, _lastSentUv) >= threshold)
                {
                    _pendingPoints.Add(uv);
                    _lastSentUv = uv;
                }
            }
        }

        // =====================================================================
        // Public API (appelé par WhiteboardSync pour les traits distants)
        // =====================================================================

        /// <summary>Dessine un segment distant sur le board (temps réel).</summary>
        public void DrawRemoteSegment(Vector2 from, Vector2 to, float brushRadius, bool erasing)
        {
            float savedRadius = _activeRadius;
            _activeRadius = brushRadius;
            DrawLineAt(from, to, erasing);
            _activeRadius = savedRadius;
        }

        /// <summary>Rejoue un trait complet (late-joiner).</summary>
        public void ReplayStroke(Vector2[] points, float brushRadius, bool erasing)
        {
            if (points == null || points.Length == 0) return;

            _activeRadius = brushRadius;

            StampAt(points[0], erasing);
            for (int i = 1; i < points.Length; i++)
                DrawLineAt(points[i - 1], points[i], erasing);
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

        private void StampAt(Vector2 uv, bool erasing)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _renderTexture;

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, _textureWidth, _textureHeight, 0);

            float radius = erasing ? _eraserRadius : _activeRadius;
            float px = uv.x * _textureWidth;
            float py = (1f - uv.y) * _textureHeight;
            float r = radius * _textureWidth;

            Rect brushRect = new Rect(px - r, py - r, r * 2f, r * 2f);
            Material mat = erasing ? _eraserMaterial : _drawMaterial;
            Texture2D tex = erasing ? _eraserTexture : _brushTexture;
            Graphics.DrawTexture(brushRect, tex, mat);

            GL.PopMatrix();
            RenderTexture.active = prev;
        }

        private void DrawLineAt(Vector2 from, Vector2 to, bool erasing)
        {
            float radius = erasing ? _eraserRadius : _activeRadius;
            float dist = Vector2.Distance(from, to);
            float step = radius * 0.5f;

            if (dist < 0.0001f)
            {
                StampAt(to, erasing);
                return;
            }

            int steps = Mathf.CeilToInt(dist / step);
            for (int i = 0; i <= steps; i++)
            {
                float t = steps > 0 ? (float)i / steps : 1f;
                Vector2 point = Vector2.Lerp(from, to, t);
                StampAt(point, erasing);
            }
        }

        // =====================================================================
        // Stroke lifecycle
        // =====================================================================

        private void EndStroke()
        {
            _isDrawing = false;
            _isErasing = false;
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

        private void CreateBrushTextures()
        {
            int size = _brushTextureSize;
            float center = size * 0.5f;

            // --- Brush dessin : cercle soft (alpha graduel) ---
            _brushTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float t = Mathf.Clamp01(1f - dist / center);
                    t = t * t * (3f - 2f * t); // SmoothStep falloff
                    _brushTexture.SetPixel(x, y, new Color(1f, 1f, 1f, t));
                }
            }
            _brushTexture.Apply();

            // --- Brush gomme : cercle hard (alpha = 1 dans le disque) ---
            _eraserTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float a = dist <= center ? 1f : 0f;
                    _eraserTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            _eraserTexture.Apply();

            // --- Materials ---

            // Dessin : encre sombre, alpha-blend normal
            Shader brushShader = Shader.Find("Hidden/Internal-GUITextureClip");
            _drawMaterial = new Material(brushShader);
            _drawMaterial.color = _brushColor;

            // Gomme : shader custom qui écrase les pixels (Blend One Zero + clip)
            Shader eraserShader = Shader.Find("Hidden/EraserUnlit");
            _eraserMaterial = new Material(eraserShader);
            _eraserMaterial.color = _clearColor;
        }
    }
}
