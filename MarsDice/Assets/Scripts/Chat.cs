using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chat : MonoBehaviour
{
    private const int MaxLines = 10;

    // IMGUI отрисовка читает этот список, а другие скрипты через Chat.Push() вносят туда события.
    private static readonly List<string> Lines = new List<string>(MaxLines);

    private GUIStyle _style;

    private void Awake()
    {
        if (Lines.Count > MaxLines)
        {
            Lines.RemoveRange(0, Lines.Count - MaxLines);
        }
    }

    public static void Push(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        string msg = message.Trim();
        Lines.Add(msg);
        while (Lines.Count > MaxLines)
        {
            Lines.RemoveAt(0);
        }
    }

    private void OnGUI()
    {
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white },
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
        }

        float lineHeight = 20f;
        float padding = 12f;
        float x = padding;
        float y = Screen.height - padding - lineHeight * MaxLines;
        float w = Mathf.Min(Screen.width * 0.5f, 520f);
        w = Mathf.Max(200f, w);
        float h = lineHeight * MaxLines;

        // Полупрозрачный фон под чатом.
        Rect bgRect = new Rect(x - 6f, y - 4f, w + 12f, h + 8f);
        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.Box(bgRect, GUIContent.none);
        GUI.color = prev;

        int startIndex = Mathf.Max(0, Lines.Count - MaxLines);
        int shown = Lines.Count - startIndex;

        // Рендерим ровно 10 строк (пустые сверху) — так визуально стабильнее.
        for (int i = 0; i < MaxLines; i++)
        {
            int lineIdx = startIndex + i;
            string text = i < shown ? Lines[lineIdx] : string.Empty;
            GUI.Label(new Rect(x, y + lineHeight * i, w, lineHeight), text, _style);
        }
    }
}
