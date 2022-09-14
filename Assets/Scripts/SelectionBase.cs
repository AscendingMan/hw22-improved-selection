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
        public static Color s_RootHoverColor;
        public static Color s_ChildHoverColor;
        public static Color s_ContextHoverColor;

        static Styles()
        {
            s_RootHoverColor = Color.yellow;
            s_ChildHoverColor = Color.white;
            s_ContextHoverColor = Color.cyan;
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
    private EditorWindow m_HierarchyWindow;

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUISelectionHighlight;
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.hierarchyWindowItemOnGUI -= HierarchyWindowItemOnGUI;
        EditorApplication.hierarchyWindowItemOnGUI += HierarchyWindowItemOnGUI;
        m_HoverState = new HoverState();
        m_HierarchyWindow = Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(win => win.titleContent.text == "Hierarchy");
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
        
        Debug.Log(selectionRect);
    }

    void OnSelectionChanged()
    {
        if (Selection.objects.Length == 0)
        {
            m_HoverState.Clear();
            selectionState = SelectionState.HoverRoot;
        }

        m_HoverState.m_HoveredSiblingObject = null;
    }
    
    private float pulseTime = 0.0f;
    
    public void OnSceneGUISelectionHighlight(SceneView sceneView)
    {
        sceneView.autoRepaintOnSceneChange = true;

        var currentEvent = Event.current;
        if (currentEvent.type == EventType.Repaint)
        {
            if (m_HoverState.m_HoveredSiblingObject != null) 
                Handles.DrawOutline(new []{m_HoverState.m_HoveredSiblingObject}, Styles.s_ContextHoverColor, pulseTime);
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
            
            if (m_HoverState.m_SiblingObjects != null && !m_HoverState.m_SiblingObjects.Contains(hoveredGO))
            {
                m_HoverState.m_SiblingObjects?.Clear();
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
                
                if (m_HoverState.m_SiblingObjects != null && m_HoverState.m_SiblingObjects.Contains(hoveredGO))
                    m_HoverState.m_HoveredSiblingObject = hoveredGO;
                
                m_HoverState.m_HoveredChildObject = null;
                if(Selection.activeGameObject == null)
                    m_HoverState.m_HoveredObjects = hoveredGO.transform.root.GetComponentsInChildren<Transform>().Select(x => x.gameObject).ToList();
                else
                    m_HoverState.m_HoveredObjects = hoveredGO.transform.root.GetComponentsInChildren<Transform>()
                    .Where(x => x.transform != Selection.activeGameObject.transform && !x.transform.IsChildOf(Selection.activeGameObject.transform))
                    .Select(y => y.gameObject)
                    .ToList();

                //  remove any objects that are drawn with an in-context outline
                if (m_HoverState.m_HoveredSiblingObject != null)
                {
                    m_HoverState.m_HoveredObjects.Remove(m_HoverState.m_HoveredSiblingObject.transform.parent.gameObject);
                    foreach (var sibling in m_HoverState.m_SiblingObjects)
                    {
                        m_HoverState.m_HoveredObjects.Remove(sibling);
                    }
                }
                break;
            case SelectionState.HoverWithModifier:
                m_HoverState.m_HoveredChildObject = hoveredGO;
                m_HoverState.m_HoveredObjects?.Clear();
                break;
            case SelectionState.HoverInContext:
                m_HoverState.m_SiblingObjects = new List<GameObject>();
                Transform parent = hoveredGO.transform.parent;
                if (parent != null)
                {
                    for (int i = 0; i < parent.childCount; i++)
                    {
                        m_HoverState.m_SiblingObjects.Add(parent.GetChild(i).gameObject);
                    }
                }
                else
                {
                    for (int i = 0; i < hoveredGO.transform.childCount; i++)
                    {
                        m_HoverState.m_SiblingObjects.Add(hoveredGO.transform.GetChild(i).gameObject);
                    }
                }
                break;
            case SelectionState.SelectedItem:
                if (m_HoverState.m_SiblingObjects != null && m_HoverState.m_SiblingObjects.Contains(hoveredGO))
                {
                    Selection.activeGameObject = hoveredGO;
                }
                else if (m_HoverState.m_HoveredSiblingObject == null)
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
