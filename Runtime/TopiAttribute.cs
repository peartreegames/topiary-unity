using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;
using UnityEngine.Scripting;

namespace PeartreeGames.Topiary.Unity
{
    /// <summary>
    /// Represents an attribute that declares a method as an extern topi function.
    /// Can only be used on static methods with the signature
    /// <c>static TopiValue Foo(TopiValue[] args)</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class TopiAttribute : Attribute
    {
        public string Name { get; }
        public byte Arity { get; }

        public TopiAttribute(string name, byte arity)
        {
            Name = name;
            Arity = arity;
        }

        public struct FuncPtr
        {
            public string Name;
            public byte Arity;
            public IntPtr UserData;
        }

        private static readonly List<Func<TopiValue[], TopiValue>> Slots = new();
        private static Delegates.ExternFunctionDelegate _trampoline;
        public static IntPtr TrampolinePtr { get; private set; }

        [MonoPInvokeCallback(typeof(Delegates.ExternFunctionDelegate)), Preserve]
        private static TopiValue ExternTrampoline(IntPtr vmPtr, IntPtr userData, IntPtr argsPtr, byte count)
        {
            try
            {
                var slot = userData.ToInt32();
                if (slot < 0 || slot >= Slots.Count)
                {
                    Debug.LogError($"[Topiary] extern slot {slot} out of range (count {Slots.Count})");
                    return default;
                }
                var args = TopiValue.CreateArgs(argsPtr, count);
                return Slots[slot](args);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return default;
            }
        }

        public static List<FuncPtr> GetAllTopiMethodPtrs()
        {
            Slots.Clear();
            if (_trampoline == null)
            {
                _trampoline = ExternTrampoline;
                TrampolinePtr = Marshal.GetFunctionPointerForDelegate(_trampoline);
            }

            var topiAssembly = typeof(TopiAttribute).Assembly;
            var topiName = topiAssembly.GetName().Name;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a == topiAssembly || a.GetReferencedAssemblies().Any(r => r.Name == topiName));

            var methods = new List<FuncPtr>();
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    var tempMethods = type.GetMethods(BindingFlags.Static | BindingFlags.Public |
                                                      BindingFlags.NonPublic);

                    foreach (var method in tempMethods)
                    {
                        var attr = method.GetCustomAttribute<TopiAttribute>();
                        if (attr == null) continue;

                        if (method.ReturnType != typeof(TopiValue))
                        {
                            Debug.LogError(
                                $"[Topi] {type.FullName}.{method.Name} must return TopiValue.");
                            continue;
                        }
                        var ps = method.GetParameters();
                        if (ps.Length != 1 || ps[0].ParameterType != typeof(TopiValue[]))
                        {
                            Debug.LogError(
                                $"[Topi] {type.FullName}.{method.Name} must take a single TopiValue[] parameter.");
                            continue;
                        }

                        var fn = (Func<TopiValue[], TopiValue>)Delegate.CreateDelegate(
                            typeof(Func<TopiValue[], TopiValue>), method);
                        var slot = Slots.Count;
                        Slots.Add(fn);
                        methods.Add(new FuncPtr
                        {
                            Name = attr.Name,
                            Arity = attr.Arity,
                            UserData = (IntPtr)slot
                        });
                    }
                }
            }

            return methods;
        }
    }
}
