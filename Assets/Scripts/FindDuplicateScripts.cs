using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class FindDuplicateScripts : EditorWindow
{
    [MenuItem("Tools/Find Duplicate Scripts")]
    public static void FindDuplicates()
    {
        int totalCount = 0;
        // หา GameObject ทั้งหมดใน Scene ปัจจุบัน
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (GameObject go in allObjects)
        {
            var components = go.GetComponents<Component>();
            var types = new HashSet<System.Type>();

            foreach (var comp in components)
            {
                if (comp == null) continue; // ข้าม Missing Script

                System.Type type = comp.GetType();

                // ถ้าเจอ Type ซ้ำใน GameObject เดียวกัน
                if (!types.Add(type))
                {
                    Debug.LogWarning($"[ซ้ำกัน!] GameObject: '{go.name}' มี Script '{type.Name}' มากกว่า 1 ตัว", go);
                    totalCount++;
                    break; // เจอแล้ว 1 ชนิด ข้ามไปตรวจชิ้นถัดไป
                }
            }
        }
        Debug.Log($"ค้นหาเสร็จสิ้น! พบ GameObject ที่มี Component ซ้ำกันทั้งหมด: {totalCount} ตัว");
    }
}
