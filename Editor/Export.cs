using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;


namespace DynamicBoneJson
{
    static public class Export
    {
        [MenuItem("DynamicBoneJson/Export")]
        static void DoIt()
        {
            var result = ExportJson();
            if (result.success == false)
            {
                EditorUtility.DisplayDialog("Eoorr", result.message, "OK");
            }
        }

        static float[] ToArray(this Vector3 v)
        {
            return new float[] { v.x, v.y, v.z };
        }

        static Dictionary<string, object> DynamicBoneToJson(DynamicBone db)
        {
            var dst = new Dictionary<string, object>();

            dst["Root"] = db.m_Root == null ? null : db.m_Root.name;

            if (db.m_UpdateRate != 60) dst["UpdateRate"] = db.m_UpdateRate;
            if (db.m_UpdateMode != DynamicBone.UpdateMode.Normal) dst["UpdateMode"] = db.m_UpdateMode.ToString();

            if (db.m_Damping != 0.1f) dst["Damping"] = db.m_Damping;
            if (db.m_DampingDistrib.keys.Length != 0) dst["DampingDistrib"] = db.m_DampingDistrib.keys;

            if (db.m_Elasticity != 0.1f) dst["Elasticity"] = db.m_Elasticity;
            if (db.m_ElasticityDistrib.keys.Length != 0) dst["ElasticityDistrib"] = db.m_ElasticityDistrib.keys;

            if (db.m_Stiffness != 0.1f) dst["Stiffness"] = db.m_Stiffness;
            if (db.m_StiffnessDistrib.keys.Length != 0) dst["StiffnessDistrib"] = db.m_StiffnessDistrib.keys;

            if (db.m_Inert != 0.0f) dst["Inert"] = db.m_Inert;
            if (db.m_InertDistrib.keys.Length != 0) dst["InertDistrib"] = db.m_InertDistrib.keys;

            dst["Radius"] = db.m_Radius;
            if (db.m_RadiusDistrib.keys.Length != 0) dst["RadiusDistrib"] = db.m_RadiusDistrib.keys;

            if (db.m_EndLength != 0.0f) dst["EndLength"] = db.m_EndLength;
            if (db.m_EndOffset != Vector3.zero) dst["EndOffset"] = db.m_EndOffset.ToArray();
            if (db.m_Gravity != Vector3.zero) dst["Gravity"] = db.m_Gravity.ToArray();
            if (db.m_Force != Vector3.zero) dst["Force"] = db.m_Force.ToArray();

            dst["Colliders"] = db.m_Colliders.Select(t => t == null ? null : t.name);
            if (db.m_Exclusions.Count != 0) dst["Exclusions"] = db.m_Exclusions.Select(t => t == null ? null : t.name);

            if (db.m_FreezeAxis != DynamicBone.FreezeAxis.None) dst["FreezeAxis"] = db.m_FreezeAxis.ToString();
            if (db.m_DistantDisable != false) dst["DistantDisable"] = db.m_DistantDisable;
            if (db.m_ReferenceObject != null) dst["ReferenceObject"] = db.m_ReferenceObject.name;
            if (db.m_DistanceToObject != 20.0f) dst["DistanceToObject"] = db.m_DistanceToObject;

            return dst;
        }

        static Dictionary<string, ArrayList> CreateBonesDict(Transform root)
        {
            var dst = new Dictionary<string, ArrayList>();

            foreach (var db in root.GetComponentsInChildren<DynamicBone>())
            {
                if (dst.ContainsKey(db.name) == false)
                {
                    dst[db.name] = new ArrayList();
                }

                var dbDict = DynamicBoneToJson(db);
                dst[db.name].Add(dbDict);
            }

            return dst;
        }

        static Dictionary<string, object> ColliderToJson(DynamicBoneColliderBase cb)
        {
            var dst = new Dictionary<string, object>();

            dst["Type"] = cb.GetType().ToString();

            dst["Direction"] = cb.m_Direction.ToString();
            dst["Center"] = cb.m_Center.ToArray();
            dst["Bound"] = cb.m_Bound;

            var c = cb as DynamicBoneCollider;
            if (c != null)
            {
                dst["Radius"] = c.m_Radius;
                dst["Height"] = c.m_Height;
            }

            return dst;
        }

        static Dictionary<string, object> CreateCollidersDict(Transform root)
        {
            var dst = new Dictionary<string, object>();

            foreach (var cb in root.GetComponentsInChildren<DynamicBoneColliderBase>())
            {
                dst[cb.name] = ColliderToJson(cb);
            }

            return dst;
        }

        static Dictionary<string, object> CreateDict(Transform root)
        {
            var rootDict = new Dictionary<string, object>
            {
                ["ObjectName"] = root.name,
                ["DynamicBone"] = CreateBonesDict(root),
                ["Collider"] = CreateCollidersDict(root),
            };

            return rootDict;
        }

        static string DeleteIndent(Match m)
        {
            var sr = new StringReader(m.Value);
            string dst = sr.ReadLine();

            string line;
            while ((line = sr.ReadLine()) != null)
            {
                dst += ' ' + line.TrimStart();
            }

            dst += Environment.NewLine;

            return dst;
        }

        static void WriteText(string json, string rootName)
        {
            json = json.Replace("{}\r\n", "{\r\n}\r\n")
                       .Replace("{},\r\n", "{\r\n},\r\n")
                       .Replace("[]\r\n", "[\r\n]\r\n")
                       .Replace("[],\r\n", "[\r\n],\r\n");

            string pattern1 = @"\{\r\n((?![\[\{\]\}]\r\n).)*(\}\r\n|\},\r\n)";
            string pattern2 = @"\[\r\n((?![\[\{\]\}]\r\n).)*(\]\r\n|\],\r\n)";
            string pattern = $"({pattern1})|({pattern2})";

            var reg = new Regex(pattern, RegexOptions.Singleline);
            json = reg.Replace(json, DeleteIndent);

            string path = EditorUtility.SaveFilePanel("Export Json", "", rootName, "json");
            using (var sw = new StreamWriter(path))
            {
                sw.Write(json);
            }
        }

        static (bool success, string message) ExportJson()
        {
            var root = Selection.activeTransform;
            if (root == null) return (false, "Select root hierarchy.");

            var rootDict = CreateDict(root);
            string json = JsonConvert.SerializeObject(rootDict, Formatting.Indented);
            WriteText(json, root.name);

            return (true, null);
        }
    }
}
