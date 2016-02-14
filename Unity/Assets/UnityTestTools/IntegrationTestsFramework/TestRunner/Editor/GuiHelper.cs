using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UnityTest
{
    public static class GuiHelper
    {
        public static bool GetConsoleErrorPause()
        {
            Assembly assembly = Assembly.GetAssembly(typeof(SceneView));
            Type type = assembly.GetType("UnityEditorInternal.LogEntries");
            PropertyInfo method = type.GetProperty("consoleFlags");
            var result = (int)method.GetValue(new object(), new object[] { });
            return (result & (1 << 2)) != 0;
        }

        public static void SetConsoleErrorPause(bool b)
        {
            Assembly assembly = Assembly.GetAssembly(typeof(SceneView));
            Type type = assembly.GetType("UnityEditorInternal.LogEntries");
            MethodInfo method = type.GetMethod("SetConsoleFlag");
            method.Invoke(new object(), new object[] { 1 << 2, b });
        }
    }
}
