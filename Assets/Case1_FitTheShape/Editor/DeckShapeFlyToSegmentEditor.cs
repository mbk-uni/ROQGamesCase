using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShapeController))]
public sealed class DeckShapeFlyToSegmentEditor : Editor
{
    private const int CurveSamples = 32;
    private static readonly Color CurveColor = new(0.1f, 0.9f, 1f, 1f);
    private static readonly Color GuideColor = new(0.1f, 0.9f, 1f, 0.3f);

    private void OnSceneGUI()
    {
        var flight = (ShapeController)target;
        if (flight.TargetAnchor == null)
            return;

        var start = flight.transform.position;
        var end = flight.TargetAnchor.position;
        var control = flight.GetCurveControlPoint();

        DrawGuide(start, control);
        DrawGuide(control, end);
        DrawCurve(start, control, end);

        Handles.color = CurveColor;
        var sceneCamera = SceneView.currentDrawingSceneView?.camera;
        var handleNormal = sceneCamera != null ? sceneCamera.transform.forward : Vector3.forward;
        Handles.DrawSolidDisc(control, handleNormal, HandleUtility.GetHandleSize(control) * 0.08f);
        Handles.Label(control, " Flight curve handle");
        Handles.Label(start, " Start");
        Handles.Label(end, " Target");

        EditorGUI.BeginChangeCheck();
        var newControl = Handles.PositionHandle(control, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(flight, "Edit Shape Flight Curve");
            flight.SetCurveControlPoint(newControl);
            EditorUtility.SetDirty(flight);
        }
    }

    private static void DrawGuide(Vector3 from, Vector3 to)
    {
        Handles.color = GuideColor;
        Handles.DrawDottedLine(from, to, 4f);
    }

    private static void DrawCurve(Vector3 start, Vector3 control, Vector3 end)
    {
        var points = new Vector3[CurveSamples + 1];
        for (var index = 0; index <= CurveSamples; index++)
        {
            var progress = index / (float)CurveSamples;
            var inverseProgress = 1f - progress;
            points[index] = inverseProgress * inverseProgress * start
                          + 2f * inverseProgress * progress * control
                          + progress * progress * end;
        }

        Handles.color = CurveColor;
        Handles.DrawAAPolyLine(4f, points);
    }
}
