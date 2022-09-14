using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
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
    
    private HoverState m_HoverState;
    private bool m_UsingModifier = false;
    private bool m_IsInContext = false;
    
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
            m_IsInContext = false;
        }

        if (m_HoverState.m_SibilingObjects != null && !m_HoverState.m_SibilingObjects.Contains(Selection.activeGameObject))
        {
            m_IsInContext = false;
        }
    }
    
    void GetHoveredGameObject(Event currentEvent, bool ignoreSelection = true)
    {
        if(currentEvent.type == EventType.MouseMove)
        {
            GameObject[] ignoredObjects = new GameObject[]{};
            var activeObj = Selection.activeGameObject;
            var isUsingModifier = IsUsingModifiers(currentEvent);

            if (activeObj && !m_IsInContext && !isUsingModifier && ignoreSelection)
            {
                ignoredObjects = activeObj.transform.root.GetComponentsInChildren<Transform>().Select(x => x.gameObject).ToArray();
            }
            
            var hoveredGO = HandleUtility.PickGameObject(currentEvent.mousePosition, true, ignoredObjects);
            if (hoveredGO == null)
            {
                m_HoverState.Clear();
                return;
            }

            if (isUsingModifier)
            {
                m_HoverState.m_HoveredChildObject = hoveredGO;
                m_HoverState.m_HoveredObjects.Clear();
                m_UsingModifier = true;
            }
            else
            {
                m_HoverState.m_HoveredParentObject = hoveredGO.transform.root.gameObject;
                m_HoverState.m_HoveredChildObject = null;
                m_HoverState.m_HoveredObjects = hoveredGO.transform.root.GetComponentsInChildren<Transform>().Select(x => x.gameObject).ToList();
                m_UsingModifier = false;
            }

            if (m_IsInContext && m_HoverState.m_SibilingObjects != null && m_HoverState.m_SibilingObjects.Contains(hoveredGO))
            {
                m_HoverState.m_HoveredSibilingObject = hoveredGO;
                m_HoverState.m_HoveredObjects.Clear();
            }
            else if (m_IsInContext)
            {
                m_HoverState.m_HoveredSibilingObject = null;
                m_HoverState.m_HoveredChildObject = null;
            }
        }
    }
    
    public void OnSceneGUISelectionHighlight(SceneView sceneView)
    {
        Handles.color = Color.green;
        
        var currentEvent = Event.current;
        if (currentEvent.type == EventType.Repaint)
        {
            if (m_HoverState.m_HoveredSibilingObject != null) 
                Handles.DrawOutline(new []{m_HoverState.m_HoveredSibilingObject}, Color.cyan, 0.0f);
            if (m_HoverState.m_HoveredChildObject != null)
                Handles.DrawOutline(new []{m_HoverState.m_HoveredChildObject}, Styles.s_ChildHoverColor, 0.0f);
            if(m_HoverState.m_HoveredObjects?.Count > 0) 
                Handles.DrawOutline(m_HoverState.m_HoveredObjects, Styles.s_RootHoverColor, 0.0f);
        }
        
        GetHoveredGameObject(currentEvent);

        if (currentEvent.clickCount == 2 && currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && currentEvent.isMouse &&
            (m_HoverState.m_HoveredParentObject || m_HoverState.m_HoveredChildObject) && GUIUtility.hotControl != 0)
        {
            HandleDoubleClick(currentEvent);
            return;
        }

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && currentEvent.isMouse && 
            (m_HoverState.m_HoveredParentObject || m_HoverState.m_HoveredChildObject) && GUIUtility.hotControl != 0)
        {
            if (m_UsingModifier)
            {
                m_IsInContext = true;
                m_HoverState.m_SibilingObjects = new List<GameObject>();
                Transform parent;
                int childCount;

                if (m_HoverState.m_HoveredChildObject != null)
                {
                    Selection.activeInstanceID = m_HoverState.m_HoveredChildObject.GetInstanceID();
                    parent = m_HoverState.m_HoveredChildObject.transform.parent;
                    childCount = parent.transform.childCount;
                }
                else
                {
                    parent = m_HoverState.m_HoveredParentObject.transform;
                    childCount = m_HoverState.m_HoveredParentObject.transform.childCount;
                }
                
                for (int i = 0; i < childCount; i++)
                {
                    m_HoverState.m_SibilingObjects.Add(parent.GetChild(i).gameObject);
                }
            }
            else
            {
                if (m_IsInContext)
                {
                    m_IsInContext = false;
                }
                else if (m_HoverState.m_HoveredParentObject)
                {
                    Selection.activeInstanceID = m_HoverState.m_HoveredParentObject.GetInstanceID();
                    int controlId = GUIUtility.GetControlID(FocusType.Passive);
                    GUIUtility.hotControl = controlId;
                    Event.current.Use();
                }
            }
        }
    }

    void HandleDoubleClick(Event currentEvent)
    {
        var hoveredGO = HandleUtility.PickGameObject(currentEvent.mousePosition, true);
        if (hoveredGO == null)
        {
            m_HoverState.Clear();
            m_IsInContext = false;
            return;
        }

        var parent = hoveredGO.transform.parent;
        if (parent != null)
        {
            m_IsInContext = true;
            m_HoverState.m_SibilingObjects = new List<GameObject>();

            for (int i = 0; i < parent.childCount; i++)
            {
                m_HoverState.m_SibilingObjects.Add(parent.GetChild(i).gameObject);
            }
        }
        
        Selection.activeInstanceID = hoveredGO.GetInstanceID();
        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        GUIUtility.hotControl = controlId;
        Event.current.Use();
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
