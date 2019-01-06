using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DynamicBoneJson
{
    public class Import
    {
        [MenuItem("DynamicBoneJson/Import")]
        static void DoIt()
        {
            var ip = new ImportProcess();
            var result = ip.process();
            if (result.success == false)
            {
                EditorUtility.DisplayDialog("Eoorr", result.message, "OK");
            }
        }
    }

    class ImportProcess
    {
        Transforms transforms;

        public (bool success, string message) process()
        {
            var root = Selection.activeTransform;
            if (root == null) return (false, "Select root hierarchy.");

            string message = CollectHierarchies(root);
            if (message != null) return (false, message);

            bool yes = DeletePreComponents(root);
            if (yes == false) return (true, null);

            string path = EditorUtility.OpenFilePanel("Import JSON", null, "json");
            var sr = new StreamReader(path);
            string json = sr.ReadToEnd();
            var jo = JObject.Parse(json);

            DeserializeColliders((JObject)jo["Collider"]);
            DeserializeDynamicBones((JObject)jo["DynamicBone"]);

            return (true, null);
        }

        static void ParseEnum<T>(out T dst, JToken value)
        {
            dst = (T)Enum.Parse(typeof(T), (string)value);
        }

        static AnimationCurve deserializeAnimationCurve(JToken array)
        {
            var keys = JsonConvert.DeserializeObject<Keyframe[]>(array.ToString());
            return new AnimationCurve(keys);
        }

        static Vector3 deserializeVector3(JToken array)
        {
            return new Vector3((float)array[0], (float)array[1], (float)array[2]);
        }

        List<DynamicBoneColliderBase> collectColliders(JToken namesArray)
        {
            return namesArray.Select(name =>
            {
                var t = transforms.get(name);
                return t == null ? null : t.GetComponent<DynamicBoneColliderBase>();
            }).ToList();
        }

        void DeserializeDynamicBone(Transform target, JObject component)
        {
            var db = target.gameObject.AddComponent<DynamicBone>();

            foreach (var prop in component)
            {
                switch (prop.Key)
                {
                    case "Root":
                        db.m_Root = transforms.get(prop.Value);
                        break;
                    case "UpdateRate":
                        db.m_UpdateRate = (float)prop.Value;
                        break;
                    case "UpdateMode":
                        ParseEnum(out db.m_UpdateMode, prop.Value);
                        break;
                    case "Damping":
                        db.m_Damping = (float)prop.Value;
                        break;
                    case "DampingDistrib":
                        db.m_DampingDistrib = deserializeAnimationCurve(prop.Value);
                        break;
                    case "Elasticity":
                        db.m_Elasticity = (float)prop.Value;
                        break;
                    case "ElasticityDistrib":
                        db.m_ElasticityDistrib = deserializeAnimationCurve(prop.Value);
                        break;
                    case "Stiffness":
                        db.m_Stiffness = (float)prop.Value;
                        break;
                    case "StiffnessDistrib":
                        db.m_StiffnessDistrib = deserializeAnimationCurve(prop.Value);
                        break;
                    case "Inert":
                        db.m_Inert = (float)prop.Value;
                        break;
                    case "InertDistrib":
                        db.m_InertDistrib = deserializeAnimationCurve(prop.Value);
                        break;
                    case "Radius":
                        db.m_Radius = (float)prop.Value;
                        break;
                    case "RadiusDistrib":
                        db.m_RadiusDistrib = deserializeAnimationCurve(prop.Value);
                        break;
                    case "EndLength":
                        db.m_EndLength = (float)prop.Value;
                        break;
                    case "EndOffset":
                        db.m_EndOffset = deserializeVector3(prop.Value);
                        break;
                    case "Gravity":
                        db.m_Gravity = deserializeVector3(prop.Value);
                        break;
                    case "Force":
                        db.m_Force = deserializeVector3(prop.Value);
                        break;
                    case "Colliders":
                        db.m_Colliders = collectColliders(prop.Value);
                        break;
                    case "Exclusions":
                        db.m_Exclusions = prop.Value.Select(name => transforms.get(name)).ToList();
                        break;
                    case "FreezeAxis":
                        ParseEnum(out db.m_FreezeAxis, prop.Value);
                        break;
                    case "DistantDisable":
                        db.m_DistantDisable = (bool)prop.Value;
                        break;
                    case "ReferenceObject":
                        db.m_ReferenceObject = transforms.get(prop.Value);
                        break;
                    case "DistanceToObject":
                        db.m_DistanceToObject = (float)prop.Value;
                        break;
                    default:
                        break;
                }
            }
        }

        void DeserializeDynamicBones(JObject componentsObject)
        {
            foreach (var components in componentsObject)
            {
                var t = transforms.get(components.Key);
                if (t == null) continue;

                foreach (var c in components.Value)
                {
                    DeserializeDynamicBone(t, (JObject)c);
                }
            }
        }

        void DeserializeCollider(JObject props, Transform t)
        {
            Type type = Type.GetType((string)props["Type"] + ",Assembly-CSharp");
            var cb = (DynamicBoneColliderBase)t.gameObject.AddComponent(type);

            ParseEnum(out cb.m_Direction, props["Direction"]);
            cb.m_Center = deserializeVector3(props["Center"]);
            ParseEnum(out cb.m_Bound, props["Bound"]);

            var c = cb as DynamicBoneCollider;
            if (c != null)
            {
                c.m_Radius = (float)props["Radius"];
                c.m_Height = (float)props["Height"];
            }
        }

        void DeserializeColliders(JObject colliders)
        {
            foreach (var c in colliders)
            {
                var t = transforms.get(c.Key);
                if (t != null) DeserializeCollider((JObject)c.Value, t);
            }
        }

        static bool DeletePreComponents(Transform root)
        {
            var dynamicBones = root.GetComponentsInChildren<DynamicBone>().Select(db => (MonoBehaviour)db);
            var colliders = root.GetComponentsInChildren<DynamicBoneColliderBase>().Select(cb => (MonoBehaviour)cb);
            var preComponents = dynamicBones.Concat(colliders);

            if (preComponents.Count() == 0) return true;

            string message = $@"Dynamic Bone or Dynamic Bone Collider components already exist in {root.name} or its children.
Are you sure to you want to delete these components?";
            bool yes = EditorUtility.DisplayDialog(null, message, "Yes", "No");

            if (yes == false) return false;

            foreach (var pc in preComponents)
            {
                UnityEngine.Object.DestroyImmediate(pc);
            }

            return true;
        }

        string CollectHierarchies(Transform root)
        {
            transforms = new Transforms(root);
            if (transforms.duplications.Count > 0)
            {
                return "Same name hierarchies:\r\n\r\n" + string.Join("\r\n", transforms.duplications);
            }

            return null;
        }
    }

    class Transforms
    {
        public Dictionary<string, Transform> transforms { get; } = new Dictionary<string, Transform>();
        public SortedSet<string> duplications = new SortedSet<string>();

        public Transforms(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>())
            {
                if (transforms.ContainsKey(t.name)) duplications.Add(t.name);
                transforms[t.name] = t;
            }
        }

        public Transform get(JToken name)
        {
            string sName = (string)name;
            if (sName == null) return null;
            return transforms.ContainsKey(sName) ? transforms[sName] : null;
        }
    }
}
