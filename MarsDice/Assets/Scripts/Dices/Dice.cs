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

    private bool isRolling;
    public bool IsRolling => isRolling;
    public int LastResult { get; private set; } = 1;
    public bool LastFailed { get; private set; }

    private void Start()
    {
        ApplyFaceTextures();
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
        yield return RotateForSegment();
        yield return RotateForSegment();
        yield return RotateForSegment();

        int result = GetFaceTowardCamera();
        yield return AlignFaceToCamera(result);
        LastResult = result;
        LastFailed = IsFaceFailed(result);
        Debug.Log($"Результат броска: {result}. Failed: {LastFailed}");
        isRolling = false;
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
            transform.Rotate(axis, speed * Time.deltaTime, Space.World);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private int GetFaceTowardCamera()
    {
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
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        transform.rotation = targetRotation;
    }

    private void ApplyFaceTextures()
    {
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

        Vector3 centerLocal = transform.InverseTransformPoint(ownerRenderer.bounds.center);
        Vector3 sizeLocal = transform.InverseTransformVector(ownerRenderer.bounds.size);

        for (int i = 0; i < localFaceDirections.Length; i++)
        {
            EnsureFaceVisualExists(i);
            ConfigureFaceVisual(faceVisuals[i], localFaceDirections[i], centerLocal, sizeLocal, textures[i]);
        }
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
            col.enabled = false;
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

        Shader shader = Shader.Find("Unlit/Texture");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material mat = faceRenderer.sharedMaterial;
        if (mat == null || mat.shader != shader)
        {
            mat = new Material(shader);
            faceRenderer.sharedMaterial = mat;
        }

        mat.mainTexture = texture;
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
