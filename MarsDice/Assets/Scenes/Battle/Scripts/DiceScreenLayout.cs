using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Раскладка 3D-кубиков в мировом пространстве: центр экрана (луч из камеры через 0.5,0.5 viewport),
/// по горизонтали экрана (ось camera.right), группа по центру как выравнивание текста.
/// </summary>
public static class DiceScreenLayout
{
    /// <param name="distanceFromCamera">Расстояние от камеры до плоскости раскладки (ViewportToWorldPoint z).</param>
    /// <param name="horizontalSpacing">Расстояние между центрами соседних кубиков вдоль camera.right.</param>
    public static void LayoutTransformsCentered(Camera camera, float distanceFromCamera, float horizontalSpacing, IReadOnlyList<Transform> transforms)
    {
        if (camera == null || transforms == null)
        {
            return;
        }

        List<Transform> list = new List<Transform>(transforms.Count);
        for (int i = 0; i < transforms.Count; i++)
        {
            if (transforms[i] != null)
            {
                list.Add(transforms[i]);
            }
        }

        if (list.Count == 0)
        {
            return;
        }

        Vector3 center = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distanceFromCamera));
        Vector3 right = camera.transform.right;
        int n = list.Count;
        float totalWidth = (n - 1) * horizontalSpacing;
        float startOffset = -0.5f * totalWidth;

        for (int i = 0; i < n; i++)
        {
            list[i].position = center + right * (startOffset + i * horizontalSpacing);
        }
    }

    public static void LayoutDiceCenteredOnScreen(Camera camera, float distanceFromCamera, float horizontalSpacing, IReadOnlyList<Dice> dices)
    {
        if (dices == null)
        {
            return;
        }

        List<Transform> list = new List<Transform>(dices.Count);
        for (int i = 0; i < dices.Count; i++)
        {
            if (dices[i] != null)
            {
                list.Add(dices[i].transform);
            }
        }

        LayoutTransformsCentered(camera, distanceFromCamera, horizontalSpacing, list);
    }
}
