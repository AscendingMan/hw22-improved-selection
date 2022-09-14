using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TerrainTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using ColorUtility = UnityEngine.ColorUtility;

[ExecuteAlways]
[ExecuteInEditMode]
public class SelectionBase : MonoBehaviour
{
    static class Styles
    {
        public static Color32 s_RootHoverColor;
        public static Color32 s_ChildHoverColor;

        static Styles()
        {
            s_RootHoverColor = Color.yellow;//new Color32(0x8c, 0x64, 0xcc, 0xFF);
            s_ChildHoverColor = Color.white; //new Color32(0xde, 0x58, 0xe0, 0xFF);
        }
    }

    enum SelectionState
    {
        HoverRoot,
        HoverInContext,
        HoverWithModifier,
        SelectedItem
    }

    private SelectionState selectionState;
    private HoverState m_HoverState;
    private GameObject m_LastHovered;

    
    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUISelectionHighlight;
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.hierarchyWindowItemOnGUI -= HierarchyWindowItemOnGUI;
        EditorApplication.hierarchyWindowItemOnGUI += HierarchyWindowItemOnGUI;
        m_HoverState = new HoverState();
    }

    private void HierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
    {
        if (!selectionRect.Contains(Event.current.mousePosition))
            return;
        
        m_HoverState.m_HoveredChildObject = null;
        m_HoverState.m_HoveredObjects?.Clear();
        
        var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (!go)
            return;

        if (go.transform.parent != null)
            m_HoverState.m_HoveredChildObject = go;
        else
            m_HoverState.m_HoveredObjects = new List<GameObject>(){go};
    }

    void OnSelectionChanged()
    {
        if (Selection.objects.Length == 0)
        {
            m_HoverState.Clear();
            selectionState = SelectionState.HoverRoot;
        }

        m_HoverState.m_HoveredSibilingObject = null;
    }
    
    private float pulseTime = 0.0f;
    
    public void OnSceneGUISelectionHighlight(SceneView sceneView)
    {
        sceneView.autoRepaintOnSceneChange = true;

        var currentEvent = Event.current;
        if (currentEvent.type == EventType.Repaint)
        {
            if (m_HoverState.m_HoveredSibilingObject != null) 
                Handles.DrawOutline(new []{m_HoverState.m_HoveredSibilingObject}, Color.cyan, pulseTime);
            if (m_HoverState.m_HoveredChildObject != null)
                Handles.DrawOutline(new []{m_HoverState.m_HoveredChildObject}, Styles.s_ChildHoverColor, pulseTime);
            if(m_HoverState.m_HoveredObjects?.Count > 0) 
                Handles.DrawOutline(m_HoverState.m_HoveredObjects, Styles.s_RootHoverColor, pulseTime);
        }

        if (currentEvent.type == EventType.Layout || currentEvent.type == EventType.Repaint)
            return;
        
        var hoveredGO = HandleUtility.PickGameObject(currentEvent.mousePosition, true);
        // if (hoveredGO == m_LastHovered)
        // {
        //     pulseTime = Mathf.Sin(Time.realtimeSinceStartup * 10) * 0.25f;
        //     EditorApplication.QueuePlayerLoopUpdate();
        // }
        // else
        //     pulseTime = 0;

        m_LastHovered = hoveredGO;
        
        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
        {
            if(currentEvent.clickCount == 2 || IsUsingModifiers(currentEvent))
                selectionState = SelectionState.HoverInContext;
            else
                selectionState = SelectionState.SelectedItem;
            
            if (m_HoverState.m_SibilingObjects != null && !m_HoverState.m_SibilingObjects.Contains(hoveredGO))
            {
                m_HoverState.m_SibilingObjects?.Clear();
            }
        }
        else if (currentEvent.type == EventType.MouseMove)
        {
            selectionState = IsUsingModifiers(currentEvent)
                ? SelectionState.HoverWithModifier
                : SelectionState.HoverRoot;
        }

        if (hoveredGO == null)
        {
            m_HoverState.Clear();
            return;
        }
        
        Transform p;
        // Handle states
        switch (selectionState)
        {
            case SelectionState.HoverRoot:
                
                if (m_HoverState.m_SibilingObjects != null && m_HoverState.m_SibilingObjects.Contains(hoveredGO))
                    m_HoverState.m_HoveredSibilingObject = hoveredGO;
                
                m_HoverState.m_HoveredParentObject = hoveredGO.transform.root.gameObject;
                m_HoverState.m_HoveredChildObject = null;
                m_HoverState.m_HoveredObjects = hoveredGO.transform.root.GetComponentsInChildren<Transform>().Select(x => x.gameObject).ToList();
                m_HoverState.m_HoveredObjects.Remove(m_HoverState.m_HoveredSibilingObject);

                if (Selection.activeGameObject != null)
                {
                    p = Selection.activeGameObject.transform.parent;
                    if (p == null)
                    {
                        for (int i = 0; i < Selection.activeGameObject.transform.childCount; i++)
                        {
                            m_HoverState.m_HoveredObjects.Remove(Selection.activeGameObject.transform.GetChild(i).gameObject);
                        }
                    }
                    
                    m_HoverState.m_HoveredObjects.Remove(Selection.activeGameObject);
                }
                
                break;
            case SelectionState.HoverWithModifier:
                m_HoverState.m_HoveredParentObject = null;
                m_HoverState.m_HoveredChildObject = hoveredGO;
                m_HoverState.m_HoveredObjects?.Clear();
                break;
            case SelectionState.HoverInContext:
                m_HoverState.m_SibilingObjects = new List<GameObject>();
                Transform parent = hoveredGO.transform.parent;
                if (parent != null)
                {
                    for (int i = 0; i < parent.childCount; i++)
                    {
                        m_HoverState.m_SibilingObjects.Add(parent.GetChild(i).gameObject);
                    }
                }
                break;
            case SelectionState.SelectedItem:
                if (m_HoverState.m_SibilingObjects != null && m_HoverState.m_SibilingObjects.Contains(hoveredGO))
                {
                    Selection.activeGameObject = hoveredGO;
                }
                else if (m_HoverState.m_HoveredSibilingObject == null)
                {
                    m_HoverState.m_HoveredObjects = hoveredGO.transform.root.GetComponentsInChildren<Transform>().Select(x => x.gameObject).ToList();
                    Selection.activeGameObject = hoveredGO.transform.root.gameObject;
                    int controlId = GUIUtility.GetControlID(FocusType.Passive);
                    GUIUtility.hotControl = controlId;
                    Event.current.Use();
                }
                break;
        }
    }

    bool IsUsingModifiers(Event evt)
    {
        if (evt.modifiers == EventModifiers.Control)
            return true;
        
        // Don't use CTRL/CMD for now
        // if (Application.platform == RuntimePlatform.OSXEditor && evt.modifiers == EventModifiers.Command)
        //     return true;
        // if (Application.platform != RuntimePlatform.OSXEditor && evt.modifiers == EventModifiers.Control)
        //     return true;

        return false;
    }
}
