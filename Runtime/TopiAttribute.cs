using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AOT;
using UnityEngine;

namespace PeartreeGames.Topiary.Unity
{
    /// <summary>
    /// Represents an attribute that declares a method as an extern topi function.
    /// Can only be used on static methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class TopiAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the function in the topi file.
        /// </summary>
        public string Name { get; }

        public byte Arity { get; }

        /// <summary>
        /// Declare the function as an extern topi function
        /// Can only be used on static methods
        /// </summary>
        /// <param name="name">Name of the function in the topi file</param>
        /// <param name="arity">The number of arguments this function expects</param>
        public TopiAttribute(string name, byte arity)
        {
            Name = name;
            Arity = arity;
        }

        public struct FuncPtr
        {
            public string Name;
            public byte Arity;
            public IntPtr Ptr;
        }
    
        public static List<FuncPtr> GetAllTopiMethodPtrs()
        {
            var assemblyRegex = new Regex("^(System|Microsoft|mscorlib)", RegexOptions.Compiled);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !assemblyRegex.IsMatch(a.FullName));

            var methods = new List<FuncPtr>();
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    var tempMethods = type.GetMethods(BindingFlags.Static | BindingFlags.Public |
                                                      BindingFlags.NonPublic);

                    foreach (var method in tempMethods)
                    {
                        var attr = method.GetCustomAttribute<TopiAttribute>();
                        if (attr == null) continue;
                        if (method.GetCustomAttribute<MonoPInvokeCallbackAttribute>() == null)
                        {
                            Debug.LogError(
                                $"Method {method.Name} in {type.FullName} is missing the MonoPInvokeCallback attribute.");
                            continue;
                        }

                        var del =
                            (Delegates.ExternFunctionDelegate)Delegate.CreateDelegate(
                                typeof(Delegates.ExternFunctionDelegate), method);
                        methods.Add(new FuncPtr{ Name = attr.Name, Arity = attr.Arity, Ptr = Marshal.GetFunctionPointerForDelegate(del) });
                    }
                }
            }

            return methods;
        }
    }
}