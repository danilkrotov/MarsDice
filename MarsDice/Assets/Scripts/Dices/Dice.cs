using System.Collections;
using UnityEngine;

public class Dice : MonoBehaviour
{
    [Header("Textures by dice value")]
    [SerializeField] private Texture2D face1Texture;
    [SerializeField] private Texture2D face2Texture;
    [SerializeField] private Texture2D face3Texture;
    [SerializeField] private Texture2D face4Texture;
    [SerializeField] private Texture2D face5Texture;
    [SerializeField] private Texture2D face6Texture;

    [Header("Грань которая приводит к провалу")]
    [SerializeField] private bool face1failed;
    [SerializeField] private bool face2failed;
    [SerializeField] private bool face3failed;
    [SerializeField] private bool face4failed;
    [SerializeField] private bool face5failed;
    [SerializeField] private bool face6failed;

    [Header("Roll settings")]
    [SerializeField] private float segmentDuration = 1f;
    [SerializeField] private Vector2 angularSpeedRange = new Vector2(360f, 900f);
    [SerializeField] private float faceOffset = 0.005f;
    [SerializeField] private float settleDuration = 0.15f;

    [Header("Подсветка грани при наведении")]
    [SerializeField] private bool enableFaceHoverHighlight = true;
    [SerializeField] private Color hoverHighlightColor = new Color(1f, 0.92f, 0.45f, 1f);
    [SerializeField] [Range(0f, 1f)] private float hoverBlend = 0.4f;
    [SerializeField] private float hoverRaycastMaxDistance = 80f;
    [SerializeField] private LayerMask hoverRaycastLayers = ~0;

    private readonly Vector3[] localFaceDirections =
    {
        Vector3.up,      // 1
        Vector3.down,    // 2
        Vector3.left,    // 3
        Vector3.right,   // 4
        Vector3.forward, // 5
        Vector3.back     // 6
    };
    private readonly string[] faceNames = { "1", "2", "3", "4", "5", "6" };
    private readonly GameObject[] faceVisuals = new GameObject[6];

    private readonly Color[] _faceBaseColors = new Color[6];
    private bool _faceColorsCached;
    private int _hoveredFaceIndex;

    private bool isRolling;
    public bool IsRolling => isRolling;
    public int LastResult { get; private set; } = 1;
    public bool LastFailed { get; private set; }

    private void Start()
    {
        ApplyFaceTextures();
    }

    private void LateUpdate()
    {
        if (!enableFaceHoverHighlight)
        {
            ClearFaceHover();
            return;
        }

        UpdateFaceHover();
    }

    private void OnDisable()
    {
        ClearFaceHover();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        ApplyFaceTextures();
    }

    public IEnumerator RollDice()
    {
        if (isRolling)
        {
            yield break;
        }

        isRolling = true;
        try
        {
            ClearFaceHover();
            yield return RotateForSegment();
            if (!IsAlive())
            {
                yield break;
            }

            yield return RotateForSegment();
            if (!IsAlive())
            {
                yield break;
            }

            yield return RotateForSegment();
            if (!IsAlive())
            {
                yield break;
            }

            int result = GetFaceTowardCamera();
            yield return AlignFaceToCamera(result);
            if (!IsAlive())
            {
                yield break;
            }

            LastResult = result;
            LastFailed = IsFaceFailed(result);
            Debug.Log($"Результат броска: {result}. Failed: {LastFailed}");
        }
        finally
        {
            if (IsAlive())
            {
                isRolling = false;
            }
        }
    }

    /// <summary>Unity «уничтоженный» объект даёт false, не бросая при проверке.</summary>
    private bool IsAlive()
    {
        return this != null;
    }

    public bool IsFaceFailed(int face)
    {
        switch (face)
        {
            case 1: return face1failed;
            case 2: return face2failed;
            case 3: return face3failed;
            case 4: return face4failed;
            case 5: return face5failed;
            case 6: return face6failed;
            default: return false;
        }
    }

    private IEnumerator RotateForSegment()
    {
        float elapsed = 0f;
        Vector3 axis = Random.onUnitSphere;
        float speed = Random.Range(angularSpeedRange.x, angularSpeedRange.y);

        while (elapsed < segmentDuration)
        {
            if (!IsAlive())
            {
                yield break;
            }

            transform.Rotate(axis, speed * Time.deltaTime, Space.World);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private int GetFaceTowardCamera()
    {
        if (!IsAlive())
        {
            return 1;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("Camera.main не найдена. Возвращаю 1 по умолчанию.");
            return 1;
        }

        Vector3 toCamera = (cam.transform.position - transform.position).normalized;
        float bestDot = float.NegativeInfinity;
        int bestFace = 1;

        for (int i = 0; i < localFaceDirections.Length; i++)
        {
            Vector3 worldDir = transform.TransformDirection(localFaceDirections[i]).normalized;
            float dot = Vector3.Dot(worldDir, toCamera);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestFace = i + 1;
            }
        }

        return bestFace;
    }

    private IEnumerator AlignFaceToCamera(int face)
    {
        if (!IsAlive())
        {
            yield break;
        }

        Camera cam = Camera.main;
        if (cam == null || face < 1 || face > localFaceDirections.Length)
        {
            yield break;
        }

        Vector3 toCamera = (cam.transform.position - transform.position).normalized;
        Vector3 faceWorldDirection = transform.TransformDirection(localFaceDirections[face - 1]).normalized;
        Quaternion targetRotation = Quaternion.FromToRotation(faceWorldDirection, toCamera) * transform.rotation;

        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;

        if (settleDuration <= 0f)
        {
            transform.rotation = targetRotation;
            yield break;
        }

        while (elapsed < settleDuration)
        {
            if (!IsAlive())
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        if (IsAlive())
        {
            transform.rotation = targetRotation;
        }
    }

    private void UpdateFaceHover()
    {
        if (isRolling)
        {
            ClearFaceHover();
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            ClearFaceHover();
            return;
        }

        if (!_faceColorsCached)
        {
            CacheFaceBaseColors();
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, hoverRaycastMaxDistance, hoverRaycastLayers, QueryTriggerInteraction.Ignore);
        int bestFace = -1;
        float bestDist = float.MaxValue;
        for (int h = 0; h < hits.Length; h++)
        {
            Transform t = hits[h].collider.transform;
            int face = GetFaceIndexFromTransform(t);
            if (face < 1 || face > 6)
            {
                continue;
            }

            if (!IsTransformUnderThisDice(t))
            {
                continue;
            }

            if (hits[h].distance < bestDist)
            {
                bestDist = hits[h].distance;
                bestFace = face;
            }
        }

        if (bestFace < 1)
        {
            ClearFaceHover();
            return;
        }

        SetHoveredFace(bestFace);
    }

    private static int GetFaceIndexFromTransform(Transform t)
    {
        if (t == null || !t.name.StartsWith("Face_"))
        {
            return -1;
        }

        string suffix = t.name.Substring("Face_".Length);
        if (!int.TryParse(suffix, out int idx) || idx < 1 || idx > 6)
        {
            return -1;
        }

        return idx;
    }

    private bool IsTransformUnderThisDice(Transform t)
    {
        while (t != null)
        {
            if (t == transform)
            {
                return true;
            }

            t = t.parent;
        }

        return false;
    }

    private void CacheFaceBaseColors()
    {
        for (int i = 0; i < 6; i++)
        {
            if (faceVisuals[i] == null)
            {
                continue;
            }

            Renderer r = faceVisuals[i].GetComponent<Renderer>();
            if (r == null)
            {
                _faceBaseColors[i] = Color.white;
                continue;
            }

            // На префабе и в OnValidate нельзя трогать .material — только sharedMaterial.
            Material mat = Application.isPlaying ? r.material : r.sharedMaterial;
            _faceBaseColors[i] = GetMaterialTint(mat);
        }

        _faceColorsCached = true;
    }

    private void SetHoveredFace(int faceIndex)
    {
        if (_hoveredFaceIndex == faceIndex)
        {
            return;
        }

        RestoreFaceColor(_hoveredFaceIndex);
        _hoveredFaceIndex = faceIndex;
        int i = faceIndex - 1;
        if (faceVisuals[i] == null)
        {
            return;
        }

        Renderer r = faceVisuals[i].GetComponent<Renderer>();
        if (r == null)
        {
            return;
        }

        Color baseC = _faceBaseColors[i];
        Color tinted = Color.Lerp(baseC, hoverHighlightColor, hoverBlend);
        Material instanceMat = r.material;
        SetMaterialTint(instanceMat, tinted);
    }

    private void RestoreFaceColor(int faceIndex)
    {
        if (faceIndex < 1 || faceIndex > 6)
        {
            return;
        }

        int i = faceIndex - 1;
        if (faceVisuals[i] == null)
        {
            return;
        }

        Renderer r = faceVisuals[i].GetComponent<Renderer>();
        if (r == null)
        {
            return;
        }

        Material instanceMat = r.material;
        SetMaterialTint(instanceMat, _faceBaseColors[i]);
    }

    private static Color GetMaterialTint(Material mat)
    {
        if (mat == null)
        {
            return Color.white;
        }

        if (mat.HasProperty("_Color"))
        {
            return mat.GetColor("_Color");
        }

        if (mat.HasProperty("_BaseColor"))
        {
            return mat.GetColor("_BaseColor");
        }

        if (mat.HasProperty("_TintColor"))
        {
            return mat.GetColor("_TintColor");
        }

        return Color.white;
    }

    private static void SetMaterialTint(Material mat, Color color)
    {
        if (mat == null)
        {
            return;
        }

        if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }

        if (mat.HasProperty("_TintColor"))
        {
            mat.SetColor("_TintColor", color);
        }
    }

    private static Shader ResolveFaceShader()
    {
        Shader s = Shader.Find("Sprites/Default");
        if (s != null)
        {
            return s;
        }

        s = Shader.Find("Unlit/Color");
        if (s != null)
        {
            return s;
        }

        s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s != null)
        {
            return s;
        }

        return Shader.Find("Unlit/Texture");
    }

    private static void ApplyTextureAndDefaultTint(Material mat, Texture2D texture)
    {
        if (mat == null)
        {
            return;
        }

        if (texture != null)
        {
            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", texture);
            }
            else if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", texture);
            }
            else
            {
                mat.mainTexture = texture;
            }
        }

        Color white = Color.white;
        if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", white);
        }

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", white);
        }

        if (mat.HasProperty("_TintColor"))
        {
            mat.SetColor("_TintColor", white);
        }
    }

    private void ClearFaceHover()
    {
        if (_hoveredFaceIndex == 0)
        {
            return;
        }

        RestoreFaceColor(_hoveredFaceIndex);
        _hoveredFaceIndex = 0;
    }

    private void ApplyFaceTextures()
    {
        ClearFaceHover();
        _faceColorsCached = false;

        Texture2D[] textures =
        {
            face1Texture,
            face2Texture,
            face3Texture,
            face4Texture,
            face5Texture,
            face6Texture
        };

        Renderer ownerRenderer = GetComponent<Renderer>();
        if (ownerRenderer == null)
        {
            return;
        }

        GetDiceBodyLocalBounds(ownerRenderer, out Vector3 centerLocal, out Vector3 sizeLocal);

        for (int i = 0; i < localFaceDirections.Length; i++)
        {
            EnsureFaceVisualExists(i);
            ConfigureFaceVisual(faceVisuals[i], localFaceDirections[i], centerLocal, sizeLocal, textures[i]);
        }

        CacheFaceBaseColors();
    }

    /// <summary>
    /// Локальный центр и размер тела кубика для раскладки граней.
    /// Нельзя брать Renderer.bounds.size и кормить в InverseTransformVector — это мировой AABB по осям мира;
    /// на повёрнутом кубе он «раздувается» и даёт несоразмерные Quad'ы (визуально «вытянутый» кубик).
    /// </summary>
    private void GetDiceBodyLocalBounds(Renderer ownerRenderer, out Vector3 centerLocal, out Vector3 sizeLocal)
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Bounds meshBounds = meshFilter.sharedMesh.bounds;
            centerLocal = meshBounds.center;
            sizeLocal = Vector3.Scale(meshBounds.size, transform.localScale);
            return;
        }

        Bounds worldBounds = ownerRenderer.bounds;
        centerLocal = transform.InverseTransformPoint(worldBounds.center);
        Vector3 lossy = transform.lossyScale;
        const float eps = 1e-5f;
        sizeLocal = new Vector3(
            worldBounds.size.x / Mathf.Max(Mathf.Abs(lossy.x), eps),
            worldBounds.size.y / Mathf.Max(Mathf.Abs(lossy.y), eps),
            worldBounds.size.z / Mathf.Max(Mathf.Abs(lossy.z), eps));
    }

    private void EnsureFaceVisualExists(int index)
    {
        if (faceVisuals[index] != null)
        {
            return;
        }

        Transform existing = transform.Find("Face_" + faceNames[index]);
        if (existing != null)
        {
            faceVisuals[index] = existing.gameObject;
            return;
        }

        GameObject face = GameObject.CreatePrimitive(PrimitiveType.Quad);
        face.name = "Face_" + faceNames[index];
        face.transform.SetParent(transform, false);

        Collider col = face.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
        }

        faceVisuals[index] = face;
    }

    private void ConfigureFaceVisual(GameObject face, Vector3 localDirection, Vector3 centerLocal, Vector3 sizeLocal, Texture2D texture)
    {
        Vector3 absDir = new Vector3(Mathf.Abs(localDirection.x), Mathf.Abs(localDirection.y), Mathf.Abs(localDirection.z));
        float halfDepth = Vector3.Scale(sizeLocal, absDir).magnitude * 0.5f;

        face.transform.localPosition = centerLocal + localDirection * (halfDepth + faceOffset);
        // У Quad лицевая сторона направлена в противоположную сторону,
        // поэтому разворачиваем, чтобы текстура смотрела наружу куба.
        face.transform.localRotation = Quaternion.LookRotation(-localDirection);

        Vector2 sideSize = GetSideSize(localDirection, sizeLocal);
        face.transform.localScale = new Vector3(sideSize.x, sideSize.y, 1f);

        Renderer faceRenderer = face.GetComponent<Renderer>();
        if (faceRenderer == null)
        {
            return;
        }

        Shader shader = ResolveFaceShader();
        Material mat = faceRenderer.sharedMaterial;
        if (mat == null || mat.shader != shader)
        {
            mat = new Material(shader);
            faceRenderer.sharedMaterial = mat;
        }

        ApplyTextureAndDefaultTint(mat, texture);

        Collider hoverCol = face.GetComponent<Collider>();
        if (hoverCol != null)
        {
            hoverCol.enabled = true;
        }
    }

    private static Vector2 GetSideSize(Vector3 dir, Vector3 size)
    {
        if (Mathf.Abs(dir.x) > 0.5f)
        {
            return new Vector2(size.z, size.y);
        }

        if (Mathf.Abs(dir.y) > 0.5f)
        {
            return new Vector2(size.x, size.z);
        }

        return new Vector2(size.x, size.y);
    }
}
