using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Planet))]
public class PlanetEditor : Editor
{
    Planet planet;
    Editor shapeSettingsEditor;
    Editor colorSettingsEditor;
    Editor caveSettingsEditor;

    public override void OnInspectorGUI()
    {
        using var check = new EditorGUI.ChangeCheckScope();

        base.OnInspectorGUI();
        
        DrawSettingsEditor(planet.shapeSettings, ref planet.shapeSettingsFoldout, ref shapeSettingsEditor);
        DrawSettingsEditor(planet.caveSettings, ref planet.caveSettingsFoldout, ref caveSettingsEditor); 
    }

    void DrawSettingsEditor(Object settings, ref bool foldout, ref Editor editor)
    {
        if (settings == null) return;

        foldout = EditorGUILayout.InspectorTitlebar(foldout, settings);

        using var check = new EditorGUI.ChangeCheckScope();

        if (foldout)
        {
            CreateCachedEditor(settings, null, ref editor);
            editor.OnInspectorGUI();
        }
    }

    private void OnEnable()
    {
        planet = (Planet)target;
    }
}