using UnityEngine;
using UnityEngine.EventSystems;

public class WCameraController : MonoBehaviour
{
    [Header("Edge scroll")]
    [SerializeField] private bool _edgeScrollEnabled = true;
    [SerializeField] private float _edgeBorderPx = 18f;
    [SerializeField] private float _edgeMoveSpeed = 12f;
    [SerializeField] private float _edgeSmoothTime = 0.12f;
    [SerializeField] private bool _disableEdgeScrollOverUI = true;

    [Header("Zoom (mouse wheel)")]
    [SerializeField] private bool _zoomEnabled = true;
    [SerializeField] private float _minHeight = 6f;
    [SerializeField] private float _maxHeight = 40f;
    [SerializeField] private float _scrollStep = 2f;
    [SerializeField] private float _zoomSmoothTime = 0.10f;

    [Header("Follow target")]
    [SerializeField] private bool _followEnabled;
    [SerializeField] private Transform _followTarget;
    [SerializeField] private Vector3 _followOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private float _followLerp = 10f;
    [SerializeField] private bool _centerFollowTargetInView = true;

    private Vector3 _edgeVelocity;
    private float _heightVelocity;
    private Camera _cam;

    private void Awake()
    {
        _cam = GetComponentInChildren<Camera>();
        if (_cam == null) _cam = Camera.main;
    }

    public bool EdgeScrollEnabled
    {
        get => _edgeScrollEnabled;
        set => _edgeScrollEnabled = value;
    }

    public bool IsFollowing => _followEnabled && _followTarget != null;

    public void BeginFollow(Transform target, Vector3? offset = null, bool disableEdgeScroll = true)
    {
        _followTarget = target;
        _followEnabled = target != null;
        if (offset.HasValue) _followOffset = offset.Value;
        if (disableEdgeScroll) _edgeScrollEnabled = false;
    }

    public void EndFollow(bool enableEdgeScroll = true)
    {
        _followEnabled = false;
        _followTarget = null;
        if (enableEdgeScroll) _edgeScrollEnabled = true;
    }

    private void Update()
    {
        ApplyZoom();

        if (!_edgeScrollEnabled) return;
        if (_disableEdgeScrollOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        var delta = GetEdgeScrollDelta();
        if (delta.sqrMagnitude <= 0.000001f)
        {
            _edgeVelocity = Vector3.SmoothDamp(_edgeVelocity, Vector3.zero, ref _edgeVelocity, _edgeSmoothTime);
            return;
        }

        var desired = delta * _edgeMoveSpeed;
        var smooth = Vector3.SmoothDamp(_edgeVelocity, desired, ref _edgeVelocity, _edgeSmoothTime);
        transform.position += smooth * Time.deltaTime;

        ClampHeight();
    }

    private void LateUpdate()
    {
        if (!_followEnabled || _followTarget == null) return;

        // Следуем по XZ, сохраняя текущую высоту камеры (Y) и не “приближаясь в упор”.
        var desired = _followTarget.position + _followOffset;
        desired.y = transform.position.y;

        if (_centerFollowTargetInView)
        {
            var delta = GetViewportCenteringDeltaXZ(_followTarget.position);
            desired += delta;
        }

        var t = 1f - Mathf.Exp(-_followLerp * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);

        ClampHeight();
    }

    private Vector3 GetViewportCenteringDeltaXZ(Vector3 targetWorldPos)
    {
        if (_cam == null) return Vector3.zero;

        // Считаем, куда сейчас “смотрит” центр экрана на плоскости земли цели,
        // и сдвигаем камеру так, чтобы эта точка совпала с целью.
        var plane = new Plane(Vector3.up, new Vector3(0f, targetWorldPos.y, 0f));
        var ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!plane.Raycast(ray, out var enter)) return Vector3.zero;

        var hit = ray.GetPoint(enter);
        var delta = targetWorldPos - hit;
        delta.y = 0f;
        return delta;
    }

    private void ApplyZoom()
    {
        if (!_zoomEnabled) return;
        if (_disableEdgeScrollOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        var wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) <= 0.0001f) return;

        // В Unity: wheel > 0 обычно “вверх” (приблизить). Мы уменьшаем высоту, чтобы приблизить.
        var targetY = transform.position.y - wheel * _scrollStep;
        targetY = Mathf.Clamp(targetY, _minHeight, _maxHeight);

        var y = Mathf.SmoothDamp(transform.position.y, targetY, ref _heightVelocity, _zoomSmoothTime);
        var p = transform.position;
        p.y = y;
        transform.position = p;
    }

    private void ClampHeight()
    {
        var p = transform.position;
        var clamped = Mathf.Clamp(p.y, _minHeight, _maxHeight);
        if (!Mathf.Approximately(p.y, clamped))
        {
            p.y = clamped;
            transform.position = p;
        }
    }

    private Vector3 GetEdgeScrollDelta()
    {
        var mp = Input.mousePosition;
        var w = Screen.width;
        var h = Screen.height;
        var b = Mathf.Max(0f, _edgeBorderPx);

        float x = 0f;
        float z = 0f;

        if (mp.x <= b) x = -EdgeFactor(mp.x, b);
        else if (mp.x >= w - b) x = EdgeFactor(w - mp.x, b);

        if (mp.y <= b) z = -EdgeFactor(mp.y, b);
        else if (mp.y >= h - b) z = EdgeFactor(h - mp.y, b);

        // направление берём из ориентации камеры/трансформа, но движение держим по земле (XZ)
        var right = transform.right;
        right.y = 0f;
        right.Normalize();

        var forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        return right * x + forward * z;
    }

    private static float EdgeFactor(float distToEdgePx, float borderPx)
    {
        if (borderPx <= 0.0001f) return 1f;
        var t = Mathf.Clamp01(1f - (distToEdgePx / borderPx)); // 0 в середине бордера, 1 на самом краю
        return t;
    }
}
