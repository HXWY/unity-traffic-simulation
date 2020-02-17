﻿// Traffic Simulation
// https://github.com/mchrbn/unity-traffic-simulation

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace TrafficSimulation{

    [CustomEditor(typeof(TrafficSystem))]
    public class TrafficEditor : Editor {

        private TrafficSystem wps;
        
        //References for moving a waypoint
        private Vector3 lastPoint;
        private Waypoint lastWaypoint;
        
        [MenuItem("Component/Traffic Simulation/Create Traffic Objects")]
        static void CreateTraffic(){
            //Create new Undo Group to collect all changes in one Undo
            Undo.SetCurrentGroupName("Create Traffic Objects");
            
            GameObject mainGo = CreateGameObjectWithUndo("Traffic System");
            mainGo.transform.position = Vector3.zero;
            AddComponentWithUndo<TrafficSystem>(mainGo);

            GameObject segmentsGo = CreateGameObjectWithUndo("Segments", mainGo.transform);
            segmentsGo.transform.position = Vector3.zero;

            GameObject intersectionsGo = CreateGameObjectWithUndo("Intersections", mainGo.transform);
            intersectionsGo.transform.position = Vector3.zero;
            
            //Close Undo Operation
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        }

        void OnEnable(){
            wps = target as TrafficSystem;
        }

        private void OnSceneGUI() {
            Event e = Event.current;
            if (e == null) return;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit) && e.type == EventType.MouseDown && e.button == 0) {
                //Add a new waypoint on mouseclick + shift
                if (e.shift) {
                    if (wps.curSegment == null) {
                        return;
                    }

                    //Create new Undo Group to collect all changes in one Undo
                    Undo.SetCurrentGroupName("Add Waypoint");

                    //Register all TrafficSystem changes after this (string not relevant here)
                    Undo.RegisterFullObjectHierarchyUndo(wps.gameObject, "Add Waypoint");

                    AddWaypoint(hit.point);

                    //Close Undo Operation
                    Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
                }

                //Create a segment + add a new waypoint on mouseclick + ctrl
                else if (e.control) {
                    //Create new Undo Group to collect all changes in one Undo
                    Undo.SetCurrentGroupName("Add Segment");

                    //Register all TrafficSystem changes after this (string not relevant here)
                    Undo.RegisterFullObjectHierarchyUndo(wps.gameObject, "Add Segment");

                    AddSegment(hit.point);
                    AddWaypoint(hit.point);

                    //Close Undo Operation
                    Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
                }

                //Create an intersection type
                else if (e.alt) {
                    //Create new Undo Group to collect all changes in one Undo
                    Undo.SetCurrentGroupName("Add Intersection");

                    //Register all TrafficSystem changes after this (string not relevant here)
                    Undo.RegisterFullObjectHierarchyUndo(wps.gameObject, "Add Intersection");

                    AddIntersection(hit.point);

                    //Close Undo Operation
                    Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
                }
            }

            //Set waypoint system as the selected gameobject in hierarchy
            Selection.activeGameObject = wps.gameObject;

            bool moved = false;

            //Handle the selected waypoint
            if (lastWaypoint != null) {
                //Uses a endless plain for the ray to hit
                Plane plane = new Plane(Vector3.up.normalized, lastWaypoint.transform.position);
                plane.Raycast(ray, out float dst);
                Vector3 hitPoint = ray.GetPoint(dst);

                //Reset lastPoint if the mouse button is pressed down the first time
                if (e.type == EventType.MouseDown && e.button == 0) {
                    lastPoint = hitPoint;
                }

                //Move the selected waypoint
                if (e.type == EventType.MouseDrag && e.button == 0) {
                    Vector3 realDPos = new Vector3(hitPoint.x - lastPoint.x, 0, hitPoint.z - lastPoint.z);
                    moved = true;

                    lastWaypoint.transform.position += realDPos;
                    lastPoint = hitPoint;
                }

                //Draw a Sphere
                Handles.SphereHandleCap(0, lastWaypoint.transform.position, Quaternion.identity, 1, EventType.Repaint);
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                SceneView.RepaintAll();
            }

            //Look if the users mouse is over a waypoint
            List<RaycastHit> hits = Physics.RaycastAll(ray, float.MaxValue, LayerMask.GetMask("UnityEditor")).ToList();

            //Set the current hovering waypoint
            if (lastWaypoint == null && hits.Exists(i => i.collider.CompareTag("Waypoint"))) {
                lastWaypoint = hits.First(i => i.collider.CompareTag("Waypoint")).collider.GetComponent<Waypoint>();
            } 
            
            //Only reset if the current waypoint was not used
            else if (e.type == EventType.MouseMove && !moved) {
                lastWaypoint = null;
            }

            //Tell Unity that something changed and the scene has to be saved
            if (moved && !EditorUtility.IsDirty(target)) {
                EditorUtility.SetDirty(target);
            }
        }

        public override void OnInspectorGUI(){

            //Editor properties
            EditorGUILayout.LabelField("Guizmo Config", EditorStyles.boldLabel);
            wps.hideGuizmos = EditorGUILayout.Toggle("Hide Guizmos", wps.hideGuizmos);
            EditorGUILayout.LabelField("System Config", EditorStyles.boldLabel);
            wps.segDetectThresh = EditorGUILayout.FloatField("Segment Detection Threshold", wps.segDetectThresh);
            EditorGUILayout.HelpBox("Ctrl + Left Click to create a new segment\nShift + Left Click to create a new waypoint.\nAlt + Left Click to create a new intersection", MessageType.Info);
            EditorGUILayout.HelpBox("Reminder: The cars will follow the point depending on the sequence you added them. (go to the 1st waypoint added, then to the second, etc.)", MessageType.Info);


            //Rename waypoints if some have been deleted
            if(GUILayout.Button("Re-Structure Traffic System")){
                RestructureSystem();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void AddWaypoint(Vector3 position) {
            GameObject go = CreateGameObjectWithUndo("Waypoint-" + wps.curSegment.waypoints.Count, wps.curSegment.transform);
            go.transform.position = position;

            Waypoint wp = AddComponentWithUndo<Waypoint>(go);
            AddComponentWithUndo<SphereCollider>(go);

            wp.Refresh(wps.curSegment.waypoints.Count, wps.curSegment);

            //Record changes to the TrafficSystem (string not relevant here)
            Undo.RecordObject(wps.curSegment, "");
            wps.curSegment.waypoints.Add(wp);
        }

        private void AddSegment(Vector3 position) {
            int segId = wps.segments.Count;
            GameObject segGo = CreateGameObjectWithUndo("Segment-" + segId, wps.transform.GetChild(0).transform);
            segGo.transform.position = position;

            wps.curSegment = AddComponentWithUndo<Segment>(segGo);
            wps.curSegment.id = segId;
            wps.curSegment.waypoints = new List<Waypoint>();
            wps.curSegment.nextSegments = new List<Segment>();

            //Record changes to the TrafficSystem (string not relevant here)
            Undo.RecordObject(wps, "");
            wps.segments.Add(wps.curSegment);
        }

        private void AddIntersection(Vector3 position) {
            int intId = wps.intersections.Count;
            GameObject intGo = CreateGameObjectWithUndo("Intersection-" + intId, wps.transform.GetChild(1).transform);
            intGo.transform.position = position;

            BoxCollider bc = AddComponentWithUndo<BoxCollider>(intGo);
            bc.isTrigger = true;
            Intersection intersection = AddComponentWithUndo<Intersection>(intGo);
            intersection.id = intId;

            //Record changes to the TrafficSystem (string not relevant here)
            Undo.RecordObject(wps, "");
            wps.intersections.Add(intersection);
        }

        void RestructureSystem(){

            //Rename and restructure segments and waypoitns
            List<Segment> nSegments = new List<Segment>();
            int itSeg = 0;
            foreach(Segment segment in wps.segments){
                if(segment != null){
                    List<Waypoint> nWaypoints = new List<Waypoint>();
                    segment.id = itSeg;
                    segment.gameObject.name = "Segment-" + itSeg;
                    
                    int itWp = 0;
                    foreach(Waypoint waypoint in segment.waypoints){
                        if(waypoint != null) {
                            waypoint.Refresh(itWp, segment);
                            nWaypoints.Add(waypoint);
                            itWp++;
                        }
                    }

                    segment.waypoints = nWaypoints;
                    nSegments.Add(segment);
                    itSeg++;
                }
            }

            //Check if next segments still exist
            foreach(Segment segment in nSegments){
                List<Segment> nNextSegments = new List<Segment>();
                foreach(Segment nextSeg in segment.nextSegments){
                    if(nextSeg != null){
                        nNextSegments.Add(nextSeg);
                    }
                }
                segment.nextSegments = nNextSegments;
            }
            wps.segments = nSegments;

            //Check intersections
            List<Intersection> nIntersections = new List<Intersection>();
            int itInter = 0;
            foreach(Intersection intersection in wps.intersections){
                if(intersection != null){
                    intersection.id = itInter;
                    intersection.gameObject.name = "Intersection-" + itInter;
                    nIntersections.Add(intersection);
                    itInter++;
                }
            }
            wps.intersections = nIntersections;
            
            //Tell Unity that something changed and the scene has to be saved
            if (!EditorUtility.IsDirty(target)) {
                EditorUtility.SetDirty(target);
            }

            Debug.Log("[Traffic Simulation] Successfully rebuilt the traffic system.");
        }

        private static GameObject CreateGameObjectWithUndo(string name, Transform parent = null) {
            GameObject newGameObject = new GameObject(name);

            //Register changes (string not relevant here)
            Undo.RegisterCreatedObjectUndo(newGameObject, "Spawn new GameObject");
            Undo.SetTransformParent(newGameObject.transform, parent, "Set parent");

            return newGameObject;
        }

        private static T AddComponentWithUndo<T>(GameObject target) where T : Component {
            return Undo.AddComponent<T>(target);
        }
    }
}
