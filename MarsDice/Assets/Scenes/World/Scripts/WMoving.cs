using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WMoving : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private Camera _camera;
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private float _breadcrumbSpacing = 1.25f;
    [SerializeField] private float _breadcrumbScale = 0.25f;
    [SerializeField] private float _endCubeScale = 0.6f;
    [SerializeField] private float _endLabelHeight = 1.2f;
    [SerializeField] private float _navMeshSampleRadius = 2.0f;

    [Header("Input")]
    [SerializeField] private LayerMask _clickMask = ~0; // по умолчанию всё, включая Terrain Collider

    [Header("Orientation")]
    [SerializeField] private bool _preserveInitialRotation = true; // держим X/Z как в сцене (-90/90 и т.п.)
    [SerializeField] private bool _agentUpdatesRotation = false; // NavMeshAgent не должен менять rotation
    [SerializeField] private float _turnSmoothing = 12f; // скорость поворота по Y при движении
    [SerializeField] private float _modelYawCorrectionDeg = 0f; // если едет боком — поставь +90 или -90

    private readonly List<GameObject> _breadcrumbs = new List<GameObject>();
    private GameObject _endCube;
    private TextMesh _endDistanceText;
    private NavMeshPath _plannedPath;

    private Button _goButton;
    private bool _isMoving;

    private const string UiRootName = "WMoving_UI";

    private Quaternion _initialRotation;
    private Vector3 _initialEuler;

    [Header("Camera follow (on Go)")]
    [SerializeField] private WCameraController _cameraController;
    [SerializeField] private Transform _cameraFollowTarget;
    [SerializeField] private bool _followCameraWhileMoving = true;
    [SerializeField] private Vector3 _cameraFollowOffset = Vector3.zero;

    private void Awake()
    {
        _initialRotation = transform.rotation;
        _initialEuler = _initialRotation.eulerAngles;
        if (_camera == null) _camera = Camera.main;
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        _plannedPath = new NavMeshPath();

        if (_agent != null)
            _agent.updateRotation = _agentUpdatesRotation;

        if (_cameraController == null && _camera != null)
            _cameraController = _camera.GetComponent<WCameraController>();
    }

    private void Start()
    {
        EnsureUi();
        SetGoButtonInteractable(false);
    }

    private void Update()
    {
        if (_camera == null || _agent == null) return;

        if (!_isMoving && Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            TryPlanPathFromClick();
        }
    }

    private void LateUpdate()
    {
        if (_agent == null) return;

        if (_isMoving)
        {
            // Поворачиваем только по Y, чтобы “ехать носом вперёд”, но при этом сохраняем X/Z как у модели в сцене.
            var v = _agent.velocity;
            v.y = 0f;
            if (v.sqrMagnitude > 0.0004f)
            {
                var desiredYaw = Quaternion.LookRotation(v.normalized, Vector3.up).eulerAngles.y;
                var currentYaw = GetRawYaw();
                var newYaw = Mathf.LerpAngle(currentYaw, desiredYaw, Time.deltaTime * _turnSmoothing);
                ApplyYawRaw(newYaw);
                return;
            }
        }

        if (_preserveInitialRotation)
        {
            // Даже если агент где-то “дёрнул” rotation — возвращаем как было в сцене (с тем же Y).
            ApplyYawRaw(GetRawYaw());
        }
    }

    private float GetRawYaw()
    {
        // Текущий yaw без добавленной коррекции (иначе будет "убегать" и крутиться по кругу)
        return Mathf.Repeat(transform.rotation.eulerAngles.y - _modelYawCorrectionDeg + 360f, 360f);
    }

    private void ApplyYawRaw(float rawWorldYawDeg)
    {
        var worldYawDeg = rawWorldYawDeg + _modelYawCorrectionDeg;

        if (!_preserveInitialRotation)
        {
            var e = transform.rotation.eulerAngles;
            transform.rotation = Quaternion.Euler(e.x, worldYawDeg, e.z);
            return;
        }

        // Сохраняем исходные X/Z (как у -90/90), меняем только мировой Y
        transform.rotation = Quaternion.Euler(_initialEuler.x, worldYawDeg, _initialEuler.z);
    }

    private void TryPlanPathFromClick()
    {
        var ray = _camera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 5000f, _clickMask, QueryTriggerInteraction.Ignore))
            return;

        if (!NavMesh.SamplePosition(hit.point, out var navHit, _navMeshSampleRadius, NavMesh.AllAreas))
            return;

        if (!_agent.CalculatePath(navHit.position, _plannedPath))
            return;

        if (_plannedPath.status != NavMeshPathStatus.PathComplete || _plannedPath.corners == null || _plannedPath.corners.Length < 2)
        {
            ClearPlannedVisuals();
            SetGoButtonInteractable(false);
            return;
        }

        BuildPlannedVisuals(_plannedPath);
        SetGoButtonInteractable(true);
    }

    private void BuildPlannedVisuals(NavMeshPath path)
    {
        ClearPlannedVisuals();

        var corners = path.corners;
        var totalDistance = CalculatePathLength(corners);

        // Хлебные крошки: равномерно вдоль сегментов
        var distCarry = 0f;
        for (int i = 0; i < corners.Length - 1; i++)
        {
            var a = corners[i];
            var b = corners[i + 1];
            var seg = Vector3.Distance(a, b);
            if (seg <= 0.0001f) continue;

            var dir = (b - a) / seg;
            var t = 0f;

            if (distCarry > 0f)
            {
                var firstStep = _breadcrumbSpacing - distCarry;
                if (firstStep < seg)
                {
                    t = firstStep;
                    SpawnBreadcrumb(a + dir * t);
                }
                else
                {
                    distCarry += seg;
                    distCarry %= _breadcrumbSpacing;
                    continue;
                }
            }

            for (t += _breadcrumbSpacing; t < seg; t += _breadcrumbSpacing)
            {
                SpawnBreadcrumb(a + dir * t);
            }

            distCarry = (distCarry + seg) % _breadcrumbSpacing;
        }

        // Куб на конце
        var end = corners[corners.Length - 1];
        _endCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _endCube.name = "WMoving_EndMarker";
        _endCube.transform.position = end + Vector3.up * (_endCubeScale * 0.5f);
        _endCube.transform.localScale = Vector3.one * _endCubeScale;

        var cubeRenderer = _endCube.GetComponent<Renderer>();
        if (cubeRenderer != null)
        {
            var mat = cubeRenderer.material;
            mat.color = new Color(1f, 0.92f, 0.2f, 1f);
        }

        // Текст расстояния над кубом
        var textGo = new GameObject("WMoving_EndDistance");
        textGo.transform.position = end + Vector3.up * _endLabelHeight;
        _endDistanceText = textGo.AddComponent<TextMesh>();
        _endDistanceText.text = $"{totalDistance:0.0} m";
        _endDistanceText.characterSize = 0.12f;
        _endDistanceText.fontSize = 80;
        _endDistanceText.anchor = TextAnchor.MiddleCenter;
        _endDistanceText.alignment = TextAlignment.Center;
        _endDistanceText.color = Color.white;

        // Повернуть текст к камере (один раз; в большинстве случаев достаточно)
        if (_camera != null)
        {
            var lookPos = _camera.transform.position;
            lookPos.y = textGo.transform.position.y;
            textGo.transform.LookAt(lookPos);
            textGo.transform.Rotate(0f, 180f, 0f);
        }
    }

    private void SpawnBreadcrumb(Vector3 position)
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = "WMoving_Breadcrumb";
        s.transform.position = position + Vector3.up * (_breadcrumbScale * 0.5f);
        s.transform.localScale = Vector3.one * _breadcrumbScale;

        var r = s.GetComponent<Renderer>();
        if (r != null)
        {
            var mat = r.material;
            mat.color = Color.white;
        }

        _breadcrumbs.Add(s);
    }

    private static float CalculatePathLength(Vector3[] corners)
    {
        var total = 0f;
        for (int i = 0; i < corners.Length - 1; i++)
        {
            total += Vector3.Distance(corners[i], corners[i + 1]);
        }
        return total;
    }

    private void ClearPlannedVisuals()
    {
        for (int i = 0; i < _breadcrumbs.Count; i++)
        {
            if (_breadcrumbs[i] != null) Destroy(_breadcrumbs[i]);
        }
        _breadcrumbs.Clear();

        if (_endCube != null) Destroy(_endCube);
        _endCube = null;

        if (_endDistanceText != null) Destroy(_endDistanceText.gameObject);
        _endDistanceText = null;
    }

    private void EnsureUi()
    {
        // EventSystem
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // Canvas root
        var existing = GameObject.Find(UiRootName);
        if (existing != null)
        {
            _goButton = existing.GetComponentInChildren<Button>(true);
            if (_goButton != null)
            {
                _goButton.onClick.RemoveListener(OnGoClicked);
                _goButton.onClick.AddListener(OnGoClicked);
            }
            return;
        }

        var uiRoot = new GameObject(UiRootName);
        var canvas = uiRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiRoot.AddComponent<CanvasScaler>();
        uiRoot.AddComponent<GraphicRaycaster>();

        // Button
        var btnGo = new GameObject("GoButton");
        btnGo.transform.SetParent(uiRoot.transform, false);

        var btnImage = btnGo.AddComponent<Image>();
        btnImage.color = new Color(0.12f, 0.65f, 0.18f, 0.95f);

        var button = btnGo.AddComponent<Button>();
        button.onClick.AddListener(OnGoClicked);
        _goButton = button;

        var rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 24f);
        rt.sizeDelta = new Vector2(260f, 70f);

        // Button text
        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(btnGo.transform, false);
        var text = txtGo.AddComponent<Text>();
        text.text = "Поехали";
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.fontSize = 28;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }

    private void OnGoClicked()
    {
        if (_isMoving) return;
        if (_agent == null) return;
        if (_plannedPath == null || _plannedPath.corners == null || _plannedPath.corners.Length < 2) return;

        if (_plannedPath.status != NavMeshPathStatus.PathComplete) return;

        _isMoving = true;
        SetGoButtonInteractable(false);

        if (_followCameraWhileMoving && _cameraController != null)
        {
            var target = _cameraFollowTarget != null ? _cameraFollowTarget : transform;
            _cameraController.BeginFollow(target, _cameraFollowOffset, disableEdgeScroll: true);
        }

        // Удаляем визуализацию пути при старте движения
        ClearPlannedVisuals();

        // Едем по рассчитанному пути
        _agent.ResetPath();
        _agent.SetPath(_plannedPath);

        StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        // Ждём пока агент примет путь
        while (_agent.pathPending)
            yield return null;

        // Движение до конца
        while (true)
        {
            if (_agent.pathPending)
            {
                yield return null;
                continue;
            }

            if (!_agent.hasPath)
                break;

            if (_agent.remainingDistance <= _agent.stoppingDistance)
            {
                if (!_agent.isStopped && (_agent.velocity.sqrMagnitude <= 0.01f))
                    break;
            }

            yield return null;
        }

        _agent.ResetPath();
        _isMoving = false;

        if (_cameraController != null && _cameraController.IsFollowing)
            _cameraController.EndFollow(enableEdgeScroll: true);

        // Теперь можно снова кликать и строить новый маршрут
        SetGoButtonInteractable(false);
        _plannedPath.ClearCorners();
    }

    private void SetGoButtonInteractable(bool value)
    {
        if (_goButton != null) _goButton.interactable = value && !_isMoving;
    }
}
